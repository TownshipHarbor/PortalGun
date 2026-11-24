using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2Cpp;
using Il2CppAlta.Networking;

[assembly: MelonInfo(typeof(PortalGunQuest.Core), PortalGunQuest.Core.Name, PortalGunQuest.Core.Version, "caleb, ported by Shadow")]
[assembly: MelonGame("Alta", "Att_Quest")]

namespace PortalGunQuest
{
    public sealed class Core : MelonMod
    {
        internal const string Name = "PortalGunQuest";
        internal const string Version = "0.1.1";
        static MelonLogger.Instance _log;
        static bool _running, _reset;
        readonly string[] _last = new string[3];

        internal static void Log(string m) { if (_log != null) _log.Msg(m); }
        internal static void Error(string m) { if (_log != null) _log.Error(m); }

        public override void OnInitializeMelon()
        {
            _log = LoggerInstance;
            try
            {
                Pack.Read();
                Sync.Prepare();
                HarmonyLib.Harmony h = new HarmonyLib.Harmony("portalgun.quest");
                int patched = 0;
                patched += Patch(h, typeof(JoinServerPipeline), "ConnectionApprovedByServer", null, "Approved");
                patched += Patch(h, typeof(SpawnManager), "SpawnLocal", null, "Spawned");
                patched += Patch(h, typeof(SmoothLocomotion), "RepositionCharacter", "Reposition", null, typeof(float));
                patched += Patch(h, typeof(Pickup), "EnsureBestGrabFound", "BestGrab", null);
                if (patched < 2)
                {
                    Error("the join or spawn hook is missing; Portal Gun stays off.");
                    return;
                }
                _running = true;
                Log(Version + " started (" + patched + "/4 hooks), waiting for a server that runs Portal Gun");
            }
            catch (Exception e)
            {
                Error("failed to start: " + e);
            }
        }

        int Patch(HarmonyLib.Harmony h, Type type, string method, string prefix, string postfix, params Type[] args)
        {
            try
            {
                MethodInfo target = args.Length > 0 ? AccessTools.Method(type, method, args) : AccessTools.Method(type, method);
                if (target == null) throw new MissingMethodException(type.Name + "." + method);
                h.Patch(target, prefix == null ? null : new HarmonyMethod(typeof(Core), prefix), postfix == null ? null : new HarmonyMethod(typeof(Core), postfix));
                return 1;
            }
            catch (Exception e)
            {
                Error("hook " + type.Name + "." + method + " failed: " + e.Message);
                return 0;
            }
        }

        static void Approved(Connection connection)
        {
            try
            {
                _reset = true;
                Sync.Listen(connection);
            }
            catch (Exception e) { Error("new connection: " + e); }
        }

        static void Spawned(uint prefabHash, NetworkEntity __result)
        {
            try { Live.Spawned(prefabHash, __result); }
            catch (Exception e) { Error("spawn: " + e.Message); }
        }

        static bool Reposition(SmoothLocomotion __instance)
        {
            try { return !Passage.IsPassing(__instance); }
            catch (Exception) { return true; }
        }

        static bool BestGrab(Pickup __instance, Interactor interactor)
        {
            return Grip.Before(__instance, interactor);
        }

        public override void OnUpdate()
        {
            if (!_running) return;
            try
            {
                if (_reset)
                {
                    _reset = false;
                    Live.Clear();
                    Registry.Unregister();
                }
                Sync.Tick();
            }
            catch (Exception e) { Report(0, e); }
            try { Live.Tick(); }
            catch (Exception e) { Report(1, e); }
        }

        public override void OnLateUpdate()
        {
            if (!_running) return;
            try { Live.LateTick(); }
            catch (Exception e) { Report(2, e); }
        }

        void Report(int slot, Exception e)
        {
            string m = e.GetType().Name + ": " + e.Message;
            if (m == _last[slot]) return;
            _last[slot] = m;
            Error("tick " + slot + " threw: " + e);
        }
    }
}
