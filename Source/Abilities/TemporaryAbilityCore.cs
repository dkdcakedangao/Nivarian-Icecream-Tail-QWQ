using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// 四酒核心
namespace NivarianIcecreamTail
{
    public abstract class IcecreamTailTemporaryAbility : Ability
    {
        protected IcecreamTailTemporaryAbility()
        {
        }

        protected IcecreamTailTemporaryAbility(Pawn pawn) : base(pawn)
        {
        }

        protected IcecreamTailTemporaryAbility(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
        }
    }

    public sealed class HediffCompProperties_IcecreamTailBeerFourAbilities : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailBeerFourAbilities()
        {
            compClass = typeof(HediffComp_IcecreamTailBeerFourAbilities);
        }
    }

    public sealed class HediffComp_IcecreamTailBeerFourAbilities : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            IcecreamTailTemporaryAbilityUtility.SyncPawn(parent.pawn);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            IcecreamTailTemporaryAbilityUtility.SyncPawn(parent.pawn);
        }

        public override void CompPostPostRemoved()
        {
            Pawn pawn = parent.pawn;
            base.CompPostPostRemoved();
            IcecreamTailTemporaryAbilityUtility.RemoveTemporaryAbilitiesAndMagicBody(pawn);
        }
    }

    public static class IcecreamTailTemporaryAbilityUtility
    {
        public const string BeerBuffDefName = "IcecreamTailBeerEaterBuff";
        public const string BurstAbilityDefName = "IcecreamTailAbilityBurst";
        public const string Burst2AbilityDefName = "IcecreamTailAbilityBurst2";
        public const string MagicBodyDefName = "IcecreamTailMagicBody";

        public static bool IsFourBeerEligible(Pawn pawn)
        {
            if (!IcecreamTailMod.Enabled || !IcecreamTailUtility.IsNivarian(pawn) || pawn.health == null)
            {
                return false;
            }

            Hediff beer = pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def != null && hediff.def.defName == BeerBuffDefName);
            return beer != null && beer.Severity >= 4f;
        }

        public static bool HasMagicBody(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return false;
            }

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(MagicBodyDefName);
            return def != null && pawn.health.hediffSet.GetFirstHediffOfDef(def) != null;
        }

        public static bool CanCastTemporaryAbility(Pawn pawn, out string reason)
        {
            if (!IsFourBeerEligible(pawn))
            {
                reason = "只有处于四酒状态的涅瓦莲才能使用该能力。";
                return false;
            }

            if (!pawn.Drafted)
            {
                reason = "需要先征召该涅瓦莲。";
                return false;
            }

            reason = null;
            return true;
        }

        public static void SyncPawn(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null)
            {
                return;
            }

            AbilityDef burstDef = DefDatabase<AbilityDef>.GetNamedSilentFail(BurstAbilityDefName);
            AbilityDef burst2Def = DefDatabase<AbilityDef>.GetNamedSilentFail(Burst2AbilityDefName);
            bool eligible = IsFourBeerEligible(pawn);
            SyncAbility(pawn, burstDef, eligible);
            SyncAbility(pawn, burst2Def, eligible);
            if (!eligible)
            {
                IcecreamTailBurst2Runtime.Cancel(pawn);
                RemoveMagicBody(pawn);
            }
        }

        private static void SyncAbility(Pawn pawn, AbilityDef def, bool eligible)
        {
            if (def == null) return;
            Ability ability = pawn.abilities.GetAbility(def, false);
            if (eligible && ability == null) pawn.abilities.GainAbility(def);
            else if (!eligible && ability != null) pawn.abilities.RemoveAbility(def);
        }

        public static void RemoveTemporaryAbilitiesAndMagicBody(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            IcecreamTailBurst2Runtime.Cancel(pawn);

            if (pawn.abilities != null)
            {
                List<AbilityDef> defs = pawn.abilities.AllAbilitiesForReading
                    .Where(ability => ability is IcecreamTailTemporaryAbility)
                    .Select(ability => ability.def)
                    .Where(def => def != null)
                    .Distinct()
                    .ToList();
                foreach (AbilityDef def in defs)
                {
                    pawn.abilities.RemoveAbility(def);
                }
            }

            RemoveMagicBody(pawn);
        }

        // 开发者
        public static int ResetTemporaryAbilityCooldowns(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Ability ability in pawn.abilities.AllAbilitiesForReading)
            {
                if (ability is IcecreamTailTemporaryAbility)
                {
                    ability.ResetCooldown();
                    count++;
                }
            }

            return count;
        }

        // 除错
        private static void RemoveMagicBody(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(MagicBodyDefName);
            Hediff magicBody = def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (magicBody != null)
            {
                pawn.health.RemoveHediff(magicBody);
            }
        }
    }

}
