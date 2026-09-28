using System.Collections.Generic;
using RimWorld;
using Verse;

// 灭菌器大人临时技能，入口、除错
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityMatchaSterilizer : IcecreamTailTemporaryAbility
    {
        public IcecreamTailAbilityMatchaSterilizer() { }
        public IcecreamTailAbilityMatchaSterilizer(Pawn pawn) : base(pawn) { }
        public IcecreamTailAbilityMatchaSterilizer(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastMatchaAbility(pawn, out reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (IcecreamTailSterilizerRuntime.IsActive(pawn))
            {
                Messages.Message("灭菌器大人仍在助阵。", pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Activate(target, dest);
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }
    }

    public sealed class Verb_CastAbilityMatchaSterilizer : Verb_CastAbility
    {
        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            Pawn caster = CasterPawn;
            if (caster != null && caster.Map != null && target.IsValid && CanHitTarget(target))
            {
                IcecreamTailSterilizerRuntime.DrawPreview(caster, target.Cell);
            }
        }
    }

    public sealed class CompProperties_IcecreamTailMatchaSterilizer : CompProperties_IcecreamTailTargeting
    {
        public CompProperties_IcecreamTailMatchaSterilizer()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailMatchaSterilizer);
        }
    }

    public sealed class CompAbilityEffect_IcecreamTailMatchaSterilizer : CompAbilityEffect_IcecreamTailTargeting
    {
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            Pawn caster = parent == null ? null : parent.pawn;
            return caster != null && target.IsValid && target.Cell != caster.Position &&
                base.Valid(target, showMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            string reason;
            bool started = caster != null &&
                IcecreamTailTemporaryAbilityUtility.CanCastMatchaAbility(caster, out reason) &&
                IcecreamTailSterilizerRuntime.Begin(caster, target.Cell);
            if (!started && parent != null)
            {
                parent.ResetCooldown();
            }
        }
    }
}
