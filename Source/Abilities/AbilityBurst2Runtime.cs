using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// SKILL2 迸！ 的特殊效果
// 也有gpt老师的味道
// 这部分代码大部分都是gpt写的
namespace NivarianIcecreamTail
{
    internal enum IcecreamTailBurst2Phase
    {
        Moving,
        AfterLanding
    }

    internal sealed class IcecreamTailBurst2State
    {
        public IntVec3 startCell;
        public IntVec3 landingCell;
        public IcecreamTailBurst2Phase phase;
        public int ticksRemaining;
        public int moveElapsedTicks;
        public int moveTotalTicks;
        public int pathIndex;
        public bool empowered;
        public List<IntVec3> pathCells = new List<IntVec3>();
        public List<Pawn> stunnedPawns = new List<Pawn>();
    }

    public sealed class IcecreamTailBurst2GameComponent : GameComponent
    {
        public IcecreamTailBurst2GameComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            IcecreamTailBurst2Runtime.ExposeData();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            IcecreamTailBurst2Runtime.TickAll();
        }
    }

    internal sealed class IcecreamTailBurst2SavedState : IExposable
    {
        public Pawn pawn;
        public IntVec3 startCell;
        public IntVec3 landingCell;
        public int phase;
        public int ticksRemaining;
        public int moveElapsedTicks;
        public int moveTotalTicks;
        public int pathIndex;
        public bool empowered;
        public List<IntVec3> pathCells;
        public List<Pawn> stunnedPawns;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref startCell, "startCell");
            Scribe_Values.Look(ref landingCell, "landingCell");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining");
            Scribe_Values.Look(ref moveElapsedTicks, "moveElapsedTicks");
            Scribe_Values.Look(ref moveTotalTicks, "moveTotalTicks");
            Scribe_Values.Look(ref pathIndex, "pathIndex");
            Scribe_Values.Look(ref empowered, "empowered");
            Scribe_Collections.Look(ref pathCells, "pathCells", LookMode.Value);
            Scribe_Collections.Look(ref stunnedPawns, "stunnedPawns", LookMode.Reference);
        }
    }

    internal static class IcecreamTailBurst2Runtime
    {
        private static readonly Dictionary<Pawn, IcecreamTailBurst2State> Active = new Dictionary<Pawn, IcecreamTailBurst2State>();
        private static readonly Dictionary<Pawn, Pawn> PendingLargeStuns = new Dictionary<Pawn, Pawn>();
        private static readonly List<Pawn> TickBuffer = new List<Pawn>();

        public static void ExposeData()
        {
            List<IcecreamTailBurst2SavedState> saved = new List<IcecreamTailBurst2SavedState>();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saved = Active.Select(pair => new IcecreamTailBurst2SavedState
                {
                    pawn = pair.Key,
                    startCell = pair.Value.startCell,
                    landingCell = pair.Value.landingCell,
                    phase = (int)pair.Value.phase,
                    ticksRemaining = pair.Value.ticksRemaining,
                    moveElapsedTicks = pair.Value.moveElapsedTicks,
                    moveTotalTicks = pair.Value.moveTotalTicks,
                    pathIndex = pair.Value.pathIndex,
                    empowered = pair.Value.empowered,
                    pathCells = pair.Value.pathCells,
                    stunnedPawns = pair.Value.stunnedPawns
                }).ToList();
            }

            Scribe_Collections.Look(ref saved, "burst2States", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Active.Clear();
                if (saved == null) return;
                foreach (IcecreamTailBurst2SavedState item in saved)
                {
                    if (item.pawn == null) continue;
                    AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.Burst2AbilityDefName);
                    Ability ability = item.pawn.abilities == null || def == null
                        ? null
                        : item.pawn.abilities.GetAbility(def, false);
                    if (ability == null)
                    {
                        continue;
                    }

                    Active[item.pawn] = new IcecreamTailBurst2State
                    {
                        startCell = item.startCell,
                        landingCell = item.landingCell,
                        phase = (IcecreamTailBurst2Phase)item.phase,
                        ticksRemaining = item.ticksRemaining,
                        empowered = item.empowered,
                        moveElapsedTicks = item.moveElapsedTicks,
                        moveTotalTicks = item.moveTotalTicks,
                        pathIndex = item.pathIndex,
                        pathCells = item.pathCells ?? new List<IntVec3>(),
                        stunnedPawns = item.stunnedPawns ?? new List<Pawn>()
                    };
                }
            }
        }

        public static bool Begin(Pawn pawn, LocalTargetInfo target)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return false;
            List<IntVec3> path;
            IntVec3 landingCell;
            if (!TryBuildPath(pawn, target, out path, out landingCell)) return false;
            if (landingCell == pawn.Position && target.Cell != pawn.Position)
            {
                pawn.Rotation = Rot4.FromAngleFlat((target.Cell - pawn.Position).AngleFlat);
            }

            Active[pawn] = new IcecreamTailBurst2State
            {
                startCell = pawn.Position,
                landingCell = landingCell,
                pathCells = path,
                phase = IcecreamTailBurst2Phase.Moving,
                ticksRemaining = 0,
                moveElapsedTicks = 0,
                moveTotalTicks = Mathf.Max(1, path.Count * 4),
                pathIndex = 0,
                empowered = false
            };
            pawn.pather.StopDead();
            PlaySound("IcecreamTailSkillBurst21", new TargetInfo(pawn.Position, pawn.Map));
            AdvanceMovement(pawn, Active[pawn], 0);
            return true;
        }

        public static void OnDamage(Pawn pawn, float damage)
        {
            IcecreamTailBurst2State state;
            if (damage > 0f && pawn != null && Active.TryGetValue(pawn, out state) &&
                state.phase == IcecreamTailBurst2Phase.AfterLanding)
            {
                state.empowered = true;
            }
        }

        public static void Tick(Pawn pawn, int delta)
        {
            IcecreamTailBurst2State state;
            if (pawn == null || !Active.TryGetValue(pawn, out state)) return;
            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(pawn, out reason) || pawn.Dead ||
                !pawn.Spawned || pawn.Map == null)
            {
                Cancel(pawn);
                return;
            }

            state.ticksRemaining -= delta;
            if (state.phase == IcecreamTailBurst2Phase.Moving)
            {
                AdvanceMovement(pawn, state, delta);
                return;
            }
            if (state.phase == IcecreamTailBurst2Phase.AfterLanding && state.ticksRemaining <= 0)
            {
                if (state.empowered)
                {
                    ExecuteExplosion(pawn, pawn.Map, state.landingCell, 2.5f, MeleeDamage(pawn) * 2, 240, true);
                }
                Cancel(pawn);
            }
        }

        public static void TickAll()
        {
            foreach (Pawn enemy in PendingLargeStuns.Keys.ToList())
            {
                Pawn caster = PendingLargeStuns[enemy];
                if (enemy == null || enemy.DestroyedOrNull() || enemy.Spawned ||
                    caster == null || caster.DestroyedOrNull() || caster.Dead)
                {
                    PendingLargeStuns.Remove(enemy);
                }
            }

            TickBuffer.Clear();
            TickBuffer.AddRange(Active.Keys);
            foreach (Pawn pawn in TickBuffer)
            {
                Tick(pawn, 1);
            }
        }

        public static void Cancel(Pawn pawn)
        {
            if (pawn != null) Active.Remove(pawn);
        }

        public static bool IsActive(Pawn pawn)
        {
            return pawn != null && Active.ContainsKey(pawn);
        }

        public static Vector3 GetVisualOffset(Pawn pawn)
        {
            IcecreamTailBurst2State state;
            if (pawn == null || !Active.TryGetValue(pawn, out state) || state.phase != IcecreamTailBurst2Phase.Moving ||
                state.moveTotalTicks <= 0)
            {
                return Vector3.zero;
            }

            float progress = Mathf.Clamp01((float)state.moveElapsedTicks / state.moveTotalTicks);
            IntVec3 delta = state.landingCell - state.startCell;
            return new Vector3(delta.x, 0f, delta.z) * progress;
        }

        private static int MeleeDamage(Pawn pawn)
        {
            return Mathf.Max(1, Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.MeleeDPS)));
        }

        public static bool TryBuildPath(Pawn pawn, LocalTargetInfo target, out List<IntVec3> path, out IntVec3 landingCell)
        {
            path = new List<IntVec3>();
            landingCell = IntVec3.Invalid;
            if (pawn == null || pawn.Map == null || !target.IsValid)
            {
                return false;
            }

            IntVec3 targetCell = target.Cell;
            Map map = pawn.Map;
            bool targetPawnOnMap = target.Pawn != null && target.Pawn.Spawned && target.Pawn.Map == map;
            if (!targetCell.InBounds(map) || !targetCell.Standable(map) || IsWallBlocking(map, targetCell))
            {
                return false;
            }

            List<IntVec3> line = GenSight.BresenhamCellsBetween(pawn.Position, targetCell);
            if (line.Count < 2)
            {
                return false;
            }

            for (int i = 1; i < line.Count; i++)
            {
                IntVec3 cell = line[i];
                if (!cell.InBounds(map) || IsWallBlocking(map, cell))
                {
                    return false;
                }

                path.Add(cell);
            }

            int landingIndex = path.Count - 1;
            while (landingIndex >= 0 && !IsLegalLandingCell(pawn, path[landingIndex]))
            {
                landingIndex--;
            }

            if (landingIndex < 0)
            {
                bool targetIsAdjacent = Math.Abs(targetCell.x - pawn.Position.x) <= 1 &&
                    Math.Abs(targetCell.z - pawn.Position.z) <= 1;
                if (targetPawnOnMap && targetIsAdjacent)
                {
                    path.Clear();
                    landingCell = pawn.Position;
                    return true;
                }

                path.Clear();
                return false;
            }

            if (landingIndex < path.Count - 1)
            {
                path.RemoveRange(landingIndex + 1, path.Count - landingIndex - 1);
            }

            landingCell = path[landingIndex];
            if (!targetPawnOnMap && landingCell != targetCell)
            {
                path.Clear();
                landingCell = IntVec3.Invalid;
                return false;
            }

            return path.Count > 0;
        }

        private static bool IsLegalLandingCell(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null || !cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map))
            {
                return false;
            }

            Pawn occupant = cell.GetFirstPawn(pawn.Map);
            return occupant == null || occupant == pawn;
        }

        private static void AdvanceMovement(Pawn pawn, IcecreamTailBurst2State state, int delta)
        {
            Map map = pawn.Map;
            state.moveElapsedTicks = Mathf.Min(state.moveTotalTicks, state.moveElapsedTicks + delta);
            int cellsToProcess = Mathf.Min(state.pathCells.Count,
                Mathf.CeilToInt((float)state.moveElapsedTicks / Mathf.Max(1, state.moveTotalTicks) * state.pathCells.Count));
            while (state.pathIndex < cellsToProcess)
            {
                IntVec3 cell = state.pathCells[state.pathIndex++];
                if (!cell.InBounds(map) || IsWallBlocking(map, cell))
                {
                    break;
                }

                List<Thing> things = cell.GetThingList(map);
                if (things != null)
                {
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing thing = things[i];
                        Pawn other = thing as Pawn;
                        if (other == null || other == pawn) continue;
                        if (other.HostileTo(pawn) && !state.stunnedPawns.Contains(other))
                        {
                            other.stances.stunner.StunFor(60, pawn, true, true, true);
                            state.stunnedPawns.Add(other);
                        }
                    }
                }
            }

            if (state.moveElapsedTicks >= state.moveTotalTicks)
            {
                if (state.landingCell != state.startCell)
                {
                    pawn.Rotation = Rot4.FromAngleFlat((state.landingCell - state.startCell).AngleFlat);
                }

                pawn.Position = state.landingCell;
                pawn.Drawer.tweener.ResetTweenedPosToRoot();
                pawn.Notify_Teleported(true, true);
                ExecuteExplosion(pawn, map, state.landingCell, 1.5f, MeleeDamage(pawn), 120, false);
                state.phase = IcecreamTailBurst2Phase.AfterLanding;
                state.ticksRemaining = 120;
            }
        }

        private static void ExecuteExplosion(Pawn pawn, Map map, IntVec3 center, float radius, int damage, int stunTicks, bool empowered)
        {
            if (pawn == null || map == null || !center.InBounds(map)) return;
            List<Pawn> enemies = GenRadial.RadialDistinctThingsAround(center, map, radius, true)
                .OfType<Pawn>()
                .Where(other => other != pawn && other.HostileTo(pawn))
                .Distinct()
                .ToList();
            List<Thing> ignored = GenRadial.RadialDistinctThingsAround(center, map, radius, true)
                .Where(thing => !enemies.Contains(thing))
                .ToList();
            SoundDef attackSound = GetRandomAttackSound();
            if (attackSound == null)
            {
                Log.Error("Nivarian Icecream Tail: missing attack SoundDef.");
            }
            GenExplosion.DoExplosion(center, map, radius, DamageDefOf.Blunt, pawn, damage, -1f, attackSound, null, null, null, null, ignoredThings: ignored);
            foreach (Pawn enemy in enemies)
            {
                if (enemy.DestroyedOrNull() || enemy.Dead) continue;
                if (empowered)
                {
                    if (Knockback(pawn, map, enemy, center))
                    {
                        PendingLargeStuns[enemy] = pawn;
                    }
                    else
                    {
                        enemy.stances.stunner.StunFor(stunTicks, pawn, true, true, true);
                    }
                }
                else
                {
                    enemy.stances.stunner.StunFor(stunTicks, pawn, true, true, true);
                }
            }
        }

        public static void OnKnockbackLanded(Pawn enemy)
        {
            Pawn caster;
            if (enemy == null || !PendingLargeStuns.TryGetValue(enemy, out caster))
            {
                return;
            }

            PendingLargeStuns.Remove(enemy);
            if (!enemy.DestroyedOrNull() && !enemy.Dead && enemy.Spawned)
            {
                enemy.stances.stunner.StunFor(240, caster, true, true, true);
            }
        }

        private static bool IsWallBlocking(Map map, IntVec3 cell)
        {
            Building edifice = cell.GetEdifice(map);
            return edifice != null && edifice.def != null && edifice.def.building != null && edifice.def.building.isWall;
        }

        private static void PlaySound(string defName, TargetInfo target)
        {
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (sound == null)
            {
                Log.Error("Nivarian Icecream Tail: missing " + defName + " SoundDef.");
                return;
            }

            if (target.Map != null)
            {
                sound.PlayOneShot(target);
            }
        }

        private static SoundDef GetRandomAttackSound()
        {
            string defName = "IcecreamTailSkillAttack" + Rand.RangeInclusive(1, 3);
            return DefDatabase<SoundDef>.GetNamedSilentFail(defName);
        }

        private static bool Knockback(Pawn pawn, Map map, Pawn enemy, IntVec3 center)
        {
            IntVec3 delta = enemy.Position - center;
            if (delta.x == 0 && delta.z == 0) return false;
            Vector3 direction = new Vector3(delta.x, 0f, delta.z).normalized;
            IntVec3 desired = enemy.Position + new IntVec3(Mathf.RoundToInt(direction.x * 2f), 0, Mathf.RoundToInt(direction.z * 2f));
            IntVec3 destination = IntVec3.Invalid;
            for (int radius = 0; radius <= 2 && !destination.IsValid; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(desired, radius, true))
                {
                    if (cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null &&
                        cell.GetFirstPawn(map) == null)
                    {
                        destination = cell;
                        break;
                    }
                }
            }
            if (!destination.IsValid) return false;
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
            if (flyer != null)
            {
                GenSpawn.Spawn(flyer, startCell, map);
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
    public static class Patch_Pawn_PostApplyDamage_IcecreamTailBurst2
    {
        public static void Postfix(Pawn __instance, float totalDamageDealt)
        {
            IcecreamTailBurst2Runtime.OnDamage(__instance, totalDamageDealt);
        }
    }

    [HarmonyPatch(typeof(PawnFlyer), "RespawnPawn")]
    public static class Patch_PawnFlyer_RespawnPawn_IcecreamTailBurst2
    {
        public static void Prefix(PawnFlyer __instance, ref Pawn __state)
        {
            __state = __instance == null ? null : __instance.FlyingPawn;
        }

        public static void Postfix(Pawn __state)
        {
            IcecreamTailBurst2Runtime.OnKnockbackLanded(__state);
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_DrawPos")]
    public static class Patch_Pawn_DrawPos_IcecreamTailBurst2
    {
        public static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            if (__instance != null)
            {
                __result += IcecreamTailBurst2Runtime.GetVisualOffset(__instance);
            }
        }
    }
}
