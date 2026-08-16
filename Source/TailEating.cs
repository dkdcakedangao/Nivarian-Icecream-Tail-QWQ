using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace NivarianIcecreamTail
// 很难想象，我实在是找不到原版游戏的那种饥饿速率是怎么写的，总之是用了神秘的小思路，写了个奇妙的小简介。可能……大概……肯定……是要优化的吧？
{
    public sealed class Hediff_IcecreamTailRecovery : HediffWithComps
    {
        public override string LabelBase
        {
            get { return "冰淇淋尾巴恢复中（" + Severity.ToStringPercent() + "）"; }
        }

        public override string TipStringExtra
        {
            get
            {
                string tip = "恢复进度：" + Severity.ToStringPercent();
                if (IcecreamTailMod.Settings == null || IcecreamTailMod.Settings.EnableRecoveryHunger)
                {
                    tip += "\n饥饿速度：110%";
                }

                return tip;
            }
        }
    }

    public sealed class Hediff_IcecreamTailRecoveryHunger : HediffWithComps
    {
        public override bool Visible
        {
            get { return false; }
        }
    }

    public sealed class HediffCompProperties_IcecreamTailRecovery : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailRecovery()
        {
            compClass = typeof(HediffComp_IcecreamTailRecovery);
        }
    }

    public sealed class HediffComp_IcecreamTailRecovery : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            BodyPartRecord tailPart;
            if (!IcecreamTailMod.Enabled || IcecreamTailUtility.GetTailKind(parent.pawn, out tailPart) == TailKind.None || parent.Part != tailPart)
            {
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("恢复被取消：" + IcecreamTailDebug.PawnInfo(parent.pawn) + "，原因=" + (!IcecreamTailMod.Enabled ? "总开关关闭" : "尾巴不可用或部位已变更"));
                }

                TailEatingUtility.RemoveRecovery(parent.pawn);
                return;
            }

            TailEatingUtility.SyncRecoveryHunger(parent.pawn);
            float days = TailEatingUtility.RecoveryDays(parent.pawn) * IcecreamTailMod.Settings.RecoveryTimeMultiplier;
            float increase = delta / (60000f * days) * TailEatingUtility.CoreRecoveryFactor(parent.pawn);
            if (parent.Severity + increase >= 1f)
            {
                TailEatingUtility.FinishRecovery(parent.pawn, parent);
                return;
            }

            severityAdjustment += increase;
        }
    }

    public static class TailEatingUtility
    {
        private const string RecoveryDefName = "IcecreamTailRecovery";
        private const string HungerDefName = "IcecreamTailRecoveryHunger";
        private const string BuffDefName = "IcecreamTailEaterBuff";
        private const string IcyCoreDefName = "Nivarian_IcyCore";
        private const string EaterMoodThoughtDefName = "IcecreamTailMemoryEater";
        private const string OwnerMoodThoughtDefName = "IcecreamTailMemoryOwner";
        private const string EaterSocialThoughtDefName = "IcecreamTailSocialEater";
        private const string OwnerSocialThoughtDefName = "IcecreamTailSocialOwner";
        private const string LickJobDefName = "LickIcecreamTail";
        private const int BaseLickDurationTicks = 600;

        public static int LickDurationTicks
        {
            get
            {
                float multiplier = IcecreamTailMod.Settings == null ? 1f : Math.Max(0.1f, Math.Min(10f, IcecreamTailMod.Settings.LickTimeMultiplier));
                return (int)Math.Round(BaseLickDurationTicks * multiplier);
            }
        }

        private static Hediff GetHediff(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            return pawn.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def.defName == defName);
        }

        public static Hediff GetRecovery(Pawn pawn)
        {
            return GetHediff(pawn, RecoveryDefName);
        }

        public static bool IsReadyTailPawn(Pawn pawn)
        {
            BodyPartRecord tailPart;
            return IcecreamTailMod.Enabled && IcecreamTailUtility.GetTailKind(pawn, out tailPart) != TailKind.None && IcecreamTailUtility.GetPlaceholder(pawn) != null && GetRecovery(pawn) == null;
        }

        public static float RecoveryDays(Pawn pawn)
        {
            BodyPartRecord tailPart;
            TailKind kind = IcecreamTailUtility.GetTailKind(pawn, out tailPart);
            if (kind == TailKind.Bionic)
            {
                return 0.8f;
            }

            return kind == TailKind.Fake ? 1.5f : 1f;
        }

        public static float CoreRecoveryFactor(Pawn pawn)
        {
            if (!IcecreamTailMod.Settings.EnableCoreRecoveryEffect)
            {
                return 1f;
            }

            Hediff core = GetHediff(pawn, IcyCoreDefName);
            if (core == null)
            {
                return 1f;
            }

            if (core.Severity < 0.4f)
            {
                return 1.25f;
            }

            if (core.Severity < 0.7f)
            {
                return 1.1f;
            }

            if (core.Severity < 0.85f)
            {
                return 1f;
            }

            if (core.Severity < 0.95f)
            {
                return 0.75f;
            }

            return 0.5f;
        }

        public static void StartRecovery(Pawn pawn)
        {
            BodyPartRecord tailPart;
            if (pawn == null || !IcecreamTailMod.Enabled || IcecreamTailUtility.GetTailKind(pawn, out tailPart) == TailKind.None || GetRecovery(pawn) != null)
            {
                return;
            }

            HediffDef recoveryDef = DefDatabase<HediffDef>.GetNamedSilentFail(RecoveryDefName);
            if (recoveryDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing IcecreamTailRecovery HediffDef.");
                return;
            }

            Hediff recovery = HediffMaker.MakeHediff(recoveryDef, pawn, tailPart);
            recovery.Severity = 0.001f;
            pawn.health.AddHediff(recovery, tailPart);
            if (IcecreamTailDebug.TailEnabled)
            {
                float baseDays = RecoveryDays(pawn);
                float coreFactor = CoreRecoveryFactor(pawn);
                float finalDays = baseDays * IcecreamTailMod.Settings.RecoveryTimeMultiplier / coreFactor;
                IcecreamTailDebug.Tail("恢复开始：" + IcecreamTailDebug.PawnInfo(pawn) + "，尾巴类型=" + IcecreamTailUtility.GetTailKind(pawn, out tailPart) + "，基础=" + baseDays.ToString("0.0") + " 天，时间倍率=" + IcecreamTailMod.Settings.RecoveryTimeMultiplier.ToString("0.0") + "，核心倍率=" + coreFactor.ToString("0.00") + "，预计=" + finalDays.ToString("0.00") + " 天。");
            }

            SyncRecoveryHunger(pawn);
        }

        public static void FinishRecovery(Pawn pawn, Hediff recovery)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            if (recovery != null)
            {
                pawn.health.RemoveHediff(recovery);
            }

            RemoveRecoveryHunger(pawn);
            if (IcecreamTailMod.Enabled)
            {
                IcecreamTailUtility.TryAddPlaceholder(pawn);
            }
            // 冰淇淋尾巴长好了，到底要叫什么？成熟？？结冰？反正debug，没人看的，就写成熟吧
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("恢复完成：" + IcecreamTailDebug.PawnInfo(pawn) + "，已尝试恢复成熟冰淇淋尾巴。");
            }
        }

        public static void RemoveRecovery(Pawn pawn)
        {
            Hediff recovery = GetRecovery(pawn);
            if (recovery != null && pawn.health != null)
            {
                pawn.health.RemoveHediff(recovery);
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已移除恢复状态：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }

            RemoveRecoveryHunger(pawn);
        }

        public static void SyncRecoveryHunger(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || !IcecreamTailMod.Enabled || !IcecreamTailMod.Settings.EnableRecoveryHunger || GetRecovery(pawn) == null)
            {
                RemoveRecoveryHunger(pawn);
                return;
            }

            if (GetHediff(pawn, HungerDefName) != null)
            {
                return;
            }

            HediffDef hungerDef = DefDatabase<HediffDef>.GetNamedSilentFail(HungerDefName);
            if (hungerDef != null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(hungerDef, pawn));
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已添加恢复期饥饿速度效果：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }
        }

        private static void RemoveRecoveryHunger(Pawn pawn)
        {
            Hediff hunger = GetHediff(pawn, HungerDefName);
            if (hunger != null && pawn != null && pawn.health != null)
            {
                pawn.health.RemoveHediff(hunger);
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("已移除恢复期饥饿速度效果：" + IcecreamTailDebug.PawnInfo(pawn));
                }
            }
        }

        public static void ConsumeTail(Pawn eater, Pawn target)
        {
            if (!IsReadyTailPawn(target) || eater == null || eater == target)
            {
                return;
            }

            IcecreamTailUtility.RemovePlaceholder(target);
            StartRecovery(target);
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("冰淇淋尾巴已被舔食：食用者=" + IcecreamTailDebug.PawnInfo(eater) + "，尾巴主人=" + IcecreamTailDebug.PawnInfo(target));
            }
            if (eater.needs != null && eater.needs.food != null)
            {
                eater.needs.food.CurLevel += 0.9f;
            }

            if (IcecreamTailMod.Settings.EnableMoodEffects)
            {
                GainOrRefreshMood(eater, EaterMoodThoughtDefName);
                GainOrRefreshMood(target, OwnerMoodThoughtDefName);
            }

            if (IcecreamTailMod.Settings.EnableRelationshipEffects)
            {
                GainSocialMemory(eater, EaterSocialThoughtDefName, target);
                GainSocialMemory(target, OwnerSocialThoughtDefName, eater);
            }

            if (IcecreamTailMod.Settings.EnableTailBuff)
            {
                RefreshTailBuff(eater);
            }
        }

        private static void GainOrRefreshMood(Pawn pawn, string defName)
        {
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
            if (thoughtDef == null || pawn == null || pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null || pawn.needs.mood.thoughts.memories == null)
            {
                return;
            }

            Thought_Memory memory = pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(thoughtDef);
            if (memory == null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }
            else
            {
                memory.age = 0;
            }
        }

        private static void GainSocialMemory(Pawn pawn, string defName, Pawn otherPawn)
        {
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
            if (thoughtDef != null && pawn != null && otherPawn != null && pawn.needs != null && pawn.needs.mood != null && pawn.needs.mood.thoughts != null && pawn.needs.mood.thoughts.memories != null)
            {
                Thought_Memory existing = pawn.needs.mood.thoughts.memories.Memories.FirstOrDefault(memory => memory.def == thoughtDef && memory.otherPawn == otherPawn);
                if (existing == null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, otherPawn);
                }
                else
                {
                    existing.age = 0;
                }
            }
        }

        private static void RefreshTailBuff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            Hediff existing = GetHediff(pawn, BuffDefName);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }

            HediffDef buffDef = DefDatabase<HediffDef>.GetNamedSilentFail(BuffDefName);
            if (buffDef != null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(buffDef, pawn));
            }
        }

        public static bool CanLickTail(Pawn eater, Pawn target, bool requireHungry, int radius, out string reason)
        {
            // 需要额外制作其他可能的种族扩展的兼容性
            reason = null;
            if (!IcecreamTailMod.Enabled)
            {
                reason = "功能已关闭";
                return false;
            }

            if (eater == null || target == null)
            {
                reason = "目标无效";
                return false;
            }

            if (eater == target)
            {
                reason = "不能舔自己的尾巴";
                return false;
            }

            if (!eater.RaceProps.Humanlike)
            {
                reason = "只有人形角色会舔尾巴";
                return false;
            }

            if (eater.Dead || eater.Downed || eater.InMentalState)
            {
                reason = "当前无法行动";
                return false;
            }

            if (!IsReadyTailPawn(target))
            {
                reason = "没有可舔的冰淇淋尾巴";
                return false;
            }

            if (!eater.Spawned || !target.Spawned || eater.Map != target.Map)
            {
                reason = "不在同一张地图";
                return false;
            }

            if (eater.HostileTo(target) || target.HostileTo(eater))
            {
                reason = "关系不太友好";
                return false;
            }

            if (requireHungry && (eater.needs == null || eater.needs.food == null || eater.needs.food.CurCategory < HungerCategory.Hungry))
            {
                reason = "还不饿";
                return false;
            }

            if (radius > 0 && eater.Position.DistanceToSquared(target.Position) > radius * radius)
            {
                reason = "距离太远";
                return false;
            }

            if (!eater.CanReserveAndReach(target, PathEndMode.Touch, Danger.None, 1, -1, null, false))
            {
                reason = "无法到达或尾巴正被占用";
                return false;
            }

            return true;
        }

        public static void StartLicking(Pawn eater, Pawn target, bool playerForced)
        {
            string reason;
            if (!CanLickTail(eater, target, false, 0, out reason))
            {
                return;
            }

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(LickJobDefName);
            if (jobDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing LickIcecreamTail JobDef.");
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, target);
            job.playerForced = playerForced;
            if (playerForced)
            {
                eater.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
            }
            else
            {
                eater.jobs.StartJob(job, JobCondition.InterruptOptional, null, false, false, null, null, false, false, null, false, false, false);
            }
        }

        public static void HoldTailOwner(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null || pawn.Dead || pawn.Downed)
            {
                return;
            }

            JobDef waitJobDef = DefDatabase<JobDef>.GetNamedSilentFail("LickedIcecreamTail");
            if (waitJobDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing LickedIcecreamTail JobDef.");
                return;
            }

            Job waitJob = JobMaker.MakeJob(waitJobDef);
            waitJob.expiryInterval = LickDurationTicks;
            waitJob.playerForced = true;
            pawn.jobs.StartJob(waitJob, JobCondition.InterruptForced, null, false, false, null, null, false, false, null, false, false, false);
        }

        public static bool IsAutomaticEater(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || !pawn.RaceProps.Humanlike || pawn.Downed || pawn.InMentalState || pawn.Drafted || pawn.needs == null || pawn.needs.food == null || pawn.needs.food.CurCategory < HungerCategory.Hungry)
            {
                return false;
            }

            if (IcecreamTailMod.Settings.SearchAllFriendly)
            {
                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    return false;
                }
            }
            else if (pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }

            Job currentJob = pawn.CurJob;
            if (currentJob != null && currentJob.playerForced)
            {
                return false;
            }

            return currentJob == null || currentJob.def == JobDefOf.Wait || currentJob.def == JobDefOf.Wait_Wander || currentJob.def == JobDefOf.GotoWander;
        }

        internal static IEnumerable<Pawn> AllKnownPawns()
        {
            HashSet<Pawn> pawns = new HashSet<Pawn>();
            if (Current.Game == null)
            {
                return pawns;
            }

            foreach (Map map in Find.Maps)
            {
                pawns.AddRange(map.mapPawns.AllPawns);
            }

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
            {
                pawns.Add(pawn);
            }

            return pawns;
        }

        public static void RemoveAllTailStates()
        {
            if (IcecreamTailDebug.TailEnabled)
            {
                IcecreamTailDebug.Tail("总开关关闭，正在清理全部尾巴状态。");
            }

            foreach (Pawn pawn in AllKnownPawns())
            {
                IcecreamTailUtility.RemovePlaceholder(pawn);
                RemoveRecovery(pawn);
                Hediff buff = GetHediff(pawn, BuffDefName);
                if (buff != null && pawn.health != null)
                {
                    pawn.health.RemoveHediff(buff);
                }
            }

            TailCombatUtility.RemoveAllSlowEffects();
        }

        public static void RemoveAllTailBuffs()
        {
            foreach (Pawn pawn in AllKnownPawns())
            {
                Hediff buff = GetHediff(pawn, BuffDefName);
                if (buff != null && pawn.health != null)
                {
                    pawn.health.RemoveHediff(buff);
                }
            }
        }

        public static void RefreshAllRecoveryHungerEffects()
        {
            foreach (Pawn pawn in AllKnownPawns())
            {
                SyncRecoveryHunger(pawn);
            }
        }
    }

    public static class FacialAnimationCompatibility
    {
        private const string ControllerCompTypeName = "FacialAnimation.FacialAnimationControllerComp";
        private const string IngestJobDefName = "Ingest";
        private const string LovinJobDefName = "Lovin";
        private static Type controllerCompType;
        private static FieldInfo animationDictField;
        private static FieldInfo animationDefField;
        private static FieldInfo targetJobsField;
        private static FieldInfo forcedTemporaryAnimationListField;
        private static MethodInfo playTemporaryAnimationInnerMethod;
        private static MethodInfo resetAnimationMethod;
        private static PropertyInfo finishTickProperty;
        private static bool initialized;

        public static bool TryStartLickAnimation(Pawn pawn, List<string> animationDefNames, List<int> animationFinishTicks)
        {
            return TryStartJobAnimation(pawn, IngestJobDefName, animationDefNames, animationFinishTicks);
        }

        public static bool TryStartLickedTailAnimation(Pawn pawn, List<string> animationDefNames, List<int> animationFinishTicks)
        {
            return TryStartJobAnimation(pawn, LovinJobDefName, animationDefNames, animationFinishTicks);
        }

        public static void RefreshFinishedAnimations(Pawn pawn, List<string> animationDefNames, List<int> animationFinishTicks)
        {
            if (pawn == null || animationDefNames == null || animationFinishTicks == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            for (int index = 0; index < animationDefNames.Count && index < animationFinishTicks.Count; index++)
            {
                if (tick < animationFinishTicks[index])
                {
                    continue;
                }

                int finishTick;
                if (TryResetAnimation(pawn, animationDefNames[index], out finishTick) || TryPlayAnimation(pawn, animationDefNames[index], out finishTick))
                {
                    animationFinishTicks[index] = finishTick;
                }
            }
        }

        private static bool TryPlayAnimation(Pawn pawn, string animationDefName, out int finishTick)
        {
            finishTick = 0;
            if (pawn == null || string.IsNullOrEmpty(animationDefName) || !Initialize())
            {
                return false;
            }

            try
            {
                bool accepted = (bool)playTemporaryAnimationInnerMethod.Invoke(null, new object[] { pawn, Find.TickManager.TicksGame, animationDefName });
                return accepted && TryGetTemporaryFinishTick(pawn, animationDefName, out finishTick);
            }
            catch (Exception exception)
            {
                if (IcecreamTailDebug.FacialEnabled)
                {
                    IcecreamTailDebug.FacialError("动画播放异常：" + IcecreamTailDebug.PawnInfo(pawn) + "，" + exception.GetType().Name + "：" + exception.Message + "。控制器=" + (controllerCompType != null) + "，字典=" + (animationDictField != null) + "，动画 Def=" + (animationDefField != null) + "，临时播放接口=" + (playTemporaryAnimationInnerMethod != null) + "。");
                }

                return false;
            }
        }

        public static void RemoveAnimations(Pawn pawn, IEnumerable<string> animationDefNames)
        {
            if (pawn == null || animationDefNames == null || !Initialize())
            {
                return;
            }

            try
            {
                HashSet<string> defNames = new HashSet<string>(animationDefNames);
                foreach (ThingComp comp in pawn.AllComps)
                {
                    if (!controllerCompType.IsInstanceOfType(comp))
                    {
                        continue;
                    }

                    IList animations = forcedTemporaryAnimationListField.GetValue(comp) as IList;
                    if (animations == null)
                    {
                        return;
                    }

                    for (int index = animations.Count - 1; index >= 0; index--)
                    {
                        object definition = animationDefField.GetValue(animations[index]);
                        if (definition != null && defNames.Contains(GetDefName(definition)))
                        {
                            animations.RemoveAt(index);
                        }
                    }

                    break;
                }

                if (IcecreamTailDebug.FacialEnabled)
                {
                    IcecreamTailDebug.Facial("已取消本次舔食动画：" + IcecreamTailDebug.PawnInfo(pawn) + "。");
                }
            }
            catch (Exception exception)
            {
                if (IcecreamTailDebug.FacialEnabled)
                {
                    IcecreamTailDebug.FacialError("动画取消异常：" + IcecreamTailDebug.PawnInfo(pawn) + "，" + exception.GetType().Name + "：" + exception.Message + "。");
                }
            }
        }

        private static bool Initialize()
        {
            if (initialized)
            {
                return true;
            }

            controllerCompType = GenTypes.GetTypeInAnyAssembly(ControllerCompTypeName);
            animationDictField = controllerCompType == null ? null : controllerCompType.GetField("animationDict", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type faceAnimationType = controllerCompType == null ? null : controllerCompType.Assembly.GetType("FacialAnimation.FaceAnimation");
            Type faceAnimationDefType = controllerCompType == null ? null : controllerCompType.Assembly.GetType("FacialAnimation.FaceAnimationDef");
            animationDefField = faceAnimationType == null ? null : faceAnimationType.GetField("animationDef", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            targetJobsField = faceAnimationDefType == null ? null : faceAnimationDefType.GetField("targetJobs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            forcedTemporaryAnimationListField = controllerCompType == null ? null : controllerCompType.GetField("forcedTemporaryAnimationList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            playTemporaryAnimationInnerMethod = controllerCompType == null ? null : controllerCompType.GetMethod("PlayTemporaryAnimationInner", BindingFlags.Static | BindingFlags.NonPublic);
            resetAnimationMethod = faceAnimationType == null ? null : faceAnimationType.GetMethod("Reset", BindingFlags.Instance | BindingFlags.Public);
            finishTickProperty = faceAnimationType == null ? null : faceAnimationType.GetProperty("finishTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (controllerCompType == null || animationDictField == null || animationDefField == null || targetJobsField == null || forcedTemporaryAnimationListField == null || playTemporaryAnimationInnerMethod == null || resetAnimationMethod == null || finishTickProperty == null)
            {
                LogUnavailableInterface();

                return false;
            }

            initialized = true;
            if (IcecreamTailDebug.FacialEnabled)
            {
                IcecreamTailDebug.Facial("Facial Animation 反射接口已初始化。");
            }

            return true;
        }

        private static bool TryStartJobAnimation(Pawn pawn, string sourceJobDefName, List<string> animationDefNames, List<int> animationFinishTicks)
        {
            if (pawn == null || pawn.def == null || animationDefNames == null || animationFinishTicks == null || !Initialize())
            {
                return false;
            }

            animationDefNames.Clear();
            animationFinishTicks.Clear();

            // 专门为了修这个，写的debug模式
            // 孩子们 我修不好了……
            // 孩子们 我好像修好了
            // 真修好了么
            try
            {
                foreach (ThingComp comp in pawn.AllComps)
                {
                    if (!controllerCompType.IsInstanceOfType(comp))
                    {
                        continue;
                    }

                    IDictionary animationDict = animationDictField.GetValue(comp) as IDictionary;
                    if (animationDict == null || !animationDict.Contains(sourceJobDefName))
                    {
                        if (IcecreamTailDebug.FacialEnabled)
                        {
                            IcecreamTailDebug.FacialWarning("缺少源动画：" + IcecreamTailDebug.PawnInfo(pawn) + "，源 Job=" + sourceJobDefName + "，动画字典=" + (animationDict != null) + "。");
                        }

                        return false;
                    }

                    foreach (object animation in (IEnumerable)animationDict[sourceJobDefName])
                    {
                        object definition = animationDefField.GetValue(animation);
                        IEnumerable targetJobs = definition == null ? null : targetJobsField.GetValue(definition) as IEnumerable;
                        if (definition == null || targetJobs == null || !targetJobs.Cast<object>().Any(jobName => string.Equals(jobName as string, sourceJobDefName, StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        string animationDefName = GetDefName(definition);
                        if (!string.IsNullOrEmpty(animationDefName) && !animationDefNames.Contains(animationDefName))
                        {
                            animationDefNames.Add(animationDefName);
                        }
                    }

                    if (animationDefNames.Count == 0)
                    {
                        if (IcecreamTailDebug.FacialEnabled)
                        {
                            IcecreamTailDebug.FacialWarning("源 Job 没有可播放动画：" + IcecreamTailDebug.PawnInfo(pawn) + "，源 Job=" + sourceJobDefName + "。");
                        }

                        return false;
                    }

                    RemoveAnimations(pawn, animationDefNames);
                    foreach (string animationDefName in animationDefNames)
                    {
                        int finishTick;
                        if (!TryPlayAnimation(pawn, animationDefName, out finishTick))
                        {
                            RemoveAnimations(pawn, animationDefNames);
                            animationDefNames.Clear();
                            animationFinishTicks.Clear();
                            return false;
                        }

                        animationFinishTicks.Add(finishTick);
                    }

                    if (IcecreamTailDebug.FacialEnabled)
                    {
                        IcecreamTailDebug.Facial("已开始原生 Job 动画：" + IcecreamTailDebug.PawnInfo(pawn) + "，源 Job=" + sourceJobDefName + "，Def=" + string.Join("、", animationDefNames.ToArray()) + "。");
                    }

                    return true;
                }
            }
            catch (Exception exception)
            {
                if (IcecreamTailDebug.FacialEnabled)
                {
                    IcecreamTailDebug.FacialError("动画选择异常：" + IcecreamTailDebug.PawnInfo(pawn) + "，" + exception.GetType().Name + "：" + exception.Message + "。");
                }

                return false;
            }

            if (IcecreamTailDebug.FacialEnabled)
            {
                IcecreamTailDebug.FacialWarning("未找到 Facial Animation 控制器：" + IcecreamTailDebug.PawnInfo(pawn) + "。");
            }

            return false;
        }

        private static bool TryResetAnimation(Pawn pawn, string animationDefName, out int finishTick)
        {
            finishTick = 0;
            object animation = FindTemporaryAnimation(pawn, animationDefName);
            if (animation == null)
            {
                return false;
            }

            try
            {
                resetAnimationMethod.Invoke(animation, new object[] { Find.TickManager.TicksGame });
                finishTick = (int)finishTickProperty.GetValue(animation, null);
                return true;
            }
            catch (Exception exception)
            {
                if (IcecreamTailDebug.FacialEnabled)
                {
                    IcecreamTailDebug.FacialError("动画原位重置异常：" + IcecreamTailDebug.PawnInfo(pawn) + "，" + exception.GetType().Name + "：" + exception.Message + "。");
                }

                return false;
            }
        }

        private static bool TryGetTemporaryFinishTick(Pawn pawn, string animationDefName, out int finishTick)
        {
            finishTick = 0;
            object animation = FindTemporaryAnimation(pawn, animationDefName);
            if (animation == null)
            {
                return false;
            }

            finishTick = (int)finishTickProperty.GetValue(animation, null);
            return true;
        }

        private static object FindTemporaryAnimation(Pawn pawn, string animationDefName)
        {
            foreach (ThingComp comp in pawn.AllComps)
            {
                if (!controllerCompType.IsInstanceOfType(comp))
                {
                    continue;
                }

                IList animations = forcedTemporaryAnimationListField.GetValue(comp) as IList;
                if (animations == null)
                {
                    return null;
                }

                for (int index = animations.Count - 1; index >= 0; index--)
                {
                    object animation = animations[index];
                    object definition = animationDefField.GetValue(animation);
                    if (definition != null && string.Equals(GetDefName(definition), animationDefName, StringComparison.Ordinal))
                    {
                        return animation;
                    }
                }

                return null;
            }

            return null;
        }

        private static string GetDefName(object definition)
        {
            for (Type type = definition.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField("defName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field.GetValue(definition) as string;
                }
            }

            return null;
        }

        private static void LogUnavailableInterface()
        {
            if (IcecreamTailDebug.FacialEnabled)
            {
                IcecreamTailDebug.FacialError("Facial Animation 接口不可用：控制器=" + (controllerCompType != null) + "，动画字典=" + (animationDictField != null) + "，动画 Def=" + (animationDefField != null) + "，临时播放接口=" + (playTemporaryAnimationInnerMethod != null) + "，原位重置接口=" + (resetAnimationMethod != null) + "。");
            }
        }
    }

    public sealed class MapComponent_IcecreamTailAutoLick : MapComponent
    {
        public MapComponent_IcecreamTailAutoLick(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!IcecreamTailMod.Enabled || Find.TickManager.TicksGame % 5000 != 0)
            {
                return;
            }

            List<Pawn> tails = map.mapPawns.AllPawns.Where(TailEatingUtility.IsReadyTailPawn).ToList();
            if (tails.Count == 0)
            {
                return;
            }

            List<Pawn> eaters = map.mapPawns.AllPawns.Where(TailEatingUtility.IsAutomaticEater).ToList();
            if (eaters.Count == 0)
            {
                return;
            }

            int radius = IcecreamTailMod.Settings.SearchRadius > 150 ? 0 : IcecreamTailMod.Settings.SearchRadius;
            Pawn eater = eaters.RandomElement();
            Pawn target = tails.Where(tail => CanAutoLick(eater, tail, radius)).OrderBy(tail => eater.Position.DistanceToSquared(tail.Position)).FirstOrDefault();
            if (target != null)
            {
                TailEatingUtility.StartLicking(eater, target, false);
            }
        }

        private static bool CanAutoLick(Pawn eater, Pawn target, int radius)
        {
            string reason;
            return TailEatingUtility.CanLickTail(eater, target, true, radius, out reason);
        }
    }

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
        //话说……TargetA，会不会过于“一般化”了？会不会导致可能的错误引用？应该问题不大？我要不加到逻辑图里头？
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
            });
            this.FailOn(() => !TailEatingUtility.IsReadyTailPawn(TargetPawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
            Toil holdOwner = new Toil();
            holdOwner.initAction = delegate { TailEatingUtility.HoldTailOwner(TargetPawn); };
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
