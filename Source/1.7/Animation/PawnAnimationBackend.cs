using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using LumiParticle;
using LumiParticle.Assets;
using UnityEngine;
using Verse;
using AssetBundle = RimUnity.Assets.AssetBundle;

namespace NivarianIcecreamTail
{
    public sealed class PawnAnimationBackend : IPawnAnimationBackend
    {
        private readonly Harmony patches = new Harmony("dkdcakedangao.NivarianIcecreamTailQWQ.Animation");
        private readonly List<AssetBundle> bundles = new List<AssetBundle>();
        private bool disposed;

        public static IPawnAnimationBackend Create(ModContentPack content)
        {
            var backend = new PawnAnimationBackend();
            try
            {
                // Only the host's selected, already-loaded runtime is used.
                LumiStartup.EnsureInitialized();
                backend.RegisterResources(Path.Combine(content.RootDir, "1.7", "Data", "RimUnity"));
                backend.patches.PatchAll(typeof(PawnAnimationBackend).Assembly);
                return backend;
            }
            catch { backend.Dispose(); throw; }
        }

        private void RegisterResources(string path)
        {
            var loaded = AssetBundle.LoadDirectory(path);
            if (loaded == null || loaded.Count == 0) throw new InvalidDataException("Character catalog did not contain a bundle");
            bool found = false;
            foreach (AssetBundle bundle in loaded)
            {
                bundles.Add(bundle);
                LumiAssetLoader.Instance.Bundles.Add(bundle);
                if (bundle.LoadAsset<RimUnity.GameObject>("IcecreamTailActorRig") != null) found = true;
            }
            if (!found) throw new InvalidDataException("Character catalog did not contain IcecreamTailActorRig");
        }

        public IPawnAnimationSession Start(PawnAnimationDef def, Pawn caster, Pawn target, float elapsed, int cursor, bool mirror, Vector3 root)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PawnAnimationBackend));
            return PawnAnimationSession.Start(def, caster, target, elapsed, cursor, mirror, root);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { patches.UnpatchAll(patches.Id); }
            finally
            {
                foreach (AssetBundle bundle in bundles) LumiAssetLoader.Instance.Bundles.Remove(bundle);
                bundles.Clear();
            }
        }
    }
}
