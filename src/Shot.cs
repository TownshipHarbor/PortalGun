using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PortalGunQuest
{
    internal sealed class Shot
    {
        static readonly List<Shot> All = new List<Shot>();
        static Shader _shader;
        static readonly Material[] _base = new Material[2];

        Vector3 _start, _end;
        float _born;
        GameObject _go;
        LineRenderer _trail;
        Transform _pulse;
        Material _mat;
        Color _color;

        internal static void Play(Vector3 a, Vector3 b, bool blue)
        {
            if (_shader == null) _shader = Pack.Find("Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive", "Unlit/Color");
            if (_shader == null) return;
            Shot s = new Shot { _start = a, _end = b, _born = Time.time };
            s._color = blue ? new Color(0.08f, 0.6f, 1f, 1f) : new Color(1f, 0.4f, 0.06f, 1f);
            s._mat = new Material(_shader);
            s._mat.name = "PortalGun shot";
            s.Tint(s._color);
            s._go = new GameObject("Portal shot");
            s._trail = Plat.Add<LineRenderer>(s._go);
            s._trail.sharedMaterial = s._mat;
            s._trail.useWorldSpace = true;
            s._trail.positionCount = 2;
            s._trail.startWidth = 0.035f;
            s._trail.endWidth = 0.015f;
            s._trail.startColor = s._color;
            s._trail.endColor = s._color;
            s._trail.SetPosition(0, a);
            s._trail.SetPosition(1, a);
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Portal energy pulse";
            Collider c = Plat.Get<Collider>(ball);
            if (c != null) Object.Destroy(c);
            ball.transform.SetParent(s._go.transform, false);
            Renderer r = Plat.Get<Renderer>(ball);
            r.sharedMaterial = s._mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s._pulse = ball.transform;
            s._pulse.position = a;
            s._pulse.localScale = Vector3.one * 0.09f;
            All.Add(s);
        }

        void Tint(Color c)
        {
            Pack.SetColor(_mat, "_TintColor", c * 0.5f);
            Pack.SetColor(_mat, "_Color", c);
        }

        internal static void Tick()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                Shot s = All[i];
                float t = (Time.time - s._born) / 0.18f;
                if (t > 1.95f || s._go == null)
                {
                    if (s._go != null) Object.Destroy(s._go);
                    Object.Destroy(s._mat);
                    All.RemoveAt(i);
                    continue;
                }
                float head = Mathf.Clamp01(t), tail = Mathf.Clamp01(t - 0.35f);
                s._trail.SetPosition(0, Vector3.Lerp(s._start, s._end, tail));
                s._trail.SetPosition(1, Vector3.Lerp(s._start, s._end, head));
                s._pulse.position = Vector3.Lerp(s._start, s._end, head);
                s._pulse.localScale = Vector3.one * (t < 1f ? 0.09f : 0.09f + (t - 1f) * 0.25f);
                Color c = s._color;
                c.a = 1f - Mathf.Clamp01((t - 1f) / 0.8f);
                s.Tint(c * c.a);
            }
        }
    }
}
