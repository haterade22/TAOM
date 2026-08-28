#!/usr/bin/env python3
"""Reconcile deleted Armory meshes against the items and troops that still use them.

WHY THIS EXISTS, AND WHY THE EXISTING VALIDATOR SAYS PASS

`tools/validate_mesh_refs.py` resolves every mesh reference against the COOKED
packs (`LOTRLOME_Armory/AssetPackages/pack*.tpac`). Those packs are rebuilt only
when someone re-cooks them. Delete art from `Assets/` / `AssetSources/` and the
packs keep shipping the old meshes, so the validator reports PASS, the game keeps
rendering the items, and nothing breaks until the next re-cook. On 2026-08-28 that
gap hid a 179-mesh deletion behind a clean run.

This tool diffs the two trees instead of trusting either:

    gone  = metameshes(AssetPackages/*.tpac)  -  metameshes(Assets/**/*.tpac)
    added = metameshes(Assets/**/*.tpac)      -  metameshes(AssetPackages/*.tpac)

`gone` is art that has been deleted but is still baked into the shipped packs: it
breaks on the next re-cook. `added` is art imported but not yet cooked: it renders
naked RIGHT NOW. Both are reported, because they are opposite failures and get
confused for each other.

It then joins gone -> item -> consumer so the blast radius is one table:

  1. gone mesh -> the <Item>/<CraftingPiece> entries naming it (via
     validate_mesh_refs.extract_refs, over the ModuleData ROOT - crafting pieces
     live directly under it, and scoping to LOTRLOME_items/ is the #352 miss).
  2. item id -> every consumer, across FIVE reference shapes. No single shape is
     sufficient:
       - id="Item.x"                  attribute-agnostic, multi-line tolerant
       - <EquipmentSet id="roster"/>   the roster hop, which is what decides how
                                       many characters an item in an equipment
                                       set actually reaches
       - <item id="Item.x"/>           culture reward / banner-bearer lists
       - bare ids                      four configs parsed by TAOM's own C#, not
                                       by the engine resolver, so they carry no
                                       Item. prefix and an anchored sweep misses
                                       all of them
       - XSLT-authored refs            spcultures.xslt and lords.xslt author data
                                       that exists in no .xml file
  3. Classify each affected item on two independent axes, because they drive
     different remedies:
       recoverability  TEAM_COLOUR - an exact _blue/_green/_red suffix whose base
                                     mesh survives, so re-point and let
                                     UseTeamColor do the work
                       LOST        - no surviving equivalent; delete the item and
                                     repair consumers, or restore the art
       blast radius    ORPHAN      - nothing references the item
                       EQUIPPED    - N consumers, listed
     The LOST + EQUIPPED intersection is the set needing a human decision.
     Everything else is mechanical.

Fuzzy name matching is deliberately NOT used to propose a remedy. At cutoff 0.86
difflib pairs `lotr_troll_helmet` with `lotr_troll_feet`. A fuzzy column may be
emitted for eyeballing, clearly labelled advisory, and never as a recommendation.

REPORT ONLY. This tool has no writer and touches nothing outside its report
directory. Deciding what happens to the affected items is a separate, approved
step - several of the consumers are authored in XSLT, where editing the merged
XML does nothing.

Usage:
  python tools/audit_deleted_mesh_impact.py
  python tools/audit_deleted_mesh_impact.py --out tools/reports/mesh-cleanup
  python tools/audit_deleted_mesh_impact.py --corroborate --assets-repo E:/repos/lotraom-assets \
      --commits 0ecd5df1 7946f65c f6029ca9 312f5ab9
  python tools/audit_deleted_mesh_impact.py --advisory-fuzzy

Exit code (mirrors validate_moduledata.py): 1 if any deleted mesh is still
referenced, 2 if an input path is bad, else 0.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import tempfile
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import validate_mesh_refs as vm  # noqa: E402
from _gamedir import ensure_exists, game_dir  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_ARMORY = Path(DEFAULT_GAME) / "Modules" / "LOTRLOME_Armory"
# The whole consumer side. Modules/TAOM/ModuleData is a byte-identical deployment
# of this directory, and TAOM_Map + the Armory hold zero item consumers, so
# sweeping them would only double-count.
DEFAULT_CONSUMERS = REPO_ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_OUT = REPO_ROOT / "tools" / "reports" / "mesh-cleanup"

# Recoverability
TEAM_COLOUR = "TEAM_COLOUR"
LOST = "LOST"
# Blast radius
ORPHAN = "ORPHAN"
EQUIPPED = "EQUIPPED"

# The engine renders a team-coloured item from one base mesh; per-colour meshes
# are redundant with Flags/UseTeamColor. Exact suffix rule, never fuzzy.
TEAM_COLOUR_SUFFIXES = ("_blue", "_green", "_red")


# --------------------------------------------------------------------------- #
# Mesh set diff                                                                #
# --------------------------------------------------------------------------- #
@dataclass
class MeshDelta:
    """Two opposite failures, kept apart on purpose.

    gone  - deleted from the authoring tree, still baked into the shipped packs.
            Breaks at the next re-cook.
    added - imported into the authoring tree, not yet cooked. Broken right now.
    """
    gone: set = field(default_factory=set)
    added: set = field(default_factory=set)


def _normalise(names) -> set:
    """Drop the engine-appended .lodN so a pack exposing only `x.lod2` never
    reads as `x is gone`."""
    return {vm._base_mesh_name(n) for n in names}


def diff_mesh_sets(pack_meshes, asset_meshes) -> MeshDelta:
    packs = _normalise(pack_meshes)
    assets = _normalise(asset_meshes)
    return MeshDelta(gone=packs - assets, added=assets - packs)


def classify_recoverability(name: str, surviving) -> tuple:
    """(kind, replacement). TEAM_COLOUR only when the exact base mesh survives."""
    for suffix in TEAM_COLOUR_SUFFIXES:
        if name.endswith(suffix):
            base = name[: -len(suffix)]
            if base in surviving:
                return TEAM_COLOUR, base
    return LOST, ""


# --------------------------------------------------------------------------- #
# Consumer-side reference extraction                                           #
# --------------------------------------------------------------------------- #
@dataclass
class ItemRef:
    item_id: str      # bare id, prefix stripped
    file: str
    line: int
    shape: str        # prefixed | culture_list | bare | xslt
    owner: str        # owning NPCCharacter / EquipmentRoster id, "" if none


@dataclass
class RosterRef:
    roster_id: str
    file: str
    line: int
    owner: str        # the NPCCharacter naming this roster


# Attribute-agnostic by design: anchoring on `id=` silently misses real refs
# (taom_schema.py:150-157 records the same lesson).
_ITEM_REF_RE = re.compile(r'="Item\.([A-Za-z0-9_.\-]+)"')
# An <EquipmentSet> carrying an id is a REFERENCE to a standalone roster. The
# same element without an id is the definition wrapper inside an
# <EquipmentRoster> and must not be counted.
_ROSTER_REF_RE = re.compile(r'<EquipmentSet\b[^>]*?\bid="([^"]+)"')
_ID_RE = re.compile(r'\bid="([^"]+)"')
_COMMENT_RE = re.compile(r"<!--.*?-->", re.S)
# <xsl:template match="NPCCharacter[@id='lord_1_1']"> - the owner of everything
# a stylesheet emits for one entity.
_XSLT_OWNER_RE = re.compile(
    r"<xsl:template\b[^>]*\bmatch=\"[^\"]*\[@id='([^']+)'\][^\"]*\"")


def _strip_comments(text: str) -> str:
    """Blank comments while preserving newlines, so reported lines stay true."""
    return _COMMENT_RE.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)


def _lineno(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1


def _id_by_line(text: str, names: str) -> dict:
    """line -> owning entity id, where the id must sit INSIDE the opening tag.

    Scanning for "the next id= below an opening tag" is wrong, and wrong in a way
    that looks plausible: the troop and character files wrap equipment in an
    ANONYMOUS `<EquipmentRoster civilian="true">`, so that scan walks past it and
    latches onto the first `id="Item.x"` underneath, naming every character after
    a sword. Matching the whole opening tag instead and reading the id from
    within it means an anonymous element can never claim ownership. `[^>]*` spans
    newlines, so a multi-line open still resolves.
    """
    opens = re.compile(r"<(?:" + names + r")\b[^>]*>")
    marks = []
    for m in opens.finditer(text):
        found = _ID_RE.search(m.group(0))
        if found:
            marks.append((m.start(), _lineno(text, m.start()), found.group(1)))
    # lords.xslt authors the whole <Equipments> block of ~389 vanilla lords, and
    # spcultures.xslt does the same for six re-skinned cultures. There the owner
    # is the template's match predicate, not an element, so an element-only scan
    # attributes 778 roster refs to nobody and reports every XSLT-authored lord
    # as unaffected.
    for m in _XSLT_OWNER_RE.finditer(text):
        marks.append((m.start(), _lineno(text, m.start()), m.group(1)))
    marks.sort()
    marks = [(line, owner) for _, line, owner in marks]

    result, current, i = {}, "", 0
    for line in range(1, text.count("\n") + 2):
        while i < len(marks) and marks[i][0] <= line:
            current = marks[i][1]
            i += 1
        result[line] = current
    return result


def _owner_id_by_line(text: str) -> dict:
    """line -> owning NPCCharacter / EquipmentRoster id."""
    return _id_by_line(text, "NPCCharacter|EquipmentRoster")


# validate_mesh_refs attributes a mesh to its owning <Item>/<CraftedItem> only,
# so a mesh named by a <CraftingPiece> arrives with no owner. Six of the deleted
# easterling weapon meshes are exactly that, and a row with no item id cannot be
# acted on.


def _entry_id_by_line(text: str) -> dict:
    """line -> owning Item / CraftedItem / CraftingPiece id."""
    return _id_by_line(text, "Item|CraftedItem|CraftingPiece")


def backfill_entry_ids(mesh_refs, text_by_rel) -> None:
    """Fill owners validate_mesh_refs could not attribute. Mutates in place.

    Only ever fills a BLANK id, so a real attribution is never overwritten.
    """
    maps = {}
    for ref in mesh_refs:
        if ref.item_id:
            continue
        text = text_by_rel.get(ref.file)
        if text is None:
            continue
        if ref.file not in maps:
            maps[ref.file] = _entry_id_by_line(text)
        ref.item_id = maps[ref.file].get(ref.line, "")


def extract_item_refs_from_text(text: str, rel: str) -> list:
    """Every `Item.x` reference in one document, with its owning entity."""
    stripped = _strip_comments(text)
    owners = _owner_id_by_line(text)
    shape = "xslt" if rel.endswith(".xslt") else "prefixed"
    refs = []
    for m in _ITEM_REF_RE.finditer(stripped):
        line = _lineno(stripped, m.start())
        refs.append(ItemRef(item_id=m.group(1), file=rel, line=line,
                            shape=shape, owner=owners.get(line, "")))
    return refs


def extract_roster_refs_from_text(text: str, rel: str) -> list:
    stripped = _strip_comments(text)
    owners = _owner_id_by_line(text)
    refs = []
    for m in _ROSTER_REF_RE.finditer(stripped):
        line = _lineno(stripped, m.start())
        refs.append(RosterRef(roster_id=m.group(1), file=rel, line=line,
                              owner=owners.get(line, "")))
    return refs


# Bare-id shapes are FILE-SCOPED on purpose: `<Item id="x">` in an Armory item
# file is a definition, not a reference. Keyed by a path suffix so the table
# reads as the four real files it describes.
_BARE_ATTR_RE = re.compile(r'\bitem="([A-Za-z0-9_.\-]+)"')
_BARE_ELEM_RE = re.compile(r'<Item\b[^>]*?\bid="([A-Za-z0-9_.\-]+)"')
_BARE_COLON_RE = re.compile(r'\bitem_source="item:([A-Za-z0-9_.\-]+)"')

_BARE_SOURCES = {
    "settlement_guards/settlement_guards_config.xml": _BARE_ATTR_RE,
    "culture_marketplace/culture_marketplace_config.xml": _BARE_ELEM_RE,
    "lotr_issues/taom_lotr_issues.xml": _BARE_COLON_RE,
    "banner_bearers/banner_bearers_config.json": None,   # JSON, handled below
}


def _bare_refs_from_banner_json(text: str, rel: str) -> list:
    """Banner ids are JSON VALUES, not attributes.

    Only CultureBanners values and DefaultBannerItemId are ids; the sibling
    `_comment_*` keys are prose and must not be mined.
    """
    try:
        blob = json.loads(text)
    except (ValueError, TypeError):
        return []
    ids = list(blob.get("CultureBanners", {}).values())
    default = blob.get("DefaultBannerItemId", "")
    if default:
        ids.append(default)
    return [ItemRef(i, rel, 0, "bare", "") for i in ids if i]


def extract_bare_refs(text: str, rel: str) -> list:
    """Item ids in the four configs TAOM parses itself, which carry no prefix."""
    key = next((k for k in _BARE_SOURCES if rel.replace("\\", "/").endswith(k)), None)
    if key is None:
        return []
    if key.endswith(".json"):
        return _bare_refs_from_banner_json(text, rel)
    stripped = _strip_comments(text)
    pattern = _BARE_SOURCES[key]
    return [ItemRef(m.group(1), rel, _lineno(stripped, m.start()), "bare", "")
            for m in pattern.finditer(stripped)]


def sweep_consumers(root: Path) -> tuple:
    """Walk the consumer root once, returning (item_refs, roster_refs)."""
    root = Path(root)
    item_refs, roster_refs = [], []
    for path in sorted(list(root.rglob("*.xml"))
                       + list(root.rglob("*.xslt"))
                       + list(root.rglob("*.json"))):
        if "Languages" in path.parts:
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        try:
            rel = path.relative_to(root).as_posix()
        except ValueError:
            rel = path.as_posix()
        if path.suffix == ".json":
            item_refs.extend(extract_bare_refs(text, rel))
            continue
        item_refs.extend(extract_item_refs_from_text(text, rel))
        item_refs.extend(extract_bare_refs(text, rel))
        roster_refs.extend(extract_roster_refs_from_text(text, rel))
    return item_refs, roster_refs


def resolve_roster_hop(item_ids, item_refs, roster_refs) -> dict:
    """item id -> the characters that reach it THROUGH a standalone roster.

    An item sitting in taom_lord_template_equipment.xml is named by no character
    directly; every lord reaches it via `<EquipmentSet id="...">`. Skipping this
    hop makes such an item look far less used than it is.
    """
    roster_owners = defaultdict(set)
    for ref in roster_refs:
        if ref.owner:
            roster_owners[ref.roster_id].add(ref.owner)
    reached = {i: set() for i in item_ids}
    for ref in item_refs:
        if ref.item_id in reached and ref.owner in roster_owners:
            reached[ref.item_id] |= roster_owners[ref.owner]
    return reached


# --------------------------------------------------------------------------- #
# The joined impact table                                                      #
# --------------------------------------------------------------------------- #
@dataclass
class ImpactRow:
    mesh: str
    item_id: str
    item_file: str
    item_line: int
    attr: str
    culture: str
    recoverability: str
    replacement: str
    blast: str
    direct_refs: int
    rosters: list = field(default_factory=list)
    characters: list = field(default_factory=list)
    consumer_files: list = field(default_factory=list)

    def sort_key(self):
        return (self.mesh, self.item_id)


def build_impact_rows(gone, surviving, mesh_refs, item_refs, roster_refs) -> list:
    """One row per (deleted mesh, item that still names it)."""
    by_item = defaultdict(list)
    for ref in item_refs:
        by_item[ref.item_id].append(ref)
    roster_ids = {r.roster_id for r in roster_refs}

    affected = {r.item_id for r in mesh_refs
                if r.kind == "visual_mesh" and r.name in gone and r.item_id}
    hop = resolve_roster_hop(affected, item_refs, roster_refs)

    rows, seen = [], set()
    for ref in mesh_refs:
        if ref.kind != "visual_mesh" or ref.name not in gone:
            continue
        key = (ref.name, ref.item_id)
        if key in seen:
            continue
        seen.add(key)

        consumers = by_item.get(ref.item_id, [])
        kind, replacement = classify_recoverability(ref.name, surviving)
        # An id used as a ref owner is a roster, so replace it with the
        # characters that roster reaches rather than listing the roster twice.
        direct = {c.owner for c in consumers if c.owner and c.owner not in roster_ids}
        rosters = sorted({c.owner for c in consumers if c.owner in roster_ids})
        rows.append(ImpactRow(
            mesh=ref.name,
            item_id=ref.item_id,
            item_file=ref.file,
            item_line=ref.line,
            attr=ref.attr,
            culture=ref.culture,
            recoverability=kind,
            replacement=replacement,
            blast=EQUIPPED if consumers else ORPHAN,
            direct_refs=len(consumers),
            rosters=rosters,
            characters=sorted(direct | hop.get(ref.item_id, set())),
            consumer_files=sorted({c.file for c in consumers}),
        ))
    return sorted(rows, key=lambda r: r.sort_key())


# --------------------------------------------------------------------------- #
# Optional: corroborate the deleted set against the asset repo's own history   #
# --------------------------------------------------------------------------- #
_LFS_POINTER_MAGIC = b"version https://git-lfs.github.com/spec/v1"


def is_lfs_pointer(blob: bytes) -> bool:
    """The asset repo tracks *.tpac through LFS, so a plain `git show` returns a
    ~130-byte text pointer, not the pack. Scanning that yields no meshes, and
    counting it as "nothing found" produces a silent zero indistinguishable from
    a genuinely empty result."""
    return blob.startswith(_LFS_POINTER_MAGIC)


def corroborate_with_git(repo: Path, commits: list) -> dict:
    """Scan the pre-image of every .tpac those commits deleted.

    The set difference stays the source of truth; this is the evidence trail
    tying it to real commits. Textures and .fbx carry no metamesh names, so this
    can only ever be a subset of the deleted set, never a superset.

    Uses `git cat-file --filters` rather than `git show`, because the former
    applies the LFS smudge filter and the latter does not. Every skip is
    counted and reported: a corroboration step that silently returns zero is
    worse than one that says it could not run.
    """
    out = {"commits": commits, "deleted_files": 0, "scanned": 0,
           "lfs_unresolved": 0, "unparsable": 0, "names": set(), "errors": []}
    for sha in commits:
        try:
            listing = subprocess.run(
                ["git", "show", "--name-status", "--format=", sha],
                cwd=str(repo), capture_output=True, text=True, timeout=120)
        except (OSError, subprocess.SubprocessError) as exc:
            out["errors"].append(f"{sha}: {exc}")
            continue
        if listing.returncode != 0:
            out["errors"].append(f"{sha}: {listing.stderr.strip()[:200]}")
            continue
        for row in listing.stdout.splitlines():
            parts = row.split("\t")
            if len(parts) < 2 or parts[0] != "D":
                continue
            rel = parts[-1]
            if not rel.lower().endswith(".tpac"):
                continue
            out["deleted_files"] += 1
            try:
                blob = subprocess.run(
                    ["git", "cat-file", "--filters", f"{sha}^:{rel}"],
                    cwd=str(repo), capture_output=True, timeout=300)
                if blob.returncode != 0 or not blob.stdout:
                    out["errors"].append(f"{sha}:{rel}: empty or failed read")
                    continue
                if is_lfs_pointer(blob.stdout):
                    out["lfs_unresolved"] += 1
                    continue
                with tempfile.NamedTemporaryFile(suffix=".tpac", delete=False) as fh:
                    fh.write(blob.stdout)
                    tmp = fh.name
                try:
                    res = vm.scan_tpac_metameshes(tmp)
                    if res.parsed_ok:
                        out["scanned"] += 1
                        out["names"] |= res.metamesh_names
                    else:
                        out["unparsable"] += 1
                finally:
                    Path(tmp).unlink(missing_ok=True)
            except (OSError, subprocess.SubprocessError) as exc:
                out["errors"].append(f"{sha}:{rel}: {exc}")
    return out


# --------------------------------------------------------------------------- #
# Reporting                                                                    #
# --------------------------------------------------------------------------- #
def _advisory_fuzzy(name: str, surviving) -> str:
    import difflib
    hit = difflib.get_close_matches(name, sorted(surviving), n=1, cutoff=0.86)
    return hit[0] if hit else ""


def render_report(rows, delta, present_counts, surviving, fuzzy=False,
                  corroboration=None) -> str:
    lost = [r for r in rows if r.recoverability == LOST]
    team = [r for r in rows if r.recoverability == TEAM_COLOUR]
    equipped = [r for r in rows if r.blast == EQUIPPED]
    orphan = [r for r in rows if r.blast == ORPHAN]
    decide = [r for r in rows if r.recoverability == LOST and r.blast == EQUIPPED]

    out = ["# Deleted-mesh impact report", ""]
    out.append("Diff of the cooked packs against the authoring tree, joined to "
               "every item and consumer that still names a deleted mesh.")
    out.append("")
    out.append("| Quantity | Count |")
    out.append("|---|---|")
    out.append(f"| Metameshes in packs (cooked, pre-cleanup) | {present_counts['packs']:,} |")
    out.append(f"| Metameshes in Assets (authoring, current) | {present_counts['assets']:,} |")
    out.append(f"| **Meshes deleted** | **{len(delta.gone):,}** |")
    out.append(f"| Meshes imported but not yet cooked | {len(delta.added):,} |")
    out.append(f"| **Deleted meshes still referenced** | **{len({r.mesh for r in rows}):,}** |")
    out.append(f"| **Distinct items affected** | **{len({r.item_id for r in rows}):,}** |")
    out.append(f"| Recoverable (team-colour, base survives) | {len(team):,} |")
    out.append(f"| Lost (no surviving equivalent) | {len(lost):,} |")
    out.append(f"| Referenced by a consumer | {len(equipped):,} |")
    out.append(f"| Referenced by nothing (orphan) | {len(orphan):,} |")
    out.append(f"| **Needs a human decision (lost AND equipped)** | **{len(decide):,}** |")
    out.append("")

    if delta.added:
        out.append("## Imported but not cooked (broken right now)")
        out.append("")
        out.append("These render naked until the packs are rebuilt. Work in "
                   "progress rather than findings.")
        out.append("")
        out.append("This set is **imported but not cooked**, which is deliberately "
                   "wider than what `validate_mesh_refs.py` reports. That tool "
                   "walks item XML, so it sees only meshes an item actually "
                   "names; a mesh imported into `Assets/` that no item references "
                   "yet is invisible to it and appears only here. A count "
                   "difference between the two is the expected shape, not a "
                   "discrepancy to reconcile.")
        out.append("")
        for name in sorted(delta.added):
            out.append(f"- `{name}`")
        out.append("")

    if corroboration:
        out.append("## Corroboration against the asset repo")
        out.append("")
        out.append(f"Commits: {', '.join(corroboration['commits'])}")
        out.append(f"Deleted .tpac files: {corroboration['deleted_files']} "
                   f"(parsed {corroboration['scanned']}, "
                   f"LFS pointer unresolved {corroboration['lfs_unresolved']}, "
                   f"unparsable {corroboration['unparsable']})")
        if corroboration["lfs_unresolved"]:
            out.append("")
            out.append("Some pre-images came back as LFS pointers rather than "
                       "packs, so those files contributed no names. Treat the "
                       "agreement figure below as a floor, not a verdict: "
                       "`git lfs fetch` the deleted objects to close the gap.")
        names = corroboration["names"]
        agreed = names & delta.gone
        only_git = names - delta.gone
        out.append(f"Mesh names recovered from the deleted blobs: {len(names)}")
        out.append(f"Agreeing with the set difference: {len(agreed)}")
        out.append(f"In git but NOT in the set difference: {len(only_git)}")
        if only_git:
            out.append("")
            out.append("Reported rather than reconciled - a name here was deleted "
                       "from the repo but still resolves in the authoring tree:")
            for name in sorted(only_git)[:50]:
                out.append(f"- `{name}`")
        if corroboration["errors"]:
            out.append("")
            out.append(f"Errors: {len(corroboration['errors'])} "
                       f"(first: {corroboration['errors'][0]})")
        out.append("")

    out.append("## Needs a decision: lost art that something still equips")
    out.append("")
    if not decide:
        out.append("None.")
    else:
        out.append("| Mesh | Item | Culture | Consumers | Characters reached |")
        out.append("|---|---|---|---|---|")
        for r in decide:
            chars = ", ".join(r.characters[:6]) + ("..." if len(r.characters) > 6 else "")
            out.append(f"| `{r.mesh}` | `{r.item_id}` | {r.culture or '-'} | "
                       f"{r.direct_refs} | {chars or '-'} |")
    out.append("")

    out.append("## Mechanical: team-colour variants whose base mesh survives")
    out.append("")
    if not team:
        out.append("None.")
    else:
        out.append("Re-point each to its base and let `UseTeamColor` render the colour.")
        out.append("")
        out.append("| Item | Deleted mesh | Re-point to |")
        out.append("|---|---|---|")
        for r in team:
            out.append(f"| `{r.item_id}` | `{r.mesh}` | `{r.replacement}` |")
    out.append("")

    out.append("## Lost art that nothing equips")
    out.append("")
    lost_orphans = [r for r in rows if r.recoverability == LOST and r.blast == ORPHAN]
    if not lost_orphans:
        out.append("None.")
    else:
        out.append(f"{len(lost_orphans)} items. No consumer references these, so "
                   "removing them cannot break a roster.")
        out.append("")
        out.append("| Item | Mesh | Culture | Defined in |")
        out.append("|---|---|---|---|")
        for r in lost_orphans:
            out.append(f"| `{r.item_id}` | `{r.mesh}` | {r.culture or '-'} | "
                       f"`{r.item_file}:{r.item_line}` |")
    out.append("")

    out.append("## Surviving inventory by culture")
    out.append("")
    out.append("What is left to re-point onto.")
    out.append("")
    affected_by_culture = defaultdict(int)
    for r in rows:
        affected_by_culture[r.culture or "(root)"] += 1
    out.append("| Culture | Items affected |")
    out.append("|---|---|")
    for culture, count in sorted(affected_by_culture.items(), key=lambda kv: -kv[1]):
        out.append(f"| {culture} | {count} |")
    out.append("")

    if fuzzy:
        out.append("## Advisory only: nearest surviving name")
        out.append("")
        out.append("**Not a recommendation.** Fuzzy matching pairs unrelated "
                   "meshes (`lotr_troll_helmet` with `lotr_troll_feet`). Eyeball "
                   "these, never apply them.")
        out.append("")
        out.append("| Deleted mesh | Nearest surviving |")
        out.append("|---|---|")
        for r in lost:
            near = _advisory_fuzzy(r.mesh, surviving)
            if near:
                out.append(f"| `{r.mesh}` | `{near}` |")
        out.append("")

    return "\n".join(out)


def write_outputs(out_dir: Path, rows, report_text) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "REPORT.md").write_text(report_text, encoding="utf-8")

    header = ["mesh", "item_id", "item_file", "item_line", "attr", "culture",
              "recoverability", "replacement", "blast", "direct_refs",
              "rosters", "characters", "consumer_files"]
    lines = ["\t".join(header)]
    for r in rows:
        lines.append("\t".join([
            r.mesh, r.item_id, r.item_file, str(r.item_line), r.attr, r.culture,
            r.recoverability, r.replacement, r.blast, str(r.direct_refs),
            ";".join(r.rosters), ";".join(r.characters), ";".join(r.consumer_files),
        ]))
    (out_dir / "impact.tsv").write_text("\n".join(lines) + "\n", encoding="utf-8")

    payload = [{
        "mesh": r.mesh, "item_id": r.item_id, "item_file": r.item_file,
        "item_line": r.item_line, "attr": r.attr, "culture": r.culture,
        "recoverability": r.recoverability, "replacement": r.replacement,
        "blast": r.blast, "direct_refs": r.direct_refs, "rosters": r.rosters,
        "characters": r.characters, "consumer_files": r.consumer_files,
    } for r in rows]
    (out_dir / "impact.json").write_text(
        json.dumps(payload, indent=2), encoding="utf-8")


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--armory", default=str(DEFAULT_ARMORY),
                    help="LOTRLOME_Armory module root")
    ap.add_argument("--consumers", default=str(DEFAULT_CONSUMERS),
                    help="ModuleData root holding troops/characters/equipment sets")
    ap.add_argument("--out", default=str(DEFAULT_OUT),
                    help="Report output directory")
    ap.add_argument("--corroborate", action="store_true",
                    help="Cross-check the deleted set against the asset repo's "
                         "own history (slow: extracts every deleted .tpac blob)")
    ap.add_argument("--assets-repo", default=r"E:\repos\lotraom-assets",
                    help="Asset repo for --corroborate")
    ap.add_argument("--commits", nargs="+", default=[],
                    help="Commits whose deletions to corroborate against")
    ap.add_argument("--advisory-fuzzy", action="store_true",
                    help="Add the advisory nearest-surviving-name column. Never "
                         "a recommendation; it mis-pairs unrelated meshes.")
    args = ap.parse_args()

    armory = ensure_exists(args.armory, "the LOTRLOME_Armory module")
    consumers = ensure_exists(args.consumers, "the consumer ModuleData root")
    packs_dir = ensure_exists(armory / "AssetPackages", "the Armory AssetPackages")
    assets_dir = ensure_exists(armory / "Assets", "the Armory Assets tree")

    pack_paths = sorted(packs_dir.glob("*.tpac"))
    asset_paths = sorted(assets_dir.rglob("*.tpac"))
    print(f"Scanning {len(pack_paths)} cooked pack(s) and "
          f"{len(asset_paths)} authoring asset(s)...")
    packs = vm.build_present_set(pack_paths)
    assets = vm.build_present_set(asset_paths)
    if packs.unparsed or assets.unparsed:
        print(f"WARNING: {len(packs.unparsed) + len(assets.unparsed)} .tpac "
              f"soft-failed to parse; a mesh inside one reads as absent.",
              file=sys.stderr)

    delta = diff_mesh_sets(packs.metameshes, assets.metameshes)
    surviving = _normalise(assets.metameshes)
    print(f"packs={len(packs.metameshes):,}  assets={len(assets.metameshes):,}  "
          f"gone={len(delta.gone):,}  uncooked={len(delta.added):,}")

    mesh_refs = vm.extract_refs(armory / "ModuleData")
    unattributed = {r.file for r in mesh_refs if not r.item_id}
    if unattributed:
        module_data = armory / "ModuleData"
        texts = {}
        for rel in unattributed:
            try:
                texts[rel] = (module_data / rel).read_text(
                    encoding="utf-8", errors="ignore")
            except OSError:
                continue
        backfill_entry_ids(mesh_refs, texts)

    item_refs, roster_refs = sweep_consumers(consumers)
    print(f"refs: {len(mesh_refs):,} mesh, {len(item_refs):,} item, "
          f"{len(roster_refs):,} roster")

    rows = build_impact_rows(delta.gone, surviving, mesh_refs, item_refs, roster_refs)
    counts = {"packs": len(packs.metameshes), "assets": len(assets.metameshes)}

    corroboration = None
    if args.corroborate:
        if not args.commits:
            print("ERROR: --corroborate needs --commits", file=sys.stderr)
            return 2
        repo = ensure_exists(args.assets_repo, "the asset repo")
        print(f"Corroborating against {len(args.commits)} commit(s)...")
        corroboration = corroborate_with_git(repo, args.commits)

    text = render_report(rows, delta, counts, surviving,
                         fuzzy=args.advisory_fuzzy, corroboration=corroboration)
    out_dir = Path(args.out)
    write_outputs(out_dir, rows, text)

    decide = [r for r in rows if r.recoverability == LOST and r.blast == EQUIPPED]
    print()
    print(f"{len({r.mesh for r in rows})} deleted mesh(es) still referenced by "
          f"{len({r.item_id for r in rows})} item(s)")
    print(f"  team-colour recoverable : {len([r for r in rows if r.recoverability == TEAM_COLOUR])}")
    print(f"  lost                    : {len([r for r in rows if r.recoverability == LOST])}")
    print(f"  equipped                : {len([r for r in rows if r.blast == EQUIPPED])}")
    print(f"  orphan                  : {len([r for r in rows if r.blast == ORPHAN])}")
    print(f"  NEEDS A DECISION        : {len(decide)}")
    print(f"\nReport: {out_dir / 'REPORT.md'}")
    return 1 if rows else 0


if __name__ == "__main__":
    raise SystemExit(main())
