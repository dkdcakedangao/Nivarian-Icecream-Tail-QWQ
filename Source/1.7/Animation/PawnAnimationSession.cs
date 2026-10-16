using System;
using System.IO;
using LumiParticle.Core;
using LumiParticle.Core.Lifetime;
using LumiParticle.Data;
using LumiParticle.Effects;
using LumiParticle.Items;
using UnityEngine;
using Verse;
using Verse.Sound;

// 总管理，确定播放位置、人物，速度、音效什么的
namespace NivarianIcecreamTail
{
    internal struct PawnAnimationPose
    {
        internal Vector3 position, scale;
        internal Quaternion rotation;
        internal Matrix4x4 matrix;
        internal float flipX, flipY, visible;
        internal Color tint;
        internal Rot4 facing;
    }

    internal sealed class PawnAnimationActor
    {
        internal readonly Pawn Pawn;
        internal readonly int BodyIndex, HeadIndex;
        internal readonly int[] Hands;
        internal PawnAnimationPose Body, Head;
        internal bool RecachePending = true;
        internal object MeleeIdleRenderer;
        internal readonly PawnAnimationPose[] HandPoses;
        internal readonly Matrix4x4[] HandMatrices;
        internal Matrix4x4 BodyMatrix, HeadMatrix;
        internal bool SeparateHead;
        internal readonly Vector3 RootPosition, LifeStageOffset;
        internal readonly Color SkinColor;
        internal readonly Material HandMaterial;
        internal readonly MaterialPropertyBlock HandProperties = new MaterialPropertyBlock();

        internal PawnAnimationActor(Pawn pawn, CachedPawnAnimation clip, int role, PawnAnimationSession session)
        {
            Pawn = pawn; BodyIndex = clip.BodyIndices[role]; HeadIndex = clip.HeadIndices[role]; Hands = clip.HandIndices[role];
            RootPosition = session.RootPosition;
            LifeStageOffset = pawn.ageTracker.CurLifeStage.bodyDrawOffset;
            HandPoses = new PawnAnimationPose[Hands.Length];
            HandMatrices = new Matrix4x4[Hands.Length];
            SkinColor = pawn.story == null ? Color.white : pawn.story.SkinColor;
            HandMaterial = PawnAnimationGraphics.HandMaterial;
            Capture(session);
        }

        internal void Capture(PawnAnimationSession session)
        {
            PawnAnimationPose body = session.ReadPose(BodyIndex);
            PawnAnimationPose head = session.ReadPose(HeadIndex);
            bool separateHead = (body.position - head.position).sqrMagnitude > 0.0001f || body.facing != head.facing;
            RecachePending |= DrawRequestChanged(Body, body) || DrawRequestChanged(SeparateHead ? Head : Body, separateHead ? head : body);
            Body = body; Head = head;
            SeparateHead = separateHead;
            BodyMatrix = PawnAnimationRendering.PawnMatrix(body, RootPosition, LifeStageOffset);
            HeadMatrix = SeparateHead ? PawnAnimationRendering.PawnMatrix(head, RootPosition, LifeStageOffset) : BodyMatrix;
            for (int i = 0; i < Hands.Length; i++)
            {
                HandPoses[i] = session.ReadPose(Hands[i]);
                HandMatrices[i] = Matrix4x4.Translate(RootPosition) * HandPoses[i].matrix;
            }
        }

        internal static bool DrawRequestChanged(PawnAnimationPose previous, PawnAnimationPose current)
        {
            return previous.facing != current.facing || (previous.visible >= 0.5f) != (current.visible >= 0.5f) ||
                previous.tint != current.tint ||
                (previous.scale.x * previous.scale.y * previous.scale.z < 0f) != (current.scale.x * current.scale.y * current.scale.z < 0f);
        }

        internal bool IsHead(Verse.PawnRenderNode node)
        {
            for (var parent = node; parent != null; parent = parent.parent)
                if (parent.Props.tagDef != null && parent.Props.tagDef.defName == "Head") return true;
            return false;
        }

        internal void Restore()
        {
            if (Pawn != null && Pawn.Spawned && !Pawn.Dead) Pawn.Drawer.renderer.renderTree.SetDirty();
        }
    }

    internal sealed class PawnAnimationSession : IDisposable
    {
        private const string PrefabName = "IcecreamTailActorRig";
        private LumiVisualHandle<LumiAnimatorItem> handle;
        private NativeAnimationRig rig;
        private readonly SoundDef[] sounds;
        internal readonly CachedPawnAnimation Clip;
        internal readonly PawnAnimationActor[] Actors;
        internal readonly bool Mirror;
        internal readonly Vector3 RootPosition;
        internal float Elapsed { get; private set; }
        internal int SoundCursor { get; private set; }
        internal bool Finished { get { return Elapsed >= Clip.Clip.length; } }
        internal bool Alive { get { return handle != null && handle.IsAlive; } }
        private bool disposed;

        internal static PawnAnimationSession Start(PawnAnimationDef def, Pawn caster, Pawn target, float elapsed, int cursor, bool mirror, Vector3 root)
        {
            CachedPawnAnimation clip = CachedPawnAnimation.Get(def);
            if (clip == null) return null;
            try { return new PawnAnimationSession(clip, caster, target, elapsed, cursor, mirror, root); }
            catch (Exception error)
            {
                Log.Error("[Nivarian Icecream Tail] Character playback could not start: " + error);
                return null;
            }
        }

        private PawnAnimationSession(CachedPawnAnimation clip, Pawn caster, Pawn target, float elapsed, int cursor, bool mirror, Vector3 root)
        {
            Clip = clip; Mirror = mirror; RootPosition = root;
            if (!float.IsFinite(elapsed) || elapsed < 0 || elapsed > clip.Clip.length || cursor < 0 || cursor > clip.Sounds.Length ||
                !float.IsFinite(root.x) || !float.IsFinite(root.y) || !float.IsFinite(root.z))
                throw new InvalidDataException("Invalid animation resume position");
            sounds = new SoundDef[clip.Sounds.Length];
            Actors = new PawnAnimationActor[2];
            try
            {
                // 直接涅入口！
                handle = LumiParticleEffects.SpawnAnimator(caster.Map, new LumiAnimatorData {
                    prefabName = PrefabName, position = Vector3.zero, scale = 1f, rotation = Quaternion.identity
                }, LumiVisualLifetime.OneShot(clip.Clip.length - elapsed + 5f, LumiGameClock.Instance), null);
                if (handle == null || !handle.IsAlive || handle.Item.Animator == null)
                    throw new InvalidOperationException("Nivarian's animator entry did not create the character rig");
                handle.Item.LifetimeOwner = caster;
                rig = new NativeAnimationRig(clip, handle.Item.GameObject, mirror);
                Actors[0] = new PawnAnimationActor(caster, clip, 0, this);
                Actors[1] = new PawnAnimationActor(target, clip, 1, this);
                Elapsed = elapsed; SoundCursor = cursor;
                // 除错
                if (elapsed > 0f)
                    while (SoundCursor < clip.Sounds.Length && clip.Sounds[SoundCursor].Time <= elapsed + 0.00001f) SoundCursor++;
                if (elapsed != 0f)
                {
                    rig.Seek(elapsed, clip.Clip.length);
                }
                for (int i = 0; i < sounds.Length; i++) sounds[i] = DefDatabase<SoundDef>.GetNamedSilentFail(clip.Sounds[i].Data.Split(';')[1]);
                Capture();
                PawnAnimationRendering.Bind(this);
            }
            catch { Dispose(); throw; }
        }

        internal void Advance()
        {
            if (!Alive || Finished) return;
            float step = Math.Min(1f / 60f, Clip.Clip.length - Elapsed);
            rig.Advance(step);
            Elapsed = Math.Min(Clip.Clip.length, Elapsed + step);
            while (SoundCursor < Clip.Sounds.Length && Clip.Sounds[SoundCursor].Time <= Elapsed + 0.00001f)
            {
                SoundDef sound = sounds[SoundCursor++];
                if (IcecreamTailSkillAudio.Enabled && sound != null)
                    sound.PlayOneShot(new TargetInfo(Actors[0].Pawn.Position, Actors[0].Pawn.Map));
            }
            Capture();
        }

        private void Capture()
        {
            for (int i = 0; i < Actors.Length; i++) if (Actors[i] != null) Actors[i].Capture(this);
        }

        internal PawnAnimationPose ReadPose(int index)
        {
            return rig.ReadPose(index);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            PawnAnimationRendering.Unbind(this);
            try { if (rig != null) rig.Dispose(); }
            catch (Exception error) { Log.Warning("[Nivarian Icecream Tail] Character rig release: " + error.Message); }
            finally
            {
                rig = null;
                try { if (handle != null) handle.Dispose(); }
                catch (Exception error) { Log.Warning("[Nivarian Icecream Tail] Character handle release: " + error.Message); }
                finally
                {
                    handle = null;
                    if (Actors != null) foreach (var actor in Actors) if (actor != null) actor.Restore();
                }
            }
        }
    }
}
