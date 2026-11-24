using UnityEngine;

namespace PortalGunQuest
{
    internal static class TransitMath
    {
        internal static Vector3 HeadAnchoredPoint(Matrix4x4 map, Vector3 head, Vector3 point, Quaternion uprightDelta)
        {
            return map.MultiplyPoint(head) + uprightDelta * (point - head);
        }

        internal static Quaternion FistRotation(Vector3 wrist, Vector3 middle, Vector3 index, Vector3 little)
        {
            return Quaternion.LookRotation(middle - wrist, index - little);
        }

        internal static Quaternion TravelHeading(Quaternion body, Vector3 headForward, Quaternion map, Vector3 entryNormal, Vector3 exitNormal)
        {
            Vector3 current = Vector3.ProjectOnPlane(headForward, Vector3.up);
            if (current.sqrMagnitude < 0.01f) current = body * Vector3.forward;
            Vector3 desired = Vector3.ProjectOnPlane(map * headForward, Vector3.up);
            if (Mathf.Abs(exitNormal.y) > 0.7f) desired = current;
            else if (Mathf.Abs(entryNormal.y) > 0.7f) desired = Vector3.ProjectOnPlane(exitNormal, Vector3.up);
            if (desired.sqrMagnitude < 0.01f) desired = current;
            return UprightRotation(Quaternion.AngleAxis(Vector3.SignedAngle(current, desired, Vector3.up), Vector3.up) * body, current);
        }

        internal static bool FitsCapsule(Vector3 center, Vector3 axis, float half, float radius, float width, float height, bool rectangular)
        {
            if (rectangular) return Mathf.Abs(center.x) + Mathf.Abs(axis.x) * half + radius <= width && Mathf.Abs(center.y) + Mathf.Abs(axis.y) * half + radius <= height;
            radius += 0.003f;
            for (int end = -1; end <= 1; end += 2)
            {
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI / 16f;
                    float x = center.x + axis.x * half * end + Mathf.Cos(angle) * radius;
                    float y = center.y + axis.y * half * end + Mathf.Sin(angle) * radius;
                    if (x * x / (width * width) + y * y / (height * height) > 1f) return false;
                }
            }
            return true;
        }

        internal static Quaternion UprightRotation(Quaternion rotation, Vector3 fallback)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(fallback, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        internal static bool UnsafePassage(float signedCenter, float support, float elapsed)
        {
            return float.IsNaN(signedCenter) || float.IsInfinity(signedCenter) || signedCenter < -support - 0.5f || elapsed > 8f;
        }

        internal static bool CrossesPlane(Vector3 from, Vector3 to, float width, float height, bool rectangular)
        {
            if (from.z <= 0f || to.z > 0f || width <= 0f || height <= 0f) return false;
            Vector3 p = Vector3.Lerp(from, to, from.z / (from.z - to.z));
            if (rectangular) return Mathf.Abs(p.x) <= width && Mathf.Abs(p.y) <= height;
            return p.x * p.x / (width * width) + p.y * p.y / (height * height) <= 1f;
        }

        internal static bool Finite(Vector3 v)
        {
            float m = v.sqrMagnitude;
            return !float.IsNaN(m) && !float.IsInfinity(m);
        }
    }
}
