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
    public static partial class TailEatingUtility
    {
        public static bool CanLickTail(Pawn eater, Pawn target, bool requireHungry, int radius, out string reason)
        {
            // 需要额外制作其他可能的种族扩展的兼容性
            // 万一什么新的扩展，加了个新的尾巴，那不就完蛋了？到时候再说！
            reason = null;
            if (!IcecreamTailMod.Enabled)
            {
                reason = "功能已关闭";
                return false;
            }

            if (eater == null || target == null)
            {
                reason = "目标无效";
                return false;
            }

            if (eater == target)
            {
                reason = "不能舔自己的尾巴";
                return false;
            }

            if (!eater.RaceProps.Humanlike)
            {
                reason = "只有人形角色会舔尾巴";
                return false;
            }

            if (eater.Dead || eater.Downed || eater.InMentalState)
            {
                reason = "当前无法行动";
                return false;
            }

            if (!IsReadyTailPawn(target))
            {
                reason = "没有可舔的冰淇淋尾巴";
                return false;
            }

            if (!eater.Spawned || !target.Spawned || eater.Map != target.Map)
            {
                reason = "不在同一张地图";
                return false;
            }

            if (eater.HostileTo(target) || target.HostileTo(eater))
            {
                reason = "关系不太友好";
                return false;
            }

            if (requireHungry && (eater.needs == null || eater.needs.food == null || eater.needs.food.CurCategory < HungerCategory.Hungry))
            {
                reason = "还不饿";
                return false;
            }

            if (radius > 0 && eater.Position.DistanceToSquared(target.Position) > radius * radius)
            {
                reason = "距离太远";
                return false;
            }

            if (!eater.CanReserveAndReach(target, PathEndMode.Touch, Danger.None, 1, -1, null, false))
            {
                reason = "无法到达或尾巴正被占用";
                return false;
            }

            return true;
        }

        public static void StartLicking(Pawn eater, Pawn target, bool playerForced)
        {
            string reason;
            if (!CanLickTail(eater, target, false, 0, out reason))
            {
                return;
            }

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(LickJobDefName);
            if (jobDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing LickIcecreamTail JobDef.");
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, target);
            job.playerForced = playerForced;
            if (playerForced)
            {
                eater.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
            }
            else
            {
                eater.jobs.StartJob(job, JobCondition.InterruptOptional, null, false, false, null, null, false, false, null, false, false, false);
            }
        }

        public static void HoldTailOwner(Pawn pawn, Pawn eater)
        {
            if (pawn == null || eater == null || pawn.jobs == null || pawn.Dead || pawn.Downed)
            {
                return;
            }

            JobDef waitJobDef = DefDatabase<JobDef>.GetNamedSilentFail(LickedJobDefName);
            if (waitJobDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing LickedIcecreamTail JobDef.");
                return;
            }

            Job waitJob = JobMaker.MakeJob(waitJobDef, eater);
            waitJob.expiryInterval = LickDurationTicks + 60;
            waitJob.playerForced = true;
            pawn.jobs.StartJob(waitJob, JobCondition.InterruptForced, null, false, false, null, null, false, false, null, false, false, false);
        }

        public static void InterruptLickingPartner(Pawn endingPawn, Pawn counterpart, JobDef expectedJobDef, Pawn expectedTarget)
        {
            if (endingPawn == null || counterpart == null || expectedJobDef == null || counterpart.jobs == null || counterpart.Dead || CancellingPawns.Contains(endingPawn) || CancellingPawns.Contains(counterpart))
            {
                return;
            }

            Job currentJob = counterpart.CurJob;
            if (currentJob == null || currentJob.def != expectedJobDef || (expectedTarget != null && currentJob.targetA.Pawn != expectedTarget))
            {
                return;
            }

            CancellingPawns.Add(endingPawn);
            CancellingPawns.Add(counterpart);
            try
            {
                counterpart.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            finally
            {
                CancellingPawns.Remove(endingPawn);
                CancellingPawns.Remove(counterpart);
            }
        }

        public static bool IsAutomaticEater(Pawn pawn)
        {
            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            if (settings == null || !settings.EnableAutoLick || pawn == null || !pawn.Spawned || !pawn.RaceProps.Humanlike || pawn.Downed || pawn.InMentalState || pawn.Drafted || pawn.needs == null || pawn.needs.food == null || pawn.needs.food.CurLevelPercentage >= settings.AutoLickFoodThreshold)
            {
                return false;
            }

            if (IcecreamTailMod.Settings.SearchAllFriendly)
            {
                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    return false;
                }
            }
            else if (pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }

            Job currentJob = pawn.CurJob;
            return currentJob == null || !currentJob.playerForced;
        }

        // 自动吃吃吃
        public static bool TryMakeAutomaticLickJob(Pawn eater, out Job job, out string reason)
        {
            job = null;
            if (!IsAutomaticEater(eater))
            {
                reason = "当前不适合自动进食";
                return false;
            }

            int radius = IcecreamTailMod.Settings.SearchRadius > 150 ? 0 : IcecreamTailMod.Settings.SearchRadius;
            Pawn nearestTarget = null;
            float nearestDistance = float.MaxValue;
            foreach (Pawn target in eater.Map.mapPawns.AllPawns)
            {
                string targetReason;
                if (!CanLickTail(eater, target, false, radius, out targetReason))
                {
                    continue;
                }

                float distance = eater.Position.DistanceToSquared(target.Position);
                if (distance < nearestDistance)
                {
                    nearestTarget = target;
                    nearestDistance = distance;
                }
            }

            if (nearestTarget == null)
            {
                reason = "没有可达且未被占用的成熟冰淇淋尾巴";
                return false;
            }

            JobDef lickJobDef = DefDatabase<JobDef>.GetNamedSilentFail(LickJobDefName);
            if (lickJobDef == null)
            {
                reason = "缺少舔尾巴 Job";
                Log.Error("Nivarian Icecream Tail: missing LickIcecreamTail JobDef.");
                return false;
            }

            job = JobMaker.MakeJob(lickJobDef, nearestTarget);
            reason = null;
            return true;
        }

        internal static IEnumerable<Pawn> AllKnownPawns()
        {
            HashSet<Pawn> pawns = new HashSet<Pawn>();
            if (Current.Game == null)
            {
                return pawns;
            }

            foreach (Map map in Find.Maps)
            {
                pawns.AddRange(map.mapPawns.AllPawns);
            }

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
            {
                pawns.Add(pawn);
            }

            return pawns;
        }

        // 除错
        // 完全移除就是必须的
        public static void RemoveAllTailStates()
        {
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("总开关关闭，正在清理全部尾巴状态。");
            }

            foreach (Pawn pawn in AllKnownPawns())
            {
                IcecreamTailUtility.RemovePlaceholder(pawn);
                RemoveRecovery(pawn);
                IcecreamTailFlavorUtility.RemoveTailBuffs(pawn);
                RemoveBeerHangover(pawn);
            }

            TailCombatUtility.RemoveAllSlowEffects();
        }

        public static void RemoveAllTailBuffs()
        {
            foreach (Pawn pawn in AllKnownPawns())
            {
                IcecreamTailFlavorUtility.RemoveTailBuffs(pawn);
            }
        }

        private static void RemoveBeerHangover(Pawn pawn)
        {
            Hediff hangover = GetHediff(pawn, BeerHangoverDefName);
            if (hangover != null && pawn != null && pawn.health != null)
            {
                pawn.health.RemoveHediff(hangover);
            }
        }

        public static void RemoveAllBeerHangovers()
        {
            foreach (Pawn pawn in AllKnownPawns())
            {
                RemoveBeerHangover(pawn);
            }
        }

        public static void RefreshAllRecoveryHungerEffects()
        {
            foreach (Pawn pawn in AllKnownPawns())
            {
                SyncRecoveryHunger(pawn);
            }
        }
    }
}
