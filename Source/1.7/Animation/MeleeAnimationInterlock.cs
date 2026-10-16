using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

// 在动画播放期间，阻止MA动画播放，属于是除错的
// 同时，也不会播放MA的手部、武器，以及原版的武器
namespace NivarianIcecreamTail
{
    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleeStart
    {
        private static Type parameters, renderer;
        private static bool Prepare()
        {
            parameters = AccessTools.TypeByName("AM.AnimationStartParameters");
            renderer = AccessTools.TypeByName("AM.AnimRenderer");
            return parameters != null && renderer != null;
        }
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(parameters, "TryTrigger", new[] { renderer.MakeByRefType() });
        }
        internal static bool Blocks(Pawn main, Pawn second, List<Pawn> extra)
        {
            if (IcecreamTailSkill3Runtime.IsProtected(main) || IcecreamTailSkill3Runtime.IsProtected(second)) return true;
            if (extra != null)
                for (int i = 0; i < extra.Count; i++) if (IcecreamTailSkill3Runtime.IsProtected(extra[i])) return true;
            return false;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var original = new List<CodeInstruction>(instructions);
            Label proceed = generator.DefineLabel();
            original[0].labels.Add(proceed);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(IcecreamTailSkill3Runtime), "HasNativeProtection"));
            yield return new CodeInstruction(OpCodes.Brfalse, proceed);
            foreach (string field in new[] { "MainPawn", "SecondPawn", "ExtraPawns" })
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(parameters, field));
            }
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_CharacterAnimation_MeleeStart), "Blocks"));
            yield return new CodeInstruction(OpCodes.Brfalse, proceed);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldnull);
            yield return new CodeInstruction(OpCodes.Stind_Ref);
            yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            yield return new CodeInstruction(OpCodes.Ret);
            foreach (var instruction in original) yield return instruction;
        }
    }

    internal static class MeleeIdleDrawSuppression
    {
        private static Type idleType;
        private static Func<object, object> currentAnimation;
        private static readonly Dictionary<object, PawnAnimationActor> Owners = new Dictionary<object, PawnAnimationActor>(ReferenceEqualityComparer.Instance);

        internal static void Initialize(Type type)
        {
            idleType = type;
            var instance = Expression.Parameter(typeof(object));
            currentAnimation = Expression.Lambda<Func<object, object>>(
                Expression.Convert(Expression.Property(Expression.Convert(instance, type), "CurrentAnimation"), typeof(object)), instance).Compile();
        }

        internal static void Bind(PawnAnimationActor actor)
        {
            if (idleType == null) return;
            var comps = actor.Pawn.AllComps;
            if (comps == null) return;
            for (int i = 0; i < comps.Count; i++)
                if (idleType.IsInstanceOfType(comps[i])) { Track(actor, currentAnimation(comps[i])); return; }
        }

        internal static void Refresh(ThingComp comp)
        {
            PawnAnimationActor actor;
            if (PawnAnimationRendering.TryGet(comp.parent as Pawn, out actor)) Track(actor, currentAnimation(comp));
        }

        private static void Track(PawnAnimationActor actor, object renderer)
        {
            if (ReferenceEquals(actor.MeleeIdleRenderer, renderer)) return;
            Unbind(actor);
            if (renderer == null) return;
            Owners.Add(renderer, actor);
            actor.MeleeIdleRenderer = renderer;
        }

        internal static void Unbind(PawnAnimationActor actor)
        {
            PawnAnimationActor owner;
            if (actor.MeleeIdleRenderer != null && Owners.TryGetValue(actor.MeleeIdleRenderer, out owner) && ReferenceEquals(owner, actor))
                Owners.Remove(actor.MeleeIdleRenderer);
            actor.MeleeIdleRenderer = null;
        }

        internal static bool Contains(object renderer) { return Owners.ContainsKey(renderer); }
    }

    // MA补丁
    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleeDraw
    {
        private static Type renderer;
        private static Func<object, List<Pawn>> pawns;
        private static bool Prepare()
        {
            renderer = AccessTools.TypeByName("AM.AnimRenderer");
            if (renderer == null) return false;
            var instance = Expression.Parameter(typeof(object));
            pawns = Expression.Lambda<Func<object, List<Pawn>>>(Expression.Field(Expression.Convert(instance, renderer), "Pawns"), instance).Compile();
            return true;
        }
        private static MethodBase TargetMethod() { return AccessTools.Method(renderer, "Draw"); }
        private static bool Prefix(object __instance)
        {
            if (!PawnAnimationRendering.Any) return true;
            if (MeleeIdleDrawSuppression.Contains(__instance)) return false;
            var actors = pawns(__instance);
            for (int i = 0; i < actors.Count; i++)
                if (PawnAnimationRendering.IsBound(actors[i])) return false;
            return true;
        }
    }

    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleeIdleStart
    {
        private static Type idle;
        private static bool Prepare()
        {
            idle = AccessTools.TypeByName("AM.Idle.IdleControllerComp");
            if (idle == null) return false;
            MeleeIdleDrawSuppression.Initialize(idle);
            return true;
        }
        private static MethodBase TargetMethod() { return AccessTools.Method(idle, "StartAnim"); }
        private static bool Prefix(ThingComp __instance)
        {
            return !IcecreamTailSkill3Runtime.IsProtected(__instance.parent as Pawn);
        }
        private static void Postfix(ThingComp __instance)
        {
            if (PawnAnimationRendering.Any) MeleeIdleDrawSuppression.Refresh(__instance);
        }
    }

    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleeIdle
    {
        private static Type idle;
        private static bool Prepare() { idle = AccessTools.TypeByName("AM.Idle.IdleControllerComp"); return idle != null; }
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(idle, "CompTick");
            yield return AccessTools.Method(idle, "AddBodyDrawOffset");
        }
        private static bool Prefix(ThingComp __instance)
        {
            return !PawnAnimationRendering.Any || !PawnAnimationRendering.IsBound(__instance.parent as Pawn);
        }
    }

    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleePawnLookup
    {
        private static Type patchMaster;
        private static bool Prepare() { patchMaster = AccessTools.TypeByName("AM.Patches.PatchMaster"); return patchMaster != null; }
        private static MethodBase TargetMethod() { return AccessTools.Method(patchMaster, "GetAnimator"); }
        private static bool Prefix(Pawn __0, ref object __result)
        {
            if (!PawnAnimationRendering.IsBound(__0)) return true;
            __result = null;
            return false;
        }
    }
}
