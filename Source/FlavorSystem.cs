using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

// 味道切换
// 看到我，记得提醒我，下次记得分文件夹
namespace NivarianIcecreamTail
{
    public sealed class ThoughtWorker_IcecreamTailBeerOwner : ThoughtWorker
    {
        private const string BeerFlavorDefName = "IcecreamTailFlavorBeer";

        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            if (pawn == null || (IcecreamTailMod.Settings != null && !IcecreamTailMod.Settings.EnableMoodEffects))
            {
                return ThoughtState.Inactive;
            }

            Hediff tail = IcecreamTailUtility.GetPlaceholder(pawn);
            IcecreamTailFlavorDef flavor = IcecreamTailFlavorUtility.GetFlavor(tail);
            return tail != null && flavor != null && flavor.defName == BeerFlavorDefName;
        }
    }

    public sealed class IcecreamTailFlavorDef : Def
    {
        public float recoverySpeedFactor = 1f;
        public Color labelColor = IcecreamTailColors.LabelBlue;
        public string tailDescription;
        public HediffDef eaterBuff;
        public ThoughtDef extraEaterMoodThought;
    }

    public sealed class IcecreamTailFlavorExtension : DefModExtension
    {
        public IcecreamTailFlavorDef flavor;
    }

    public sealed class HediffCompProperties_IcecreamTailFlavor : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailFlavor()
        {
            compClass = typeof(HediffComp_IcecreamTailFlavor);
        }
    }

    public sealed class HediffComp_IcecreamTailFlavor : HediffComp
    {
        private IcecreamTailFlavorDef flavor;

        public IcecreamTailFlavorDef Flavor
        {
            get { return flavor ?? IcecreamTailFlavorUtility.Original; }
        }

        public void SetFlavor(IcecreamTailFlavorDef newFlavor)
        {
            flavor = newFlavor ?? IcecreamTailFlavorUtility.Original;
        }

        public override void CompExposeData()
        {
            Scribe_Defs.Look(ref flavor, "flavor");
        }
    }

    public static class IcecreamTailFlavorUtility
    {
        private const string OriginalDefName = "IcecreamTailFlavorOriginal";
        private const string SeasoningThoughtDefName = "IcecreamTailMemorySeasoning";

        public static IcecreamTailFlavorDef Original
        {
            get { return DefDatabase<IcecreamTailFlavorDef>.GetNamedSilentFail(OriginalDefName); }
        }

        public static IcecreamTailFlavorDef GetFlavor(Hediff hediff)
        {
            if (hediff == null)
            {
                return Original;
            }

            HediffComp_IcecreamTailFlavor comp = hediff.TryGetComp<HediffComp_IcecreamTailFlavor>();
            return comp == null ? Original : comp.Flavor;
        }

        public static void SetFlavor(Hediff hediff, IcecreamTailFlavorDef flavor)
        {
            if (hediff == null)
            {
                return;
            }

            HediffComp_IcecreamTailFlavor comp = hediff.TryGetComp<HediffComp_IcecreamTailFlavor>();
            if (comp != null)
            {
                comp.SetFlavor(flavor);
            }
        }

        public static bool TrySetTailFlavor(Pawn pawn, IcecreamTailFlavorDef flavor)
        {
            BodyPartRecord tailPart;
            if (pawn == null || !IcecreamTailMod.Enabled || !IcecreamTailUtility.IsNivarian(pawn) || flavor == null || IcecreamTailUtility.GetTailKind(pawn, out tailPart) == TailKind.None)
            {
                return false;
            }

            Hediff tailState = IcecreamTailUtility.GetPlaceholder(pawn) ?? TailEatingUtility.GetRecovery(pawn);
            if (tailState == null)
            {
                return false;
            }

            SetFlavor(tailState, flavor);
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("尾巴口味已切换：" + IcecreamTailDebug.PawnInfo(pawn) + "，口味=" + flavor.label + "，恢复中=" + (TailEatingUtility.GetRecovery(pawn) != null) + "，进度=" + tailState.Severity.ToStringPercent());
            }

            return true;
        }

        // 切换味道是对的，分文件也是对的！
        // 但是！我！忘记！写！路径图了！
        // 完蛋了！
        public static string TailLabel(Hediff hediff, string originalLabel)
        {
            IcecreamTailFlavorDef flavor = GetFlavor(hediff);
            return flavor == null || flavor == Original ? originalLabel : flavor.label + "冰淇淋尾巴";
        }

        public static string ReadyTailDescription(Hediff hediff)
        {
            IcecreamTailFlavorDef flavor = GetFlavor(hediff);
            return flavor == null || flavor.tailDescription.NullOrEmpty() ? "冰冰的尾巴，看起来非常好吃喵~" : flavor.tailDescription;
        }

        public static Color LabelColor(Hediff hediff)
        {
            IcecreamTailFlavorDef flavor = GetFlavor(hediff);
            return flavor == null ? IcecreamTailColors.LabelBlue : flavor.labelColor;
        }

        public static Color BuffLabelColor(HediffDef buffDef)
        {
            IcecreamTailFlavorDef flavor = DefDatabase<IcecreamTailFlavorDef>.AllDefsListForReading.FirstOrDefault(def => def.eaterBuff == buffDef);
            return flavor == null ? IcecreamTailColors.LabelBlue : flavor.labelColor;
        }

        public static float RecoverySpeedFactor(Hediff recovery)
        {
            IcecreamTailFlavorDef flavor = GetFlavor(recovery);
            return flavor == null ? 1f : flavor.recoverySpeedFactor;
        }

        public static void GainOrRefreshMemory(Pawn pawn, ThoughtDef thoughtDef)
        {
            if (thoughtDef == null || pawn == null || pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null || pawn.needs.mood.thoughts.memories == null)
            {
                return;
            }

            Thought_Memory memory = pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(thoughtDef);
            if (memory == null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }
            else
            {
                memory.age = 0;
            }
        }

        public static void GainSeasoningMemory(Pawn pawn)
        {
            GainOrRefreshMemory(pawn, DefDatabase<ThoughtDef>.GetNamedSilentFail(SeasoningThoughtDefName));
        }

        public static void RemoveTailBuffs(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            HashSet<HediffDef> buffDefs = new HashSet<HediffDef>(DefDatabase<IcecreamTailFlavorDef>.AllDefsListForReading.Where(flavor => flavor.eaterBuff != null).Select(flavor => flavor.eaterBuff));
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(hediff => buffDefs.Contains(hediff.def)).ToList())
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public static void RemoveTailBuff(Pawn pawn, HediffDef buffDef)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || buffDef == null)
            {
                return;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(hediff => hediff.def == buffDef).ToList())
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }

    public sealed class IngestionOutcomeDoer_IcecreamTailSeasoning : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (!IcecreamTailMod.Enabled || pawn == null || ingested == null || ingested.def == null)
            {
                return;
            }

            IcecreamTailFlavorExtension extension = ingested.def.GetModExtension<IcecreamTailFlavorExtension>();
            if (extension == null || extension.flavor == null)
            {
                Log.Error("Nivarian Icecream Tail: seasoning is missing its flavor extension.");
                return;
            }

            IcecreamTailFlavorUtility.GainSeasoningMemory(pawn);
            bool applied = IcecreamTailFlavorUtility.TrySetTailFlavor(pawn, extension.flavor);
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("调味料已食用：食用者=" + IcecreamTailDebug.PawnInfo(pawn) + "，物品=" + ingested.def.defName + "，口味=" + extension.flavor.label + "，切换结果=" + (applied ? "成功" : "失败：没有可切换的冰淇淋尾巴"));
            }
        }
    }
}
