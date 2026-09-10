using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

// SKILL5 超级爆！爆！回！
// 应该分文件的
// gpt老师帮忙的
// 不然我的石山代码会把你气死的
// 重灾区
// 0.25扫一次，可能会造成性能问题，需要优化
// 再说吧
namespace NivarianIcecreamTail
{
    internal enum IcecreamTailBakkaiPhase
    {
        Moving,
        FinalSpin
    }

    internal sealed class IcecreamTailBakkaiState
    {
        public Pawn pawn;
        public Map map;
        public IntVec3 startCell;
        public IntVec3 landingCell;
        public IcecreamTailBakkaiPhase phase;
        public int elapsedTicks;
        public int spinAccelerationTicks;
        public float spinAngle;
        public float spinDegreesPerTick;
        public float finalSpinStartAngle;
        public float finalSpinTargetAngle;
        public float finalSpinExponent;
        public float twirlSpawnProgress;
        public int nextPulseTick;
        public List<IntVec3> routeCells = new List<IntVec3>();
    }

    internal sealed class IcecreamTailBakkaiSavedState : IExposable
    {
        public Pawn pawn;
        public IntVec3 startCell;
        public IntVec3 landingCell;
        public int phase;
        public int elapsedTicks;
        public int spinAccelerationTicks;
        public float spinAngle;
        public float spinDegreesPerTick;
        public float finalSpinStartAngle;
        public float finalSpinTargetAngle;
        public float finalSpinExponent;
        public float twirlSpawnProgress;
        public int nextPulseTick;
        public List<IntVec3> routeCells;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref startCell, "startCell");
            Scribe_Values.Look(ref landingCell, "landingCell");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref elapsedTicks, "elapsedTicks");
            Scribe_Values.Look(ref spinAccelerationTicks, "spinAccelerationTicks");
            Scribe_Values.Look(ref spinAngle, "spinAngle");
            Scribe_Values.Look(ref spinDegreesPerTick, "spinDegreesPerTick");
            Scribe_Values.Look(ref finalSpinStartAngle, "finalSpinStartAngle");
            Scribe_Values.Look(ref finalSpinTargetAngle, "finalSpinTargetAngle");
            Scribe_Values.Look(ref finalSpinExponent, "finalSpinExponent");
            Scribe_Values.Look(ref twirlSpawnProgress, "twirlSpawnProgress");
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick");
            Scribe_Collections.Look(ref routeCells, "routeCells", LookMode.Value);
        }
    }

    public sealed class IcecreamTailBakkaiGameComponent : GameComponent
    {
        public IcecreamTailBakkaiGameComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            IcecreamTailBakkaiRuntime.ExposeData();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            IcecreamTailBakkaiRuntime.TickAll();
        }
    }

    public sealed class JobDriver_IcecreamTailBakkaiLocked : JobDriver
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
            toil.AddEndCondition(delegate
            {
                return IcecreamTailBakkaiRuntime.IsActive(pawn) ? JobCondition.Ongoing : JobCondition.Succeeded;
            });
            yield return toil;
        }
    }

    internal static class IcecreamTailBakkaiRuntime
    {
        private const int PulseIntervalTicks = 15;
        private const int MoveTicks = 180;
        private const int FinalSpinTicks = 60;
        private const int FinalSpinHoldTicks = 30;
        private const int SpinDecelerationTicks = 30;
        private const float MaximumSpinDegreesPerTick = 12f;
        private const int TwirlIntervalAtMaximumSpeed = 15;
        private const int MinimumSpinAccelerationTicks = 15;
        private const int MaximumSpinAccelerationTicks = 45;
        private const int PulseStunTicks = 60;
        private const int StunTicks = 120;
        private const string LockJobDefName = "IcecreamTailBakkaiLocked";
        private const string TwirlMoteDefName = "Mote_IcecreamTailBakkaiTwirl";

        private static readonly Dictionary<Pawn, IcecreamTailBakkaiState> Active =
            new Dictionary<Pawn, IcecreamTailBakkaiState>();
        private static readonly Dictionary<Pawn, Pawn> PendingStuns = new Dictionary<Pawn, Pawn>();
        private static readonly List<Pawn> TickBuffer = new List<Pawn>();

        public static void ExposeData()
        {
            List<IcecreamTailBakkaiSavedState> saved = new List<IcecreamTailBakkaiSavedState>();
            List<Pawn> pendingPawns = null;
            List<Pawn> pendingCasters = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saved = Active.Select(pair => new IcecreamTailBakkaiSavedState
                {
                    pawn = pair.Key,
                    startCell = pair.Value.startCell,
                    landingCell = pair.Value.landingCell,
                    phase = (int)pair.Value.phase,
                    elapsedTicks = pair.Value.elapsedTicks,
                    spinAccelerationTicks = pair.Value.spinAccelerationTicks,
                    spinAngle = pair.Value.spinAngle,
                    spinDegreesPerTick = pair.Value.spinDegreesPerTick,
                    finalSpinStartAngle = pair.Value.finalSpinStartAngle,
                    finalSpinTargetAngle = pair.Value.finalSpinTargetAngle,
                    finalSpinExponent = pair.Value.finalSpinExponent,
                    twirlSpawnProgress = pair.Value.twirlSpawnProgress,
                    nextPulseTick = pair.Value.nextPulseTick,
                    routeCells = pair.Value.routeCells
                }).ToList();
                pendingPawns = PendingStuns.Keys.ToList();
                pendingCasters = pendingPawns.Select(pawn => PendingStuns[pawn]).ToList();
            }

            Scribe_Collections.Look(ref saved, "bakkaiStates", LookMode.Deep);
            Scribe_Collections.Look(ref pendingPawns, "bakkaiPendingStunPawns", LookMode.Reference);
            Scribe_Collections.Look(ref pendingCasters, "bakkaiPendingStunCasters", LookMode.Reference);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            Active.Clear();
            PendingStuns.Clear();
            if (saved != null)
            {
                foreach (IcecreamTailBakkaiSavedState item in saved)
                {
                    if (item.pawn == null || item.pawn.DestroyedOrNull() || item.pawn.Dead ||
                        !item.pawn.Spawned || item.pawn.Map == null)
                    {
                        continue;
                    }

                    Active[item.pawn] = new IcecreamTailBakkaiState
                    {
                        pawn = item.pawn,
                        map = item.pawn.Map,
                        startCell = item.startCell,
                        landingCell = item.landingCell,
                        phase = (IcecreamTailBakkaiPhase)item.phase,
                        elapsedTicks = item.elapsedTicks,
                        spinAccelerationTicks = item.spinAccelerationTicks,
                        spinAngle = item.spinAngle,
                        spinDegreesPerTick = item.spinDegreesPerTick,
                        finalSpinStartAngle = item.finalSpinStartAngle,
                        finalSpinTargetAngle = item.finalSpinTargetAngle,
                        finalSpinExponent = item.finalSpinExponent,
                        twirlSpawnProgress = item.twirlSpawnProgress,
                        nextPulseTick = item.nextPulseTick,
                        routeCells = item.routeCells ?? new List<IntVec3> { item.pawn.Position }
                    };
                    BakkaiAnimationBridge.Prepare(item.pawn);
                }
            }

            if (pendingPawns != null && pendingCasters != null)
            {
                int count = Math.Min(pendingPawns.Count, pendingCasters.Count);
                for (int i = 0; i < count; i++)
                {
                    if (pendingPawns[i] != null)
                    {
                        PendingStuns[pendingPawns[i]] = pendingCasters[i];
                    }
                }
            }
        }

        public static bool Begin(Pawn pawn, LocalTargetInfo target)
        {
            if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead || !pawn.Spawned || pawn.Map == null ||
                Active.ContainsKey(pawn))
            {
                return false;
            }

            List<IntVec3> route;
            IntVec3 landingCell;
            if (!TryBuildRoute(pawn, target, out route, out landingCell))
            {
                return false;
            }

            int moveCellCount = Math.Max(0, route.Count - 1);
            IcecreamTailBakkaiState state = new IcecreamTailBakkaiState
            {
                pawn = pawn,
                map = pawn.Map,
                startCell = pawn.Position,
                landingCell = landingCell,
                phase = IcecreamTailBakkaiPhase.Moving,
                elapsedTicks = 0,
                spinAccelerationTicks = SpinAccelerationTicks(moveCellCount),
                spinAngle = 0f,
                spinDegreesPerTick = 0f,
                twirlSpawnProgress = 0f,
                nextPulseTick = PulseIntervalTicks,
                routeCells = route
            };

            if (target.Cell != pawn.Position)
            {
                pawn.Rotation = Rot4.FromAngleFlat((target.Cell - pawn.Position).AngleFlat);
            }

            Active[pawn] = state;
            pawn.pather.StopDead();
            BakkaiAnimationBridge.Prepare(pawn);
            EnsureLockJob(pawn);
            PlayStartSound(pawn);
            ApplyPulse(state, pawn.Position);
            return true;
        }

        public static void TickAll()
        {
            foreach (Pawn enemy in PendingStuns.Keys.ToList())
            {
                Pawn caster = PendingStuns[enemy];
                if (enemy == null || enemy.DestroyedOrNull() || enemy.Dead)
                {
                    PendingStuns.Remove(enemy);
                }
                else if (enemy.Spawned)
                {
                    ApplyStun(enemy, caster);
                    PendingStuns.Remove(enemy);
                }
            }

            TickBuffer.Clear();
            TickBuffer.AddRange(Active.Keys);
            foreach (Pawn pawn in TickBuffer)
            {
                Tick(pawn);
            }
        }

        private static void Tick(Pawn pawn)
        {
            IcecreamTailBakkaiState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state))
            {
                return;
            }

            if (pawn.DestroyedOrNull() || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Map != state.map)
            {
                Cancel(pawn);
                return;
            }

            pawn.pather.StopDead();
            if (!pawn.Downed)
            {
                EnsureLockJob(pawn);
            }

            state.elapsedTicks++;
            if (state.phase == IcecreamTailBakkaiPhase.Moving)
            {
                AdvanceMovingSpin(state);
                if (state.elapsedTicks >= MoveTicks)
                {
                    CompleteMovement(state);
                }
            }
            else
            {
                AdvanceFinalSpin(state);
            }
            AdvanceTwirl(state);

            int totalTicks = MoveTicks + FinalSpinTicks;
            while (state.nextPulseTick <= state.elapsedTicks && state.nextPulseTick < totalTicks)
            {
                ApplyPulse(state, CurrentCenterCell(state));
                state.nextPulseTick += PulseIntervalTicks;
            }

            if (state.elapsedTicks >= totalTicks)
            {
                Finish(state);
            }
        }

        public static bool IsActive(Pawn pawn)
        {
            return pawn != null && Active.ContainsKey(pawn);
        }

        public static void Cancel(Pawn pawn)
        {
            if (pawn == null || !Active.Remove(pawn))
            {
                return;
            }

            EndLockJob(pawn);
            BakkaiAnimationBridge.Cancel(pawn);
        }

        public static bool TryBuildRoute(Pawn pawn, LocalTargetInfo target, out List<IntVec3> route,
            out IntVec3 landingCell)
        {
            route = new List<IntVec3>();
            landingCell = IntVec3.Invalid;
            if (pawn == null || pawn.Map == null || !target.IsValid || !target.Cell.InBounds(pawn.Map) ||
                (target.Thing != null && target.Thing.Map != pawn.Map))
            {
                return false;
            }

            Map map = pawn.Map;
            IntVec3 targetCell = target.Cell;
            bool targetPawnOnMap = target.Pawn != null && target.Pawn.Spawned && target.Pawn.Map == map;
            if (!targetCell.Standable(map) || IsWall(map, targetCell) ||
                !GenSight.LineOfSight(pawn.Position, targetCell, map))
            {
                return false;
            }

            route.Add(pawn.Position);
            List<IntVec3> line = GenSight.BresenhamCellsBetween(pawn.Position, targetCell);
            for (int i = 1; i < line.Count; i++)
            {
                IntVec3 cell = line[i];
                if (!cell.InBounds(map) || IsWall(map, cell))
                {
                    route.Clear();
                    return false;
                }

                route.Add(cell);
            }

            int landingIndex = route.Count - 1;
            while (landingIndex > 0 && !IsLegalLandingCell(pawn, route[landingIndex]))
            {
                landingIndex--;
            }

            if (!IsLegalLandingCell(pawn, route[landingIndex]))
            {
                route.Clear();
                return false;
            }

            if (landingIndex < route.Count - 1)
            {
                route.RemoveRange(landingIndex + 1, route.Count - landingIndex - 1);
            }

            landingCell = route[route.Count - 1];
            if (!targetPawnOnMap && landingCell != targetCell)
            {
                route.Clear();
                landingCell = IntVec3.Invalid;
                return false;
            }

            return true;
        }

        public static HashSet<IntVec3> BuildAttackArea(IEnumerable<IntVec3> route, Map map)
        {
            HashSet<IntVec3> cells = new HashSet<IntVec3>();
            if (route == null || map == null)
            {
                return cells;
            }

            foreach (IntVec3 center in route)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        IntVec3 cell = center + new IntVec3(x, 0, z);
                        if (cell.InBounds(map))
                        {
                            cells.Add(cell);
                        }
                    }
                }
            }

            return cells;
        }

        public static Vector3 GetVisualOffset(Pawn pawn)
        {
            IcecreamTailBakkaiState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state) ||
                state.phase != IcecreamTailBakkaiPhase.Moving)
            {
                return Vector3.zero;
            }

            float progress = EaseInOutCubic(Mathf.Clamp01((float)state.elapsedTicks / MoveTicks));
            IntVec3 delta = state.landingCell - state.startCell;
            return new Vector3(delta.x, 0f, delta.z) * progress;
        }

        public static bool TryGetRenderAngle(Pawn pawn, out float angle)
        {
            IcecreamTailBakkaiState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state))
            {
                angle = 0f;
                return false;
            }

            angle = Mathf.Repeat(state.spinAngle, 360f);
            return true;
        }

        private static int SpinAccelerationTicks(int moveCellCount)
        {
            float distance = Mathf.Clamp(moveCellCount, 1, 10);
            float progress = (distance - 1f) / 9f;
            return Mathf.RoundToInt(Mathf.Lerp(MinimumSpinAccelerationTicks,
                MaximumSpinAccelerationTicks, progress));
        }

        private static void AdvanceMovingSpin(IcecreamTailBakkaiState state)
        {
            float progress = Mathf.Clamp01((float)state.elapsedTicks / Math.Max(1, state.spinAccelerationTicks));
            state.spinDegreesPerTick = Mathf.SmoothStep(0f, MaximumSpinDegreesPerTick, progress);
            state.spinAngle += state.spinDegreesPerTick;
        }

        private static void AdvanceFinalSpin(IcecreamTailBakkaiState state)
        {
            int finalElapsedTicks = state.elapsedTicks - MoveTicks;
            if (finalElapsedTicks <= FinalSpinHoldTicks)
            {
                state.spinDegreesPerTick = MaximumSpinDegreesPerTick;
                state.spinAngle += state.spinDegreesPerTick;
                if (finalElapsedTicks == FinalSpinHoldTicks)
                {
                    PrepareFinalDeceleration(state);
                }
                return;
            }

            int decelerationElapsedTicks = Mathf.Clamp(finalElapsedTicks - FinalSpinHoldTicks,
                0, SpinDecelerationTicks);
            float progress = (float)decelerationElapsedTicks / SpinDecelerationTicks;
            float exponent = Math.Max(0.01f, state.finalSpinExponent);
            float easedProgress = 1f - Mathf.Pow(1f - progress, exponent + 1f);
            float nextAngle = Mathf.Lerp(state.finalSpinStartAngle, state.finalSpinTargetAngle, easedProgress);
            state.spinDegreesPerTick = nextAngle - state.spinAngle;
            state.spinAngle = nextAngle;
        }

        private static void AdvanceTwirl(IcecreamTailBakkaiState state)
        {
            if (state.spinDegreesPerTick <= 0f)
            {
                return;
            }

            state.twirlSpawnProgress += state.spinDegreesPerTick /
                (MaximumSpinDegreesPerTick * TwirlIntervalAtMaximumSpeed);
            while (state.twirlSpawnProgress >= 0.9999f)
            {
                SpawnTwirl(state.pawn);
                state.twirlSpawnProgress = Math.Max(0f, state.twirlSpawnProgress - 1f);
            }
        }

        private static void CompleteMovement(IcecreamTailBakkaiState state)
        {
            Pawn pawn = state.pawn;
            int landingIndex = state.routeCells.Count - 1;
            while (landingIndex > 0 && !IsLegalLandingCell(pawn, state.routeCells[landingIndex]))
            {
                landingIndex--;
            }

            if (landingIndex < state.routeCells.Count - 1)
            {
                state.routeCells.RemoveRange(landingIndex + 1, state.routeCells.Count - landingIndex - 1);
            }

            state.landingCell = state.routeCells[landingIndex];
            pawn.Position = state.landingCell;
            pawn.Drawer.tweener.ResetTweenedPosToRoot();
            pawn.Notify_Teleported(true, true);
            state.phase = IcecreamTailBakkaiPhase.FinalSpin;
        }

        private static void PrepareFinalDeceleration(IcecreamTailBakkaiState state)
        {
            state.finalSpinStartAngle = state.spinAngle;
            float remainder = Mathf.Repeat(state.spinAngle, 360f);
            float finalDegrees = remainder < 0.01f || remainder > 359.99f ? 360f : 360f - remainder;
            state.finalSpinTargetAngle = state.finalSpinStartAngle + finalDegrees;
            state.finalSpinExponent = MaximumSpinDegreesPerTick * SpinDecelerationTicks / finalDegrees - 1f;
        }

        private static IntVec3 CurrentCenterCell(IcecreamTailBakkaiState state)
        {
            if (state.phase != IcecreamTailBakkaiPhase.Moving ||
                state.routeCells.Count <= 1)
            {
                return state.pawn.Position;
            }

            float progress = EaseInOutCubic(Mathf.Clamp01((float)state.elapsedTicks / MoveTicks));
            int index = Mathf.Clamp(Mathf.RoundToInt(progress * (state.routeCells.Count - 1)),
                0, state.routeCells.Count - 1);
            return state.routeCells[index];
        }

        private static void ApplyPulse(IcecreamTailBakkaiState state, IntVec3 center)
        {
            Pawn caster = state.pawn;
            Map map = state.map;
            int damage = Mathf.Max(1, Mathf.RoundToInt(caster.GetStatValue(StatDefOf.MeleeDPS) * 0.5f));
            foreach (Pawn enemy in EnemiesInSquare(caster, map, center))
            {
                DamageInfo info = new DamageInfo(DamageDefOf.Blunt, damage, -1f, -1f, caster);
                enemy.TakeDamage(info);
                if (!enemy.DestroyedOrNull() && !enemy.Dead && enemy.Spawned)
                {
                    enemy.stances.stunner.StunFor(PulseStunTicks, caster, true, true, true);
                }
            }
        }

        private static void SpawnTwirl(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Map.moteCounter.SaturatedLowPriority)
            {
                return;
            }

            ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail(TwirlMoteDefName);
            if (moteDef == null)
            {
                return;
            }

            int quarterTurn = Rand.Range(0, 4);
            float rotation = quarterTurn * 90f;
            Vector3 offset = Quaternion.AngleAxis(rotation, Vector3.up) * new Vector3(0.10f, 0f, 0.10f);
            MoteMaker.MakeStaticMote(pawn.DrawPos + offset, pawn.Map, moteDef, 1f, true, rotation);
        }

        private static float EaseInOutCubic(float value)
        {
            return value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
        }

        private static List<Pawn> EnemiesInSquare(Pawn caster, Map map, IntVec3 center)
        {
            HashSet<Pawn> enemies = new HashSet<Pawn>();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    IntVec3 cell = center + new IntVec3(x, 0, z);
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    List<Thing> things = cell.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                    {
                        Pawn enemy = things[i] as Pawn;
                        if (enemy != null && enemy != caster && enemy.Spawned && enemy.Map == map &&
                            !enemy.Dead && !enemy.Downed && enemy.HostileTo(caster))
                        {
                            enemies.Add(enemy);
                        }
                    }
                }
            }

            return enemies.ToList();
        }

        private static void Finish(IcecreamTailBakkaiState state)
        {
            Pawn caster = state.pawn;
            Map map = state.map;
            IntVec3 center = caster.Position;
            foreach (Pawn enemy in EnemiesInSquare(caster, map, center))
            {
                if (TryKnockback(caster, map, enemy, center))
                {
                    PendingStuns[enemy] = caster;
                }
                else
                {
                    ApplyStun(enemy, caster);
                }
            }

            Active.Remove(caster);
            EndLockJob(caster);
            BakkaiAnimationBridge.Cancel(caster);
        }

        private static bool TryKnockback(Pawn caster, Map map, Pawn enemy, IntVec3 center)
        {
            IntVec3 delta = enemy.Position - center;
            if (delta.x == 0 && delta.z == 0)
            {
                return false;
            }

            IntVec3 destination = enemy.Position + new IntVec3(Math.Sign(delta.x), 0, Math.Sign(delta.z));
            if (!destination.InBounds(map) || !destination.Standable(map) || destination.GetEdifice(map) != null ||
                destination.GetFirstPawn(map) != null)
            {
                return false;
            }

            IntVec3 startCell = enemy.Position;
            PawnFlyer flyer = PawnFlyer.MakeFlyer(
                ThingDefOf.PawnFlyer_Stun,
                enemy,
                destination,
                null,
                null,
                false,
                null,
                null,
                new LocalTargetInfo(destination));
            if (flyer == null)
            {
                return false;
            }

            GenSpawn.Spawn(flyer, startCell, map);
            return true;
        }

        public static void OnKnockbackLanded(Pawn enemy)
        {
            Pawn caster;
            if (enemy == null || !PendingStuns.TryGetValue(enemy, out caster))
            {
                return;
            }

            PendingStuns.Remove(enemy);
            if (!enemy.DestroyedOrNull() && !enemy.Dead && enemy.Spawned)
            {
                ApplyStun(enemy, caster);
            }
        }

        private static void ApplyStun(Pawn enemy, Pawn caster)
        {
            enemy.stances.stunner.StunFor(StunTicks, caster, true, true, true);
        }

        private static bool IsLegalLandingCell(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map) ||
                IsWall(pawn.Map, cell))
            {
                return false;
            }

            Pawn occupant = cell.GetFirstPawn(pawn.Map);
            return occupant == null || occupant == pawn;
        }

        private static bool IsWall(Map map, IntVec3 cell)
        {
            Building building = cell.GetEdifice(map);
            return building != null && building.def != null && building.def.building != null &&
                building.def.building.isWall;
        }

        private static void EnsureLockJob(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null || pawn.Downed ||
                (pawn.CurJobDef != null && pawn.CurJobDef.defName == LockJobDefName))
            {
                return;
            }

            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(LockJobDefName);
            if (def != null)
            {
                pawn.jobs.StartJob(JobMaker.MakeJob(def), JobCondition.InterruptForced);
            }
        }

        private static void EndLockJob(Pawn pawn)
        {
            if (pawn != null && pawn.jobs != null && pawn.CurJobDef != null &&
                pawn.CurJobDef.defName == LockJobDefName)
            {
                pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }

        private static void PlayStartSound(Pawn pawn)
        {
            string defName = "IcecreamTailSkill5Sound" + Rand.RangeInclusive(1, 3);
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (sound == null)
            {
                Log.Error("Nivarian Icecream Tail: missing " + defName + " SoundDef.");
                return;
            }

            sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_DrawPos")]
    internal static class Patch_Pawn_DrawPos_IcecreamTailBakkai
    {
        private static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            __result += IcecreamTailBakkaiRuntime.GetVisualOffset(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
    internal static class Patch_Pawn_PreApplyDamage_IcecreamTailBakkai
    {
        private static bool Prefix(Pawn __instance, ref bool absorbed)
        {
            if (!IcecreamTailBakkaiRuntime.IsActive(__instance))
            {
                return true;
            }

            absorbed = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_PawnRenderer_DisableCache_IcecreamTailBakkai
    {
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (IcecreamTailBakkaiRuntime.IsActive(___pawn))
            {
                disableCache = true;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    internal static class Patch_PawnRenderer_GetDrawParms_IcecreamTailBakkai
    {
        private static void Postfix(ref PawnDrawParms __result)
        {
            PawnRenderFlags ignored = PawnRenderFlags.Portrait | PawnRenderFlags.Cache | PawnRenderFlags.Statue;
            if ((__result.flags & ignored) != PawnRenderFlags.None)
            {
                return;
            }

            float angle;
            if (IcecreamTailBakkaiRuntime.TryGetRenderAngle(__result.pawn, out angle))
            {
                __result.matrix = __result.matrix * Matrix4x4.Rotate(Quaternion.AngleAxis(angle, Vector3.up));
            }
        }
    }

    [HarmonyPatch(typeof(PawnFlyer), "RespawnPawn")]
    internal static class Patch_PawnFlyer_RespawnPawn_IcecreamTailBakkai
    {
        private static void Prefix(PawnFlyer __instance, ref Pawn __state)
        {
            __state = __instance == null ? null : __instance.FlyingPawn;
        }

        private static void Postfix(Pawn __state)
        {
            IcecreamTailBakkaiRuntime.OnKnockbackLanded(__state);
        }
    }
}
