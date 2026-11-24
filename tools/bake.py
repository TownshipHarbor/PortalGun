import argparse
import hashlib
import io
import os
import re
import struct
import sys
import zlib

import yaml
from PIL import Image

MAGIC = b"PGQ1"
GUN_TEXTURE = 1024
ATLAS_TEXTURE_MAX = 512
CUBE = "cube"


class UnityLoader(yaml.SafeLoader):
    pass


def _any(loader, suffix, node):
    if isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node, deep=True)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    return loader.construct_scalar(node)


UnityLoader.add_multi_constructor("tag:unity3d.com,2011:", _any)


def documents(path):
    text = open(path, encoding="utf-8").read()
    parts = re.split(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", text, flags=re.M)
    out = {}
    for i in range(1, len(parts), 4):
        cls, fid, body = int(parts[i]), int(parts[i + 1]), parts[i + 3]
        doc = yaml.load(body, Loader=UnityLoader) or {}
        name, value = next(iter(doc.items())) if doc else ("", {})
        out[fid] = (cls, name, value or {})
    return out


def meta_guid(path):
    m = re.search(r"guid: ([0-9a-f]{32})", open(path + ".meta", encoding="utf-8").read())
    return m.group(1)


def vec(d, keys="xyz"):
    return tuple(float(d[k]) for k in keys)


def load_material(path):
    docs = documents(path)
    mat = next(v for c, n, v in docs.values() if n == "Material")
    props = mat.get("m_SavedProperties", {})
    color = (1.0, 1.0, 1.0, 1.0)
    for entry in props.get("m_Colors", []) or []:
        if "_Color" in entry:
            c = entry["_Color"]
            color = (float(c["r"]), float(c["g"]), float(c["b"]), float(c["a"]))
    floats = {}
    for entry in props.get("m_Floats", []) or []:
        for k, v in entry.items():
            floats[k] = float(v)
    tex = None
    scale, offset = (1.0, 1.0), (0.0, 0.0)
    for entry in props.get("m_TexEnvs", []) or []:
        if "_MainTex" in entry:
            t = entry["_MainTex"]
            ref = t.get("m_Texture") or {}
            if ref.get("guid"):
                tex = ref["guid"]
            scale = vec(t["m_Scale"], "xy")
            offset = vec(t["m_Offset"], "xy")
    return {
        "name": mat["m_Name"],
        "shader": (mat.get("m_Shader") or {}).get("guid"),
        "color": color,
        "tex": tex,
        "scale": scale,
        "offset": offset,
        "gloss": floats.get("_Glossiness", 0.3),
        "metallic": floats.get("_Metallic", 0.0),
    }


def unity_cube():
    faces = [
        ((0, 0, 1), (-1, 0, 0), (0, 1, 0)),
        ((0, 0, -1), (1, 0, 0), (0, 1, 0)),
        ((1, 0, 0), (0, 0, 1), (0, 1, 0)),
        ((-1, 0, 0), (0, 0, -1), (0, 1, 0)),
        ((0, 1, 0), (1, 0, 0), (0, 0, 1)),
        ((0, -1, 0), (1, 0, 0), (0, 0, -1)),
    ]
    pos, nrm, uv, idx = [], [], [], []
    for n, r, u in faces:
        base = len(pos)
        for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            p = tuple(0.5 * n[k] + 0.5 * su * r[k] + 0.5 * sv * u[k] for k in range(3))
            pos.append(p)
            nrm.append(n)
            uv.append(((su + 1) / 2, (sv + 1) / 2))
        idx += [base, base + 2, base + 1, base, base + 3, base + 2]
    return {"name": "Cube", "pos": pos, "nrm": nrm, "uv": uv, "subs": [idx]}


def check_winding(mesh):
    p, n = mesh["pos"], mesh["nrm"]
    good = bad = 0
    for sub in mesh["subs"]:
        for i in range(0, len(sub), 3):
            a, b, c = p[sub[i]], p[sub[i + 1]], p[sub[i + 2]]
            e1 = [b[k] - a[k] for k in range(3)]
            e2 = [c[k] - a[k] for k in range(3)]
            cr = (e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0])
            avg = [n[sub[i]][k] + n[sub[i + 1]][k] + n[sub[i + 2]][k] for k in range(3)]
            d = sum(cr[k] * avg[k] for k in range(3))
            if d > 0:
                good += 1
            elif d < 0:
                bad += 1
    return good, bad


def load_mesh_asset(path):
    docs = documents(path)
    m = next(v for c, n, v in docs.values() if n == "Mesh")
    vd = m["m_VertexData"]
    count = int(vd["m_VertexCount"])
    channels = vd["m_Channels"]
    data = bytes.fromhex(str(vd["_typelessdata"]))
    stride = 0
    for ch in channels:
        if int(ch["dimension"]):
            stride = max(stride, int(ch["offset"]) + 4 * int(ch["dimension"]))
    if int(m.get("m_IndexFormat", 0)) != 0:
        raise SystemExit("32-bit index buffers are not handled")
    ib = bytes.fromhex(str(m["m_IndexBuffer"]))
    allidx = list(struct.unpack("<%dH" % (len(ib) // 2), ib))

    def read(ch_index, dim):
        ch = channels[ch_index]
        if int(ch["dimension"]) != dim or int(ch["format"]) != 0:
            raise SystemExit("unexpected channel %d in %s" % (ch_index, path))
        off = int(ch["offset"])
        return [struct.unpack_from("<%df" % dim, data, i * stride + off) for i in range(count)]

    subs = []
    for sm in m["m_SubMeshes"]:
        first = int(sm["firstByte"]) // 2
        subs.append(allidx[first:first + int(sm["indexCount"])])
    return {"name": m["m_Name"], "pos": read(0, 3), "nrm": read(1, 3), "uv": read(4, 2), "subs": subs}


def load_obj(path):
    v, vt, vn = [], [], []
    order, faces = [], {}
    current = None
    for line in open(path, encoding="utf-8"):
        t = line.split()
        if not t:
            continue
        if t[0] == "v":
            v.append(tuple(map(float, t[1:4])))
        elif t[0] == "vt":
            vt.append(tuple(map(float, t[1:3])))
        elif t[0] == "vn":
            vn.append(tuple(map(float, t[1:4])))
        elif t[0] == "usemtl":
            current = t[1]
            if current not in faces:
                faces[current] = []
                order.append(current)
        elif t[0] == "f":
            faces[current].append([tuple(int(x) if x else 0 for x in (c.split("/") + ["", ""])[:3]) for c in t[1:]])
    if not vn:
        raise SystemExit("the OBJ has no normals")
    pos, nrm, uv, key = [], [], [], {}
    subs = []
    for name in order:
        idx = []
        for face in faces[name]:
            ids = []
            for a, b, c in face:
                k = (a, b, c)
                if k not in key:
                    key[k] = len(pos)
                    p = v[a - 1]
                    n = vn[c - 1]
                    pos.append((-p[0], p[1], p[2]))
                    nrm.append((-n[0], n[1], n[2]))
                    uv.append(vt[b - 1] if b else (0.0, 0.0))
                ids.append(key[k])
            for i in range(1, len(ids) - 1):
                idx += [ids[0], ids[i + 1], ids[i]]
        subs.append(idx)
    return {"name": os.path.splitext(os.path.basename(path))[0], "pos": pos, "nrm": nrm, "uv": uv, "subs": subs}, order


def mip_chain(img):
    out = bytearray()
    w, h = img.size
    level = img
    mips = 0
    while True:
        out += level.tobytes()
        mips += 1
        if w == 1 and h == 1:
            break
        w, h = max(1, w // 2), max(1, h // 2)
        level = img.resize((w, h), Image.LANCZOS)
    return bytes(out), mips


def fit(size, cap):
    w, h = size
    s = min(1.0, cap / max(w, h))
    return max(4, int(round(w * s))), max(4, int(round(h * s)))


class Pack:
    def __init__(self):
        self.textures, self.materials, self.meshes, self.objects = [], [], [], []
        self._tex, self._mat, self._mesh = {}, {}, {}

    def texture(self, key, img, name):
        if key in self._tex:
            return self._tex[key]
        img = img.convert("RGBA").transpose(Image.FLIP_TOP_BOTTOM)
        data, mips = mip_chain(img)
        self.textures.append((name, img.size[0], img.size[1], mips, data))
        self._tex[key] = len(self.textures) - 1
        return self._tex[key]

    def material(self, key, m):
        if key not in self._mat:
            self.materials.append(m)
            self._mat[key] = len(self.materials) - 1
        return self._mat[key]

    def mesh(self, key, m):
        if key not in self._mesh:
            self.meshes.append(m)
            self._mesh[key] = len(self.meshes) - 1
        return self._mesh[key]

    def write(self):
        b = io.BytesIO()
        w = b.write

        def s(text):
            raw = text.encode("utf-8")
            n = len(raw)
            while True:
                byte = n & 0x7F
                n >>= 7
                w(bytes([byte | (0x80 if n else 0)]))
                if not n:
                    break
            w(raw)

        def i(*vals):
            w(struct.pack("<%di" % len(vals), *vals))

        def f(*vals):
            w(struct.pack("<%df" % len(vals), *vals))

        w(MAGIC)
        i(len(self.textures))
        for name, tw, th, mips, data in self.textures:
            s(name)
            i(tw, th, mips, len(data))
            w(data)
        i(len(self.materials))
        for m in self.materials:
            s(m["name"])
            f(*m["color"])
            i(m["tex"])
            f(m["gloss"], m["metallic"])
        i(len(self.meshes))
        for m in self.meshes:
            s(m["name"])
            i(len(m["pos"]))
            for p in m["pos"]:
                f(*p)
            for n in m["nrm"]:
                f(*n)
            for u in m["uv"]:
                f(*u)
            i(len(m["subs"]))
            for sub in m["subs"]:
                i(len(sub))
                i(*sub)
        i(len(self.objects))
        for o in self.objects:
            s(o["name"])
            i(len(o["parts"]))
            for p in o["parts"]:
                s(p["name"])
                i(p["parent"])
                f(*p["pos"])
                f(*p["rot"])
                f(*p["scale"])
                i(p["mesh"])
                i(len(p["mats"]))
                if p["mats"]:
                    i(*p["mats"])
                i(len(p["boxes"]))
                for c, z in p["boxes"]:
                    f(*c)
                    f(*z)
        return b.getvalue()


def material_entry(pack, project, guids, guid, crop_cache):
    path = guids[guid]
    m = load_material(path)
    tex = -1
    if m["tex"]:
        tpath = guids[m["tex"]]
        img = Image.open(tpath)
        sx, sy = m["scale"]
        ox, oy = m["offset"]
        if (sx, sy, ox, oy) != (1.0, 1.0, 0.0, 0.0):
            W, H = img.size
            left = int(round(ox * W))
            right = int(round((ox + sx) * W))
            top = int(round((1 - oy - sy) * H))
            bottom = int(round((1 - oy) * H))
            if not (0 <= left < right <= W and 0 <= top < bottom <= H):
                raise SystemExit("material %s crops outside its texture" % m["name"])
            key = (m["tex"], left, top, right, bottom)
            if key not in crop_cache:
                crop = img.crop((left, top, right, bottom))
                crop = crop.resize(fit(crop.size, ATLAS_TEXTURE_MAX), Image.LANCZOS)
                crop_cache[key] = pack.texture(key, crop, m["name"])
            tex = crop_cache[key]
        else:
            cap = GUN_TEXTURE if max(img.size) > ATLAS_TEXTURE_MAX else max(img.size)
            img = img.resize(fit(img.size, cap), Image.LANCZOS)
            tex = pack.texture(m["tex"], img, os.path.splitext(os.path.basename(tpath))[0])
    entry = {"name": m["name"], "color": m["color"], "tex": tex, "gloss": m["gloss"], "metallic": m["metallic"]}
    return pack.material(guid, entry)


def guid_map(project):
    out = {}
    for root, _, files in os.walk(os.path.join(project, "Assets")):
        for name in files:
            if name.endswith(".meta"):
                p = os.path.join(root, name[:-5])
                if os.path.isfile(p):
                    out[meta_guid(p)] = p
    return out


def door_object(pack, project, guids, prefab, crop_cache, cube_index):
    docs = documents(prefab)
    go = {fid: v for fid, (c, n, v) in docs.items() if c == 1}
    tr = {fid: v for fid, (c, n, v) in docs.items() if c == 4}
    comps = {fid: (c, v) for fid, (c, n, v) in docs.items() if c in (23, 33, 65)}
    roots = [fid for fid, t in tr.items() if int(t["m_Father"]["fileID"]) == 0]
    if len(roots) != 1:
        raise SystemExit("%s has %d roots" % (prefab, len(roots)))
    parts = []

    def visit(tid, parent):
        t = tr[tid]
        g = go[int(t["m_GameObject"]["fileID"])]
        mesh, mats, boxes = -1, [], []
        for ref in g["m_Component"]:
            cid = int(ref["component"]["fileID"])
            if cid not in comps:
                continue
            c, v = comps[cid]
            if c == 33:
                mref = v["m_Mesh"]
                if int(mref["fileID"]) == 10202:
                    mesh = cube_index
                else:
                    mesh = pack.mesh(mref["guid"], load_mesh_asset(guids[mref["guid"]]))
            elif c == 23:
                mats = [material_entry(pack, project, guids, m["guid"], crop_cache) for m in v["m_Materials"]]
            elif c == 65:
                boxes.append((vec(v["m_Center"]), vec(v["m_Size"])))
        if not int(g.get("m_IsActive", 1)):
            return
        index = len(parts)
        parts.append({
            "name": g["m_Name"],
            "parent": parent,
            "pos": vec(t["m_LocalPosition"]),
            "rot": vec(t["m_LocalRotation"], "xyzw"),
            "scale": vec(t["m_LocalScale"]),
            "mesh": mesh,
            "mats": mats,
            "boxes": boxes,
        })
        for child in t["m_Children"]:
            visit(int(child["fileID"]), index)

    visit(roots[0], -1)
    return parts


def gun_object(pack, project, guids, crop_cache):
    base = os.path.join(project, "Assets", "PortalGun")
    prefab = os.path.join(base, "Prefabs", "PortalGun.prefab")
    docs = documents(prefab)
    box = next(v for c, n, v in docs.values() if c == 65)
    inst = next(v for c, n, v in docs.values() if c == 1001)
    mods = {}
    for m in inst["m_Modification"]["m_Modifications"]:
        mods[m["propertyPath"]] = m
    pos = tuple(float(mods["m_LocalPosition." + k]["value"]) for k in "xyz")
    rot = tuple(float(mods["m_LocalRotation." + k]["value"]) for k in "xyzw")
    scale = tuple(float(mods["m_LocalScale." + k]["value"]) for k in "xyz")
    slot_guids = [mods["m_Materials.Array.data[%d]" % i]["objectReference"]["guid"] for i in range(3)]
    mesh, order = load_obj(os.path.join(base, "Models", "portalgunModel.obj"))
    names = {}
    for g in slot_guids:
        names[load_material(guids[g])["name"]] = g
    semantic = {"PTG.001": "PortalGunBody", "Plastic_tube.001": "PortalGunTube", "Emissive.001": "PortalGunBlue"}
    mats = [material_entry(pack, project, guids, names[semantic[o]], crop_cache) for o in order]
    mi = pack.mesh("gun", mesh)
    return [
        {"name": "PortalGun", "parent": -1, "pos": (0, 0, 0), "rot": (0, 0, 0, 1), "scale": (1, 1, 1), "mesh": -1, "mats": [], "boxes": [(vec(box["m_Center"]), vec(box["m_Size"]))]},
        {"name": "Visual", "parent": 0, "pos": pos, "rot": rot, "scale": scale, "mesh": mi, "mats": mats, "boxes": []},
    ], order


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project")
    ap.add_argument("out")
    a = ap.parse_args()
    guids = guid_map(a.project)
    pack = Pack()
    crops = {}
    cube = pack.mesh(CUBE, unity_cube())
    gun, order = gun_object(pack, a.project, guids, crops)
    pack.objects.append({"name": "PortalGun", "parts": gun})
    for door in ("PortalDoorA", "PortalDoorB"):
        parts = door_object(pack, a.project, guids, os.path.join(a.project, "Assets", "PortalGun", "Doorways", door + ".prefab"), crops, cube)
        pack.objects.append({"name": door, "parts": parts})
    for m in pack.meshes:
        good, bad = check_winding(m)
        print("mesh %-14s %5d vertices %s triangles, winding %d front %d back" % (m["name"], len(m["pos"]), "+".join(str(len(s) // 3) for s in m["subs"]), good, bad))
    for t in pack.textures:
        print("texture %-16s %dx%d mips %d" % (t[0], t[1], t[2], t[3]))
    for m in pack.materials:
        print("material %-16s color %s texture %d" % (m["name"], tuple(round(c, 3) for c in m["color"]), m["tex"]))
    for o in pack.objects:
        boxes = sum(len(p["boxes"]) for p in o["parts"])
        print("object %-12s %d parts %d boxes %s" % (o["name"], len(o["parts"]), boxes, ",".join(sorted({p["name"] for p in o["parts"] if p["boxes"]}))))
    print("gun submesh order " + " ".join(order))
    raw = pack.write()
    packed = zlib.compress(raw, 9)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out + ".tmp", "wb") as fh:
        fh.write(struct.pack("<i", len(raw)))
        fh.write(packed[2:-4])
    os.replace(a.out + ".tmp", a.out)
    print("pack %d bytes raw, %d stored, sha256 %s" % (len(raw), os.path.getsize(a.out), hashlib.sha256(open(a.out, "rb").read()).hexdigest()[:8]))


if __name__ == "__main__":
    sys.exit(main())
