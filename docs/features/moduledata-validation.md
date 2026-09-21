# ModuleData Validation (schema-driven cross-reference validator)

## Overview

A single reusable, read-only validator for TAOM's Bannerlord ModuleData XML. It builds id registries from the installed game + the TAOM repo, resolves prefix-based cross-references (`Item.` / `NPCCharacter.` / `Culture.` / `PartyTemplate.`), and runs per-schema duplicate-id / enum / civilian-type checks, emitting a severity-classified report. It consolidates the scattered one-shot validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the equipment-`equipmentType` PowerShell snippet, the duplicate-id-across-Armory-folders checks) into one schema-driven engine.

The same engine backs **three consumers**: a **CLI** (`validate_moduledata.py` — batch report), a **pre-commit hook** (`.claude/hooks/check-moduledata-validation.sh` — blocks Claude-driven commits on ERRORs), and an **MCP server** (`taom_mcp_server.py` — interactive agent queries like "does this item exist?" / "what references this troop?").

A schema-driven engine (`SchemaDefinition` / cross-reference / validation services) with an MCP-tool surface, implemented in Python.

## Why This Exists

TAOM repeatedly ships the same data-integrity bug classes, each previously caught (if at all) by a separate ad-hoc script run by hand:

- **Underwear bug** — a troop references `Item.X` that resolves to no item → spawns naked. (`BROKEN_ITEM_REF`)
- **Dead troop ref** — `upgrade_target`/party-template `troop=`/culture `basic_troop=` points at a deleted troop. (`BROKEN_TROOP_REF`)
- **Stale culture / "wrote rohan instead of vlandia"** — `culture="Culture.X"` where X is not a real StringId. (`UNKNOWN_CULTURE`)
- **Duplicate item id across Armory folders** — engine silently shadows one. (`DUPLICATE_ITEM_DEF`)
- **Missing civilian `equipmentType`** — Faramir/Boromir wrong-outfit bug. (`MISSING_CIVILIAN_TYPE`)
- **Harness with no `family_type`** — defaults to 0 (human family), so the inventory screen refuses it on every mount with no message → "this item is not equipable". (`MISSING_HARNESS_FAMILY_TYPE`, `HARNESS_FAMILY_MISMATCH`)
- **A Horse slot with an empty `HorseHarness` slot.** A harness is required, not optional: on some
  mounts it carries the rider's seat rather than armour, and the war ram's body meshes are bare
  pelts, so the troop spawns sitting on hide. No reference is broken and no mesh is missing, which
  is why every other gate passes it. (`MOUNT_WITHOUT_HARNESS`)
- Duplicate NPC/culture/roster ids; invalid `default_group`; broken party-template refs.
- **`face_key_template` pointing at an undefined `BodyProperty`** — not an XML error: the engine
  registers a placeholder, `MBObjectManager.UnregisterNonReadyObjects` drops it, and the character
  silently loses its authored face. (`BROKEN_BODY_PROPERTY_REF`)
- **A lord's culture owns no settlement** — vanilla `SpawnLordParty` ends with an unguarded
  `Settlement.All.First(x => x.Culture == hero.Culture)`, so a landless culture on an
  `Occupation.Lord` hero is a latent `InvalidOperationException` on the daily clan tick.
  (`LANDLESS_CULTURE`)
- **A dwarf authored as cavalry, or handed a mount other than the Dwarven war ram.** The dwarf
  skeleton's rider bone is misaligned, so a mounted dwarf spawns inside the horse mesh.
  (`MOUNTED_DWARF`)

"Schemas are the source of truth": field/enum/ref knowledge lives in `tools/schemas/*.json`, not hardcoded in Python.

## Architecture

```
tools/schemas/*.json   (declarative source of truth: entry element, id, enums, special rules)
        │  load_schemas()
        ▼
tools/taom_schema.py   ── build_registries(moduledata, game_modules)
   Registries (items, item_def_files, npccharacters, cultures, party_templates,
               body_properties, settled_cultures, suspect_registries)            ← injected (testable)
   REF_KINDS (prefix-based, attribute-agnostic)
   Validator.run():
     pass 1  global cross-reference sweep  (every *.xml under ModuleData
                                           + every extra_ref_root, e.g. LOTRLOME_Armory)
     pass 2  per-schema: duplicate-id, enum, civilian-type rule
     pass 3  duplicate item definitions across Armory folders
     pass 4b landless cultures (Lord NPCs / Factions / Kingdoms vs settled_cultures)
        │
        ├──▶ tools/validate_moduledata.py                  (CLI: batch report, --json, --code, exit 1 on ERROR)
        ├──▶ .claude/hooks/check-moduledata-validation.sh  (PreToolUse gate: blocks commits on ERROR)
        └──▶ tools/taom_query.py ──▶ tools/taom_mcp_server.py  (MCP: interactive per-id queries for agents)
```

The CLI, hook, and MCP server are thin front-ends over the same engine — the engine is the single source of validation logic; each front-end only changes *how/when* it is invoked (batch, commit-gate, interactive).

**Key design decisions:**
- **Registries are injected**, so the engine is unit-testable with synthetic data (no game install needed).
- **Cross-ref patterns are attribute-agnostic** — they match the prefix on ANY attribute (`id="Item.x"`, `troop="NPCCharacter.x"`, `culture="Culture.x"`, `*_party_template="PartyTemplate.x"`). Anchoring on `id=` silently misses party-template `troop=` refs (deep-review 2026-05-30, HIGH). Definitions never carry the prefix in their value, so a def is never mistaken for a ref.
- **Empty registry → skip that ref kind** (a registry that couldn't be built is "unavailable", not "everything is broken"). With no game install, item/troop/party-template checks are skipped (the CLI reports the skip); culture/dup-id/civilian/enum still run.
- **Fail-fast schema load** — an unknown `special_rules` string or a missing required field raises at load, never a silent no-op.

## Configuration

`tools/schemas/*.json` — one schema per XML type. Fields:

| Key | Meaning |
|---|---|
| `applies_to` | globs (relative to ModuleData) the schema covers |
| `entry_element` | XML element whose `id` is an entry (for dup-id + enum + attribution) |
| `id_attribute` | the id attribute name (default `id`) |
| `duplicate_code` | issue code emitted for a duplicate (`DUPLICATE_NPC_ID`, etc.) |
| `enums` | `{attr: [allowed values]}` → `INVALID_ENUM` (warning) on mismatch |
| `special_rules` | named handlers; currently only `civilian_equipment_type` |
| `description` | shown by the CLI |

Cross-reference kinds are defined in `REF_KINDS` (code, not schema) because the prefixes are fixed Bannerlord conventions. The vanilla-culture floor set (`VANILLA_CULTURES`) backstops cultures that exist only in code/XSLT output.

## Coverage boundary: the engine XSD layer lives in a sibling tool

**Since 2026-09-18 the XSD layer has its own gate: `python tools/validate_xml_schemas.py`.** It validates every engine-loaded file against the XSD the engine itself picks, following `MBObjectManager.GetMergedXmlForManaged`'s file resolution and schema choice (it does not apply the `<IncludedGameTypes>` filter, so it may validate a file one game type never loads, never the reverse): files come from each SubModule.xml `<XmlName id path>` (`ModuleData/<path>.xml`, else the top level of the `ModuleData/<path>/` folder), the schema is `<Module>/ModuleData/XmlSchemas/<id>.xsd` when the module ships one and `<game>/XmlSchemas/<id>.xsd` otherwise, and `_replaceWhileMerging` is accepted as the optional boolean the engine injects. Pass files to check only those (each is validated under its own module); `--live` adds `TAOM_Map` and the Armory. It also reports registrations that load nothing or that the engine throws on at startup, and a DOCTYPE (the engine prohibits DTDs, so such a file loads nothing). The engine validates on a normal campaign load too, but `LoadXmlWithValidation` only prints a failure to the log and loads the file anyway, which is why the cases below shipped. **The same check runs inside `validate_moduledata.py` as `SCHEMA_INVALID`** (ERROR, repo module only; one WARNING when lxml or the engine's `XmlSchemas` folder is absent, never a silent pass), so the commit hook gates it. Measured on 2026-09-18: the repo module's 97 validated files all pass, so any failure there is new; `--live` finds one Armory file (#620); the seven dead `TAOM_Map` registrations it found were removed the same day (#619), and `tools/tests/test_validate_xml_schemas.py` pins the live registrations to `Settlements`, so a resync from the mirror, which still ships all eight over empty stubs, fails it. An XSD can lag the code, so confirm a failure against the element's deserializer before editing. The rest of this section is the history that motivated it, and still describes what `validate_moduledata.py` alone does not do.

The validator checks **cross-references** (the `Item.` / `NPCCharacter.` / `Culture.` / `PartyTemplate.` prefixes, on any attribute in any `ModuleData/**/*.xml` — including `characters/clans.xml`) plus **per-schema** duplicate-id / enum / civilian-type rules. It does **not** validate the engine's own XSD schemas (`<game>/XmlSchemas/*.xsd`), which enforce *required-attribute presence* and element structure.

Concretely, `characters/clans.xml` has **no validator schema** (`tools/schemas/` covers only equipmentsets / npccharacter / spcultures). A clan (`<Faction>`) missing a `Factions.xsd`-required attribute such as `initial_home_settlement` therefore passes both `validate_moduledata.py` and the pre-commit hook, and surfaces only when the engine/editor loads the file (and the engine reads `Factions`, `Kingdoms` and `Heroes` only when a NEW campaign starts, so loading a save never reports it) (`Error: The required attribute 'initial_home_settlement' is missing. Node: Faction`). This is how `clan_umbar_3` shipped without a home settlement (fixed 2026-06-22). When authoring or editing `clans.xml`, open the file in the Bannerlord editor (or rely on an engine load) to catch required-attribute violations; `validate_moduledata.py` alone will not. A future improvement would be a `taom_factions.json` schema, but presence-of-required-attribute is not currently in the validator's model.

The same boundary applies in the other direction (an **extra, XSD-undeclared attribute**), even for files the validator *does* schema. `validate_moduledata.py` checks declared field types, enums, and cross-refs but does not reject unknown attributes; the engine's `NPCCharacters.xsd` does. So `npcs_rohan.xml` passed the validator (all 4,522 NPCCharacters parse) while the editor flagged a bogus `child_monster="…"` on its 10 child-template blocks (`Error: The 'child_monster' attribute is not declared`, fixed 2026-06-22). A clean `validate_moduledata.py` is necessary but not sufficient: the Bannerlord editor / an engine load is the authority for the XSD layer (both required-attribute presence *and* no-undeclared-attributes). Decide a flagged attribute via the decompiled deserializer + vanilla usage: bogus → remove (`child_monster`), real-but-XSD-incomplete → keep. **Correction (2026-09-18):** `family_type` on `<Horse>` was filed here as the second kind. It is the first. The engine reads `family_type` in `ArmorComponent.Deserialize` (`<Armor>`, harness matching), `Monster.Deserialize` and `ConversationAnimationManager` only; `HorseComponent` reads its fields through `XmlHelper` and never names it (v1.5.3 installed DLL). Vanilla `horses_and_others.xml` carries it on 34 `<Armor>` elements and no `<Horse>`. So the three `<Horse family_type>` in the Armory's `LOTRAOM_horses.xml` (attributes on lines 697, 726 and 766; the tool reports 698, 727 and 767, the end of each start tag) are ignored: a horse's family comes from its `Monster`, and those monsters already carry the same values (#620).

A third boundary, and the one that hid nine cultures' worth of Calradian troops for months: **the ref
sweep can only prove a reference RESOLVES, never that it points at the right thing, and it never reads
`spcultures.xslt` at all.** `settlement_patrol_template_level_1="PartyTemplate.patrol_party_empire_template_level_1"`
resolves perfectly, because vanilla ships that template; it is simply Calradian. And the XSLT is a
stylesheet rather than a data file, so a binding that lives only there is outside everything the
validator walks, as is a binding that is *absent* and inherited through the passthrough.

`PASS` from this validator therefore says nothing about whether a culture fields its own troops. That
question is owned by `TAOM.Tests/Core/CulturePartyTemplateTests.cs`, which transforms the stylesheet
and checks each emitted binding against the set of ids TAOM authors. The two are complementary: the
validator catches a typo'd id, the test catches a real id that belongs to the wrong faction. Run both.

A fourth boundary is semantic rather than structural: **the validator models ids and enums, so a
field whose correct value depends on who the character IS is invisible to it.** `is_female` has no
rule at all, and a lord's name is free text behind a localization key. The `is_female` gap cost us
twice. `TAOM.Tests/Core/TownsfolkAndNotableSexConsistencyTests.cs` now owns the townsfolk half,
after 166 female-role entries across 17 cultures shipped with no `is_female="true"` and rendered as
men, and after all 596 notable templates turned out to be male ([rca-townsfolk-sex-2026-09-06](../reviews/rca-townsfolk-sex-2026-09-06.md)).
Both remain invisible to the validator: adding a rule would mean teaching it that an id prefix
implies a sex, which is exactly the semantic judgement this boundary describes. `lord_WE8_c` therefore passed
every run while shipping as vanilla's female "Icratia" long after TAOM had renamed him to Pelendur,
son of Golasgil, and `lord_1_46_1` passed while shipping Malrior's wife as a bearded man. Nor does
`BROKEN_BODY_PROPERTY_REF` help: it fires only on a `BodyProperty.*` reference, and every Gondor lord
carries an inline `<BodyProperties key=>` instead, so a female body key worn by a man resolves to
nothing to check. `TAOM.Tests/Core/LordNameAndSexConsistencyTests.cs` owns this pair, asserting that
every inline English name fallback matches `taom_xslt_strings.xml` and that no `is_female="true"` lord
carries `<beard_tags>`. Same complementary split as above: the validator catches a bad id, the test
catches a good id describing the wrong person.

A fifth boundary is an element that is simply **absent**: `BROKEN_BODY_PROPERTY_REF` fires on a
`BodyProperty.*` reference that resolves to nothing, so it has plenty to say about a typo'd
`face_key_template` and nothing at all to say about an `NPCCharacter` with no `<face>` element. That
is not cosmetic. `BasicCharacterObject.Deserialize` then builds the character's `MBBodyProperty` from
`default(BodyProperties)`, whose age is 0, and the engine renders it on the toddler skin: TAOM's arena
practice set shipped that way for ten cultures, 46 characters, and players reported the arena fighters
as children (2026-09-06). `TAOM.Tests/Core/CharacterFaceCoverageTests.cs` owns it, asserting that every
`NPCCharacter` under `Main/_Module/ModuleData` declares a `<face>`. The mechanism, and why
`Mission.SpawnAgent`'s two age guards read a different age and miss it, is in
[body-properties.md](../modding/body-properties.md) "Gotchas".

## Landless-culture check (`LANDLESS_CULTURE`)

**Severity ERROR.** Fires when a culture carried by an `NPCCharacter` with `occupation="Lord"`, a
`<Faction>` or a `<Kingdom>` owns **no settlement in the world the game actually builds**.

Vanilla `HeroSpawnCampaignBehavior.SpawnLordParty` ends with an unguarded
`Settlement.All.First(x => x.Culture == hero.Culture)`, reached whenever the hero's map faction has
no `InitialHomeSettlement`. Vanilla never throws there because every Calradian culture owns land;
TAOM can, because `TAOM_Map/ModuleData/settlements.xslt` deletes every vanilla settlement and its
988 replacements cover only part of the 38 defined cultures (27 before the Khand retag, 28 after).
That is crash `099f650c` (2026-08-04) — `InvalidOperationException: Sequence contains no matching
element` out of `Campaign.Tick`'s daily clan tick. `Patch65_LandlessCultureSpawnGuard` catches it at
runtime; this check catches it before it ships. Full analysis:
[`lord-spawn-guard.md`](./lord-spawn-guard.md) (#374).

Evidence: **18** `LANDLESS_CULTURE` errors before the retag (the 18 TAOM-authored Variag lords in
`characters/lords.xml`), `PASS: no validation issues found.` after.

## Settlement-economy floor check (`SETTLEMENT_ECONOMY_FLOOR`)

**Severity ERROR.** Fires when a settlement whose culture is named in
[`tools/settlement_economy_floor.json`](../../tools/settlement_economy_floor.json) sits below that
spec's `town` / `castle` / `hearth` floor in the world the game actually builds.

The 2026-08-14 faction-economy pass raised every fief of eight fief-starved cultures in the **LIVE**
`<game>/Modules/TAOM_Map/ModuleData/settlements.xml`. That module is unversioned, so a reinstall
reverts the whole pass and nothing in this repo would otherwise notice: the same class of silent
loss behind CLAUDE.md's "A fix in a dependency module" trap, which closed seven issues in the
2026-08-08 triage. The spec file is the single source of truth, read by both
`rebalance_settlement_prosperity.py` (which writes the floor) and this check (which verifies it), so
neither restates the numbers. Re-apply with:

```
python tools/rebalance_settlement_prosperity.py --culture-floor-file tools/settlement_economy_floor.json --apply
```

Three degraded states are deliberately distinguished, because each one used to read as a pass:

| State | Result |
|---|---|
| Registry unavailable (no game install) | silent; the CLI already exits 2 |
| Spec file missing, or declaring no floor/cultures | **ERROR**: a deleted spec disables the gate exactly when it is needed |
| Spec names a culture that owns no settlement | **ERROR**: a retag or a typo'd id leaves the gate covering nothing |

The floor is clamped to the same `PROSPERITY_CAP` / `HEARTH_CAP` the writer clamps to, imported
from the writer rather than restated. Without that, a spec value above a cap would demand a number
no `--apply` could ever produce and the gate would fail on every commit forever.

Evidence: adding a culture whose fiefs sit below the floor produced 4 errors and exit 1; the shipped
spec reports `PASS`. Pinned by `SettlementEconomyFloorTests` and `SettlementEconomyRegistryTests` in
`tools/tests/test_validate_moduledata.py`.

## Fortification without village (`FORTIFICATION_WITHOUT_VILLAGE`)

**Severity WARNING.** Fires for a town or castle in the world the game actually builds that no
`<Village bound="Settlement.<id>">` names. Such a fief starts every campaign with no village trade,
no villager parties and no rural notables, and its food runs on the fief alone. `town_EW10`
(Serelond) and `town_EW11` (Methir) shipped that way until #597 (2026-09-13), and the only thing
that noticed was a hand count over the LIVE `TAOM_Map/ModuleData/settlements.xml`, which is
unversioned: this is the in-repo gate beside that external edit.

The pass reads the `bound` field that `build_settlement_economy` now records per village, so it is
skipped, never faked, without a game install. A warning rather than an error, because the fix is a
map-editor placement plus a data row, not a commit, and the commit hook cannot see the file it is
about anyway. Exemptions live in `Validator._VILLAGELESS_BY_DESIGN` with a reason each (none today:
`castle_G4`, Framsburg, sat there for an hour on 2026-09-13 until its two villages were placed), and an entry rots two ways that
both used to read as a pass: naming a fortification that is not in the world, or naming one that has
villages again. Both are reported.

Fix: place the village entities in the editor, then add rows with
[`tools/add_map_villages.py`](../../tools/add_map_villages.py) or rebind a neighbour with
[`tools/rename_map_settlements.py`](../../tools/rename_map_settlements.py).

## Mounted-dwarf check (`MOUNTED_DWARF`)

**Severity ERROR.** Fires on an `NPCCharacter` with `race="dwarf"` that can reach a mount which is
not a Dwarven war ram (a `slot="Horse"` entry in its own inline `<EquipmentRoster>`, or in a
standalone `<EquipmentRoster>` it names via `<EquipmentSet id="…"/>`), or that is tagged
`default_group="Cavalry"`/`"HorseArcher"` while carrying no ram.

Dwarves use a custom, shorter skeleton whose rider bone is misaligned, so a mounted dwarf spawns
*inside* the horse mesh. `Patch46_TournamentDwarfDismount` already strips Horse + HorseHarness from
dwarf tournament participants at runtime, keyed on race; this check is the data-layer half of the
same invariant, so a troop revamp or a copy-pasted roster cannot reintroduce the defect.

**The two halves are not interchangeable** (decompiled v1.4.7, 2026-08-04):

| | Troop (non-hero) | Lord / hero |
|---|---|---|
| `default_group` | **is** the battlefield formation (`BasicCharacterObject.GetFormationClass():543` returns `DefaultFormationClass`) | ignored for formation; drives party-screen icons, tooltips, `CharacterCode` previews |
| Horse in the equipment slot | the actual mount | **decides the formation on its own** — `CharacterObject.GetFormationClass():818-839` overrides the base and, when `IsHero`, reads only `BattleEquipment` (a `HasHorseComponent` item in `EquipmentIndex.Horse` → Cavalry; plus a bow/crossbow → HorseArcher) |

So `default_group="Infantry"` on a lord holding a horse buys nothing — the mount alone spawns him
mounted. Checking only the enum would have missed exactly that case.

### The war-ram carve-out (`WAR_RAM_MOUNT_IDS`, #515)

The [Dwarven war ram](./war-ram.md) is the one mount a dwarf may ride: it is built for the dwarf
skeleton, so the rider bone lines up and he does not spawn inside the mesh. The allowlist is exactly
two ids, pinned in `tools/taom_schema.py`:

```python
WAR_RAM_MOUNT_IDS = frozenset({"taom_war_ram_a", "taom_war_ram_b"})
```

A dwarf who carries one is legal, and **the group rule relaxes with it**: `default_group="Cavalry"`
and `"HorseArcher"` both pass for a ram rider, because a ram rider genuinely is cavalry. The relaxation
is per character, not global. Every other mount that character can reach is still reported, and a
dwarf with a cavalry `default_group` and no ram is still an error.

Two implementation properties matter when reading a result:

- **Every reachable mount is resolved, not just the first.** `_mounts_in` collects all `slot="Horse"`
  entries in document order, and the character's inline equipment *and* every standalone roster he
  names are both read even when the inline half already produced a mount. Before the allowlist existed
  the first-wins shortcut was harmless; with it, a ram listed ahead of a horse would have taken the
  pass and masked the horse. Only one issue is raised per character, naming the first mount the
  allowlist does not cover, so the offender is not buried under the legal ram.
- **The ids are pinned, not prefix-matched, and the `Item.` prefix is required.** A later
  `taom_war_ram_c` has to be reviewed into the frozenset deliberately. And the mount id compared
  against the allowlist is whatever `_ITEM_REF_ATTR_RE` (`id="Item\.(…)"`) captured, so a Horse slot
  written `id="taom_war_ram_a"` without the prefix matches nothing, is recorded as `(unnamed mount)`,
  and errors. That is the intended failure direction: an unparseable mount is never allowlisted.

**Scope, and the blind spot it left.** The check reads `NPCCharacter` definitions and the rosters they
name. Player rosters selected by culture (character creation, career starters) were declared out of
scope on the reasoning that no `NPCCharacter` references them and all 12 custom cultures ship the same
16-of-55 sumpter-horse template, so gating them would flag a shared vanilla-parity pattern rather than
a dwarf defect. **The first half of that reasoning is exactly why a real defect hid there.** The career
starting rosters in `equipmentsets/taom_career_starting_equipment.xml` are applied to the player at
runtime by `CareerStartingEquipmentService.ApplyCareerStartingEquipment`, which builds the roster id
from culture, career archetype and sex and hands it to the equipment adapter. Nothing in the XML tree
points at them, so the sweep cannot see them, and `player_career_erebor_cavalry_m`/`_f` shipped
equipping `Item.saddle_horse`: a vanilla horse on a dwarf, the precise case this check exists to
forbid, through every green validator run. It was found by hand during #515.

Coverage for those rosters now lives in tests rather than in this validator:
`TAOM.Tests/Features/CharacterCreation/CareerCultureCoverageTests.cs` pins both directions
(`EreborCareerStartingRosters_EquipOnlyWarRams` rejects any non-ram mount, and
`EreborCavalryCareerRoster_ActuallyGrantsAWarRam` stops the first test passing trivially if the mount
is simply deleted). The general lesson generalises past dwarves: **any XML applied to a character at
runtime is invisible to every pass in this validator**, which walks static definitions only.

Evidence at introduction (2026-08-04): **0** `MOUNTED_DWARF` issues across all 185 `race="dwarf"`
characters (169 Infantry, 16 Ranged); the data already complied. Negative control: flipping
`lord_E1_1` to `default_group="Cavalry"` produced `1 error(s)` at `characters/lords.xml:9829`, then
reverted.

Re-measured 2026-08-28 after the war ram landed: **191** `race="dwarf"` characters (159 Infantry,
16 Ranged, 16 Cavalry) and a full-run `PASS`. All 16 Cavalry dwarves carry a ram: 12 Erebor lords via
the `erebor_bat_template_ram_a..e` rosters, and the 4 Ironpass troops inline. No dwarf anywhere in
TAOM's ModuleData reaches a non-ram mount.

Re-measured 2026-09-06: **193** `race="dwarf"` characters (159 Infantry, 16 Ranged, **18** Cavalry).
The Cavalry figure moved because the ram branch is six rungs, not the four counted above: the 18 are
the same 12 Erebor lords plus all six `ironpass_*` ram troops. Still a full-run `PASS`, and still no
dwarf anywhere in TAOM's ModuleData reaching a non-ram mount.

### A harness is required, not optional (`MOUNT_WITHOUT_HARNESS`)

Every `slot="Horse"` entry must have a `slot="HorseHarness"` entry beside it in the same equipment
set. This is a different question from the carve-out above: `WAR_RAM_MOUNT_IDS` asks *may a dwarf
ride this*, and this rule asks *does the rider have something to sit on*.

The rule is universal because **the data cannot answer the question it depends on.** Whether a
mount carries its own saddle is a property of a mesh, and no attribute in any XML records it. The
war ram is the proof: `sk_eb_goat_a` and `sk_eb_goat_b` are bare pelts, and every saddle lives on
one of the eight `sk_eb_goat_bard_*` harness meshes, so an empty slot there is not an unarmoured
mount but an unsaddled rider.

The first version of this check tried to be clever about it, scoping itself to a pinned set of
mounts believed to be saddleless. That was wrong twice over. It rested on a survey claim that the
spider carries a seat on its body, which is false (see the allowlist below), and more importantly
it repeated the mistake it existed to catch: it decided from a desk which mounts were fine bare.
Requiring the harness and forcing every exception to be argued is the only version that does not
depend on a belief about a mesh.

Two properties worth knowing when reading a result:

- **Judged per equipment set, not per troop.** The engine draws each slot from an independently
  chosen set, so filling the harness in three sets of four still spawns bare rams a quarter of the
  time. Four bad sets are four findings.
- **It does not need the game install, and that is deliberate.** The sibling harness passes
  (`MISSING_HARNESS_FAMILY_TYPE`, `HARNESS_FAMILY_MISMATCH`) compare family types read from the
  INSTALLED modules and go quiet when that registry is empty. Slot presence is answerable from repo
  XML alone, so this check stays live on a machine with a partial `LOTRLOME_Armory`. Landing it
  meant deleting the early returns in `_harness_family_types` and `_harness_pairings` that returned
  `[]` on an empty registry, or the check would have switched itself off in exactly the situation
  where the ram data is least trustworthy while the validator still printed `PASS`.
  `test_it_still_reports_without_an_installed_game` pins that.

#### The exemptions (`_HARNESSLESS_BY_DESIGN`)

Keyed by the owning `NPCCharacter` id, or by the standalone `EquipmentRoster` id where there is no
character above it, in the `_BODYLESS_BY_DESIGN` style. Each entry carries its reason, and a test
asserts both that the reason is real and that the id still exists, because an allowlist entry for a
renamed troop rots silently.

| Owner | Why |
|---|---|
| `harad_mumakil_rider` | A `HorseHarness` **suppresses the Horse item's `<AdditionalMeshes>`** (native mount compositing), and `taom_mumakil` keeps its war-platform there. Equipping one would delete the howdah. This is an engine constraint, not a preference |
| `taom_spider_creature`, `taom_spider_rider_brown`, `taom_spider_rider_pale` | **An open gap, not a design choice.** No spider `HorseHarness` item has ever been authored, so there is nothing to equip and the rider sits on the spider with no saddle geometry: the same defect class as the ram, recorded as a known limitation when the troop landed and still open. Delete this entry when a `spider_saddle` harness item lands |

Shipped twice, which is why the rule is not scoped. `ironpass_ram_herder` was authored with no
harness on all four sets, on purpose, on the reading that a bare ram was merely unarmoured, and
[war-ram.md](./war-ram.md) recorded that as deliberate until players reported dwarves riding
bareback (2026-09-06). `eomer_bat_equipment` carried the full Theoden kit on a bare charger while
all fourteen of its Rohan siblings had a harness; the universal rule is what surfaced it.


### The `settled_cultures` registry (`build_settled_cultures`)

`build_settled_cultures(game_modules)` walks the settlement-contributing modules in SubModule load
order — `Native`, `SandBoxCore`, `SandBox`, `CustomBattle`, `TAOM_Map` — collecting every
`<Settlement … culture="Culture.X">` from each module's `settlements.xml` (`_SETTLEMENT_CULTURE_RE`).

**It honours the unconditional strip, and that is the load-bearing detail.** When a module's
`settlements.xslt` carries an empty `<xsl:template match="Settlement"/>` (TAOM_Map ships exactly
one), everything accumulated so far is discarded before that module's own settlements are added.
A registry built without that models a world the game never builds: it counts vanilla's 494 deleted
settlements, reports every culture as landed, and prints PASS while the game crashes. Both spellings
of the empty template (self-closing and empty-body) are matched by `_SETTLEMENT_STRIP_RE`.

Two guards match the other registries' behaviour: an **empty** `settled_cultures` (no game install)
skips the check entirely rather than reporting everything broken, and a **size floor of 15** feeds
`Registries.suspect_registries`, so a shrunken registry is named out loud instead of passing quietly.

### `_LANDLESS_BY_DESIGN` allowlist

The ten cultures still landless after the Khand retag are allowlisted, each with its reason in-code.
Adding an entry is a deliberate act — state why:

| Cultures | Why they cannot reach the throwing line |
|---|---|
| `looters`, `sea_raiders`, `mountain_bandits`, `forest_bandits`, `desert_bandits`, `steppe_bandits` | Bandit heroes are `Occupation.Bandit`; `GetBestAvailableCommander` filters on `Occupation.Lord`. |
| `neutral_culture` | Vanilla placeholder culture, carried by no TAOM lord or clan. |
| `darshi`, `nord`, `vakken` | Vanilla minor-faction cultures (ghilman / skolderbrotva / forest_people) TAOM inherits but never re-cultured. All three clans keep a valid `initial_home_settlement`, so vanilla never reaches the `First()`; Patch65 covers them if a mod re-parents their lords. |

### Scope: TAOM's own ModuleData only

The sweep reads `Main/_Module/ModuleData/**/*.xml`, matching the validator's documented contract. A
vanilla-inherited faction whose `InitialHomeSettlement` is null at runtime is Patch65's problem, not
a TAOM data defect — nothing in the files this validator owns is wrong in that case.

**Tests:** `tools/tests/test_validate_moduledata.py` carries two classes for this check.
`LandlessCultureTests` — `test_lord_in_landless_culture_is_reported`,
`test_lord_in_landed_culture_is_clean`, `test_clan_and_kingdom_in_landless_culture_are_reported`,
`test_non_lord_occupation_is_ignored`, `test_allowlisted_cultures_are_not_reported`,
`test_check_skipped_when_settlement_registry_unavailable`.
`SettledCultureRegistryTests` — `test_unconditional_strip_discards_earlier_modules`,
`test_without_a_strip_modules_merge`, `test_no_game_install_yields_empty_registry`,
`test_registry_is_wired_into_build_registries_and_floored`. The strip and merge cases differ only
by the presence of `settlements.xslt` and expect different sets, so they fail if the strip handling
regresses.

## Armour-slot coverage (`MISSING_BODY_ARMOUR`, `INCONSISTENT_ARMOUR_SLOT`)

Added 2026-09-01. Every other check in this tool asks **"does this reference
resolve"**. A troop wearing nothing has no reference to resolve and no mesh to look
up, so it passes all of them. That is how 15 of 16 Umbar troops shipped in vanilla
peasant rags, with Head, Cape and Gloves absent entirely, against a completely green
board: `validate_moduledata.py` PASS, `validate_mesh_refs.py` 0 errors,
`validate_all_troop_refs.py` PASS.

Two questions at deliberately different severities.

**`MISSING_BODY_ARMOUR` (ERROR).** No battle set of a troop fills the `Body` slot.
Measured repo-wide at exactly three troops, all of them bare-chested on purpose, so
`Validator._BODYLESS_BY_DESIGN` is the whole of the known debt and a fourth is a real
regression:

| troop | file | why it is exempt |
|---|---|---|
| `dg_goblin_slave` | `troops_dolguldur.xml` | a slave in rags; bare torso is the intended look |
| `urukhai_champion` | `troops_isengard.xml` | Uruk-hai champions fight bare-chested by design |
| `urukhai_berserker` | `troops_isengard.xml` | same |

A test asserts all three ids still exist in the troop files. An allowlist entry for a
renamed or deleted troop otherwise rots with no signal, quietly widening the
exemption, which is the failure the `MOUNTED_DWARF` war-ram carve-out guards against
the same way.

**`INCONSISTENT_ARMOUR_SLOT` (WARNING).** A slot filled in some battle sets and empty
in others. Per [`.claude/rules/troops.md`](../../.claude/rules/troops.md), the engine
draws each of the 12 slots from an **independently chosen** set, starting at two sets,
so this ships a combination nobody authored. It is invisible in play until a battle:
the encyclopedia, party screen, troop tree and tournament all render set #1. 96 exist
across 10 cultures, so it warns rather than blocks, on the reasoning that a gate which
fails on pre-existing debt gets disabled rather than fixed.

### Scope, and the parsing traps that shaped it

Only files named `troops_*.xml` are scanned. Notables, lord rosters, wanderers and
education templates legitimately vary per character, and sweeping them buries the
signal (17 characters repo-wide have no Body item once `npcs_*.xml` is included,
almost all of them `prison_guard_<culture>`).

Three parsing details are load-bearing, each of which produced a wrong verdict in
review before it was fixed:

- **Self-closing rosters.** `<EquipmentRoster />` appears 326 times in vanilla. A
  pattern without a `/>` alternation matches its own `>` and then runs forward to eat
  the *next* roster's closing tag, which both hides a genuinely empty set and invents
  an ERROR on a correctly dressed troop.
- **Civilian detection must be anchored.** A substring test for `civilian` reads
  `civilian="false"` as civilian, and reads `id="x_civilian_y"` as civilian too,
  dropping a real battle set from the comparison.
- **Both quote styles.** `slot='Body'` is legal XML; a double-quote-only matcher reads
  the set as empty and reports a dressed troop as naked.

### The gate that enforces the ERROR

`check-moduledata-validation.sh` filters this tool to an explicit `--code` list. When
these checks were added, **four of the nine error codes were absent from that list**
and so could never block a commit. `CommitGateCoverageTests` now asserts the two sets
agree in both directions. See [`.claude/rules/moduledata-validation.md`](../../.claude/rules/moduledata-validation.md).

## Upgrade armour regression (`UPGRADE_ARMOUR_REGRESSION`)

**WARNING.** The equipment half of the ladder rule (#541). For every upgrade edge the validator
averages, over each troop's battle sets, the `head + body + arm + leg` of the item in each of the
five armour slots (Head, Body, Cape, Gloves, Leg; an unfilled slot is 0, because the engine draws
each slot from an independently chosen set) and warns when the target's total is below the
source's. Item values are `Registries.item_armour`, built from the same item roots as the
reference registry, so without the install the table is empty and the check is skipped rather
than run against a handful of repo items. Militia-to-militia edges are exempt, and the
`_BODYLESS_BY_DESIGN` troops are compared without Body and Cape (their skirt fills the Cape slot
as the chest stand-in). The repair is `tools/fix_upgrade_armour_regressions.py --apply`; the first
run found 62 regressing edges across 13 cultures and left 0.

## Cross-culture armour inversion (`CROSS_CULTURE_ARMOUR_INVERSION`)

**WARNING.** The cross-kingdom half of the ladder rule (#581). The edge gate above compares a troop
with its own parent; this one compares a culture's tier with the other kingdoms. For every
`(culture, engine tier)` cell (culture = the `troops_<culture>.xml` stem, tier =
`clamp(ceil((level - 5) / 5), 0, 10)`) the validator takes the median of its troops' armour totals
(the same per-slot battle-set average as the edge gate, summed over the five slots) and holds it
against the median of every OTHER culture's median two tiers lower. It warns when the cell sits more
than 20 points under that field and at least three other cultures hold a cell at that lower tier.
The arithmetic is the pure `taom_schema.cross_culture_armour_inversions`, which
`tools/analyze_kingdom_armour.py` also calls for its gate preview, so the report and the validator
cannot disagree. Since the kingdom-cap curve (#583) every ITEM's value is first scaled to the 57
reference cap of the line it belongs to (`scale_to_reference_cap`, `item_cap_for`: the item's
`LOTRLOME_items` folder from `Registries.item_folder`, routed through `rebalance_armor.kingdom_key`
so a Black Numenorean piece is 57-cap kit whoever wears it), because kingdoms differ in armour
power by design and a goblin at 38 under a dwarf at 70 is not a finding; what remains is a troop
dressed below the power of its own kit. Item level rather than culture level: the first draft
scaled by the wearer's culture, and an Umbar noble in full Black Numenorean plate read 29% higher
than it was (deep review, 2026-09-13). Without the curve module the check runs unscaled. Skipped
without the install, like the edge gate. Left out: `characters/npcs_*.xml`
(villagers are not a culture), the `_BODYLESS_BY_DESIGN` troops (their totals are not comparable),
and `_ARMOUR_LADDER_EXEMPT`, an allowlist with a reason per id (`cave_troll`, the two Harad mount
riders, `gondor_ithilien_ranger`); a test fails when an exempt id no longer exists. First run
2026-09-12: four cells, `goblin/tier7`, `gondor/tier9`, `lindon/tier10`, `rivendell/tier10`. The
repair is a roster, item or curve decision, not a script; the options are in
`docs/features/armor-balance.md` "Kingdom armour ladder".

## Ranged ladder inversion (`RANGED_LADDER_INVERSION`)

**WARNING.** The ranged half of the ladder rule (#582). An archer's reach is its launcher's
`missile_speed` and nothing else (`Mission.cs:4943` launches at the bow's speed,
`SandboxAgentStatCalculateModel.cs:978` pins `MissileSpeedMultiplier` at 1 for bows; Bow skill feeds
accuracy, cadence and AI error). Two rules, per launcher class: inside a LINE (a `troops_<culture>.xml`
file, or an id prefix inside one, ranked in `tools/ranged_ladders.json`) a lower tier never beats a
higher tier; at the same TIER a better-ranked line never loses to a worse-ranked one. (#582 judged
the second rule inside a BAND of tiers, E T0-2 to C T9-10, on speed alone, from a
`band_base[band]` grid; #617 made it per tier and per stat, below.) The cells come from per-tier
curves in the spec, speed being `tier_base[t] + rank_step * (worst overall rank - overall rank)`
(the others are in `docs/features/ranged-ladders.md`), and the validator, the report and the
roster tool all call the pure `ranged_ladder.inversions`, so they cannot disagree. Speeds are
`Registries.launchers` (every Bow and Crossbow `<Item>` over the same item roots as the armour
index), so without the install the check is skipped, never faked. A spec that cannot be read or
contradicts the install, and a ranged troop in a file no line claims, are findings rather than
silence; `_RANGED_LADDER_EXEMPT` holds off-ladder troops with a reason each (empty today). One
warning per (rule, class, scope) naming the worst pair and the pair count. First run 2026-09-12:
1,741 pairs (the worst `dunland_dragon_firebolt` T5 at 97 over `sagarun_crossbowman` T5 at 60); the
repair is `tools/generate_ranged_ladder_items.py --apply` then
`tools/rebalance_ranged_ladders.py --apply`, which left 0. Design and grid:
`docs/features/ranged-ladders.md`.

**Since #617 (2026-09-18) the rules run per stat, not only on speed.** Speed, damage and accuracy of
the launcher and the troop's own Bow or Crossbow each obey both rules, and rule 2 compares troops at
the same TIER (the band now only picks a donor mesh) on that stat's own rank list (lines rank overall,
damage and accuracy separately). A troop at a tier its line lists no cell for is a finding too: the
generator makes no item there. The first per-stat run found 709 pairs, every one because each tier
carried its donor's damage and accuracy; the repair left 0.

## Ranged damage ceiling (`RANGED_DAMAGE_CEILING`)

**WARNING.** A bow or crossbow that a Lord, a Wanderer or any `is_hero` character outside `troops/`
can carry, through its own equipment, the battle `EquipmentSet` rosters it names, or a template
`lords.xslt` hands a retagged vanilla lord (`ranged_ladder.hero_launchers`, `Equipment` matched in
either case), or that a roster the game applies to the player at runtime hands out
(`player_char_creation_*`, `player_career_*` and the enlistment quartermaster's `enlist_*`, which no
`NPCCharacter` names), whose `thrust_damage` is above `hero_ceiling` in `tools/ranged_ladders.json`
(Bow 90, Crossbow 105). Civilian sets are skipped, whether the roster or an inner `<EquipmentSet>`
carries the flag. The Armory's own launchers are what heroes and the shops hand out: before #617 its
38 ran 62 to 130 damage at accuracy 70 to 100, and nine that a hero or the player could carry hit 92
to 112 (measured 2026-09-18 against the pre-restat file). One path the sweep cannot see: Field
Commission (`HeroCommissionAdapter`) copies a troop's first battle set onto the new companion in C#.
Repair: the item's row in the spec's `donor_stats`, then `python tools/restat_ranged_donors.py
--apply`. Skipped, never faked, without the install.

## Generator retired-item refs (`GENERATOR_RETIRED_ITEM_REF`)

**WARNING.** Every other pass reads the XML that ships. None reads the Python that writes it, so a
generator or apply script can sit under `tools/` naming items the Armory retired months ago, and
nothing says so until somebody re-runs it: a troop spawns naked (`BROKEN_ITEM_REF` on the output,
if the validator is run afterwards) or a battle hangs on a missing collision body (#352). Found
2026-09-13 while porting KEYforce's reference repair: the Gondor and Rhûn troop-tree scaffolders,
the character-creation, career and starter-armour tables and both wanderer generators held 67 such
ids between them, 21 of which had never existed in this Armory at all. The pass lives in
`tools/check_generator_item_refs.py`: each registered generator is read either by running it and
taking every `id="Item.X"` it prints (the scaffolders) or by importing it without running and
walking its named tables (dicts, lists, tuple rows and embedded XML template strings; folder and
culture labels are skipped). Every id is then resolved against `Registries.items`, the same set
`BROKEN_ITEM_REF` uses, so the live `LOTRLOME_Armory` item XML and the vanilla modules are the
authority. One warning per generator, naming the first eight ids and the count; a generator that
cannot be read at all is a finding under the same code, never a pass. Without the install the
registry is TAOM-only, so the pass is skipped and says so. The standalone CLI exits 1 on any
finding and 2 without the install; `tools/tests/test_check_generator_item_refs.py` carries the same
check as a live-install gate. **Add a script to `GENERATORS` when it writes item ids into
ModuleData.** A one-off swap map names retired ids on its FROM side by design and does not belong
there.

## Borrowed collision body (`COLLISION_BODY_BORROWED`)

**ERROR.** The question `MISSING_COLLISION_BODY` cannot ask. That check asks whether a `body_name`
resolves, and a borrowed body resolves: it is some other mesh's twin, shipped in some other pack.
This asks whether the body is this mesh's own.

The convention is `docs/ai-includes/weapon-creation-workflow.md` Step D: a weapon body is `bo_` plus
the exact mesh id, authored into the mesh's own FBX. That doc also sanctions borrowing a same-shaped
body from another kit as a placeholder until the artist delivers. #633 is what happens when the
placeholder ships: three Rhun longbow meshes (`sm_rh_drag_longbow_a`, `sm_dg_khml_longbow_a`,
`sm_rh_loke_longbow_a`) had no twin, carried `bo_wm_elven_bow_a03`, and six generated `ladder_*`
clones inherited it by `copy.deepcopy`. A borrow is a dependency on art the borrower does not own:
the 2026-09-11 art drop had already renamed that body once (#599), and every "does it resolve" gate,
`audit_armory_refs.py` included, read CLEAN on 2026-09-20.

**A borrow is a body that is provably another mesh's twin.** For a `body_name` an Armory tpac ships:
if it equals `bo_<mesh>` or `bo_cap_<mesh>` it is the item's own; if it is `bo_<M2>` or
`bo_cap_<M2>` for a shipped mesh M2 that is not this item's mesh, it is a borrow; if no shipped mesh
matches (`bo_uruk_halberd_blade_a1` for `sm_uruk_halberd_blade_a1`) it is the artist's own naming
and nobody's twin. Refs are paired with their owner's `mesh` by item id, which for a
`<CraftingPiece>` needed `validate_mesh_refs._ITEM_OPEN_RE` to learn that element: before #633 every
piece ref carried item id `""` and all 313 piece bodies compared against one mesh.

Three exemptions, each measured on the live install on 2026-09-21:

- **Same-kit sharing is design.** A kit is the first two name tokens after the `sm_`/`wm_`/`bo_`
  prefix (`rh_drag`, `elven_bow`, `rivendell_sword`). The ruby and topaz Aranruth blades, the silver
  and black Rivendell swords, the `_a2` Erebor axe on `_a`'s body: one geometry, several textures.
  20 shipped rows.
- **Authorised cross-kit shares** live in `_SHARED_BODY_BY_DESIGN` with a reason each: Dragon and
  Khamul are re-textured Loke geometry, so the three Rhun kits share bodies (Mike, 2026-09-21).
  38 shipped rows.
- **Vanilla bodies and shields.** A Native body is resident whatever TAOM does and vanilla art is
  never our placeholder. A shield carries the `bo_cap_*` capsule in `body_name` and the full body in
  `shield_body_name`, and sharing a sibling culture's is the convention
  (`docs/modding/items-shields.md`). A body no pack ships is left to `MISSING_COLLISION_BODY`.

Result: 0 findings on items and 0 on crafting pieces after the #633 repair; the nine #633 items fire
under the rule (proved by restoring the borrow on one donor: one ERROR, `LOTRAOM_weapons.xml:8643`).

**What this gate is not.** The first version, `COLLISION_BODY_FOREIGN_PACK`, compared the tpac that
ships the body with the tpac that ships the mesh, on the theory that the release cook keeps a source
tpac together and a body cooked into a different `AssetPackages/pack*.tpac` than its mesh is absent
when the mesh loads. The `/deep-review` of 2026-09-21 refuted that against the shipped
`E:\LOTRAOM_Releases\patreon` tree: the cook groups every one of the Armory's bodies into `pack0`
and `pack1` and every mesh into the other packs, so the working elven and Isengard bows are split
exactly like the broken Rhun ones, and `bo_wm_elven_bow_a03` is present in that tree's `pack0`. The
engine resolves a body by name, process-wide (`PhysicsShape.GetFromResource`). Pack co-residency is
neither achieved nor needed, and a draft that flagged every split pairing returned 430 findings.
Why the player's build hung is therefore still open: the leading hypothesis is a build whose packs
predate the 2026-09-11 art drop while its XML carries the post-#599 name, the plain #599 class.

The repair is to author the twin, not to repoint the borrow:
`tools/blender/add_collision_body.py --fbx <file> --mesh <MeshObject> --material <physics_material>
--apply`, then a Modding Kit import of that FBX, or the new name exists in no tpac.

Skipped, never faked, without the install; a run that finds no Armory packs is itself a finding.
About 3.5 s, almost all of it `validate_mesh_refs.extract_refs` (the TOC scan is 0.2 s). Tests:
`tools/tests/test_collision_body_borrowed.py` (14, synthetic, no install needed).

## Key Files

| File | Purpose |
|---|---|
| `tools/taom_schema.py` | Engine (issue model, registries, schema model, `Validator`, `build_registries`, `build_settled_cultures`, report) |
| `tools/taom_query.py` | Query API over the engine (`item_exists` / `troop_exists` / `culture_exists` / `find_references` / `validate` / listings) — backs the MCP server, pure stdlib |
| `tools/validate_moduledata.py` | CLI front-end (batch report) |
| `tools/taom_mcp_server.py` | MCP stdio server front-end (9 tools via the `mcp` SDK / FastMCP) |
| `tools/tests/test_taom_mcp_server.py` | in-process MCP server tests (skip if `mcp` SDK absent) |
| `tools/schemas/taom_npccharacter.json` | Troops + characters + wanderers + companions + education templates |
| `tools/schemas/taom_spcultures.json` | Cultures |
| `tools/schemas/taom_equipmentsets.json` | Equipment rosters (all `equipmentsets/*.xml`) |
| `tools/tests/test_validate_moduledata.py` | 167 unittest cases (validator; counted 2026-09-18) |
| `tools/validate_xml_schemas.py` | Engine-XSD layer: every engine-loaded file against its engine schema; also the `SCHEMA_INVALID` pass |
| `tools/tests/test_validate_xml_schemas.py` | 47 unittest cases on synthetic modules, plus a repo-baseline gate (skips without the install or lxml) |
| `tools/tests/test_taom_query.py` | unittest cases (query API) |
| `tools/tests/test_collision_body_borrowed.py` | 14 synthetic cases for `COLLISION_BODY_BORROWED`: the #633 shape, own twin, own capsule, variant name, same-kit share, the Rhun family, vanilla, shields, missing body, per-piece pairing, no packs, a raising scan, `_kit`, `_twin_owner` |
| `tools/tests/test_add_collision_body.py` | 9 cases for the twin-authoring tool's pure parts (`bpy` stubbed): the three required arguments, the report path under bad arguments, the round-trip comparison |
| `.claude/hooks/check-moduledata-validation.sh` | PreToolUse commit gate (blocks on ERROR; fail-open) |
| `.claude/rules/moduledata-validation.md` | Auto-loaded rule when editing the covered XML / schemas |

## Dependencies

The **engine + query API + CLI** use the **Python 3 standard library only** (`re`, `json`, `glob`, `fnmatch`, `dataclasses`, `enum`, `pathlib`), no pip install. The **MCP server** additionally needs the **`mcp` Python SDK** (FastMCP); it is present in this environment. The **XSD layer** (`validate_xml_schemas.py`, `SCHEMA_INVALID`) needs **`lxml`** and the engine's `XmlSchemas` folder; without either it reports that it did not run (the CLI exits 2, the validator pass emits one WARNING), and CI's tools-tests job, which installs nothing, skips its lxml tests. Registries are built from the installed game at `E:\Steam\...\Modules` (override with `--game-modules`, or the `BANNERLORD_GAME_MODULES` env var for the MCP server) + `Main/_Module/ModuleData`.

## Tests

```bash
python -m unittest discover -s tools/tests -p "test_*.py"
```

`test_validate_moduledata.py` — 75 cases, one per issue code (positive + negatives) plus edge cases (Item.None allowed, malformed XML doesn't crash, file matching no schema is still swept, fail-fast on unknown rule / missing field, the Codex-fix regressions: culture-registry pollution, comment-stripping, child-template civilian, education-template exclusion, the harness family-type set: missing attribute, cross-set non-pairing, ambiguous monster ids, degraded mode, and the mounted-dwarf set: both rules, an absent `race`, the lowercase `<equipment>` spelling, and a non-dwarf positive control). `test_taom_query.py` — the query API (existence checks incl. prefix/sentinel/duplicate, `find_references` with line numbers + comment-stripping, `validate` counts + code filter, listings). `test_taom_mcp_server.py` — in-process MCP tests (`list_tools()` returns all 9 tools; `call_tool()` for culture/schemas/registry/validate; install-independent, skips if the `mcp` SDK is absent). Full suite: **297 tests** across 12 files in `tools/tests/`.

## How-To

```bash
# Full validation against the installed game registry
python tools/validate_moduledata.py

# Only one check, write machine-readable output, treat warnings as errors
python tools/validate_moduledata.py --code BROKEN_ITEM_REF --json report.json --warnings-as-errors

# On a machine without the game install (item/troop/party checks auto-skip)
python tools/validate_moduledata.py --game-modules /nonexistent
```

**To add a new check:** prefer adding it to a schema (`enums`, or a new `special_rules` handler registered in `KNOWN_SPECIAL_RULES` + a `_*_rule` method). New cross-reference prefixes go in `REF_KINDS`. Add a test for the new issue code (positive + negative) — the suite is expected to cover every code the engine can emit.

## MCP server (interactive querying)

`tools/taom_mcp_server.py` is a stdio MCP server (FastMCP) that exposes the query API as tools, so a Claude agent can check mod-data integrity mid-task instead of grep-and-hope. Nine tools:

| Tool | Returns |
|---|---|
| `validate_moduledata(codes?)` | `{error_count, warning_count, issues[]}` — the full validation, optionally filtered to specific codes |
| `item_exists(item_id)` | `{id, exists, duplicate_in[]}` (bare or `Item.`-prefixed; `duplicate_in` non-empty if defined in >1 Armory folder) |
| `troop_exists(troop_id)` | `{id, exists}` |
| `culture_exists(culture_id)` | `{id, exists}` (e.g. `rohan` → false; the StringId is `vlandia`) |
| `party_template_exists(template_id)` | `{id, exists}` (bare or `PartyTemplate.`-prefixed) |
| `find_references(target_id, kind?, limit?)` | `{target, kind, count, truncated, references[{file,line,kind,ref}]}` (`kind` ∈ item/troop/culture/party_template, `npccharacter` aliased to troop; default `limit` 200) |
| `list_cultures()` | every valid culture StringId |
| `registry_sizes()` | `{items, npccharacters, cultures, party_templates}` — confirms the game install was found |
| `list_schemas()` | the schemas + what each checks |

**Activation** (one-time; the server can't be loaded mid-session — Claude reads MCP config at startup):
1. Ensure the `mcp` Python SDK is installed (`python -c "import mcp.server.fastmcp"` — present in this environment).
2. It is registered in [`.mcp.json`](../../.mcp.json) as the `taom-moduledata` stdio server and enabled in [`.claude/settings.local.json`](../../.claude/settings.local.json) → `enabledMcpjsonServers`.
3. **Restart Claude Code** to load it. Its tools then appear as `mcp__taom-moduledata__*` (deferred — schemas fetched via ToolSearch on demand).

**Smoke-test standalone** (no restart needed): `python tools/taom_mcp_server.py` starts the stdio server; or verify in-process:
```python
import asyncio, taom_mcp_server as srv
asyncio.run(srv.mcp.list_tools())          # 9 tools
asyncio.run(srv.mcp.call_tool("culture_exists", {"culture_id": "rohan"}))  # exists=False
```

The server resolves data paths from its own location, so it is cwd-independent; the game-modules path comes from `BANNERLORD_GAME_MODULES` (env) or the default Steam path, and degrades to TAOM-only registries if absent (same as the CLI).

## Performance

One pass over `Main/_Module/ModuleData/**/*.xml` **plus every `extra_ref_root`** (regex,
line-numbered) + a registry build that scans the game-module item/character/culture/party-template/
body-property XML once. 259 TAOM files + 382 Armory files = 641, each read once, patterns
pre-compiled. Full live run completes in a few seconds; ~27k item refs + ~2.9k troop refs resolved
per run.

## Known Scope (intentional)

Out of scope (documented as coverage gaps, not bugs): armor `covers_legs`/`covers_hands`
(Armory-side schema), weapon-craft piece refs, scene refs (covered by `audit_scene_names.py`), and
inline `EquipmentSet`-by-id refs to vanilla rosters. `BodyProperty.` refs **were** on this list and
are now checked (2026-08-03).

### Foreign-module sweep (`extra_ref_roots`)

The CLI passes `LOTRLOME_Armory/ModuleData` as an extra ref root. TAOM authors item XML directly
into that module (see `/author-armor`), so it is TAOM's to keep correct even though it lives
outside this repo and outside git. Extra roots are swept for **cross-references only** — the schema
contracts (duplicate ids, civilian `equipmentType`, enums) describe TAOM's own files and must not
report defects against a module this validator does not own.

Today that sweep is load-bearing for `Culture.` refs (104 Armory files carry them) and effectively
vacuous for the other four kinds — Armory XML currently contains no live `NPCCharacter.`,
`PartyTemplate.` or `BodyProperty.` refs, and its single `Item.` hit is inside a comment. The
wiring is correct and will catch a real dangling ref if one is introduced; just don't read a PASS
as proof those kinds were stress-tested against the Armory.

### Module coverage at a glance (and what is NOT covered)

TAOM's data spans **three modules**, and only one of them is in this repo. `TAOM_Map` and
`LOTRLOME_Armory` live in the game install and are unversioned, which is why CLAUDE.md's
"A fix in a dependency module" trap insists on an in-repo gate beside every external edit.
Counts measured 2026-08-18:

| Module | Location | XML (ModuleData / all) | XSLT | XML well-formedness | Cross-ref sweep | XSLT checked |
|---|---|---|---|---|---|---|
| TAOM | this repo | 259 / 338 | 8 | CI, `Main/_Module/ModuleData/**` | full (259 files), plus schema contracts | CI (8 of 8) + `check_external_xslt.py`; `/xslt-check` maps 6 of 8 |
| TAOM_Map | game install | 44 / 313 | 1 | `check_external_xslt.py` | full (44 files) since #462 | `check_external_xslt.py` |
| LOTRLOME_Armory | game install | 382 / 406 | 7 | `check_external_xslt.py` | **full (382 files)** via `extra_ref_roots` | `check_external_xslt.py` |
| total | | 685 / **1,057** | **16** | | **685 files swept** | **16 of 16** |

The two columns matter: the validator only ever reads `ModuleData`, so the left number is its
ceiling and the right one is the module's whole XML surface. Of TAOM's 259 ModuleData files, 145
are localization files under `Languages/` (12 language folders). The deployed `Modules/TAOM` copy is build output; the repo is
authoritative for it, so do not count it twice. Counts measured 2026-08-18; the 641 is
`len(_xml_files()) + len(_extra_ref_files())` on a live `Validator`.

**The Armory is in better shape than its "foreign module" status suggests.** All 382 of its
ModuleData XML are swept for dangling refs, and it contributes to two registries, not one: items via
`item_roots`, and `<Monster>` declarations via `build_harness_registries`, which feeds
`mount_family_types`. What it does *not* get is TAOM's schema contracts (duplicate ids, enums,
civilian `equipmentType`), because those describe TAOM's own files. That is currently free: the
Armory defines 3,727 items and 63 monsters and **zero** NPCCharacters, EquipmentRosters, party
templates, cultures or body properties, so the passes that skip it have nothing to miss. That is an
assumption about today's data, not a guarantee, and `/author-armor`'s workflow makes it plausible
someone authors a troop there. Worth an invariant test that fails loudly when it stops holding.

**`TAOM_Map` WAS the sharp gap, closed by #462.** Its ModuleData is now an `extra_ref_root`, so all
44 files and their 1,012 `Culture.` refs are swept; the validator visits 685 files (259 repo, 382
Armory, 44 TAOM_Map). What follows is the shape of the hole, kept because the registry asymmetry it
describes is still true and still the reason the sweep matters. Exactly **two**
of the 45 XML and XSLT files in its `ModuleData` are ever opened (44 XML plus `settlements.xslt`;
the directory holds 88 files in all, the other 43 being 39 `.bak*`/`.prev` copies, three
`DistanceCaches` outputs and `project.mbproj`), both inside `build_settled_cultures` and
`build_settlement_economy`: `settlements.xslt`, read only to evaluate one boolean regex, and
`settlements.xml`. TAOM_Map appears in no other root list, not `item_roots`, not `npc_roots`, not
`pt_roots`, not `culture_files`, and not `extra_ref_roots`.

The consequence is an inversion worth stating plainly. **The validator checks the dead file's refs
and trusts the live one's.** The repo's `Main/_Module/ModuleData/settlements.xml` is a stale shadow
that contributes to no registry, yet its `Culture.` refs are checked on every run. The live
`TAOM_Map/ModuleData/settlements.xml` is the *sole* source of `settled_cultures` and
`settlement_economy`, and its **1,012 `Culture.` references (30 distinct) are never checked for
`UNKNOWN_CULTURE`**. Since that same file is what decides which cultures count as settled, a bad id
there corrupts the `LANDLESS_CULTURE` verdict in one direction or the other with no diagnostic at
all. Verified 2026-08-18: all 30 currently resolve against the 40-entry culture registry, so the
gap is latent, not live.

**Fixed in #462.** `build_extra_ref_roots()` in `validate_moduledata.py` now returns both modules
from one `_EXTRA_REF_MODULES` tuple, and `ExtraRefRootTests` pins the contract in both directions,
including an end-to-end case where a bogus `Culture.` id in a synthetic TAOM_Map root must raise
`UNKNOWN_CULTURE`. The registry asymmetry above is unchanged: TAOM_Map is still the sole source of
`settled_cultures` and still contributes to no other registry. What changed is that its refs are no
longer taken on trust.

**XSLT is barely modelled anywhere.** `_SETTLEMENT_STRIP_RE` matches only an empty
`<xsl:template match="Settlement"/>` or its empty-body form; the file is never parsed as XML and
never transformed. If that live, unversioned stylesheet were malformed, or the strip were rewritten
to an equivalent the regex does not match (extra whitespace, a non-`xsl` prefix, an
identity-suppressing variant), the match returns nothing, vanilla's stripped settlements are counted
as live, and every culture reports as landed. That is precisely the false-clean `LANDLESS_CULTURE`
exists to prevent, and the tests use synthetic fixtures, so they pin the detector and never touch
the live file.

**The 8 external stylesheets had no validation path until #462.**
[`tools/check_external_xslt.py`](../../tools/check_external_xslt.py) now gates all 16 across the
three modules: XML well-formedness always, a root-element check (a stylesheet the engine will
silently ignore is worse than a broken one), and a real stylesheet compile when `lxml` is present.
It is a developer-side script by necessity, since CI cannot see the live modules. The limitation
below is why it exists. `/xslt-check` resolves its target
under `Main/_Module/ModuleData/`, and the CI `validate-xml` job globs that same repo path, so
neither reaches them; CI *structurally* cannot, because those modules are not in the checkout. Two
are read narrowly for unrelated purposes and both fail open: `audit_mount_parity.py` string-replaces
`action_sets.xml` to `action_sets.xslt` and regexes out chariot animations behind an `os.path.exists`
guard, and `weapon_xml/verify.py` regex-checks only the piece ids a given `build_weapon_xml.py` run
just generated, never pre-existing content. Note that `audit_action_set_parity.py`, which CLAUDE.md
names as the gate for the root-`<action>` dedicated-server hazard, reads `action_sets.xml` only and
contains no XSLT handling at all.

Treat this section as the answer to "is my change covered", not as a to-do list someone is working
through.

### Silent-scope guards

Two failure modes make an under-scoped run indistinguishable from a clean one, so both are reported
rather than inferred from the numbers:
- A **missing extra ref root** (renamed/moved Armory) is recorded in `Validator.missing_ref_roots`
  and the CLI prints a WARNING naming the skipped module. Silently dropping it would revert the
  sweep to TAOM-only while still printing PASS.
- A **shrunken registry** (a renamed vanilla `*_bodyproperties.xml`, a typo'd `--game-modules` that
  still resolves to a real directory) is flagged via `Registries.suspect_registries`. Floors are set
  far below real counts (121 body properties, 38 cultures) — they catch "the file list broke", not
  "the data changed a bit". Full shrinkage would otherwise trip the empty-registry guard and skip
  the check entirely.

NPC duplicate-id + enum coverage spans `troops/`, `characters/`, `named_companions/`, `taom_wanderers.xml`, and `taom_education_character_templates.xml` (the `taom_npccharacter.json` `applies_to` set — **add any new `<NPCCharacter>`-defining file there** or its dup/enum checks won't run; Codex review 2026-05-30 caught three uncovered files). The civilian-type rule treats `_civ*` and `child_template_*` rosters as civilian and checks every `<EquipmentSet>`, but **deliberately excludes** `child_education_*` education templates (0/784 are `Civilian`-tagged in real data — an unconfirmed convention; flagging them would be 784 false positives — confirm the convention before extending the rule).

## Changelog

- 2026-09-21: `COLLISION_BODY_BORROWED` added (#633). `MISSING_COLLISION_BODY` asks whether a
  body resolves; a borrowed body does, as another mesh's twin, so three Rhun longbows shipped on
  the elven bow's `bo_wm_elven_bow_a03` while every gate read CLEAN. This asks whether the body is
  the item's own (`bo_<mesh>` / `bo_cap_<mesh>`), with same-kit sharing, authorised cross-kit
  shares (`_SHARED_BODY_BY_DESIGN`), vanilla bodies and shields exempt. The first version the same
  day, `COLLISION_BODY_FOREIGN_PACK`, compared owning tpacs on the theory that the release cook
  strands a body cooked apart from its mesh; the `/deep-review` refuted it against the shipped
  patreon tree (every body cooks into pack0/pack1, the working bows are split identically, the
  borrowed body is present there), so the gate was rewritten before it was ever committed and the
  cause of the player's hang is recorded as open. Also fixed on the way: `validate_mesh_refs.
  _ITEM_OPEN_RE` now recognises `<CraftingPiece>`, so a piece's `<BladeData body_name>` is
  attributed to the piece (every piece ref used to carry item id `""`). Wired into `main()` and
  the hook's `--code` list (`CommitGateCoverageTests` pins the pair); 0 findings on the live
  install; about 3.5 s. Repair: `tools/blender/add_collision_body.py` plus a Modding Kit import,
  never a different borrow. Like the other passes `main()` adds, the MCP's `validate_moduledata`
  does not run it (#623).
- 2026-09-19: `SKILL_TEMPLATE_MISMATCH` replaces `SKILL_TEMPLATE_SHADOWS_SKILLS` (#626). The old code, emitted
  from the Validator's upgrade index over `troops/` and `characters/npcs_*.xml`, refused any character that
  declared both a `skill_template` and inline `<skills>` rows, on the 1.4.8 rule. Since 1.5.2 the engine lays the
  rows over the template, so the new pass in `validate_moduledata.py` errors only on a row that differs from the
  SkillSet (or a template with rows that names no SkillSet), over every XML and XSLT file of the repo module,
  lords included, and on a run that finds no templated character at all. Detection is
  `sync_lord_inline_skills.py`'s own, so the fixer and the gate agree. The hook's `--code` line follows
  (`CommitGateCoverageTests` pins the pair) and its trigger now includes ModuleData `*.xslt`. 0 findings on the
  live install (1,528 blocks); the pass costs about 0.2 s. Like the other passes `main()` adds, the MCP's
  `validate_moduledata` does not run it (#623).
- 2026-09-18: `SCHEMA_INVALID` added (#621): the engine-XSD layer from `tools/validate_xml_schemas.py`, run on the
  repo module so the commit hook gates it (the hook's `--code` list carries it; `CommitGateCoverageTests` pins the
  pair). Found in the same review: `missing_collision_body_issues` is defined but `main()` never calls it, so
  the 2026-09-15 entry below overstated what shipped: `MISSING_COLLISION_BODY` had never fired, and its hook line
  blocked nothing. Wired into `main()` the same day (#622), with a test that drives `main()`; 0 issues on the
  live install; the pass adds about 3 s (the validator went from about 5.9 s to about 9 s), and it now scans only the packs that failed to parse and turns a crash into a NOT-verified ERROR. A Modules folder missing a live module no
  longer crashes `main()` either (the missing-root warning called `.parent` on a string).
- 2026-09-13: `FORTIFICATION_WITHOUT_VILLAGE` added (#597): every town and castle in the live world
  must have a bound village, and `build_settlement_economy` now records `bound` per village. Found
  Serelond, Methir and Framsburg empty; all three gained villages the same evening.
- 2026-09-15: `MISSING_COLLISION_BODY` (error) and `MISSING_VISUAL_MESH` (warn) added (#599): the
  `validate_mesh_refs.py` Tier B + C findings, emitted here so the commit hook, the MCP tool and
  `/verify` see the #352 infinite-load class without a second command. The 2026-09-11 art drop
  retired the elven bow bodies and 18 refs kept the old names; the elf start hung on every
  tournament for two days. Hook allowlist carries the error; `CommitGateCoverageTests` now scans
  both emitting files. The troop join and the committed report live in `tools/audit_armory_refs.py`
  ([armory-ref-audit.md](armory-ref-audit.md)).
- 2026-09-13: `GENERATOR_RETIRED_ITEM_REF` added: the tools/ generators' own item tables are now
  resolved against the live install, after seven of them were found writing 67 retired or
  never-defined ids. Standalone CLI `check_generator_item_refs.py` and a unittest gate carry the same
  check.
- 2026-08-28: `MOUNTED_DWARF` gained the war-ram carve-out (`WAR_RAM_MOUNT_IDS`, #515): a dwarf
  carrying `taom_war_ram_a`/`_b` is legal, cavalry `default_group` included, and every other mount he
  can reach still errors. The work also closed a latent hole in the pre-existing code: mount
  resolution was first-wins, so once an allowlist existed a ram listed ahead of a horse would have
  masked the horse. `_mounts_in` now returns every Horse slot and both the inline equipment and every
  named roster are read. Same issue recorded the structural blind spot above: career starting rosters
  are applied at runtime and named by no `NPCCharacter`, so this validator never saw
  `player_career_erebor_cavalry_m`/`_f` equipping `Item.saddle_horse`. Coverage for those moved to
  `CareerCultureCoverageTests`.
- 2026-08-04 — Added `MOUNTED_DWARF` (pass 6). Asked to confirm no dwarven lord is cavalry, the
  audit found the data already compliant — 185 `race="dwarf"` characters, all Infantry or Ranged,
  no lord roster carrying a mount — so the work became pinning the invariant rather than fixing it.
  Checks both the `default_group` enum and reachable Horse slots, because decompiling v1.4.7 showed
  `CharacterObject.GetFormationClass()` ignores `default_group` for heroes and reads equipment
  instead: an enum-only check would pass a lord who still spawns mounted. `"erebor"` also dropped
  from `HORSE_CULTURES` in `tools/fix_lord_cultures_and_mounts.py`, which would otherwise have
  injected `Item.charger` into the Erebor lord rosters if that (currently broken) script were repaired.
- 2026-08-04 — Added `LANDLESS_CULTURE` (pass 4b) and the `settled_cultures` registry
  (`build_settled_cultures` — load-order walk that honours TAOM_Map's unconditional
  `<xsl:template match="Settlement"/>` strip, size floor 15). Came out of crash `099f650c`: TAOM's
  `battania` is the authored Variag culture, its K-series settlements were never migrated with it,
  and vanilla `SpawnLordParty`'s unguarded `Settlement.All.First(culture)` threw on the daily clan
  tick. 18 errors before the Khand settlement retag
  (`tools/oneoff/retag_khand_to_variag.py`), PASS after. Runtime guard:
  [`lord-spawn-guard.md`](./lord-spawn-guard.md) (#374).
- 2026-08-03 — Added `BROKEN_BODY_PROPERTY_REF` (registry from the 4 authoritative
  `*_bodyproperties.xml` files, 121 ids) and the `extra_ref_roots` foreign-module sweep, wired to
  `LOTRLOME_Armory/ModuleData`. Both came out of the dwarf-vs-Rhûn crash investigation, whose log
  showed three dangling refs this validator could not have caught
  (`docs/reviews/investigation-rhun-dwarf-ctd-2026-08-02.md`). The deep review then found the new
  sweep could skip silently and print PASS; `missing_ref_roots` + `suspect_registries` close that
  (`docs/reviews/rca-validator-silent-scope-2026-08-03.md`).
- 2026-05-30 — Initial schema-driven ModuleData cross-reference validator: unified `taom_schema.py` engine + `validate_moduledata.py` CLI + 3 schemas catching the recurring bug classes (broken item/troop/culture/party-template refs, duplicate ids, missing civilian type, invalid enum); wired in as an auto-loaded scoped rule + a commit-blocking PreToolUse hook. Same dated entry covers the 2026-05-31 follow-up: the `taom_query.py` query API + `taom_mcp_server.py` MCP server (9 tools) and a second deep-review pass. See repo-root `CHANGELOG.md` for full detail.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/doc-health-linter.md](./doc-health-linter.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/cultures.md](../modding/cultures.md)
- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/equipment-rosters.md](../modding/equipment-rosters.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/id-cheatsheet.md](../modding/id-cheatsheet.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/modding/items-shields.md](../modding/items-shields.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](../modding/lords-and-heroes.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/npcs-notables-and-townsfolk.md](../modding/npcs-notables-and-townsfolk.md)
- [docs/modding/party-templates.md](../modding/party-templates.md)
- [docs/modding/README.md](../modding/README.md)
- [docs/modding/recipe-add-a-culture.md](../modding/recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](../modding/recipe-add-a-kingdom.md)
- [docs/modding/recipe-retire-content.md](../modding/recipe-retire-content.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/modding/troops.md](../modding/troops.md)
- [docs/modding/troubleshooting.md](../modding/troubleshooting.md)
- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)
- [docs/modding/wanderers-and-named-companions.md](../modding/wanderers-and-named-companions.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/engine/formations-and-team-ai.md](../reference/engine/formations-and-team-ai.md)
- [docs/reviews/lessons/xslt-moduledata.md](../reviews/lessons/xslt-moduledata.md)
- [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../reviews/rca-tournament-dwarf-dismount-2026-06-09.md)
- [docs/reviews/rca-townsfolk-sex-2026-09-06.md](../reviews/rca-townsfolk-sex-2026-09-06.md)

<!-- backlinks-end -->
