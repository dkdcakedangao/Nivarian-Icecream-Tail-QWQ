using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Verse;
using RU = RimUnity;

// JSON 数据结构、属性翻译、校验及曲线／控制器缓存；XML Def 留在主模块。
// 石山，待优化
// gpt老师已经帮我优化过一次了，之前没法调用动画的时候，gpt老师改过一次翻译
// 接下来如果要优化，应该是尝试做一下翻译缓存？省的每次读取都翻译一次？
namespace NivarianIcecreamTail
{
    // 公共字段喵：RimUnity 绑定一次，然后进缓存
    public sealed class PawnAnimationChannels : RU.Component
    {
        public float direction;
        public float flipX, flipY;
        public float visible = 1f;
        public float tintR = 1f, tintG = 1f, tintB = 1f, tintA = 1f;
    }

    internal sealed class AnimationJson
    {
        public string Name { get; set; }
        public string ExportTimeUTC { get; set; }
        public JsonElement Bounds { get; set; }
        public float Length { get; set; }
        public AnimationNodeJson[] Parts { get; set; }
        public AnimationEventJson[] Events { get; set; }
    }
    internal sealed class AnimationNodeJson
    {
        public int ID { get; set; }
        public int? ParentID { get; set; }
        public string Path { get; set; }
        public string CustomName { get; set; }
        public string TexturePath { get; set; }
        public bool TransparentByDefault { get; set; }
        public int? SplitDrawPivotPartID { get; set; }
        public JsonElement SweepPaths { get; set; }
        public Dictionary<string, AnimationCurveJson> Curves { get; set; }
        public Dictionary<string, float> DefaultValues { get; set; }
    }
    internal sealed class AnimationCurveJson
    {
        public AnimationKeyJson[] Keyframes { get; set; }
        public int PreWrapMode { get; set; }
        public int PostWrapMode { get; set; }
    }
    internal sealed class AnimationKeyJson
    {
        public float time { get; set; }
        public float value { get; set; }
        public float inTangent { get; set; }
        public float outTangent { get; set; }
        public float inWeight { get; set; } = 1f / 3f;
        public float outWeight { get; set; } = 1f / 3f;
        public int weightedMode { get; set; }
        public int tangentMode { get; set; }
    }
    internal sealed class AnimationEventJson
    {
        public float Time { get; set; }
        public string Data { get; set; }
    }

    internal sealed class CachedPawnAnimation
    {
        private static readonly Dictionary<PawnAnimationDef, CachedPawnAnimation> Cache = new Dictionary<PawnAnimationDef, CachedPawnAnimation>();
        private static readonly HashSet<PawnAnimationDef> Failed = new HashSet<PawnAnimationDef>();
        internal readonly PawnAnimationDef Def;
        internal readonly AnimationNodeJson[] Nodes;
        internal readonly AnimationEventJson[] Sounds;
        internal readonly RU.AnimationClip Clip;
        internal readonly RU.RuntimeAnimatorController Controller;
        internal readonly int[] BodyIndices, HeadIndices;
        internal readonly int[][] HandIndices;
        internal const string StateName = "CharacterClip";

        internal static CachedPawnAnimation Get(PawnAnimationDef def)
        {
            if (def == null || Failed.Contains(def)) return null;
            CachedPawnAnimation result;
            if (Cache.TryGetValue(def, out result)) return result;
            try
            {
                string root = Path.GetFullPath(def.modContentPack.RootDir) + Path.DirectorySeparatorChar;
                string path = Path.GetFullPath(Path.Combine(root, def.jsonPath));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("jsonPath escapes the mod directory");
                result = Parse(def, File.ReadAllText(path));
                Cache.Add(def, result);
                return result;
            }
            catch (Exception error)
            {
                Failed.Add(def);
                throw new InvalidDataException("Character animation '" + def.defName + "': " + error.Message, error);
            }
        }

        internal static CachedPawnAnimation Parse(PawnAnimationDef def, string json)
        {
            var data = JsonSerializer.Deserialize<AnimationJson>(json, new JsonSerializerOptions {
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            });
            if (data == null || !float.IsFinite(data.Length) || data.Length <= 0f || data.Parts == null || def.roles.Count != 2)
                throw new InvalidDataException("Animation requires a finite positive length, parts and two roles");
            return new CachedPawnAnimation(def, data);
        }

        private CachedPawnAnimation(PawnAnimationDef def, AnimationJson data)
        {
            Def = def;
            if (def.roles[0].body == def.roles[1].body || def.roles.Any(role => !IsUnder(role.head, role.body)))
                throw new InvalidDataException("Roles require distinct bodies and heads under their own bodies");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<int>();
            var nodes = new List<AnimationNodeJson>();
            foreach (var node in data.Parts)
            {
                if (string.IsNullOrEmpty(node.Path) || !paths.Add(node.Path) || !ids.Add(node.ID))
                    throw new InvalidDataException("Duplicate/missing node path or ID: " + node.Path);
                bool ignored = def.ignoredRoots.Any(root => IsUnder(node.Path, root));
                if (ignored) continue;
                if (!def.roles.Any(role => IsUnder(node.Path, role.body) || IsUnder(node.Path, role.handsRoot)))
                    throw new InvalidDataException("Unbound character/unsupported node: " + node.Path);
                if (!string.IsNullOrEmpty(node.TexturePath) || node.SplitDrawPivotPartID.HasValue ||
                    (node.SweepPaths.ValueKind == JsonValueKind.Array && node.SweepPaths.GetArrayLength() != 0))
                    throw new InvalidDataException("Unsupported character texture/cutoff/sweep: " + node.Path);
                nodes.Add(node);
            }
            Nodes = nodes.ToArray();
            var indexById = Nodes.Select((node, index) => new { node.ID, index }).ToDictionary(x => x.ID, x => x.index);
            foreach (var node in Nodes)
            {
                string expected = node.Path.Contains('/') ? node.Path.Substring(0, node.Path.LastIndexOf('/')) : null;
                if (node.ParentID.HasValue)
                {
                    int parent;
                    if (!indexById.TryGetValue(node.ParentID.Value, out parent) || Nodes[parent].Path != expected)
                        throw new InvalidDataException("Missing/mismatched parent at " + node.Path);
                }
                else if (expected != null) throw new InvalidDataException("Missing ParentID at " + node.Path);
            }
            BodyIndices = new int[2]; HeadIndices = new int[2]; HandIndices = new int[2][];
            for (int i = 0; i < 2; i++)
            {
                BodyIndices[i] = Array.FindIndex(Nodes, n => n.Path == def.roles[i].body);
                HeadIndices[i] = Array.FindIndex(Nodes, n => n.Path == def.roles[i].head);
                if (BodyIndices[i] < 0 || HeadIndices[i] < 0) throw new InvalidDataException("Missing body/head for role " + i);
                HandIndices[i] = Nodes.Select((n, index) => new { n, index }).Where(x => IsUnder(x.n.Path, def.roles[i].handsRoot)
                    && x.n.Path != def.roles[i].handsRoot).Select(x => x.index).ToArray();
            }

            var tracks = new List<RU.AnimationTrack>();
            foreach (var node in Nodes)
            {
                if (node.DefaultValues != null)
                    foreach (var value in node.DefaultValues)
                    {
                        string type, property;
                        ResolveProperty(node.Path, value.Key, value.Value, false, out type, out property);
                    }
                if (node.Curves == null) continue;
                foreach (var pair in node.Curves)
                {
                    string type, property;
                    ResolveProperty(node.Path, pair.Key, 0f, true, out type, out property);
                    var keys = pair.Value.Keyframes;
                    if (keys == null || keys.Length == 0) throw new InvalidDataException("Empty curve at " + node.Path + ": " + pair.Key);
                    float previous = -1f;
                    var frames = new RU.Keyframe[keys.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var key = keys[i];
                        if (!float.IsFinite(key.time) || !float.IsFinite(key.value) || key.time < previous || key.time < 0 || key.time > data.Length ||
                            float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent) || !float.IsFinite(key.inWeight) || !float.IsFinite(key.outWeight) || key.weightedMode < 0 || key.weightedMode > 3)
                            throw new InvalidDataException("Invalid keyframe at " + node.Path + ": " + pair.Key);
                        previous = key.time;
                        frames[i] = new RU.Keyframe(key.time, key.value, key.inTangent, key.outTangent, key.inWeight, key.outWeight) { weightedMode = (RU.WeightedMode)key.weightedMode };
                    }
                    tracks.Add(new RU.AnimationTrack { path = node.Path, type = type, property = property,
                        curve = new RU.AnimationCurve(frames) { preWrapMode = Wrap(pair.Value.PreWrapMode), postWrapMode = Wrap(pair.Value.PostWrapMode) } });
                }
            }
            Clip = new RU.AnimationClip { name = def.defName, length = data.Length, frameRate = 60f, isLooping = false,
                tracks = tracks.ToArray(), events = Array.Empty<RU.AnimationEvent>() };
            Controller = new RU.RuntimeAnimatorController { layers = new[] { new RU.AnimatorLayer {
                name = "Character", defaultState = StateName, defaultWeight = 1f,
                states = new[] { new RU.AnimatorState { name = StateName, speed = 1f, motion = new RU.AnimationMotion { clip = Clip } } }
            } } };
            Sounds = (data.Events ?? Array.Empty<AnimationEventJson>()).OrderBy(e => e.Time).ToArray();
            foreach (var sound in Sounds)
            {
                var fields = sound.Data == null ? Array.Empty<string>() : sound.Data.Split(';');
                if (!float.IsFinite(sound.Time) || sound.Time < 0 || sound.Time > Clip.length || fields.Length != 6 || fields[0] != "Audio" || string.IsNullOrEmpty(fields[1]))
                    throw new InvalidDataException("Unsupported event at " + sound.Time + ": " + sound.Data);
                if (fields[2] != "0,0,0" || fields[3] != "1" || fields[4] != "1" || fields[5] != "False")
                    throw new InvalidDataException("Unsupported audio offset/volume/pitch/follow at " + sound.Time + ": " + sound.Data);
            }
        }

        private static RU.WrapMode Wrap(int mode)
        {
            if (mode == 0 || mode == 1 || mode == 8) return RU.WrapMode.ClampForever;
            if (mode == 2) return RU.WrapMode.Loop;
            if (mode == 4) return RU.WrapMode.PingPong;
            throw new InvalidDataException("Unsupported curve wrap mode " + mode);
        }

        internal static bool IsUnder(string path, string root)
        {
            return !string.IsNullOrEmpty(root) && (path == root || path.StartsWith(root + "/", StringComparison.Ordinal));
        }

        internal static void ResolveProperty(string path, string source, float value, bool animated, out string type, out string property)
        {
            type = "RimUnity.Transform";
            property = source;
            if (!float.IsFinite(value)) throw new InvalidDataException("Invalid default at " + path + ": " + source);
            string[] prefixes = { "Transform.m_LocalPosition.", "Transform.m_LocalScale.", "Transform.localEulerAnglesRaw." };
            string[] targets = { "localPosition.", "localScale.", "localEulerAngles." };
            for (int i = 0; i < prefixes.Length; i++)
                if (source.StartsWith(prefixes[i], StringComparison.Ordinal) && source.Length == prefixes[i].Length + 1 && "xyz".Contains(source[source.Length - 1]))
                { property = targets[i] + source[source.Length - 1]; return; }
            type = typeof(PawnAnimationChannels).FullName;
            switch (source)
            {
                case "PawnBody.Direction": property = "direction"; return;
                case "AnimatedPart.FlipX": property = "flipX"; return;
                case "AnimatedPart.FlipY": property = "flipY"; return;
                case "GameObject.m_IsActive": property = "visible"; return;
                case "AnimatedPart.Tint.r": property = "tintR"; return;
                case "AnimatedPart.Tint.g": property = "tintG"; return;
                case "AnimatedPart.Tint.b": property = "tintB"; return;
                case "AnimatedPart.Tint.a": property = "tintA"; return;
                case "AnimatedPart.DataA": case "AnimatedPart.DataB": case "AnimatedPart.DataC":
                case "AnimatedPart.SplitDrawMode": case "AnimatedPart.FrameIndex":
                    if (!animated && value == 0f) { property = null; return; }
                    break;
            }
            throw new InvalidDataException("Unsupported character property at " + path + ": " + source);
        }
    }
}
