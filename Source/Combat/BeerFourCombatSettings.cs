using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;

// 四酒mod选项的相关数值设置
namespace NivarianIcecreamTail
{
    internal static class BeerFourCombatSettings
    {
        private const string BeerBuffDefName = "IcecreamTailBeerEaterBuff";

        public static void Apply()
        {
            HediffDef beerBuffDef = DefDatabase<HediffDef>.GetNamedSilentFail(BeerBuffDefName);
            HediffStage fourBeerStage = beerBuffDef == null || beerBuffDef.stages == null
                ? null
                : beerBuffDef.stages.FirstOrDefault(stage => stage != null && stage.minSeverity >= 4f);
            if (fourBeerStage == null || fourBeerStage.statFactors == null)
            {
                return;
            }

            StatModifier cooldownModifier = fourBeerStage.statFactors.FirstOrDefault(modifier => modifier.stat == StatDefOf.MeleeCooldownFactor);
            if (cooldownModifier == null)
            {
                return;
            }

            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            cooldownModifier.value = settings == null ? 0.4f : Mathf.Clamp(settings.BeerFourMeleeCooldownFactor, 0.1f, 1f);
            RefreshPawnStatCaches();
        }

        private static void RefreshPawnStatCaches()
        {
            foreach (Pawn pawn in TailEatingUtility.AllKnownPawns())
            {
                if (pawn != null)
                {
                    StatDefOf.MeleeCooldownFactor.Worker.ClearCacheForThing(pawn);
                }
            }
        }
    }

}
