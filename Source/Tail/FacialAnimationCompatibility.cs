using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

// 表情相关的
// 其实本来是想直接做个动画什么的
// 但是还是算了，太重了
namespace NivarianIcecreamTail
{
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
            // 我修好了
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

}
