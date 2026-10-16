using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

// 首次生成手部贴图,动画播放阶段“手”相关的东西。这里其实很多都是直接复制MA的，我不确定能不能用？
// gpt老师改了部分
namespace NivarianIcecreamTail
{
    internal static class PawnAnimationGraphics
    {
        private static readonly Dictionary<Mesh, Mesh[]> Variants = new Dictionary<Mesh, Mesh[]>();
        private static Material handMaterial;

        internal static Material HandMaterial
        {
            get
            {
                if (handMaterial == null)
                {
                    var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) {
                        name = "IcecreamTailHand", filterMode = FilterMode.Bilinear
                    };
                    texture.SetPixels(MakeHandPixels(64));
                    texture.Apply(false, true);
                    handMaterial = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Cutout, Color.white));
                }
                return handMaterial;
            }
        }

        internal static Color[] MakeHandPixels(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float radius = u * u + v * v;
                    pixels[y * size + x] = radius > 0.25f ? Color.clear : radius > 0.405f * 0.405f ? Color.black :
                        (u + 0.035f) * (u + 0.035f) + (v + 0.05f) * (v + 0.05f) < 0.36f * 0.36f ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
                }
            return pixels;
        }

        internal static Color HandColor(Color skin, PawnAnimationPose pose)
        {
            // 手部颜色，这个是抄的MA的
            skin.a *= pose.tint.a;
            return skin;
        }

        internal static bool Reversed(Matrix4x4 m)
        {
            return m.m00 * (m.m11 * m.m22 - m.m12 * m.m21) - m.m01 * (m.m10 * m.m22 - m.m12 * m.m20) +
                m.m02 * (m.m10 * m.m21 - m.m11 * m.m20) < 0f;
        }

        internal static int MeshMask(PawnAnimationPose pose, bool parentReversed)
        {
            return (pose.flipX >= 0.5f ? 1 : 0) | (pose.flipY >= 0.5f ? 2 : 0) |
                ((pose.scale.x * pose.scale.y * pose.scale.z < 0f) != parentReversed ? 4 : 0);
        }

        internal static Mesh GetMesh(Mesh original, int mask)
        {
            if (original == null || mask == 0) return original;
            lock (Variants) return GetOrCreateMesh(original, mask);
        }

        private static Mesh GetOrCreateMesh(Mesh original, int mask)
        {
            Mesh[] variants;
            if (!Variants.TryGetValue(original, out variants))
            {
                variants = new Mesh[8];
                Variants.Add(original, variants);
            }
            if (variants[mask] != null) return variants[mask];
            var uv = (Vector2[])original.uv.Clone();
            for (int i = 0; i < uv.Length; i++)
            {
                if ((mask & 1) != 0) uv[i].x = 1f - uv[i].x;
                if ((mask & 2) != 0) uv[i].y = 1f - uv[i].y;
            }
            var triangles = (int[])original.triangles.Clone();
            if ((mask & 4) != 0)
                for (int i = 0; i < triangles.Length; i += 3)
                { int first = triangles[i]; triangles[i] = triangles[i + 1]; triangles[i + 1] = first; }
            var mesh = new Mesh {
                name = "IcecreamTailSprite_" + mask, vertices = original.vertices, normals = original.normals,
                uv = uv, uv2 = original.uv2, triangles = triangles
            };
            mesh.RecalculateBounds();
            variants[mask] = mesh;
            return mesh;
        }
    }
}
