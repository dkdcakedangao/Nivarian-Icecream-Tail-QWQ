using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

// skill3 月牙叉炮
// 石山
// 动画调用、还有简易版本动画部分的码是gpt老师写的，效果还不错
// 待优化
namespace NivarianIcecreamTail
{
    internal enum IcecreamTailSkill3Phase
    {
        Dashing,
        Waiting,
        Animating
    }

    internal sealed class IcecreamTailSkill3State
    {
        public Pawn caster;
        public Pawn target;
        public IntVec3 startCell;
        public IntVec3 landingCell;
        public IntVec3 impactCell;
        public IcecreamTailSkill3Phase phase;
        public int moveElapsedTicks;
        public int moveTotalTicks;
        public int ticksRemaining;
        public List<IntVec3> pathCells = new List<IntVec3>();
    }

    internal sealed class IcecreamTailSkill3SavedState : IExposable
    {
        public Pawn caster;
        public Pawn target;
        public IntVec3 startCell;
        public IntVec3 landingCell;
        public IntVec3 impactCell;
        public int phase;
        public int moveElapsedTicks;
        public int moveTotalTicks;
        public int ticksRemaining;
        public List<IntVec3> pathCells;

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref startCell, "startCell");
            Scribe_Values.Look(ref landingCell, "landingCell");
            Scribe_Values.Look(ref impactCell, "impactCell");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref moveElapsedTicks, "moveElapsedTicks");
            Scribe_Values.Look(ref moveTotalTicks, "moveTotalTicks");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining");
            Scribe_Collections.Look(ref pathCells, "pathCells", LookMode.Value);
        }
    }

    public sealed class IcecreamTailSkill3GameComponent : GameComponent
    {
        public IcecreamTailSkill3GameComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            IcecreamTailSkill3Runtime.ExposeData();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            IcecreamTailSkill3Runtime.TickAll();
        }
    }

    public sealed class JobDriver_IcecreamTailSkill3Locked : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = ToilMaker.MakeToil();
            toil.handlingFacing = true;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.tickAction = delegate
            {
                Rot4 facing;
                if (IcecreamTailSkill3Runtime.TryGetFacing(pawn, out facing))
                {
                    job.overrideFacing = facing;
                }
            };
            toil.AddEndCondition(delegate
            {
                return IcecreamTailSkill3Runtime.IsProtected(pawn) ? JobCondition.Ongoing : JobCondition.Succeeded;
            });
            yield return toil;
        }
    }

    internal static class IcecreamTailSkill3Runtime
    {
        private const int AttackCount = 15;
        private const int WaitTicks = 120;
        private const float ExplosionRadius = 1.5f;
        private const string LockJobDefName = "IcecreamTailSkill3Locked";
        private const string NonAnimationStartSoundDefName = "IcecreamTailSkill3Sound3";
        private const string NonAnimationFinishSoundDefName = "IcecreamTailSkill3Sound4";

        private static readonly Dictionary<Pawn, IcecreamTailSkill3State> Active = new Dictionary<Pawn, IcecreamTailSkill3State>();
        private static readonly Dictionary<Pawn, IcecreamTailSkill3State> Protected = new Dictionary<Pawn, IcecreamTailSkill3State>();
        private static readonly List<Pawn> TickBuffer = new List<Pawn>();
        private static readonly HashSet<Pawn> InternalDamageAllowed = new HashSet<Pawn>();
        private static readonly HashSet<Pawn> DeathAllowed = new HashSet<Pawn>();
        private static readonly Dictionary<Type, MethodInfo> MeleeDamageMethods = new Dictionary<Type, MethodInfo>();

        public static void ExposeData()
        {
            List<IcecreamTailSkill3SavedState> saved = new List<IcecreamTailSkill3SavedState>();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saved = Active.Values.Select(state => new IcecreamTailSkill3SavedState
                {
                    caster = state.caster,
                    target = state.target,
                    startCell = state.startCell,
                    landingCell = state.landingCell,
                    impactCell = state.impactCell,
                    phase = (int)state.phase,
                    moveElapsedTicks = state.moveElapsedTicks,
                    moveTotalTicks = state.moveTotalTicks,
                    ticksRemaining = state.ticksRemaining,
                    pathCells = state.pathCells
                }).ToList();
            }

            Scribe_Collections.Look(ref saved, "icecreamTailSkill3States", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Active.Clear();
                Protected.Clear();
                InternalDamageAllowed.Clear();
                DeathAllowed.Clear();
                if (saved == null) return;
                foreach (IcecreamTailSkill3SavedState item in saved)
                {
                    if (item.caster == null || item.target == null) continue;
                    IcecreamTailSkill3State state = new IcecreamTailSkill3State
                    {
                        caster = item.caster,
                        target = item.target,
                        startCell = item.startCell,
                        landingCell = item.landingCell,
                        impactCell = item.impactCell,
                        phase = (IcecreamTailSkill3Phase)item.phase,
                        moveElapsedTicks = item.moveElapsedTicks,
                        moveTotalTicks = item.moveTotalTicks,
                        ticksRemaining = item.ticksRemaining,
                        pathCells = item.pathCells ?? new List<IntVec3>()
                    };
                    Active[state.caster] = state;
                    Protected[state.caster] = state;
                    Protected[state.target] = state;
                    DropHostileJobsTargeting(state.caster);
                    DropHostileJobsTargeting(state.target);
                }
            }
        }

        // 选择器
        public static bool CanTarget(Pawn caster, Pawn target, out string reason)
        {
            if (caster == null || target == null || target == caster || caster.Map == null || !target.Spawned || target.Map != caster.Map)
            {
                reason = "必须选择同一地图中的敌对目标。";
                return false;
            }
            if (target.Dead || target.Downed)
            {
                reason = "无法对死亡或倒地目标使用月牙叉炮。";
                return false;
            }
            if (!target.HostileTo(caster))
            {
                reason = "月牙叉炮只能选择敌对目标。";
                return false;
            }
            if (caster.Position.DistanceTo(target.Position) > 5f || !GenSight.LineOfSight(caster.Position, target.Position, caster.Map))
            {
                reason = "目标超出月牙叉炮射程或视线受阻。";
                return false;
            }

            // 除错
            if (Protected.ContainsKey(caster) || Protected.ContainsKey(target))
            {
                reason = "目标正在另一轮月牙叉炮中。";
                return false;
            }
            reason = null;
            return true;
        }

        public static bool Begin(Pawn caster, Pawn target)
        {
            string reason;
            List<IntVec3> path;
            IntVec3 landingCell;
            if (!CanTarget(caster, target, out reason) || !TryBuildApproach(caster, target, out path, out landingCell))
            {
                return false;
            }

            IcecreamTailSkill3State state = new IcecreamTailSkill3State
            {
                caster = caster,
                target = target,
                startCell = caster.Position,
                landingCell = landingCell,
                impactCell = target.Position,
                phase = IcecreamTailSkill3Phase.Dashing,
                moveElapsedTicks = 0,
                moveTotalTicks = Mathf.Max(1, path.Count * 4),
                pathCells = path
            };
            Active[caster] = state;
            Protected[caster] = state;
            Protected[target] = state;
            try
            {
                if (!Skill3AnimationBridge.Prepare(caster, target))
                {
                    Cancel(caster);
                    return false;
                }
                DropHostileJobsTargeting(caster);
                DropHostileJobsTargeting(target);
                GiveLockJob(caster, target);
                GiveLockJob(target, caster);
                caster.pather.StopDead();
                target.pather.StopDead();
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("[Nivarian Icecream Tail] 月牙叉炮启动失败：" + exception);
                Cancel(caster);
                return false;
            }
        }

        public static void TickAll()
        {
            TickBuffer.Clear();
            TickBuffer.AddRange(Active.Keys);
            foreach (Pawn caster in TickBuffer)
            {
                try
                {
                    Tick(caster);
                }
                catch (Exception exception)
                {
                    int key = 1978042332 ^ (caster == null ? 0 : caster.thingIDNumber);
                    Log.ErrorOnce("Nivarian Icecream Tail: Skill3 runtime failed and was cancelled: " + exception, key);
                    Cancel(caster);
                }
            }
        }

        // 动画器 锁
        private static void Tick(Pawn caster)
        {
            IcecreamTailSkill3State state;
            if (caster == null || !Active.TryGetValue(caster, out state)) return;
            string reason;
            if (caster.Dead || !caster.Spawned || caster.Map == null ||
                !IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(caster, out reason))
            {
                Cancel(caster);
                return;
            }

            if (state.phase == IcecreamTailSkill3Phase.Dashing)
            {
                if (!IsLiveTarget(state))
                {
                    Cancel(caster);
                    return;
                }
                AdvanceDash(state);
                return;
            }

            if (state.phase == IcecreamTailSkill3Phase.Waiting)
            {
                if (!IsLiveTarget(state))
                {
                    Cancel(caster);
                    return;
                }
                MaintainWaitingLock(state);
                state.ticksRemaining--;
                if (state.ticksRemaining <= 0)
                {
                    try
                    {
                        PlaySound(NonAnimationFinishSoundDefName, state.impactCell, caster.Map);
                        ResolveExplosionAndFinisher(state);
                    }
                    finally
                    {
                        Cancel(caster);
                    }
                }
                return;
            }

            if (!IsLiveTarget(state))
            {
                Cancel(caster);
                return;
            }
            Skill3AnimationStatus status = Skill3AnimationBridge.GetStatus(state.caster, state.target);
            if (status == Skill3AnimationStatus.Running) return;
            try
            {
                if (status == Skill3AnimationStatus.Completed)
                {
                    ResolveAllDamage(state);
                }
            }
            finally
            {
                Cancel(caster);
            }
        }

        private static void AdvanceDash(IcecreamTailSkill3State state)
        {
            state.moveElapsedTicks = Mathf.Min(state.moveTotalTicks, state.moveElapsedTicks + 1);
            if (state.moveElapsedTicks < state.moveTotalTicks) return;

            Pawn caster = state.caster;
            Pawn target = state.target;
            if (!IsLandingCellFree(caster, state.landingCell))
            {
                Cancel(caster);
                return;
            }
            caster.Position = state.landingCell;
            caster.Drawer.tweener.ResetTweenedPosToRoot();
            caster.Notify_Teleported(true, true);
            FaceEachOther(caster, target);

            if ((IcecreamTailMod.Settings == null || IcecreamTailMod.Settings.EnableSkill3MeleeAnimation) &&
                Skill3AnimationBridge.Provider != null)
            {
                EndLockJob(caster);
                EndLockJob(target);
                if (Skill3AnimationBridge.TryStart(caster, target))
                {
                    state.phase = IcecreamTailSkill3Phase.Animating;
                    return;
                }
                GiveLockJob(caster, target);
                GiveLockJob(target, caster);
            }

            PlaySound(NonAnimationStartSoundDefName, caster.Position, caster.Map);
            ResolveMeleeHits(state);
            state.phase = IcecreamTailSkill3Phase.Waiting;
            state.ticksRemaining = WaitTicks;
        }

        private static void ResolveAllDamage(IcecreamTailSkill3State state)
        {
            ResolveMeleeHits(state);
            ResolveExplosionAndFinisher(state);
        }

        private static void ResolveMeleeHits(IcecreamTailSkill3State state)
        {
            using (EnterInternalDamage(state.target))
            {
                for (int i = 0; i < AttackCount; i++)
                {
                    if (!IsLiveTarget(state)) break;
                    Verb verb = state.caster.meleeVerbs.TryGetMeleeVerb(state.target);
                    if (verb == null) break;
                    if (!TryApplyMeleeDamage(verb, state.target)) break;
                }
            }
        }

        private static bool TryApplyMeleeDamage(Verb verb, Pawn target)
        {
            Verb_MeleeAttackDamage damageVerb = verb as Verb_MeleeAttackDamage;
            if (damageVerb == null) return false;

            Type verbType = damageVerb.GetType();
            MethodInfo method;
            if (!MeleeDamageMethods.TryGetValue(verbType, out method))
            {
                method = AccessTools.Method(verbType, "ApplyMeleeDamageToTarget", new[] { typeof(LocalTargetInfo) });
                MeleeDamageMethods[verbType] = method;
            }
            if (method == null) return false;

            try
            {
                method.Invoke(damageVerb, new object[] { (LocalTargetInfo)target });
                return true;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("Nivarian Icecream Tail: Skill3 melee resolution failed: " + exception, 1978042331);
                return false;
            }
        }

        private static void ResolveExplosionAndFinisher(IcecreamTailSkill3State state)
        {
            Pawn caster = state.caster;
            Map map = caster == null ? null : caster.Map;
            if (caster == null || map == null || !state.impactCell.InBounds(map)) return;

            using (EnterInternalDamage(state.target))
            using (EnterDeathAllowed(state.target))
            {
                List<Pawn> enemies = GenRadial.RadialDistinctThingsAround(state.impactCell, map, ExplosionRadius, true)
                    .OfType<Pawn>()
                    .Where(pawn => pawn != caster && (pawn == state.target || pawn.HostileTo(caster)))
                    .Distinct()
                    .ToList();
                List<Thing> ignored = GenRadial.RadialDistinctThingsAround(state.impactCell, map, ExplosionRadius, true)
                    .Where(thing => !enemies.Contains(thing))
                    .ToList();
                int damage = Mathf.Max(1, Mathf.RoundToInt(caster.GetStatValue(StatDefOf.MeleeDPS)));
                DamageDef bombDamage = DamageDefOf.Bomb;
                SoundDef explosionSound = bombDamage == null ? null : bombDamage.soundExplosion;
                GenExplosion.DoExplosion(state.impactCell, map, ExplosionRadius, DamageDefOf.Blunt, caster, damage,
                    explosionSound: explosionSound, ignoredThings: ignored, doSoundEffects: explosionSound != null);
                if (IsLiveTarget(state))
                {
                    ApplyFinisher(caster, state.target);
                }
            }
        }

        private static void ApplyFinisher(Pawn caster, Pawn target)
        {
            Verb verb = caster.meleeVerbs.TryGetMeleeVerb(target);
            if (verb == null) return;
            ThingWithComps weapon = verb.EquipmentSource;
            float damage = verb.verbProps.AdjustedMeleeDamageAmount(verb.tool, caster, weapon, verb.HediffCompSource) * 2f;
            float armorPenetration = verb.verbProps.AdjustedArmorPenetration(verb.tool, caster, weapon, verb.HediffCompSource);
            BodyPartRecord hitPart = target.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(part => part.def == BodyPartDefOf.Neck) ?? target.RaceProps.body.corePart;
            DamageDef damageDef = verb.GetDamageDef() ?? DamageDefOf.Blunt;
            DamageInfo info = new DamageInfo(damageDef, damage, armorPenetration, -1f, caster, hitPart,
                weapon == null ? null : weapon.def);
            target.TakeDamage(info);
        }

        // 方向锁
        // 不一定有用
        // 待优化
        public static bool TryBuildApproach(Pawn caster, Pawn target, out List<IntVec3> path, out IntVec3 landingCell)
        {
            path = new List<IntVec3>();
            landingCell = IntVec3.Invalid;
            string reason;
            if (!CanTarget(caster, target, out reason)) return false;

            IntVec3 west = target.Position + IntVec3.West;
            IntVec3 east = target.Position + IntVec3.East;
            IntVec3 first = caster.Position.x <= target.Position.x ? west : east;
            IntVec3 second = first == west ? east : west;
            if (!TryUseLandingCell(caster, first, out path))
            {
                if (!TryUseLandingCell(caster, second, out path)) return false;
                landingCell = second;
            }
            else
            {
                landingCell = first;
            }
            return true;
        }

        private static bool TryUseLandingCell(Pawn caster, IntVec3 cell, out List<IntVec3> path)
        {
            path = new List<IntVec3>();
            if (!IsLandingCellFree(caster, cell)) return false;
            List<IntVec3> line = GenSight.BresenhamCellsBetween(caster.Position, cell);
            for (int i = 1; i < line.Count; i++)
            {
                IntVec3 current = line[i];
                if (!current.InBounds(caster.Map) || IsWall(caster.Map, current))
                {
                    path.Clear();
                    return false;
                }
                path.Add(current);
            }
            return path.Count > 0 || caster.Position == cell;
        }

        // 落点检测
        private static bool IsLandingCellFree(Pawn caster, IntVec3 cell)
        {
            if (caster == null || caster.Map == null || !cell.InBounds(caster.Map) || !cell.Standable(caster.Map) || IsWall(caster.Map, cell))
                return false;
            Pawn occupant = cell.GetFirstPawn(caster.Map);
            return occupant == null || occupant == caster;
        }

        private static bool IsWall(Map map, IntVec3 cell)
        {
            Building building = cell.GetEdifice(map);
            return building != null && building.def != null && building.def.building != null && building.def.building.isWall;
        }

        private static bool IsLiveTarget(IcecreamTailSkill3State state)
        {
            return state.target != null && !state.target.Dead && !state.target.Destroyed && state.target.Spawned &&
                state.target.Map == state.caster.Map;
        }

        private static void FaceEachOther(Pawn caster, Pawn target)
        {
            caster.Rotation = target.Position.x < caster.Position.x ? Rot4.West : Rot4.East;
            target.Rotation = caster.Position.x < target.Position.x ? Rot4.West : Rot4.East;
        }

        private static void PlaySound(string defName, IntVec3 cell, Map map)
        {
            if (map == null) return;
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (sound != null)
            {
                sound.PlayOneShot(new TargetInfo(cell, map));
            }
        }

        private static void MaintainWaitingLock(IcecreamTailSkill3State state)
        {
            Pawn caster = state.caster;
            Pawn target = state.target;
            FaceEachOther(caster, target);
            EnsureLockJob(caster, target);
            EnsureLockJob(target, caster);
            caster.pather.StopDead();
            target.pather.StopDead();
        }

        public static Vector3 GetVisualOffset(Pawn pawn)
        {
            IcecreamTailSkill3State state;
            if (pawn == null || !Active.TryGetValue(pawn, out state) || state.phase != IcecreamTailSkill3Phase.Dashing || state.moveTotalTicks <= 0)
                return Vector3.zero;
            float progress = Mathf.Clamp01((float)state.moveElapsedTicks / state.moveTotalTicks);
            IntVec3 delta = state.landingCell - state.startCell;
            return new Vector3(delta.x, 0f, delta.z) * progress;
        }

        public static bool IsProtected(Pawn pawn)
        {
            return pawn != null && Protected.ContainsKey(pawn);
        }

        public static bool ShouldAbsorbDamage(Pawn pawn)
        {
            return IsProtected(pawn) && !InternalDamageAllowed.Contains(pawn);
        }

        public static bool ShouldPreventDeath(Pawn pawn)
        {
            return IsProtected(pawn) && !DeathAllowed.Contains(pawn);
        }

        public static bool ShouldPreventDowning(Pawn pawn)
        {
            return IsProtected(pawn) && !DeathAllowed.Contains(pawn);
        }

        public static bool ShouldSuppressInternalDamageClamor(Thing source)
        {
            Pawn pawn = source as Pawn;
            return pawn != null && InternalDamageAllowed.Contains(pawn);
        }

        public static bool TryGetFacing(Pawn pawn, out Rot4 facing)
        {
            IcecreamTailSkill3State state;
            if (pawn != null && Protected.TryGetValue(pawn, out state) && state.caster != null && state.target != null)
            {
                Pawn other = pawn == state.caster ? state.target : state.caster;
                facing = other.Position.x < pawn.Position.x ? Rot4.West : Rot4.East;
                return true;
            }
            facing = Rot4.Invalid;
            return false;
        }

        public static void Cancel(Pawn caster)
        {
            IcecreamTailSkill3State state;
            if (caster == null || !Active.TryGetValue(caster, out state)) return;
            if (state.phase == IcecreamTailSkill3Phase.Animating)
            {
                Skill3AnimationBridge.Cancel(state.caster, state.target);
            }
            Active.Remove(state.caster);
            Protected.Remove(state.caster);
            Protected.Remove(state.target);
            EndLockJob(state.caster);
            EndLockJob(state.target);
        }

        private static void DropHostileJobsTargeting(Pawn protectedPawn)
        {
            if (protectedPawn == null || !protectedPawn.Spawned || protectedPawn.Map == null) return;
            foreach (Pawn pawn in protectedPawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.jobs == null || IsProtected(pawn) || !pawn.HostileTo(protectedPawn)) continue;
                Job job = pawn.CurJob;
                if (job != null && job.AnyTargetIs((LocalTargetInfo)protectedPawn))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            }
        }

        private static void GiveLockJob(Pawn pawn, Pawn other)
        {
            if (pawn == null || pawn.jobs == null) return;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(LockJobDefName);
            if (def == null) return;
            pawn.jobs.StartJob(JobMaker.MakeJob(def, other), JobCondition.InterruptForced);
        }

        private static void EnsureLockJob(Pawn pawn, Pawn other)
        {
            if (pawn != null && (pawn.CurJobDef == null || pawn.CurJobDef.defName != LockJobDefName))
            {
                GiveLockJob(pawn, other);
            }
        }

        private static void EndLockJob(Pawn pawn)
        {
            if (pawn != null && pawn.CurJobDef != null && pawn.CurJobDef.defName == LockJobDefName)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        private static IDisposable EnterInternalDamage(Pawn pawn)
        {
            if (pawn != null) InternalDamageAllowed.Add(pawn);
            return new InternalDamageScope(pawn);
        }

        private static IDisposable EnterDeathAllowed(Pawn pawn)
        {
            if (pawn != null) DeathAllowed.Add(pawn);
            return new DeathAllowedScope(pawn);
        }

        private sealed class InternalDamageScope : IDisposable
        {
            private readonly Pawn pawn;

            public InternalDamageScope(Pawn pawn)
            {
                this.pawn = pawn;
            }

            public void Dispose()
            {
                if (pawn != null) InternalDamageAllowed.Remove(pawn);
            }
        }

        private sealed class DeathAllowedScope : IDisposable
        {
            private readonly Pawn pawn;

            public DeathAllowedScope(Pawn pawn)
            {
                this.pawn = pawn;
            }

            public void Dispose()
            {
                if (pawn != null) DeathAllowed.Remove(pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
    internal static class Patch_Pawn_PreApplyDamage_IcecreamTailSkill3
    {
        private static bool Prefix(Pawn __instance, ref bool absorbed)
        {
            if (IcecreamTailSkill3Runtime.ShouldAbsorbDamage(__instance))
            {
                absorbed = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "Kill")]
    internal static class Patch_Pawn_Kill_IcecreamTailSkill3
    {
        private static bool Prefix(Pawn __instance)
        {
            return !IcecreamTailSkill3Runtime.ShouldPreventDeath(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    internal static class Patch_PawnHealthTracker_MakeDowned_IcecreamTailSkill3
    {
        private static bool Prefix(Pawn ___pawn)
        {
            return !IcecreamTailSkill3Runtime.ShouldPreventDowning(___pawn);
        }
    }

    [HarmonyPatch(typeof(GenClamor), "DoClamor", new[] { typeof(Thing), typeof(float), typeof(ClamorDef) })]
    internal static class Patch_GenClamor_DoClamor_IcecreamTailSkill3
    {
        private static bool Prefix(Thing __0)
        {
            return !IcecreamTailSkill3Runtime.ShouldSuppressInternalDamageClamor(__0);
        }
    }

    [HarmonyPatch(typeof(Pawn), "ThreatDisabled")]
    internal static class Patch_Pawn_ThreatDisabled_IcecreamTailSkill3
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (IcecreamTailSkill3Runtime.IsProtected(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_DrawPos")]
    internal static class Patch_Pawn_DrawPos_IcecreamTailSkill3
    {
        private static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            __result += IcecreamTailSkill3Runtime.GetVisualOffset(__instance);
        }
    }
}
