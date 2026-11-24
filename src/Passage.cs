using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Character;
using Il2CppAlta.Chunks;

namespace PortalGunQuest
{
    internal sealed class Passage
    {
        static readonly List<Passage> All = new List<Passage>();
        static Passage _active;
        static float _transitUntil;
        static Camera _nearCam;
        static float _nearWas;

        readonly Transform _t;
        readonly float _hw, _hh;
        readonly bool _rect;
        bool _linked, _crossed, _mustLeave, _hasPrevious;
        Matrix4x4 _exit;
        Vector3 _previousHead, _recoveryCenter, _savedFeet, _entryAtLease;
        Quaternion _savedRotation, _entryRotationAtLease;
        CharacterController _capsule;
        Collider _entrySurface, _exitSurface;
        readonly List<Collider> _leased = new List<Collider>();
        float _lastBlockedLog, _enteredAt, _allow = 0.18f;
        string _reason = "clear";

        internal Passage(Transform t, float hw, float hh, bool rect)
        {
            _t = t;
            _hw = hw;
            _hh = hh;
            _rect = rect;
            All.Add(this);
        }

        internal static bool IsPassing(SmoothLocomotion motion)
        {
            return _active != null && _active._capsule != null && motion != null && Plat.Same(_active._capsule, motion.CharacterController);
        }

        static SmoothLocomotionPlayerController Local
        {
            get
            {
                PlayerController pc = PlayerController.Current;
                return pc == null ? null : pc.TryCast<SmoothLocomotionPlayerController>();
            }
        }

        internal void SetExit(bool ready, Vector3 p, Vector3 n, Vector3 u)
        {
            if (_active == this && _linked && ready && (Vector3.Distance(_exit.MultiplyPoint(Vector3.zero), p) > 0.05f || Vector3.Angle(_exit.MultiplyVector(Vector3.forward), n) > 2f))
            {
                _reason = "destination moved during crossing";
                Release(true);
            }
            _linked = ready && n.sqrMagnitude > 0.5f && u.sqrMagnitude > 0.5f;
            if (_linked) _exit = Matrix4x4.TRS(p, Quaternion.LookRotation(n, u), Vector3.one);
            else Release(true);
        }

        internal static void Tick()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]._t == null) { All[i].Release(true); All.RemoveAt(i); }
            SmoothLocomotionPlayerController player = Local;
            NearPlane(player);
            if (player == null || !player.IsLocalPlayer || player.SmoothLocomotion == null) return;
            if (_active != null) { _active.Maintain(player); return; }
            foreach (Passage p in All.ToArray()) if (p.TryAcquire(player)) break;
        }

        internal static void LateTick()
        {
            if (_active != null) _active.Late();
        }

        static void NearPlane(SmoothLocomotionPlayerController player)
        {
            Camera c = Camera.main;
            bool want = _active != null && player != null && player.Head != null && c != null && Vector3.Distance(c.transform.position, player.Head.position) < 0.4f;
            if (want && _nearCam == null)
            {
                _nearCam = c;
                _nearWas = c.nearClipPlane;
                c.nearClipPlane = Mathf.Min(_nearWas, 0.005f);
            }
            else if (!want && _nearCam != null)
            {
                _nearCam.nearClipPlane = _nearWas;
                _nearCam = null;
            }
        }

        static void Capsule(CharacterController cc, out Vector3 center, out Vector3 axis, out float half, out float radius)
        {
            Transform ct = cc.transform;
            center = ct.TransformPoint(cc.center);
            radius = cc.radius * Mathf.Max(Mathf.Abs(ct.lossyScale.x), Mathf.Abs(ct.lossyScale.z));
            half = Mathf.Max(0f, cc.height * Mathf.Abs(ct.lossyScale.y) * 0.5f - radius);
            axis = ct.up;
        }

        bool TryAcquire(SmoothLocomotionPlayerController player)
        {
            if (!_linked || player.IsDowned || player.Head == null || Time.time < _transitUntil) return false;
            CharacterController cc = player.SmoothLocomotion.CharacterController;
            if (cc == null || !cc.enabled) return false;
            Vector3 center, axis;
            float half, radius;
            Capsule(cc, out center, out axis, out half, out radius);
            Vector3 local = _t.InverseTransformPoint(center);
            float support = radius + half * Mathf.Abs(Vector3.Dot(axis, _t.forward));
            if (_mustLeave)
            {
                Vector3 away = local - _recoveryCenter;
                if (away.z > 0.2f || away.x * away.x + away.y * away.y > 0.0625f) _mustLeave = false;
                else return false;
            }
            float approach = Mathf.Min(0.5f, player.SmoothLocomotion.velocity.magnitude * Time.deltaTime + 0.12f);
            if (local.z < -0.05f || local.z > support + approach || !Fits(center, axis, half, radius, _t.worldToLocalMatrix, 0f)) return false;
            if (Vector3.Dot(player.Head.position - _t.position, _t.forward) <= 0f) return false;
            LocationChunk chunk = LocationChunkHelper.GetBestContaining(_exit.MultiplyPoint(Vector3.forward * 0.2f));
            if (chunk == null || !chunk.IsFullyLoaded)
            {
                if (Time.time > _lastBlockedLog)
                {
                    _lastBlockedLog = Time.time + 5f;
                    Core.Log("passage waits: the other side is not loaded yet");
                }
                return false;
            }
            _entrySurface = FindSurface(_t.position, _t.forward);
            _exitSurface = FindSurface(_exit.MultiplyPoint(Vector3.zero), _exit.MultiplyVector(Vector3.forward));
            if (_entrySurface == null || _exitSurface == null) return false;
            _active = this;
            _capsule = cc;
            _crossed = false;
            _hasPrevious = true;
            _previousHead = player.Head.position;
            _enteredAt = Time.time;
            _reason = "clear";
            _entryAtLease = _t.position;
            _allow = Mathf.Max(0.18f, local.z - support + 0.1f);
            _entryRotationAtLease = _t.rotation;
            _savedFeet = player.PlayerFeetPosition + _t.forward * Mathf.Max(0f, support + cc.skinWidth + 0.15f - local.z);
            _savedRotation = TransitMath.UprightRotation(player.transform.rotation, player.Head.forward);
            Lease(_entrySurface);
            Lease(_exitSurface);
            Core.Log("passage enter: center " + local.z.ToString("0.00") + " m off the plane, support " + support.ToString("0.00") + ", approach " + approach.ToString("0.00") + ", radius " + radius.ToString("0.00") + ", half " + half.ToString("0.00"));
            return true;
        }

        Matrix4x4 Mapping { get { return _exit * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * _t.worldToLocalMatrix; } }

        static Collider FindSurface(Vector3 p, Vector3 n)
        {
            foreach (RaycastHit h in Plat.RaycastAll(p + n * 0.12f, -n, 0.3f))
            {
                if (Vector3.Dot(h.normal, n) <= 0.7f) continue;
                bool ours = Live.IsPortal(h.collider);
                if (!ours || h.collider.name == "PortalDoorBacking") return h.collider;
            }
            return null;
        }

        bool Fits(Vector3 center, Vector3 axis, float half, float radius, Matrix4x4 toPlane, float margin)
        {
            Vector3 c = toPlane.MultiplyPoint(center), a = toPlane.MultiplyVector(axis);
            return TransitMath.FitsCapsule(c, a, half, radius, _hw + margin, _hh + margin, _rect);
        }

        void Lease(Collider c)
        {
            if (_leased.Contains(c)) return;
            _leased.Add(c);
            Physics.IgnoreCollision(_capsule, c, true);
        }

        void Late()
        {
            SmoothLocomotionPlayerController player = Local;
            if (player == null || player.Head == null) { Release(false); return; }
            if (_t == null) { _reason = "the portal went away"; Release(true); return; }
            Maintain(player);
            if (_active != this) return;
            Vector3 head = player.Head.position;
            Matrix4x4 plane = _crossed ? _exit.inverse : _t.worldToLocalMatrix;
            Vector3 old = plane.MultiplyPoint(_previousHead), now = plane.MultiplyPoint(head);
            _previousHead = head;
            if (_hasPrevious && TransitMath.CrossesPlane(old, now, _hw, _hh, _rect)) Cross(player, head);
            _hasPrevious = true;
        }

        void Cross(SmoothLocomotionPlayerController player, Vector3 head)
        {
            Matrix4x4 map = _crossed ? Mapping.inverse : Mapping;
            Quaternion rotation = _exit.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(_t.rotation);
            if (_crossed) rotation = Quaternion.Inverse(rotation);
            SmoothLocomotion motion = player.SmoothLocomotion;
            Vector3 velocity = motion.CurrentMovingVelocity;
            if (motion.velocity.sqrMagnitude > velocity.sqrMagnitude) velocity = motion.velocity;
            Matrix4x4 target = _crossed ? _t.localToWorldMatrix : _exit;
            Vector3 sourceNormal = _crossed ? _exit.MultiplyVector(Vector3.forward) : _t.forward;
            Quaternion upright = TransitMath.TravelHeading(player.transform.rotation, player.Head.forward, rotation, sourceNormal, target.MultiplyVector(Vector3.forward));
            Quaternion yaw = upright * Quaternion.Inverse(player.transform.rotation);
            Vector3 targetHead = map.MultiplyPoint(head);
            Vector3 center, axis;
            float half, radius;
            Capsule(_capsule, out center, out axis, out half, out radius);
            Vector3 targetCenter = TransitMath.HeadAnchoredPoint(map, head, center, yaw);
            Vector3 targetBody = TransitMath.HeadAnchoredPoint(map, head, _capsule.transform.position, yaw);
            string blocker = Obstructed(targetCenter, half, radius, player);
            if (blocker != null)
            {
                float step = Mathf.Clamp(_capsule.stepOffset, 0f, 0.35f);
                if (step > 0f && Obstructed(targetCenter + Vector3.up * step, half, radius, player) == null)
                {
                    targetHead += Vector3.up * step;
                    targetCenter += Vector3.up * step;
                    targetBody += Vector3.up * step;
                    blocker = null;
                }
            }
            if (blocker != null) { _reason = "the exit is blocked by " + blocker; Release(true); return; }
            try
            {
                player.LocomotionController.RotateTo(upright, LocomotionStyle.Instant, 0f);
                player.LocomotionController.MoveTo(targetHead, TeleportMode.Head, LocomotionFunction.Reposition);
                _capsule.transform.position = targetBody;
                Physics.SyncTransforms();
            }
            catch (Exception e)
            {
                _reason = "the move failed";
                Core.Error("passage move failed: " + e.Message);
                Release(true);
                return;
            }
            Capsule(_capsule, out center, out axis, out half, out radius);
            blocker = Obstructed(center, half, radius, player);
            if (blocker != null) { _reason = "the body landed in " + blocker; Release(true); return; }
            float error = Vector3.Distance(player.Head.position, targetHead);
            if (error > 0.03f) { _reason = "head off by " + error.ToString("0.000") + " m"; Release(true); return; }
            _crossed = !_crossed;
            motion.velocity = rotation * velocity;
            Plat.ClearCollisionFlags(motion);
            player.LocomotionController.IsGrounded = false;
            motion.TreatAsGrounded();
            _enteredAt = Time.time;
            _previousHead = player.Head.position;
            Core.Log("passage cross " + (_crossed ? "through" : "back") + ", head error " + error.ToString("0.000"));
        }

        string Obstructed(Vector3 center, float half, float radius, PlayerController player)
        {
            foreach (Collider c in Plat.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half, radius))
            {
                if (Plat.Same(c, _capsule) || Plat.Same(c, _entrySurface) || Plat.Same(c, _exitSurface)) continue;
                if (c.transform.IsChildOf(player.transform) || Live.HeldBy(c, player) || Loose(c)) continue;
                return c.name + " (layer " + c.gameObject.layer + ")";
            }
            return null;
        }

        static bool Loose(Collider c)
        {
            Pickup p = Plat.Parent<Pickup>(c);
            if (p == null || p.IsDocked) return false;
            Rigidbody body = Plat.Get<Rigidbody>(p);
            return body != null && !body.isKinematic;
        }

        void Maintain(SmoothLocomotionPlayerController player)
        {
            if (_t == null) { _reason = "the portal went away"; Release(true); return; }
            if (!_linked || _capsule == null || _entrySurface == null || _exitSurface == null || !_entrySurface.enabled || !_exitSurface.enabled) { _reason = "a surface went away"; Release(true); return; }
            if (_rect && (Vector3.Distance(_entryAtLease, _t.position) > 0.05f || Quaternion.Angle(_entryRotationAtLease, _t.rotation) > 2f)) { _reason = "the door moved"; Release(true); return; }
            foreach (Collider c in _leased) if (c != null) Physics.IgnoreCollision(_capsule, c, true);
            Matrix4x4 plane = _crossed ? _exit.inverse : _t.worldToLocalMatrix;
            Vector3 center, axis;
            float half, radius;
            Capsule(_capsule, out center, out axis, out half, out radius);
            Vector3 c0 = plane.MultiplyPoint(center), a0 = plane.MultiplyVector(axis);
            float support = radius + half * Mathf.Abs(a0.z);
            if (TransitMath.UnsafePassage(c0.z, support, Time.time - _enteredAt)) { _reason = "time or depth guard"; Release(true); return; }
            if (c0.z > support + (_crossed ? -0.03f : _allow))
            {
                _reason = (_crossed ? "cleared the exit" : "stepped back out") + " (center " + c0.z.ToString("0.00") + ", support " + support.ToString("0.00") + ")";
                _transitUntil = Time.time + 0.12f;
                Release(false);
                return;
            }
            if (!_crossed && !Fits(center, axis, half, radius, plane, 0.1f)) { _reason = "left the opening"; Release(true); return; }
            if (_crossed)
            {
                Vector3 h = plane.MultiplyPoint(player.Head.position);
                float w = _hw + 0.1f, hh = _hh + 0.1f;
                bool outside = _rect ? Mathf.Abs(h.x) > w || Mathf.Abs(h.y) > hh : h.x * h.x / (w * w) + h.y * h.y / (hh * hh) > 1f;
                if (outside) { _reason = "head left the exit before the body"; Release(true); }
            }
        }

        void Release(bool recover)
        {
            if (_active != this) return;
            SmoothLocomotionPlayerController player = Local;
            try
            {
                if (recover && player != null && _capsule != null)
                {
                    _mustLeave = true;
                    _transitUntil = Time.time + 0.5f;
                    player.LocomotionController.RotateTo(_savedRotation, LocomotionStyle.Instant, 0f);
                    player.LocomotionController.MoveTo(_savedFeet, TeleportMode.Feet, LocomotionFunction.Reposition);
                    player.SmoothLocomotion.velocity = Vector3.zero;
                    if (_t != null) _recoveryCenter = _t.InverseTransformPoint(_capsule.transform.TransformPoint(_capsule.center));
                    else _mustLeave = false;
                }
            }
            catch (Exception e) { Core.Error("passage recovery failed: " + e.Message); }
            finally
            {
                foreach (Collider c in _leased) if (_capsule != null && c != null) Physics.IgnoreCollision(_capsule, c, false);
                _leased.Clear();
                _capsule = null;
                _active = null;
                _hasPrevious = false;
                Core.Log("passage end, recovered " + recover + ", " + _reason + ", " + (Time.time - _enteredAt).ToString("0.00") + " s");
            }
        }

        internal void Destroy()
        {
            Release(true);
            All.Remove(this);
        }
    }
}
