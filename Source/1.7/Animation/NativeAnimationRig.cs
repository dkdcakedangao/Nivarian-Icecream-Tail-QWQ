using System;
using System.Collections.Generic;
using UnityEngine;
using RU = RimUnity;

// 为一次播放创建人物节点层级，恢复默认值喵
namespace NivarianIcecreamTail
{
    // 场景绑定
    // RimUnity 加载器和播放器
    internal sealed class NativeAnimationRig : IDisposable
    {
        private static bool registered;
        private readonly RU.Transform[] transforms;
        private readonly PawnAnimationChannels[] channels;
        private readonly int[] parents;
        private readonly RU.GameObject root;
        private readonly RU.Animator animator;
        private readonly bool mirror;
        private bool disposed;

        internal NativeAnimationRig(CachedPawnAnimation clip, RU.GameObject root, bool mirror)
        {
            this.root = root; this.mirror = mirror;
            transforms = new RU.Transform[clip.Nodes.Length];
            channels = new PawnAnimationChannels[clip.Nodes.Length];
            parents = new int[clip.Nodes.Length];
            var nodeIndices = new Dictionary<int, int>();
            for (int i = 0; i < clip.Nodes.Length; i++) nodeIndices.Add(clip.Nodes[i].ID, i);
            for (int i = 0; i < clip.Nodes.Length; i++)
                parents[i] = clip.Nodes[i].ParentID.HasValue ? nodeIndices[clip.Nodes[i].ParentID.Value] : -1;
            if (!registered)
            {
                RU.Assets.TypeResolver.Register(typeof(PawnAnimationChannels).FullName, typeof(PawnAnimationChannels));
                registered = true;
            }
            animator = root.GetComponent<RU.Animator>();
            if (animator == null) throw new InvalidOperationException("The character rig requires an Animator");
            try
            {
                root.transform.localScale = RU.Vector3.one;
                var created = new Dictionary<string, RU.Transform>(StringComparer.Ordinal) { { "", root.transform } };
                for (int i = 0; i < clip.Nodes.Length; i++)
                {
                    string current = "";
                    RU.Transform parent = root.transform;
                    foreach (string name in clip.Nodes[i].Path.Split('/'))
                    {
                        current = current.Length == 0 ? name : current + "/" + name;
                        RU.Transform next;
                        if (!created.TryGetValue(current, out next))
                        {
                            next = new RU.GameObject(name).transform;
                            next.SetParent(parent, false);
                            created.Add(current, next);
                        }
                        parent = next;
                    }
                    transforms[i] = parent;
                    channels[i] = parent.gameObject.AddComponent<PawnAnimationChannels>();
                    ApplyDefaults(clip.Nodes[i], transforms[i], channels[i]);
                }
                animator.enabled = false;
                animator.fireEvents = false;
                animator.runtimeAnimatorController = clip.Controller;
                Seek(0f, clip.Clip.length);
            }
            catch { Dispose(); throw; }
        }

        private static void ApplyDefaults(AnimationNodeJson node, RU.Transform transform, PawnAnimationChannels channels)
        {
            if (node.DefaultValues == null) return;
            RU.Vector3 position = RU.Vector3.zero, scale = RU.Vector3.one, angles = RU.Vector3.zero;
            foreach (var value in node.DefaultValues)
            {
                string type, property;
                CachedPawnAnimation.ResolveProperty(node.Path, value.Key, value.Value, false, out type, out property);
                if (type == "RimUnity.Transform")
                {
                    int axis = property[property.Length - 1] - 'x';
                    if (property.StartsWith("localPosition.")) position[axis] = value.Value;
                    else if (property.StartsWith("localScale.")) scale[axis] = value.Value;
                    else angles[axis] = value.Value;
                }
                else if (property != null) typeof(PawnAnimationChannels).GetField(property).SetValue(channels, value.Value);
            }
            transform.localPosition = position; transform.localScale = scale; transform.localEulerAngles = angles;
        }

        internal void Seek(float seconds, float length)
        {
            animator.speed = 1f;
            animator.Play(CachedPawnAnimation.StateName, 0, seconds / length);
            animator.Update(0f);
        }

        internal void Advance(float seconds)
        {
            animator.speed = 1f;
            animator.Update(seconds);
        }

        internal PawnAnimationPose ReadPose(int index)
        {
            RU.Transform node = transforms[index];
            RU.Matrix4x4 sampled = node.localToWorldMatrix;
            // RimUnity and 1.7 UnityStubs use different one-dimensional index orders.
            // gpt老师修的，大概是索引顺序不同？
            // 说实话，我原本写的target[i] = source[i]也能用，虽然不知道为什么能用？但是gpt老师反正都修了
            // gpt老师说是两种矩阵的单下标排列顺序不同，必须按 m00～m33 显式对应，不能按 [i] 直接复制。
            // 大概就是原本[1]是m01，现在变成了m10？我总感觉之后还要修……
            // 妈耶……头好疼好痛……总之能跑就行？
            // 好想喝可乐……
            // 有没有哪个聪明善良、帅气英俊的有钱人，请我喝点可乐……
            Matrix4x4 matrix = new Matrix4x4 {
                m00 = sampled.m00, m01 = sampled.m01, m02 = sampled.m02, m03 = sampled.m03,
                m10 = sampled.m10, m11 = sampled.m11, m12 = sampled.m12, m13 = sampled.m13,
                m20 = sampled.m20, m21 = sampled.m21, m22 = sampled.m22, m23 = sampled.m23,
                m30 = sampled.m30, m31 = sampled.m31, m32 = sampled.m32, m33 = sampled.m33
            };
            // 矩阵
            if (mirror) matrix = Reflection * matrix * Reflection;
            Vector3 position = matrix.MultiplyPoint3x4(Vector3.zero);
            RU.Vector3 scale = node.localScale;
            var value = channels[index];
            float visible = value.visible;
            for (int parent = parents[index]; parent >= 0; parent = parents[parent]) visible = Math.Min(visible, channels[parent].visible);
            int direction = Mathf.Clamp(Mathf.RoundToInt(value.direction), 0, 3);
            if (mirror && (direction == 1 || direction == 3)) direction = 4 - direction;
            return new PawnAnimationPose {
                position = new Vector3(position.x, position.y, position.z),
                rotation = Quaternion.Euler(0f, matrix.rotation.eulerAngles.y, 0f),
                scale = new Vector3(scale.x, scale.y, scale.z), matrix = matrix,
                flipX = mirror ? (value.flipX >= 0.5f ? 0f : 1f) : value.flipX, flipY = value.flipY, visible = visible,
                tint = new Color(value.tintR, value.tintG, value.tintB, value.tintA), facing = new Verse.Rot4(direction)
            };
        }

        private static readonly Matrix4x4 Reflection = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (animator != null) animator.runtimeAnimatorController = null;
            if (root != null)
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    RU.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        }
    }
}
