using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

[assembly: InternalsVisibleTo("NivarianIcecreamTail.Animation")]

namespace NivarianIcecreamTail
{
    public sealed class PawnAnimationRole
    {
        public string body;
        public string head;
        public string handsRoot;
    }

    public sealed class PawnAnimationDef : Def
    {
        public string jsonPath;
        public List<PawnAnimationRole> roles = new List<PawnAnimationRole>();
        public List<string> ignoredRoots = new List<string>();
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrEmpty(jsonPath)) yield return "Missing jsonPath";
            if (roles.Count != 2) yield return "The current character binding requires two roles";
            foreach (PawnAnimationRole role in roles)
                if (string.IsNullOrEmpty(role.body) || string.IsNullOrEmpty(role.head)) yield return "Each role requires body and head paths";
        }
    }

    // Boundary contains only game and main-module types; no framework object crosses it.
    public interface IPawnAnimationBackend : IDisposable
    {
        IPawnAnimationSession Start(PawnAnimationDef def, Pawn caster, Pawn target, float elapsed, int cursor, bool mirror, Vector3 root);
    }

    public interface IPawnAnimationSession : IDisposable
    {
        PawnAnimationDef Def { get; }
        float Elapsed { get; }
        int SoundCursor { get; }
        bool Mirror { get; }
        Vector3 RootPosition { get; }
        bool Finished { get; }
        bool Alive { get; }
        void Advance();
    }
}
