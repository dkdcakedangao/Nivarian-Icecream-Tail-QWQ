using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;

// 食物相关的
namespace NivarianIcecreamTail
{
    public sealed class IngredientValueGetter_TailIcecream : IngredientValueGetter
    {
        public override float ValuePerUnitOf(ThingDef thingDef)
        {
            ThingCategoryDef foodRaw = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodRaw");
            return foodRaw != null && thingDef.IsWithinCategory(foodRaw)
                ? thingDef.GetStatValueAbstract(StatDefOf.Nutrition)
                : 1f;
        }

        public override string BillRequirementsDescription(RecipeDef recipe, IngredientCount ingredient)
        {
            if (ingredient.GetBaseCount() == 0.5f)
            {
                return "0.5 营养";
            }

            ThingDef herbalMedicine = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");
            if (herbalMedicine != null && ingredient.filter.Allows(herbalMedicine))
            {
                return ingredient.GetBaseCount() + " × " + herbalMedicine.LabelCap;
            }

            ThingDef nivarianScale = DefDatabase<ThingDef>.GetNamedSilentFail("Nivarian_Scale");
            if (nivarianScale != null && ingredient.filter.Allows(nivarianScale))
            {
                return ingredient.GetBaseCount() + " × " + nivarianScale.LabelCap;
            }

            return ingredient.GetBaseCount().ToString();
        }
    }

    public sealed class IngestionOutcomeDoer_TailIcecream : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (!IcecreamTailMod.Enabled || pawn == null)
            {
                return;
            }

            if (!IcecreamTailUtility.IsNivarian(pawn))
            {
                IcecreamTailUtility.GainOrRefreshMemory(pawn, false);
                return;
            }

            if (IcecreamTailUtility.GetPlaceholder(pawn) == null && IcecreamTailUtility.TryAddPlaceholder(pawn))
            {
                IcecreamTailUtility.GainOrRefreshMemory(pawn, true);
            }
        }
    }

    public sealed class IngestionOutcomeDoer_TailRestoreIce : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            BodyPartRecord tailPart;
            if (!IcecreamTailMod.Enabled || !IcecreamTailUtility.IsNivarian(pawn) || IcecreamTailUtility.GetTailKind(pawn, out tailPart) == TailKind.None)
            {
                return;
            }

            Hediff recovery = TailEatingUtility.GetRecovery(pawn);
            if (recovery != null)
            {
                TailEatingUtility.FinishRecovery(pawn, recovery);
            }
        }
    }

}
