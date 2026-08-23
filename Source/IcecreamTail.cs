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

    public sealed class IcecreamTailSettings : ModSettings
    {
        public bool Enabled = true;
        public bool SearchAllFriendly = true;
        public bool EnableMoodEffects = true;
        public bool EnableRelationshipEffects = true;
        public bool EnableTailBuff = true;
        public bool EnableCoreRecoveryEffect = true;
        public bool EnableRecoveryHunger = true;
        public bool EnableMatureTailCombat = true;
        public bool EnableAutoLick = true;
        public bool EnableChocolateWolfeinEasterEgg = true;
        public bool LogTailStateDebug;
        public bool LogFacialAnimationDebug;
        public bool LogAutoLickDebug;
        public bool LogEasterEggDebug;
        public float RecoveryTimeMultiplier = 1f;
        public float LickTimeMultiplier = 1f;
        public float TailBuffDurationMultiplier = 1f;
        public float AutoLickFoodThreshold = 0.2f;
        public int SearchRadius = 60;
        public bool HalveVanillaBuffDuration = true;

        public void ResetToDefaults()
        {
            Enabled = true;
            SearchAllFriendly = true;
            EnableMoodEffects = true;
            EnableRelationshipEffects = true;
            EnableTailBuff = true;
            EnableCoreRecoveryEffect = true;
            EnableRecoveryHunger = true;
            EnableMatureTailCombat = true;
            EnableAutoLick = true;
            EnableChocolateWolfeinEasterEgg = true;
            LogTailStateDebug = false;
            LogFacialAnimationDebug = false;
            LogAutoLickDebug = false;
            LogEasterEggDebug = false;
            RecoveryTimeMultiplier = 1f;
            LickTimeMultiplier = 1f;
            TailBuffDurationMultiplier = 1f;
            AutoLickFoodThreshold = 0.2f;
            SearchRadius = 60;
            HalveVanillaBuffDuration = true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "Enabled", true);
            Scribe_Values.Look(ref SearchAllFriendly, "SearchAllFriendly", true);
            Scribe_Values.Look(ref EnableMoodEffects, "EnableMoodEffects", true);
            Scribe_Values.Look(ref EnableRelationshipEffects, "EnableRelationshipEffects", true);
            Scribe_Values.Look(ref EnableTailBuff, "EnableTailBuff", true);
            Scribe_Values.Look(ref EnableCoreRecoveryEffect, "EnableCoreRecoveryEffect", true);
            Scribe_Values.Look(ref EnableRecoveryHunger, "EnableRecoveryHunger", true);
            Scribe_Values.Look(ref EnableMatureTailCombat, "EnableMatureTailCombat", true);
            Scribe_Values.Look(ref EnableAutoLick, "EnableAutoLick", true);
            Scribe_Values.Look(ref EnableChocolateWolfeinEasterEgg, "EnableChocolateWolfeinEasterEgg", true);
            Scribe_Values.Look(ref LogTailStateDebug, "LogTailStateDebug", false);
            Scribe_Values.Look(ref LogFacialAnimationDebug, "LogFacialAnimationDebug", false);
            Scribe_Values.Look(ref LogAutoLickDebug, "LogAutoLickDebug", false);
            Scribe_Values.Look(ref LogEasterEggDebug, "LogEasterEggDebug", false);
            Scribe_Values.Look(ref RecoveryTimeMultiplier, "RecoveryTimeMultiplier", 1f);
            Scribe_Values.Look(ref LickTimeMultiplier, "LickTimeMultiplier", 1f);
            Scribe_Values.Look(ref TailBuffDurationMultiplier, "TailBuffDurationMultiplier", 1f);
            Scribe_Values.Look(ref AutoLickFoodThreshold, "AutoLickFoodThreshold", 0.2f);
            Scribe_Values.Look(ref SearchRadius, "SearchRadius", 60);
            Scribe_Values.Look(ref HalveVanillaBuffDuration, "HalveVanillaBuffDuration", true);
        }
    }

    public sealed class IcecreamTailMod : Mod
    {
        public static IcecreamTailSettings Settings;
        private const float SectionWidth = 160f;
        private const float SectionGap = 12f;
        private const float HeaderHeight = 36f;
        private static readonly string[] SectionLabels = { "基础", "食用奖励", "恢复", "战斗", "调试", "彩蛋" };
        private int settingsPage;
        private Vector2 scrollPosition;

        // 准备 mod选项
        public IcecreamTailMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<IcecreamTailSettings>();
        }

        public static bool Enabled
        {
            get { return Settings == null || Settings.Enabled; }
        }


        // mod选项相关
        // 看到这个提醒我一下，逻辑图没有写着部分的逻辑，可能需要补一下
        // 简写了一下
        public override string SettingsCategory()
        {
            return LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.folderName.StartsWith("ChineseSimplified")
                ? "冰龙尾巴就是！冰淇淋！"
                : "Nivarian Icecream Tail QWQ";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect sectionRect = new Rect(inRect.x, inRect.y, SectionWidth, inRect.height);
            Rect titleRect = new Rect(inRect.x + SectionWidth + SectionGap, inRect.y, inRect.width - SectionWidth - SectionGap, HeaderHeight);
            Rect contentViewRect = new Rect(inRect.x + SectionWidth + SectionGap, inRect.y + HeaderHeight, inRect.width - SectionWidth - SectionGap, inRect.height - HeaderHeight);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, SectionLabels[settingsPage]);
            Text.Font = GameFont.Small;

            DrawSectionList(sectionRect);

            float contentHeight = Mathf.Max(contentViewRect.height, SettingsContentHeight());
            Rect contentRect = new Rect(0f, 0f, contentViewRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(contentViewRect, ref scrollPosition, contentRect, true);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(contentRect);
            bool wasEnabled = Settings.Enabled;
            bool wasBuffEnabled = Settings.EnableTailBuff;
            bool wasHungerEnabled = Settings.EnableRecoveryHunger;
            bool wasCombatEnabled = Settings.EnableMatureTailCombat;

            // 配置界面
            // 石山最密集的地方！
            // 待优化
            if (settingsPage == 0)
            {
                listing.CheckboxLabeled("启用冰淇淋尾巴", ref Settings.Enabled, "关闭后立即移除全部冰淇淋尾巴状态。 ");
                listing.CheckboxLabeled("小人会自动舔冰淇淋尾巴", ref Settings.EnableAutoLick, "仅当原版已找到普通食物、饱食度严格低于替代阈值且尾巴可用时，才会将本次进食替换为舔尾巴。 ");
                listing.CheckboxLabeled("友方也会自己来舔尾巴", ref Settings.SearchAllFriendly, "关闭后只会从己方小人中自动寻找食用者。 ");
                listing.Label("尾巴替代普通食物的饱食度阈值：" + Settings.AutoLickFoodThreshold.ToStringPercent());
                Settings.AutoLickFoodThreshold = listing.Slider(Settings.AutoLickFoodThreshold, 0.05f, 0.5f);
                listing.Label(Settings.SearchRadius > 150 ? "自动搜索范围：整张地图" : "自动搜索范围：" + Settings.SearchRadius + " 格");
                Settings.SearchRadius = (int)listing.Slider(Settings.SearchRadius, 30f, 151f);
                listing.Label("舔食时间倍率：" + Settings.LickTimeMultiplier.ToString("0.0") + "×（" + (10f * Settings.LickTimeMultiplier).ToString("0.0") + " 秒）");
                Settings.LickTimeMultiplier = listing.Slider(Settings.LickTimeMultiplier, 0.1f, 10f);
                listing.Gap(8f);
                if (Widgets.ButtonText(listing.GetRect(32f), "恢复全部默认设置"))
                {
                    Settings.ResetToDefaults();
                }
            }
            else if (settingsPage == 1)
            {
                listing.CheckboxLabeled("给予特殊心情", ref Settings.EnableMoodEffects, "食用者和冰龙都会获得对应心情记忆。 ");
                listing.CheckboxLabeled("关系变得更好", ref Settings.EnableRelationshipEffects, "食用者与冰龙会获得互相的好感记忆。 ");
                listing.CheckboxLabeled("给予增益buff", ref Settings.EnableTailBuff, "食用者获得一天的移动、射击和近战增益。 ");
                listing.Label("增益 buff 持续时间倍率：" + Settings.TailBuffDurationMultiplier.ToString("0.0") + "×（" + (24f * Settings.TailBuffDurationMultiplier).ToString("0.0") + " 小时）");
                Settings.TailBuffDurationMultiplier = listing.Slider(Settings.TailBuffDurationMultiplier, 0.1f, 10f);
                listing.CheckboxLabeled("香草味增益时间减半", ref Settings.HalveVanillaBuffDuration, "开启后，香草味冰淇淋尾巴增益仅持续其他口味的一半；同样受上方倍率影响。 ");
            }
            else if (settingsPage == 2)
            {
                listing.CheckboxLabeled("冰晶核心影响恢复", ref Settings.EnableCoreRecoveryEffect, "冰晶核心越稳定，冰淇淋尾巴恢复越快。 ");
                listing.CheckboxLabeled("恢复时更容易饿", ref Settings.EnableRecoveryHunger, "尾巴努力凝结冰淇淋时，冰龙饥饿速度变为 110%。 ");
                listing.Label("恢复时间倍率：" + Settings.RecoveryTimeMultiplier.ToString("0.0") + "×");
                Settings.RecoveryTimeMultiplier = listing.Slider(Settings.RecoveryTimeMultiplier, 0.1f, 10f);
            }
            else if (settingsPage == 3)
            {
                listing.CheckboxLabeled("冰淇淋尾巴战斗效果", ref Settings.EnableMatureTailCombat, "若冰龙若有完整的冰淇淋尾巴，会对敌人造成额外伤害，并减速敌人。 ");
            }
            else if (settingsPage == 4)
            {
                listing.CheckboxLabeled("记录尾巴状态与恢复日志", ref Settings.LogTailStateDebug, "在 Player.log 记录尾巴成熟、恢复、断尾和恢复期饥饿状态变化。 ");
                listing.CheckboxLabeled("记录动画兼容日志", ref Settings.LogFacialAnimationDebug, "在 Player.log 记录 Facial Animation 控制器、动画映射和播放请求结果。 ");
                listing.CheckboxLabeled("记录自动舔食检查日志", ref Settings.LogAutoLickDebug, "在 Player.log 记录原版找食物时，是否用尾巴替代这次进食。 ");
                listing.CheckboxLabeled("记录彩蛋日志", ref Settings.LogEasterEggDebug, "在 Player.log 记录巧克力尾巴与沃芬的彩蛋触发情况。 ");
            }
            else
            {
                listing.CheckboxLabeled("沃芬吃巧克力会中毒", ref Settings.EnableChocolateWolfeinEasterEgg, "沃芬舔食巧克力冰淇淋尾巴时，会获得约 10 分钟的严重食物中毒和特殊心情。 ");
            }

            listing.End();
            Widgets.EndScrollView();

            if (wasEnabled && !Settings.Enabled)
            {
                TailEatingUtility.RemoveAllTailStates();
            }

            if (wasBuffEnabled && !Settings.EnableTailBuff)
            {
                TailEatingUtility.RemoveAllTailBuffs();
            }

            if (wasHungerEnabled != Settings.EnableRecoveryHunger)
            {
                TailEatingUtility.RefreshAllRecoveryHungerEffects();
            }

            if (wasCombatEnabled && !Settings.EnableMatureTailCombat)
            {
                TailCombatUtility.RemoveAllSlowEffects();
            }
        }

        private void DrawSectionList(Rect sectionRect)
        {
            Listing_Standard sectionListing = new Listing_Standard();
            sectionListing.Begin(sectionRect);
            for (int page = 0; page < SectionLabels.Length; page++)
            {
                Rect rect = sectionListing.GetRect(30f, 1f);
                if (settingsPage == page)
                {
                    string label = SectionLabels[page];
                    NivarianVisualHelper.UIWithColor(Color.yellow, delegate { NivarianVisualHelper.DrawNivarianTechBtnBackground(rect, false); });
                    Widgets.ButtonText(rect, label, false, true, true, TextAnchor.MiddleCenter);
                }
                else if (NivarianVisualHelper.DrawNivarianTechBtn(rect, SectionLabels[page], TextAnchor.MiddleCenter, true))
                {
                    settingsPage = page;
                    scrollPosition = Vector2.zero;
                }

                sectionListing.Gap(4f);
            }

            sectionListing.End();
        }

        private float SettingsContentHeight()
        {
            if (settingsPage == 0)
            {
                return 280f;
            }

            if (settingsPage == 1)
            {
                return 160f;
            }

            return settingsPage == 2 || settingsPage == 4 ? 130f : 100f;
        }
    }

    internal static class IcecreamTailDebug
    {
        public static bool TailEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogTailStateDebug; }
        }

        public static bool FacialEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogFacialAnimationDebug; }
        }

        public static bool AutoLickEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogAutoLickDebug; }
        }

        public static bool EasterEggEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogEasterEggDebug; }
        }

        public static void Tail(string message)
        {
            if (TailEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][Tail] " + message);
            }
        }

        public static void Facial(string message)
        {
            if (FacialEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][Facial] " + message);
            }
        }

        public static void FacialWarning(string message)
        {
            if (FacialEnabled)
            {
                Log.Warning("[Nivarian Icecream Tail][Facial] " + message);
            }
        }

        public static void FacialError(string message)
        {
            if (FacialEnabled)
            {
                Log.Error("[Nivarian Icecream Tail][Facial] " + message);
            }
        }

        public static void AutoLick(string message)
        {
            if (AutoLickEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][AutoLick] " + message);
            }
        }

        public static void EasterEgg(string message)
        {
            if (EasterEggEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][EasterEgg] " + message);
            }
        }

        public static string PawnInfo(Pawn pawn)
        {
            if (pawn == null)
            {
                return "<null>";
            }

            return pawn.LabelShortCap + " (" + (pawn.def == null ? "<no def>" : pawn.def.defName) + ")";
        }
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

    public sealed class HediffCompProperties_IcecreamTailPlaceholder : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailPlaceholder()
        {
            compClass = typeof(HediffComp_IcecreamTailPlaceholder);
        }
    }

    public sealed class Hediff_IcecreamTailPlaceholder : HediffWithComps
    {
        public override string LabelBase
        {
            get { return IcecreamTailFlavorUtility.TailLabel(this, base.LabelBase); }
        }

        public override Color LabelColor
        {
            get { return IcecreamTailFlavorUtility.LabelColor(this); }
        }

        public override string TipStringExtra
        {
            get { return IcecreamTailFlavorUtility.ReadyTailDescription(this); }
        }
    }

    // buff引用
    // 待优化……
    public sealed class Hediff_IcecreamTailFlavorEaterBuff : HediffWithComps
    {
        public override Color LabelColor
        {
            get { return IcecreamTailFlavorUtility.BuffLabelColor(def); }
        }

        public override string TipStringExtra
        {
            get
            {
                TailEatingUtility.HediffComp_IcecreamTailBuffDuration duration = this.TryGetComp<TailEatingUtility.HediffComp_IcecreamTailBuffDuration>();
                string baseTip = base.TipStringExtra;
                if (duration == null)
                {
                    return baseTip;
                }

                // 似乎……没能正常实现呢……？
                // 现在实现了
                // 把批注当log写不是好习惯
                // 但是我喜欢！
                string durationTip = "剩余时间：" + duration.RemainingTicks.ToStringTicksToPeriod();
                return baseTip.NullOrEmpty() ? durationTip : baseTip + "\n" + durationTip;
            }
        }
    }

    public sealed class Hediff_IcecreamTailSlow : HediffWithComps
    {
        public override Color LabelColor
        {
            get { return IcecreamTailColors.LabelBlue; }
        }
    }

    internal static class IcecreamTailColors
    {
        public static readonly Color LabelBlue = new Color(0.56f, 0.94f, 1f);
    }

    public sealed class HediffComp_IcecreamTailPlaceholder : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            BodyPartRecord tailPart;
            if (!IcecreamTailMod.Enabled || IcecreamTailUtility.GetTailKind(parent.pawn, out tailPart) == TailKind.None || parent.Part != tailPart)
            {
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("成熟尾巴被清理：" + IcecreamTailDebug.PawnInfo(parent.pawn) + "，原因=" + (!IcecreamTailMod.Enabled ? "总开关关闭" : "尾巴不可用或部位已变更"));
                }

                parent.pawn.health.RemoveHediff(parent);
            }
        }
    }

    public static class IcecreamTailDebugActions
    {
        [DebugActionYielder]
        public static IEnumerable<DebugActionNode> DebugActions()
        {
            DebugActionNode action = new DebugActionNode("切换冰淇淋尾巴", DebugActionType.ToolMapForPawns, null, ToggleIcecreamTail);
            action.category = "Nivarian Icecream Tail";
            action.visibilityGetter = () => IcecreamTailMod.Enabled && Find.CurrentMap != null;
            yield return action;
        }

        private static void ToggleIcecreamTail(Pawn pawn)
        {
            if (!IcecreamTailMod.Enabled)
            {
                Messages.Message("冰淇淋尾巴功能已关闭。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            BodyPartRecord tailPart;
            if (!IcecreamTailUtility.IsNivarian(pawn))
            {
                Messages.Message("目标不是涅瓦莲。", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (IcecreamTailUtility.GetTailKind(pawn, out tailPart) == TailKind.None)
            {
                Messages.Message("目标没有可用尾巴。", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (IcecreamTailUtility.GetPlaceholder(pawn) == null)
            {
                TailEatingUtility.RemoveRecovery(pawn);
                IcecreamTailUtility.TryAddPlaceholder(pawn);
                Messages.Message("已添加冰淇淋尾巴。", pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                IcecreamTailUtility.RemovePlaceholder(pawn);
                Messages.Message("已移除冰淇淋尾巴。", pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
// 16，10，7，13 “我将会去买一箩筐的点心，麻利的送来医院”
