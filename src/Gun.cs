using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Il2Cpp;
using Il2CppAlta.Networking;
using Il2CppAlta.Networking.Scripts.Player;
using GameStream = Il2CppAlta.Serialization.Stream;

namespace PortalGunQuest
{
    internal sealed class Gun
    {
        readonly NetworkEntity _entity;
        readonly Pickup _pickup;
        readonly List<Renderer> _donor = new List<Renderer>();
        readonly SerializeMethod _effect, _request;
        Interactor _lastHolder;
        bool _pressed = true;
        float _nextShot;
        readonly object _lock = new object();
        readonly List<Vector3[]> _effects = new List<Vector3[]>();
        internal static int Shots, Effects;

        internal Gun(NetworkEntity e)
        {
            _entity = e;
            _pickup = Plat.Get<Pickup>(e);
            foreach (Renderer r in Plat.All<Renderer>(e.transform, true))
            {
                if (r.transform.parent != null && r.transform.parent.name == "PortalGunVisual") continue;
                if (r.name == "PortalGunVisual") continue;
                _donor.Add(r);
            }
            Hide();
            _effect = Plat.Method(Effect);
            _request = Plat.Method(Request);
            Plat.Listen(e, EntityMessageType.SyncDebugCommandFull, _effect);
            Core.Log("gun " + e.Identifier + " ready, " + _donor.Count + " donor renderers hidden");
        }

        void Hide()
        {
            foreach (Renderer r in _donor) if (r != null && r.enabled) r.enabled = false;
        }

        void Effect(IPlayer sender, GameStream s)
        {
            Vector3 a = Vector3.zero, b = Vector3.zero, c = Vector3.zero;
            s.SerializeVector3(ref a);
            s.SerializeVector3(ref b);
            s.SerializeVector3(ref c);
            if (!s.IsReading || !TransitMath.Finite(b)) return;
            lock (_lock) _effects.Add(new[] { a, b, c });
        }

        void Request(IPlayer sender, GameStream s)
        {
            Vector3 one = Vector3.one;
            s.SerializeVector3(ref one);
        }

        internal bool Tick()
        {
            if (_entity == null || !_entity.IsInitialized) return _entity != null && !_entity.IsBeingDestroyed;
            Hide();
            lock (_lock)
            {
                foreach (Vector3[] fx in _effects)
                {
                    Effects++;
                    Core.Log("shot effect " + (fx[2].x == 0f ? "blue" : "orange") + " to " + fx[1]);
                    Shot.Play(fx[0], fx[1], fx[2].x == 0f);
                }
                _effects.Clear();
            }
            if (_pickup == null) return true;
            Interactor holder = Holder();
            if (Plat.Key(holder) != Plat.Key(_lastHolder))
            {
                _lastHolder = holder;
                _pressed = true;
                Core.Log("gun " + _entity.Identifier + " held: " + (holder == null ? "none" : holder.Input == null ? "?" : holder.Input.HandIndex.ToString()));
            }
            if (holder == null) { _pressed = true; return true; }
            bool down = holder.Input != null && Trigger(holder.Input);
            if (down && !_pressed && Time.time >= _nextShot)
            {
                Plat.SendToServer(_entity, EntityMessageType.SyncDebugCommand, _request);
                _nextShot = Time.time + 0.35f;
                try { holder.Input.TriggerHaptic(0.3f, 0.08f, 80f); }
                catch (Exception) { }
                Shots++;
                Core.Log("gun " + _entity.Identifier + " trigger");
            }
            _pressed = down;
            return true;
        }

        Interactor Holder()
        {
            var hands = _pickup.Interactors;
            if (hands == null) return null;
            for (int i = 0; i < hands.Count; i++)
            {
                Interactor x = hands[i];
                if (x != null && x.Controller != null && x.Controller.PlayerController != null && x.Controller.PlayerController.IsLocalPlayer) return x;
            }
            return null;
        }

        static bool Trigger(PlayerInput input)
        {
            ControllerInput raw = input.RawInput;
            if (raw != null && (raw.SecondaryAxis > 0.75f || (raw.GrabAssist != null && raw.GrabAssist.State))) return true;
            try
            {
                InputDevice d = InputDevices.GetDeviceAtXRNode(input.HandIndex == HandIndex.Right ? XRNode.RightHand : XRNode.LeftHand);
                float axis;
                if (d.isValid && d.TryGetFeatureValue(CommonUsages.trigger, out axis) && axis > 0.75f) return true;
            }
            catch (Exception) { }
            return false;
        }

        internal void Destroy()
        {
        }
    }
}
