using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
                reason = "IcecreamTail_Disabled".Translate().ToString();
                return false;
            }

            if (eater == null || target == null)
            {
                reason = "IcecreamTail_InvalidTarget".Translate().ToString();
                return false;
            }

            if (eater == target)
            {
                reason = "IcecreamTail_OwnTail".Translate().ToString();
                return false;
            }

            if (!eater.RaceProps.Humanlike)
            {
                reason = "IcecreamTail_HumanlikeOnly".Translate().ToString();
                return false;
            }

            if (eater.Dead || eater.Downed || eater.InMentalState)
            {
                reason = "IcecreamTail_CannotAct".Translate().ToString();
                return false;
            }

            if (!IsReadyTailPawn(target))
            {
                reason = "IcecreamTail_NoTail".Translate().ToString();
                return false;
            }

            if (!eater.Spawned || !target.Spawned || eater.Map != target.Map)
            {
                reason = "IcecreamTail_DifferentMap".Translate().ToString();
                return false;
            }

            if (eater.HostileTo(target) || target.HostileTo(eater))
            {
                reason = "IcecreamTail_Unfriendly".Translate().ToString();
                return false;
            }

            if (requireHungry && (eater.needs == null || eater.needs.food == null || eater.needs.food.CurCategory < HungerCategory.Hungry))
            {
                reason = "IcecreamTail_NotHungry".Translate().ToString();
                return false;
            }

            if (radius > 0 && eater.Position.DistanceToSquared(target.Position) > radius * radius)
            {
                reason = "IcecreamTail_TooFar".Translate().ToString();
                return false;
            }

            if (!eater.CanReserveAndReach(target, PathEndMode.Touch, Danger.None, 1, -1, null, false))
            {
                reason = "IcecreamTail_Unreachable".Translate().ToString();
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

        internal static bool IsLickingOrBeingLicked(Pawn pawn)
        {
            Job currentJob = pawn == null ? null : pawn.CurJob;
            return currentJob != null && currentJob.def != null &&
                (currentJob.def.defName == LickJobDefName || currentJob.def.defName == LickedJobDefName);
        }

        public static bool IsAutomaticEater(Pawn pawn)
        {
            if (IsLickingOrBeingLicked(pawn))
            {
                return false;
            }

            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            if (settings == null || !settings.EnableAutoLick || !IsEligibleAutomaticEater(pawn) || !pawn.Spawned || pawn.Downed || pawn.InMentalState || pawn.Drafted || pawn.needs == null || pawn.needs.food == null || pawn.needs.food.CurLevelPercentage >= settings.AutoLickFoodThreshold)
            {
                return false;
            }

            Job currentJob = pawn.CurJob;
            return currentJob == null || !currentJob.playerForced;
        }

        public static bool IsEligibleAutomaticEater(Pawn pawn)
        {
            if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || pawn.IsPrisonerOfColony)
            {
                return false;
            }

            if (pawn.IsColonistPlayerControlled || pawn.IsSlaveOfColony)
            {
                return true;
            }

            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            Lord lord = pawn.GetLord();
            return settings != null
                && settings.SearchAllFriendly
                && lord != null
                && lord.LordJob is LordJob_VisitColony;
        }

        // 自动吃吃吃
        public static bool TryMakeAutomaticLickJob(Pawn eater, out Job job, out string reason)
        {
            job = null;
            if (!IsAutomaticEater(eater))
            {
                reason = "IcecreamTail_NoAutoEat".Translate().ToString();
                return false;
            }

            int radius = IcecreamTailMod.Settings.SearchRadius > 150 ? 0 : IcecreamTailMod.Settings.SearchRadius;
            Pawn nearestTarget = null;
            float nearestDistance = float.MaxValue;
            foreach (Pawn target in eater.Map.mapPawns.AllHumanlikeSpawned)
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
                reason = "IcecreamTail_NoAutoTail".Translate().ToString();
                return false;
            }

            JobDef lickJobDef = DefDatabase<JobDef>.GetNamedSilentFail(LickJobDefName);
            if (lickJobDef == null)
            {
                reason = "IcecreamTail_MissingJob".Translate().ToString();
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
