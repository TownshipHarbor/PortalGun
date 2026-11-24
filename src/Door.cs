using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortalGunQuest
{
    internal static class Door
    {
        static readonly Material[] _runes = new Material[2];

        internal static void Collision(GameObject root)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground < 0) throw new InvalidOperationException("the game's Ground layer is missing");
            int count = 0;
            foreach (Collider c in Plat.All<Collider>(root.transform, true))
            {
                if (c.isTrigger) continue;
                c.gameObject.layer = ground;
                c.enabled = true;
                count++;
            }
            if (count < 3) throw new InvalidOperationException("the door frame has " + count + " colliders");
        }

        internal static void Runes(GameObject root, int end)
        {
            if (_runes[end] == null)
            {
                Material m = Pack.Lit();
                m.name = end == 0 ? "PortalGun Rune A" : "PortalGun Rune B";
                m.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Pack.SetColor(m, "_Color", end == 0 ? new Color(0.08f, 0.55f, 0.9f) : new Color(1f, 0.38f, 0.08f));
                _runes[end] = m;
            }
            foreach (Renderer r in Plat.All<Renderer>(root.transform, true))
            {
                if (!r.name.StartsWith("Rune", StringComparison.Ordinal)) continue;
                List<Material> mats = new List<Material>();
                mats.Add(_runes[end]);
                Plat.SetMaterials(r, mats);
            }
        }
    }
}
