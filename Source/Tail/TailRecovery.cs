using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace NivarianIcecreamTail
// 很难想象，我实在是找不到原版游戏的那种饥饿速率是怎么写的，总之是用了神秘的小思路，写了个奇妙的小简介。可能……大概……肯定……是要优化的吧？
{
    public sealed class Hediff_IcecreamTailRecovery : HediffWithComps
    {
        public override string LabelBase
        {
            get { return IcecreamTailFlavorUtility.TailLabel(this, "冰淇淋尾巴") + "恢复中（" + Severity.ToStringPercent() + "）"; }
        }

        public override string TipStringExtra
        {
            get
            {
                string tip = "- 恢复进度：" + Severity.ToStringPercent();
                if (IcecreamTailMod.Settings == null || IcecreamTailMod.Settings.EnableRecoveryHunger)
                {
                    tip += "\n- 饥饿速度：110%";
                }

                return tip;
            }
        }

        public override UnityEngine.Color LabelColor
        {
            get { return IcecreamTailFlavorUtility.LabelColor(this); }
        }
    }

    public sealed class Hediff_IcecreamTailRecoveryHunger : HediffWithComps
    {
        public override bool Visible
        {
            get { return false; }
        }
    }

    public sealed class HediffCompProperties_IcecreamTailRecovery : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailRecovery()
        {
            compClass = typeof(HediffComp_IcecreamTailRecovery);
        }
    }

    public sealed class HediffComp_IcecreamTailRecovery : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            BodyPartRecord tailPart;
            if (!IcecreamTailMod.Enabled || IcecreamTailUtility.GetTailKind(parent.pawn, out tailPart) == TailKind.None || parent.Part != tailPart)
            {
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("恢复被取消：" + IcecreamTailDebug.PawnInfo(parent.pawn) + "，原因=" + (!IcecreamTailMod.Enabled ? "总开关关闭" : "尾巴不可用或部位已变更"));
                }

                TailEatingUtility.RemoveRecovery(parent.pawn);
                return;
            }

            TailEatingUtility.SyncRecoveryHunger(parent.pawn);
            float days = TailEatingUtility.RecoveryDays(parent.pawn) * IcecreamTailMod.Settings.RecoveryTimeMultiplier;
            float increase = delta / (60000f * days) * TailEatingUtility.CoreRecoveryFactor(parent.pawn) * IcecreamTailFlavorUtility.RecoverySpeedFactor(parent);
            if (parent.Severity + increase >= 1f)
            {
                TailEatingUtility.FinishRecovery(parent.pawn, parent);
                return;
            }

            severityAdjustment += increase;
        }
    }

    public static partial class TailEatingUtility
    {
        private const string RecoveryDefName = "IcecreamTailRecovery";
        private const string HungerDefName = "IcecreamTailRecoveryHunger";
        private const string IcyCoreDefName = "Nivarian_IcyCore";
        private const string EaterMoodThoughtDefName = "IcecreamTailMemoryEater";
        private const string OwnerMoodThoughtDefName = "IcecreamTailMemoryOwner";
        private const string EaterSocialThoughtDefName = "IcecreamTailSocialEater";
        private const string OwnerSocialThoughtDefName = "IcecreamTailSocialOwner";
        private const string ChocolateFlavorDefName = "IcecreamTailFlavorChocolate";
        private const string BeerFlavorDefName = "IcecreamTailFlavorBeer";
        private const string VanillaFlavorDefName = "IcecreamTailFlavorVanilla";
        private const string WolfeinPawnDefName = "Wolfein_Race";
        private const string FoodPoisoningDefName = "FoodPoisoning";
        private const string CryoSlowDefName = "Nivarian_Hediff_CryoSlow";
        private const string WolfeinChocolateThoughtDefName = "IcecreamTailMemoryWolfeinChocolate";
        private const float WolfeinChocolateFoodPoisoningSeverity = 0.6f;
        private const string BeerBuffDefName = "IcecreamTailBeerEaterBuff";
        private const string BeerHangoverDefName = "IcecreamTailBeerHangover";
        private const string AlcoholHighDefName = "AlcoholHigh";
        private const float BeerAlcoholSeverity = 0.15f;
        private const string LickJobDefName = "LickIcecreamTail";
        private const string LickedJobDefName = "LickedIcecreamTail";
        private const int BaseLickDurationTicks = 600;
        private const int BaseTailBuffDurationTicks = 60000;
        private const int BeerTailBuffDurationTicks = 15000;
        private static readonly HashSet<Pawn> CancellingPawns = new HashSet<Pawn>();

        public static int LickDurationTicks
        {
            get
            {
                float multiplier = IcecreamTailMod.Settings == null ? 1f : Math.Max(0.1f, Math.Min(10f, IcecreamTailMod.Settings.LickTimeMultiplier));
                return (int)Math.Round(BaseLickDurationTicks * multiplier);
            }
        }

        private static Hediff GetHediff(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            return pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def.defName == defName);
        }

        public static Hediff GetRecovery(Pawn pawn)
        {
            return GetHediff(pawn, RecoveryDefName);
        }

        public static bool IsReadyTailPawn(Pawn pawn)
        {
            BodyPartRecord tailPart;
            return IcecreamTailMod.Enabled && IcecreamTailUtility.GetTailKind(pawn, out tailPart) != TailKind.None && IcecreamTailUtility.GetPlaceholder(pawn) != null && GetRecovery(pawn) == null;
        }

        public static float RecoveryDays(Pawn pawn)
        {
            BodyPartRecord tailPart;
            TailKind kind = IcecreamTailUtility.GetTailKind(pawn, out tailPart);
            if (kind == TailKind.Bionic)
            {
                return 0.8f;
            }

            return kind == TailKind.Fake ? 1.5f : 1f;
        }

        public static float CoreRecoveryFactor(Pawn pawn)
        {
            if (!IcecreamTailMod.Settings.EnableCoreRecoveryEffect)
            {
                return 1f;
            }

            Hediff core = GetHediff(pawn, IcyCoreDefName);
            if (core == null)
            {
                return 1f;
            }

            if (core.Severity < 0.4f)
            {
                return 1.25f;
            }

            if (core.Severity < 0.7f)
            {
                return 1.1f;
            }

            if (core.Severity < 0.85f)
            {
                return 1f;
            }

            if (core.Severity < 0.95f)
            {
                return 0.75f;
            }

            return 0.5f;
        }

        public static void StartRecovery(Pawn pawn, IcecreamTailFlavorDef flavor)
        {
            BodyPartRecord tailPart;
            if (pawn == null || !IcecreamTailMod.Enabled || IcecreamTailUtility.GetTailKind(pawn, out tailPart) == TailKind.None || GetRecovery(pawn) != null)
            {
                return;
            }

            HediffDef recoveryDef = DefDatabase<HediffDef>.GetNamedSilentFail(RecoveryDefName);
            if (recoveryDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing IcecreamTailRecovery HediffDef.");
                return;
            }

            Hediff recovery = HediffMaker.MakeHediff(recoveryDef, pawn, tailPart);
            recovery.Severity = 0.001f;
            IcecreamTailFlavorUtility.SetFlavor(recovery, flavor);
            pawn.health.AddHediff(recovery, tailPart);
            if (IcecreamTailDebug.TailEnabled)
            {
                float baseDays = RecoveryDays(pawn);
                float coreFactor = CoreRecoveryFactor(pawn);
                float flavorFactor = IcecreamTailFlavorUtility.RecoverySpeedFactor(recovery);
                float finalDays = baseDays * IcecreamTailMod.Settings.RecoveryTimeMultiplier / coreFactor / flavorFactor;
                IcecreamTailDebug.Tail("恢复开始：" + IcecreamTailDebug.PawnInfo(pawn) + "，尾巴类型=" + IcecreamTailUtility.GetTailKind(pawn, out tailPart) + "，口味=" + IcecreamTailFlavorUtility.GetFlavor(recovery).label + "，基础=" + baseDays.ToString("0.0") + " 天，时间倍率=" + IcecreamTailMod.Settings.RecoveryTimeMultiplier.ToString("0.0") + "，核心倍率=" + coreFactor.ToString("0.00") + "，口味倍率=" + flavorFactor.ToString("0.00") + "，预计=" + finalDays.ToString("0.00") + " 天。");
            }

            SyncRecoveryHunger(pawn);
        }

        public static void FinishRecovery(Pawn pawn, Hediff recovery)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            IcecreamTailFlavorDef flavor = IcecreamTailFlavorUtility.GetFlavor(recovery);
            if (recovery != null)
            {
                pawn.health.RemoveHediff(recovery);
            }

            RemoveRecoveryHunger(pawn);
            if (IcecreamTailMod.Enabled)
            {
                IcecreamTailUtility.TryAddPlaceholder(pawn, flavor);
            }
            
            // 冰淇淋尾巴长好了，到底要叫什么？成熟？？结冰？反正debug，没人看的，就写成熟吧
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("恢复完成：" + IcecreamTailDebug.PawnInfo(pawn) + "，已尝试恢复成熟冰淇淋尾巴。");
            }
        }

        public static void RemoveRecovery(Pawn pawn)
        {
            Hediff recovery = GetRecovery(pawn);
            if (recovery != null && pawn.health != null)
            {
                pawn.health.RemoveHediff(recovery);
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已移除恢复状态：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }

            RemoveRecoveryHunger(pawn);
        }

        public static void SyncRecoveryHunger(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || !IcecreamTailMod.Enabled || !IcecreamTailMod.Settings.EnableRecoveryHunger || GetRecovery(pawn) == null)
            {
                RemoveRecoveryHunger(pawn);
                return;
            }

            if (GetHediff(pawn, HungerDefName) != null)
            {
                return;
            }

            HediffDef hungerDef = DefDatabase<HediffDef>.GetNamedSilentFail(HungerDefName);
            if (hungerDef != null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(hungerDef, pawn));
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已添加恢复期饥饿速度效果：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }
        }

        private static void RemoveRecoveryHunger(Pawn pawn)
        {
            Hediff hunger = GetHediff(pawn, HungerDefName);
            if (hunger != null && pawn != null && pawn.health != null)
            {
                pawn.health.RemoveHediff(hunger);
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已移除恢复期饥饿速度效果：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }
        }

    }
}
