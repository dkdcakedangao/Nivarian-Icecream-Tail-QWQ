using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace NivarianIcecreamTail
{
    public sealed class Hediff_IcecreamTailBeerHangover : HediffWithComps
    {
        public override UnityEngine.Color LabelColor
        {
            get { return new UnityEngine.Color(1f, 0.18f, 0.18f); }
        }
    }
    
    // 核心！智慧的结晶！吃吃你的尾巴！
    public static partial class TailEatingUtility
    {
        public static void ConsumeTail(Pawn eater, Pawn target)
        {
            if (!IsReadyTailPawn(target) || eater == null || eater == target)
            {
                return;
            }

            Hediff placeholder = IcecreamTailUtility.GetPlaceholder(target);
            IcecreamTailFlavorDef flavor = IcecreamTailFlavorUtility.GetFlavor(placeholder);
            bool beerEffectsBlocked = IsBeerFlavor(flavor) && HasBeerHangover(eater);
            IcecreamTailUtility.RemovePlaceholder(target);
            StartRecovery(target, flavor);
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("冰淇淋尾巴已被舔食：食用者=" + IcecreamTailDebug.PawnInfo(eater) + "，尾巴主人=" + IcecreamTailDebug.PawnInfo(target));
            }
            if (eater.needs != null && eater.needs.food != null)
            {
                eater.needs.food.CurLevel += 0.9f;
            }

            // 吃尾巴 四酒宿醉状态无法获得buff
            if (IcecreamTailMod.Settings.EnableMoodEffects)
            {
                GainOrRefreshMood(eater, EaterMoodThoughtDefName);
                if (!beerEffectsBlocked)
                {
                    IcecreamTailFlavorUtility.GainOrRefreshMemory(eater, flavor.extraEaterMoodThought);
                }
                GainOrRefreshMood(target, OwnerMoodThoughtDefName);
            }

            if (IcecreamTailMod.Settings.EnableRelationshipEffects)
            {
                GainSocialMemory(eater, EaterSocialThoughtDefName, target);
                GainSocialMemory(target, OwnerSocialThoughtDefName, eater);
            }

            if (IcecreamTailMod.Settings.EnableTailBuff && !beerEffectsBlocked)
            {
                RefreshTailBuff(eater, flavor);
            }

            if (!beerEffectsBlocked && (IcecreamTailMod.Settings == null || IcecreamTailMod.Settings.EnableTailBuff))
            {
                ApplyBeerAlcoholEffect(eater, flavor);
            }

            if (beerEffectsBlocked && IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("啤酒口味专属效果被宿醉阻止：" + IcecreamTailDebug.PawnInfo(eater));
            }
            ApplyChocolateWolfeinEasterEgg(eater, flavor);
            ApplyFrozenTongueEasterEgg(eater, target);
        }

        private static bool IsBeerFlavor(IcecreamTailFlavorDef flavor)
        {
            return flavor != null && flavor.defName == BeerFlavorDefName;
        }

        private static bool HasBeerHangover(Pawn pawn)
        {
            return GetHediff(pawn, BeerHangoverDefName) != null;
        }

        private static void ApplyBeerHangover(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || HasBeerHangover(pawn) || IcecreamTailMod.Settings == null || !IcecreamTailMod.Settings.EnableBeerHangover)
            {
                return;
            }

            HediffDef hangoverDef = DefDatabase<HediffDef>.GetNamedSilentFail(BeerHangoverDefName);
            if (hangoverDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing IcecreamTailBeerHangover HediffDef.");
                return;
            }

            pawn.health.AddHediff(HediffMaker.MakeHediff(hangoverDef, pawn));
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("四酒结束，已进入啤酒冰淇淋宿醉：" + IcecreamTailDebug.PawnInfo(pawn));
            }
        }

        private static void ApplyBeerAlcoholEffect(Pawn eater, IcecreamTailFlavorDef flavor)
        {
            if (eater == null || eater.health == null || flavor == null || flavor.defName != BeerFlavorDefName)
            {
                return;
            }

            HediffDef alcoholDef = DefDatabase<HediffDef>.GetNamedSilentFail(AlcoholHighDefName);
            if (alcoholDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing vanilla AlcoholHigh HediffDef.");
                return;
            }

            Hediff alcohol = GetHediff(eater, AlcoholHighDefName);
            float previousSeverity = alcohol == null ? 0f : alcohol.Severity;
            HealthUtility.AdjustSeverity(eater, alcoholDef, BeerAlcoholSeverity);
            alcohol = GetHediff(eater, AlcoholHighDefName);
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("啤酒尾巴醉酒效果已添加：" + IcecreamTailDebug.PawnInfo(eater) + "，AlcoholHigh=" + previousSeverity.ToString("0.00") + " → " + (alcohol == null ? "<missing>" : alcohol.Severity.ToString("0.00")) + "，不增加耐受与成瘾。");
            }
        }
        
        // 大狗彩蛋，这段有gpt老师的味道喵
        private static void ApplyChocolateWolfeinEasterEgg(Pawn eater, IcecreamTailFlavorDef flavor)
        {
            if (eater == null || flavor == null || flavor.defName != ChocolateFlavorDefName || IcecreamTailMod.Settings == null || !IcecreamTailMod.Settings.EnableChocolateWolfeinEasterEgg)
            {
                return;
            }

            if (eater.def == null || eater.def.defName != WolfeinPawnDefName)
            {
                return;
            }

            HediffDef foodPoisoningDef = DefDatabase<HediffDef>.GetNamedSilentFail(FoodPoisoningDefName);
            if (foodPoisoningDef == null || eater.health == null || eater.health.hediffSet == null)
            {
                if (IcecreamTailDebug.EasterEggEnabled)
                {
                    IcecreamTailDebug.EasterEgg("彩蛋未触发：缺少 FoodPoisoning 或食用者健康状态无效，食用者=" + IcecreamTailDebug.PawnInfo(eater));
                }

                return;
            }

            Hediff foodPoisoning = eater.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def == foodPoisoningDef);
            if (foodPoisoning == null)
            {
                foodPoisoning = HediffMaker.MakeHediff(foodPoisoningDef, eater);
                eater.health.AddHediff(foodPoisoning);
            }

            foodPoisoning.Severity = WolfeinChocolateFoodPoisoningSeverity;
            IcecreamTailFlavorUtility.GainOrRefreshMemory(eater, DefDatabase<ThoughtDef>.GetNamedSilentFail(WolfeinChocolateThoughtDefName));
            if (IcecreamTailDebug.EasterEggEnabled)
            {
                IcecreamTailDebug.EasterEgg("巧克力彩蛋触发：" + IcecreamTailDebug.PawnInfo(eater) + " 获得严重食物中毒 10 分钟与特殊心情。");
            }
        }

        // 粘舌头彩蛋
        private static void ApplyFrozenTongueEasterEgg(Pawn eater, Pawn target)
        {
            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            if (eater == null || target == null || settings == null || !settings.EnableFrozenTongueEasterEgg ||
                !Rand.Chance(UnityEngine.Mathf.Clamp01(settings.FrozenTongueEasterEggChance)))
            {
                return;
            }

            if (eater.health == null || eater.health.hediffSet == null)
            {
                Log.Error("Nivarian Icecream Tail: frozen tongue easter egg target has no valid health tracker.");
                return;
            }

            HediffDef cryoSlowDef = DefDatabase<HediffDef>.GetNamedSilentFail(CryoSlowDefName);
            if (cryoSlowDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing Nivarian_Hediff_CryoSlow HediffDef.");
                return;
            }

            float rolledSeverity = Rand.Range(0.25f, 0.9f);
            Hediff cryoSlow = eater.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def == cryoSlowDef);
            if (cryoSlow == null)
            {
                cryoSlow = HediffMaker.MakeHediff(cryoSlowDef, eater);
                cryoSlow.Severity = rolledSeverity;
                eater.health.AddHediff(cryoSlow);
                cryoSlow = eater.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def == cryoSlowDef);
            }
            else
            {
                cryoSlow.Severity = Math.Max(cryoSlow.Severity, rolledSeverity);
            }

            if (cryoSlow == null)
            {
                Log.Error("Nivarian Icecream Tail: failed to apply frozen tongue easter egg Hediff.");
                return;
            }

            Messages.Message(
                "“" + eater.LabelShortCap + "”在品尝“" + target.LabelShortCap + "”的冰淇淋尾巴时，舌头被冻住了了喵！",
                eater,
                MessageTypeDefOf.NegativeEvent,
                false);

            if (IcecreamTailDebug.EasterEggEnabled)
            {
                IcecreamTailDebug.EasterEgg("冻住舌头彩蛋触发：食用者=" + IcecreamTailDebug.PawnInfo(eater) +
                    "，尾巴主人=" + IcecreamTailDebug.PawnInfo(target) +
                    "，随机严重度=" + rolledSeverity.ToString("0.000") +
                    "，最终严重度=" + cryoSlow.Severity.ToString("0.000"));
            }
        }

        private static void GainOrRefreshMood(Pawn pawn, string defName)
        {
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
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

        private static void GainSocialMemory(Pawn pawn, string defName, Pawn otherPawn)
        {
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
            if (thoughtDef != null && pawn != null && otherPawn != null && pawn.needs != null && pawn.needs.mood != null && pawn.needs.mood.thoughts != null && pawn.needs.mood.thoughts.memories != null)
            {
                Thought_Memory existing = pawn.needs.mood.thoughts.memories.Memories.FirstOrDefault(memory => memory.def == thoughtDef && memory.otherPawn == otherPawn);
                if (existing == null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, otherPawn);
                }
                else
                {
                    existing.age = 0;
                }
            }
        }

        // 刷新时长、四酒叠层的区分
        private static void RefreshTailBuff(Pawn pawn, IcecreamTailFlavorDef flavor)
        {
            if (pawn == null || pawn.health == null || flavor == null || flavor.eaterBuff == null)
            {
                return;
            }

            Hediff buff = pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def == flavor.eaterBuff);
            bool beerBuff = flavor.eaterBuff.defName == BeerBuffDefName;
            int previousLevel = buff == null || !beerBuff ? 0 : Math.Max(1, Math.Min(4, (int)Math.Round(buff.Severity)));
            string refreshMode;
            if (buff == null)
            {
                buff = HediffMaker.MakeHediff(flavor.eaterBuff, pawn);
                if (beerBuff)
                {
                    buff.Severity = 1f;
                }

                refreshMode = "新建";
            }
            else if (beerBuff)
            {
                buff.Severity = Math.Min(4f, Math.Max(1f, buff.Severity + 1f));
                refreshMode = previousLevel >= 4 ? "四酒刷新" : "叠层";
            }
            else
            {
                pawn.health.RemoveHediff(buff);
                buff = HediffMaker.MakeHediff(flavor.eaterBuff, pawn);
                refreshMode = "重置";
            }

            HediffComp_IcecreamTailBuffDuration duration = buff.TryGetComp<HediffComp_IcecreamTailBuffDuration>();
            if (duration != null)
            {
                duration.Initialize(TailBuffDurationTicks(flavor));
            }

            if (!pawn.health.hediffSet.hediffs.Contains(buff))
            {
                pawn.health.AddHediff(buff);
            }

            int finalLevel = beerBuff ? Math.Max(1, Math.Min(4, (int)Math.Round(buff.Severity))) : 0;
            if (beerBuff && previousLevel == 3 && finalLevel == 4)
            {
                BeerFourVfxUtility.TryTriggerFourBeerTransition(pawn);
            }

            if (beerBuff)
            {
                IcecreamTailTemporaryAbilityUtility.SyncPawn(pawn);
            }

            if (IcecreamTailDebug.TailEnabled)
            {
                string levelText = beerBuff ? "，层数=" + Math.Max(1, Math.Min(4, (int)Math.Round(buff.Severity))) : string.Empty;
                string durationText = duration == null ? string.Empty : "，剩余=" + duration.RemainingTicks.ToStringTicksToPeriod();
                IcecreamTailDebug.Tail("食用 Buff 已处理：" + IcecreamTailDebug.PawnInfo(pawn) + "，口味=" + flavor.label + "，方式=" + refreshMode + levelText + durationText);
            }
        }

        private static int TailBuffDurationTicks(IcecreamTailFlavorDef flavor)
        {
            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            float multiplier = settings == null ? 1f : Math.Max(0.1f, Math.Min(10f, settings.TailBuffDurationMultiplier));
            if (settings != null && settings.HalveVanillaBuffDuration && flavor != null && flavor.defName == VanillaFlavorDefName)
            {
                multiplier *= 0.5f;
            }

            int baseDuration = flavor != null && flavor.eaterBuff != null && flavor.eaterBuff.defName == BeerBuffDefName
                ? BeerTailBuffDurationTicks
                : BaseTailBuffDurationTicks;
            return Math.Max(1, (int)Math.Round(baseDuration * multiplier));
        }

        internal static int TailBuffDurationTicksForHediff(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
            {
                return BaseTailBuffDurationTicks;
            }

            IcecreamTailFlavorDef flavor = DefDatabase<IcecreamTailFlavorDef>.AllDefsListForReading.FirstOrDefault(def => def.eaterBuff == hediff.def);
            return TailBuffDurationTicks(flavor);
        }

        public sealed class HediffCompProperties_IcecreamTailBuffDuration : HediffCompProperties
        {
            public HediffCompProperties_IcecreamTailBuffDuration()
            {
                compClass = typeof(HediffComp_IcecreamTailBuffDuration);
            }
        }

        public sealed class HediffComp_IcecreamTailBuffDuration : HediffComp
        {
            private int remainingTicks = -1;

            public int RemainingTicks
            {
                get { return Math.Max(0, remainingTicks); }
            }

            public void Initialize(int ticks)
            {
                remainingTicks = Math.Max(1, ticks);
            }

            public override void CompExposeData()
            {
                Scribe_Values.Look(ref remainingTicks, "remainingTicks", -1);
            }

            public override void CompPostTickInterval(ref float severityAdjustment, int delta)
            {
                if (parent.def != null && parent.def.defName == BeerBuffDefName)
                {
                    parent.Severity = (float)Math.Round(parent.Severity);
                    parent.Severity = Math.Max(1f, Math.Min(4f, parent.Severity));
                }

                if (remainingTicks < 0)
                {
                    remainingTicks = TailBuffDurationTicksForHediff(parent);
                }

                remainingTicks -= delta;
                if (remainingTicks <= 0 && parent.pawn != null && parent.pawn.health != null)
                {
                    bool enterBeerHangover = parent.def != null && parent.def.defName == BeerBuffDefName && parent.Severity >= 4f;
                    Pawn pawn = parent.pawn;
                    parent.pawn.health.RemoveHediff(parent);
                    if (enterBeerHangover)
                    {
                        ApplyBeerHangover(pawn);
                    }
                }
            }
        }
    }
}
// 有非常多需要优化的地方，比如说宿醉buff的检测，buff给予方面，用的if实在是有点多？
// 后续有时间有灵感了，会慢慢优化
