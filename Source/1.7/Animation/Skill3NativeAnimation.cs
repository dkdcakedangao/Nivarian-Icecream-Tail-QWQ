using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

// 技能3，也就是CA动画的读取，基本上就是为了这个，才自己写的框架……
// job、状态、查询，以及伤害保护和除错，都在这里
namespace NivarianIcecreamTail
{
    [HarmonyPatch(typeof(Game), "Dispose")]
    internal static class Patch_CharacterAnimation_GameDisposed
    {
        private static void Prefix() { IcecreamTailSkill3Runtime.ClearNativeRuntime(); }
    }

    [StaticConstructorOnStartup]
    internal static class NativeCharacterAnimationBootstrap
    {
        static NativeCharacterAnimationBootstrap()
        {
            Skill3AnimationBridge.Register(new NativeSkill3AnimationProvider());
            LongEventHandler.ExecuteWhenFinished(NativeAnimationHost.Initialize);
        }
    }

    internal sealed class NativeSkill3AnimationProvider : ISkill3AnimationProvider
    {
        public bool Prepare(Pawn caster, Pawn target) { return true; }
        public bool TryStart(Pawn caster, Pawn target) { return IcecreamTailSkill3Runtime.StartNativeAnimation(caster, target); }
        public Skill3AnimationStatus GetStatus(Pawn caster, Pawn target) { return IcecreamTailSkill3Runtime.NativeAnimationStatus(caster, target); }
        public void Cancel(Pawn caster, Pawn target) { IcecreamTailSkill3Runtime.ReleaseNativeAnimation(caster); }
    }

    internal static partial class IcecreamTailSkill3Runtime
    {
        internal static bool HasNativeProtection { get { return Protected.Count != 0; } }
        internal static void RememberDeferredDeath(Pawn pawn, DamageInfo? damage, Hediff culprit)
        {
            IcecreamTailSkill3State state;
            if (damage.HasValue && InternalDamageAllowed.Contains(pawn) && Protected.TryGetValue(pawn, out state) &&
                pawn == state.target && state.deferredDeath == null)
                state.deferredDeath = new Skill3DeferredDeath(damage.Value, culprit);
        }

        private static void ResolveNativeDamage(IcecreamTailSkill3State state, bool melee, bool finish)
        {
            state.resolvingDamage = true;
            try
            {
                if (melee && finish) ResolveAllDamage(state);
                else if (melee) ResolveMeleeHits(state);
                else if (finish) ResolveExplosionAndFinisher(state);
            }
            finally
            {
                bool interrupted = state.casterLockJob == null || state.targetLockJob == null ||
                    !ReferenceEquals(state.caster.CurJob, state.casterLockJob) ||
                    !ReferenceEquals(state.target.CurJob, state.targetLockJob);
                try
                {
                    if ((finish || interrupted) && IsLiveTarget(state))
                        using (EnterDeathAllowed(state.target)) state.target.health.CheckForStateChange(
                            state.deferredDeath == null ? (DamageInfo?)null : state.deferredDeath.Damage,
                            state.deferredDeath == null ? null : state.deferredDeath.Culprit);
                }
                finally
                {
                    state.resolvingDamage = false;
                    if (state.casterLockJob == null || state.targetLockJob == null ||
                        !ReferenceEquals(state.caster.CurJob, state.casterLockJob) ||
                        !ReferenceEquals(state.target.CurJob, state.targetLockJob)) Cancel(state.caster);
                }
            }
        }

        internal static void TickFromNativeLock(Pawn pawn, Job lockJob)
        {
            IcecreamTailSkill3State state;
            if (!Active.TryGetValue(pawn, out state) || !ReferenceEquals(lockJob, state.casterLockJob)) return;
            try { Tick(pawn); }
            catch (Exception error)
            {
                Log.ErrorOnce("[Nivarian Icecream Tail] Character skill cancelled: " + error, 1978042332 ^ pawn.thingIDNumber);
                Cancel(pawn);
            }
        }

        internal static void OnNativeLockEnded(Pawn pawn, Job job)
        {
            IcecreamTailSkill3State state;
            if (!Protected.TryGetValue(pawn, out state) ||
                !ReferenceEquals(job, pawn == state.caster ? state.casterLockJob : state.targetLockJob)) return;
            if (pawn == state.caster) state.casterLockJob = null;
            else state.targetLockJob = null;
            Cancel(state.caster);
        }

        internal static bool StartNativeAnimation(Pawn caster, Pawn target)
        {
            IcecreamTailSkill3State state;
            if (!Active.TryGetValue(caster, out state) || state.target != target) return false;
            PawnAnimationDef def = DefDatabase<PawnAnimationDef>.GetNamedSilentFail("IcecreamTailAnimationSkill3");
            state.animation = NativeAnimationHost.Start(def, caster, target, 0f, 0,
                target.Position.x < caster.Position.x, caster.Position.ToVector3ShiftedWithAltitude(caster.DrawPos.y));
            return state.animation != null;
        }

        internal static Skill3AnimationStatus NativeAnimationStatus(Pawn caster, Pawn target)
        {
            IcecreamTailSkill3State state;
            if (!Active.TryGetValue(caster, out state) || state.target != target || state.animation == null)
                return Skill3AnimationStatus.Interrupted;
            try
            {
                if (state.animation.Finished) return Skill3AnimationStatus.Completed;
                if (state.animation.Alive) return Skill3AnimationStatus.Running;
                throw new InvalidOperationException("Animation playback handle became invalid");
            }
            catch (Exception error) { NativeAnimationHost.Disable(error); return Skill3AnimationStatus.Interrupted; }
        }

        internal static void ReleaseNativeAnimation(Pawn caster)
        {
            IcecreamTailSkill3State state;
            if (caster != null && Active.TryGetValue(caster, out state)) ReleaseNativeSession(state);
        }

        private static void ReleaseNativeSession(IcecreamTailSkill3State state)
        {
            if (state.animation != null)
            {
                var animation = state.animation;
                state.animation = null;
                try { animation.Dispose(); }
                catch (Exception error) { NativeAnimationHost.Disable(error); }
            }
        }

        private static void DropNativeHostileJobs(Pawn caster, Pawn target)
        {
            foreach (Pawn pawn in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.jobs == null || IsProtected(pawn)) continue;
                Job job = pawn.CurJob;
                if (job != null && ((pawn.HostileTo(caster) && job.AnyTargetIs(caster)) ||
                    (pawn.HostileTo(target) && job.AnyTargetIs(target)))) pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
            }
        }

        internal static void ClearNativeRuntime()
        {
            // Dispose may invoke framework callbacks. Detach before calling it, and
            // remove ownership first so old callbacks cannot cancel newer jobs.
            Exception failure = null;
            try
            {
                foreach (Pawn caster in new List<Pawn>(Active.Keys))
                {
                    try { Cancel(caster); }
                    catch (Exception error) { if (failure == null) failure = error; }
                }
            }
            finally { Active.Clear(); Protected.Clear(); InternalDamageAllowed.Clear(); DeathAllowed.Clear(); }
            if (failure != null)
                Log.Warning("[Nivarian Icecream Tail] Character runtime cleanup encountered an error; remaining sessions were processed: " + failure);
        }

        internal static void CancelNativeAnimations()
        {
            Exception failure = null;
            foreach (Pawn caster in new List<Pawn>(Active.Keys))
            {
                IcecreamTailSkill3State active;
                try { if (Active.TryGetValue(caster, out active) && active.animation != null) Cancel(caster); }
                catch (Exception error) { if (failure == null) failure = error; }
            }
            if (failure != null) throw new InvalidOperationException("Character skill cleanup failed", failure);
        }

        internal static void ExposeData(ref List<IcecreamTailSkill3SavedState> saved)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saved = new List<IcecreamTailSkill3SavedState>(Active.Count);
                foreach (var state in Active.Values)
                {
                    var animation = state.animation;
                    saved.Add(new IcecreamTailSkill3SavedState {
                        caster = state.caster, target = state.target, startCell = state.startCell, landingCell = state.landingCell,
                        impactCell = state.impactCell, phase = (int)state.phase, moveElapsedTicks = state.moveElapsedTicks,
                        moveTotalTicks = state.moveTotalTicks, ticksRemaining = state.ticksRemaining, pathCells = state.pathCells,
                        animationDef = animation == null ? null : animation.Def.defName,
                        animationTime = animation == null ? 0f : animation.Elapsed, soundCursor = animation == null ? 0 : animation.SoundCursor,
                        mirror = animation != null && animation.Mirror, animationRoot = animation == null ? default(UnityEngine.Vector3) : animation.RootPosition,
                        damageResolved = state.damageResolved, deferredDeath = state.deferredDeath
                    });
                }
            }
            Scribe_Collections.Look(ref saved, "icecreamTailSkill3States", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) saved = null;
        }

        internal static void RestoreNativeStates(List<IcecreamTailSkill3SavedState> saved)
        {
            ClearNativeRuntime();
            if (saved == null) return;
            foreach (var item in saved)
            {
                string reason;
                bool valid = item.caster != null && item.target != null && item.caster != item.target &&
                    !item.caster.Dead && !item.caster.Downed && item.caster.Spawned && !item.target.Dead && !item.target.Downed && item.target.Spawned &&
                    item.caster.Map == item.target.Map && !IsProtected(item.caster) && !IsProtected(item.target) &&
                    IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(item.caster, out reason) &&
                    item.phase >= 0 && item.phase <= (int)IcecreamTailSkill3Phase.Animating && !item.damageResolved &&
                    HasNativeLock(item.caster, item.target) && HasNativeLock(item.target, item.caster) &&
                    (item.phase != (int)IcecreamTailSkill3Phase.Animating ||
                     IcecreamTailMod.Settings == null || IcecreamTailMod.Settings.EnableSkill3MeleeAnimation);
                if (!valid) { EndSavedLock(item.caster, item.target); EndSavedLock(item.target, item.caster); continue; }
                var state = new IcecreamTailSkill3State {
                    caster = item.caster, target = item.target, startCell = item.startCell, landingCell = item.landingCell,
                    impactCell = item.impactCell, phase = (IcecreamTailSkill3Phase)item.phase,
                    moveElapsedTicks = item.moveElapsedTicks, moveTotalTicks = item.moveTotalTicks,
                    ticksRemaining = item.ticksRemaining, pathCells = item.pathCells ?? new List<IntVec3>(),
                    casterLockJob = item.caster.CurJob, targetLockJob = item.target.CurJob, deferredDeath = item.deferredDeath
                };
                Active.Add(state.caster, state); Protected.Add(state.caster, state); Protected.Add(state.target, state);
                try
                {
                    if (state.phase == IcecreamTailSkill3Phase.Animating)
                    {
                        var def = DefDatabase<PawnAnimationDef>.GetNamedSilentFail(item.animationDef ?? "");
                        state.animation = NativeAnimationHost.Start(def, state.caster, state.target, item.animationTime,
                            item.soundCursor, item.mirror, item.animationRoot);
                        if (state.animation == null) { Cancel(state.caster); continue; }
                    }
                }
                catch (Exception error)
                {
                    Log.Error("[Nivarian Icecream Tail] Could not restore character skill: " + error.Message);
                    Cancel(state.caster);
                }
            }
        }

        private static bool HasNativeLock(Pawn pawn, Pawn other)
        {
            return pawn != null && pawn.CurJobDef != null && pawn.CurJobDef.defName == LockJobDefName && pawn.CurJob.targetA.Pawn == other;
        }

        private static void EndNativeLock(Pawn pawn, Job owned)
        {
            if (pawn != null && owned != null && ReferenceEquals(pawn.CurJob, owned))
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private static void EndSavedLock(Pawn pawn, Pawn other)
        {
            if (!IsProtected(pawn) && HasNativeLock(pawn, other))
                EndLockJob(pawn);
        }
    }
}
