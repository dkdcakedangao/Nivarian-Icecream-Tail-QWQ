using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;

//入口，公共颜色
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailSettings : ModSettings
    {
        public bool Enabled = true;
        public bool SearchAllFriendly = true;
        public bool EnableMoodEffects = true;
        public bool EnableRelationshipEffects = true;
        public bool EnableTailBuff = true;
        public bool EnableBeerHangover = true;
        public bool EnableBeerWinEasterEgg = true;
        public bool EnableCoreRecoveryEffect = true;
        public bool EnableRecoveryHunger = true;
        public bool EnableMatureTailCombat = true;
        public bool EnableSkill3MeleeAnimation = true;
        public bool EnableAutoLick = true;
        public bool EnableChocolateWolfeinEasterEgg = true;
        public bool EnableFrozenTongueEasterEgg = true;
        public bool LogTailStateDebug;
        public bool LogFacialAnimationDebug;
        public bool LogAutoLickDebug;
        public bool LogEasterEggDebug;
        public bool ShowTemporaryAbilityCooldownDebug;
        public float RecoveryTimeMultiplier = 1f;
        public float LickTimeMultiplier = 1f;
        public float TailBuffDurationMultiplier = 1f;
        public float BeerWinEasterEggChance = 0.1f;
        public float FrozenTongueEasterEggChance = 0.01f;
        public float BeerFourMeleeCooldownFactor = 0.4f;
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
            EnableBeerHangover = true;
            EnableBeerWinEasterEgg = true;
            EnableCoreRecoveryEffect = true;
            EnableRecoveryHunger = true;
            EnableMatureTailCombat = true;
            EnableSkill3MeleeAnimation = true;
            EnableAutoLick = true;
            EnableChocolateWolfeinEasterEgg = true;
            EnableFrozenTongueEasterEgg = true;
            LogTailStateDebug = false;
            LogFacialAnimationDebug = false;
            LogAutoLickDebug = false;
            LogEasterEggDebug = false;
            ShowTemporaryAbilityCooldownDebug = false;
            RecoveryTimeMultiplier = 1f;
            LickTimeMultiplier = 1f;
            TailBuffDurationMultiplier = 1f;
            BeerWinEasterEggChance = 0.1f;
            FrozenTongueEasterEggChance = 0.01f;
            BeerFourMeleeCooldownFactor = 0.4f;
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
            Scribe_Values.Look(ref EnableBeerHangover, "EnableBeerHangover", true);
            Scribe_Values.Look(ref EnableBeerWinEasterEgg, "EnableBeerWinEasterEgg", true);
            Scribe_Values.Look(ref EnableCoreRecoveryEffect, "EnableCoreRecoveryEffect", true);
            Scribe_Values.Look(ref EnableRecoveryHunger, "EnableRecoveryHunger", true);
            Scribe_Values.Look(ref EnableMatureTailCombat, "EnableMatureTailCombat", true);
            Scribe_Values.Look(ref EnableSkill3MeleeAnimation, "EnableSkill3MeleeAnimation", true);
            Scribe_Values.Look(ref EnableAutoLick, "EnableAutoLick", true);
            Scribe_Values.Look(ref EnableChocolateWolfeinEasterEgg, "EnableChocolateWolfeinEasterEgg", true);
            Scribe_Values.Look(ref EnableFrozenTongueEasterEgg, "EnableFrozenTongueEasterEgg", true);
            Scribe_Values.Look(ref LogTailStateDebug, "LogTailStateDebug", false);
            Scribe_Values.Look(ref LogFacialAnimationDebug, "LogFacialAnimationDebug", false);
            Scribe_Values.Look(ref LogAutoLickDebug, "LogAutoLickDebug", false);
            Scribe_Values.Look(ref LogEasterEggDebug, "LogEasterEggDebug", false);
            Scribe_Values.Look(ref ShowTemporaryAbilityCooldownDebug, "ShowTemporaryAbilityCooldownDebug", false);
            Scribe_Values.Look(ref RecoveryTimeMultiplier, "RecoveryTimeMultiplier", 1f);
            Scribe_Values.Look(ref LickTimeMultiplier, "LickTimeMultiplier", 1f);
            Scribe_Values.Look(ref TailBuffDurationMultiplier, "TailBuffDurationMultiplier", 1f);
            Scribe_Values.Look(ref BeerWinEasterEggChance, "BeerWinEasterEggChance", 0.1f);
            Scribe_Values.Look(ref FrozenTongueEasterEggChance, "FrozenTongueEasterEggChance", 0.01f);
            Scribe_Values.Look(ref BeerFourMeleeCooldownFactor, "BeerFourMeleeCooldownFactor", 0.4f);
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
            LongEventHandler.ExecuteWhenFinished(BeerFourCombatSettings.Apply);
        }

        public static bool Enabled
        {
            get { return Settings == null || Settings.Enabled; }
        }


        // mod选项相关
        // 看到这个提醒我一下，逻辑图没有写着部分的逻辑，可能需要补一下
        // 简写了一下
        // kp的代码抄起来就是舒服喵~
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
            bool wasBeerHangoverEnabled = Settings.EnableBeerHangover;
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
                    BeerFourCombatSettings.Apply();
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
                listing.CheckboxLabeled("四酒结束后会宿醉", ref Settings.EnableBeerHangover, "开启后，四酒自然结束会产生一天宿醉；宿醉期间无法再次获得啤酒口味专属效果。 ");
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
                listing.CheckboxLabeled("开关“月牙叉炮(CA)”的动画", ref Settings.EnableSkill3MeleeAnimation, "开关“月牙叉炮(CA)”的动画，该动画需要有MeleeAnimation作为前置。若关闭，或没有MeleeAnimation模组，则跳过动画，简单播放一个流程(?非常简易的流程)。若你不喜欢释放技能时，有10秒钟动画的话，或是占用过大之类的（大概是不会），可以关闭该选项");
                listing.Label("四酒 buff 近战冷却倍率：" + Settings.BeerFourMeleeCooldownFactor.ToString("0.00") + "×");
                float beerFourMeleeCooldownFactor = listing.Slider(Settings.BeerFourMeleeCooldownFactor, 0.1f, 1f);
                float roundedFactor = Mathf.Round(beerFourMeleeCooldownFactor / 0.05f) * 0.05f;
                if (!Mathf.Approximately(Settings.BeerFourMeleeCooldownFactor, roundedFactor))
                {
                    Settings.BeerFourMeleeCooldownFactor = roundedFactor;
                    BeerFourCombatSettings.Apply();
                }
            }
            else if (settingsPage == 4)
            {
                listing.CheckboxLabeled("记录尾巴状态与恢复日志", ref Settings.LogTailStateDebug, "在 Player.log 记录尾巴成熟、恢复、断尾和恢复期饥饿状态变化。 ");
                listing.CheckboxLabeled("记录动画兼容日志", ref Settings.LogFacialAnimationDebug, "在 Player.log 记录 Facial Animation 控制器、动画映射和播放请求结果。 ");
                listing.CheckboxLabeled("记录自动舔食检查日志", ref Settings.LogAutoLickDebug, "在 Player.log 记录原版找食物时，是否用尾巴替代这次进食。 ");
                listing.CheckboxLabeled("记录彩蛋日志", ref Settings.LogEasterEggDebug, "在 Player.log 记录巧克力尾巴、啤酒四酒与冻住舌头彩蛋触发情况。 ");
                if (Prefs.DevMode)
                {
                    listing.CheckboxLabeled("显示临时技能冷却 Debug 按钮", ref Settings.ShowTemporaryAbilityCooldownDebug, "在已征召的四酒涅瓦莲 Gizmo 栏显示按钮；只清空本模组临时技能冷却。 ");
                }
            }
            else
            {
                listing.CheckboxLabeled("沃芬吃巧克力会中毒", ref Settings.EnableChocolateWolfeinEasterEgg, "沃芬舔食巧克力冰淇淋尾巴时，会获得约 10 分钟的严重食物中毒和特殊心情。 ");
                listing.CheckboxLabeled("啤酒四酒 WIN 彩蛋", ref Settings.EnableBeerWinEasterEgg, "仅涅瓦莲从三酒升到四酒时判定；触发后显示 WIN 图片并播放音效。 ");
                listing.Label("啤酒四酒 WIN 彩蛋概率：" + (Settings.BeerWinEasterEggChance * 100f).ToString("0") + "%");
                Settings.BeerWinEasterEggChance = listing.Slider(Settings.BeerWinEasterEggChance, 0f, 1f);
                listing.CheckboxLabeled("开关冻住舌头彩蛋", ref Settings.EnableFrozenTongueEasterEgg, "若关闭，则不会触发冻住舌头的彩蛋");
                listing.Label("舔冰淇淋尾巴时，被冻住舌头的概率：" + (Settings.FrozenTongueEasterEggChance * 100f).ToString("0.0") + "%");
                Settings.FrozenTongueEasterEggChance = Mathf.Clamp01(listing.Slider(Settings.FrozenTongueEasterEggChance, 0f, 1f));
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

            if (wasBeerHangoverEnabled && !Settings.EnableBeerHangover)
            {
                TailEatingUtility.RemoveAllBeerHangovers();
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
                return 190f;
            }

            if (settingsPage == 4)
            {
                return Prefs.DevMode ? 190f : 130f;
            }

            return settingsPage == 2 ? 130f : (settingsPage == 5 ? 230f : 140f);
        }
    }

    // "为什么要放在这里？"放在这里挺好
    // 我说啊，就你写到后面，重新开始拆分的时候，就一下子不知道，为什么文字颜色，要放在选项和健康hediff中间了。
    // 正常拆分的话，应该是把文字颜色，放到FlavorSystem.cs，口味和hediff颜色，对吧？但是！我不要！
    // 健康栏中冰淇淋尾巴、恢复状态、食用 Buff 和减速 Hediff 使用的默认冰蓝色（大概?)
    // 才不是已经完全不记得这到底是不是文字颜色，是不是之前代码没删干净，删掉有没有影响之类的……
    internal static class IcecreamTailColors
    {
        public static readonly Color LabelBlue = new Color(0.56f, 0.94f, 1f);
    }

}
