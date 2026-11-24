using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppAlta.Networking.Scripts.Player;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using GameStream = Il2CppAlta.Serialization.Stream;
using Object = UnityEngine.Object;

namespace PortalGunQuest
{
    internal static class Plat
    {
        internal const bool Quest = true;

        internal static T Get<T>(Component c) where T : Component
        {
            if (c == null) return null;
            Component x = c.GetComponent(Il2CppType.Of<T>());
            return x == null ? null : x.TryCast<T>();
        }

        internal static T Get<T>(GameObject g) where T : Component
        {
            return g == null ? null : Get<T>(g.transform);
        }

        internal static T Add<T>(GameObject g) where T : Component
        {
            return g.AddComponent(Il2CppType.Of<T>()).TryCast<T>();
        }

        internal static List<T> All<T>(Component c, bool inactive) where T : Component
        {
            List<T> list = new List<T>();
            if (c == null) return list;
            foreach (Component x in c.GetComponentsInChildren(Il2CppType.Of<T>(), inactive))
            {
                T t = x == null ? null : x.TryCast<T>();
                if (t != null) list.Add(t);
            }
            return list;
        }

        internal static T Parent<T>(Component c) where T : Component
        {
            for (Transform t = c == null ? null : c.transform; t != null; t = t.parent)
            {
                T x = Get<T>(t);
                if (x != null) return x;
            }
            return null;
        }

        internal static long Key(Object o)
        {
            return o == null ? 0 : (long)o.Pointer;
        }

        internal static bool Same(Object a, Object b)
        {
            if (a == null || b == null) return false;
            return Key(a) == Key(b);
        }

        internal static GameObject Copy(GameObject g, Transform parent)
        {
            Object o = Object.Instantiate((Object)g, parent);
            return o == null ? null : o.TryCast<GameObject>();
        }

        internal static T CopyAsset<T>(T a) where T : Object
        {
            Object o = Object.Instantiate((Object)a);
            return o == null ? null : o.TryCast<T>();
        }

        internal static SerializeMethod Method(Action<IPlayer, GameStream> a)
        {
            return DelegateSupport.ConvertDelegate<SerializeMethod>(a);
        }

        internal static SerializeConnectionMethod ConnectionMethod(Action<Connection, GameStream> a)
        {
            return DelegateSupport.ConvertDelegate<SerializeConnectionMethod>(a);
        }

        internal static void Listen(NetworkEntity e, EntityMessageType type, SerializeMethod m)
        {
            e.MessageHandler.SetHandler(type, m);
        }

        internal static void SendToServer(NetworkEntity e, EntityMessageType type, SerializeMethod m)
        {
            INetworkEntity i = e.TryCast<INetworkEntity>();
            if (i == null) throw new InvalidCastException("the entity is not an INetworkEntity");
            InternalMessage.SendToServer(i, type, m, null);
        }

        internal static NetworkEntity ReadEntity(GameStream s, NetworkEntity e, NetworkEntity scene)
        {
            INetworkScene sc = scene == null ? null : scene.Scene;
            StreamEntityExtensions.SerializeNetworkEntity(s, ref e, sc);
            return e;
        }

        internal static Collider[] Colliders(List<Collider> list)
        {
            return list.ToArray();
        }

        internal static Il2CppReferenceArray<T> RefArray<T>(List<T> list) where T : Il2CppSystem.Object
        {
            Il2CppSystem.Array arr = Il2CppSystem.Array.CreateInstance(Il2CppType.Of<T>(), list.Count);
            Il2CppReferenceArray<T> r = new Il2CppReferenceArray<T>(arr.Pointer);
            for (int i = 0; i < list.Count; i++) r[i] = list[i];
            return r;
        }

        internal static Il2CppSystem.Collections.Generic.Dictionary<uint, NetworkPrefab> PrefabMap { get { return PrefabManager.prefabMap; } }
        internal static Il2CppSystem.Collections.Generic.Dictionary<uint, Item> Items { get { return HashedGeneralValue<Item>.items; } }

        internal static void ResetItemNames()
        {
            HashedGeneralValue<Item>.itemsByName = null;
        }

        internal static void SetPrefabHash(NetworkPrefab p, uint hash)
        {
            p.hash = (int)hash;
        }

        internal static void SetItem(Item item, uint hash, NetworkPrefab prefab)
        {
            item.Prefab = prefab;
            item.hash = (int)hash;
            item.isStackable = false;
            item.isStackableWhenCrafted = false;
            item.isSnappingToHand = true;
        }

        internal static void SetPickup(Pickup p, List<Collider> colliders, GrabPoint grip)
        {
            List<GrabPoint> points = new List<GrabPoint>();
            points.Add(grip);
            p.childrenColliders = RefArray(colliders);
            p.grabPoints = RefArray(points);
            p.grabPointsScale = 1f;
        }

        internal static GrabPoint ResetGrip(Pickup p, Vector3 position)
        {
            Il2CppReferenceArray<GrabPoint> points = p.grabPoints;
            GrabPoint g = points == null || points.Length == 0 ? null : points[0];
            if (g == null) throw new InvalidOperationException("the donor has no grab point to reuse");
            g.isDisabled = false;
            g.transform = null;
            g.isUsingLocal = true;
            g.position = position;
            g.rotationEuler = Vector3.zero;
            g.linear = Vector3.zero;
            g.rotationMode = RotationMode.Fixed;
            g.poleForward = Vector3.forward;
            g.offset = 0f;
            g.isSetupWithLeft = false;
            g.dockedRestriction = (PickupFromDockRestriction)0;
            g.nonDockedRestriction = (PickupFromDockRestriction)0;
            if (g.enabledFromDocks != null) g.enabledFromDocks.Clear();
            g.isIgnoringAngle = false;
            g.thumbPosition = (ThumbPosition)0;
            g.refuseMultipleHands = false;
            g.interactor = null;
            return g;
        }

        internal static void BindBehaviours(GameObject root, NetworkEntity entity)
        {
            List<MonoBehaviour> found = new List<MonoBehaviour>();
            foreach (Component c in root.GetComponents(Il2CppType.Of<MonoBehaviour>()))
            {
                MonoBehaviour m = c == null ? null : c.TryCast<MonoBehaviour>();
                if (m == null || m.TryCast<INetworkEntityBehaviour>() == null) continue;
                NetworkEntityBehaviour b = m.TryCast<NetworkEntityBehaviour>();
                if (b != null) b.entity = entity;
                found.Add(m);
            }
            entity.networkEntityBehaviours = RefArray(found);
            entity.hasNetworkBehaviours = found.Count > 0;
        }

        internal static void Link(NetworkPrefab p, NetworkEntity e)
        {
            p.entity = e;
            e.prefab = p;
        }

        internal static void CacheTransform(NetworkRigidbody body, Transform t)
        {
            body.cachedTransform = t;
        }

        internal static void ClearCollisionFlags(SmoothLocomotion motion)
        {
            motion.currentCollisionFlags = CollisionFlags.None;
        }

        static Il2CppReferenceArray<Collider> _overlap;

        internal static List<Collider> OverlapCapsule(Vector3 a, Vector3 b, float radius)
        {
            List<Collider> list = new List<Collider>();
            if (_overlap == null) _overlap = new Il2CppReferenceArray<Collider>(Il2CppSystem.Array.CreateInstance(Il2CppType.Of<Collider>(), 32).Pointer);
            int n = Physics.defaultPhysicsScene.OverlapCapsule(a, b, radius, _overlap, -1, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && i < _overlap.Length; i++) if (_overlap[i] != null) list.Add(_overlap[i]);
            return list;
        }

        internal static List<RaycastHit> RaycastAll(Vector3 origin, Vector3 direction, float distance)
        {
            List<RaycastHit> list = new List<RaycastHit>();
            foreach (RaycastHit h in Physics.RaycastAll(origin, direction, distance, -1, QueryTriggerInteraction.Ignore)) list.Add(h);
            list.Sort((x, y) => x.distance.CompareTo(y.distance));
            return list;
        }

        internal static Mesh NewMesh(string name, Vector3[] v, Vector3[] n, Vector2[] uv, List<int[]> subs)
        {
            Mesh m = new Mesh();
            m.name = name;
            if (v.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = new Il2CppStructArray<Vector3>(v);
            m.normals = new Il2CppStructArray<Vector3>(n);
            m.uv = new Il2CppStructArray<Vector2>(uv);
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(new Il2CppStructArray<int>(subs[i]), i);
            m.RecalculateBounds();
            return m;
        }

        internal static Texture2D NewTexture(string name, int w, int h, byte[] data)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            t.name = name;
            t.LoadRawTextureData(new Il2CppStructArray<byte>(data));
            t.Apply(false, true);
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }

        internal static void SetMaterials(Renderer r, List<Material> mats)
        {
            r.sharedMaterials = RefArray(mats);
        }

        internal static List<Object> Loaded<T>() where T : Object
        {
            List<Object> list = new List<Object>();
            foreach (Object o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>())) list.Add(o);
            return list;
        }

        internal static T As<T>(Object o) where T : Object
        {
            return o == null ? null : o.TryCast<T>();
        }
    }
}
