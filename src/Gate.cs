using System;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Networking;
using Il2CppAlta.Networking.Scripts.Player;
using GameStream = Il2CppAlta.Serialization.Stream;

namespace PortalGunQuest
{
    internal sealed class Gate
    {
        readonly NetworkEntity _entity;
        readonly int _door;
        readonly SerializeMethod _state, _transit;
        readonly object _lock = new object();
        bool _dirty, _placed, _linked;
        int _color;
        Vector3 _p, _n, _u, _ep, _en, _eu;
        bool _moveDirty;
        NetworkEntity _moveEntity;
        Vector3 _moveP, _moveV, _moveW;
        Quaternion _moveQ;
        View _view;
        bool _wasLinked;
        Passage _passage;
        internal static int States, Transits;

        bool IsDoor { get { return _door >= 0; } }
        float HalfWidth { get { return IsDoor ? 0.8f : 0.72f; } }
        float HalfHeight { get { return IsDoor ? 1.2f : 1.08f; } }

        internal Gate(NetworkEntity e, int door)
        {
            _entity = e;
            _door = door;
            _state = Plat.Method(State);
            _transit = Plat.Method(Transit);
            Plat.Listen(e, EntityMessageType.SyncDebugCommandFull, _state);
            Plat.Listen(e, EntityMessageType.SyncDebugCommand, _transit);
            Name = (IsDoor ? (door == 0 ? "door A " : "door B ") : "gate ") + e.Identifier;
            Core.Log(Name + " ready");
        }

        internal readonly string Name;

        void State(IPlayer sender, GameStream s)
        {
            Vector3 p = Vector3.zero, n = Vector3.zero, u = Vector3.zero, flags = Vector3.zero, ep = Vector3.zero, en = Vector3.zero, eu = Vector3.zero;
            s.SerializeVector3(ref p);
            s.SerializeVector3(ref n);
            s.SerializeVector3(ref u);
            s.SerializeVector3(ref flags);
            s.SerializeVector3(ref ep);
            s.SerializeVector3(ref en);
            s.SerializeVector3(ref eu);
            if (!s.IsReading) return;
            if (!TransitMath.Finite(p) || n.sqrMagnitude < 0.5f || u.sqrMagnitude < 0.5f) return;
            lock (_lock)
            {
                _p = p; _n = n; _u = u;
                _placed = flags.x == 1f;
                _color = flags.y == 1f ? 1 : 0;
                _linked = flags.z == 1f;
                _ep = ep; _en = en; _eu = eu;
                _dirty = true;
            }
        }

        void Transit(IPlayer sender, GameStream s)
        {
            NetworkEntity e = null;
            Vector3 p = Vector3.zero, v = Vector3.zero, w = Vector3.zero;
            Quaternion q = Quaternion.identity;
            e = Plat.ReadEntity(s, e, _entity);
            s.SerializeVector3(ref p);
            s.SerializeQuaternionCompressed(ref q, 10);
            s.SerializeVector3(ref v);
            s.SerializeVector3(ref w);
            if (!s.IsReading || e == null || !TransitMath.Finite(p) || !TransitMath.Finite(v)) return;
            lock (_lock)
            {
                _moveEntity = e;
                _moveP = p; _moveQ = q; _moveV = v; _moveW = w;
                _moveDirty = true;
            }
        }

        internal bool Tick()
        {
            if (_entity == null) return false;
            if (_entity.IsBeingDestroyed) return false;
            bool dirty, moved;
            lock (_lock)
            {
                dirty = _dirty;
                _dirty = false;
                moved = _moveDirty;
                _moveDirty = false;
            }
            if (dirty) Apply();
            if (moved) Move();
            return true;
        }

        void Apply()
        {
            States++;
            Transform t = _entity.transform;
            t.SetPositionAndRotation(_p, Quaternion.LookRotation(_n, _u));
            if (!_placed) return;
            if (_view == null)
            {
                _view = new View(t, HalfWidth, HalfHeight, IsDoor);
                _passage = new Passage(t, HalfWidth, HalfHeight, IsDoor);
                Core.Log((IsDoor ? "door " : "gate ") + _entity.Identifier + " shown, " + (_color == 0 ? "blue" : "orange") + (_linked ? ", linked" : ", waiting for its pair"));
            }
            else if (_linked != _wasLinked)
            {
                Core.Log((IsDoor ? "door " : "gate ") + _entity.Identifier + (_linked ? " linked" : " unlinked"));
            }
            _wasLinked = _linked;
            Color c = _color == 0 ? new Color(0.05f, 0.55f, 1f) : new Color(1f, 0.38f, 0.03f);
            _view.SetColor(c, _linked);
            _passage.SetExit(_linked, _ep, _en, _eu);
            _view.SetExit(_linked, _ep, _en, _eu);
        }

        void Move()
        {
            NetworkEntity e = _moveEntity;
            _moveEntity = null;
            if (e == null) return;
            Pickup pickup = Plat.Get<Pickup>(e);
            Rigidbody body = Plat.Get<Rigidbody>(e);
            if (pickup == null || body == null || pickup.IsDocked || pickup.Interactors.Count != 0) return;
            body.position = _moveP;
            body.rotation = _moveQ;
            e.transform.SetPositionAndRotation(_moveP, _moveQ);
            if (!body.isKinematic)
            {
                body.velocity = _moveV;
                body.angularVelocity = _moveW;
                body.WakeUp();
            }
            NetworkRigidbody net = Plat.Get<NetworkRigidbody>(e);
            if (net != null)
            {
                net.SetActive(true);
                net.LerpAuthorityFromClient(-0.25f, 1f);
            }
            Transits++;
            Core.Log("item " + e.Identifier + " came through a portal");
        }

        internal void Destroy()
        {
            if (_passage != null) _passage.Destroy();
            if (_view != null) _view.Destroy();
            _passage = null;
            _view = null;
        }
    }
}
