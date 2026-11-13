"""Write the faction screen emblems for the Eriador kingdoms from their banner sigils.

The faction screen's top strip shows GUI/FactionUI/RuntimeSprites/fs_emblem_<faction key>.png and
falls back to the star (fs_emblem_default) when the file is missing. For each faction in FACTIONS
this tool reads the kingdom's banner_key in taom_spkingdoms.xml, takes the first layer icon with a
ui_taom_bannericons sprite (the sigil; vanilla shapes under it have none),
crops that icon's white-on-alpha sprite out of the baked UI banner atlas (rect from
TAOMSpriteData.xml), and fills it with the colour every shipped emblem uses for the faction's side.

Only the factions listed in FACTIONS are written, so the hand-made emblems stay untouched.

Usage:
    python tools/make_faction_emblems.py            # dry run: list what would change
    python tools/make_faction_emblems.py --check    # exit 1 when a file is missing or differs
    python tools/make_faction_emblems.py --apply    # write the PNGs
    --out <dir>                                     # write somewhere else (tests)
"""
import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageChops

REPO = Path(__file__).resolve().parent.parent
MODULE = REPO / "Main" / "_Module"
MODULE_DATA = MODULE / "ModuleData"
KINGDOMS_XML = MODULE_DATA / "taom_spkingdoms.xml"
FACTION_KINGDOMS = MODULE_DATA / "FactionUI" / "faction_kingdoms.json"
FACTIONS_JSON = MODULE_DATA / "factionmap" / "factions.json"
SPRITE_DATA = MODULE / "GUI" / "TAOMSpriteData.xml"
ATLAS = MODULE / "AssetSources" / "GauntletUI" / "ui_taom_bannericons_{sheet}.png"
OUT_DIR = MODULE / "GUI" / "FactionUI" / "RuntimeSprites"

FACTIONS = (
    "high_kingdom_of_lindon",
    "kingdom_of_arthedain",
    "kingdom_of_cardolan",
    "kingdom_of_rhudaur",
    "kingdom_of_ered_duin",
)

# Measured from the shipped fs_emblem_*.png: the one opaque colour each side's emblems use.
SIDE_COLOURS = {
    "free": (236, 228, 206),
    "neutral": (214, 186, 112),
    "evil": (206, 94, 82),
}

EMBLEM_SIZE = (256, 256)
SIGIL_FIELD = 10  # a banner key is 10 fields per layer; layer 1's first field is its icon id


def side_colour(side):
    if side not in SIDE_COLOURS:
        raise ValueError(f"no emblem colour for side {side!r}")
    return SIDE_COLOURS[side]


def sigil_icon(banner_key, sprites):
    """The sigil's icon id: the first layer whose icon is one of `sprites`. A layered banner can put
    vanilla shapes, which have no ui_taom_bannericons sprite, under its sigil."""
    fields = banner_key.split(".")
    if len(fields) < 2 * SIGIL_FIELD:
        raise ValueError(f"banner key has no sigil layer: {banner_key!r}")
    for icon in fields[SIGIL_FIELD::SIGIL_FIELD]:
        if icon in sprites:
            return icon
    raise ValueError(f"banner key has no layer with a ui_taom_bannericons sprite: {banner_key!r}")


def _kingdom_banner_keys():
    text = KINGDOMS_XML.read_bytes().decode("utf-8-sig")
    keys = {}
    for kingdom in ET.fromstring(text).iter("Kingdom"):
        if kingdom.get("banner_key"):
            keys[kingdom.get("id")] = kingdom.get("banner_key")
    return keys


def _sprite_rects():
    rects = {}
    for part in ET.parse(SPRITE_DATA).getroot().iter("SpritePart"):
        if part.findtext("CategoryName") != "ui_taom_bannericons":
            continue
        rects[part.findtext("Name")] = (
            int(part.findtext("SheetID")),
            int(part.findtext("SheetX")),
            int(part.findtext("SheetY")),
            int(part.findtext("Width")),
            int(part.findtext("Height")),
        )
    return rects


def build_emblems(only=None):
    """Return {faction key: 256x256 RGBA emblem} for FACTIONS, or the subset named in only."""
    keys = list(only) if only is not None else list(FACTIONS)
    for key in keys:
        if key not in FACTIONS:
            raise ValueError(f"{key} is not a faction this tool writes")

    faction_kingdoms = json.loads(FACTION_KINGDOMS.read_text(encoding="utf-8"))
    factions = json.loads(FACTIONS_JSON.read_text(encoding="utf-8"))
    banner_keys = _kingdom_banner_keys()
    rects = _sprite_rects()
    atlases = {}

    emblems = {}
    for key in keys:
        kingdom = faction_kingdoms[key]
        try:
            icon = sigil_icon(banner_keys[kingdom], sprites=rects)
        except ValueError as error:
            raise ValueError(f"{key}: {error}") from error
        sheet, x, y, w, h = rects[icon]
        if sheet not in atlases:
            atlases[sheet] = Image.open(str(ATLAS).format(sheet=sheet)).convert("RGBA")
        alpha = atlases[sheet].crop((x, y, x + w, y + h)).getchannel("A")
        if alpha.size != EMBLEM_SIZE:
            alpha = alpha.resize(EMBLEM_SIZE, Image.LANCZOS)
        if alpha.getbbox() is None:
            raise ValueError(f"{key}: sigil {icon} sprite is empty in atlas {sheet}")
        emblem = Image.new("RGBA", EMBLEM_SIZE, side_colour(factions[key]["side"]) + (255,))
        emblem.putalpha(alpha)
        emblems[key] = emblem
    return emblems


def _differs(path, image):
    if not path.exists():
        return True
    current = Image.open(path).convert("RGBA")
    return current.size != image.size or ImageChops.difference(current, image).getbbox() is not None


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--apply", action="store_true")
    parser.add_argument("--out", type=Path, default=OUT_DIR)
    args = parser.parse_args(argv)

    stale = []
    for key, image in build_emblems().items():
        path = args.out / f"fs_emblem_{key}.png"
        if not _differs(path, image):
            continue
        stale.append(path)
        if args.apply:
            path.parent.mkdir(parents=True, exist_ok=True)
            image.save(path)

    verb = "wrote" if args.apply else "stale"
    for path in stale:
        print(f"{verb}: {path}")
    if not stale:
        print("all faction emblems up to date")
    return 1 if args.check and stale else 0


if __name__ == "__main__":
    sys.exit(main())
