using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Verse;

namespace NivarianIcecreamTail
{
    internal static class NativeAnimationHost
    {
        private enum StartupState { Uninitialized, Available, Unavailable }
        private static StartupState state;
        private static IPawnAnimationBackend backend;
        internal static string UnavailableReason { get; private set; }

        internal static void Initialize()
        {
            if (state != StartupState.Uninitialized) return;
            // Latch before invoking any optional code. No update loop or retry path.
            state = StartupState.Unavailable;
            try
            {
                foreach (string name in new[] { "RimUnity.Core", "RimUnity", "LumiParticle" })
                    if (FindLoaded(name) == null) throw new InvalidOperationException("The base mod has not loaded " + name);
                var content = IcecreamTailMod.ContentPack;
                if (content == null) throw new InvalidOperationException("Add-on content is not ready");
                string path = Path.Combine(content.RootDir, "1.7", "Animation", "NivarianIcecreamTail.Animation.dll");
                Assembly adapter = Assembly.LoadFrom(path);
                backend = (IPawnAnimationBackend)adapter.GetType("NivarianIcecreamTail.PawnAnimationBackend", true)
                    .GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { content });
                if (backend == null) throw new InvalidOperationException("The animation adapter did not return a backend");
                state = StartupState.Available;
            }
            catch (Exception error) { Disable(error); }
        }

        internal static Assembly FindLoaded(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == name) return assembly;
            return null;
        }

        internal static IPawnAnimationSession Start(PawnAnimationDef def, Pawn caster, Pawn target, float elapsed, int cursor, bool mirror, Vector3 root)
        {
            if (state != StartupState.Available) return null;
            try { return backend.Start(def, caster, target, elapsed, cursor, mirror, root); }
            catch (Exception error) { Disable(error); return null; }
        }

        internal static void Disable(Exception error)
        {
            if (UnavailableReason != null) return;
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            state = StartupState.Unavailable;
            UnavailableReason = error.GetType().Name + ": " + error.Message;
            IPawnAnimationBackend previous = backend;
            backend = null;
            try { IcecreamTailSkill3Runtime.CancelNativeAnimations(); }
            catch (Exception cleanup) { UnavailableReason += "; job cleanup: " + cleanup.Message; }
            finally
            {
                try { if (previous != null) previous.Dispose(); }
                catch (Exception cleanup) { UnavailableReason += "; cleanup: " + cleanup.Message; }
                Log.Warning("[Nivarian Icecream Tail] 内置人物动画本次启动不可用，后续使用简化流程。" + UnavailableReason);
            }
        }
    }
}
