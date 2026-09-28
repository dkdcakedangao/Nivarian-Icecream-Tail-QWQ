using System.Collections.Generic;
using HarmonyLib;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// “大龙叫！叫！叫！”的入口，嘴巴位置补丁也在这里~
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityMintBreath : IcecreamTailTemporaryAbility
    {
        private bool wasCasting;

        public IcecreamTailAbilityMintBreath()
        {
        }

        public IcecreamTailAbilityMintBreath(Pawn pawn) : base(pawn)
        {
        }

        public IcecreamTailAbilityMintBreath(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastMintAbility(pawn, out reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            bool activated = base.Activate(target, dest);
            if (activated && IcecreamTailSkillAudio.Enabled && pawn.Spawned && pawn.Map != null)
            {
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("IcecreamTailSkill6Success");
                if (sound != null)
                {
                    sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    Log.Error("Nivarian Icecream Tail: missing IcecreamTailSkill6Success SoundDef.");
                }
            }

            return activated;
        }

        public override void AbilityTick()
        {
            bool casting = Casting;
            if (casting && !wasCasting && IcecreamTailSkillAudio.Enabled && pawn.Spawned && pawn.Map != null)
            {
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("IcecreamTailSkill6Start");
                if (sound != null)
                {
                    sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    Log.Error("Nivarian Icecream Tail: missing IcecreamTailSkill6Start SoundDef.");
                }
            }

            wasCasting = casting;
            base.AbilityTick();
            if (!Casting)
            {
                wasCasting = false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wasCasting, "mintBreathWasCasting", false);
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }
    }

    [HarmonyPatch(typeof(NivarianVisualHelper), "GetMouthPos")]
    public static class Patch_NivarianVisualHelper_GetMouthPos_MintBreath
    {
        public static void Postfix(Pawn __0, ref Vector3 __result)
        {
            if (!IcecreamTailUtility.IsNivarian(__0))
            {
                return;
            }

            __result = __0.DrawPos + __0.Rotation.FacingCell.ToVector3() * 0.35f;
        }
    }
}
