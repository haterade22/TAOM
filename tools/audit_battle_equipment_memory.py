#!/usr/bin/env python3
"""Rank the equipment assets a battle preloads, offline, from the release packs.

WHY
  A battle costs about 2 to 3 GB of native memory and gives it back afterwards, and the main
  menu floor attributes about 970 MB to LOTRLOME_Armory alone, but nothing says which items,
  meshes or textures carry that weight. This is the battle counterpart of
  tools/audit_map_scene_memory.py: for every troop, and for chosen sides, it resolves the
  equipment the engine preloads to metameshes, materials, textures and collision bodies in the
  cooked packs and sums their bytes, counting a shared asset once. It attributes pack bytes; it
  does not measure process memory.

WHAT IT READS (read-only on every input)
  <module>/SubModule.xml        Module/Xmls/XmlNode/XmlName@id and @path for Items,
                                CraftingPieces, EquipmentRosters and NPCCharacters
  <module>/ModuleData/<path>.xml, else every *.xml directly in ModuleData/<path>/
  <module>/ModuleData/skins.xml race skins (body meshes, hair and beards)
  <module>/AssetPackages, EmAssetPackages: the tpac tables of contents, through the map tool's
                                AssetIndex and decoders; with --loose-assets, the module's Assets
                                tree instead when it holds a loose tpac (ENGINE RULES)
  A module resolves to <release-modules>/<name> when that folder exists, else
  <game-dir>/Modules/<name>.

ENGINE RULES (Bannerlord v1.5.3, decompiled with tools/taom-src.ps1)
  Which troops: SandBox.View.Missions.MissionPreloadView.OnPreMissionTick preloads every troop
    of every involved party, through PreloadHelper.PreloadCharacters.
  Which sets: PreloadHelper.PreloadCharacters adds character.BattleEquipments, plus
    CivilianEquipments when the mission requires civilian equipment (--include-civilian).
    BasicCharacterObject.Deserialize: the container is <Equipments> or <equipments>; every
    direct <equipment> child is a slot override; <EquipmentRoster> adds one inline set
    (MBEquipmentRoster.InitEquipment: equipmentType, else legacy civilian="true", else
    Battle); a lowercase <equipmentRoster> adds no set (MBEquipmentRoster.Init builds one only
    for a node named EquipmentRoster); <EquipmentSet> or <equipmentSet> adds every set of the
    standalone roster `id` whose type equals the node's type
    (MBEquipmentRoster.AddEquipmentRoster); the overrides then apply to every set
    (AddOverriddenEquipments). Equipment.DeserializeNode takes the item id after the first
    "." and maps Item0..Item3 to Weapon0..Weapon3 and Item4 to ExtraWeaponSlot. Stealth sets
    are neither battle nor civilian and are never counted.
  Which items: Equipment.DeserializeNode puts an item in a slot only when Equipment.IsItemFitsToSlot
    allows it, and otherwise leaves the slot as it was (it raises a failed assert, which the audit
    does not model). The audit applies the rule to each assignment in engine order: an inline
    roster's elements, a standalone roster's set, then every <equipment> override on every set. An
    item of a weapon type (OneHandedWeapon, TwoHandedWeapon, Polearm, Arrows, Bolts, SlingStones,
    Shield, Bow, Crossbow, Sling, Thrown, Pistol, Musket, Bullets, Banner) fits Weapon0 to Weapon3,
    or only ExtraWeaponSlot when its Flags element sets DropOnWeaponChange or DropOnAnyAction;
    HeadArmor fits Head, BodyArmor Body, LegArmor Leg, HandArmor Gloves, Cape Cape, HorseHarness
    HorseHarness, Horse and Animal Horse; Goods, ChestArmor, Book and Invalid fit nowhere. The type
    is ItemObject.Type: the Type attribute (read ignoring case), replaced by the type of the first
    weapon_class of the item's WeaponComponent, which a Weapon or Banner element builds
    (BannerComponent derives from WeaponComponent, and ItemObject.Deserialize builds a fresh one for
    a Banner element, which replaces a Weapon element read before it;
    WeaponComponentData.GetItemTypeFromWeaponClass; an undefined class gives Invalid); no Type
    attribute leaves Invalid; a crafted item has its template's type. An id no definition holds fits
    (the engine's null item does) and is reported as ITEM_UNRESOLVED; a refused assignment is reported
    as ITEM_SLOT_REJECTED and adds nothing: the slot keeps what it held, and the item still counts for
    the troop if another of its slots or sets holds it.
  Which asset tree: with --loose-assets a module that ships an Assets tree holding a loose tpac
    reads only that tree, and a module with none keeps its AssetPackages and EmAssetPackages. The
    engine does the same: its log names Assets for TAOM, which ships both trees
    (docs/reference/armory-guide.md "Two asset trees"), art imported after the last cook renders at
    once, and art deleted from Assets is broken in game whatever a stale pack still holds. The rule
    is read from the engine's log and behaviour, not from the native loader (UNVERIFIED). Each
    cooked tree not read is reported as COOKED_TREE_NOT_READ.
  Which meshes: PreloadHelper.AddItemObject registers the item's mesh and holster mesh; for an
    item with a WeaponComponent (a Weapon or Banner element) the holster-with-weapon, holster and
    flying meshes (for a crafted weapon the piece
    meshes and the Blade piece's holster mesh, CraftedDataView, which builds no holster mesh
    for a template with use_weapon_as_holster_mesh: OneHandedAxe, TwoHandedAxe,
    TwoHandedPolearm, Pike, Mace and TwoHandedMace); for a horse every
    AdditionalMeshes/Mesh; for armour with reins_mesh the reins and reins + "_rope"; then
    GetMultiMesh(male, not slim), and for armour also (male, slim), (female, not slim) and
    (female, slim), whatever the troop's own gender. GetMultiMesh passes isFemale only when
    has_gender_variations (default true) holds, and
    ItemObjectViewExtensions.GetMultiMeshCopyWithGenderData tries m_female or m_male, then
    m_converted / m_converted_slim (female) or m_slim (male, slim), then m. Bodies:
    shield_body_name, body_name and holster_body_name (a crafted item: the Blade piece's
    body_name, and holster_body_name or else body_name, ItemObject.InitCraftedItemObject).
    PreloadHelper keeps every name in a HashSet, so each asset counts once per item.
  Not from PreloadHelper: AddItemObject registers no horse material and PreloadCharacters no
    skin mesh. The audit still counts each horse's Materials and each troop's adult race skin
    (body meshes, hair and beards) as battle cost; where the engine loads them (agent creation
    is likely) is UNVERIFIED.
  Which game types: an XmlNode whose IncludedGameTypes is present and lists no Campaign is
    skipped, so the audit models campaign battles.

SIZES
  floor  = render buffers (metamesh segment 97f81dbb) + texture mip chains + collision bodies
  upper  = floor + edit data (metamesh segment 5f98413d) + every other metamesh segment
  Texture bytes are the map tool's `_texture_row` resident bytes: the pixel segment when the
  pack carries it, else the header formula (flag PIXELS_NOT_IN_PACKS), else the stub segment,
  else 0. The stub and the 0 are both TEXTURE_SIZE_UNKNOWN, and either makes every total that
  holds the texture a lower bound (APPROXIMATIONS). A material adds 0 bytes of its own. A key
  reached twice counts once in a union (a culture, a side, the battle) and in full in each
  item's and troop's own total.

OUTPUTS (default folder: `battle-equipment` beside the map tool's default output folder;
  --out-dir, --report and --tsv-dir as in the map tool; nothing is written inside the game
  install or the release folder)
  battle-equipment-memory.md  method, the run log, sides and battle (with sides), per-culture
                              totals, the top N assets by floor bytes, meshes by edit bytes,
                              items and troops by floor bytes, a flag summary, the unresolved
                              summary by reason, and the approximations below
  assets.tsv      one row per asset the selected troops reach, by floor bytes: kind (metamesh,
                  material, texture, physics), name, module, pack, floor_bytes, edit_bytes,
                  other_bytes, format, width, height, mips (textures), lod0_faces (metameshes;
                  UNKNOWN when a LOD 0 record does not decode), flags (`;`), items, troops and
                  cultures (counts), sample_items and sample_troops (up to five ids, `;`)
  items.tsv       one row per item the selected troops reach: item_id, module, kind (Item or
                  CraftedItem), type, meshes and bodies (the distinct resolved names, each once,
                  sorted case-insensitively, `;`), floor_bytes, upper_bytes, troops (count),
                  unresolved (the item's own unresolved rows)
  troops.tsv      troop_id, module, culture, race, is_hero, battle_sets (sets preloaded), items
                  (distinct defined items), floor_bytes, upper_bytes, race_floor_bytes (the skin
                  meshes alone), unresolved (the troop's unresolved rows)
  cultures.tsv    culture, troops, items, metameshes, textures, physics, render_bytes, edit_bytes,
                  texture_bytes, physics_bytes, floor_bytes, upper_bytes (each a union)
  sides.tsv       with sides only: side, troops, floor_bytes, upper_bytes, shared_floor_bytes (the
                  floor of the side's assets any other side also holds)
  unresolved.tsv  reason, subject, referenced_by: every unresolved row, once
  run.log         the run log below, flushed line by line
  Each run first deletes the report and the TSVs an earlier run left (run.log is rewritten), so a
  folder never mixes two runs: a run without sides leaves no sides.tsv, and a run that aborts
  leaves only run.log. Nothing else in the folder is touched; give a run its own --out-dir to keep
  another.
  Flags: LOD0_OVER_FACE_BUDGET (LOD 0 faces above --face-budget, default 20000),
  LOD0_FACES_UNKNOWN, TEXTURE_4K_ON_SMALL_ITEM (a texture edge of at least --big-texture,
  default 4096, reached from a HeadArmor, HandArmor, LegArmor, Cape, Arrows, Bolts, Thrown,
  OneHandedWeapon or Shield item), RENDER_BUFFERS_NOT_IN_TREE, and the map tool's texture
  flags (UNCOMPRESSED, NO_MIPS_ABOVE_512, PIXELS_NOT_IN_PACKS, FULL_CHAIN_ONLY_IN_EMASSETPACKAGES,
  FORMULA_MISMATCH, HEADER_UNDECODED).

RUN LOG (stdout and <tsv-dir>/run.log; the report repeats it). Integers are unformatted and
  bytes are plain integers. In order: one config line, one module line per --xml-modules entry,
  one index line per --pack-modules entry, one fallback line per reason code that occurred
  (sorted by code), side and battle lines only when sides are given, and the summary line last.
  A run that cannot use its input ends instead with one abort line (exit code 2).
  [EquipMemAudit] config releaseModules=<path> gameModules=<path> xmlModules=<a,b,...>
      packModules=<a,b,...> trees=<AssetPackages+EmAssetPackages[+Assets]> sides=<n>
      civilian=<on|off> heroes=<on|off> faceBudget=<n> bigTexture=<n>
    The folders searched, the module lists in load and resolution order, the trees named (with
    --loose-assets a module that has an Assets tree reads only that), the side count (0: the
    whole population), the switches and the two flag limits.
    Example: [EquipMemAudit] config releaseModules=<releases>\\testing\\Modules
      gameModules=<game>\\Modules xmlModules=Native,SandBoxCore,SandBox,LOTRLOME_Armory,TAOM
      packModules=LOTRLOME_Armory,TAOM,Native,SandBox trees=AssetPackages+EmAssetPackages
      sides=0 civilian=off heroes=off faceBudget=20000 bigTexture=4096
  [EquipMemAudit] module name=<m> root=<release|game|missing> registrations=<n> files=<n>
      items=<n> craftingPieces=<n> rosters=<n> characters=<n> races=<n>
    Where the module was found; the Items, CraftingPieces, EquipmentRosters and NPCCharacters
    registrations kept (campaign game type), the files parsed, the definitions read from this
    module (before later modules override them) and the adult skins read from its skins.xml.
    Example: [EquipMemAudit] module name=LOTRLOME_Armory root=release registrations=22
      files=132 items=3882 craftingPieces=719 rosters=0 characters=0 races=28
  [EquipMemAudit] index module=<m> packs=<n> metameshes=<n> materials=<n> textures=<n>
      physics=<n> errors=<n>
    The tpacs indexed and the item records seen in them (a name another module already
    supplies is counted here but not used), and the pack errors (PACK_ERROR rows).
    Example: [EquipMemAudit] index module=LOTRLOME_Armory packs=10 metameshes=4549
      materials=950 textures=2595 physics=393 errors=0
  [EquipMemAudit] fallback reason=<CODE> count=<n> first=<subject> consequence=<text>
    One line per reason code: how many unresolved rows carry it, the subject of the first, and
    what the audit does without it (REASONS). Every row is in unresolved.tsv.
    Example: [EquipMemAudit] fallback reason=XSLT_NOT_APPLIED count=1 first=TAOM/lords.xslt
      consequence=the transform the engine applies at load is not applied; its edits are not
      modelled
  [EquipMemAudit] side name=<label> troops=<n> items=<n> floorBytes=<n> upperBytes=<n>
      sharedFloorBytes=<n>
    One side: its label (the --troops file name without extension, or the --culture id), its
    troops, the distinct defined items they wear, the union's floor and upper bytes, and the
    floor bytes of its assets any other side also holds.
    Example: [EquipMemAudit] side name=mordor troops=105 items=314 floorBytes=1240873093
      upperBytes=1968990736 sharedFloorBytes=599784697
  [EquipMemAudit] battle sides=<n> troops=<n> floorBytes=<n> upperBytes=<n>
      sumOfSidesFloorBytes=<n>
    The union of every side: distinct troops, floor and upper bytes, and the sum of the
    sides' own floors (the battle floor is never above it; the gap is what the sides share).
    Example: [EquipMemAudit] battle sides=2 troops=336 floorBytes=2579739831
      upperBytes=4442661897 sumOfSidesFloorBytes=3179524528
  [EquipMemAudit] summary troops=<n> items=<n> metameshes=<n> textures=<n> physics=<n>
      renderBytes=<n> editBytes=<n> otherMeshBytes=<n> textureBytes=<n> physicsBytes=<n>
      floorBytes=<n> upperBytes=<n> maxTroop=<id>:<floorBytes>
      maxAsset=<kind>:<name>:<floorBytes> unresolved=<n> elapsedS=<x.x>
    Totals over the union of the selected troops (the population, or every side's troops):
    troops, distinct items, distinct assets per kind, bytes per segment kind, floor and upper
    bytes, the troop and the asset with the largest floor, the unresolved row count and the
    run time in seconds.
    Example: [EquipMemAudit] summary troops=336 items=754 metameshes=1217 textures=682
      physics=98 renderBytes=1450138308 editBytes=1839923748 otherMeshBytes=22998318
      textureBytes=1128198740 physicsBytes=1402783 floorBytes=2579739831
      upperBytes=4442661897 maxTroop=gondor_pg_cavalry:611869964
      maxAsset=texture:weapon_crafting_n:44739280 unresolved=428 elapsedS=1.7
  [EquipMemAudit] abort reason=<CODE> detail=<text>
    Why the run stopped: TROOP_FILE_UNREADABLE (a --troops file does not read), TROOP_UNKNOWN
    (it names an id no module defines), SIDE_EMPTY (a side selects no troop) or
    NO_TROOPS_SELECTED (no side given and the population is empty, as when --xml-modules leaves
    TAOM out). No outputs but run.log are written, and the files an earlier run left are gone.
    Example: [EquipMemAudit] abort reason=SIDE_EMPTY detail=side rohan selects no troops
  (Each line above is one line in the log; it is wrapped here for width.)

EXIT CODES
  0 the outputs are written (--help also exits 0, writing nothing); 2 an input folder is
  missing (printed before any log exists), an abort line ends the run, or the command line
  does not parse (argparse prints the usage error; no run.log is written); 1 a refusal to
  write inside the game install or the release folder, or an unexpected error (a Python
  traceback).

Usage:
  python tools/audit_battle_equipment_memory.py                      # testing channel, every troop
  python tools/audit_battle_equipment_memory.py --culture gondor --culture mordor
  python tools/audit_battle_equipment_memory.py --troops side_a.txt --troops side_b.txt --top 100

APPROXIMATIONS
  - XSLT transforms are not applied (XSLT_NOT_APPLIED): characters a transform authors or
    edits, such as TAOM's lords.xslt, are not modelled.
  - Same-id definitions are last-wins (DUPLICATE_DEFINITION); the engine merges them by its
    XSD rules.
  - Equipment TAOM's C# changes at runtime (career kits, creature gear, enlistment) and
    Mission.GetExtraEquipmentElementsForCharacter are not modelled.
  - Hair and beard rows are an upper bound: the face key picks one hair and one beard per
    agent, and every hair and beard name and every cover_typeN is counted.
  - Horse materials are an upper bound: one is picked per horse, and every one is counted.
  - The siege missiles MissionPreloadView preloads in a siege
    (PreloadItems(GetSiegeMissiles())) are not modelled.
  - Stealth equipment sets are never counted: a battle preloads only battle sets (and civilian
    sets with --include-civilian).
  - Registrations whose IncludedGameTypes omit Campaign are skipped, so the audit models
    campaign battles only.
  - Batched copies (needBatchedVersion) and the crafted weapon meshes the engine assembles at
    runtime allocate buffers that are in no pack.
  - Edit data residency is UNVERIFIED: whether worn armour, hair and beards load their
    5f98413d segments in a battle is not settled, so the floor and the upper bound bracket that
    question only; neither is a strict bound on the battle's cost (see the other items here).
  - Race skins count only the body meshes, hair and beards: the eyebrow meshes, face and mouth
    textures and tattoo materials skins.xml also lists are not counted.
  - Race meshes and horse materials come from outside PreloadHelper (ENGINE RULES), so
    counting them as battle cost is the audit's choice, not the preload's rule.
  - Slot fit follows Equipment.IsItemFitsToSlot (ENGINE RULES) with three gaps: a crafted item's
    flags come from its Blade piece's Flags element, which the audit does not read (no crafting
    piece in the testing release's Armory or in Native sets DropOnWeaponChange or DropOnAnyAction),
    so a crafted item is placed as a plain weapon; a slot named other than the twelve
    EquipmentIndex slots (the enum also accepts aliases such as NumAllWeaponSlots) is not judged
    and counts; and the engine's failed assert on a refusal is not modelled.
  - With --include-heroes every battle set in the XML counts; the engine preloads a hero's one
    live HeroObject.BattleEquipment.
  - An item's component is read by a fixed priority (Weapon, then Horse, then Armor); the
    engine keeps the last ItemComponent child. The type column and TEXTURE_4K_ON_SMALL_ITEM read
    Type verbatim; the engine parses it case-insensitively and overrides it with the weapon class
    for a weapon, and the slot-fit check uses that engine type.
  - A crafted weapon counts every listed piece; the engine builds only the pieces in its
    template's BuildOrders.
  - Only ModuleData/<path>.xslt is detected; the engine also applies <path>.xsl and per-file
    stylesheets beside files in a registered folder.
  - Materials built at runtime for banner-using items are not modelled.
  - With --loose-assets a module that has a loose Assets tree reads only that tree, as the
    engine does, so a name only its cooked packs hold is unresolved (COOKED_TREE_NOT_READ). The
    engine's rule is taken from its log and docs/reference/armory-guide.md, not from the native
    loader.
  - Loose textures: a loose texture carries no pixel segment, so its bytes come from the header
    formula, and one whose metadata is not version 3 (the shared decoder reads only 3; 115 of the
    live Armory's 2,595 loose textures are version 2, measured 2026-10-03) has no size: it counts
    at its stub segment's bytes or 0 (TEXTURE_SIZE_UNKNOWN; the default population reaches 82 of
    the 115 on the live install). Plan 038 Step 9.4 makes an undecoded loose header a STOP
    condition; this tool reports the fallback and decodes nothing, so a --loose-assets total is a
    lower bound, not a finished attribution. Decoding version 2 is a research follow-up.
  - A metamesh's bytes are for all its LODs: the segment order in the table of contents does
    not always follow the record order (audit_map_scene_memory.py docstring), so per-LOD bytes
    are not claimed.
  - A character with no race attribute is read as human.
  - body_mesh_suffix in skins.xml is not applied (the managed preload never reads it).
  - An item id takes the segment after the first dot, as Equipment.DeserializeNode splits it;
    an EquipmentSet id is looked up whole, as BasicCharacterObject.Deserialize does.
  - The population is TAOM's own characters (not templates; heroes only with
    --include-heroes) that have a set to preload; it counts each troop once, whatever its head
    count, because the preload loads each name once.
"""
from __future__ import annotations

import argparse
import collections
import os
import re
import struct
import sys
import time
import weakref
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402
import audit_map_scene_memory as amsm  # noqa: E402

# Native crafting_templates.xml: template id -> item type
CRAFTING_TEMPLATE_TYPES = {
    "OneHandedSword": "OneHandedWeapon", "Dagger": "OneHandedWeapon",
    "OneHandedAxe": "OneHandedWeapon", "Mace": "OneHandedWeapon",
    "TwoHandedSword": "TwoHandedWeapon", "TwoHandedAxe": "TwoHandedWeapon",
    "TwoHandedMace": "TwoHandedWeapon",
    "TwoHandedPolearm": "Polearm", "Pike": "Polearm",
    "ThrowingKnife": "Thrown", "ThrowingAxe": "Thrown", "Javelin": "Thrown",
}
# Native crafting_templates.xml: the templates with use_weapon_as_holster_mesh="true", for which
# CraftedDataView builds no holster mesh from the Blade's BladeData (LOTRLOME_Armory's
# crafting_templates.xslt changes only UsablePieces)
WEAPON_AS_HOLSTER_TEMPLATES = frozenset({"OneHandedAxe", "TwoHandedAxe", "TwoHandedPolearm", "Pike", "Mace",
                                         "TwoHandedMace"})

# the eight skin body attributes (the same eight tools/validate_mesh_refs.py lists)
SKIN_BODY_ATTRS = ("body_meta_mesh", "body_meta_mesh_shoulders", "body_meta_mesh_upperbody",
                   "face_meta_mesh", "hands_mesh", "legs_mesh", "underwear_bottom_mesh",
                   "underwear_top_mesh")
COVER_ATTRS = ("cover_type1", "cover_type2", "cover_type3", "cover_type4")
EQUIPMENT_TYPES = ("Battle", "Civilian", "Stealth")
OLD_SLOT_NAMES = {"Item0": "Weapon0", "Item1": "Weapon1", "Item2": "Weapon2", "Item3": "Weapon3",
                  "Item4": "ExtraWeaponSlot"}

# Equipment.IsItemFitsToSlot (v1.5.3). The ItemObject.ItemTypeEnum members it sends to a weapon slot (Weapon0 to
# Weapon3, or only ExtraWeaponSlot for an item whose Flags set DropOnWeaponChange or DropOnAnyAction), and the one
# slot each other fitting type takes. Goods, ChestArmor, Book and Invalid fit no slot.
WEAPON_ITEM_TYPES = frozenset({"OneHandedWeapon", "TwoHandedWeapon", "Polearm", "Arrows", "Bolts", "SlingStones",
                               "Shield", "Bow", "Crossbow", "Sling", "Thrown", "Pistol", "Musket", "Bullets",
                               "Banner"})
ARMOUR_ITEM_SLOTS = {"HeadArmor": "Head", "BodyArmor": "Body", "LegArmor": "Leg", "HandArmor": "Gloves",
                     "Cape": "Cape", "HorseHarness": "HorseHarness", "Horse": "Horse", "Animal": "Horse"}
WEAPON_SLOTS = ("Weapon0", "Weapon1", "Weapon2", "Weapon3")
EXTRA_SLOT = "ExtraWeaponSlot"
EXTRA_SLOT_FLAGS = ("DropOnWeaponChange", "DropOnAnyAction")
# the twelve EquipmentIndex slots the rule is stated for; a slot named any other way is not judged
FIT_SLOTS = frozenset(WEAPON_SLOTS + (EXTRA_SLOT,) + tuple(ARMOUR_ITEM_SLOTS.values()))
# WeaponComponentData.GetItemTypeFromWeaponClass: the item type the first weapon_class of a Weapon or Banner component
# gives the item; Undefined, NumClasses and any other class give Invalid
WEAPON_CLASS_ITEM_TYPES = {
    "Dagger": "OneHandedWeapon", "OneHandedSword": "OneHandedWeapon", "OneHandedAxe": "OneHandedWeapon",
    "Mace": "OneHandedWeapon", "TwoHandedSword": "TwoHandedWeapon", "TwoHandedAxe": "TwoHandedWeapon",
    "Pick": "TwoHandedWeapon", "TwoHandedMace": "TwoHandedWeapon", "OneHandedPolearm": "Polearm",
    "TwoHandedPolearm": "Polearm", "LowGripPolearm": "Polearm", "Arrow": "Arrows", "Bolt": "Bolts",
    "SlingStone": "SlingStones", "Cartridge": "Bullets", "Bow": "Bow", "Crossbow": "Crossbow", "Sling": "Sling",
    "Stone": "Thrown", "Boulder": "Thrown", "ThrowingAxe": "Thrown", "ThrowingKnife": "Thrown",
    "Javelin": "Thrown", "BallistaBoulder": "Thrown", "BallistaStone": "Thrown", "Pistol": "Pistol",
    "Musket": "Musket", "SmallShield": "Shield", "LargeShield": "Shield", "Banner": "Banner",
}
# the Type attribute is parsed ignoring case (Enum.Parse with ignoreCase)
ITEM_TYPE_NAMES = {name.lower(): name for name in (*WEAPON_ITEM_TYPES, *ARMOUR_ITEM_SLOTS)}


# --------------------------------------------------------------------------- #
# definitions
# --------------------------------------------------------------------------- #
@dataclass
class ItemDef:
    id: str
    module: str
    file: str
    tag: str                    # Item or CraftedItem
    type: str = ""
    mesh: str = ""
    holster_mesh: str = ""
    holster_mesh_with_weapon: str = ""
    flying_mesh: str = ""
    body_name: str = ""
    holster_body_name: str = ""
    shield_body_name: str = ""
    component: str = ""         # Weapon (a Weapon or Banner element: the engine's WeaponComponent), Horse, Armor or ""
    weapon_class: str = ""      # the weapon_class of the Banner element, else of the first Weapon element
    extra_slot_only: bool = False   # Flags DropOnWeaponChange or DropOnAnyAction: only ExtraWeaponSlot takes it
    has_gender_variations: bool = True
    reins_mesh: str = ""
    horse_materials: list = field(default_factory=list)
    additional_meshes: list = field(default_factory=list)
    crafting_template: str = ""
    pieces: list = field(default_factory=list)      # (piece id, Type)


@dataclass
class PieceDef:
    id: str
    module: str
    file: str
    piece_type: str = ""
    mesh: str = ""
    has_blade: bool = False
    blade_body_name: str = ""
    blade_holster_mesh: str = ""
    blade_holster_body_name: str = ""


@dataclass
class CharDef:
    id: str
    module: str
    file: str
    culture: str = ""
    race: str = "human"
    is_female: bool = False
    is_hero: bool = False
    is_template: bool = False
    equipments: list = field(default_factory=list)  # the <Equipments> / <equipments> elements


@dataclass
class Race:
    race_id: str
    gender: str
    module: str
    body: dict = field(default_factory=dict)        # attribute -> mesh name
    hair: list = field(default_factory=list)
    beards: list = field(default_factory=list)


@dataclass
class Definitions:
    items: dict = field(default_factory=dict)
    pieces: dict = field(default_factory=dict)
    rosters: dict = field(default_factory=dict)     # id -> [(equipment type, [(slot, item id)])]
    characters: dict = field(default_factory=dict)
    races: dict = field(default_factory=dict)       # (race id, gender) -> Race
    unresolved: list = field(default_factory=list)  # (reason, subject, referenced_by)
    module_stats: dict = field(default_factory=dict)
    cache: dict = field(default_factory=dict)
    seen: set = field(default_factory=set)

    def miss(self, reason: str, subject: str, referenced_by: str) -> None:
        """Record one unresolved row, once."""
        row = (reason, subject, referenced_by)
        if row not in self.seen:
            self.seen.add(row)
            self.unresolved.append(row)


def _attr(el, name: str) -> str:
    return (el.get(name) or "").strip()


def _bool(el, name: str, default: bool = False) -> bool:
    v = el.get(name)
    if v is None:
        return default
    return v.strip().lower() == "true"


def _object_id(raw: str) -> str:
    """Equipment.DeserializeNode: `Item.x` names x (the segment after the first dot)."""
    raw = (raw or "").strip()
    return raw.split(".")[1] if "." in raw else raw


def _slot(raw: str) -> str:
    raw = (raw or "").strip()
    return OLD_SLOT_NAMES.get(raw, raw)


def _equipment_type(el) -> str:
    """equipmentType (Enum.TryParse, case-sensitive; a failed parse leaves Battle), else
    legacy civilian="true", else Battle."""
    et = el.get("equipmentType")
    if et is not None:
        return et if et in EQUIPMENT_TYPES else "Battle"
    if _bool(el, "civilian"):
        return "Civilian"
    return "Battle"


def _assignments(elements) -> list:
    """Equipment.Deserialize: (slot, item id) for each element that has an id and a slot, in document order."""
    out = []
    for el in elements:
        if not isinstance(el.tag, str) or el.get("slot") is None or el.get("id") is None:
            continue
        out.append((_slot(el.get("slot")), _object_id(el.get("id"))))
    return out


def _flag_set(el, name: str) -> bool:
    """ItemObject.Deserialize sets a flag for a Flags attribute whose value is anything but "false" (any case)."""
    value = el.get(name)
    return value is not None and value.lower() != "false"


def read_registrations(module_dir) -> list:
    """(xml id, path) per XmlName, skipping a node whose IncludedGameTypes lists no Campaign."""
    sub = Path(module_dir) / "SubModule.xml"
    root = ET.fromstring(sub.read_bytes())
    out = []
    for node in root.iter("XmlNode"):
        included = node.find("IncludedGameTypes")
        if included is not None:
            values = [g.get("value", "") for g in included.findall("GameType")]
            if "Campaign" not in values:
                continue
        for name in node.findall("XmlName"):
            out.append((name.get("id", ""), name.get("path", "")))
    return out


def registered_files(module_dir, path) -> list:
    """ModuleData/<path>.xml when it is a file, else the sorted *.xml directly in ModuleData/<path>/."""
    base = Path(module_dir) / "ModuleData"
    single = base / f"{path}.xml"
    if single.is_file():
        return [single]
    folder = base / path
    if folder.is_dir():
        return sorted(p for p in folder.iterdir() if p.is_file() and p.suffix.lower() == ".xml")
    return []


def _parse_item(el, module: str, file: str, defs: Definitions) -> ItemDef:
    it = ItemDef(id=_attr(el, "id"), module=module, file=file, tag=el.tag)
    if el.tag == "CraftedItem":
        it.crafting_template = _attr(el, "crafting_template")
        it.type = CRAFTING_TEMPLATE_TYPES.get(it.crafting_template, "UNKNOWN")
        if it.type == "UNKNOWN":
            defs.miss("CRAFTING_TEMPLATE_UNKNOWN", it.crafting_template, it.id)
        it.component = "Weapon"
        pieces = el.find("Pieces")
        if pieces is not None:
            it.pieces = [(_attr(p, "id"), _attr(p, "Type")) for p in pieces.findall("Piece")]
        return it
    it.type = _attr(el, "Type")
    for f in ("mesh", "holster_mesh", "holster_mesh_with_weapon", "flying_mesh", "body_name",
              "holster_body_name", "shield_body_name"):
        setattr(it, f, _attr(el, f))
    comp = el.find("ItemComponent")
    if comp is not None:
        # a Banner element is the engine's BannerComponent, a WeaponComponent built fresh, so it replaces a Weapon
        # element read before it (ItemObject.Deserialize); test for None, an element with no children is falsy
        weapon = comp.find("Banner")
        if weapon is None:
            weapon = comp.find("Weapon")
        horse, armor = comp.find("Horse"), comp.find("Armor")
        if weapon is not None:
            it.component = "Weapon"
            it.weapon_class = _attr(weapon, "weapon_class")
        elif horse is not None:
            it.component = "Horse"
            mats = horse.find("Materials")
            if mats is not None:
                it.horse_materials = [_attr(m, "name") for m in mats.findall("Material") if _attr(m, "name")]
            extra = horse.find("AdditionalMeshes")
            if extra is not None:
                it.additional_meshes = [_attr(m, "name") for m in extra.findall("Mesh") if _attr(m, "name")]
        elif armor is not None:
            it.component = "Armor"
        if armor is not None:
            it.has_gender_variations = _bool(armor, "has_gender_variations", True)
            it.reins_mesh = _attr(armor, "reins_mesh")
    it.extra_slot_only = any(_flag_set(flags, name) for flags in el.findall("Flags") for name in EXTRA_SLOT_FLAGS)
    return it


def _parse_piece(el, module: str, file: str) -> PieceDef:
    pc = PieceDef(id=_attr(el, "id"), module=module, file=file, piece_type=_attr(el, "piece_type"),
                  mesh=_attr(el, "mesh"))
    blade = el.find("BladeData")
    if blade is not None:
        pc.has_blade = True
        pc.blade_body_name = _attr(blade, "body_name")
        pc.blade_holster_mesh = _attr(blade, "holster_mesh")
        pc.blade_holster_body_name = _attr(blade, "holster_body_name")
    return pc


def _parse_character(el, module: str, file: str) -> CharDef:
    culture = _attr(el, "culture")
    if culture.startswith("Culture."):
        culture = culture[len("Culture."):]
    return CharDef(id=_attr(el, "id"), module=module, file=file, culture=culture,
                   race=_attr(el, "race") or "human", is_female=_bool(el, "is_female"),
                   is_hero=_bool(el, "is_hero"), is_template=_bool(el, "is_template"),
                   equipments=[c for c in el if c.tag in ("Equipments", "equipments")])


def _read_skins(path: Path, module: str, defs: Definitions) -> int:
    root = ET.fromstring(Path(path).read_bytes())
    count = 0
    for race in root.iter("race"):
        race_id = _attr(race, "id")
        for skin in race.findall("skin"):
            if _attr(skin, "mesh_maturity_type") != "adult":
                continue
            r = Race(race_id=race_id, gender=_attr(skin, "gender") or "0", module=module)
            r.body = {a: _attr(skin, a) for a in SKIN_BODY_ATTRS if _attr(skin, a)}
            for tag, target in (("hair_mesh", r.hair), ("beard_mesh", r.beards)):
                for m in skin.iter(tag):
                    for a in ("name",) + COVER_ATTRS:
                        name = _attr(m, a)
                        if name and name not in target:
                            target.append(name)
            defs.races[(r.race_id, r.gender)] = r
            count += 1
    return count


def load_definitions(modules: list) -> Definitions:
    """Read every module in load order (later wins). `modules` is [(name, module folder)]."""
    defs = Definitions()
    tables = {"Items": ("items", ("Item", "CraftedItem")), "CraftingPieces": ("pieces", ("CraftingPiece",)),
              "EquipmentRosters": ("rosters", ("EquipmentRoster",)),
              "NPCCharacters": ("characters", ("NPCCharacter",))}
    owners = {"items": {}, "pieces": {}, "rosters": {}, "characters": {}}
    for module, module_dir in modules:
        stats = {"registrations": 0, "files": 0, "items": 0, "craftingPieces": 0, "rosters": 0,
                 "characters": 0, "races": 0}
        defs.module_stats[module] = stats
        module_dir = Path(module_dir) if module_dir is not None else None
        if module_dir is None or not module_dir.is_dir():
            defs.miss("MODULE_MISSING", module, "--xml-modules")
            continue
        try:
            if (module_dir / "SubModule.xml").is_file():
                registrations = read_registrations(module_dir)
            else:
                defs.miss("SUBMODULE_MISSING", f"{module}/SubModule.xml", "--xml-modules")
                registrations = []
        except ET.ParseError as exc:
            defs.miss("XML_PARSE_ERROR", f"{module}/SubModule.xml", f"ParseError: {exc}")
            registrations = []
        for xml_id, path in registrations:
            if xml_id not in tables:
                continue
            stats["registrations"] += 1
            files = registered_files(module_dir, path)
            xslt = module_dir / "ModuleData" / f"{path}.xslt"
            if xslt.is_file():
                defs.miss("XSLT_NOT_APPLIED", f"{module}/{path}.xslt", xml_id)
            elif not files:
                defs.miss("REGISTERED_FILE_MISSING", f"{module}/{path}", xml_id)
            attr, tags = tables[xml_id]
            for f in files:
                rel = f"{module}/{f.relative_to(module_dir / 'ModuleData').as_posix()}"
                try:
                    root = ET.fromstring(f.read_bytes())
                except (ET.ParseError, OSError) as exc:
                    defs.miss("XML_PARSE_ERROR", rel, f"{type(exc).__name__}: {exc}")
                    continue
                stats["files"] += 1
                for el in root:
                    if el.tag not in tags or not _attr(el, "id"):
                        continue
                    if attr == "items":
                        obj = _parse_item(el, module, rel, defs)
                        stats["items"] += 1
                    elif attr == "pieces":
                        obj = _parse_piece(el, module, rel)
                        stats["craftingPieces"] += 1
                    elif attr == "rosters":
                        obj = [(_equipment_type(s), _assignments(s)) for s in el.findall("EquipmentSet")]
                        stats["rosters"] += 1
                    else:
                        obj = _parse_character(el, module, rel)
                        stats["characters"] += 1
                    oid = _attr(el, "id")
                    table = getattr(defs, attr)
                    if oid in table:
                        defs.miss("DUPLICATE_DEFINITION", oid, f"{rel} over {owners[attr][oid]}")
                    table[oid] = obj
                    owners[attr][oid] = rel
        skins = module_dir / "ModuleData" / "skins.xml"
        if skins.is_file():
            try:
                stats["races"] = _read_skins(skins, module, defs)
            except (ET.ParseError, OSError) as exc:
                defs.miss("XML_PARSE_ERROR", f"{module}/skins.xml", f"{type(exc).__name__}: {exc}")
    for cid, ch in defs.characters.items():
        for container in ch.equipments:
            for child in container:
                if child.tag in ("EquipmentSet", "equipmentSet") and _roster_id(child) not in defs.rosters:
                    defs.miss("ROSTER_UNRESOLVED", _roster_id(child), cid)
    return defs


def _roster_id(el) -> str:
    """BasicCharacterObject.Deserialize looks an EquipmentSet id up whole (no dot split)."""
    return el.get("id", "")


def item_type(item: ItemDef) -> str:
    """ItemObject.Type as ItemObject.Deserialize settles it: the Type attribute, read ignoring case, replaced by the
    type of the first weapon_class of a Weapon or Banner component (the engine's WeaponComponent). No Type attribute
    leaves Invalid whatever the component. A crafted item has its template's type (UNKNOWN when the audit does not
    know it)."""
    if not item.type:
        return "Invalid"
    if item.tag == "CraftedItem":
        return item.type
    if item.component == "Weapon":
        return WEAPON_CLASS_ITEM_TYPES.get(item.weapon_class, "Invalid")
    return ITEM_TYPE_NAMES.get(item.type.lower(), item.type)


def item_fits_slot(item, slot: str) -> bool:
    """Equipment.IsItemFitsToSlot for a slot name. The audit refuses only what it can prove the engine refuses: the
    engine's null item (an id no definition holds) fits any slot, so does an item whose type the audit cannot say (a
    crafted item on an unknown template), and so does a slot outside FIT_SLOTS."""
    if item is None or slot not in FIT_SLOTS:
        return True
    kind = item_type(item)
    if kind == "UNKNOWN":
        return True
    if kind in WEAPON_ITEM_TYPES:
        return slot == EXTRA_SLOT if item.extra_slot_only else slot in WEAPON_SLOTS
    return ARMOUR_ITEM_SLOTS.get(kind) == slot


def _place(slots: dict, assignments: list, defs: Definitions, etype: str, refused: list) -> None:
    """Equipment.DeserializeNode for each (slot, item id): an item that does not fit the slot is refused and the
    slot keeps what it held."""
    for slot, item_id in assignments:
        if item_fits_slot(defs.items.get(item_id), slot):
            slots[slot] = item_id
        else:
            refused.append((etype, slot, item_id))


def assemble_sets(character: CharDef, defs: Definitions) -> tuple:
    """Every set the character's XML builds, in engine order, and every assignment the engine refuses.

    Returns ([[equipment type, slot -> item id]], [(equipment type, slot, item id)]). Each assignment goes through
    Equipment.DeserializeNode in turn: an inline <EquipmentRoster>'s elements, a standalone roster's set, then the
    <equipment> overrides of the container on every set built so far (MBEquipmentRoster.AddOverriddenEquipments)."""
    sets, refused = [], []      # sets: [equipment type, slots]
    for container in character.equipments:
        for child in container:
            if child.tag == "EquipmentRoster":
                etype, slots = _equipment_type(child), {}
                _place(slots, _assignments(child), defs, etype, refused)
                sets.append([etype, slots])
            elif child.tag in ("EquipmentSet", "equipmentSet"):
                wanted = _equipment_type(child)
                for etype, assignments in defs.rosters.get(_roster_id(child), []):
                    if etype == wanted:
                        slots = {}
                        _place(slots, assignments, defs, etype, refused)
                        sets.append([etype, slots])
        overrides = _assignments(c for c in container if c.tag == "equipment")
        for etype, slots in sets:
            _place(slots, overrides, defs, etype, refused)
    return sets, refused


def _kept_types(include_civilian: bool) -> tuple:
    return ("Battle", "Civilian") if include_civilian else ("Battle",)


def battle_sets(character: CharDef, defs: Definitions, include_civilian: bool = False) -> list:
    """One slot -> item id dict per Battle set (and Civilian set when asked), in engine order."""
    sets, _refused = assemble_sets(character, defs)
    return [slots for etype, slots in sets if etype in _kept_types(include_civilian)]


def slot_rejections(character: CharDef, defs: Definitions, include_civilian: bool = False) -> list:
    """The distinct (slot, item id) pairs the engine refuses in the sets the preload walks, in first-seen order."""
    _sets, refused = assemble_sets(character, defs)
    out = []
    for etype, slot, item_id in refused:
        if etype in _kept_types(include_civilian) and (slot, item_id) not in out:
            out.append((slot, item_id))
    return out


# --------------------------------------------------------------------------- #
# item -> asset names (the engine's preload rule)
# --------------------------------------------------------------------------- #
@dataclass
class ItemNames:
    item_id: str
    mesh_candidates: list = field(default_factory=list)   # [(names tried in order, required)]
    materials: list = field(default_factory=list)
    bodies: list = field(default_factory=list)
    misses: list = field(default_factory=list)            # (reason, subject, referenced_by)


def _gender_lists(m: str, armour: bool, gender_variations: bool) -> list:
    """GetMultiMesh for (male, not slim), and for armour (male, slim), (female, not slim),
    (female, slim); a female call passes isFemale only when the armour has gender variations."""
    if not m:
        return []
    lists = [(f"{m}_male", m)]
    if armour:
        lists.append((f"{m}_male", f"{m}_slim", m))
        if gender_variations:
            lists.append((f"{m}_female", f"{m}_converted", m))
            lists.append((f"{m}_female", f"{m}_converted_slim", m))
        else:
            lists.append((f"{m}_male", m))
            lists.append((f"{m}_male", f"{m}_slim", m))
    return lists


def item_asset_names(item: ItemDef, defs: Definitions) -> ItemNames:
    """The metamesh candidate lists, horse materials and bodies PreloadHelper.AddItemObject
    registers for one item (see ENGINE RULES)."""
    out = ItemNames(item.id)

    def req(name):
        if name:
            out.mesh_candidates.append(((name,), True))

    if item.tag == "CraftedItem":
        blade = None
        for piece_id, piece_slot in item.pieces:
            piece = defs.pieces.get(piece_id)
            if piece is None:
                out.misses.append(("PIECE_UNRESOLVED", piece_id, item.id))
                continue
            req(piece.mesh)
            if blade is None and (piece_slot == "Blade" or (not piece_slot and piece.piece_type == "Blade")):
                blade = piece
        if blade is not None:
            if item.crafting_template not in WEAPON_AS_HOLSTER_TEMPLATES:
                req(blade.blade_holster_mesh)
            if blade.blade_body_name:
                out.bodies.append(blade.blade_body_name)
            holster_body = blade.blade_holster_body_name or blade.blade_body_name
            if holster_body:
                out.bodies.append(holster_body)
        return out

    m = item.mesh
    req(m)
    req(item.holster_mesh)
    armour = item.component == "Armor"
    if item.component == "Weapon":
        req(item.holster_mesh_with_weapon)
        req(item.flying_mesh)
    elif item.component == "Horse":
        for name in item.additional_meshes:
            req(name)
        out.materials = list(item.horse_materials)
    out.mesh_candidates.extend((names, False) for names in _gender_lists(m, armour, item.has_gender_variations))
    if armour and item.reins_mesh:
        req(item.reins_mesh)
        out.mesh_candidates.append(((f"{item.reins_mesh}_rope",), False))
    out.bodies = [b for b in (item.shield_body_name, item.body_name, item.holster_body_name) if b]
    return out


def resolve_names(names: ItemNames, index) -> tuple:
    """(metamesh items, physics items, misses), each asset and each miss once, first-found order."""
    metas, bodies, misses = [], [], []
    seen = set()

    def add_miss(row):
        if row not in misses:
            misses.append(row)

    for row in names.misses:
        add_miss(row)
    for candidates, required in names.mesh_candidates:
        found = None
        for name in candidates:
            found = index.get("metamesh", name)
            if found is not None:
                break
        if found is None:
            if required:
                add_miss(("MESH_UNRESOLVED", candidates[0], names.item_id))
            continue
        key = ("metamesh", found.name.lower())
        if key not in seen:
            seen.add(key)
            metas.append(found)
    for name in names.bodies:
        found = index.get("physics", name)
        if found is None:
            add_miss(("BODY_UNRESOLVED", name, names.item_id))
            continue
        key = ("physics", found.name.lower())
        if key not in seen:
            seen.add(key)
            bodies.append(found)
    return metas, bodies, misses


# --------------------------------------------------------------------------- #
# sizes and asset sets
# --------------------------------------------------------------------------- #
SMALL_ITEM_TYPES = frozenset({"HeadArmor", "HandArmor", "LegArmor", "Cape", "Arrows", "Bolts", "Thrown",
                              "OneHandedWeapon", "Shield"})
DEFAULT_FACE_BUDGET = 20000
DEFAULT_BIG_TEXTURE = 4096
_LOD_RE = re.compile(r"\.lod(\d+)", re.IGNORECASE)


def asset_floor(value: dict) -> int:
    """Render buffers, texture chains and collision bodies: the floor of what a battle holds."""
    return value["render_bytes"] + value["texture_bytes"] + value["physics_bytes"]


def asset_upper(value: dict) -> int:
    """The floor plus edit data and every other mesh segment: the upper bound."""
    return asset_floor(value) + value["edit_bytes"] + value["other_bytes"]


class AssetSet(dict):
    """(kind, lowercase name) -> that asset's value (sizes, metadata, intrinsic flags).

    A key reached twice counts once in every total. `misses` carries the unresolved rows met
    while building the set, each once.
    """

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.misses = []
        self.meshes = []        # an item's resolved metamesh names (item sets only)
        self.bodies = []        # an item's resolved body names (item sets only)

    def add_miss(self, row) -> None:
        if row not in self.misses:
            self.misses.append(row)

    def merge(self, other) -> None:
        for key, value in other.items():
            self.setdefault(key, value)
        for row in other.misses:
            self.add_miss(row)

    def total(self, name: str) -> int:
        return sum(v[name] for v in self.values())

    @property
    def floor_bytes(self) -> int:
        return sum(asset_floor(v) for v in self.values())

    @property
    def upper_bytes(self) -> int:
        return sum(asset_upper(v) for v in self.values())


def union(*sets) -> AssetSet:
    out = AssetSet()
    for s in sets:
        out.merge(s)
    return out


def _value(item, kind: str) -> dict:
    return {"kind": kind, "name": item.name, "module": item.module, "pack": item.pack,
            "render_bytes": 0, "edit_bytes": 0, "other_bytes": 0, "texture_bytes": 0, "physics_bytes": 0,
            "format": "", "width": "", "height": "", "mips": "", "lod0_faces": "", "flags": []}


def _where(item) -> str:
    return f"{item.module}/{item.pack}"


_CACHES: "weakref.WeakKeyDictionary" = weakref.WeakKeyDictionary()


def _cache(index) -> dict:
    cache = _CACHES.get(index)
    if cache is None:
        cache = _CACHES[index] = {}
    return cache


def _sized_ascii(guid: bytes) -> bool:
    """True when 16 guid bytes read as a sized string: a little endian u32 length of 1 to 255,
    then 12 printable ASCII bytes. A misread layout yields this; a real guid all but never does."""
    return (len(guid) == 16 and 1 <= struct.unpack_from("<I", guid)[0] <= 255
            and all(0x20 <= b < 0x7f for b in guid[4:]))


def mesh_sizes(item, index) -> dict:
    """Segment bytes, LOD 0 faces (None when a LOD 0 record does not decode), material guids, and
    the (record name, guid) of each record whose material guid is in no indexed pack. Misread
    guids are left out: a record whose counts do not decode, and a "guid" that reads as a sized
    string (a cloth record's extra `uses_cloth_s...` string shifts the layout, and its misread
    counts can still look plausible). The guid scan still finds such a mesh's real materials."""
    render = item.seg_bytes(amsm.SEG_MESH_B)
    edit = item.seg_bytes(amsm.SEG_MESH_A)
    lod0 = 0
    records = amsm.parse_metamesh_records(item.meta, item.name)
    lost = [(rec.name, rec.material_guid) for rec in records
            if rec.material_guid not in (amsm.ZERO_GUID, b"") and rec.material_guid not in index.by_guid
            and 0 < rec.faces <= 20_000_000 and 0 < rec.positions <= 20_000_000
            and not _sized_ascii(rec.material_guid)]
    for rec in records:
        tail = rec.name[len(item.name):]
        m = _LOD_RE.search(tail)
        if m and int(m.group(1)) != 0:
            continue
        if not amsm.plausible_counts(rec, index.by_guid):
            lod0 = None
            break
        lod0 += rec.faces
    if not records:
        lod0 = None
    return {"render_bytes": render, "edit_bytes": edit, "other_bytes": item.seg_bytes() - render - edit,
            "lod0_faces": lod0, "has_render": item.has_segment(amsm.SEG_MESH_B),
            "materials": amsm.scan_guids(item.meta, index.by_guid, "material"), "lost_materials": lost}


def _texture_assets(tex, index) -> AssetSet:
    cache = _cache(index)
    key = ("texture", tex.name.lower())
    if key not in cache:
        row = amsm._texture_row(tex, index, "")
        v = _value(tex, "texture")
        v.update(texture_bytes=row["resident_bytes"], format=row["format"], width=row["width"],
                 height=row["height"], mips=row["mips"], flags=list(row["flags"]))
        out = AssetSet({key: v})
        if row["size_source"].startswith("formula"):
            out.add_miss(("TEXTURE_SIZE_FROM_HEADER", tex.name, _where(tex)))
        elif row["size_source"] != "pixel_segment":
            out.add_miss(("TEXTURE_SIZE_UNKNOWN", tex.name, _where(tex)))
        cache[key] = out
    return cache[key]


def material_assets(mat, index) -> AssetSet:
    """A material and the textures it names (a guid scan when its layout does not decode)."""
    cache = _cache(index)
    key = ("material", mat.name.lower())
    if key in cache:
        return cache[key]
    out = AssetSet({key: _value(mat, "material")})
    try:
        guids = [g for _slot, g in amsm.parse_material_meta(mat.meta).textures]
    except (ValueError, struct.error, IndexError):
        out.add_miss(("MATERIAL_UNDECODED", mat.name, _where(mat)))
        guids = sorted(amsm.scan_guids(mat.meta, index.by_guid, "texture"))
    for g in guids:
        if g == amsm.ZERO_GUID:
            continue
        tex = index.by_guid.get(g)
        if tex is None or tex.kind != "texture":
            out.add_miss(("TEXTURE_UNRESOLVED", g.hex(), mat.name))
            continue
        # by_guid keeps the first copy of a guid (an AssetPackages stub); the name index holds
        # its same-guid twin with the pixel segment when one exists
        twin = index.get("texture", tex.name)
        if twin is not None and twin.guid == tex.guid:
            tex = twin
        out.merge(_texture_assets(tex, index))
    cache[key] = out
    return out


def mesh_assets(item, index) -> AssetSet:
    """A metamesh, its materials and their textures."""
    cache = _cache(index)
    key = ("metamesh", item.name.lower())
    if key in cache:
        return cache[key]
    s = mesh_sizes(item, index)
    v = _value(item, "metamesh")
    v.update(render_bytes=s["render_bytes"], edit_bytes=s["edit_bytes"], other_bytes=s["other_bytes"],
             lod0_faces=s["lod0_faces"])
    out = AssetSet({key: v})
    if not s["has_render"]:
        v["flags"].append("RENDER_BUFFERS_NOT_IN_TREE")
        out.add_miss(("RENDER_BUFFERS_NOT_IN_TREE", item.name, _where(item)))
    if s["lod0_faces"] is None:
        out.add_miss(("LOD0_FACES_UNKNOWN", item.name, _where(item)))
    for rec_name, g in s["lost_materials"]:
        out.add_miss(("MATERIAL_GUID_UNRESOLVED", g.hex(), f"{item.name}/{rec_name}"))
    for g in sorted(s["materials"]):
        out.merge(material_assets(index.by_guid[g], index))
    cache[key] = out
    return out


def _physics_assets(body) -> AssetSet:
    v = _value(body, "physics")
    v["physics_bytes"] = body.seg_bytes()
    return AssetSet({("physics", body.name.lower()): v})


def item_assets(item_id: str, defs: Definitions, index) -> AssetSet:
    """One item's metameshes, their materials and textures, its bodies, and horse materials.
    An id no definition holds gives an empty set (the troop reports ITEM_UNRESOLVED)."""
    key = ("item", id(index), item_id)
    if key in defs.cache:
        return defs.cache[key]
    out = AssetSet()
    item = defs.items.get(item_id)
    if item is not None:
        names = item_asset_names(item, defs)
        metas, bodies, misses = resolve_names(names, index)
        for row in misses:
            out.add_miss(row)
        for m in metas:
            out.merge(mesh_assets(m, index))
        for b in bodies:
            out.merge(_physics_assets(b))
        for name in names.materials:
            mat = index.get("material", name)
            if mat is None:
                out.add_miss(("MATERIAL_UNRESOLVED", name, item.id))
            else:
                out.merge(material_assets(mat, index))
        out.meshes = sorted((m.name for m in metas), key=str.lower)
        out.bodies = sorted((b.name for b in bodies), key=str.lower)
    defs.cache[key] = out
    return out


def race_assets(character: CharDef, defs: Definitions, index) -> AssetSet:
    """The adult skin of the character's race and gender: body meshes, every hair and beard
    name and cover type (an upper bound; the face key picks one per agent)."""
    gender = "1" if character.is_female else "0"
    race = defs.races.get((character.race, gender))
    if race is None:
        out = AssetSet()
        out.add_miss(("RACE_UNRESOLVED", f"{character.race}/{gender}", character.id))
        return out
    key = ("race", id(index), character.race, gender)
    if key in defs.cache:
        return defs.cache[key]
    out = AssetSet()
    names = []
    for name in list(race.body.values()) + race.hair + race.beards:
        if name not in names:
            names.append(name)
    for name in names:
        m = index.get("metamesh", name)
        if m is None:
            out.add_miss(("RACE_MESH_UNRESOLVED", name, f"race:{character.race}/{gender}"))
        else:
            out.merge(mesh_assets(m, index))
    defs.cache[key] = out
    return out


def troop_item_ids(character: CharDef, defs: Definitions, include_civilian: bool = False) -> list:
    """Distinct non-empty item ids over the sets the preload walks, in first-seen order."""
    ids = []
    for s in battle_sets(character, defs, include_civilian):
        for item_id in s.values():
            if item_id and item_id not in ids:
                ids.append(item_id)
    return ids


def troop_assets(character: CharDef, defs: Definitions, index, include_civilian: bool = False) -> AssetSet:
    out = AssetSet()
    for slot, item_id in slot_rejections(character, defs, include_civilian):
        out.add_miss(("ITEM_SLOT_REJECTED", item_id, f"{character.id}:{slot}"))
    for item_id in troop_item_ids(character, defs, include_civilian):
        if item_id not in defs.items:
            out.add_miss(("ITEM_UNRESOLVED", item_id, character.id))
            continue
        out.merge(item_assets(item_id, defs, index))
    out.merge(race_assets(character, defs, index))
    return out


def culture_assets(culture: str, troops: list, defs: Definitions, index, include_civilian: bool = False) -> AssetSet:
    return union(*(troop_assets(t, defs, index, include_civilian) for t in troops if t.culture == culture))


def side_assets(troops: list, defs: Definitions, index, include_civilian: bool = False) -> AssetSet:
    return union(*(troop_assets(t, defs, index, include_civilian) for t in troops))


def battle_assets(sides: list) -> AssetSet:
    return union(*sides)


def shared_floor_bytes(side: AssetSet, other_sides: list) -> int:
    """Floor bytes of the side's assets that any other side also holds."""
    return sum(asset_floor(v) for k, v in side.items() if any(k in o for o in other_sides))


def population(defs: Definitions, include_heroes: bool = False, include_civilian: bool = False) -> list:
    """TAOM's own characters: not templates, not heroes unless asked, with a set to preload."""
    out = []
    for ch in defs.characters.values():
        if ch.module.lower() != "taom" or ch.is_template or (ch.is_hero and not include_heroes):
            continue
        if battle_sets(ch, defs, include_civilian):
            out.append(ch)
    return out


def asset_flags(value: dict, item_types, face_budget: int = DEFAULT_FACE_BUDGET,
                big_texture: int = DEFAULT_BIG_TEXTURE) -> list:
    """The asset's intrinsic flags plus the budget flags for the items that reach it."""
    flags = list(value["flags"])
    if value["kind"] == "metamesh":
        if value["lod0_faces"] is None:
            flags.append("LOD0_FACES_UNKNOWN")
        elif value["lod0_faces"] > face_budget:
            flags.append("LOD0_OVER_FACE_BUDGET")
    elif value["kind"] == "texture":
        dims = [d for d in (value["width"], value["height"]) if isinstance(d, int)]
        if dims and max(dims) >= big_texture and SMALL_ITEM_TYPES.intersection(item_types):
            flags.append("TEXTURE_4K_ON_SMALL_ITEM")
    return flags


# --------------------------------------------------------------------------- #
# run log, outputs and CLI
# --------------------------------------------------------------------------- #
DEFAULT_RELEASE_MODULES = r"E:\LOTRAOM_Releases\testing\Modules"
DEFAULT_OUT = str(Path(amsm.DEFAULT_OUT).parent / "battle-equipment")
DEFAULT_XML_MODULES = ("Native", "SandBoxCore", "SandBox", "LOTRLOME_Armory", "TAOM")
DEFAULT_PACK_MODULES = ("LOTRLOME_Armory", "TAOM", "Native", "SandBox")
LOG_PREFIX = "[EquipMemAudit]"
SAMPLES = 5

REASONS = {
    "MODULE_MISSING": "the module is in neither the release folder nor the game install; "
                      "nothing it defines or packs is read",
    "SUBMODULE_MISSING": "the module folder has no SubModule.xml; none of its registrations is read",
    "REGISTERED_FILE_MISSING": "the registration loads nothing; its definitions are absent",
    "XML_PARSE_ERROR": "the file does not parse; its definitions are absent",
    "XSLT_NOT_APPLIED": "the transform the engine applies at load is not applied; its edits are not modelled",
    "DUPLICATE_DEFINITION": "the later definition is kept whole; the engine merges by XSD rules",
    "CRAFTING_TEMPLATE_UNKNOWN": "the crafted item's type reads UNKNOWN; its pieces still resolve",
    "ROSTER_UNRESOLVED": "the referenced equipment roster adds no set",
    "ITEM_UNRESOLVED": "the slot's item is defined nowhere and is not counted",
    "ITEM_SLOT_REJECTED": "the engine refuses an item whose type does not fit the slot (Equipment.DeserializeNode, "
                          "IsItemFitsToSlot) and the slot keeps what it held; the item still counts for the troop if "
                          "another of its slots or sets holds it",
    "PIECE_UNRESOLVED": "the crafting piece is defined nowhere; its mesh and blade data are not counted",
    "MESH_UNRESOLVED": "a metamesh the definition names is in no indexed pack and is not counted",
    "BODY_UNRESOLVED": "a collision body the item names is in no indexed pack and is not counted",
    "MATERIAL_UNRESOLVED": "a horse material is in no indexed pack; its textures are not counted",
    "MATERIAL_UNDECODED": "the material layout did not decode; its textures come from a guid scan",
    "TEXTURE_UNRESOLVED": "a texture the material names is in no indexed pack and is not counted",
    "MATERIAL_GUID_UNRESOLVED": "a mesh record's decoded material guid is in no indexed pack (or was "
                                "misread); that material's textures are not counted",
    "TEXTURE_SIZE_FROM_HEADER": "no pixel segment in the packs; the texture is sized by the header's "
                                "mip chain formula",
    "TEXTURE_SIZE_UNKNOWN": "no pixel segment and the header did not size the texture; it counts at its "
                            "stub segment's bytes, or 0 without one, so every total that holds it is a lower bound",
    "RACE_UNRESOLVED": "no adult skin for that race and gender; body, hair and beard meshes are not counted",
    "RACE_MESH_UNRESOLVED": "a skin mesh is in no indexed pack and is not counted",
    "RENDER_BUFFERS_NOT_IN_TREE": "the metamesh has no render buffer segment; it adds 0 to the floor "
                                  "and its edit data to the upper bound only",
    "LOD0_FACES_UNKNOWN": "a LOD 0 record did not decode; the face budget check skips that mesh",
    "PACK_ERROR": "that pack or module tree is not indexed; assets only it holds are unresolved",
    "COOKED_TREE_NOT_READ": "the module ships a loose Assets tree, which the engine loads instead of its cooked "
                            "packs (--loose-assets); names only the cooked tree holds are unresolved",
}
TSV_FILES = ("assets.tsv", "items.tsv", "troops.tsv", "cultures.tsv", "sides.tsv", "unresolved.tsv")


class RunLog:
    """Each line to stdout and, flushed line by line, to <tsv-dir>/run.log."""

    def __init__(self, path):
        self.lines = []
        self._f = open(path, "w", encoding="utf-8", newline="\n")

    def __call__(self, text: str) -> None:
        line = f"{LOG_PREFIX} {text}"
        print(line, flush=True)
        self._f.write(line + "\n")
        self._f.flush()
        self.lines.append(line)

    def close(self) -> None:
        self._f.close()


def _doc_section(name: str) -> str:
    """One section of this module's docstring (the report reuses APPROXIMATIONS verbatim)."""
    doc = __doc__ or ""
    start = doc.find(f"\n{name}\n")
    if start < 0:
        return ""
    body = doc[start + len(name) + 2:]
    nxt = re.search(r"\n[A-Z][A-Z ]+\n", body)
    return body[:nxt.start()] if nxt else body


def _read_troop_file(path) -> list:
    ids = []
    for raw in Path(path).read_text(encoding="utf-8-sig").splitlines():
        line = raw.split("#", 1)[0].strip()
        if line and line not in ids:
            ids.append(line)
    return ids


def _abort(log, code: str, detail: str) -> int:
    """Log why the run stops (an input the audit cannot use) and return exit code 2."""
    log(f"abort reason={code} detail={detail}")
    return 2


def _locate(name: str, release: Path, game_modules: Path):
    for root, label in ((release, "release"), (game_modules, "game")):
        if (root / name).is_dir():
            return root / name, label
    return None, "missing"


def _remove_previous_outputs(report: Path, tsv_dir: Path) -> None:
    """Delete what an earlier run left: the report and the TSVs (run.log is rewritten anyway), so a folder never
    mixes two runs. Without this a run without sides keeps the last run's sides.tsv, and a run that aborts sits
    beside the last run's tables. Only these files go, never anything else in the folder."""
    for path in [report] + [tsv_dir / name for name in TSV_FILES]:
        try:
            path.unlink()
        except FileNotFoundError:
            pass


def _defined_items(character, defs, include_civilian) -> list:
    return [i for i in troop_item_ids(character, defs, include_civilian) if i in defs.items]


def _counts(s: AssetSet) -> dict:
    kinds = {"metamesh": 0, "material": 0, "texture": 0, "physics": 0}
    for kind, _name in s:
        kinds[kind] += 1
    return kinds


def _samples(ordered: dict) -> str:
    return ";".join(list(ordered)[:SAMPLES])


def main(argv=None) -> int:
    started = time.perf_counter()
    ap = argparse.ArgumentParser(description="Rank the equipment assets a battle preloads, from the release packs.")
    ap.add_argument("--release-modules", default=DEFAULT_RELEASE_MODULES,
                    help="release channel Modules folder, searched first (default the testing channel)")
    ap.add_argument("--game-dir", default=game_dir(amsm.DEFAULT_GAME), help="Bannerlord install root")
    ap.add_argument("--xml-modules", default=",".join(DEFAULT_XML_MODULES),
                    help="modules whose ModuleData is read, in load order (later wins)")
    ap.add_argument("--pack-modules", default=",".join(DEFAULT_PACK_MODULES),
                    help="modules whose packs are indexed, in resolution priority (first wins)")
    ap.add_argument("--loose-assets", action="store_true",
                    help="read each pack module's loose Assets tree instead of its pack trees when it has one, as "
                         "the engine does (a module with no loose tpac keeps its packs); a loose texture whose "
                         "header is not version 3 has no size and counts 0 bytes, so loose totals are lower bounds")
    sides_group = ap.add_mutually_exclusive_group()
    sides_group.add_argument("--troops", action="append", default=[],
                             help="a side: a file of troop ids, one per line, '#' comments (repeatable)")
    sides_group.add_argument("--culture", action="append", default=[],
                             help="a side: the population troops of one culture (repeatable)")
    ap.add_argument("--include-civilian", action="store_true",
                    help="add civilian sets (missions that require civilian equipment)")
    ap.add_argument("--include-heroes", action="store_true", help="add is_hero characters to the population")
    ap.add_argument("--face-budget", type=int, default=DEFAULT_FACE_BUDGET, help="LOD 0 face budget per mesh")
    ap.add_argument("--big-texture", type=int, default=DEFAULT_BIG_TEXTURE,
                    help="texture edge that counts as big on a small item")
    ap.add_argument("--out-dir", default=DEFAULT_OUT, help="default folder for the report and TSVs")
    ap.add_argument("--report", default=None, help="Markdown report (default <out-dir>/battle-equipment-memory.md)")
    ap.add_argument("--tsv-dir", default=None, help="TSV and run.log folder (default <out-dir>)")
    ap.add_argument("--top", type=int, default=50, help="rows in the top-N tables")
    args = ap.parse_args(argv)

    release = Path(args.release_modules)
    game_root = Path(args.game_dir)
    game_modules = game_root / "Modules"
    if not release.is_dir():
        print(f"release modules folder not found: {release}")
        return 2
    if not game_modules.is_dir():
        print(f"game Modules folder not found: {game_modules}; is the game install path right?")
        return 2
    out_dir = Path(args.out_dir)
    report = Path(args.report) if args.report else out_dir / "battle-equipment-memory.md"
    tsv_dir = Path(args.tsv_dir) if args.tsv_dir else out_dir
    for target in (out_dir, report, tsv_dir):
        for root in (game_root, release):
            amsm.refuse_inside_game(target, root)
    report.parent.mkdir(parents=True, exist_ok=True)
    tsv_dir.mkdir(parents=True, exist_ok=True)
    _remove_previous_outputs(report, tsv_dir)

    log = RunLog(tsv_dir / "run.log")
    try:
        return _run(args, release, game_modules, report, tsv_dir, log, started)
    finally:
        log.close()


def _run(args, release, game_modules, report, tsv_dir, log, started) -> int:
    xml_modules = [m.strip() for m in args.xml_modules.split(",") if m.strip()]
    pack_modules = [m.strip() for m in args.pack_modules.split(",") if m.strip()]
    trees = amsm.PACK_TREES + ("Assets",) if args.loose_assets else amsm.PACK_TREES
    civ = args.include_civilian
    n_sides = len(args.troops) or len(args.culture)
    log(f"config releaseModules={release} gameModules={game_modules} xmlModules={','.join(xml_modules)} "
        f"packModules={','.join(pack_modules)} trees={'+'.join(trees)} sides={n_sides} "
        f"civilian={'on' if civ else 'off'} heroes={'on' if args.include_heroes else 'off'} "
        f"faceBudget={args.face_budget} bigTexture={args.big_texture}")

    troop_files = []    # (path, [troop id])
    for path in args.troops:
        try:
            troop_files.append((path, _read_troop_file(path)))
        except (OSError, UnicodeDecodeError) as exc:
            return _abort(log, "TROOP_FILE_UNREADABLE", f"{path}: {type(exc).__name__}: {exc}")

    located = {m: _locate(m, release, game_modules) for m in dict.fromkeys(xml_modules + pack_modules)}
    defs = load_definitions([(m, located[m][0]) for m in xml_modules])
    for m in xml_modules:
        st = defs.module_stats[m]
        log(f"module name={m} root={located[m][1]} registrations={st['registrations']} files={st['files']} "
            f"items={st['items']} craftingPieces={st['craftingPieces']} rosters={st['rosters']} "
            f"characters={st['characters']} races={st['races']}")

    index = amsm.AssetIndex()
    extra_rows = []
    for m in pack_modules:
        path = located[m][0]
        errors_before = len(index.errors)
        if path is None:
            extra_rows.append(("MODULE_MISSING", m, "--pack-modules"))
        else:
            index.add_module(m, path, trees=trees)
        for mod, label, err in index.errors[errors_before:]:
            subject = mod if Path(label).is_absolute() else f"{mod}/{label}"
            extra_rows.append(("PACK_ERROR", subject, err))
        c = index.counts
        log(f"index module={m} packs={sum(1 for p in index.packs if p[0] == m)} metameshes={c[(m, 'metamesh')]} "
            f"materials={c[(m, 'material')]} textures={c[(m, 'texture')]} physics={c[(m, 'physics')]} "
            f"errors={len(index.errors) - errors_before}")
    extra_rows.extend(("COOKED_TREE_NOT_READ", f"{mod}/{tree}", "--loose-assets")
                      for mod, tree in index.skipped_trees)

    # sides and the selected troops
    pop = population(defs, args.include_heroes, civ)
    sides = []          # (label, [CharDef])
    for path, ids in troop_files:
        unknown = [i for i in ids if i not in defs.characters]
        if unknown:
            return _abort(log, "TROOP_UNKNOWN", f"{path} names troop ids no module defines: {', '.join(unknown)}")
        sides.append((Path(path).stem, [defs.characters[i] for i in ids]))
    for culture in args.culture:
        sides.append((culture, [c for c in pop if c.culture == culture]))
    for label, troops in sides:
        if not troops:
            return _abort(log, "SIDE_EMPTY", f"side {label} selects no troops")
    selected = list({c.id: c for _l, ts in sides for c in ts}.values()) if sides else pop
    if not selected:
        return _abort(log, "NO_TROOPS_SELECTED", "no TAOM character that is not a template (nor a hero, "
                      "without --include-heroes) has a set to preload; check --xml-modules")

    troop_sets = {c.id: troop_assets(c, defs, index, civ) for c in selected}
    total = union(*troop_sets.values())

    unresolved = []
    seen = set()
    for row in defs.unresolved + extra_rows + [r for s in troop_sets.values() for r in s.misses]:
        if row not in seen:
            seen.add(row)
            unresolved.append(row)
    by_reason = collections.OrderedDict()
    for row in unresolved:
        by_reason.setdefault(row[0], []).append(row)
    for code in sorted(by_reason):
        rows = by_reason[code]
        log(f"fallback reason={code} count={len(rows)} first={rows[0][1]} consequence={REASONS[code]}")

    side_sets = [(label, troops, union(*(troop_sets[c.id] for c in troops))) for label, troops in sides]
    side_rows = []
    for i, (label, troops, s) in enumerate(side_sets):
        others = [o for j, (_l, _t, o) in enumerate(side_sets) if j != i]
        shared = shared_floor_bytes(s, others)
        items = {i2 for c in troops for i2 in _defined_items(c, defs, civ)}
        side_rows.append({"side": label, "troops": len(troops), "floor_bytes": s.floor_bytes,
                          "upper_bytes": s.upper_bytes, "shared_floor_bytes": shared})
        log(f"side name={label} troops={len(troops)} items={len(items)} floorBytes={s.floor_bytes} "
            f"upperBytes={s.upper_bytes} sharedFloorBytes={shared}")
    battle_row = None
    if side_sets:
        battle = battle_assets([s for _l, _t, s in side_sets])
        battle_row = {"sides": len(side_sets), "troops": len(selected), "floor_bytes": battle.floor_bytes,
                      "upper_bytes": battle.upper_bytes,
                      "sum_of_sides_floor_bytes": sum(s.floor_bytes for _l, _t, s in side_sets)}
        log(f"battle sides={battle_row['sides']} troops={battle_row['troops']} floorBytes={battle.floor_bytes} "
            f"upperBytes={battle.upper_bytes} sumOfSidesFloorBytes={battle_row['sum_of_sides_floor_bytes']}")

    # usage maps: asset -> items, troops, cultures; item -> troops
    asset_items = collections.defaultdict(dict)
    asset_types = collections.defaultdict(set)
    asset_troops = collections.defaultdict(dict)
    asset_cultures = collections.defaultdict(dict)
    item_troops = collections.defaultdict(dict)
    for c in selected:
        for item_id in _defined_items(c, defs, civ):
            item_troops[item_id][c.id] = None
            for key in item_assets(item_id, defs, index):
                asset_items[key][item_id] = None
                asset_types[key].add(defs.items[item_id].type)
        for key in troop_sets[c.id]:
            asset_troops[key][c.id] = None
            asset_cultures[key][c.culture] = None

    counts = _counts(total)
    max_troop = max(selected, key=lambda c: troop_sets[c.id].floor_bytes, default=None)
    max_asset = max(total.values(), key=asset_floor, default=None)
    elapsed = time.perf_counter() - started
    log(f"summary troops={len(selected)} items={len(item_troops)} metameshes={counts['metamesh']} "
        f"textures={counts['texture']} physics={counts['physics']} renderBytes={total.total('render_bytes')} "
        f"editBytes={total.total('edit_bytes')} otherMeshBytes={total.total('other_bytes')} "
        f"textureBytes={total.total('texture_bytes')} physicsBytes={total.total('physics_bytes')} "
        f"floorBytes={total.floor_bytes} upperBytes={total.upper_bytes} "
        f"maxTroop={(max_troop.id if max_troop else 'none')}:{troop_sets[max_troop.id].floor_bytes if max_troop else 0} "
        f"maxAsset={(max_asset['kind'] + ':' + max_asset['name']) if max_asset else 'none:none'}:"
        f"{asset_floor(max_asset) if max_asset else 0} unresolved={len(unresolved)} elapsedS={elapsed:.1f}")

    # rows
    asset_rows = []
    for key, v in total.items():
        lod0 = v["lod0_faces"]
        asset_rows.append({
            "kind": v["kind"], "name": v["name"], "module": v["module"], "pack": v["pack"],
            "floor_bytes": asset_floor(v), "edit_bytes": v["edit_bytes"], "other_bytes": v["other_bytes"],
            "format": v["format"], "width": v["width"], "height": v["height"], "mips": v["mips"],
            "lod0_faces": "UNKNOWN" if lod0 is None else lod0,
            "flags": ";".join(asset_flags(v, asset_types[key], args.face_budget, args.big_texture)),
            "items": len(asset_items[key]), "troops": len(asset_troops[key]), "cultures": len(asset_cultures[key]),
            "sample_items": _samples(asset_items[key]), "sample_troops": _samples(asset_troops[key])})
    asset_rows.sort(key=lambda r: (-r["floor_bytes"], r["kind"], r["name"].lower()))
    item_rows = []
    for item_id, troops in item_troops.items():
        it, s = defs.items[item_id], item_assets(item_id, defs, index)
        item_rows.append({"item_id": item_id, "module": it.module, "kind": it.tag, "type": it.type,
                          "meshes": ";".join(s.meshes), "bodies": ";".join(s.bodies),
                          "floor_bytes": s.floor_bytes, "upper_bytes": s.upper_bytes, "troops": len(troops),
                          "unresolved": len(s.misses)})
    item_rows.sort(key=lambda r: (-r["floor_bytes"], r["item_id"]))
    troop_rows = []
    for c in selected:
        s = troop_sets[c.id]
        troop_rows.append({"troop_id": c.id, "module": c.module, "culture": c.culture, "race": c.race,
                           "is_hero": "true" if c.is_hero else "false",
                           "battle_sets": len(battle_sets(c, defs, civ)), "items": len(_defined_items(c, defs, civ)),
                           "floor_bytes": s.floor_bytes, "upper_bytes": s.upper_bytes,
                           "race_floor_bytes": race_assets(c, defs, index).floor_bytes, "unresolved": len(s.misses)})
    troop_rows.sort(key=lambda r: (-r["floor_bytes"], r["troop_id"]))
    culture_rows = []
    for culture in dict.fromkeys(c.culture for c in selected):
        members = [c for c in selected if c.culture == culture]
        s = culture_assets(culture, members, defs, index, civ)
        k = _counts(s)
        culture_rows.append({
            "culture": culture, "troops": len(members),
            "items": len({i for c in members for i in _defined_items(c, defs, civ)}),
            "metameshes": k["metamesh"], "textures": k["texture"], "physics": k["physics"],
            "render_bytes": s.total("render_bytes"), "edit_bytes": s.total("edit_bytes"),
            "texture_bytes": s.total("texture_bytes"), "physics_bytes": s.total("physics_bytes"),
            "floor_bytes": s.floor_bytes, "upper_bytes": s.upper_bytes})
    culture_rows.sort(key=lambda r: (-r["floor_bytes"], r["culture"]))

    amsm.write_tsv(tsv_dir / "assets.tsv", asset_rows, ASSET_COLUMNS)
    amsm.write_tsv(tsv_dir / "items.tsv", item_rows, ITEM_COLUMNS)
    amsm.write_tsv(tsv_dir / "troops.tsv", troop_rows, TROOP_COLUMNS)
    amsm.write_tsv(tsv_dir / "cultures.tsv", culture_rows, CULTURE_COLUMNS)
    if side_rows:
        amsm.write_tsv(tsv_dir / "sides.tsv", side_rows, SIDE_COLUMNS)
    amsm.write_tsv(tsv_dir / "unresolved.tsv",
                   [{"reason": r, "subject": s, "referenced_by": b} for r, s, b in unresolved],
                   ["reason", "subject", "referenced_by"])
    write_report(report, log.lines, side_rows, battle_row, culture_rows, asset_rows, item_rows, troop_rows,
                 by_reason, args.top)
    print(f"report: {report}")
    print(f"tsv:    {tsv_dir}")
    return 0


ASSET_COLUMNS = ["kind", "name", "module", "pack", "floor_bytes", "edit_bytes", "other_bytes", "format", "width",
                 "height", "mips", "lod0_faces", "flags", "items", "troops", "cultures", "sample_items",
                 "sample_troops"]
ITEM_COLUMNS = ["item_id", "module", "kind", "type", "meshes", "bodies", "floor_bytes", "upper_bytes", "troops",
                "unresolved"]
TROOP_COLUMNS = ["troop_id", "module", "culture", "race", "is_hero", "battle_sets", "items", "floor_bytes",
                 "upper_bytes", "race_floor_bytes", "unresolved"]
CULTURE_COLUMNS = ["culture", "troops", "items", "metameshes", "textures", "physics", "render_bytes", "edit_bytes",
                   "texture_bytes", "physics_bytes", "floor_bytes", "upper_bytes"]
SIDE_COLUMNS = ["side", "troops", "floor_bytes", "upper_bytes", "shared_floor_bytes"]


def write_report(path, log_lines, side_rows, battle_row, culture_rows, asset_rows, item_rows, troop_rows,
                 by_reason, top) -> None:
    L = ["# Battle equipment memory audit", ""]
    L.append("Generated by `tools/audit_battle_equipment_memory.py`; read-only on every input. Every figure "
             "attributes bytes in the cooked packs to the equipment a battle preloads "
             "(`PreloadHelper.PreloadCharacters` and `AddItemObject`, see the module docstring), plus race "
             "skin meshes and horse materials, which the engine loads from outside `PreloadHelper` and the "
             "audit counts as battle cost too. It is an attribution, not a measurement of process memory. "
             "The **floor** is render buffers (segment 97f81dbb), texture mip chains and collision bodies; "
             "the **upper bound** adds mesh edit data (segment 5f98413d) and every other mesh segment. The "
             "two totals bracket only the edit data question (whether worn armour, hair and beards load "
             "their edit data in a battle is UNVERIFIED): both already count every hair, beard and horse "
             "material alternative, and neither counts runtime buffers or the skin assets the Approximations "
             "list. A shared asset counts once in any union and in full in each item's or troop's own total.")
    unsized = by_reason.get("TEXTURE_SIZE_UNKNOWN")
    if unsized:
        L += ["", f"**Incomplete totals.** TEXTURE_SIZE_UNKNOWN rows: {len(unsized)}. Each is a texture the selected "
                  "troops reach whose size the audit cannot read: the trees read hold no pixel segment for it, and "
                  "its header either did not decode (the shared decoder reads only texture metadata version 3) or "
                  "names a format the size tables lack. It counts at its stub segment's bytes, or 0 without one, so "
                  "every texture, floor and upper figure below is a lower bound by those textures' mip chains. The "
                  "rows are in `unresolved.tsv`."]
    L += ["", "## Run log", "", "```"] + list(log_lines) + ["```", ""]
    if side_rows:
        L += ["## Sides and battle", "", "| side | troops | floor bytes | upper bytes | shared floor bytes |",
              "|---|---:|---:|---:|---:|"]
        L += [f"| {r['side']} | {r['troops']} | {r['floor_bytes']:,} | {r['upper_bytes']:,} | "
              f"{r['shared_floor_bytes']:,} |" for r in side_rows]
        if battle_row:
            L.append(f"| battle (union of the sides) | {battle_row['troops']} | {battle_row['floor_bytes']:,} | "
                     f"{battle_row['upper_bytes']:,} | |")
            L += ["", f"The sides' own floors sum to {battle_row['sum_of_sides_floor_bytes']:,} bytes; the battle "
                      "floor counts each shared asset once."]
        L.append("")
    L += ["## Per-culture totals", "", "| culture | troops | items | metameshes | textures | floor bytes | "
          "upper bytes | texture bytes | render bytes | edit bytes |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    L += [f"| {r['culture']} | {r['troops']} | {r['items']} | {r['metameshes']} | {r['textures']} | "
          f"{r['floor_bytes']:,} | {r['upper_bytes']:,} | {r['texture_bytes']:,} | {r['render_bytes']:,} | "
          f"{r['edit_bytes']:,} |" for r in culture_rows]
    L += ["", f"## Top {top} assets by floor bytes", "",
          "| kind | name | module | floor bytes | edit bytes | flags | items | troops |", "|---|---|---|---:|---:|---|---|---|"]
    L += [f"| {r['kind']} | {r['name']} | {r['module']} | {r['floor_bytes']:,} | {r['edit_bytes']:,} | "
          f"{r['flags'].replace(';', ' ')} | {r['items']}: {r['sample_items'].replace(';', ' ')} | "
          f"{r['troops']}: {r['sample_troops'].replace(';', ' ')} |" for r in asset_rows[:top]]
    meshes = sorted((r for r in asset_rows if r["kind"] == "metamesh"), key=lambda r: (-r["edit_bytes"], r["name"]))
    L += ["", f"## Top {top} meshes by edit bytes", "",
          "| name | module | edit bytes | floor bytes | lod0 faces | flags | items |", "|---|---|---:|---:|---:|---|---|"]
    L += [f"| {r['name']} | {r['module']} | {r['edit_bytes']:,} | {r['floor_bytes']:,} | {r['lod0_faces']} | "
          f"{r['flags'].replace(';', ' ')} | {r['items']}: {r['sample_items'].replace(';', ' ')} |"
          for r in meshes[:top]]
    L += ["", f"## Top {top} items by floor bytes", "",
          "| item | module | type | floor bytes | upper bytes | troops | meshes |", "|---|---|---|---:|---:|---:|---|"]
    L += [f"| {r['item_id']} | {r['module']} | {r['type']} | {r['floor_bytes']:,} | {r['upper_bytes']:,} | "
          f"{r['troops']} | {r['meshes'].replace(';', ' ')} |" for r in item_rows[:top]]
    L += ["", f"## Top {top} troops by floor bytes", "",
          "| troop | culture | race | floor bytes | upper bytes | race floor bytes | items |", "|---|---|---|---:|---:|---:|---:|"]
    L += [f"| {r['troop_id']} | {r['culture']} | {r['race']} | {r['floor_bytes']:,} | {r['upper_bytes']:,} | "
          f"{r['race_floor_bytes']:,} | {r['items']} |" for r in troop_rows[:top]]
    flag_counts = {}
    for r in asset_rows:
        for f in filter(None, r["flags"].split(";")):
            n, b = flag_counts.get(f, (0, 0))
            flag_counts[f] = (n + 1, b + r["floor_bytes"])
    L += ["", "## Flags", "", "| flag | assets | floor bytes |", "|---|---:|---:|"]
    L += [f"| {f} | {n} | {b:,} |" for f, (n, b) in sorted(flag_counts.items())] or ["| none | 0 | 0 |"]
    L += ["", "## Unresolved by reason", "", "Every row is in `unresolved.tsv`.", "",
          "| reason | count | first | consequence |", "|---|---:|---|---|"]
    L += [f"| {code} | {len(rows)} | {rows[0][1]} | {REASONS[code]} |" for code, rows in sorted(by_reason.items())]
    L += ["", "## Approximations", "", "```", _doc_section("APPROXIMATIONS").strip("\n"), "```", ""]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L) + "\n")


if __name__ == "__main__":
    sys.exit(main())
