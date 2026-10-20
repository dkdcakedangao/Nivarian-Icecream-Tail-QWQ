using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace NivarianIcecreamTail
{
    // Optional host decoration only. Bind once; the settings UI uses cached delegates.
    internal static class NivarianHostBridge
    {
        private static bool initialized;
        private static Func<Rect, string, TextAnchor, bool, bool> button;
        private static Action<Rect, bool> background;
        internal static MethodInfo MouthPosition { get; private set; }

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                Type helper = NativeAnimationHost.FindLoaded("Nivarian_Race")?.GetType("Nivarian.Helper.NivarianVisualHelper");
                if (helper == null) return;
                button = Bind<Func<Rect, string, TextAnchor, bool, bool>>(helper, "DrawNivarianTechBtn", typeof(Rect), typeof(string), typeof(TextAnchor), typeof(bool));
                background = Bind<Action<Rect, bool>>(helper, "DrawNivarianTechBtnBackground", typeof(Rect), typeof(bool));
                MouthPosition = helper.GetMethod("GetMouthPos", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Pawn) }, null);
                if (MouthPosition != null && MouthPosition.ReturnType != typeof(Vector3)) MouthPosition = null;
            }
            catch (Exception error)
            {
                // A missing mouth/helper API must not discard successful UI bindings.
                Log.Warning("[Nivarian Icecream Tail] 本体可选接口未完成绑定，保留已绑定接口：" + error.Message);
            }
        }

        private static T Bind<T>(Type type, string name, params Type[] arguments) where T : Delegate
        {
            try
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, arguments, null);
                return method == null ? null : method.CreateDelegate<T>();
            }
            catch (Exception error)
            {
                Log.Warning("[Nivarian Icecream Tail] 本体可选接口 " + name + " 绑定失败，仅该接口使用原版 UI：" + error.Message);
                return null;
            }
        }

        internal static void UIWithColor(Color color, Action draw)
        {
            Color previous = GUI.color;
            try { GUI.color = color; draw(); }
            finally { GUI.color = previous; }
        }

        internal static void DrawNivarianTechBtnBackground(Rect rect, bool mouseover)
        {
            if (background != null) background(rect, mouseover);
            else Widgets.DrawMenuSection(rect);
        }

        internal static bool DrawNivarianTechBtn(Rect rect, string label, TextAnchor anchor, bool active)
        {
            return button != null ? button(rect, label, anchor, active) : Widgets.ButtonText(rect, label, active: active);
        }
    }
}
