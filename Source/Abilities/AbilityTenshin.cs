using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// SKILL4 点辰
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailAbilityTenshin : IcecreamTailTemporaryAbility
    {
        public IcecreamTailAbilityTenshin() { }
        public IcecreamTailAbilityTenshin(Pawn pawn) : base(pawn) { }
        public IcecreamTailAbilityTenshin(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string reason;
            if (!IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(pawn, out reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Activate(target, dest);
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield break;
        }
    }

    public sealed class CompProperties_IcecreamTailTenshin : CompProperties_AbilityEffect
    {
        public CompProperties_IcecreamTailTenshin()
        {
            compClass = typeof(CompAbilityEffect_IcecreamTailTenshin);
        }
    }

    public sealed class CompAbilityEffect_IcecreamTailTenshin : CompAbilityEffect
    {
        private const int StunTicks = 300;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            string reason;
            if (caster == null || caster.Map == null ||
                !IcecreamTailTemporaryAbilityUtility.CanCastTemporaryAbility(caster, out reason))
            {
                return;
            }

            Map map = caster.Map;
            HashSet<Pawn> enemies = new HashSet<Pawn>();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    IntVec3 cell = caster.Position + new IntVec3(x, 0, z);
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    List<Thing> things = cell.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                    {
                        Pawn enemy = things[i] as Pawn;
                        if (enemy != null && enemy != caster && enemy.Spawned && !enemy.Dead && !enemy.Downed && enemy.HostileTo(caster))
                        {
                            enemies.Add(enemy);
                        }
                    }
                }
            }

            foreach (Pawn enemy in enemies)
            {
                enemy.stances.stunner.StunFor(StunTicks, caster, true, true, false);
                MoteMaker.ThrowText(enemy.DrawPos, map, "STUN", Color.white, 3.65f);
            }

            PlayResultSound(caster, enemies.Count > 0);
        }

        private static void PlayResultSound(Pawn caster, bool hit)
        {
            string defName = hit ? "IcecreamTailSkill4Hit" : "IcecreamTailSkill4Miss" + Rand.RangeInclusive(1, 4);
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(defName);
            if (sound == null)
            {
                Log.Error("Nivarian Icecream Tail: missing " + defName + " SoundDef.");
                return;
            }

            sound.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
        }
    }
}
