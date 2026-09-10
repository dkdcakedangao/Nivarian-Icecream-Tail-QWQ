using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// SKILL1 爆！
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityBurst : IcecreamTailTemporaryAbility
    {
        public IcecreamTailAbilityBurst()
        {
        }

        public IcecreamTailAbilityBurst(Pawn pawn) : base(pawn)
        {
        }

        public IcecreamTailAbilityBurst(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(pawn, out reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            bool activated = base.Activate(target, dest);
            if (activated)
            {
                PlayCastSound();
            }

            return activated;
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }

        private void PlayCastSound()
        {
            string defName = Rand.Bool ? "IcecreamTailSkillBurst1" : "IcecreamTailSkillBurst2";
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (sound == null)
            {
                Log.Error("Nivarian Icecream Tail: missing " + defName + " SoundDef.");
                return;
            }

            if (pawn.Spawned && pawn.Map != null)
            {
                sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }
    }

    public sealed class CompProperties_IcecreamTailBurst : CompProperties_AbilityEffect
    {
        public CompProperties_IcecreamTailBurst()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailBurst);
        }
    }

    public sealed class CompAbilityEffect_IcecreamTailBurst : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent == null ? null : parent.pawn;
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(pawn, out reason))
            {
                return;
            }

            HediffDef magicBodyDef = DefDatabase<HediffDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.MagicBodyDefName);
            if (magicBodyDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing IcecreamTailMagicBody HediffDef.");
                return;
            }

            Hediff magicBody = pawn.health.hediffSet.GetFirstHediffOfDef(magicBodyDef);
            if (magicBody == null)
            {
                magicBody = HediffMaker.MakeHediff(magicBodyDef, pawn);
                pawn.health.AddHediff(magicBody);
            }
            else
            {
                HediffComp_Disappears disappears = magicBody.TryGetComp<HediffComp_Disappears>();
                if (disappears != null)
                {
                    disappears.ResetElapsedTicks();
                }
            }

        }
    }

    // 魔身 话说gpt翻译成magicbody，我觉得非常有意思，所以就用这个了
    // 这部分也有gpt老师的味道
    public sealed class Hediff_IcecreamTailMagicBody : HediffWithComps
    {
        public override Color LabelColor
        {
            get { return new Color(1f, 0.12f, 0.12f); }
        }
    }

    public sealed class HediffCompProperties_IcecreamTailMagicBodyValidator : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailMagicBodyValidator()
        {
            compClass = typeof(HediffComp_IcecreamTailMagicBodyValidator);
        }
    }

    public sealed class HediffComp_IcecreamTailMagicBodyValidator : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (!IcecreamTailTemporaryAbilityUtility.IsFourBeerEligible(parent.pawn))
            {
                parent.pawn.health.RemoveHediff(parent);
            }
        }
    }

}
