using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AM;
using AM.Idle;
using AM.Patches;
using HarmonyLib;
using Verse;

// 近战动画
namespace NivarianIcecreamTail.MeleeAnimationCompatibility
{
    [StaticConstructorOnStartup]
    internal static class MeleeAnimationCompatibilityBootstrap
    {
        private static readonly FieldInfo WasInterruptedField =
            AccessTools.Field(typeof(AnimRenderer), "<WasInterrupted>k__BackingField");

        static MeleeAnimationCompatibilityBootstrap()
        {
            Skill3AnimationBridge.Register(new MeleeAnimationSkill3Provider());
            BakkaiAnimationBridge.Register(new MeleeAnimationBakkaiProvider());
            new Harmony("dkdcakedangao.NivarianIcecreamTailQWQ.MeleeAnimation")
                .PatchAll(Assembly.GetExecutingAssembly());
            AnimRenderer.PrePawnSpecialRender += ApplyIndependentHeadDirection;
        }

        internal static bool IsSkill3Renderer(AnimRenderer renderer)
        {
            return renderer != null && renderer.Def != null &&
                   renderer.Def.defName == MeleeAnimationSkill3Provider.AnimationDefName;
        }

        internal static void ClearPawnAnimations(Pawn pawn)
        {
            if (pawn == null) return;

            IdleControllerComp idleController = pawn.GetComp<IdleControllerComp>();
            if (idleController != null)
            {
                InterruptRenderer(idleController.CurrentAnimation);
                idleController.ClearAnimation();
            }

            InterruptRenderer(AnimRenderer.TryGetAnimator(pawn));
        }

        internal static void InterruptRenderer(AnimRenderer renderer)
        {
            if (renderer == null || renderer.IsDestroyed) return;
            if (WasInterruptedField == null)
            {
                throw new MissingFieldException(typeof(AnimRenderer).FullName, "<WasInterrupted>k__BackingField");
            }

            WasInterruptedField.SetValue(renderer, true);
            renderer.Destroy();
        }

        private static void ApplyIndependentHeadDirection(Pawn pawn, AnimRenderer renderer, Map map)
        {
            if (!IsSkill3Renderer(renderer) ||
                Patch_PawnRenderer_RenderPawnAt.NextDrawMode != Patch_PawnRenderer_RenderPawnAt.DrawMode.HeadOnly)
            {
                return;
            }

            AnimPartData head = renderer.GetPawnHead(pawn);
            if (head == null) return;

            AnimPartSnapshot snapshot = head.GetSnapshot(renderer);
            Patch_PawnRenderer_RenderPawnAt.HeadRotation = snapshot.GetWorldDirection();
        }
    }

    [HarmonyPatch]
    internal static class Skill3BlockCompetingAnimationsPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AnimationStartParameters), "TryTrigger",
                new Type[] { typeof(AnimRenderer).MakeByRefType() });
        }

        private static bool Prefix(ref AnimationStartParameters __instance, ref AnimRenderer animation, ref bool __result)
        {
            bool isSkill3Animation = __instance.Animation != null &&
                __instance.Animation.defName == MeleeAnimationSkill3Provider.AnimationDefName;

            foreach (Pawn pawn in __instance.EnumeratePawns())
            {
                if (BakkaiAnimationBridge.IsPawnActive(pawn) ||
                    (Skill3AnimationBridge.IsPawnProtected(pawn) && !isSkill3Animation))
                {
                    animation = null;
                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(IdleControllerComp), "CompTick")]
    internal static class Skill3SuppressIdleAnimationsPatch
    {
        private static bool Prefix(IdleControllerComp __instance)
        {
            Pawn pawn = __instance == null ? null : __instance.parent as Pawn;
            return !Skill3AnimationBridge.IsPawnProtected(pawn) && !BakkaiAnimationBridge.IsPawnActive(pawn);
        }
    }

    [HarmonyPatch(typeof(IdleControllerComp), "NotifyPawnDidMeleeAttack")]
    internal static class Skill3SuppressMeleeAnimationNotificationPatch
    {
        private static bool Prefix(IdleControllerComp __instance)
        {
            Pawn pawn = __instance == null ? null : __instance.parent as Pawn;
            return !Skill3AnimationBridge.IsPawnProtected(pawn) && !BakkaiAnimationBridge.IsPawnActive(pawn);
        }
    }

    internal sealed class MeleeAnimationBakkaiProvider : IBakkaiAnimationProvider
    {
        public void Prepare(Pawn pawn)
        {
            MeleeAnimationCompatibilityBootstrap.ClearPawnAnimations(pawn);
        }

        public void Cancel(Pawn pawn)
        {
        }
    }

    [HarmonyPatch]
    internal static class Skill3IndependentHeadPassPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AnimRenderer), "IsPawnBeheaded");
        }

        private static void Postfix(object[] __args, ref bool __result)
        {
            if (__result || __args == null || __args.Length < 3) return;

            Pawn pawn = __args[0] as Pawn;
            AnimRenderer renderer = pawn == null ? null : AnimRenderer.TryGetAnimator(pawn);
            if (!MeleeAnimationCompatibilityBootstrap.IsSkill3Renderer(renderer)) return;

            AnimPartSnapshot body = (AnimPartSnapshot)__args[1];
            AnimPartSnapshot head = (AnimPartSnapshot)__args[2];
            if (body.Valid && head.Valid && body.GetWorldDirection().AsInt != head.GetWorldDirection().AsInt)
            {
                __result = true;
            }
        }
    }

    internal sealed class MeleeAnimationSkill3Provider : ISkill3AnimationProvider
    {
        internal const string AnimationDefName = "IcecreamTail_Skill3_MeleeAnimation";
        private readonly Dictionary<Pawn, AnimRenderer> running = new Dictionary<Pawn, AnimRenderer>();
        private readonly Dictionary<Pawn, Skill3AnimationStatus> finished = new Dictionary<Pawn, Skill3AnimationStatus>();

        public bool Prepare(Pawn caster, Pawn target)
        {
            MeleeAnimationCompatibilityBootstrap.ClearPawnAnimations(caster);
            MeleeAnimationCompatibilityBootstrap.ClearPawnAnimations(target);
            return true;
        }

        public bool TryStart(Pawn caster, Pawn target)
        {
            AnimDef def = DefDatabase<AnimDef>.GetNamedSilentFail(AnimationDefName);
            string path = def == null ? null : def.FullDataPath;
            if (def == null || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            AnimationStartParameters parameters = new AnimationStartParameters(def, caster, target)
            {
                FlipX = target.Position.x < caster.Position.x,
                ExecutionOutcome = ExecutionOutcome.Nothing
            };
            AnimRenderer renderer;
            if (!parameters.TryTrigger(out renderer) || renderer == null)
            {
                return false;
            }

            finished.Remove(caster);
            running[caster] = renderer;
            renderer.OnEndAction += OnAnimationEnded;
            return true;
        }

        public Skill3AnimationStatus GetStatus(Pawn caster, Pawn target)
        {
            Skill3AnimationStatus status;
            if (finished.TryGetValue(caster, out status))
            {
                return status;
            }

            AnimRenderer renderer;
            if (running.TryGetValue(caster, out renderer))
            {
                return Skill3AnimationStatus.Running;
            }

            renderer = AnimRenderer.TryGetAnimator(caster);
            if (renderer != null && renderer.Def != null && renderer.Def.defName == AnimationDefName)
            {
                running[caster] = renderer;
                renderer.OnEndAction += OnAnimationEnded;
                return Skill3AnimationStatus.Running;
            }

            return Skill3AnimationStatus.Interrupted;
        }

        public void Cancel(Pawn caster, Pawn target)
        {
            AnimRenderer renderer;
            if (caster != null && running.TryGetValue(caster, out renderer) && renderer != null && !renderer.IsDestroyed)
            {
                MeleeAnimationCompatibilityBootstrap.InterruptRenderer(renderer);
            }
            running.Remove(caster);
            finished.Remove(caster);
        }

        private void OnAnimationEnded(AnimRenderer renderer)
        {
            Pawn caster = renderer == null || renderer.Pawns.Count == 0 ? null : renderer.Pawns[0];
            if (caster == null) return;
            running.Remove(caster);
            finished[caster] = renderer.WasInterrupted
                ? Skill3AnimationStatus.Interrupted
                : Skill3AnimationStatus.Completed;
        }
    }
}
