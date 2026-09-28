using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

// 灭菌器大人临时技能，主体，包括了一些音效、特效，除错
namespace NivarianIcecreamTail
{
    internal sealed class IcecreamTailSterilizerState
    {
        public Pawn pawn;
        public Map map;
        public IntVec3 originCell;
        public float baseAngle;
        public int elapsedTicks;
        public bool animationActive = true;
        public bool spraySoundPlayed;
        public List<Pawn> affectedPawns = new List<Pawn>();
        public Dictionary<IntVec3, int> hazardExpiryTicks = new Dictionary<IntVec3, int>();
        public List<IntVec3> visibleCells = new List<IntVec3>();
    }

    internal sealed class IcecreamTailSterilizerSavedState : IExposable
    {
        public Pawn pawn;
        public Map map;
        public IntVec3 originCell;
        public float baseAngle;
        public int elapsedTicks;
        public bool animationActive;
        public bool spraySoundPlayed;
        public List<Pawn> affectedPawns;
        public List<IntVec3> hazardCells;
        public List<int> hazardExpiryTicks;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref originCell, "originCell");
            Scribe_Values.Look(ref baseAngle, "baseAngle", 0f);
            Scribe_Values.Look(ref elapsedTicks, "elapsedTicks", 0);
            Scribe_Values.Look(ref animationActive, "animationActive", true);
            Scribe_Values.Look(ref spraySoundPlayed, "spraySoundPlayed", false);
            Scribe_Collections.Look(ref affectedPawns, "affectedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref hazardCells, "hazardCells", LookMode.Value);
            Scribe_Collections.Look(ref hazardExpiryTicks, "hazardExpiryTicks", LookMode.Value);
        }
    }

    public sealed class IcecreamTailSterilizerGameComponent : GameComponent
    {
        // Deep-loaded records must survive LoadingVars until references resolve and PostLoadInit runs.
        private List<IcecreamTailSterilizerSavedState> savedStates;

        public IcecreamTailSterilizerGameComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            IcecreamTailSterilizerRuntime.ExposeData(ref savedStates);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            IcecreamTailSterilizerRuntime.TickAll();
        }
    }

    public sealed class JobDriver_IcecreamTailSterilizerLocked : JobDriver
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
                return IcecreamTailSterilizerRuntime.IsActive(pawn) ? JobCondition.Ongoing : JobCondition.Succeeded;
            });
            yield return toil;
        }
    }

    internal static class IcecreamTailSterilizerRuntime
    {
        private const int RiseTicks = 60;
        private const int SweepTicks = 150;
        private const int FadeTicks = 30;
        private const int GasExpansionTicks = 75;
        private const int HazardLifetimeTicks = 600;
        private const int HazardScanIntervalTicks = 5;
        private const int RiseEffectLeadOutTicks = 30;
        private const int TotalTicks = RiseTicks + SweepTicks;
        private const int GlowIntervalTicks = 8;
        private const int HaloIntervalTicks = 15;
        private const int GasIntervalTicks = 3;
        private const float HalfSweepAngle = 30f;
        private const float MaximumRange = 10f;
        private const float RiseHeight = 0.32f;
        private const string LockJobDefName = "IcecreamTailSterilizerLocked";
        private const string WeaponTexturePath = "Effects/chuifengji";
        private const string GlowMoteDefName = "Mote_IcecreamTailSterilizerGlow";
        private const string HaloMoteDefName = "Mote_IcecreamTailSterilizerHalo";
        private const string GasMoteDefName = "Mote_IcecreamTailSterilizerGas";
        private const string ToxicIrritationDefName = "IcecreamTailSterilizerToxicIrritation";
        private const string FoodPoisoningDefName = "IcecreamTailSterilizerFoodPoisoning";
        private const string EnemyThoughtDefName = "IcecreamTailMemorySterilizerEnemy";
        private const string FriendlyThoughtDefName = "IcecreamTailMemorySterilizerFriendly";

        private static readonly Dictionary<Pawn, IcecreamTailSterilizerState> Active =
            new Dictionary<Pawn, IcecreamTailSterilizerState>();
        // Earlier casts keep their own hazards and hit history without blocking a new animation.
        private static readonly List<IcecreamTailSterilizerState> Lingering =
            new List<IcecreamTailSterilizerState>();
        private static readonly List<Pawn> TickBuffer = new List<Pawn>();
        private static readonly List<IntVec3> ExpiredCellBuffer = new List<IntVec3>();

        public static bool Begin(Pawn pawn, IntVec3 targetCell)
        {
            if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead || !pawn.Spawned || pawn.Map == null ||
                targetCell == pawn.Position || !targetCell.InBounds(pawn.Map) || IsActive(pawn))
            {
                return false;
            }

            IcecreamTailSterilizerState previous;
            if (Active.TryGetValue(pawn, out previous) && previous.hazardExpiryTicks.Count > 0)
            {
                Lingering.Add(previous);
            }

            IcecreamTailSterilizerState state = new IcecreamTailSterilizerState
            {
                pawn = pawn,
                map = pawn.Map,
                originCell = pawn.Position,
                baseAngle = (targetCell - pawn.Position).AngleFlat,
                elapsedTicks = 0
            };
            Active[pawn] = state;
            pawn.Rotation = Rot4.FromAngleFlat(state.baseAngle);
            pawn.pather.StopDead();
            EnsureLockJob(pawn);
            return true;
        }

        public static bool IsActive(Pawn pawn)
        {
            IcecreamTailSterilizerState state;
            return pawn != null && Active.TryGetValue(pawn, out state) && state.animationActive;
        }

        public static void ExposeData(ref List<IcecreamTailSterilizerSavedState> saved)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saved = new List<IcecreamTailSterilizerSavedState>(Active.Count + Lingering.Count);
                foreach (KeyValuePair<Pawn, IcecreamTailSterilizerState> pair in Active)
                {
                    saved.Add(CreateSavedState(pair.Value));
                }
                for (int i = 0; i < Lingering.Count; i++)
                {
                    saved.Add(CreateSavedState(Lingering[i]));
                }
            }

            Scribe_Collections.Look(ref saved, "sterilizerStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saved = null;
                return;
            }
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            Active.Clear();
            Lingering.Clear();
            if (saved == null)
            {
                return;
            }

            foreach (IcecreamTailSterilizerSavedState item in saved)
            {
                Map map = item.map;
                if (item.pawn == null || map == null || item.elapsedTicks < 0 ||
                    (item.animationActive && item.elapsedTicks >= TotalTicks))
                {
                    continue;
                }

                IcecreamTailSterilizerState state = new IcecreamTailSterilizerState
                {
                    pawn = item.pawn,
                    map = map,
                    originCell = item.originCell,
                    baseAngle = item.baseAngle,
                    elapsedTicks = item.elapsedTicks,
                    animationActive = item.animationActive,
                    spraySoundPlayed = item.spraySoundPlayed,
                    affectedPawns = item.affectedPawns ?? new List<Pawn>()
                };
                int now = Find.TickManager.TicksGame;
                if (item.hazardCells != null && item.hazardExpiryTicks != null)
                {
                    int count = Math.Min(item.hazardCells.Count, item.hazardExpiryTicks.Count);
                    for (int i = 0; i < count; i++)
                    {
                        IntVec3 cell = item.hazardCells[i];
                        int expiryTick = item.hazardExpiryTicks[i];
                        if (cell.InBounds(map) && expiryTick > now)
                        {
                            state.hazardExpiryTicks[cell] = expiryTick;
                            SpawnGasCell(state, cell, expiryTick - now);
                        }
                    }
                }

                if (state.animationActive && IsAnimationValid(state))
                {
                    Active[item.pawn] = state;
                    EnsureLockJob(item.pawn);
                }
                else if (state.hazardExpiryTicks.Count > 0)
                {
                    state.animationActive = false;
                    Lingering.Add(state);
                }
            }
            saved = null;
        }

        private static IcecreamTailSterilizerSavedState CreateSavedState(IcecreamTailSterilizerState state)
        {
            List<IntVec3> hazardCells = new List<IntVec3>(state.hazardExpiryTicks.Count);
            List<int> hazardExpiryTicks = new List<int>(state.hazardExpiryTicks.Count);
            foreach (KeyValuePair<IntVec3, int> hazard in state.hazardExpiryTicks)
            {
                hazardCells.Add(hazard.Key);
                hazardExpiryTicks.Add(hazard.Value);
            }
            return new IcecreamTailSterilizerSavedState
            {
                pawn = state.pawn,
                map = state.map,
                originCell = state.originCell,
                baseAngle = state.baseAngle,
                elapsedTicks = state.elapsedTicks,
                animationActive = state.animationActive,
                spraySoundPlayed = state.spraySoundPlayed,
                affectedPawns = state.affectedPawns,
                hazardCells = hazardCells,
                hazardExpiryTicks = hazardExpiryTicks
            };
        }

        public static void TickAll()
        {
            if (Active.Count == 0 && Lingering.Count == 0)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick % HazardScanIntervalTicks == 0)
            {
                // Only already-emitted hazards are processed; no Pawn scan or new per-tick list.
                for (int i = Lingering.Count - 1; i >= 0; i--)
                {
                    IcecreamTailSterilizerState state = Lingering[i];
                    try
                    {
                        TickHazards(state, currentTick);
                        if (state.hazardExpiryTicks.Count == 0)
                        {
                            Lingering.RemoveAt(i);
                        }
                    }
                    catch (Exception exception)
                    {
                        int key = 1453917133 ^ (state.pawn == null ? 0 : state.pawn.thingIDNumber);
                        Log.ErrorOnce("Nivarian Icecream Tail: lingering sterilizer hazards failed and were removed: " + exception, key);
                        Lingering.RemoveAt(i);
                    }
                }
            }

            if (Active.Count == 0)
            {
                return;
            }

            TickBuffer.Clear();
            TickBuffer.AddRange(Active.Keys);
            for (int i = 0; i < TickBuffer.Count; i++)
            {
                Pawn pawn = TickBuffer[i];
                try
                {
                    Tick(pawn);
                }
                catch (Exception exception)
                {
                    int key = 1453917133 ^ (pawn == null ? 0 : pawn.thingIDNumber);
                    Log.ErrorOnce("Nivarian Icecream Tail: sterilizer runtime failed and was cancelled: " + exception, key);
                    Discard(pawn);
                }
            }
        }

        private static void Tick(Pawn pawn)
        {
            IcecreamTailSterilizerState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state))
            {
                return;
            }

            if (state.animationActive)
            {
                if (!IsAnimationValid(state))
                {
                    StopAnimation(state);
                }
                else
                {
                    TickAnimation(state);
                }
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick % HazardScanIntervalTicks == 0 && state.hazardExpiryTicks.Count > 0)
            {
                TickHazards(state, currentTick);
            }

            if (!state.animationActive && state.hazardExpiryTicks.Count == 0)
            {
                Active.Remove(pawn);
            }
        }

        private static void TickAnimation(IcecreamTailSterilizerState state)
        {
            Pawn pawn = state.pawn;
            pawn.Rotation = Rot4.FromAngleFlat(state.baseAngle);
            pawn.pather.StopDead();
            EnsureLockJob(pawn);
            state.elapsedTicks++;

            if (state.elapsedTicks <= RiseTicks)
            {
                if (state.elapsedTicks <= RiseTicks - RiseEffectLeadOutTicks &&
                    state.elapsedTicks % GlowIntervalTicks == 0)
                {
                    SpawnRainbowGlow(state);
                }
                if (state.elapsedTicks <= RiseTicks - RiseEffectLeadOutTicks &&
                    state.elapsedTicks % HaloIntervalTicks == 0)
                {
                    SpawnSoftGlow(state);
                }
                return;
            }

            int sweepElapsed = state.elapsedTicks - RiseTicks;
            if (sweepElapsed % GasIntervalTicks == 0)
            {
                if (!state.spraySoundPlayed)
                {
                    state.spraySoundPlayed = true;
                    PlaySpraySound(pawn);
                }
                RegisterHazardSweep(state, sweepElapsed);
            }

            if (state.elapsedTicks >= TotalTicks)
            {
                StopAnimation(state);
            }
        }

        private static bool IsAnimationValid(IcecreamTailSterilizerState state)
        {
            Pawn pawn = state.pawn;
            return pawn != null && !pawn.DestroyedOrNull() && !pawn.Dead && !pawn.Downed && pawn.Spawned &&
                pawn.Map == state.map && pawn.Position == state.originCell &&
                IcecreamTailTemporaryAbilityUtility.IsMatchaEligible(pawn);
        }

        public static Vector3 GetVisualOffset(Pawn pawn)
        {
            IcecreamTailSterilizerState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state) || !state.animationActive)
            {
                return Vector3.zero;
            }

            return Vector3.up * (RiseHeight * LiftProgress(state.elapsedTicks));
        }

        public static void DrawWeapon(Pawn pawn)
        {
            IcecreamTailSterilizerState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state) || !state.animationActive ||
                pawn.Map == null || !pawn.Spawned)
            {
                return;
            }

            float scale;
            float alpha;
            float weaponAngle;
            GetWeaponVisual(state, out scale, out alpha, out weaponAngle);
            if (scale <= 0.001f || alpha <= 0.001f)
            {
                return;
            }

            Vector3 direction = FlatDirection(weaponAngle);
            Vector3 pivot = pawn.DrawPos - FlatDirection(state.baseAngle) * 0.22f + Vector3.up * 0.03f;
            Vector3 center = pivot + direction * (scale * 0.5f);
            Quaternion rotation = Quaternion.AngleAxis(weaponAngle - 90f, Vector3.up);
            Vector3 drawScale = new Vector3(scale, 1f, scale * 0.5625f);
            Color color = state.elapsedTicks <= RiseTicks
                ? RainbowColor((state.elapsedTicks / 10) % 6)
                : Color.white;
            color.a = alpha;
            Material material = MaterialPool.MatFrom(WeaponTexturePath, ShaderDatabase.Transparent, color);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, rotation, drawScale), material, 0);
        }

        public static void DrawPreview(Pawn pawn, IntVec3 targetCell)
        {
            if (pawn == null || pawn.Map == null || targetCell == pawn.Position || !targetCell.InBounds(pawn.Map))
            {
                return;
            }

            float baseAngle = (targetCell - pawn.Position).AngleFlat;
            List<IntVec3> cells = new List<IntVec3>();
            CollectVisibleConeCells(pawn.Map, pawn.Position, baseAngle, -HalfSweepAngle, HalfSweepAngle,
                MaximumRange, cells);
            if (cells.Count > 0)
            {
                GenDraw.DrawFieldEdges(cells, Color.green, 0.1f, null, 0);
            }
        }

        private static void RegisterHazardSweep(IcecreamTailSterilizerState state, int sweepElapsed)
        {
            float progress = Mathf.Clamp01((float)sweepElapsed / SweepTicks);
            float currentOffset = Mathf.Lerp(-HalfSweepAngle, HalfSweepAngle, EaseInOutCubic(progress));
            float range = Mathf.Lerp(1f, MaximumRange,
                EaseInOutCubic(Mathf.Clamp01((float)sweepElapsed / GasExpansionTicks)));
            CollectVisibleConeCells(state.map, state.originCell, state.baseAngle, -HalfSweepAngle, currentOffset,
                range, state.visibleCells);
            for (int i = 0; i < state.visibleCells.Count; i++)
            {
                IntVec3 cell = state.visibleCells[i];
                if (!state.hazardExpiryTicks.ContainsKey(cell))
                {
                    state.hazardExpiryTicks.Add(cell, Find.TickManager.TicksGame + HazardLifetimeTicks);
                    SpawnGasCell(state, cell, HazardLifetimeTicks);
                }
            }
        }

        private static void CollectVisibleConeCells(Map map, IntVec3 origin, float baseAngle, float minimumOffset,
            float maximumOffset, float range, List<IntVec3> cells)
        {
            cells.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, range, true))
            {
                if (cell == origin || !cell.InBounds(map) || origin.DistanceTo(cell) > range)
                {
                    continue;
                }

                float offset = Mathf.DeltaAngle(baseAngle, (cell - origin).AngleFlat);
                if (offset < minimumOffset || offset > maximumOffset || !HasClearPath(map, origin, cell))
                {
                    continue;
                }

                cells.Add(cell);
            }
        }

        private static void TickHazards(IcecreamTailSterilizerState state, int currentTick)
        {
            ExpiredCellBuffer.Clear();
            foreach (KeyValuePair<IntVec3, int> hazard in state.hazardExpiryTicks)
            {
                if (hazard.Value <= currentTick)
                {
                    ExpiredCellBuffer.Add(hazard.Key);
                    continue;
                }

                List<Thing> things = hazard.Key.GetThingList(state.map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn target = things[i] as Pawn;
                    if (CanAffect(state, target))
                    {
                        ApplyEffects(state, target);
                    }
                }
            }

            for (int i = 0; i < ExpiredCellBuffer.Count; i++)
            {
                state.hazardExpiryTicks.Remove(ExpiredCellBuffer[i]);
            }
        }

        private static bool CanAffect(IcecreamTailSterilizerState state, Pawn target)
        {
            return target != null && target != state.pawn && !target.DestroyedOrNull() && !target.Dead &&
                target.Spawned && target.Map == state.map && target.health != null && target.RaceProps != null &&
                target.RaceProps.IsFlesh && !state.affectedPawns.Contains(target);
        }

        private static void ApplyEffects(IcecreamTailSterilizerState state, Pawn target)
        {
            state.affectedPawns.Add(target);
            if (target.HostileTo(state.pawn))
            {
                ApplyOrRefreshHediff(target, ToxicIrritationDefName);
                ApplyOrRefreshHediff(target, FoodPoisoningDefName);
                GainOrRefreshMood(target, EnemyThoughtDefName);
            }
            else
            {
                ApplyOrRefreshHediff(target, FoodPoisoningDefName);
                GainOrRefreshMood(target, FriendlyThoughtDefName);
            }
        }

        private static void ApplyOrRefreshHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def == null || pawn == null || pawn.health == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(def, pawn));
                return;
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ResetElapsedTicks();
            }
        }

        private static void GainOrRefreshMood(Pawn pawn, string defName)
        {
            if (IcecreamTailMod.Settings != null && !IcecreamTailMod.Settings.EnableMoodEffects)
            {
                return;
            }

            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
            if (def == null || pawn == null || pawn.needs == null || pawn.needs.mood == null ||
                pawn.needs.mood.thoughts == null || pawn.needs.mood.thoughts.memories == null)
            {
                return;
            }

            Thought_Memory memory = pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def);
            if (memory == null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(def);
            }
            else
            {
                memory.age = 0;
            }
        }

        private static void SpawnRainbowGlow(IcecreamTailSterilizerState state)
        {
            if (state.map.moteCounter.SaturatedLowPriority)
            {
                return;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(GlowMoteDefName);
            if (def == null)
            {
                return;
            }

            float scale;
            float alpha;
            float angle;
            GetWeaponVisual(state, out scale, out alpha, out angle);
            Vector3 center = state.pawn.DrawPos + FlatDirection(angle) * (scale * 0.25f);
            Vector3 position = center + new Vector3(Rand.Range(-0.45f, 0.45f), 0.05f,
                Rand.Range(-0.45f, 0.45f));
            MoteThrown mote = MoteMaker.MakeStaticMote(position, state.map, def, Rand.Range(0.35f, 0.60f),
                true, Rand.Range(0f, 360f)) as MoteThrown;
            if (mote != null)
            {
                mote.instanceColor = RainbowColor(Rand.Range(0, 6));
            }
        }

        private static void SpawnSoftGlow(IcecreamTailSterilizerState state)
        {
            if (state.map.moteCounter.SaturatedLowPriority)
            {
                return;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(HaloMoteDefName);
            if (def == null)
            {
                return;
            }

            float scale;
            float alpha;
            float angle;
            GetWeaponVisual(state, out scale, out alpha, out angle);
            Vector3 center = state.pawn.DrawPos + FlatDirection(angle) * (scale * 0.25f);
            MoteThrown mote = MoteMaker.MakeStaticMote(center, state.map, def,
                Mathf.Lerp(0.6f, 1.15f, scale / 3f), true, 0f) as MoteThrown;
            if (mote != null)
            {
                Color color = RainbowColor((state.elapsedTicks / 10) % 6);
                color.a = 0.38f;
                mote.instanceColor = color;
            }
        }

        private static void SpawnGasCell(IcecreamTailSterilizerState state, IntVec3 cell, int remainingTicks)
        {
            if (remainingTicks <= 0 || state.map == null || state.map.moteCounter.SaturatedLowPriority)
            {
                return;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(GasMoteDefName);
            if (def == null)
            {
                return;
            }

            Vector3 position = cell.ToVector3Shifted();
            position.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            MoteThrown mote = MoteMaker.MakeStaticMote(position, state.map, def, Rand.Range(1.55f, 2.20f),
                false, Rand.Range(0f, 360f)) as MoteThrown;
            if (mote != null)
            {
                mote.instanceColor = new Color(0.45f, 0.90f, 0.43f, 0.66f);
                mote.rotationRate = Rand.Range(-18f, 18f);
                mote.SetVelocity(Rand.Range(0f, 360f), Rand.Range(0.015f, 0.045f));
                float remainingSeconds = (float)remainingTicks / GenTicks.TicksPerRealSecond;
                mote.solidTimeOverride = Mathf.Max(0f, remainingSeconds - 1f);
            }
        }

        private static void GetWeaponVisual(IcecreamTailSterilizerState state, out float scale, out float alpha,
            out float angle)
        {
            if (state.elapsedTicks <= RiseTicks)
            {
                float progress = EaseInOutCubic(Mathf.Clamp01((float)state.elapsedTicks / RiseTicks));
                scale = 3f * progress;
                alpha = progress;
                angle = 0f;
                return;
            }

            int sweepElapsed = state.elapsedTicks - RiseTicks;
            float sweepProgress = Mathf.Clamp01((float)sweepElapsed / SweepTicks);
            scale = 3f;
            alpha = sweepElapsed <= SweepTicks - FadeTicks
                ? 1f
                : 1f - EaseInOutCubic(Mathf.Clamp01((float)(sweepElapsed - (SweepTicks - FadeTicks)) / FadeTicks));
            angle = state.baseAngle + Mathf.Lerp(-HalfSweepAngle, HalfSweepAngle, EaseInOutCubic(sweepProgress));
        }

        private static float LiftProgress(int elapsedTicks)
        {
            if (elapsedTicks <= RiseTicks)
            {
                return EaseInOutCubic(Mathf.Clamp01((float)elapsedTicks / RiseTicks));
            }

            int sweepElapsed = elapsedTicks - RiseTicks;
            if (sweepElapsed <= SweepTicks - FadeTicks)
            {
                return 1f;
            }

            return 1f - EaseInOutCubic(Mathf.Clamp01((float)(sweepElapsed - (SweepTicks - FadeTicks)) /
                FadeTicks));
        }

        private static bool HasClearPath(Map map, IntVec3 origin, IntVec3 destination)
        {
            int x = origin.x;
            int z = origin.z;
            int deltaX = Mathf.Abs(destination.x - x);
            int stepX = x < destination.x ? 1 : -1;
            int deltaZ = -Mathf.Abs(destination.z - z);
            int stepZ = z < destination.z ? 1 : -1;
            int error = deltaX + deltaZ;

            while (x != destination.x || z != destination.z)
            {
                int doubledError = 2 * error;
                if (doubledError >= deltaZ)
                {
                    error += deltaZ;
                    x += stepX;
                }
                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    z += stepZ;
                }

                IntVec3 cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(map) || IsBlocking(map, cell))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsBlocking(Map map, IntVec3 cell)
        {
            Building building = cell.GetEdifice(map);
            if (building == null)
            {
                return false;
            }

            Building_Door door = building as Building_Door;
            return (door != null && !door.Open) || (building.def != null && building.def.building != null &&
                building.def.building.isWall);
        }

        private static Vector3 FlatDirection(float angle)
        {
            return Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
        }

        private static Color RainbowColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(1f, 0.22f, 0.30f, 0.95f);
                case 1: return new Color(1f, 0.63f, 0.18f, 0.95f);
                case 2: return new Color(1f, 0.92f, 0.20f, 0.95f);
                case 3: return new Color(0.34f, 1f, 0.38f, 0.95f);
                case 4: return new Color(0.25f, 0.70f, 1f, 0.95f);
                default: return new Color(0.72f, 0.38f, 1f, 0.95f);
            }
        }

        private static float EaseInOutCubic(float value)
        {
            return value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
        }

        private static void EnsureLockJob(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null || (pawn.CurJobDef != null && pawn.CurJobDef.defName == LockJobDefName))
            {
                return;
            }

            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(LockJobDefName);
            if (def != null)
            {
                pawn.jobs.StartJob(JobMaker.MakeJob(def), JobCondition.InterruptForced);
            }
        }

        private static void PlaySpraySound(Pawn pawn)
        {
            if (!IcecreamTailSkillAudio.Enabled)
            {
                return;
            }

            string defName = "IcecreamTailSkill7Sound" + Rand.RangeInclusive(1, 9);
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (sound == null)
            {
                Log.Error("Nivarian Icecream Tail: missing " + defName + " SoundDef.");
                return;
            }

            sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }

        public static void Cancel(Pawn pawn)
        {
            IcecreamTailSterilizerState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state))
            {
                return;
            }

            StopAnimation(state);
            if (state.hazardExpiryTicks.Count == 0)
            {
                Active.Remove(pawn);
            }
        }

        private static void Discard(Pawn pawn)
        {
            IcecreamTailSterilizerState state;
            if (pawn == null || !Active.TryGetValue(pawn, out state))
            {
                return;
            }

            StopAnimation(state);
            Active.Remove(pawn);
        }

        private static void StopAnimation(IcecreamTailSterilizerState state)
        {
            if (state == null || !state.animationActive)
            {
                return;
            }

            state.animationActive = false;
            Pawn pawn = state.pawn;
            if (pawn != null && pawn.jobs != null && pawn.CurJobDef != null &&
                pawn.CurJobDef.defName == LockJobDefName)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_DrawPos")]
    internal static class Patch_Pawn_DrawPos_IcecreamTailSterilizer
    {
        private static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            __result += IcecreamTailSterilizerRuntime.GetVisualOffset(__instance);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_PawnRenderer_DisableCache_IcecreamTailSterilizer
    {
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (IcecreamTailSterilizerRuntime.IsActive(___pawn))
            {
                disableCache = true;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    internal static class Patch_PawnRenderer_DrawSterilizer
    {
        private static void Postfix(Pawn ___pawn)
        {
            IcecreamTailSterilizerRuntime.DrawWeapon(___pawn);
        }
    }
}
