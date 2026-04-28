"""Resolve where the four output files live (project-agnostic).

Resolution order:
1. CLI flag --module-data <path>
2. weapon_xml.toml in the current working directory
3. Interactive prompt: scan %BANNERLORD_GAME_DIR%/Modules/*/ModuleData/ for any folder
   containing all four target files; offer the matches; the user picks.

Also handles the culture prompt when a manifest entry leaves culture unspecified
and the prefix-detector returns nothing — empire is the documented fallback.
"""

from __future__ import annotations

import os
import sys
from dataclasses import dataclass, replace
from typing import Optional

# Python 3.11+ has tomllib in stdlib; fall back to tomli on older.
try:
    import tomllib  # type: ignore
except ModuleNotFoundError:  # pragma: no cover
    import tomli as tomllib  # type: ignore


CONFIG_FILENAME = "weapon_xml.toml"

DEFAULT_FILES = {
    "crafting_pieces_xml":      "LOTRLOME_crafting_pieces.xml",
    "items_xml":                "LOTRLOME_items/LOTRAOM_weapons.xml",
    "crafting_templates_xslt":  "crafting_templates.xslt",
    "weapon_descriptions_xslt": "weapon_descriptions.xslt",
}

DEFAULT_CULTURE = "empire"


@dataclass
class OutputPaths:
    module_data: str
    crafting_pieces_xml: str
    items_xml: str
    crafting_templates_xslt: str
    weapon_descriptions_xslt: str

    @classmethod
    def from_module_data(cls, module_data: str, overrides: Optional[dict[str, str]] = None) -> "OutputPaths":
        files = dict(DEFAULT_FILES)
        if overrides:
            files.update(overrides)
        return cls(
            module_data=module_data,
            crafting_pieces_xml=os.path.join(module_data, files["crafting_pieces_xml"]),
            items_xml=os.path.join(module_data, files["items_xml"]),
            crafting_templates_xslt=os.path.join(module_data, files["crafting_templates_xslt"]),
            weapon_descriptions_xslt=os.path.join(module_data, files["weapon_descriptions_xslt"]),
        )

    def all_paths(self) -> list[str]:
        return [
            self.crafting_pieces_xml,
            self.items_xml,
            self.crafting_templates_xslt,
            self.weapon_descriptions_xslt,
        ]


def resolve_output_paths(
    *,
    cli_module_data: Optional[str],
    cwd: str,
    interactive: bool,
) -> OutputPaths:
    """Pick the ModuleData target. See module docstring for resolution order."""
    overrides: dict[str, str] = {}

    # 1. CLI override wins.
    if cli_module_data:
        return OutputPaths.from_module_data(cli_module_data)

    # 2. Config file in cwd.
    config_path = os.path.join(cwd, CONFIG_FILENAME)
    if os.path.isfile(config_path):
        with open(config_path, "rb") as f:
            cfg = tomllib.load(f)
        out = cfg.get("output", {})
        module_data = out.get("module_data")
        if module_data:
            for key in DEFAULT_FILES:
                if key in out:
                    overrides[key] = out[key]
            return OutputPaths.from_module_data(module_data, overrides=overrides)

    # 3. Interactive scan of installed modules.
    if interactive:
        candidates = _scan_candidates()
        chosen = _prompt_module_data(candidates)
        return OutputPaths.from_module_data(chosen)

    raise SystemExit(
        f"No --module-data flag, no {CONFIG_FILENAME} in CWD, and not interactive. "
        f"Pass --module-data <ModuleData path> or run with --init."
    )


def _scan_candidates() -> list[str]:
    """List ModuleData dirs under %BANNERLORD_GAME_DIR%/Modules that have all 4 files."""
    game_dir = os.environ.get("BANNERLORD_GAME_DIR")
    if not game_dir:
        return []
    modules_dir = os.path.join(game_dir, "Modules")
    if not os.path.isdir(modules_dir):
        return []

    matches: list[str] = []
    for entry in sorted(os.listdir(modules_dir)):
        md = os.path.join(modules_dir, entry, "ModuleData")
        if not os.path.isdir(md):
            continue
        # Match if at least the two XSLTs and crafting_pieces.xml exist
        # (LOTRAOM_weapons.xml may be inside a subfolder so we don't require it here).
        has_pieces = os.path.isfile(os.path.join(md, DEFAULT_FILES["crafting_pieces_xml"]))
        has_t_xslt = os.path.isfile(os.path.join(md, DEFAULT_FILES["crafting_templates_xslt"]))
        has_w_xslt = os.path.isfile(os.path.join(md, DEFAULT_FILES["weapon_descriptions_xslt"]))
        if has_pieces and has_t_xslt and has_w_xslt:
            matches.append(md)
    return matches


def _prompt_module_data(candidates: list[str]) -> str:
    if not candidates:
        print("No installed modules found that contain crafting_pieces.xml + both XSLTs.", file=sys.stderr)
        print("Set BANNERLORD_GAME_DIR or pass --module-data <path>.", file=sys.stderr)
        raise SystemExit(1)
    print("Choose a target ModuleData directory:")
    for i, c in enumerate(candidates, 1):
        print(f"  [{i}] {c}")
    print(f"  [c] (custom path)")
    choice = input("Selection: ").strip().lower()
    if choice == "c":
        return input("Path: ").strip()
    try:
        idx = int(choice) - 1
        return candidates[idx]
    except (ValueError, IndexError):
        raise SystemExit(f"Invalid selection: {choice!r}")


def prompt_culture(piece_or_weapon_id: str, *, default: str = DEFAULT_CULTURE, interactive: bool) -> str:
    """Resolve culture when manifest left it blank and prefix-detection returned None."""
    if not interactive:
        return default
    answer = input(f"Culture for {piece_or_weapon_id!r} [{default}]: ").strip()
    return answer or default


def write_starter_config(cwd: str, module_data: str) -> str:
    path = os.path.join(cwd, CONFIG_FILENAME)
    contents = (
        "# weapon_xml.toml — sticky defaults for tools/build_weapon_xml.py\n"
        "[output]\n"
        f'module_data = "{module_data.replace(chr(92), "/")}"\n'
        "# All four filenames are configurable; uncomment to override:\n"
        "# crafting_pieces_xml      = \"LOTRLOME_crafting_pieces.xml\"\n"
        "# items_xml                = \"LOTRLOME_items/LOTRAOM_weapons.xml\"\n"
        "# crafting_templates_xslt  = \"crafting_templates.xslt\"\n"
        "# weapon_descriptions_xslt = \"weapon_descriptions.xslt\"\n"
    )
    with open(path, "w", encoding="utf-8") as f:
        f.write(contents)
    return path
