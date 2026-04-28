"""Glue layer: manifest + (optional) FBX -> rendered file deltas.

Consumed by tools/build_weapon_xml.py. Lives inside the package so the CLI script
itself stays small.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Optional

from . import classify, config, render_items, render_pieces, render_xslt
from .manifest import (
    CraftedWeaponSpec,
    Manifest,
    PieceSpec,
    SinglePieceWeaponSpec,
    is_single_piece_class,
)


@dataclass
class FileDelta:
    """A file's before/after text. Empty `after_text` == no change."""

    path: str
    before_text: str
    after_text: str

    @property
    def changed(self) -> bool:
        return self.before_text != self.after_text


@dataclass
class PipelineResult:
    deltas: dict[str, FileDelta] = field(default_factory=dict)         # keyed by short name
    new_piece_ids: list[str] = field(default_factory=list)
    new_crafted_ids: list[str] = field(default_factory=list)
    new_single_ids: list[str] = field(default_factory=list)
    skipped_piece_ids: list[str] = field(default_factory=list)
    skipped_item_ids: list[str] = field(default_factory=list)
    weapon_class_by_piece_id: dict[str, str] = field(default_factory=dict)


def build_deltas(
    manifest: Manifest,
    *,
    pieces_xml_path: str,
    items_xml_path: str,
    crafting_templates_xslt_path: str,
    weapon_descriptions_xslt_path: str,
    fbx_meshes: Optional[list[str]] = None,
    interactive_culture: bool = False,
) -> PipelineResult:
    """Apply the manifest (and optionally FBX mesh names) to produce file deltas.

    `fbx_meshes` is the list of mesh names extracted from the FBX. If provided,
    the pipeline cross-checks that every CraftedWeapon's expected piece meshes
    actually exist in the FBX. If None, it trusts the manifest.
    """
    # Load existing files.
    pieces_text = _read(pieces_xml_path)
    items_text  = _read(items_xml_path)
    ct_xslt     = _read(crafting_templates_xslt_path)
    wd_xslt     = _read(weapon_descriptions_xslt_path)

    fbx_set = set(fbx_meshes or [])

    result = PipelineResult()
    piece_renders: list[render_pieces.PieceRender] = []
    item_renders: list[render_items.ItemRender] = []
    pieces_by_class: dict[str, list[str]] = {}     # weapon_class -> [piece_ids]

    # 1. Crafted weapons (4-piece).
    for spec in manifest.crafted:
        _resolve_culture_inplace(spec, identifier=spec.base_id, interactive=interactive_culture)
        crafted_renders = _render_crafted(spec, fbx_set)
        if crafted_renders is None:
            continue
        item_render, piece_renders_for_weapon, piece_ids_by_role = crafted_renders
        item_renders.append(item_render)
        piece_renders.extend(piece_renders_for_weapon)
        pieces_by_class.setdefault(spec.weapon_class, []).extend(p.piece_id for p in piece_renders_for_weapon)
        for p in piece_renders_for_weapon:
            result.weapon_class_by_piece_id[p.piece_id] = spec.weapon_class

    # 2. Single-piece weapons.
    for spec in manifest.single:
        if not is_single_piece_class(spec.weapon_class):
            # Allow it but warn — the manifest author may know what they're doing.
            pass
        _resolve_culture_inplace(spec, identifier=spec.item_id, interactive=interactive_culture)
        item_renders.append(render_items.render_single_piece_item(spec))

    # 3. Insert into pieces XML.
    pieces_after, inserted_pieces = render_pieces.insert_blocks(pieces_text, piece_renders)
    skipped_pieces = [p.piece_id for p in piece_renders if p.piece_id not in {ip.piece_id for ip in inserted_pieces}]
    result.new_piece_ids = [p.piece_id for p in inserted_pieces]
    result.skipped_piece_ids = skipped_pieces
    result.deltas["crafting_pieces_xml"] = FileDelta(pieces_xml_path, pieces_text, pieces_after)

    # 4. Insert into items XML.
    items_after, inserted_items = render_items.insert_blocks(items_text, item_renders)
    inserted_item_ids = {i.item_id for i in inserted_items}
    skipped_items = [r.item_id for r in item_renders if r.item_id not in inserted_item_ids]
    result.new_crafted_ids = [s.base_id for s in manifest.crafted if s.base_id in inserted_item_ids]
    result.new_single_ids = [s.item_id for s in manifest.single if s.item_id in inserted_item_ids]
    result.skipped_item_ids = skipped_items
    result.deltas["items_xml"] = FileDelta(items_xml_path, items_text, items_after)

    # 5. Insert into XSLTs.
    # Self-heal: pass ALL piece IDs from the manifest (not just new_piece_ids) — each
    # render_xslt call is idempotent per-entry, so duplicates are skipped. This way,
    # if a previous run wrote crafting_pieces.xml but crashed before the XSLTs, re-running
    # the manifest catches up the XSLTs even though the pieces themselves were skipped.
    # Without this, partial-failure recovery would silently orphan the pieces from the
    # smithing system. (deep-review #1, gap 2/3)
    ct_after = ct_xslt
    wd_after = wd_xslt
    for weapon_class, piece_ids in pieces_by_class.items():
        if not piece_ids:
            continue
        ct_after, _ = render_xslt.insert_usable_pieces(ct_after, weapon_class, piece_ids)
        wd_after, _ = render_xslt.insert_available_pieces(wd_after, weapon_class, piece_ids)

    result.deltas["crafting_templates_xslt"] = FileDelta(crafting_templates_xslt_path, ct_xslt, ct_after)
    result.deltas["weapon_descriptions_xslt"] = FileDelta(weapon_descriptions_xslt_path, wd_xslt, wd_after)

    return result


# ---------------------------------------------------------------------------
# Internal: culture resolution (deep-review #1, gap 6/7)
# ---------------------------------------------------------------------------

def _resolve_culture_inplace(spec, *, identifier: str, interactive: bool) -> None:
    """Fill in spec.culture if the manifest left it blank.

    Resolution order: explicit attribute (already set) -> prefix detection -> interactive
    prompt with `empire` default. Mirrors the contract documented in config.py and the
    feature doc.
    """
    if spec.culture:
        return
    detected = classify.detect_culture_from_id(identifier)
    if detected:
        spec.culture = detected
        return
    spec.culture = config.prompt_culture(identifier, interactive=interactive)


# ---------------------------------------------------------------------------
# Internal: render one CraftedWeapon spec into 4 pieces + 1 item.
# ---------------------------------------------------------------------------

def _render_crafted(
    spec: CraftedWeaponSpec,
    fbx_meshes: set[str],
) -> Optional[tuple[render_items.ItemRender, list[render_pieces.PieceRender], dict[str, str]]]:
    if not spec.pieces:
        return None
    if not spec.tier:
        raise ValueError(f"<CraftedWeapon id={spec.base_id!r}>: missing tier=")

    piece_renders: list[render_pieces.PieceRender] = []
    piece_ids_by_role: dict[str, str] = {}

    for piece in spec.pieces:
        piece_id, mesh = _piece_id_and_mesh(spec.base_id, piece)
        if fbx_meshes and mesh not in fbx_meshes:
            print(
                f"[warn] mesh {mesh!r} expected for {piece.role} of {spec.base_id} "
                f"but not present in the supplied FBX",
            )
        # Auto-populate body_name on blade if absent. The Armory convention drops the
        # `sm_` prefix when forming the collision name (kept for `wm_`); see
        # classify.derive_collision_name. (deep-review #1, gap 10)
        if piece.role == "blade" and "body_name" not in piece.blade_data:
            piece.blade_data["body_name"] = classify.derive_collision_name(mesh)
        piece_name = _piece_display_name(spec.name, piece.role)
        piece_renders.append(
            render_pieces.render_piece(
                piece_id,
                piece,
                name=piece_name,
                tier=spec.tier,
                mesh=mesh,
            )
        )
        piece_ids_by_role[piece.role] = piece_id

    item_render = render_items.render_crafted_item(spec, piece_ids_by_role=piece_ids_by_role)
    return item_render, piece_renders, piece_ids_by_role


def _piece_id_and_mesh(base_id: str, piece: PieceSpec) -> tuple[str, str]:
    """Compose `<base_id>_<role>` for both id and mesh (the convention)."""
    suffix = piece.role
    piece_id = f"{base_id}_{suffix}"
    mesh = piece_id     # By convention, mesh name == piece id in the Armory.
    return piece_id, mesh


def _piece_display_name(weapon_name: str, role: str) -> str:
    """Synthesize a piece's localized name from the weapon's name.

    Strips the `{=key}` prefix, appends a role label, then re-keys with a piece-specific key.
    `{=aom_x_name}Foo Sword` + role=blade -> `{=aom_x_blade_name}Foo Sword Blade`.
    """
    # Best-effort: keep the {=...} key, append _<role>_name suffix, label visible text.
    role_label = {
        "blade": "Blade",
        "guard": "Guard",
        "hilt": "Hilt",
        "handle": "Handle",
        "pommel": "Pommel",
    }[role]

    if weapon_name.startswith("{=") and "}" in weapon_name:
        end = weapon_name.index("}")
        key = weapon_name[2:end]
        text = weapon_name[end + 1 :]
        # Replace `_name` suffix (if any) with `_<role>_name`, else append.
        if key.endswith("_name"):
            key = key[: -len("_name")] + f"_{role}_name"
        else:
            key = key + f"_{role}_name"
        return f"{{={key}}}{text} {role_label}"
    return f"{weapon_name} {role_label}"


def _read(path: str) -> str:
    # newline="" preserves the file's original line-ending style; the writer side
    # in build_weapon_xml.py uses the same. (deep-review #1, newline handling)
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()
