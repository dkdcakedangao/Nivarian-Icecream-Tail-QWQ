using RimWorld;
using Verse;

namespace NivarianIcecreamTail
{
    public class CompProperties_IcecreamTailTargeting : CompProperties_AbilityEffect
    {
        public CompProperties_IcecreamTailTargeting()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailTargeting);
        }
    }

    public class CompAbilityEffect_IcecreamTailTargeting : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            Pawn pawn = parent == null ? null : parent.pawn;
            if (pawn == null || pawn.Map == null || !target.IsValid ||
                (target.Thing != null && target.Thing.Map != pawn.Map))
            {
                return false;
            }

            IntVec3 cell = target.Cell;
            if (!cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map) || IsWall(pawn.Map, cell))
            {
                return false;
            }

            return base.Valid(target, showMessages);
        }

        private static bool IsWall(Map map, IntVec3 cell)
        {
            Building building = cell.GetEdifice(map);
            return building != null && building.def != null && building.def.building != null &&
                building.def.building.isWall;
        }
    }
}
