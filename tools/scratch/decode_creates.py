"""
decode_creates.py — extract & decode all ObjectCreate (0x8C) messages
from s2c_payloads.txt (tshark output: "frameno hexpayload" per line).
RakNet 3.x reassembly included.  Wire = LE throughout.
"""
import struct, sys, os, collections

# ---------------------------------------------------------------------------
# RakNet reassembly (verbatim from parse_raknet.py logic)
# ---------------------------------------------------------------------------
RELIABLE = {2,3,4,6,7}
ORDERED  = {1,3,4}

def u16be(b,i): return (b[i]<<8)|b[i+1]
def u24le(b,i): return b[i]|(b[i+1]<<8)|(b[i+2]<<16)
def u32be(b,i): return (b[i]<<24)|(b[i+1]<<16)|(b[i+2]<<8)|b[i+3]

PAYLOAD_FILE = os.path.join(os.path.dirname(__file__), "..", "..", "s2c_payloads.txt")

splits   = {}
messages = []   # (frame_no, data)

for raw_line in open(PAYLOAD_FILE, encoding="ascii", errors="ignore"):
    parts = raw_line.split()
    if len(parts) < 2:
        continue
    try:
        frame_no = int(parts[0])
    except ValueError:
        continue
    hexs = parts[1].replace(":", "")
    try:
        d = bytes.fromhex(hexs)
    except ValueError:
        continue
    if not d:
        continue
    h = d[0]
    if (h & 0x80) == 0:
        continue   # not a data datagram
    if (h & 0x40) or (h & 0x20):
        continue   # ACK / NAK

    p = 1 + 3  # flags byte + datagram number (uint24 LE)

    while p < len(d):
        if p + 3 > len(d):
            break
        rb  = d[p]; p += 1
        rel = (rb >> 5) & 7
        hasSplit = (rb >> 4) & 1

        if p + 2 > len(d):
            break
        bits = u16be(d, p); p += 2

        if rel in RELIABLE:
            if p + 3 > len(d): break
            p += 3   # reliableMessageNumber uint24 LE
        if rel in ORDERED:
            if p + 4 > len(d): break
            p += 4   # orderingIndex uint24 + channel uint8

        scount = sid = sidx = None
        if hasSplit:
            if p + 10 > len(d): break
            scount = u32be(d, p); p += 4
            sid    = u16be(d, p); p += 2
            sidx   = u32be(d, p); p += 4

        nbytes = (bits + 7) // 8
        if p + nbytes > len(d):
            break
        payload = d[p:p+nbytes]; p += nbytes

        if hasSplit:
            s = splits.setdefault(sid, {"count": scount, "frags": {}})
            s["frags"][sidx] = payload
            if len(s["frags"]) == s["count"]:
                full = b"".join(s["frags"][i] for i in range(s["count"]))
                messages.append((frame_no, full))
                del splits[sid]
        else:
            messages.append((frame_no, payload))

print(f"Total reassembled messages: {len(messages)}")

# ---------------------------------------------------------------------------
# Field definitions
# ---------------------------------------------------------------------------

# createData fields in bit-order (bit i → field i)
CREATE_FIELDS = [
    ("noun",            4),   # 0  u32 LE
    ("position",        12),  # 1  3×f32 LE
    ("rotX",            4),   # 2  f32 LE
    ("rotY",            4),   # 3  f32 LE
    ("rotZ",            4),   # 4  f32 LE
    ("assetId",         8),   # 5  u64 LE (seen in decode_obj.py)
    ("scale",           4),   # 6  f32 LE
    ("team",            1),   # 7  u8
    ("hasCollision",    1),   # 8  u8
    ("playerControlled",1),   # 9  u8
]

# SporelabsObject reflection fields: id -> (name, byte_width)
OBJ_FIELDS = {
    0:  ("mTeam",               1),
    1:  ("mbPlayerControlled",  1),
    2:  ("mInputSyncStamp",     4),
    3:  ("mPlayerIndex",        1),
    4:  ("mLinearVelocity",    12),
    5:  ("mAngularVelocity",   12),
    6:  ("mPosition",          12),
    7:  ("mOrientation",       16),
    8:  ("mScale",              4),
    9:  ("mMarkerScale",        4),
   10:  ("mLastAnimState",      4),
   11:  ("mLastAnimPlayTime",   4),
   12:  ("mOverrideMoveIdle",   4),
   13:  ("mGraphicsState",      4),
   14:  ("mGraphicsStateStart", 4),
   15:  ("mNewGfxStart",        4),
   16:  ("mVisible",            1),
   17:  ("mbHasCollision",      1),
   18:  ("mOwnerID",            4),
   19:  ("mMovementType",       1),
   20:  ("mDisableRepulsion",   1),
   21:  ("mInteractableState",  4),
   22:  ("mMarkerId",           4),
}

HERO_NOUNS = {0x2CA50A9A, 0xD6189D41, 0x6367B6CD}

# ---------------------------------------------------------------------------
# Decoder
# ---------------------------------------------------------------------------

def decode_create(m):
    """Return dict with decoded fields, or None on parse error."""
    result = {}
    p = 0
    if len(m) < 1 or m[0] != 0x8C:
        return None
    p += 1  # skip type byte

    if p + 4 > len(m): return None
    obj_id = struct.unpack_from("<I", m, p)[0]; p += 4
    result["objectId"] = obj_id

    if p + 2 > len(m): return None
    bitmap = struct.unpack_from("<H", m, p)[0]; p += 2
    result["createBitmap"] = bitmap
    result["createFields"] = {}

    for i, (name, sz) in enumerate(CREATE_FIELDS):
        if bitmap & (1 << i):
            if p + sz > len(m): break
            raw = m[p:p+sz]; p += sz
            result["createFields"][i] = (name, raw)

    result["objReflOffset"] = p
    result["objReflFields"] = []

    while p < len(m):
        fid = m[p]; p += 1
        if fid == 0xFF:
            result["terminator"] = True
            break
        if fid not in OBJ_FIELDS:
            result["unknownField"] = fid
            result["remainingBytes"] = m[p:].hex()
            break
        name, sz = OBJ_FIELDS[fid]
        if p + sz > len(m): break
        raw = m[p:p+sz]; p += sz
        result["objReflFields"].append((fid, name, raw))

    result["trailingBytes"] = m[p:] if p < len(m) else b""
    return result


def fmt_create_field(name, raw):
    """Human-readable decode of createData fields."""
    if name == "noun":
        v = struct.unpack_from("<I", raw)[0]
        h = "HERO" if v in HERO_NOUNS else "world"
        return f"0x{v:08X}  ({h})"
    if name == "position":
        x,y,z = struct.unpack_from("<fff", raw)
        return f"({x:.3f}, {y:.3f}, {z:.3f})"
    if name in ("rotX","rotY","rotZ","scale"):
        v = struct.unpack_from("<f", raw)[0]
        return f"{v:.4f}"
    if name == "assetId":
        v = struct.unpack_from("<Q", raw)[0]
        return f"0x{v:016X}"
    if name in ("team","hasCollision","playerControlled"):
        return f"{raw[0]}"
    return raw.hex()


def fmt_obj_field(name, raw):
    """Human-readable decode of sporelabs object reflection fields."""
    if name == "mPosition":
        x,y,z = struct.unpack_from("<fff", raw)
        return f"({x:.3f}, {y:.3f}, {z:.3f})"
    if name == "mOrientation":
        a,b,c,d = struct.unpack_from("<ffff", raw)
        return f"quat({a:.4f},{b:.4f},{c:.4f},{d:.4f})"
    if name in ("mScale","mMarkerScale"):
        return f"{struct.unpack_from('<f',raw)[0]:.4f}"
    if name == "mOwnerID":
        return f"0x{struct.unpack_from('<I',raw)[0]:08X}"
    if name == "mMarkerId":
        return f"0x{struct.unpack_from('<I',raw)[0]:08X}"
    if name == "mInteractableState":
        return f"0x{struct.unpack_from('<I',raw)[0]:08X}"
    if len(raw) == 1:
        return f"{raw[0]}"
    if len(raw) == 4:
        return f"0x{struct.unpack_from('<I',raw)[0]:08X}"
    return raw.hex()


# ---------------------------------------------------------------------------
# Collect all 0x8C messages
# ---------------------------------------------------------------------------

creates = [(fn, m) for fn, m in messages if m and m[0] == 0x8C]
print(f"\n0x8C ObjectCreate messages: {len(creates)}")

# Group by length
by_len = collections.defaultdict(list)
for fn, m in creates:
    by_len[len(m)].append((fn, m))

print("\n--- Size distribution ---")
for sz in sorted(by_len):
    frames = [fn for fn,_ in by_len[sz]]
    print(f"  {sz}B  x{len(by_len[sz])}  frames={frames}")

# ---------------------------------------------------------------------------
# Decode each group's first representative
# ---------------------------------------------------------------------------

print("\n")
print("=" * 72)
print("DECODED OBJECT CREATES — one representative per size group")
print("=" * 72)

representatives = {}
for sz in sorted(by_len):
    fn, m = by_len[sz][0]
    representatives[sz] = (fn, m)

all_nouns = {}  # objectId -> noun
for sz in sorted(by_len):
    fn, m = by_len[sz][0]
    dec = decode_create(m)
    if dec is None:
        print(f"\n[sz={sz}] PARSE ERROR")
        continue

    noun_val = None
    if 0 in dec["createFields"]:
        noun_raw = dec["createFields"][0][1]
        noun_val = struct.unpack_from("<I", noun_raw)[0]
    is_hero = noun_val in HERO_NOUNS if noun_val else False

    print(f"\n{'-'*60}")
    print(f"SIZE={sz}B  frame={fn}  {'*** HERO ***' if is_hero else 'world object'}")
    print(f"  objectId : 0x{dec['objectId']:08X}  ({dec['objectId']})")
    print(f"  hex dump : {m.hex()}")
    print(f"\n  --- createData bitmap=0x{dec['createBitmap']:04X} ---")
    for i, (name, raw) in dec["createFields"].items():
        print(f"    bit{i:2d}  {name:20s}  {raw.hex():24s}  → {fmt_create_field(name, raw)}")

    print(f"\n  --- SporelabsObject reflection (starts @byte {dec['objReflOffset']}) ---")
    for fid, name, raw in dec["objReflFields"]:
        print(f"    fid=0x{fid:02X}  {name:22s}  {raw.hex():34s}  → {fmt_obj_field(name, raw)}")

    if dec.get("unknownField") is not None:
        print(f"  !! UNKNOWN FIELD ID=0x{dec['unknownField']:02X}, remaining: {dec['remainingBytes']}")
    if dec["trailingBytes"]:
        print(f"  trailing {len(dec['trailingBytes'])} bytes: {dec['trailingBytes'].hex()}")

    all_nouns[dec["objectId"]] = noun_val

# ---------------------------------------------------------------------------
# Full list of all creates with objectId + noun
# ---------------------------------------------------------------------------
print("\n")
print("=" * 72)
print("ALL ObjectCreates — chronological")
print("=" * 72)
for fn, m in creates:
    dec = decode_create(m)
    if not dec:
        print(f"  frame={fn} PARSE_ERROR")
        continue
    noun_val = None
    if 0 in dec["createFields"]:
        noun_raw = dec["createFields"][0][1]
        noun_val = struct.unpack_from("<I", noun_raw)[0]
    tag = "HERO   " if (noun_val in HERO_NOUNS) else "world  "
    noun_str = f"0x{noun_val:08X}" if noun_val else "no_noun"
    print(f"  frame={fn:6d}  sz={len(m):3d}  {tag}  objId=0x{dec['objectId']:08X}  noun={noun_str}")

# ---------------------------------------------------------------------------
# Chronological message-type sequence around the ObjectCreate burst
# (~80 messages: from first 0x8C to 20 after last)
# ---------------------------------------------------------------------------
print("\n")
print("=" * 72)
print("MESSAGE-TYPE SEQUENCE — burst window")
print("=" * 72)

first_idx = next((i for i,(fn,m) in enumerate(messages) if m and m[0]==0x8C), None)
last_idx  = max((i for i,(fn,m) in enumerate(messages) if m and m[0]==0x8C), default=None)

if first_idx is not None:
    window_start = max(0, first_idx - 5)
    window_end   = min(len(messages), last_idx + 21)
    for i in range(window_start, window_end):
        fn, m = messages[i]
        if not m: continue
        tag = ""
        if m[0] == 0x8C:
            dec = decode_create(m)
            noun_val = None
            if dec and 0 in dec["createFields"]:
                noun_raw = dec["createFields"][0][1]
                noun_val = struct.unpack_from("<I", noun_raw)[0]
            tag = " HERO" if (noun_val in HERO_NOUNS) else " world"
        print(f"  [{i:4d}]  frame={fn:6d}  type=0x{m[0]:02X}  sz={len(m):4d}{tag}")
