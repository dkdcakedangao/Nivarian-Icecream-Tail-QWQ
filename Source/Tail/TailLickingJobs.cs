using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace NivarianIcecreamTail
{
    //右键
    public sealed class FloatMenuOptionProvider_IcecreamTail : FloatMenuOptionProvider
    {
        protected override bool Drafted { get { return true; } }
        protected override bool Undrafted { get { return true; } }
        protected override bool Multiselect { get { return false; } }
        protected override bool RequiresManipulation { get { return false; } }
        protected override bool MechanoidCanDo { get { return false; } }
        protected override bool CanSelfTarget { get { return false; } }

        protected override bool AppliesInt(FloatMenuContext context)
        {
            return IcecreamTailMod.Enabled && context.ClickedPawns.Any(IcecreamTailUtility.IsNivarian);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn target, FloatMenuContext context)
        {
            if (!IcecreamTailUtility.IsNivarian(target))
            {
                yield break;
            }

            Pawn eater = context.FirstSelectedPawn;
            if (eater == null || eater.Faction != Faction.OfPlayer || !eater.RaceProps.Humanlike)
            {
                yield break;
            }

            string reason;
            if (TailEatingUtility.CanLickTail(eater, target, false, 0, out reason))
            {
                FloatMenuOption option = new FloatMenuOption("舔舔冰淇淋尾巴", delegate { TailEatingUtility.StartLicking(eater, target, true); }, MenuOptionPriority.High);
                yield return FloatMenuUtility.DecoratePrioritizedTask(option, eater, target, null, null);
            }
            else
            {
                yield return new FloatMenuOption("舔舔冰淇淋尾巴（" + reason + "）", null);
            }
        }
    }

    public sealed class JobDriver_LickIcecreamTail : JobDriver
    {
        //job
        private List<string> animationDefNames = new List<string>();
        private List<int> animationFinishTicks = new List<int>();
        private bool startedAnimation;

        private Pawn TargetPawn
        {
            get { return TargetA.Pawn; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref animationDefNames, "animationDefNames", LookMode.Value);
            Scribe_Collections.Look(ref animationFinishTicks, "animationFinishTicks", LookMode.Value);
            Scribe_Values.Look(ref startedAnimation, "startedAnimation", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && startedAnimation)
            {
                animationDefNames = animationDefNames ?? new List<string>();
                animationFinishTicks = animationDefNames.Select(defName => 0).ToList();
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(delegate(JobCondition condition)
            {
                if (startedAnimation)
                {
                    FacialAnimationCompatibility.RemoveAnimations(pawn, animationDefNames);
                }

                TailEatingUtility.InterruptLickingPartner(pawn, TargetPawn, DefDatabase<JobDef>.GetNamedSilentFail("LickedIcecreamTail"), pawn);
            });
            this.FailOn(() => !TailEatingUtility.IsReadyTailPawn(TargetPawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
            Toil holdOwner = new Toil();
            holdOwner.initAction = delegate { TailEatingUtility.HoldTailOwner(TargetPawn, pawn); };
            holdOwner.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return holdOwner;
            Toil lick = Toils_General.Wait(TailEatingUtility.LickDurationTicks, TargetIndex.A).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            lick.AddPreInitAction(delegate
            {
                startedAnimation = FacialAnimationCompatibility.TryStartLickAnimation(pawn, animationDefNames, animationFinishTicks);
            });
            lick.tickAction = delegate
            {
                if (startedAnimation)
                {
                    FacialAnimationCompatibility.RefreshFinishedAnimations(pawn, animationDefNames, animationFinishTicks);
                }
            };
            lick.FailOn(() => !TailEatingUtility.IsReadyTailPawn(TargetPawn));
            yield return lick;
            Toil finish = new Toil();
            finish.initAction = delegate { TailEatingUtility.ConsumeTail(pawn, TargetPawn); };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    public sealed class JobDriver_LickedIcecreamTail : JobDriver
    {
        private List<string> animationDefNames = new List<string>();
        private List<int> animationFinishTicks = new List<int>();
        private bool startedAnimation;

        private Pawn EaterPawn
        {
            get { return TargetA.Pawn; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref animationDefNames, "animationDefNames", LookMode.Value);
            Scribe_Collections.Look(ref animationFinishTicks, "animationFinishTicks", LookMode.Value);
            Scribe_Values.Look(ref startedAnimation, "startedAnimation", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && startedAnimation)
            {
                animationDefNames = animationDefNames ?? new List<string>();
                animationFinishTicks = animationDefNames.Select(defName => 0).ToList();
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(delegate(JobCondition condition)
            {
                if (startedAnimation)
                {
                    FacialAnimationCompatibility.RemoveAnimations(pawn, animationDefNames);
                }

                if (condition != JobCondition.Succeeded)
                {
                    TailEatingUtility.InterruptLickingPartner(pawn, EaterPawn, DefDatabase<JobDef>.GetNamedSilentFail("LickIcecreamTail"), pawn);
                }
            });
            Toil wait = Toils_General.Wait(job.expiryInterval > 0 ? job.expiryInterval : 240);
            wait.AddPreInitAction(delegate
            {
                startedAnimation = FacialAnimationCompatibility.TryStartLickedTailAnimation(pawn, animationDefNames, animationFinishTicks);
            });
            wait.tickAction = delegate
            {
                if (startedAnimation)
                {
                    FacialAnimationCompatibility.RefreshFinishedAnimations(pawn, animationDefNames, animationFinishTicks);
                }
            };
            yield return wait;
        }
    }
}
