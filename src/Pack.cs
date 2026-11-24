using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PortalGunQuest
{
    internal sealed class PartDef
    {
        internal string Name;
        internal int Parent, Mesh;
        internal Vector3 Pos, Scale;
        internal Quaternion Rot;
        internal int[] Mats;
        internal List<Vector3[]> Boxes = new List<Vector3[]>();
    }

    internal sealed class MeshDef
    {
        internal string Name;
        internal Vector3[] V, N;
        internal Vector2[] UV;
        internal List<int[]> Subs = new List<int[]>();
        internal Mesh Made;
    }

    internal sealed class MatDef
    {
        internal string Name;
        internal Color Color;
        internal int Tex;
        internal float Gloss, Metallic;
        internal Material Made;
    }

    internal sealed class TexDef
    {
        internal string Name;
        internal int W, H;
        internal byte[] Data;
        internal Texture2D Made;
    }

    internal static class Pack
    {
        internal const string Simple = "Township Tale/Cheap/SimpleStandard";
        static readonly List<TexDef> Textures = new List<TexDef>();
        static readonly List<MatDef> Materials = new List<MatDef>();
        static readonly List<MeshDef> Meshes = new List<MeshDef>();
        static readonly Dictionary<string, List<PartDef>> Objects = new Dictionary<string, List<PartDef>>();
        static Material _template;
        static bool _looked;
        internal static bool Loaded;

        internal static void Read()
        {
            if (Loaded) return;
            byte[] raw;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("PortalGunQuest.portalgun.pack"))
            {
                if (s == null) throw new InvalidDataException("the portal pack is missing from the DLL");
                byte[] head = new byte[4];
                if (s.Read(head, 0, 4) != 4) throw new InvalidDataException("the portal pack is empty");
                int size = BitConverter.ToInt32(head, 0);
                raw = new byte[size];
                using (DeflateStream d = new DeflateStream(s, CompressionMode.Decompress))
                {
                    int got = 0;
                    while (got < size)
                    {
                        int n = d.Read(raw, got, size - got);
                        if (n <= 0) throw new InvalidDataException("the portal pack is cut short");
                        got += n;
                    }
                }
            }
            using (BinaryReader r = new BinaryReader(new MemoryStream(raw)))
            {
                if (new string(r.ReadChars(4)) != "PGQ1") throw new InvalidDataException("the portal pack has the wrong header");
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    TexDef t = new TexDef { Name = r.ReadString(), W = r.ReadInt32(), H = r.ReadInt32() };
                    r.ReadInt32();
                    t.Data = r.ReadBytes(r.ReadInt32());
                    Textures.Add(t);
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    MatDef m = new MatDef { Name = r.ReadString() };
                    m.Color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    m.Tex = r.ReadInt32();
                    m.Gloss = r.ReadSingle();
                    m.Metallic = r.ReadSingle();
                    Materials.Add(m);
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    MeshDef m = new MeshDef { Name = r.ReadString() };
                    int n = r.ReadInt32();
                    m.V = new Vector3[n];
                    m.N = new Vector3[n];
                    m.UV = new Vector2[n];
                    for (int k = 0; k < n; k++) m.V[k] = V3(r);
                    for (int k = 0; k < n; k++) m.N[k] = V3(r);
                    for (int k = 0; k < n; k++) m.UV[k] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    int subs = r.ReadInt32();
                    for (int k = 0; k < subs; k++)
                    {
                        int[] idx = new int[r.ReadInt32()];
                        for (int j = 0; j < idx.Length; j++) idx[j] = r.ReadInt32();
                        m.Subs.Add(idx);
                    }
                    Meshes.Add(m);
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = r.ReadString();
                    int parts = r.ReadInt32();
                    List<PartDef> list = new List<PartDef>();
                    for (int k = 0; k < parts; k++)
                    {
                        PartDef p = new PartDef { Name = r.ReadString(), Parent = r.ReadInt32() };
                        p.Pos = V3(r);
                        p.Rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        p.Scale = V3(r);
                        p.Mesh = r.ReadInt32();
                        p.Mats = new int[r.ReadInt32()];
                        for (int j = 0; j < p.Mats.Length; j++) p.Mats[j] = r.ReadInt32();
                        int boxes = r.ReadInt32();
                        for (int j = 0; j < boxes; j++) p.Boxes.Add(new[] { V3(r), V3(r) });
                        list.Add(p);
                    }
                    Objects[name] = list;
                }
            }
            Loaded = true;
        }

        static Vector3 V3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        internal static List<PartDef> Parts(string name)
        {
            List<PartDef> list;
            if (!Objects.TryGetValue(name, out list)) throw new KeyNotFoundException("the portal pack has no " + name);
            return list;
        }

        internal static Mesh MeshOf(int i)
        {
            MeshDef d = Meshes[i];
            if (d.Made == null)
            {
                d.Made = Plat.NewMesh("PortalGun " + d.Name, d.V, d.N, d.UV, d.Subs);
                d.Made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return d.Made;
        }

        static Texture2D TextureOf(int i)
        {
            if (i < 0 || i >= Textures.Count) return null;
            TexDef d = Textures[i];
            if (d.Made == null && d.Data != null)
            {
                d.Made = Plat.NewTexture("PortalGun " + d.Name, d.W, d.H, d.Data);
                d.Data = null;
            }
            return d.Made;
        }

        internal static Material MaterialOf(int i)
        {
            MatDef d = Materials[i];
            if (d.Made != null) return d.Made;
            Material m = Lit();
            m.name = "PortalGun " + d.Name;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            SetColor(m, "_Color", d.Color);
            Texture2D t = TextureOf(d.Tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            SetFloat(m, "_Glossiness", d.Gloss);
            SetFloat(m, "_Metallic", d.Metallic);
            d.Made = m;
            return m;
        }

        internal static Material Lit()
        {
            LookUp();
            Material m = null;
            if (_template != null) m = Plat.CopyAsset(_template);
            if (m == null)
            {
                Shader s = Shader.Find(Simple);
                if (s == null) throw new InvalidOperationException("the game shader " + Simple + " is missing");
                m = new Material(s);
            }
            Switch(m, "_MetallicMap", "_UseMetallicMap", false);
            Switch(m, "_Normal", "_UseNormal", false);
            Switch(m, "_ScaledNormal", "_UseScaledNormal", false);
            Switch(m, "_AlphaSmooth", "_UseAlphaSmooth", false);
            Switch(m, "_AlphaEmission", "_UseAlphaEmission", false);
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_EMISSION");
            m.DisableKeyword("_ALPHATEST_ON");
            SetColor(m, "_EmissionColor", Color.black);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
            SetFloat(m, "_Cull", 2f);
            SetFloat(m, "_ZWrite", 1f);
            SetFloat(m, "_SrcBlend", 1f);
            SetFloat(m, "_DstBlend", 0f);
            m.renderQueue = 2000;
            return m;
        }

        static void LookUp()
        {
            if (_looked) return;
            _looked = true;
            int best = int.MaxValue;
            foreach (Object o in Plat.Loaded<Material>())
            {
                Material m = Plat.As<Material>(o);
                Shader sh = m == null ? null : m.shader;
                if (sh == null || sh.name != Simple) continue;
                if (m.renderQueue >= 2450 || m.IsKeywordEnabled("_UseAlphaSmooth") || m.IsKeywordEnabled("_ALPHATEST_ON")) continue;
                string n = m.name;
                if (n == null || n.StartsWith("PortalGun", StringComparison.Ordinal) || n.StartsWith("ArmorRestored", StringComparison.Ordinal) || n.IndexOf("Font", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                int extra = (m.IsKeywordEnabled("_UseMetallicMap") ? 4 : 0) + (m.IsKeywordEnabled("_UseNormal") ? 4 : 0) + (m.IsKeywordEnabled("_EMISSION") ? 2 : 0) + (m.IsKeywordEnabled("_UseScaledNormal") ? 1 : 0) + (m.IsKeywordEnabled("_UseAlphaEmission") ? 2 : 0);
                if (extra < best)
                {
                    best = extra;
                    _template = m;
                }
            }
            Core.Log("material template: " + (_template == null ? "none, a new " + Simple + " material" : _template.name));
        }

        internal static void SetColor(Material m, string property, Color c)
        {
            if (m != null && m.HasProperty(property)) m.SetColor(property, c);
        }

        static void SetFloat(Material m, string property, float v)
        {
            if (m.HasProperty(property)) m.SetFloat(property, v);
        }

        static void Switch(Material m, string property, string keyword, bool on)
        {
            if (m.HasProperty(property)) m.SetFloat(property, on ? 1f : 0f);
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        internal static GameObject Build(string name, Transform parent, int layer, List<Collider> colliders)
        {
            List<PartDef> parts = Parts(name);
            List<Transform> made = new List<Transform>();
            for (int i = 0; i < parts.Count; i++)
            {
                PartDef p = parts[i];
                GameObject go = new GameObject(p.Name);
                go.layer = layer;
                Transform t = go.transform;
                t.SetParent(p.Parent < 0 ? parent : made[p.Parent], false);
                t.localPosition = p.Pos;
                t.localRotation = p.Rot;
                t.localScale = p.Scale;
                made.Add(t);
                if (p.Mesh >= 0)
                {
                    Plat.Add<MeshFilter>(go).sharedMesh = MeshOf(p.Mesh);
                    MeshRenderer mr = Plat.Add<MeshRenderer>(go);
                    List<Material> mats = new List<Material>();
                    foreach (int k in p.Mats) mats.Add(MaterialOf(k));
                    Plat.SetMaterials(mr, mats);
                }
                foreach (Vector3[] b in p.Boxes)
                {
                    BoxCollider box = Plat.Add<BoxCollider>(go);
                    box.center = b[0];
                    box.size = b[1];
                    if (colliders != null) colliders.Add(box);
                }
            }
            return made[0].gameObject;
        }

        internal static Shader Find(params string[] names)
        {
            foreach (string n in names)
            {
                Shader s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }
    }
}
