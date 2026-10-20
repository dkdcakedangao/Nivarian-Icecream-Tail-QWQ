using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

// 绘制、渲染
namespace NivarianIcecreamTail
{
    internal static class PawnAnimationRendering
    {
        private static readonly Dictionary<Pawn, PawnAnimationActor> Bound = new Dictionary<Pawn, PawnAnimationActor>();
        internal static bool Any { get { return Bound.Count != 0; } }
        internal static bool IsBound(Pawn pawn) { return Bound.Count != 0 && pawn != null && Bound.ContainsKey(pawn); }

        internal static void Bind(PawnAnimationSession session)
        {
            foreach (var actor in session.Actors)
                if (Bound.ContainsKey(actor.Pawn)) throw new InvalidOperationException("Pawn already belongs to a character animation");
            foreach (var actor in session.Actors)
            {
                Bound.Add(actor.Pawn, actor);
                MeleeIdleDrawSuppression.Bind(actor);
                actor.Pawn.Drawer.renderer.renderTree.SetDirty();
            }
        }

        internal static void Unbind(PawnAnimationSession session)
        {
            if (session.Actors == null) return;
            Exception failure = null;
            foreach (var actor in session.Actors)
            {
                if (actor == null) continue;
                PawnAnimationActor current;
                if (Bound.TryGetValue(actor.Pawn, out current) && ReferenceEquals(actor, current))
                {
                    Bound.Remove(actor.Pawn);
                    try { MeleeIdleDrawSuppression.Unbind(actor); }
                    catch (Exception error) { if (failure == null) failure = error; }
                }
            }
            if (failure != null) throw new InvalidOperationException("Character binding cleanup failed", failure);
        }

        internal static bool TryGet(Pawn pawn, out PawnAnimationActor actor)
        {
            actor = null;
            return Bound.Count != 0 && pawn != null && Bound.TryGetValue(pawn, out actor);
        }

        internal static bool WorldDraw(PawnDrawParms parms)
        {
            return !parms.Portrait && !parms.Cache && !parms.Statue;
        }

        internal static void RequestRecache(PawnAnimationActor actor, PawnRenderNode root)
        {
            if (!actor.RecachePending || root == null) return;
            root.requestRecache = true;
            actor.RecachePending = false;
        }

        internal static bool PrepareNode(PawnRenderNode node, ref PawnDrawParms parms)
        {
            PawnAnimationActor actor;
            if (!WorldDraw(parms) || !TryGet(parms.pawn, out actor)) return true;
            PawnAnimationPose pose = actor.IsHead(node) && actor.SeparateHead ? actor.Head : actor.Body;
            parms.facing = pose.facing;
            if (node.parent == null || (node.Props.tagDef != null && node.Props.tagDef.defName == "Head")) parms.tint *= pose.tint;
            return actor.Body.visible >= 0.5f && pose.visible >= 0.5f;
        }

        internal static Matrix4x4 PawnMatrix(PawnAnimationPose pose, Vector3 root, Vector3 lifeStageOffset)
        {
            return Matrix4x4.TRS(root + pose.position + lifeStageOffset, pose.rotation, pose.scale);
        }

        internal static void SetMatrix(PawnRenderNode node, ref PawnDrawParms parms)
        {
            PawnAnimationActor actor;
            if (!WorldDraw(parms) || !TryGet(parms.pawn, out actor)) return;
            bool head = actor.IsHead(node) && actor.SeparateHead;
            parms.matrix = head ? actor.HeadMatrix : actor.BodyMatrix;
            parms.facing = head ? actor.Head.facing : actor.Body.facing;
        }

        internal static Mesh MeshFor(PawnRenderNode node, PawnDrawParms parms, Mesh original)
        {
            PawnAnimationActor actor;
            if (!WorldDraw(parms) || !TryGet(parms.pawn, out actor)) return original;
            bool head = actor.IsHead(node) && actor.SeparateHead;
            // PawnBody.Direction 和 FlipX/Y.
            return PawnAnimationGraphics.GetMesh(original, PawnAnimationGraphics.MeshMask(head ? actor.Head : actor.Body, false) & 4);
        }

        internal static void DrawHands(Pawn pawn)
        {
            PawnAnimationActor actor;
            // 只为人形绘制额外双手，非人形仍保留身体与头部动画。
            if (!TryGet(pawn, out actor) || !pawn.RaceProps.Humanlike || actor.Body.visible < 0.5f) return;
            for (int i = 0; i < actor.HandPoses.Length; i++)
            {
                PawnAnimationPose pose = actor.HandPoses[i];
                if (pose.visible < 0.5f) continue;
                actor.HandProperties.SetColor(ShaderPropertyIDs.Color, PawnAnimationGraphics.HandColor(actor.SkinColor, pose));
                int mask = (pose.flipX >= 0.5f ? 1 : 0) | (pose.flipY >= 0.5f ? 2 : 0) | (PawnAnimationGraphics.Reversed(actor.HandMatrices[i]) ? 4 : 0);
                Graphics.DrawMesh(PawnAnimationGraphics.GetMesh(MeshPool.plane10, mask), actor.HandMatrices[i], actor.HandMaterial,
                    0, null, 0, actor.HandProperties);
            }
        }

    }

    [HarmonyPatch(typeof(PawnRenderNode), "GetMesh")]
    internal static class Patch_CharacterAnimation_NodeMesh
    {
        private static void Postfix(PawnRenderNode __instance, PawnDrawParms parms, ref Mesh __result)
        {
            if (PawnAnimationRendering.Any) __result = PawnAnimationRendering.MeshFor(__instance, parms, __result);
        }
    }

    [HarmonyPatch(typeof(PawnRenderNode), "AppendRequests")]
    internal static class Patch_CharacterAnimation_NodeFacing
    {
        private static bool Prefix(PawnRenderNode __instance, ref PawnDrawParms parms)
        {
            return !PawnAnimationRendering.Any || PawnAnimationRendering.PrepareNode(__instance, ref parms);
        }
    }

    [HarmonyPatch(typeof(PawnRenderTree), "TryGetMatrix")]
    internal static class Patch_CharacterAnimation_NodeMatrix
    {
        private static void Prefix(PawnRenderNode node, ref PawnDrawParms parms)
        {
            if (PawnAnimationRendering.Any) PawnAnimationRendering.SetMatrix(node, ref parms);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_CharacterAnimation_DisableCache
    {
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            PawnAnimationActor actor;
            if (PawnAnimationRendering.TryGet(___pawn, out actor))
            {
                disableCache = true;
                PawnAnimationRendering.RequestRecache(actor, ___pawn.Drawer.renderer.renderTree.rootNode);
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    internal static class Patch_CharacterAnimation_Hands
    {
        private static void Postfix(Pawn ___pawn)
        {
            if (PawnAnimationRendering.Any) PawnAnimationRendering.DrawHands(___pawn);
        }
    }
}
