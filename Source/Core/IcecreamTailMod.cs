using System.Collections.Generic;
using System.Linq;
using LudeonTK;
#if RIMWORLD_17
using NivarianVisualHelper = NivarianIcecreamTail.NivarianHostBridge;
#else
using Nivarian.Helper;
#endif
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
        public bool EnableNutmegHistoryEasterEgg = true;
        public bool EnableEasterEggSkillSounds = true;
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
            EnableNutmegHistoryEasterEgg = true;
            EnableEasterEggSkillSounds = true;
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
            Scribe_Values.Look(ref EnableNutmegHistoryEasterEgg, "EnableNutmegHistoryEasterEgg", true);
            Scribe_Values.Look(ref EnableEasterEggSkillSounds, "EnableEasterEggSkillSounds", true);
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

    // 普通主模块入口；1.7 动画适配在 Defs 就绪后单独初始化。
    public sealed class IcecreamTailMod : Mod
    {
#if RIMWORLD_17
        internal static ModContentPack ContentPack;
#endif
        public static IcecreamTailSettings Settings;
        private const float SectionWidth = 160f;
        private const float SectionGap = 12f;
        private const float HeaderHeight = 36f;
        private static readonly string[] SectionLabels = { "IcecreamTail_SectionBasic", "IcecreamTail_SectionRewards", "IcecreamTail_SectionRecovery", "IcecreamTail_SectionCombat", "IcecreamTail_SectionDebug", "IcecreamTail_SectionEasterEggs" };
        private int settingsPage;
        private Vector2 scrollPosition;

        // 准备 mod选项
        public IcecreamTailMod(ModContentPack content) : base(content)
        {
#if RIMWORLD_17
            ContentPack = content;
#endif
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
            return "IcecreamTail_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect sectionRect = new Rect(inRect.x, inRect.y, SectionWidth, inRect.height);
            Rect titleRect = new Rect(inRect.x + SectionWidth + SectionGap, inRect.y, inRect.width - SectionWidth - SectionGap, HeaderHeight);
            Rect contentViewRect = new Rect(inRect.x + SectionWidth + SectionGap, inRect.y + HeaderHeight, inRect.width - SectionWidth - SectionGap, inRect.height - HeaderHeight);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, SectionLabels[settingsPage].Translate());
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
                listing.CheckboxLabeled("IcecreamTail_Enabled".Translate().ToString(), ref Settings.Enabled, "IcecreamTail_EnabledTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_AutoLick".Translate().ToString(), ref Settings.EnableAutoLick, "IcecreamTail_AutoLickTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_Friendly".Translate().ToString(), ref Settings.SearchAllFriendly, "IcecreamTail_FriendlyTip".Translate().ToString());
                listing.Label("IcecreamTail_FoodThreshold".Translate(Settings.AutoLickFoodThreshold.ToStringPercent()));
                Settings.AutoLickFoodThreshold = listing.Slider(Settings.AutoLickFoodThreshold, 0.05f, 0.5f);
                listing.Label(Settings.SearchRadius > 150 ? "IcecreamTail_SearchMap".Translate() : "IcecreamTail_SearchRadius".Translate(Settings.SearchRadius));
                Settings.SearchRadius = (int)listing.Slider(Settings.SearchRadius, 30f, 151f);
                listing.Label("IcecreamTail_LickDuration".Translate(Settings.LickTimeMultiplier.ToString("0.0"), (10f * Settings.LickTimeMultiplier).ToString("0.0")));
                Settings.LickTimeMultiplier = listing.Slider(Settings.LickTimeMultiplier, 0.1f, 10f);
                listing.Gap(8f);
                if (Widgets.ButtonText(listing.GetRect(32f), "IcecreamTail_ResetDefaults".Translate().ToString()))
                {
                    Settings.ResetToDefaults();
                    BeerFourCombatSettings.Apply();
                }
            }
            else if (settingsPage == 1)
            {
                listing.CheckboxLabeled("IcecreamTail_Mood".Translate().ToString(), ref Settings.EnableMoodEffects, "IcecreamTail_MoodTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_Relationship".Translate().ToString(), ref Settings.EnableRelationshipEffects, "IcecreamTail_RelationshipTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_Buff".Translate().ToString(), ref Settings.EnableTailBuff, "IcecreamTail_BuffTip".Translate().ToString());
                listing.Label("IcecreamTail_BuffDuration".Translate(Settings.TailBuffDurationMultiplier.ToString("0.0"), (24f * Settings.TailBuffDurationMultiplier).ToString("0.0")));
                Settings.TailBuffDurationMultiplier = listing.Slider(Settings.TailBuffDurationMultiplier, 0.1f, 10f);
                listing.CheckboxLabeled("IcecreamTail_HalfVanilla".Translate().ToString(), ref Settings.HalveVanillaBuffDuration, "IcecreamTail_HalfVanillaTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_Hangover".Translate().ToString(), ref Settings.EnableBeerHangover, "IcecreamTail_HangoverTip".Translate().ToString());
            }
            else if (settingsPage == 2)
            {
                listing.CheckboxLabeled("IcecreamTail_CoreRecovery".Translate().ToString(), ref Settings.EnableCoreRecoveryEffect, "IcecreamTail_CoreRecoveryTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_Hunger".Translate().ToString(), ref Settings.EnableRecoveryHunger, "IcecreamTail_HungerTip".Translate().ToString());
                listing.Label("IcecreamTail_RecoveryDuration".Translate(Settings.RecoveryTimeMultiplier.ToString("0.0")));
                Settings.RecoveryTimeMultiplier = listing.Slider(Settings.RecoveryTimeMultiplier, 0.1f, 10f);
            }
            else if (settingsPage == 3)
            {
                listing.CheckboxLabeled("IcecreamTail_TailCombat".Translate().ToString(), ref Settings.EnableMatureTailCombat, "IcecreamTail_TailCombatTip".Translate().ToString());
#if RIMWORLD_17
                listing.CheckboxLabeled("IcecreamTail_Animation".Translate().ToString(), ref Settings.EnableSkill3MeleeAnimation, "IcecreamTail_Animation17Tip".Translate().ToString());
                if (NativeAnimationHost.UnavailableReason != null)
                    listing.Label("IcecreamTail_AnimationUnavailable".Translate(NativeAnimationHost.UnavailableReason));
#else
                listing.CheckboxLabeled("IcecreamTail_Animation".Translate().ToString(), ref Settings.EnableSkill3MeleeAnimation, "IcecreamTail_Animation16Tip".Translate().ToString());
#endif
                listing.Label("IcecreamTail_BeerMeleeCooldown".Translate(Settings.BeerFourMeleeCooldownFactor.ToString("0.00")));
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
                listing.CheckboxLabeled("IcecreamTail_LogTail".Translate().ToString(), ref Settings.LogTailStateDebug, "IcecreamTail_LogTailTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_LogFace".Translate().ToString(), ref Settings.LogFacialAnimationDebug, "IcecreamTail_LogFaceTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_LogAuto".Translate().ToString(), ref Settings.LogAutoLickDebug, "IcecreamTail_LogAutoTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_LogEgg".Translate().ToString(), ref Settings.LogEasterEggDebug, "IcecreamTail_LogEggTip".Translate().ToString());
                if (Prefs.DevMode)
                {
                    listing.CheckboxLabeled("IcecreamTail_DebugCooldown".Translate().ToString(), ref Settings.ShowTemporaryAbilityCooldownDebug, "IcecreamTail_DebugCooldownTip".Translate().ToString());
                }
            }
            else
            {
                listing.CheckboxLabeled("IcecreamTail_ChocolateEgg".Translate().ToString(), ref Settings.EnableChocolateWolfeinEasterEgg, "IcecreamTail_ChocolateEggTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_HistoryEgg".Translate().ToString(), ref Settings.EnableNutmegHistoryEasterEgg, "IcecreamTail_HistoryEggTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_EggSounds".Translate().ToString(), ref Settings.EnableEasterEggSkillSounds, "IcecreamTail_EggSoundsTip".Translate().ToString());
                listing.CheckboxLabeled("IcecreamTail_WinEgg".Translate().ToString(), ref Settings.EnableBeerWinEasterEgg, "IcecreamTail_WinEggTip".Translate().ToString());
                listing.Label("IcecreamTail_WinChance".Translate((Settings.BeerWinEasterEggChance * 100f).ToString("0")));
                Settings.BeerWinEasterEggChance = listing.Slider(Settings.BeerWinEasterEggChance, 0f, 1f);
                listing.CheckboxLabeled("IcecreamTail_FrozenEgg".Translate().ToString(), ref Settings.EnableFrozenTongueEasterEgg, "IcecreamTail_FrozenEggTip".Translate().ToString());
                listing.Label("IcecreamTail_FrozenTongueChance".Translate((Settings.FrozenTongueEasterEggChance * 100f).ToString("0.0")));
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
                    string label = SectionLabels[page].Translate();
                    NivarianVisualHelper.UIWithColor(Color.yellow, delegate { NivarianVisualHelper.DrawNivarianTechBtnBackground(rect, false); });
                    Widgets.ButtonText(rect, label, false, true, true, TextAnchor.MiddleCenter);
                }
                else if (NivarianVisualHelper.DrawNivarianTechBtn(rect, SectionLabels[page].Translate(), TextAnchor.MiddleCenter, true))
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

            return settingsPage == 2 ? 130f : (settingsPage == 5 ? 290f : 140f);
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
