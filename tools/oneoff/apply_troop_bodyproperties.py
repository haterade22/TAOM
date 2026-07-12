#!/usr/bin/env python3
"""Wire each TAOM troop's body-property template to its culture/race.

Rewrites the `value="BodyProperty.X"` inside every troop's <face_key_template>
across Main/_Module/ModuleData/troops/troops_*.xml so that troops use the
culture- and race-appropriate templates defined in TAOM_bodyproperties.xml,
instead of the vanilla fallbacks (fighter_empire / _aserai / _khuzait / ...).

Decisions baked in (confirmed with the user):
  * Per-unit-type granularity for multi-race cultures (Mordor / Isengard /
    Gundabad / Dol Guldur): trolls / goblins / uruks / orcs / berserkers each
    get their own template.
  * Lore-aware: Khamul's "shadow" troops -> fighter_rhun (Easterling-aligned);
    *_militia* levies in the orc cultures keep their current (human) template.
  * Umbar -> fighter_haradrim (no dedicated fighter_umbar exists).
  * Dale: left unchanged (no fighter_dale template).

Formatting is preserved byte-for-byte except the single value string per troop:
raw binary I/O keeps the UTF-8 BOM and CRLF line endings intact, and we only
substitute inside the matched <face_key_template> token.

Usage:
    python tools/apply_troop_bodyproperties.py            # dry-run (default)
    python tools/apply_troop_bodyproperties.py --dry-run  # explicit dry-run
    python tools/apply_troop_bodyproperties.py --apply     # write changes
"""

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
TROOPS_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "troops"
BODYPROPS_XML = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "TAOM_bodyproperties.xml"

# Vanilla templates we intentionally leave in place (Dale has no custom template).
ALLOWED_VANILLA = {"fighter_sturgia", "fighter_vlandia"}

# --- Per-file configuration --------------------------------------------------

# Single-template cultures: every troop in the file -> this template.
SINGLE = {
    "troops_gondor.xml": "fighter_gondor",
    "troops_rohan.xml": "fighter_rohan",
    "troops_harad.xml": "fighter_haradrim",
    "troops_rhun_new.xml": "fighter_rhun",
    "troops_erebor.xml": "fighter_erebor",
    "troops_rivendell.xml": "fighter_rivendell",
    "troops_mirkwood.xml": "fighter_mirkwood",
    "troops_umbar.xml": "fighter_haradrim",
}

# Round-robin: assign variants cyclically by troop order (visual variety).
ROUNDROBIN = {
    "troops_dunland.xml": [
        "fighter_dunland_a",
        "fighter_dunland_b",
        "fighter_dunland_c",
        "fighter_dunland_d",
        "fighter_dunland_e",
    ],
}

# Files left entirely untouched.
SKIP = {"troops_dale.xml"}

# Multi-race cultures: ordered (substrings, template) rules, first match wins.
# A template of None means "leave this troop's template unchanged".
RULES = {
    "troops_mordor.xml": {
        "rules": [
            (("cave_troll", "troll"), "fighter_cave_troll"),
            (("militia",), None),  # Men of Nurn levies - human
            (("uruk",), "fighter_uruk_mordor"),
            (("orc", "warg"), "fighter_orc_mordor"),
        ],
        "default": "fighter_orc_mordor",
    },
    "troops_isengard.xml": {
        "rules": [
            (("militia",), None),
            (("berserker",), "fighter_uruk_berserker"),
            (("uruk",), "fighter_uruk_hai"),
            (("orc", "warg"), "fighter_orc_mordor"),  # snaga / scout orcs
        ],
        "default": "fighter_uruk_hai",
    },
    "troops_gundabad.xml": {
        "rules": [
            (("militia",), None),
            (("berserker",), "fighter_uruk_berserker"),
        ],
        "default": "fighter_gundabad",
    },
    "troops_dolguldur.xml": {
        "rules": [
            (("militia",), None),
            (("goblin",), "fighter_goblin"),
            (("khamul",), "fighter_rhun"),  # Easterling-aligned (sk_dg_khml_* -> rhun/)
            (("spider",), "fighter_goblin"),  # goblin spider-riders
            (("uruk", "orc", "warg"), "fighter_dolguldur"),
        ],
        "default": "fighter_dolguldur",
    },
}

# --- Regexes -----------------------------------------------------------------

# [^>] also matches newlines, so this spans the multi-line opening tag.
NPC_RE = re.compile(r'<NPCCharacter\b[^>]*?\bid="([^"]+)"[^>]*?>')
FACE_RE = re.compile(
    r'(<face_key_template\b[^>]*?\bvalue="BodyProperty\.)([^"]+)(")'
)


def load_valid_templates():
    """Build the set of valid custom template ids from TAOM_bodyproperties.xml."""
    raw = BODYPROPS_XML.read_bytes()
    text = raw.decode("utf-8-sig")
    ids = set(re.findall(r'<BodyProperty\b[^>]*?\bid="([^"]+)"', text))
    if not ids:
        sys.exit(f"ERROR: no <BodyProperty id=...> found in {BODYPROPS_XML}")
    return ids


def resolve_target(filename, troop_id, idx):
    """Return the target template id, or None to leave the troop unchanged."""
    if filename in SINGLE:
        return SINGLE[filename]
    if filename in ROUNDROBIN:
        variants = ROUNDROBIN[filename]
        return variants[idx % len(variants)]
    if filename in RULES:
        cfg = RULES[filename]
        tid = troop_id.lower()
        for substrings, tmpl in cfg["rules"]:
            if any(s in tid for s in substrings):
                return tmpl
        return cfg["default"]
    return None  # unmanaged file (shouldn't happen)


def process_file(path, valid_templates):
    """Return (new_text, changes) for one troop file.

    changes is a list of (troop_id, old_value, new_value_or_None, status).
    status in {"changed", "already", "kept", "noface"}.
    """
    filename = path.name
    raw = path.read_bytes()
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig") if has_bom else raw.decode("utf-8")

    matches = list(NPC_RE.finditer(text))
    if not matches:
        return text, has_bom, []

    out = [text[: matches[0].start()]]
    changes = []
    for i, m in enumerate(matches):
        start = m.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        block = text[start:end]
        troop_id = m.group(1)

        face = FACE_RE.search(block)
        old_value = face.group(2) if face else None
        target = resolve_target(filename, troop_id, i)

        if face is None:
            changes.append((troop_id, None, None, "noface"))
        elif target is None:
            changes.append((troop_id, old_value, None, "kept"))
        elif old_value == target:
            changes.append((troop_id, old_value, target, "already"))
        else:
            block = FACE_RE.sub(
                lambda mm: mm.group(1) + target + mm.group(3), block, count=1
            )
            changes.append((troop_id, old_value, target, "changed"))

        out.append(block)

    new_text = "".join(out)

    # Reference integrity: every resulting value must be a known custom or
    # intentionally-kept vanilla template.
    for value in FACE_RE.findall(new_text):
        tmpl = value[1]
        if tmpl not in valid_templates and tmpl not in ALLOWED_VANILLA:
            print(
                f"  !! WARNING: {filename} would reference unknown template "
                f"'BodyProperty.{tmpl}'",
                file=sys.stderr,
            )

    return new_text, has_bom, changes


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", help="preview only (default)")
    g.add_argument("--apply", action="store_true", help="write changes to disk")
    args = ap.parse_args()
    apply = args.apply  # dry-run is the default when neither flag is given

    valid_templates = load_valid_templates()
    print(f"Loaded {len(valid_templates)} custom BodyProperty templates from "
          f"{BODYPROPS_XML.name}\n")

    managed = set(SINGLE) | set(ROUNDROBIN) | set(RULES) | SKIP
    files = sorted(TROOPS_DIR.glob("troops_*.xml"))

    grand_changed = grand_already = grand_kept = 0
    for path in files:
        filename = path.name
        if filename in SKIP:
            print(f"== {filename}: SKIPPED (no custom template) ==")
            continue
        if filename not in managed:
            print(f"== {filename}: NOT IN CONFIG -- skipping ==", file=sys.stderr)
            continue

        new_text, has_bom, changes = process_file(path, valid_templates)
        n_changed = sum(1 for c in changes if c[3] == "changed")
        n_already = sum(1 for c in changes if c[3] == "already")
        n_kept = sum(1 for c in changes if c[3] == "kept")
        n_noface = sum(1 for c in changes if c[3] == "noface")
        grand_changed += n_changed
        grand_already += n_already
        grand_kept += n_kept

        print(f"== {filename}: {n_changed} changed, {n_already} already-correct, "
              f"{n_kept} kept-unchanged, {n_noface} no-face ==")
        for troop_id, old, new, status in changes:
            if status == "changed":
                print(f"   {troop_id:<40} {old} -> {new}")
            elif status == "kept":
                print(f"   {troop_id:<40} {old} (kept)")
            elif status == "noface":
                print(f"   {troop_id:<40} (no <face_key_template>)")
        # 'already' rows are silent to keep the table readable.

        if apply and n_changed:
            data = (b"\xef\xbb\xbf" if has_bom else b"") + new_text.encode("utf-8")
            path.write_bytes(data)

    print()
    mode = "APPLIED" if apply else "DRY-RUN (no files written)"
    print(f"[{mode}] total: {grand_changed} changed, {grand_already} "
          f"already-correct, {grand_kept} kept-unchanged")
    if not apply:
        print("Re-run with --apply to write these changes.")


if __name__ == "__main__":
    main()
