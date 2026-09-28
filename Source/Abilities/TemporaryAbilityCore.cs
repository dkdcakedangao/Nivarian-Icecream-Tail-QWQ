using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// 四酒核心
// 现在是所有临时技能的核心了
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
            IcecreamTailTemporaryAbilityUtility.RemoveBeerAbilitiesAndMagicBody(pawn);
        }
    }

    public sealed class HediffCompProperties_IcecreamTailMintAbility : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailMintAbility()
        {
            compClass = typeof(HediffComp_IcecreamTailMintAbility);
        }
    }

    public sealed class HediffComp_IcecreamTailMintAbility : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            IcecreamTailTemporaryAbilityUtility.SyncMintAbility(parent.pawn);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            IcecreamTailTemporaryAbilityUtility.SyncMintAbility(parent.pawn);
        }

        public override void CompPostPostRemoved()
        {
            Pawn pawn = parent.pawn;
            base.CompPostPostRemoved();
            IcecreamTailTemporaryAbilityUtility.RemoveMintAbility(pawn);
        }
    }

    public sealed class HediffCompProperties_IcecreamTailMatchaAbility : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailMatchaAbility()
        {
            compClass = typeof(HediffComp_IcecreamTailMatchaAbility);
        }
    }

    public sealed class HediffComp_IcecreamTailMatchaAbility : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            IcecreamTailTemporaryAbilityUtility.SyncMatchaAbility(parent.pawn);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            IcecreamTailTemporaryAbilityUtility.SyncMatchaAbility(parent.pawn);
        }

        public override void CompPostPostRemoved()
        {
            Pawn pawn = parent.pawn;
            base.CompPostPostRemoved();
            IcecreamTailTemporaryAbilityUtility.RemoveMatchaAbility(pawn);
        }
    }

    public static class IcecreamTailTemporaryAbilityUtility
    {
        public const string BeerBuffDefName = "IcecreamTailBeerEaterBuff";
        public const string BurstAbilityDefName = "IcecreamTailAbilityBurst";
        public const string Burst2AbilityDefName = "IcecreamTailAbilityBurst2";
        public const string Skill3AbilityDefName = "IcecreamTailAbilityGetsugaSaiho";
        public const string Skill4AbilityDefName = "IcecreamTailAbilityTenshin";
        public const string Skill5AbilityDefName = "IcecreamTailAbilityBakkai";
        public const string MintBuffDefName = "IcecreamTailMintEaterBuff";
        public const string MintAbilityDefName = "IcecreamTailAbilityMintBreath";
        public const string MatchaBuffDefName = "IcecreamTailMatchaEaterBuff";
        public const string MatchaAbilityDefName = "IcecreamTailAbilityMatchaSterilizer";
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

        public static bool IsMintEligible(Pawn pawn)
        {
            if (!IcecreamTailMod.Enabled || !IcecreamTailUtility.IsNivarian(pawn) || pawn.health == null)
            {
                return false;
            }

            HediffDef mintBuff = DefDatabase<HediffDef>.GetNamedSilentFail(MintBuffDefName);
            return mintBuff != null && pawn.health.hediffSet.GetFirstHediffOfDef(mintBuff) != null;
        }

        public static bool IsMatchaEligible(Pawn pawn)
        {
            if (!IcecreamTailMod.Enabled || !IcecreamTailUtility.IsNivarian(pawn) || pawn.health == null)
            {
                return false;
            }

            HediffDef matchaBuff = DefDatabase<HediffDef>.GetNamedSilentFail(MatchaBuffDefName);
            return matchaBuff != null && pawn.health.hediffSet.GetFirstHediffOfDef(matchaBuff) != null;
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

        public static bool CanCastMintAbility(Pawn pawn, out string reason)
        {
            if (!IsMintEligible(pawn))
            {
                reason = "只有吃过薄荷味冰淇淋尾巴的涅瓦莲才能使用该能力。";
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

        public static bool CanCastMatchaAbility(Pawn pawn, out string reason)
        {
            if (!IsMatchaEligible(pawn))
            {
                reason = "只有吃过抹茶味冰淇淋尾巴的涅瓦莲才能使用该能力。";
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
            AbilityDef skill3Def = DefDatabase<AbilityDef>.GetNamedSilentFail(Skill3AbilityDefName);
            AbilityDef skill4Def = DefDatabase<AbilityDef>.GetNamedSilentFail(Skill4AbilityDefName);
            AbilityDef skill5Def = DefDatabase<AbilityDef>.GetNamedSilentFail(Skill5AbilityDefName);
            bool eligible = IsFourBeerEligible(pawn);
            SyncAbility(pawn, burstDef, eligible);
            SyncAbility(pawn, burst2Def, eligible);
            SyncAbility(pawn, skill3Def, eligible);
            SyncAbility(pawn, skill4Def, eligible);
            SyncAbility(pawn, skill5Def, eligible);
            if (!eligible)
            {
                IcecreamTailBurst2Runtime.Cancel(pawn);
                IcecreamTailSkill3Runtime.Cancel(pawn);
                RemoveMagicBody(pawn);
            }
        }

        public static void SyncMintAbility(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null)
            {
                return;
            }

            AbilityDef mintAbility = DefDatabase<AbilityDef>.GetNamedSilentFail(MintAbilityDefName);
            SyncAbility(pawn, mintAbility, IsMintEligible(pawn));
        }

        public static void SyncMatchaAbility(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null)
            {
                return;
            }

            AbilityDef matchaAbility = DefDatabase<AbilityDef>.GetNamedSilentFail(MatchaAbilityDefName);
            SyncAbility(pawn, matchaAbility, IsMatchaEligible(pawn));
        }

        private static void SyncAbility(Pawn pawn, AbilityDef def, bool eligible)
        {
            if (def == null) return;
            Ability ability = pawn.abilities.GetAbility(def, false);
            if (eligible && ability == null) pawn.abilities.GainAbility(def);
            else if (!eligible && ability != null) pawn.abilities.RemoveAbility(def);
        }

        public static void RemoveBeerAbilitiesAndMagicBody(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            IcecreamTailBurst2Runtime.Cancel(pawn);
            IcecreamTailSkill3Runtime.Cancel(pawn);

            if (pawn.abilities != null)
            {
                string[] defNames =
                {
                    BurstAbilityDefName,
                    Burst2AbilityDefName,
                    Skill3AbilityDefName,
                    Skill4AbilityDefName,
                    Skill5AbilityDefName
                };
                foreach (string defName in defNames)
                {
                    AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
                    if (def != null)
                    {
                        pawn.abilities.RemoveAbility(def);
                    }
                }
            }

            RemoveMagicBody(pawn);
        }

        public static void RemoveMintAbility(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null)
            {
                return;
            }

            AbilityDef mintAbility = DefDatabase<AbilityDef>.GetNamedSilentFail(MintAbilityDefName);
            if (mintAbility != null)
            {
                pawn.abilities.RemoveAbility(mintAbility);
            }
        }

        public static void RemoveMatchaAbility(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            IcecreamTailSterilizerRuntime.Cancel(pawn);
            if (pawn.abilities == null)
            {
                return;
            }

            AbilityDef matchaAbility = DefDatabase<AbilityDef>.GetNamedSilentFail(MatchaAbilityDefName);
            if (matchaAbility != null)
            {
                pawn.abilities.RemoveAbility(matchaAbility);
            }
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
