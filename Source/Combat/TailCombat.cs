using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NivarianIcecreamTail
// 这边是！冰淇淋尾巴用于战斗时，会用到的妙妙小代码！自动舔舔的job也放在这里。别问为什么不放core里头，因为舔尾巴也是战斗（误）
{
    [StaticConstructorOnStartup]
    internal static class TailCombatPatches
    {
        static TailCombatPatches()
        {
            new Harmony("dkdcakedangao.NivarianIcecreamTailQWQ").PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    internal static class TailCombatUtility
    {
        private const string TailGroupDefName = "Nivarian_Tail";
        private const string SlowDefName = "IcecreamTailSlow";

        public static bool IsMatureTailAttack(Pawn pawn, Tool tool)
        {
            return IcecreamTailMod.Enabled
                && IcecreamTailMod.Settings != null
                && IcecreamTailMod.Settings.EnableMatureTailCombat
                && tool != null
                && tool.linkedBodyPartsGroup != null
                && tool.linkedBodyPartsGroup.defName == TailGroupDefName
                && TailEatingUtility.IsReadyTailPawn(pawn);
        }

        public static IEnumerable<DamageInfo> AdjustTailDamage(Pawn pawn, Tool tool, IEnumerable<DamageInfo> damageInfos)
        {
            bool adjusted = false;
            foreach (DamageInfo damageInfo in damageInfos)
            {
                if (!adjusted && IsMatureTailAttack(pawn, tool) && damageInfo.Tool == tool)
                {
                    damageInfo.SetAmount(damageInfo.Amount * 1.2f);
                    adjusted = true;
                }

                yield return damageInfo;
            }
        }

        public static void ApplySlow(Pawn target)
        {
            if (target == null || target.health == null || !IcecreamTailMod.Enabled || IcecreamTailMod.Settings == null || !IcecreamTailMod.Settings.EnableMatureTailCombat)
            {
                return;
            }

            Hediff existing = GetSlow(target);
            if (existing != null)
            {
                target.health.RemoveHediff(existing);
            }

            HediffDef slowDef = DefDatabase<HediffDef>.GetNamedSilentFail(SlowDefName);
            if (slowDef != null)
            {
                target.health.AddHediff(HediffMaker.MakeHediff(slowDef, target));
            }
        }

        public static void RemoveAllSlowEffects()
        {
            foreach (Pawn pawn in TailEatingUtility.AllKnownPawns())
            {
                Hediff slow = GetSlow(pawn);
                if (slow != null && pawn.health != null)
                {
                    pawn.health.RemoveHediff(slow);
                }
            }
        }

        private static Hediff GetSlow(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            HediffDef slowDef = DefDatabase<HediffDef>.GetNamedSilentFail(SlowDefName);
            return slowDef == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(slowDef, false);
        }

        public static void SpawnSlowSnow(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_IcecreamTailSlowSnow");
            if (moteDef == null)
            {
                return;
            }

            Vector3 location = pawn.DrawPos;
            location.x += Rand.Range(-0.45f, 0.45f);
            location.z += Rand.Range(-0.45f, 0.45f);
            MoteThrown mote = MoteMaker.MakeStaticMote(location, pawn.Map, moteDef, Rand.Range(0.22f, 0.32f), false, Rand.Range(0f, 360f)) as MoteThrown;
            if (mote != null)
            {
                mote.SetVelocity(Rand.Range(55f, 125f), Rand.Range(0.01f, 0.025f));
            }
        }
    }

    //自动舔尾巴的job注入
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class Patch_JobGiver_GetFood_TryGiveJob
    {
        private const int MissingFoodRetryTicks = 60;
        private static readonly ConditionalWeakTable<Pawn, AutoLickRetryState> RetryStates = new ConditionalWeakTable<Pawn, AutoLickRetryState>();

        private sealed class AutoLickRetryState
        {
            public int NextScanTick;
        }

        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (!TailEatingUtility.IsEligibleAutomaticEater(pawn))
            {
                return;
            }

            // 没有普通食物时原版会返回空 Job；合格的小人仍需尝试寻找冰淇淋尾巴。
            if (__result != null && __result.def != JobDefOf.Ingest)
            {
                return;
            }

            bool missingVanillaFood = __result == null;
            AutoLickRetryState retryState = null;
            int currentTick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (missingVanillaFood)
            {
                retryState = RetryStates.GetValue(pawn, delegate { return new AutoLickRetryState(); });
                if (currentTick < retryState.NextScanTick)
                {
                    return;
                }
            }

            Job lickJob;
            string reason;
            if (TailEatingUtility.TryMakeAutomaticLickJob(pawn, out lickJob, out reason))
            {
                __result = lickJob;
                if (retryState != null)
                {
                    retryState.NextScanTick = 0;
                }

                if (IcecreamTailDebug.AutoLickEnabled)
                {
                    string result = missingVanillaFood ? "未找到普通食物，改为舔食" : "原版进食已替换";
                    IcecreamTailDebug.AutoLick(result + "：" + IcecreamTailDebug.PawnInfo(pawn) + " → " + IcecreamTailDebug.PawnInfo(lickJob.targetA.Pawn) + "，饱食度=" + pawn.needs.food.CurLevelPercentage.ToStringPercent() + "。 ");
                }

                return;
            }

            if (retryState != null)
            {
                retryState.NextScanTick = currentTick + MissingFoodRetryTicks;
            }

            if (IcecreamTailDebug.AutoLickEnabled && IcecreamTailMod.Enabled && IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.EnableAutoLick && pawn != null && pawn.needs != null && pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < IcecreamTailMod.Settings.AutoLickFoodThreshold)
            {
                string result = missingVanillaFood ? "未找到自动舔食目标" : "保留原版进食";
                IcecreamTailDebug.AutoLick(result + "：" + IcecreamTailDebug.PawnInfo(pawn) + "，原因=" + reason + "。 ");
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    internal static class Patch_VerbMeleeAttackDamage_DamageInfosToApply
    {
        private static void Postfix(Verb_MeleeAttackDamage __instance, Tool ___tool, ref IEnumerable<DamageInfo> __result)
        {
            if (TailCombatUtility.IsMatureTailAttack(__instance.CasterPawn, ___tool))
            {
                __result = TailCombatUtility.AdjustTailDamage(__instance.CasterPawn, ___tool, __result);
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    internal static class Patch_VerbMeleeAttackDamage_ApplyMeleeDamageToTarget
    {
        private static void Postfix(Verb_MeleeAttackDamage __instance, Tool ___tool, LocalTargetInfo target, DamageWorker.DamageResult __result)
        {
            Pawn targetPawn = target.Pawn;
            if (__result.totalDamageDealt > 0f && targetPawn != null && TailCombatUtility.IsMatureTailAttack(__instance.CasterPawn, ___tool))
            {
                TailCombatUtility.ApplySlow(targetPawn);
            }
        }
    }

    public sealed class HediffCompProperties_IcecreamTailSlowVfx : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailSlowVfx()
        {
            compClass = typeof(HediffComp_IcecreamTailSlowVfx);
        }
    }

    public sealed class HediffComp_IcecreamTailSlowVfx : HediffComp
    {
        private int nextMoteTick;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref nextMoteTick, "nextMoteTick", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!IcecreamTailMod.Enabled || IcecreamTailMod.Settings == null || !IcecreamTailMod.Settings.EnableMatureTailCombat)
            {
                parent.pawn.health.RemoveHediff(parent);
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick >= nextMoteTick)
            {
                TailCombatUtility.SpawnSlowSnow(parent.pawn);
                nextMoteTick = currentTick + 30;
            }
        }
    }
}
