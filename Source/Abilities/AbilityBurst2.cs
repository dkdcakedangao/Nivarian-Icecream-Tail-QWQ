using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// SKILL2 迸！
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityBurst2 : IcecreamTailTemporaryAbility
    {
        public IcecreamTailAbilityBurst2() { }
        public IcecreamTailAbilityBurst2(Pawn pawn) : base(pawn) { }
        public IcecreamTailAbilityBurst2(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }
    }

    public sealed class Verb_CastAbilityBurst2 : Verb_CastAbility
    {
        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);

            Pawn caster = CasterPawn;
            if (caster == null || caster.Map == null || !target.IsValid || !CanHitTarget(target))
            {
                return;
            }

            IntVec3 targetCell = target.Pawn != null && target.Pawn.Spawned && target.Pawn.Map == caster.Map
                ? target.Pawn.Position
                : target.Cell;
            if (!targetCell.InBounds(caster.Map))
            {
                return;
            }

            List<IntVec3> path = new List<IntVec3>();
            List<IntVec3> line = GenSight.BresenhamCellsBetween(caster.Position, targetCell);
            for (int i = 1; i < line.Count; i++)
            {
                IntVec3 cell = line[i];
                if (!cell.InBounds(caster.Map) || IsWallBlocking(caster.Map, cell))
                {
                    break;
                }

                path.Add(cell);
            }

            if (path.Count > 0)
            {
                GenDraw.DrawFieldEdges(path, Color.green, 0.1f, null, 0);
            }
        }

        private static bool IsWallBlocking(Map map, IntVec3 cell)
        {
            Building building = cell.GetEdifice(map);
            return building != null && building.def != null && building.def.building != null &&
                building.def.building.isWall;
        }
    }

    public sealed class CompProperties_IcecreamTailBurst2 : CompProperties_IcecreamTailTargeting
    {
        public CompProperties_IcecreamTailBurst2()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailBurst2);
        }
    }

    public sealed class CompAbilityEffect_IcecreamTailBurst2 : CompAbilityEffect_IcecreamTailTargeting
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            string reason;
            bool started = caster != null && IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(caster, out reason) &&
                IcecreamTailBurst2Runtime.Begin(caster, target);
            if (!started)
            {
                if (parent != null)
                {
                    parent.ResetCooldown();
                }
                if (caster != null)
                {
                    IcecreamTailBurst2Runtime.Cancel(caster);
                }
            }
        }
    }

}
