using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;

namespace NivarianIcecreamTail
{
    // 判断尾巴状态，从而决定恢复速度、是否可以有冰淇淋尾巴
    // 需要优化
    public enum TailKind
    {
        None,
        Natural,
        Fake,
        Bionic
    }

    public static class IcecreamTailUtility
    {
        private const string NivarianPawnDef = "NivarianRace_Pawn";
        private const string NaturalTailPartDef = "Nivarian_TailPart";
        private const string FakeTailHediffDef = "Nivarian_FakeTail_hediff";
        private const string BionicTailHediffDef = "Nivarian_BionicTail_hediff";
        private const string PlaceholderHediffDef = "IcecreamTailPlaceholder";
        private const string NivarianThoughtDef = "IcecreamTailMemoryNivarian";
        private const string OtherThoughtDef = "IcecreamTailMemoryOther";

        public static bool IsNivarian(Pawn pawn)
        {
            return pawn != null && pawn.def != null && pawn.def.defName == NivarianPawnDef;
        }

        public static TailKind GetTailKind(Pawn pawn, out BodyPartRecord tailPart)
        {
            tailPart = null;
            if (!IsNivarian(pawn) || pawn.health == null)
            {
                return TailKind.None;
            }

            Hediff fakeTail = pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def.defName == FakeTailHediffDef);
            if (fakeTail != null && fakeTail.Part != null && pawn.health.hediffSet.GetNotMissingParts().Contains(fakeTail.Part))
            {
                tailPart = fakeTail.Part;
                return TailKind.Fake;
            }

            Hediff bionicTail = pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def.defName == BionicTailHediffDef);
            if (bionicTail != null && bionicTail.Part != null && pawn.health.hediffSet.GetNotMissingParts().Contains(bionicTail.Part))
            {
                tailPart = bionicTail.Part;
                return TailKind.Bionic;
            }

            tailPart = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(part => part.def.defName == NaturalTailPartDef);
            return tailPart == null ? TailKind.None : TailKind.Natural;
        }

        public static Hediff GetPlaceholder(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            return pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def.defName == PlaceholderHediffDef);
        }

        // 还是那句话，到底是该用“成熟”，还是“冰冻”，还是什么乱七八糟的东西，表示尾长好了？总之先用成熟吧……反正……debug……没人……看？
        public static bool TryAddPlaceholder(Pawn pawn, IcecreamTailFlavorDef flavor = null)
        {
            BodyPartRecord tailPart;
            TailKind tailKind = GetTailKind(pawn, out tailPart);
            if (tailKind == TailKind.None || GetPlaceholder(pawn) != null || TailEatingUtility.GetRecovery(pawn) != null)
            {
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("未添加成熟尾巴：" + IcecreamTailDebug.PawnInfo(pawn) + "，尾巴类型=" + tailKind + "，已有成熟状态=" + (GetPlaceholder(pawn) != null) + "，恢复中=" + (TailEatingUtility.GetRecovery(pawn) != null));
                }

                return false;
            }

            HediffDef placeholderDef = DefDatabase<HediffDef>.GetNamedSilentFail(PlaceholderHediffDef);
            if (placeholderDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing IcecreamTailPlaceholder HediffDef.");
                return false;
            }

            Hediff placeholder = HediffMaker.MakeHediff(placeholderDef, pawn, tailPart);
            IcecreamTailFlavorUtility.SetFlavor(placeholder, flavor);
            pawn.health.AddHediff(placeholder, tailPart);
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("已添加成熟冰淇淋尾巴：" + IcecreamTailDebug.PawnInfo(pawn) + "，尾巴类型=" + tailKind + "，口味=" + IcecreamTailFlavorUtility.GetFlavor(placeholder).label + "，部位=" + tailPart.Label);
            }

            return true;
        }

        public static void RemovePlaceholder(Pawn pawn)
        {
            Hediff placeholder = GetPlaceholder(pawn);
            if (placeholder != null)
            {
                pawn.health.RemoveHediff(placeholder);
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已移除成熟冰淇淋尾巴：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }
        }

        public static void RemoveAllPlaceholders()
        {
            if (Current.Game == null)
            {
                return;
            }

            HashSet<Pawn> pawns = new HashSet<Pawn>();
            foreach (Map map in Find.Maps)
            {
                pawns.AddRange(map.mapPawns.AllPawns);
            }

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
            {
                pawns.Add(pawn);
            }

            foreach (Pawn pawn in pawns)
            {
                RemovePlaceholder(pawn);
            }
        }

        public static void GainOrRefreshMemory(Pawn pawn, bool nivarian)
        {
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(nivarian ? NivarianThoughtDef : OtherThoughtDef);
            if (thoughtDef == null || pawn == null || pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null || pawn.needs.mood.thoughts.memories == null)
            {
                return;
            }

            Thought_Memory existing = pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(thoughtDef);
            if (existing == null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
                return;
            }

            existing.age = 0;
        }
    }

}
