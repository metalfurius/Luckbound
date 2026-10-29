"""Independent Python vectors for the documented catalog/RNG protocols.

No production encoder is imported. These are protocol fixtures, not JsonUtility
captures. Catalog order/framing: sol-f09-encounter-catalog-pin-design §2–3.
RNG framing and rejection: DEVELOPMENT_PLAN §2 random streams, fixed test inputs.
"""

import hashlib
import json
import struct
from pathlib import Path


def u32(number):
    return struct.pack(">I", number)


def lp(value):
    raw = value.encode("utf-8", errors="strict")
    return u32(len(raw)) + raw


def descriptor_ref(name):
    return lp(name) + u32(1)


def definition_record(rich):
    if not rich:
        return lp("e-1") + u32(1) + u32(0)
    variant = (lp("v") + u32(1) + lp("a") + u32(0) + u32(0)
               + descriptor_ref("room"))
    spawn = (lp("s") + lp("enemy") + descriptor_ref("enemy") + lp("anchor")
             + lp("normal") + u32(0))
    wave = lp("w") + u32(1) + spawn
    cache = b"\x01" + lp("cache-one") + descriptor_ref("cache") + lp("chest")
    return lp("enc") + u32(1) + u32(1) + variant + u32(1) + wave + cache


def catalog(rich):
    domain = b"Luckbound.EncounterCatalog.v1\0"
    if not rich:
        return (domain + u32(1) + lp("cat-é") + u32(7) + lp("encounter-spawn.v1")
                + lp("resolver-a") + u32(2) + u32(0) * 4)
    header = domain + u32(1) + lp("c") + u32(1) + lp("encounter-spawn.v1") + lp("r") + u32(1)
    generations = u32(1) + u32(1)
    descriptors = u32(3)
    for name, kind, digest in (("cache", "cache", "0" * 64),
                               ("enemy", "enemy", "1" * 64),
                               ("room", "room", "0" * 64)):
        descriptors += lp(name) + u32(1) + lp(kind) + lp(digest)
    bindings = (u32(1) + u32(1) + lp("t") + lp("combat") + lp("encounter")
                + b"\x01" + lp("enc") + u32(1))
    return header + generations + descriptors + bindings + u32(1) + definition_record(True)


def vector(raw):
    return {"bytes": len(raw), "hex": raw.hex(), "sha256": hashlib.sha256(raw).hexdigest()}


def derive_roll(sector, index, kind, purpose, slot):
    identifiers = ["9db52316c72b4747961a89b79f4d0c6a", sector + ".combat.01",
                   "room." + sector + ".c1", sector, "Combat", f"distribution.{index:03d}",
                   kind, purpose, str(slot)]
    # All fixed RNG identifiers here are ASCII, so their character lengths also
    # equal UTF-16 code-unit lengths specified by SeedDeriver v1.
    frame = "luckbound-rng-v1|1|13579BDF|4:loot|" + str(len(identifiers))
    frame += "".join("|" + str(len(value)) + ":" + value for value in identifiers)
    state = 2166136261
    for value in frame.encode("utf-8"):
        state = ((state ^ value) * 16777619) & 0xFFFFFFFF
    if state == 0:
        state = 0x6D2B79F5
    derived = state
    while True:
        state ^= (state << 13) & 0xFFFFFFFF
        state ^= state >> 17
        state ^= (state << 5) & 0xFFFFFFFF
        state &= 0xFFFFFFFF
        sample = state - 1
        if sample >= 0xFFFFFFFF % 100:
            return {"frame": frame, "derivedSeed": derived, "roll": sample % 100}


def distributions():
    result = {}
    for sector, boundaries in (("patio", (40, 75)), ("archivo", (40, 80)), ("forjas", (40, 70))):
        drops = 0
        counts = [0, 0, 0]
        draws = []
        for index in range(128):
            normal = derive_roll(sector, index, "Normal", "normal-drop-roll", 0)
            drops += normal["roll"] < 50
            for slot in range(2):
                draw = derive_roll(sector, index, "Elite", "elite-guaranteed", slot)
                counts[0 if draw["roll"] < boundaries[0] else 1 if draw["roll"] < boundaries[1] else 2] += 1
                draws.append(draw)
        result[sector] = {"normalDrops": drops, "eliteMaterialCounts": counts,
                          "firstEliteDraw": draws[0],
                          "eliteRollsSha256": hashlib.sha256(bytes(d["roll"] for d in draws)).hexdigest()}
    return result


if __name__ == "__main__":
    vectors = {"manifestEmpty": vector(catalog(False)), "manifestRich": vector(catalog(True)),
               "definitionEmpty": vector(b"Luckbound.EncounterDefinition.v1\0" + definition_record(False)),
               "definitionRich": vector(b"Luckbound.EncounterDefinition.v1\0" + definition_record(True)),
               "rewardDistributions": distributions(),
               "unityExecuted": False, "jsonUtilityExecuted": False,
               "reference": "Independent protocol framing, SHA256, FNV1a and xorshift32; fixed synthetic inputs"}
    output = Path(__file__).with_name("REFERENCE-VECTORS.json")
    output.write_text(json.dumps(vectors, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: {q: v for q, v in value.items() if q != "hex"}
                      if isinstance(value, dict) else value for k, value in vectors.items()}, indent=2))
