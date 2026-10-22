using System.Collections.Generic;
using System.Linq;
using LudeonTK;
#if !RIMWORLD_17
using Nivarian.Helper;
#endif
using RimWorld;
using UnityEngine;
using Verse;

// 集百家之智慧！我这一行debug！蕴含了我10分钟C#开发的功力！你们挡得住么！
// 被迫写的神秘debug，py养成的坏习惯，如果全开了的话，大概会各种刷屏吧？但是我喜欢！
namespace NivarianIcecreamTail
{
    internal static class IcecreamTailDebug
    {
        public static bool TailEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogTailStateDebug; }
        }

        public static bool FacialEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogFacialAnimationDebug; }
        }

        public static bool AutoLickEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogAutoLickDebug; }
        }

        public static bool EasterEggEnabled
        {
            get { return IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.LogEasterEggDebug; }
        }

        public static void Tail(string message)
        {
            if (TailEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][Tail] " + message);
            }
        }

        public static void Facial(string message)
        {
            if (FacialEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][Facial] " + message);
            }
        }

        public static void FacialWarning(string message)
        {
            if (FacialEnabled)
            {
                Log.Warning("[Nivarian Icecream Tail][Facial] " + message);
            }
        }

        public static void FacialError(string message)
        {
            if (FacialEnabled)
            {
                Log.Error("[Nivarian Icecream Tail][Facial] " + message);
            }
        }

        public static void AutoLick(string message)
        {
            if (AutoLickEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][AutoLick] " + message);
            }
        }

        public static void EasterEgg(string message)
        {
            if (EasterEggEnabled)
            {
                Log.Message("[Nivarian Icecream Tail][EasterEgg] " + message);
            }
        }

        public static string PawnInfo(Pawn pawn)
        {
            if (pawn == null)
            {
                return "<null>";
            }

            return pawn.LabelShortCap + " (" + (pawn.def == null ? "<no def>" : pawn.def.defName) + ")";
        }
    }

    public static class IcecreamTailDebugActions
    {
        [DebugActionYielder]
        public static IEnumerable<DebugActionNode> DebugActions()
        {
            DebugActionNode action = new DebugActionNode("IcecreamTail_DebugToggle".Translate().ToString(), DebugActionType.ToolMapForPawns, null, ToggleIcecreamTail);
            action.category = "Nivarian Icecream Tail";
            action.visibilityGetter = () => IcecreamTailMod.Enabled && Find.CurrentMap != null;
            yield return action;
        }

        private static void ToggleIcecreamTail(Pawn pawn)
        {
            if (!IcecreamTailMod.Enabled)
            {
                Messages.Message("IcecreamTail_DebugDisabled".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            BodyPartRecord tailPart;
            if (!IcecreamTailUtility.IsNivarian(pawn))
            {
                Messages.Message("IcecreamTail_DebugNotNivarian".Translate().ToString(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (IcecreamTailUtility.GetTailKind(pawn, out tailPart) == TailKind.None)
            {
                Messages.Message("IcecreamTail_DebugNoTail".Translate().ToString(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (IcecreamTailUtility.GetPlaceholder(pawn) == null)
            {
                TailEatingUtility.RemoveRecovery(pawn);
                IcecreamTailUtility.TryAddPlaceholder(pawn);
                Messages.Message("IcecreamTail_DebugAdded".Translate().ToString(), pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                IcecreamTailUtility.RemovePlaceholder(pawn);
                Messages.Message("IcecreamTail_DebugRemoved".Translate().ToString(), pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
// 16，10，7，13 “我将会去买一箩筐的点心，麻利的送来医院”
