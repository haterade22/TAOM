"""Classify FBX mesh names into (theme, weapon_stem, role, culture).

The Armory naming convention is:
    wm_<theme>_<weapon>_<role>            visual mesh    (e.g. wm_swan_knight_sword_a_blade)
    bo_<theme>_<weapon>_<role>            collision mesh (paired with the visual)

`role` ends in one of: blade, guard, hilt, handle, pommel.
"hilt" is treated as an alias for "handle" — both map to piece_type="Handle".
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Iterable

ROLE_SUFFIXES = ("blade", "guard", "hilt", "handle", "pommel")
ROLE_TO_PIECE_TYPE = {
    "blade": "Blade",
    "guard": "Guard",
    "hilt": "Handle",
    "handle": "Handle",
    "pommel": "Pommel",
}

_PIECE_CULTURE_PREFIXES: list[tuple[str, str]] = [
    # Heroic / named themes — most specific first so longer prefixes win
    ("wm_aragorn_",       "gondor"),
    ("wm_anduril_",       "gondor"),
    ("wm_boromir_",       "gondor"),
    ("wm_faramir_",       "gondor"),
    ("wm_swan_knight_",   "gondor"),
    ("wm_pelagir_",       "gondor"),
    ("wm_lamedon_",       "gondor"),
    ("wm_witch_king_",    "mordor"),
    ("wm_nazgul_",        "mordor"),
    ("wm_sauron_",        "mordor"),
    ("wm_thranduil_",     "mirkwood"),
    ("wm_legolas_",       "mirkwood"),
    ("wm_galadriel_",     "lothlorien"),
    ("wm_haldir_",        "lothlorien"),
    ("wm_elrond_",        "rivendell"),
    ("wm_gimli_",         "erebor"),
    ("wm_thorin_",        "erebor"),
    ("wm_dain_",          "erebor"),
    ("wm_theoden_",       "rohan"),
    ("wm_eomer_",         "rohan"),
    ("wm_eowyn_",         "rohan"),
    ("wm_saruman_",       "isengard"),
    ("wm_uruk_",          "isengard"),
    ("wm_lurtz_",         "isengard"),
    ("wm_orc_",           "mordor"),
    ("wm_easterling_",    "rhun"),
    ("wm_haradrim_",      "harad"),
    ("wm_corsair_",       "umbar"),
    ("wm_dunlending_",    "dunland"),
    ("wm_mirkwood_",      "mirkwood"),
    ("wm_lothlorien_",    "lothlorien"),
    ("wm_rivendell_",     "rivendell"),
    ("wm_erebor_",        "erebor"),
    ("wm_iron_hills_",    "erebor"),
    ("wm_dale_",          "sturgia"),
    ("wm_mordor_",        "mordor"),
    ("wm_isengard_",      "isengard"),
    ("wm_gundabad_",      "gundabad"),
    ("wm_dol_guldur_",    "dolguldur"),
    ("wm_rohan_",         "rohan"),
    ("wm_gondor_",        "gondor"),
    ("wm_arnor_",         "arnor"),
    ("wm_thenn_",         "gundabad"),
    ("wm_troll_",         "mordor"),
    ("wm_mercenary_",     "neutral"),
]


@dataclass(frozen=True)
class ClassifiedMesh:
    """One visual mesh, classified."""

    raw: str                 # original name as it came out of the FBX (e.g. wm_x_y_blade)
    stem: str                # everything before the role suffix (e.g. wm_x_y)
    role: str                # blade/guard/hilt/handle/pommel
    piece_type: str          # Blade/Guard/Handle/Pommel
    collision: str | None    # paired bo_* name if found, else None


def classify_meshes(mesh_names: Iterable[str]) -> tuple[list[ClassifiedMesh], list[str]]:
    """Split a list of FBX mesh names into classified visuals + leftover unknowns.

    A mesh is "classified" iff its name ends in one of the role suffixes (after a `_`).
    `bo_` collision meshes are paired with their visual sibling and do not produce
    their own ClassifiedMesh entry.

    Returns: (classified, unmatched).
    """
    visuals: dict[str, tuple[str, str]] = {}   # raw -> (stem, role)
    collisions: set[str] = set()
    unmatched: list[str] = []

    for name in mesh_names:
        if name.startswith("bo_"):
            collisions.add(name)
            continue
        matched = False
        for suffix in ROLE_SUFFIXES:
            tail = "_" + suffix
            if name.endswith(tail):
                stem = name[: -len(tail)]
                visuals[name] = (stem, suffix)
                matched = True
                break
        if not matched:
            unmatched.append(name)

    classified: list[ClassifiedMesh] = []
    for raw, (stem, role) in visuals.items():
        # The standard pairing rule: `bo_` + everything after the `wm_`/`sm_` prefix.
        # We try the literal `bo_<raw without first segment>` and a few fallbacks.
        candidate_collisions = _collision_candidates(raw)
        paired = next((c for c in candidate_collisions if c in collisions), None)
        classified.append(
            ClassifiedMesh(
                raw=raw,
                stem=stem,
                role=role,
                piece_type=ROLE_TO_PIECE_TYPE[role],
                collision=paired,
            )
        )

    classified.sort(key=lambda c: (c.stem, c.role))
    return classified, sorted(unmatched)


def _collision_candidates(visual: str) -> list[str]:
    """Plausible collision-mesh names for a visual mesh."""
    candidates = ["bo_" + visual]
    if visual.startswith("wm_"):
        candidates.append("bo_" + visual[len("wm_"):])
    if visual.startswith("sm_"):
        candidates.append("bo_" + visual[len("sm_"):])
    return candidates


def derive_collision_name(mesh: str) -> str:
    """Best-guess `bo_*` name for a visual mesh, matching Armory convention.

    The Armory drops the `wm_`/`sm_` prefix when forming the collision name:
        wm_witch_king_sword_blade -> bo_wm_witch_king_sword_blade   (prefix kept on bo_)
        sm_uruk_halberd_blade_a1  -> bo_uruk_halberd_blade_a1       (sm_ stripped)

    Inspecting the actual LOTRLOME_crafting_pieces.xml shows `wm_` is *kept* on the bo_
    name but `sm_` is *dropped*. We replicate that exactly.
    """
    if mesh.startswith("sm_"):
        return "bo_" + mesh[len("sm_"):]
    return "bo_" + mesh


def detect_culture_from_id(piece_id: str) -> str | None:
    """Return the LOTR culture for a piece-id prefix, or None if unknown."""
    lower = piece_id.lower()
    for prefix, culture in _PIECE_CULTURE_PREFIXES:
        if lower.startswith(prefix):
            return culture
    return None


def group_by_stem(classified: Iterable[ClassifiedMesh]) -> dict[str, list[ClassifiedMesh]]:
    """Cluster pieces sharing the same stem (one weapon = one group)."""
    groups: dict[str, list[ClassifiedMesh]] = {}
    for c in classified:
        groups.setdefault(c.stem, []).append(c)
    for stem in groups:
        groups[stem].sort(key=lambda c: c.role)
    return groups
