using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Networking;

namespace PortalGunQuest
{
    internal static class Live
    {
        static readonly List<KeyValuePair<NetworkEntity, Kind>> Pending = new List<KeyValuePair<NetworkEntity, Kind>>();
        static readonly Dictionary<long, Gun> Guns = new Dictionary<long, Gun>();
        static readonly Dictionary<long, Gate> Gates = new Dictionary<long, Gate>();
        static readonly HashSet<long> Roots = new HashSet<long>();
        internal static int Seen;

        internal static void Spawned(uint hash, NetworkEntity e)
        {
            if (e == null) return;
            Kind k = Registry.KindOf(hash);
            if (k == Kind.None) return;
            Seen++;
            Pending.Add(new KeyValuePair<NetworkEntity, Kind>(e, k));
        }

        internal static void Tick()
        {
            if (Pending.Count > 0)
            {
                KeyValuePair<NetworkEntity, Kind>[] list = Pending.ToArray();
                Pending.Clear();
                foreach (KeyValuePair<NetworkEntity, Kind> p in list) Attach(p.Key, p.Value);
            }
            List<long> dead = new List<long>();
            foreach (KeyValuePair<long, Gun> g in Guns) if (!g.Value.Tick()) dead.Add(g.Key);
            foreach (long k in dead) { Guns[k].Destroy(); Guns.Remove(k); Roots.Remove(k); }
            dead.Clear();
            foreach (KeyValuePair<long, Gate> g in Gates) if (!g.Value.Tick()) dead.Add(g.Key);
            foreach (long k in dead) { Core.Log(Gates[k].Name + " gone"); Gates[k].Destroy(); Gates.Remove(k); Roots.Remove(k); }
            Passage.Tick();
            Shot.Tick();
        }

        internal static void LateTick()
        {
            Passage.LateTick();
            View.RenderAll();
        }

        static void Attach(NetworkEntity e, Kind k)
        {
            if (e == null) return;
            long key = Plat.Key(e);
            try
            {
                if (k == Kind.Gun)
                {
                    if (!Guns.ContainsKey(key)) Guns[key] = new Gun(e);
                }
                else if (!Gates.ContainsKey(key))
                {
                    Gates[key] = new Gate(e, k == Kind.Gate ? -1 : k == Kind.DoorA ? 0 : 1);
                }
                Roots.Add(key);
            }
            catch (Exception ex)
            {
                Core.Error(k + " " + e.Identifier + " could not start: " + ex);
            }
        }

        internal static bool IsPortal(Collider c)
        {
            for (Transform t = c == null ? null : c.transform; t != null; t = t.parent)
            {
                NetworkEntity e = Plat.Get<NetworkEntity>(t);
                if (e != null) return Roots.Contains(Plat.Key(e)) && Gates.ContainsKey(Plat.Key(e));
            }
            return false;
        }

        internal static bool IsGun(Pickup p)
        {
            NetworkEntity e = Plat.Get<NetworkEntity>(p);
            return e != null && Guns.ContainsKey(Plat.Key(e));
        }

        internal static bool HeldBy(Collider c, PlayerController player)
        {
            Pickup p = Plat.Parent<Pickup>(c);
            if (p == null) return false;
            var hands = p.Interactors;
            if (hands == null) return false;
            for (int i = 0; i < hands.Count; i++)
            {
                Interactor x = hands[i];
                if (x != null && x.Controller != null && x.Controller.PlayerController != null && Plat.Same(x.Controller.PlayerController, player)) return true;
            }
            return false;
        }

        internal static int GunCount { get { return Guns.Count; } }
        internal static int GateCount { get { return Gates.Count; } }

        internal static void Clear()
        {
            Pending.Clear();
            foreach (Gun g in Guns.Values) g.Destroy();
            foreach (Gate g in Gates.Values) g.Destroy();
            Guns.Clear();
            Gates.Clear();
            Roots.Clear();
        }
    }
}
