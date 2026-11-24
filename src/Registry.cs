using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Object = UnityEngine.Object;

namespace PortalGunQuest
{
    internal enum Kind { None, Gun, Gate, DoorA, DoorB }

    internal static class Registry
    {
        const uint Donor = 19180;
        const string DonorName = "Handle Medium Straight";
        internal static readonly string[] Ids = { "PortalGun", "PortalGunGate", "PortalDoorA", "PortalDoorB" };
        internal static readonly uint[] Hashes = new uint[4];
        static readonly NetworkPrefab[] Prefabs = new NetworkPrefab[4];
        static Item _item;
        static GameObject _holder;
        internal static bool Done;
        internal static Vector3 GunBox;

        internal static Kind KindOf(uint hash)
        {
            if (!Done || hash == 0) return Kind.None;
            for (int i = 0; i < 4; i++) if (Hashes[i] == hash && Prefabs[i] != null) return (Kind)(i + 1);
            return Kind.None;
        }

        internal static bool Same(uint[] hashes)
        {
            if (!Done) return false;
            for (int i = 0; i < 4; i++) if (Hashes[i] != hashes[i]) return false;
            return true;
        }

        internal static bool Register(uint[] hashes)
        {
            if (Done) Unregister();
            DateTime start = DateTime.UtcNow;
            try
            {
                Pack.Read();
                PrefabManager.PrepareSpawnSetups();
                HashedGeneralValue<Item>.CheckItems();
                var map = Plat.PrefabMap;
                var items = Plat.Items;
                if (map == null || items == null || items.Count == 0)
                {
                    Core.Error("the prefab map or the item list is not loaded; nothing registered.");
                    return false;
                }
                for (int i = 0; i < 4; i++)
                {
                    if (map.ContainsKey(hashes[i]))
                    {
                        Core.Error(Ids[i] + " number " + hashes[i] + " is already a prefab here; nothing registered.");
                        return false;
                    }
                }
                if (items.ContainsKey(hashes[0]))
                {
                    Core.Error("PortalGun number " + hashes[0] + " is already an item here; nothing registered.");
                    return false;
                }
                NetworkPrefab donor = PrefabManager.GetPrefab(Donor);
                if (donor == null || donor.name != DonorName)
                {
                    Core.Error("the gun's donor " + Donor + " is not " + DonorName + "; nothing registered.");
                    return false;
                }
                _holder = new GameObject("PortalGun templates");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
                Array.Copy(hashes, Hashes, 4);
                Prefabs[0] = Gun(donor, hashes[0]);
                Prefabs[1] = Gate(hashes[1]);
                Prefabs[2] = MakeDoor(0, hashes[2]);
                Prefabs[3] = MakeDoor(1, hashes[3]);
                for (int i = 0; i < 4; i++)
                {
                    map.Add(hashes[i], Prefabs[i]);
                    NetworkPrefab back = PrefabManager.GetPrefab(hashes[i]);
                    if (back == null || !Plat.Same(back, Prefabs[i])) throw new InvalidOperationException("the prefab map did not take " + Ids[i]);
                }
                items.Add(hashes[0], _item);
                Plat.ResetItemNames();
                Done = true;
                Core.Log("registered gun " + hashes[0] + ", gate " + hashes[1] + ", doors " + hashes[2] + " and " + hashes[3] + " in " + (int)(DateTime.UtcNow - start).TotalMilliseconds + " ms");
                return true;
            }
            catch (Exception e)
            {
                Core.Error("registration failed: " + e);
                Unregister();
                return false;
            }
        }

        static NetworkPrefab Gun(NetworkPrefab donor, uint hash)
        {
            GameObject root = Plat.Copy(donor.gameObject, _holder.transform);
            root.name = "PortalGun";
            root.SetActive(true);
            NetworkPrefab np = Plat.Get<NetworkPrefab>(root);
            Plat.SetPrefabHash(np, hash);
            if (np.Hash != hash) throw new InvalidOperationException("the gun's prefab number did not take");
            foreach (Renderer r in Plat.All<Renderer>(root.transform, true)) r.enabled = false;
            List<PartDef> parts = Pack.Parts("PortalGun");
            PartDef visual = parts[1];
            GameObject look = new GameObject("PortalGunVisual");
            look.layer = root.layer;
            look.transform.SetParent(root.transform, false);
            Quaternion turn = Quaternion.Euler(0f, 180f, 0f);
            look.transform.localRotation = turn * visual.Rot;
            look.transform.localPosition = turn * visual.Pos;
            look.transform.localScale = visual.Scale;
            Plat.Add<MeshFilter>(look).sharedMesh = Pack.MeshOf(visual.Mesh);
            List<Material> mats = new List<Material>();
            foreach (int k in visual.Mats) mats.Add(Pack.MaterialOf(k));
            Plat.SetMaterials(Plat.Add<MeshRenderer>(look), mats);
            Vector3[] b = parts[0].Boxes[0];
            BoxCollider box = Plat.Add<BoxCollider>(root);
            box.size = b[1];
            box.center = turn * b[0];
            GunBox = b[1];
            Pickup pickup = Plat.Get<Pickup>(root);
            if (pickup == null || pickup.Item == null) throw new InvalidOperationException("the donor has no Pickup with an Item");
            Item item = Plat.CopyAsset(pickup.Item);
            item.name = "PortalGun";
            Plat.SetItem(item, hash, np);
            pickup.Item = item;
            _item = item;
            GrabPoint grip = Plat.ResetGrip(pickup, new Vector3(0f, -0.035f, -0.22f));
            Plat.SetPickup(pickup, Plat.All<Collider>(root.transform, true), grip);
            Plat.BindBehaviours(root, Plat.Get<NetworkEntity>(root));
            return np;
        }

        static NetworkPrefab Gate(uint hash)
        {
            GameObject g = new GameObject("PortalGunGate");
            g.transform.SetParent(_holder.transform, false);
            NetworkEntity e = Plat.Add<NetworkEntity>(g);
            NetworkPrefab np = Plat.Add<NetworkPrefab>(g);
            Plat.Link(np, e);
            Plat.SetPrefabHash(np, hash);
            np.Initialize();
            Plat.BindBehaviours(g, e);
            return np;
        }

        static NetworkPrefab MakeDoor(int end, uint hash)
        {
            string name = end == 0 ? "PortalDoorA" : "PortalDoorB";
            GameObject d = new GameObject(name);
            d.transform.SetParent(_holder.transform, false);
            GameObject look = Pack.Build(name, d.transform, 0, null);
            look.name = "DoorVisual";
            Rigidbody body = Plat.Add<Rigidbody>(d);
            body.isKinematic = true;
            body.useGravity = false;
            NetworkEntity e = Plat.Add<NetworkEntity>(d);
            NetworkPrefab np = Plat.Add<NetworkPrefab>(d);
            NetworkRigidbody nr = Plat.Add<NetworkRigidbody>(d);
            Plat.CacheTransform(nr, d.transform);
            Plat.Link(np, e);
            Plat.SetPrefabHash(np, hash);
            np.Initialize();
            Door.Collision(d);
            Door.Runes(d, end);
            Plat.BindBehaviours(d, e);
            return np;
        }

        internal static void Unregister()
        {
            var map = Plat.PrefabMap;
            var items = Plat.Items;
            for (int i = 0; i < 4; i++)
            {
                if (Prefabs[i] != null && map != null && map.ContainsKey(Hashes[i]) && Plat.Same(map[Hashes[i]], Prefabs[i])) map.Remove(Hashes[i]);
                Prefabs[i] = null;
            }
            if (_item != null && items != null && items.ContainsKey(Hashes[0]) && Plat.Same(items[Hashes[0]], _item)) items.Remove(Hashes[0]);
            if (_item != null) Object.Destroy(_item);
            _item = null;
            Plat.ResetItemNames();
            if (_holder != null) Object.Destroy(_holder);
            _holder = null;
            for (int i = 0; i < 4; i++) Hashes[i] = 0;
            if (Done) Core.Log("portal items removed");
            Done = false;
        }
    }
}
