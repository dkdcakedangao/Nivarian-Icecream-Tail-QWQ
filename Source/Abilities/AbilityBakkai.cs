using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

// SKILL5 超级爆！爆！回！ 入口
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityBakkai : IcecreamTailTemporaryAbility
    {
        public IcecreamTailAbilityBakkai() { }
        public IcecreamTailAbilityBakkai(Pawn pawn) : base(pawn) { }
        public IcecreamTailAbilityBakkai(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(pawn, out reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (IcecreamTailBakkaiRuntime.IsActive(pawn))
            {
                Messages.Message("超级爆！爆！回！仍在进行。", pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Activate(target, dest);
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }
    }

    public sealed class Verb_CastAbilityBakkai : Verb_CastAbility
    {
        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            Pawn caster = CasterPawn;
            List<IntVec3> route;
            IntVec3 landingCell;
            if (caster == null || caster.Map == null || !target.IsValid || !CanHitTarget(target) ||
                !IcecreamTailBakkaiRuntime.TryBuildRoute(caster, target, out route, out landingCell))
            {
                return;
            }

            List<IntVec3> area = IcecreamTailBakkaiRuntime.BuildAttackArea(route, caster.Map).ToList();
            if (area.Count > 0)
            {
                GenDraw.DrawFieldEdges(area, Color.green, 0.1f, null, 0);
            }
        }
    }

    public sealed class CompProperties_IcecreamTailBakkai : CompProperties_IcecreamTailTargeting
    {
        public CompProperties_IcecreamTailBakkai()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailBakkai);
        }
    }

    public sealed class CompAbilityEffect_IcecreamTailBakkai : CompAbilityEffect_IcecreamTailTargeting
    {
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            Pawn caster = parent == null ? null : parent.pawn;
            if (caster == null || caster.Map == null || !target.IsValid ||
                (target.Thing != null && target.Thing.Map != caster.Map) || !target.Cell.InBounds(caster.Map))
            {
                return false;
            }

            List<IntVec3> route;
            IntVec3 landingCell;
            return IcecreamTailBakkaiRuntime.TryBuildRoute(caster, target, out route, out landingCell) &&
                base.Valid(target, showMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            string reason;
            bool started = caster != null &&
                IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(caster, out reason) &&
                IcecreamTailBakkaiRuntime.Begin(caster, target);
            if (!started && parent != null)
            {
                parent.ResetCooldown();
            }
        }
    }
}
