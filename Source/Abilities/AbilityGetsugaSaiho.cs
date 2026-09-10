using System.Collections.Generic;
using RimWorld;
using Verse;

// skill3 也就是月牙叉炮的技能入口喵~
// 范围是复用的，但是删除了线条
// 这个技能的动画真的是k了好久好久……
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityGetsugaSaiho : IcecreamTailTemporaryAbility
    {
        public IcecreamTailAbilityGetsugaSaiho() { }
        public IcecreamTailAbilityGetsugaSaiho(Pawn pawn) : base(pawn) { }
        public IcecreamTailAbilityGetsugaSaiho(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }
    }

    public sealed class Verb_CastAbilityGetsugaSaiho : Verb_CastAbility { }

    public sealed class CompProperties_IcecreamTailGetsugaSaiho : CompProperties_AbilityEffect
    {
        public CompProperties_IcecreamTailGetsugaSaiho()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailGetsugaSaiho);
        }
    }
    
    public sealed class CompAbilityEffect_IcecreamTailGetsugaSaiho : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            Pawn caster = parent == null ? null : parent.pawn;
            string reason;
            if (!IcecreamTailSkill3Runtime.CanTarget(caster, target.Pawn, out reason))
            {
                if (showMessages && !string.IsNullOrEmpty(reason))
                {
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            List<IntVec3> path;
            IntVec3 landingCell;
            if (!IcecreamTailSkill3Runtime.TryBuildApproach(caster, target.Pawn, out path, out landingCell))
            {
                if (showMessages)
                {
                    Messages.Message("目标身旁没有可用的落点。", MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, showMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            string reason;
            bool started = caster != null && IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(caster, out reason) &&
                IcecreamTailSkill3Runtime.Begin(caster, target.Pawn);
            if (!started && parent != null)
            {
                parent.ResetCooldown();
            }
        }
    }
}
