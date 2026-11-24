using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppAlta.Networking;
using GameStream = Il2CppAlta.Serialization.Stream;
using StringEncoding = Il2CppAlta.Serialization.Stream.StringEncoding;

namespace PortalGunQuest
{
    internal static class Sync
    {
        internal const int Message = 18;
        static SerializeConnectionMethod _handler;
        static long _listening;
        static string _pending;
        static bool _hasPending;
        static readonly object _lock = new object();

        internal static void Prepare()
        {
            _handler = Plat.ConnectionMethod(Receive);
        }

        internal static void Listen(Connection c)
        {
            if (c == null) return;
            _listening = (long)c.Pointer;
            c.SetHandler((MessageType)Message, _handler);
            Core.Log("new server connection: waiting for the server's item numbers");
        }

        static void Receive(Connection c, GameStream s)
        {
            string json = null;
            s.SerializeString(ref json, (StringEncoding)0);
            lock (_lock)
            {
                _pending = json;
                _hasPending = true;
            }
        }

        internal static void Tick()
        {
            string json;
            lock (_lock)
            {
                if (!_hasPending) return;
                json = _pending;
                _pending = null;
                _hasPending = false;
            }
            Read(json);
        }

        static void Read(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Core.Error("the server sent an empty item list");
                return;
            }
            uint[] hashes = new uint[4];
            int found = 0;
            for (int i = 0; i < 4; i++)
            {
                Match m = Regex.Match(json, "\"" + Registry.Ids[i] + " \\(PortalGun\\)\"\\s*:\\s*(\\d+)");
                if (m.Success && uint.TryParse(m.Groups[1].Value, out hashes[i]) && hashes[i] != 0) found++;
            }
            List<string> others = new List<string>();
            foreach (Match m in Regex.Matches(json, "\"ModId\"\\s*:\\s*\"([^\"]+)\""))
            {
                string id = m.Groups[1].Value;
                if (id != "PortalGun.caleb") others.Add(id);
            }
            if (others.Count > 0) Core.Log("the server also runs SyncLib mods this headset does not have: " + string.Join(", ", others.ToArray()) + "; their items will not show");
            if (found == 0)
            {
                Core.Log("this server does not run Portal Gun; staying idle");
                if (Registry.Done) Registry.Unregister();
                return;
            }
            if (found < 4)
            {
                Core.Error("the server lists only " + found + " of the 4 portal items; staying idle");
                return;
            }
            if (Registry.Same(hashes))
            {
                Core.Log("portal items already registered with these numbers");
                return;
            }
            Live.Clear();
            Registry.Register(hashes);
        }
    }
}
