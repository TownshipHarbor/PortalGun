using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PortalGunQuest
{
    internal sealed class View
    {
        const int Width = 384, Height = 576;
        const float MaxDistance = 30f;
        static bool _rendering;
        static Shader _unlit, _flat;
        static readonly List<View> All = new List<View>();
        static readonly Dictionary<string, Mesh> Shapes = new Dictionary<string, Mesh>();
        internal static int Rendered, Frame;

        readonly Transform _entry;
        readonly float _hw, _hh;
        readonly bool _rect;
        readonly GameObject _display;
        readonly MeshRenderer _surface;
        readonly MeshRenderer _ring;
        readonly Material _live, _idle, _rim;
        Camera _cam;
        RenderTexture _rt;
        Matrix4x4 _exit;
        bool _linked, _shown, _told;
        Color _color;

        internal View(Transform entry, float hw, float hh, bool rect)
        {
            _entry = entry;
            _hw = hw;
            _hh = hh;
            _rect = rect;
            if (_unlit == null) _unlit = Pack.Find("Unlit/Texture", "Mobile/Unlit (Supports Lightmap)", "UI/Default", "Sprites/Default");
            if (_flat == null) _flat = Pack.Find("Unlit/Color", "UI/Default", "Sprites/Default");
            _display = new GameObject("Portal surface");
            _display.layer = entry.gameObject.layer;
            _display.transform.SetParent(entry, false);
            _surface = Shape("Portal view", _display.transform, Cached("surface", hw, hh, rect));
            _ring = Shape("Portal rim", _display.transform, Cached("rim", hw, hh, rect));
            _ring.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            _live = new Material(_unlit);
            _live.name = "PortalGun view";
            _idle = new Material(_flat);
            _idle.name = "PortalGun idle";
            _rim = new Material(_flat);
            _rim.name = "PortalGun rim";
            _live.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _idle.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _rim.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _surface.sharedMaterial = _idle;
            _ring.sharedMaterial = _rim;
            All.Add(this);
        }

        static MeshRenderer Shape(string name, Transform parent, Mesh mesh)
        {
            GameObject g = new GameObject(name);
            g.layer = parent.gameObject.layer;
            g.transform.SetParent(parent, false);
            Plat.Add<MeshFilter>(g).sharedMesh = mesh;
            MeshRenderer r = Plat.Add<MeshRenderer>(g);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static Mesh Cached(string kind, float hw, float hh, bool rect)
        {
            string key = kind + " " + hw + " " + hh + " " + rect;
            Mesh m;
            if (Shapes.TryGetValue(key, out m) && m != null) return m;
            m = kind == "rim" ? Rim(hw, hh, rect) : Surface(hw, hh, rect);
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Shapes[key] = m;
            return m;
        }

        static Vector3 Local(float sx, float sy, float z)
        {
            return new Vector3(-sx, sy, z);
        }

        static Mesh Facing(string name, List<Vector3> v, List<Vector2> uv, List<int> tri)
        {
            Vector3 a = v[tri[0]], b = v[tri[1]], c = v[tri[2]];
            if (Vector3.Cross(b - a, c - a).z < 0f)
            {
                for (int i = 0; i < tri.Count; i += 3)
                {
                    int t = tri[i + 1];
                    tri[i + 1] = tri[i + 2];
                    tri[i + 2] = t;
                }
            }
            Vector3[] n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.forward;
            List<int[]> subs = new List<int[]>();
            subs.Add(tri.ToArray());
            return Plat.NewMesh(name, v.ToArray(), n, uv.ToArray(), subs);
        }

        static Mesh Surface(float hw, float hh, bool rect)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            if (rect)
            {
                float[] xs = { -hw, hw, hw, -hw }, ys = { -hh, -hh, hh, hh };
                for (int i = 0; i < 4; i++)
                {
                    v.Add(Local(xs[i], ys[i], 0f));
                    uv.Add(new Vector2((xs[i] + hw) / (2f * hw), (ys[i] + hh) / (2f * hh)));
                }
                tri.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            }
            else
            {
                const int steps = 48;
                v.Add(Local(0f, 0f, 0f));
                uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    float sx = Mathf.Cos(a) * hw, sy = Mathf.Sin(a) * hh;
                    v.Add(Local(sx, sy, 0f));
                    uv.Add(new Vector2((sx + hw) / (2f * hw), (sy + hh) / (2f * hh)));
                }
                for (int i = 0; i < steps; i++) tri.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % steps });
            }
            return Facing("PortalGun surface", v, uv, tri);
        }

        static Mesh Rim(float hw, float hh, bool rect)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            int steps = rect ? 4 : 48;
            float inset = rect ? 0.035f : 0.06f;
            for (int i = 0; i < steps; i++)
            {
                float ox, oy, ix, iy;
                if (rect)
                {
                    float sx = i == 0 || i == 3 ? -1f : 1f, sy = i < 2 ? -1f : 1f;
                    ox = sx * hw; oy = sy * hh;
                    ix = sx * (hw - inset); iy = sy * (hh - inset);
                }
                else
                {
                    float a = i * Mathf.PI * 2f / steps;
                    ox = Mathf.Cos(a) * hw; oy = Mathf.Sin(a) * hh;
                    ix = Mathf.Cos(a) * hw * (1f - inset); iy = Mathf.Sin(a) * hh * (1f - inset);
                }
                v.Add(Local(ox, oy, 0f));
                v.Add(Local(ix, iy, 0f));
                uv.Add(Vector2.zero);
                uv.Add(Vector2.zero);
            }
            for (int i = 0; i < steps; i++)
            {
                int o0 = i * 2, i0 = o0 + 1, o1 = (i + 1) % steps * 2, i1 = o1 + 1;
                tri.AddRange(new[] { o0, o1, i1, o0, i1, i0 });
            }
            return Facing("PortalGun rim", v, uv, tri);
        }

        internal void SetColor(Color c, bool linked)
        {
            _color = c;
            Pack.SetColor(_rim, "_Color", c * (linked ? 1f : 0.4f));
            Pack.SetColor(_idle, "_Color", c * 0.3f);
        }

        internal void SetExit(bool ready, Vector3 p, Vector3 n, Vector3 u)
        {
            _linked = ready && TransitMath.Finite(p) && n.sqrMagnitude > 0.5f && u.sqrMagnitude > 0.5f;
            if (_linked) _exit = Matrix4x4.TRS(p, Quaternion.LookRotation(n, u), Vector3.one);
            if (!_linked) Show(false);
        }

        void Show(bool live)
        {
            if (_shown == live) return;
            _shown = live;
            _surface.sharedMaterial = live ? _live : _idle;
        }

        internal static void RenderAll()
        {
            Frame++;
            Camera source = Viewer();
            if (source == null) return;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                View v = All[i];
                if (v._entry == null || v._surface == null || v._idle == null || v._live == null) { v.Destroy(); continue; }
                v.Tick(source);
            }
        }

        static Camera _viewer;
        static float _nextSearch;
        static string _cameraTold;

        static Camera Viewer()
        {
            Il2Cpp.PlayerController me = Il2Cpp.PlayerController.Current;
            Camera own = me == null ? null : me.Camera;
            if (own != null && own.isActiveAndEnabled) return Tell(own, "player");
            Camera c = Camera.main;
            if (c != null && c.isActiveAndEnabled) return Tell(c, "main");
            if (_viewer != null && _viewer.isActiveAndEnabled) return _viewer;
            _viewer = null;
            if (Time.time < _nextSearch) return null;
            _nextSearch = Time.time + 2f;
            float depth = float.MinValue;
            foreach (Object o in Plat.Loaded<Camera>())
            {
                Camera k = Plat.As<Camera>(o);
                if (k == null || !k.isActiveAndEnabled || k.targetTexture != null || k.hideFlags != HideFlags.None) continue;
                if (k.depth > depth) { depth = k.depth; _viewer = k; }
            }
            if (_viewer != null) return Tell(_viewer, "deepest enabled");
            Il2Cpp.PlayerController pc = Il2Cpp.PlayerController.Current;
            if (pc == null || pc.Head == null) return null;
            foreach (Camera k in Plat.All<Camera>(pc.Head, false))
            {
                if (k.isActiveAndEnabled && k.targetTexture == null) { _viewer = k; break; }
            }
            return _viewer == null ? null : Tell(_viewer, "head");
        }

        static Camera Tell(Camera c, string how)
        {
            string n = how + " " + c.name;
            if (n != _cameraTold)
            {
                _cameraTold = n;
                Core.Log("portal views follow the " + how + " camera " + c.name);
            }
            return c;
        }

        void Tick(Camera source)
        {
            float wave = 0.05f + 0.07f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f));
            Pack.SetColor(_idle, "_Color", _color * (wave * 3f));
            if (!_linked || _rendering) { Show(false); return; }
            Vector3 eye = source.transform.position;
            Vector3 f = _entry.forward;
            float front = Vector3.Dot(eye - _entry.position, f);
            string why = front <= 0.0001f ? "behind it" : (eye - _entry.position).sqrMagnitude > MaxDistance * MaxDistance ? "too far" : !InView(source) ? "out of view" : null;
            if (why != null) { Idle(why); Show(false); return; }
            if (Capture(source, eye)) Show(true);
            else Idle("too close to the far side");
        }

        float _nextIdle;

        void Idle(string why)
        {
            if (Time.time < _nextIdle || _told) return;
            _nextIdle = Time.time + 10f;
            Core.Log("portal view waits: " + why);
        }

        bool InView(Camera source)
        {
            Plane[] planes = Planes(source);
            return GeometryUtility.TestPlanesAABB(planes, _surface.bounds);
        }

        static Plane[] Planes(Camera c)
        {
            Plane[] list = new Plane[6];
            int i = 0;
            foreach (Plane p in GeometryUtility.CalculateFrustumPlanes(c)) if (i < 6) list[i++] = p;
            return list;
        }

        bool Capture(Camera source, Vector3 eye)
        {
            Matrix4x4 map = _exit * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * _entry.worldToLocalMatrix;
            Vector3 right = -_entry.right, up = _entry.up, c = _entry.position;
            Vector3 pa = map.MultiplyPoint(c - right * _hw - up * _hh);
            Vector3 pb = map.MultiplyPoint(c + right * _hw - up * _hh);
            Vector3 pc = map.MultiplyPoint(c - right * _hw + up * _hh);
            Vector3 pe = map.MultiplyPoint(eye);
            Vector3 vr = (pb - pa).normalized, vu = (pc - pa).normalized;
            Vector3 vn = Vector3.Cross(vr, vu).normalized;
            if (Vector3.Dot(vn, pe - pa) < 0f) vn = -vn;
            Vector3 va = pa - pe, vb = pb - pe, vc = pc - pe;
            float d = -Vector3.Dot(va, vn);
            if (d < 0.03f) return _shown;
            float near = Mathf.Max(0.01f, d - 0.005f), far = Mathf.Max(near + 1f, source.farClipPlane);
            float l = Vector3.Dot(vr, va) * near / d, r = Vector3.Dot(vr, vb) * near / d;
            float b = Vector3.Dot(vu, va) * near / d, t = Vector3.Dot(vu, vc) * near / d;
            if (_cam == null) Make();
            _rendering = true;
            ShadowQuality shadows = QualitySettings.shadows;
            List<bool> hidden = HideSurfaces();
            try
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                _cam.CopyFrom(source);
                _cam.enabled = false;
                _cam.stereoTargetEye = StereoTargetEyeMask.None;
                _cam.allowMSAA = false;
                _cam.allowHDR = false;
                _cam.depthTextureMode = DepthTextureMode.None;
                _cam.useOcclusionCulling = false;
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.transform.SetPositionAndRotation(pe, Quaternion.LookRotation(-vn, vu));
                _cam.nearClipPlane = near;
                _cam.farClipPlane = far;
                _cam.worldToCameraMatrix = ViewMatrix(vr, vu, vn, pe);
                _cam.projectionMatrix = Frustum(l, r, b, t, near, far);
                _cam.targetTexture = _rt;
                _cam.Render();
                _cam.targetTexture = null;
                Rendered++;
                if (!_told)
                {
                    _told = true;
                    Core.Log("portal view live: " + Width + "x" + Height + ", eye " + d.ToString("0.00") + " m from the far side");
                }
            }
            finally
            {
                QualitySettings.shadows = shadows;
                ShowSurfaces(hidden);
                _rendering = false;
            }
            return true;
        }

        static List<bool> HideSurfaces()
        {
            List<bool> was = new List<bool>();
            foreach (View v in All)
            {
                bool on = v._surface != null && v._surface.enabled;
                was.Add(on);
                if (!on) continue;
                v._surface.enabled = false;
                v._ring.enabled = false;
            }
            return was;
        }

        static void ShowSurfaces(List<bool> was)
        {
            for (int i = 0; i < All.Count && i < was.Count; i++)
            {
                if (!was[i] || All[i]._surface == null) continue;
                All[i]._surface.enabled = true;
                All[i]._ring.enabled = true;
            }
        }

        static Matrix4x4 ViewMatrix(Vector3 vr, Vector3 vu, Vector3 vn, Vector3 pe)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = vr.x; m.m01 = vr.y; m.m02 = vr.z; m.m03 = -Vector3.Dot(vr, pe);
            m.m10 = vu.x; m.m11 = vu.y; m.m12 = vu.z; m.m13 = -Vector3.Dot(vu, pe);
            m.m20 = vn.x; m.m21 = vn.y; m.m22 = vn.z; m.m23 = -Vector3.Dot(vn, pe);
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        static Matrix4x4 Frustum(float l, float r, float b, float t, float n, float f)
        {
            Matrix4x4 m = Matrix4x4.zero;
            m.m00 = 2f * n / (r - l);
            m.m02 = (r + l) / (r - l);
            m.m11 = 2f * n / (t - b);
            m.m12 = (t + b) / (t - b);
            m.m22 = -(f + n) / (f - n);
            m.m23 = -2f * f * n / (f - n);
            m.m32 = -1f;
            return m;
        }

        void Make()
        {
            GameObject g = new GameObject("Portal view camera");
            g.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(g);
            _cam = Plat.Add<Camera>(g);
            _cam.enabled = false;
            _rt = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32);
            _rt.name = "PortalGun view";
            _rt.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _rt.antiAliasing = 1;
            _rt.Create();
            _live.mainTexture = _rt;
        }

        internal void Destroy()
        {
            All.Remove(this);
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_live != null) Object.Destroy(_live);
            if (_idle != null) Object.Destroy(_idle);
            if (_rim != null) Object.Destroy(_rim);
            if (_display != null) Object.Destroy(_display);
        }
    }
}
