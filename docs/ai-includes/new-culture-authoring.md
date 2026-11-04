# New Culture — Armor + Troop Tree Authoring Guide

End-to-end repeatable process for adding (or substantially revising) a TAOM culture's **armor set + troop tree + recruitment wiring**, modeled on the Dale session (May 2026, ~11 commits). Use this when:

- Solus delivers a new culture's `.tpac` armor pack and you need to wire it into the engine, OR
- An existing culture needs a fresh troop tree (e.g. revamps tracked in #99 / #211 / #212 / #224 / Dale).

If you're creating a **net-new culture entry in `taom_spcultures.xml`** (the underlying Culture object), read [`docs/cultures.md`](../cultures.md) FIRST — that covers the ~80 culture attributes / 16 NPC files / equipment rosters needed for the culture itself. This guide picks up after the culture definition exists, with the visible-in-game troops + armor flow.

**Promoting a borrowing kingdom, or cloning a realm's data:** start from `tools/promote_borrowed_cultures.py` and [recipe-add-a-culture](../modding/recipe-add-a-culture.md), then use Phases 1-3 here only for new armour and troops.

## TL;DR

1. **Armor**: enumerate Solus's mesh IDs from `.tpac` → write generator script → emit XML files → register `<culture>/` folder in `LOTRLOME_Armory/SubModule.xml`.
2. **Troops**: lore-research → tier design on paper → write generator script → emit `troops_<culture>.xml` → register in `Main/_Module/SubModule.xml`.
3. **Wire** the culture in `spcultures.xslt` (every CultureObject template/troop attribute), `taom_partyTemplates.xml` (10 standard templates), `VolunteerRecruitmentService.cs` (culture + optional settlement/clan pools), and add tests.
4. **Validate** via `python tools/validate_moduledata.py` (read its warnings), then `/verify` → `/deep-review` → RCA + commit. `/review-codex` runs only when the maintainer asks for it (paid).
5. **Expect 5–10 iteration commits** after the initial ship (renames, equipment swaps, balance tuning, settlement-specific recruitment, color schemes). That's normal — the first ship is a draft.

---

## Phase 0: Prerequisites checklist

Before starting, confirm all of these. Skipping any forces a rework later.

- [ ] **Solus's `.tpac` packs** are at `<armory>/Assets/<culture>_kingdom/sr_<culture>_kingdom_{boots,chests,gauntlets,helmets,shoulders}_geo.tpac`. Verify the 5 files exist before promising a delivery date.
- [ ] **Culture ID decision**. Check `.claude/rules/xml-data.md` Culture StringIds and [id-cheatsheet](../modding/id-cheatsheet.md):
  - Custom culture (`gondor`, `erebor`, etc.) — adds rows in `taom_spcultures.xml`.
  - XSLT passthrough culture (vanilla `vlandia`, `sturgia`, `empire`, `aserai`, `khuzait`, `battania`) — rename via `spcultures.xslt`. Dale uses `sturgia`; Rohan uses `vlandia`; Khand uses `battania`; etc.
- [ ] **Tier cap decision**. Dale stops at level 31 (engine tier 6); Gondor runs to level 51 (tier 10); TAOM's `MaxCharacterTier` is 10. Pick before authoring skill curves. Armour power is a separate decision: the kingdom's chest cap in `rebalance_armor.KINGDOM_CAPS` (#583), which the restat applies to every item by its lowest wearer's band.
- [ ] **Kingdom name**. The kingdom's `name` is the bare realm name (`Erebor`, `Lindon`, `Ered Luin`); "Kingdom of X" goes in `title` and on the faction card only. `KingdomNameTests` fails it, and [recipe-add-a-kingdom.md](../modding/recipe-add-a-kingdom.md) "Gotchas" says how to rename one without a translator run or the cache bringing the old name back (#1050).
- [ ] **Lore brief**. Pick 3–4 Tolkien primary-source citations (Hobbit, LOTR appendices, Unfinished Tales) for the culture's military identity. These inform troop naming and weapon-family choices.
  - For canonical naming + geography, see [reference/external-resources.md](../reference/external-resources.md) § LOTR/Tolkien — esp. the **RealElvish naming generators** (Sindarin/Gondor vs Old-English/Rohirrim patterns) and the **Atlas of Middle-earth** (settlement placement / travel-days). Cross-check any name against Tolkien Gateway before committing.

---

## Phase 1: Armor manifest + generator

### 1a. Harvest mesh IDs from `.tpac`

The `.tpac` files are binary. Use the spider-skeleton scanner with `--all-types` to list every AssetItem name:

```bash
for f in boots chests gauntlets helmets shoulders; do
  python tools/tpac_skeleton_scan.py \
    "<armory>/Assets/<culture>_kingdom/sr_<culture>_kingdom_${f}_geo.tpac" \
    --all-types
done | grep -oE "name='[^']+'" | sed -E "s/name='(.+)'/\1/" \
     | grep -v "\.fbx$" | sort -u > tools/<culture>_armor_meshes.txt
```

Check the manifest in: review Solus's naming conventions, watch for **typos** (Dale had `chivlary` / `infrantry` on 4 slots, `chivalry` on the chest slot — preserve verbatim, the engine binds by exact mesh name).

Watch for **missing variants**. Solus often delivers partial coverage on the shoulder slot (e.g., Dale mariner shoulders have `a01/a03/b01/b03` only — `a02/a04/b02/b04` don't exist). The generator must fall back to the next-lower available variant for those.

Watch for **`_slim` female-fit meshes** and **`clo_*` cloth overlays**. Slim variants pair with their base via `has_gender_variations="true"` (engine auto-derives the `_slim` mesh name). Cloth overlays are authored as separate body-armor items at reduced stats.

### 1b. Write `tools/generate_<culture>_armor.py`

Clone [`tools/generate_dale_armor.py`](../../tools/generate_dale_armor.py) (the most complete example). The structure is:

- **`STAT_TIERS`** dict — `{slot}.{tier} → {stats}` table. Cloned verbatim from Gondor/Dale; don't retune per culture unless intentional.
- **`MATERIAL_BY_CLASS_TIER`** dict — `{class}.{tier} → (material, modifier_group)`. This is the **only culture-specific data table**. For Dale archers: light→Cloth, medium→Leather, heavy/elite→Chainmail. Customize per Solus's design.
- **`COVERS_HANDS_FALSE`** set — explicit allow-list of bracers/gauntlets that should NOT cover the hand mesh (visual: shows fingers below the wrist guard). The rest get `covers_hands="true"`.
- **Parser** — regex over the manifest splits `[clo_]sk_<culture>_[<sub-region>_]<slot>_<class>_<variant>[_slim]`. Map components to slot key + material lookup.
- **`generate_item_xml`** — emits one `<Item>` block per mesh ID. **Always** include `<Flags UseTeamColor="true" />` for banner-tint support. Set `culture="Culture.<id>"` (use the XSLT-host culture for passthrough cultures, e.g. `Culture.sturgia` for Dale).

### 1c. Build per-class color scheme upfront

Adopt the **bronze (`a`) / silver (`b`) convention** Solus uses across all of Dale (and presumably future cultures):

| Suffix | Color | Typical use |
|---|---|---|
| `a01`..`a04` | Bronze | Lower-rank, regional, or one-side of a light/heavy split |
| `b01`..`b04` | Silver | Royal-tier elite, or the other side of a light/heavy split |

For lines with 2 rosters per troop:

| Pattern | When to use |
|---|---|
| 1 variant per tier (both rosters identical armor) | Multi-tier line with 4+ troops covering 4 variants — clean per-tier identity, weapons differentiate the rosters. Used for Dale archer + crossbow. |
| `aNN` + `bNN` per tier (one bronze + one silver roster) | "Variation per level" — both colors visible at every rank. Used for Dale royal infantry. |
| `aNN` (roster A) + `bNN` (roster B) at a split point | A single troop straddling two branches. Used for Dale Merchant Guard (T4 cavalry root with light=a and heavy=b children). |
| Overlap pattern (T4: a01+a02, T5: a02+a03, ...) | When mesh count > tier count and you want continuity between adjacent ranks. Slight visual carry-over. |

### 1d. Generate + register

```bash
python tools/generate_<culture>_armor.py --dry-run    # sanity-check the output
python tools/generate_<culture>_armor.py --apply      # writes 5 XML files
```

Register the new folder in `LOTRLOME_Armory/SubModule.xml`:

```xml
<XmlNode>
    <XmlName id="Items" path="LOTRLOME_items/<culture>"/>
    <IncludedGameTypes>
        <GameType value = "Campaign"/>
        <GameType value = "CampaignStoryMode"/>
        <GameType value = "CustomGame"/>
        <GameType value = "EditorGame"/>
    </IncludedGameTypes>
</XmlNode>
```

**Smoke test**: `python -c "import xml.etree.ElementTree as ET; ET.parse('<armory>/.../<culture>/head_armors.xml')"` — fast well-formedness check before booting the game.

> ⚠ Well-formedness ≠ loaded in-engine. A NEW armor XML file (and a NEW `<XmlName id="Items">` folder registration) only takes effect after a full game **RESTART**, Bannerlord registers the item directory at process launch and globs it (`GetFiles("*.xml")`) at campaign start, with no hot-reload. `validate_moduledata.py` passing does NOT prove the engine loaded the new files; restart + visually confirm troops are clothed in-game (naked-with-green-validator = new-file-not-loaded-until-restart). See `.claude/rules/moduledata-validation.md` ("PASS ≠ in-game loaded").

---

## Phase 2: Troop tree design + generator

### 2a. Lore research pass

Run a brief web-research agent (or use the [`Agent` tool with `subagent_type: general-purpose`](.) and a focused prompt) to surface Tolkien-canon details on:

- Military traditions of the culture (weapons, armor types, signature units).
- Named heroes (Bard, Bain, Brand for Dale; Théoden, Éomer for Rohan) — useful for elite-tier names.
- Geographical sub-regions (Lake-Town vs Dale-proper; Anórien vs Ithilien for Gondor) — drives sub-line distinctions.
- Lore guardrails (e.g., Dale isn't horse-country → cavalry is "decent", not signature; Rohan IS horse-country → cavalry is signature).

Capture findings inline as code comments in the generator so future readers see the lore justification.

### 2b. Design the tree on paper

Sketch tier-by-tier before writing code. For Dale, the final tree (after 11 commits of iteration) was:

```
T2 Lake-Town Peasant (basic_troop)
└── T3 Lake-Town Militia
    ├── T4 Lake-Town Watchman → T5 Veteran Watchman → T6 Officer of the Watch  [pikes line, mariner armor]
    └── T4 Lake-Town Patrolman → T5 Pikeman → T6 Veteran Pikeman → T7 Hearthguard  [2H halberd line, mariner armor]

T3 Dalian Levy (elite_basic_troop) — 5-way split:
├── T4 Riverman → T5 Shipman → T6 Mariner  [spear+shield, infrantry armor]
├── T4 Dalian Militia → T5 Guardsman → T6 Swordsman → T7 Royal Swordsman  [great infantry]
├── T4 Yeoman → T5 Bowman → T6 Marksman → T7 Barding  [archers, bronze]
├── T4 Crossbowman → T5 Veteran → T6 Master → T7 Royal Crossbowman  [crossbow, silver]
└── T4 Merchant Guard — splits:
    ├── T5 Northman Scout → T6 Veteran Northman Scout  [light cav, silver]
    └── T5 Dalian Cavalry → T6 Heavy Cavalry → T7 King's Guard  [heavy cav, bronze]

T2 militia_spearman + militia_archer (xslt-referenced) → T4 veteran variants
```

Key design constraints to apply (these emerged during Dale iteration — don't repeat the discovery):

- **"Royal" goes last** at the top tier. "Master" is the T6 stepping-stone. Avoid having Royal at T6 and Master at T7 — readers expect the higher rank to use the more prestigious title.
- **Save-compat: IDs are immutable**. You can rename a display name (`dale_royal_cavalier` "Royal Cavalier" → "Dalian Cavalry"), shift its tier, swap its armor, or change its upgrade target — but **never** rename or delete its `id` attribute. Existing campaigns reference IDs. Display names can drift freely from ID semantics (Dale ended with `dale_master_crossbowman` displaying "Royal Crossbowman" — documented as intentional desync).
- **Light/heavy splits** off a shared root troop: the root has mixed armor (one bronze + one silver roster) representing the choice point. Children are pure bronze (light) or pure silver (heavy).
- **Branch length**: signature branches go to T7 (or T8 if culture is uncapped). "Decent" branches cap one tier shorter. Light cavalry in Dale is intentionally short (T5–T6) because lore.

### 2c. Write `tools/generate_<culture>_troops.py`

Clone [`tools/generate_dale_troops.py`](../../tools/generate_dale_troops.py). The structure is:

- **`Troop` dataclass** with `id`, `display_name`, `tier`, `default_group`, `skills`, `rosters`, `upgrades`, etc.
- **`Skills` dataclass** — 8 skills tracked.
- **`EquipmentRoster`** — slot→item dict.
- **Skill-curve functions** (`s_yeoman_t4`, `s_bowman_t5`, etc.) — one per troop with reusable values per tier. Name them after the troop's role + tier, not the ID, so they survive ID/display drift.
- **Per-mesh-class explicit-suffix armor helpers**:
  - `chivalry_armor_explicit(suffix)` — cavalry chivlary mesh.
  - `infantry_armor_explicit(suffix)` — royal infantry infrantry mesh.
  - `archer_armor_explicit(suffix)` — archer mesh with shoulder-fallback.
  - `lake_town_armor_explicit(suffix, no_helmet=, no_shoulder=, no_bracers=)` — Lake-Town mariner mesh with optional slot skipping (used by Peasant which has only chest + boots).
  - Each takes a literal `a01`..`b04` suffix string and returns the 5-slot armor dict.
- **`build_troops()`** — `troops.append(Troop(...))` per troop. Group by branch with section comments.

**Equipment cross-reference**, every weapon/horse/shield ID must exist in vanilla or LOTRAOM: `python tools/validate_moduledata.py` reports the unresolved ones (read its warnings).

Codex caught a P2 on Dale where `lowland_yew_bow` is actually **stronger** than `lowland_longbow` in vanilla stats, but the generator placed yew at T5 and longbow at T6 — upgrading a Yeoman to a Bowman gave them a worse bow. **Always cross-reference vanilla weapon stats** (`difficulty`, `damage`, `accuracy`) when picking tier-ordered weapons.

### 2d. Generate + register

```bash
python tools/generate_<culture>_troops.py --dry-run    # verify tree shape + upgrade chains
python tools/generate_<culture>_troops.py --apply
```

Register in `Main/_Module/SubModule.xml` next to similar cultures:

```xml
<XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_<culture>"/>
    <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
        <GameType value = "CustomGame"/>
        <GameType value = "EditorGame"/>
    </IncludedGameTypes>
</XmlNode>
```

---

## Phase 3: Tree wiring

### 3a. Culture XSLT binding (MANDATORY)

For XSLT-passthrough cultures, the existing culture block in `spcultures.xslt` will preserve every vanilla attribute via `<xsl:apply-templates select="@*"/>` — including ones you don't want. Enumerate **every** CultureObject template/troop attribute the engine reads and explicitly classify each as BIND or PASSTHROUGH.

The full list of bindable culture attributes per `TaleWorlds.CampaignSystem.CultureObject.Deserialize` (v1.4.8 `CultureObject.cs:269-280`, re-verified 2026-08-12; the v1.5.2 body differs only by the added `executioner` read, body-diffed 2026-09-14):

| Attribute | Always bind to TAOM value | Optional / passthrough |
|---|---|---|
| `basic_troop` | yes | |
| `elite_basic_troop` | yes | |
| `executioner` | **yes, Bannerlord v1.5.x** | The character the execution cutscene dresses as the headsman; `HeroExecutionSceneNotificationData` dereferences it unguarded, so a culture without one crashes on its player's first execution. TAOM names the elite troop. Gate: `CultureExecutionerTests` |
| `melee_militia_troop`, `ranged_militia_troop` | yes | |
| `melee_elite_militia_troop`, `ranged_elite_militia_troop` | yes | |
| `default_party_template` | yes | |
| `villager_party_template` | **yes, 2026-08-12** | |
| `militia_party_template` | **yes — Codex #227 P1** | |
| `rebels_party_template` | **yes — Codex #227 P1** | |
| `vassal_reward_party_template` | **yes — Codex #227 P1** | |
| `settlement_patrol_template_level_1/2/3` | **yes — Codex #227 P1** | |
| `caravan_party_templates`, `elite_caravan_party_templates` (child elements) | **yes, 2026-08-12. See the two-edit rule below** | |
| `default_battle_equipment_roster` | yes | |
| `default_civilian_equipment_roster` | yes | |
| `bandit_boss_party_template` | | bandit cultures only |
| `fishing_party_template`, `settlement_patrol_template_coastal` | | NavalDLC only, unreachable (no TAOM settlement has a port) |
| `caravan_party_template`, `elite_caravan_party_template` (ATTRIBUTES) | | never read by the deserializer; do not bind |

Skipping the **bind-required** rows means the new TAOM templates you wrote for that culture are dead code — vanilla's `militia_sturgia_template` etc. flow through the passthrough.

**Three rows moved out of "inherit vanilla" on 2026-08-12, because that advice is what shipped the
fourth instance of this bug.** `villager_party_template` and both caravan child lists were listed
here as safe to inherit; the result was nine town-owning cultures fielding Calradian villagers and
caravans. Treat "inherit vanilla" as a claim needing evidence, not a default.

**Child elements need TWO edits, not one.** `Deserialize` (`CultureObject.cs:485-497`) does
`mBList10.Add(...)` inside a loop over every child named `caravan_party_templates`, so emitting yours
does not displace vanilla's. You must also add the wrapper to that block's `not(self::...)`
passthrough filter, or the culture carries both and rolls Calradian roughly half the time. The six
filters in `spcultures.xslt` exclude different name sets (8, 7 or 2), so edit each in place.

**Two of these bindings are crash surfaces, not just wrong troops.** Vanilla
`PatrolPartiesCampaignBehavior.SpawnPatrolParty` dereferences `partyTemplate.ShipHulls` and
`CaravansCampaignBehavior.SpawnCaravan` calls `GetRandomElementWithPredicate` on the caravan list,
neither guarded. So never remove a binding without adding its replacement in the same edit.

`TAOM.Tests/Core/CulturePartyTemplateTests.cs` gates all of this and will fail your PR if a binding is
absent, unbound or points outside `taom_partyTemplates.xml`. Full contract:
[culture-playability-wiring.md](../features/culture-playability-wiring.md).

### 3b. Party templates

Add 10 standard templates to `taom_partyTemplates.xml` (mirror an existing well-developed culture like Rohan, which has all ten):

| Template | Composition |
|---|---|
| `kingdom_hero_party_<culture>_template` | Full T2–T7 lineup. 10–20 lowest tier, scaling down to 1–3 elites. |
| `militia_<culture>_template` | 1+1 of the spearman + archer militia troops |
| `patrol_party_<culture>_template_level_1/2/3` | T2 / T4 / T6-grade patrols |
| `rebels_<culture>_template` | 24–32 recruits + 2–3 militia |
| `vassal_reward_troops_<culture>` | The 3–4 T7 elite terminals |
| `villager_<culture>_template` | 15 to 30 of the culture's `villager_<culture>` NPC |
| `caravan_template_<culture>` | 1 armed trader + 5-10 caravan guards + 1-5 veterans |
| `elite_caravan_template_<culture>` | 1 armed trader + 10-20 guards + 5-10 veterans |

The last three were added to this list on 2026-08-12. Dale had shipped without any of them and
silently used vanilla Sturgian villagers and caravans; authoring them was the only new content the
whole party-template fix required, because every other culture's twelve already existed.

When adding new troops later (e.g., Dale's Crossbowman line was a follow-up), update **all relevant templates** — `kingdom_hero_party_<culture>_template` always, patrol templates if the new troop fits a patrol tier, vassal_reward for new T7 terminals.

### 3c. Volunteer recruitment pool

Add `InitializeXxxCulture()`, optionally `InitializeXxxSettlements()`, optionally `InitializeXxxClans()` to `Main/Features/TroopProgression/VolunteerRecruitmentService.cs`. Call from the static constructor.

**Lookup priority** (highest first):

1. **`ConditionalSettlementMap`** — state-sensitive pools (Ithil Guard at `town_ES2` only when Gondor-owned).
2. **`SettlementMap[settlementId]`** — per-settlement override (e.g., Lake-Town `town_S1` = 9× Peasant + 1× Levy).
3. **`ClanMap[ownerClanId]`** — per-clan override (e.g., all 11 Rohan clans recruit all 7 basic troops).
4. **`CultureMap[cultureId]`** — culture-level fallback.

**Pool design heuristic**: weight the entry-tier troops (basic_troop + branch entries) high, T3+ low. For Dale: Dalian Levy 4, plus 1 each on the 6 entry troops for the 5 branches + Lake-Town Peasant. Total weight 10.

**Add tests** in `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`. Cover at minimum:

- Low roll → first pool entry (most common recruit).
- High roll → last pool entry (catches off-by-one in cumulative weight math).
- Settlement override → correct settlement pool wins over culture pool.
- Fallthrough → non-overridden settlements inherit culture pool.

---

## Phase 4: Validation gate

Run these in order. Any FAIL blocks commit.

> ⚠ These prove refs resolve **on disk**, not that the engine loaded new files. A new-culture ship writes brand-new armor XML files AND a new `<XmlName id="Items">` folder registration, both take effect only after a full game **RESTART** (Bannerlord registers the item directory at launch, globs it at campaign start, no hot-reload). A green `validate_moduledata.py` with naked troops in-game = new-file-not-loaded-until-restart, not a data defect. Restart + visually confirm clothed before commit. See `.claude/rules/moduledata-validation.md`.

```bash
# 1. Every armor, weapon, horse and shield ref resolves. Read the warnings.
python tools/validate_moduledata.py

# 2. C# compile + tests.
dotnet build Main --no-restore -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-build --filter "<Culture>Culture|<Culture>Settlement|<Culture>Clan"
```

---

## Phase 5: Review + RCA

```bash
/verify quick           # build + git status
/deep-review <culture>  # XML is code: Completeness, Data Flow, Design, XML Integrity (+ Standards, API Compat, Efficiency if C# changed)
                        # Fix all HIGH findings in-session (per .claude/skills/deep-review)
```

`/review-codex <culture>` is a paid call and runs only when the maintainer asks for it. Verify each finding by reading source.

**Phase 3e RCA is mandatory** for every confirmed Codex finding (any severity, including LOW). Write to `docs/reviews/rca-<culture>-<date>.md`. Pattern: finding, why missed, preventive action, repeat-offender check.

Dale shipped with 3 Codex findings on the first pass:

| # | Sev | Finding | Why missed |
|---|---|---|---|
| 1 | P1 | 6 culture template-binding XSLT attributes missing | Pattern-copied from Rohan's XSLT which also has this latent gap |
| 2 | P2 | `lowland_yew_bow` > `lowland_longbow` (stat inversion at T5/T6) | Assumed weapon names imply tier order |
| 3 | P3 | Cavalry skill curve 40% under Rohan, not "10% under" as commented | Wrote comment from intent, not measured value |

Expect Codex to catch similar patterns on your next culture.

### Build environment gotcha

`dotnet build Main` without `-p:ModuleId=` may fail with `System.UnauthorizedAccessException: '0Harmony.dll' is denied`: this is the TAOM.Dependencies post-build copy step failing because Bannerlord (or Steam, or a recently-closed game) still has the DLL handle open. The C# compile already succeeded; only the deploy step blocked. Close Bannerlord/Steam and retry. **Do not** authorize sandbox-bypass workarounds; this is an environment issue per `.claude/rules/environment-failures.md`.

---

## Phase 6: Iteration loops (expect 5–10 follow-up commits — this is NORMAL)

Dale shipped in **11 commits over one session** after the first pass. Future cultures will follow the same iteration shape. Plan capacity for these:

| Iteration kind | Typical trigger | Files touched |
|---|---|---|
| **Display-name renames** | User reviews the tree in-game, wants different titles | `generate_<culture>_troops.py` Troop blocks; regenerate XML; docs |
| **Equipment swaps within a line** | "Pikemen should actually have pikes, not halberds" | Same files |
| **Color/mesh swaps** | "Light cav should be silver, not bronze" | Same files |
| **Line restructure** (split/merge/retier) | "Cavalry should branch into light + heavy" | Generator + party templates + tests |
| **New parallel line** | "Add crossbowmen" | Generator (4 new troops + skill curves) + party templates + Dalian-Levy upgrade-edge |
| **Per-settlement override** | "Lake-Town should recruit mostly Peasants" | `VolunteerRecruitmentService` + tests |
| **Per-clan override** | "Every Rohan clan should recruit all basic troops" | Same |
| **Cross-culture bulk fix** | "All helmets should hide hair" | Python script over `LOTRLOME_items/*/head_armors.xml`; update generator default to match |

**Each iteration is a separate commit.** Don't batch unrelated changes — a "swap Royal/Master" commit and a "drop Peasant bracers" commit should not be the same commit even if shipped 5 minutes apart. Save-compat rules apply to every iteration (IDs immutable; display names + equipment + tier shifts allowed).

After every iteration, re-run:

```bash
python tools/generate_<culture>_troops.py --apply
python tools/validate_moduledata.py
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-build --filter "<Culture>Culture"
```

Each iteration that touches the `VolunteerRecruitmentService` pool **must update the corresponding tests** — the roll-index → troop mapping is sensitive to entry order + cumulative weights. Failing to update tests after a pool change is the most common iteration regression.

---

## Patterns codified during Dale iteration

The patterns visible in the Dale generator code (clone these explicitly for your next culture):

- Per-mesh-class explicit-suffix armor helpers (`chivalry_armor_explicit`, `infantry_armor_explicit`, `archer_armor_explicit`, `lake_town_armor_explicit`).
- Per-line color discipline (`a` = bronze, `b` = silver — same shape, different paint).
- "Royal goes last" naming convention at the highest tier.
- 1-variant-per-tier roster pattern when mesh count == tier count (clean per-tier identity, weapons differentiate).
- "Variation per level" (one `a` + one `b` per tier roster pair) when you want both colors visible at every rank.
- Light/heavy split via Y-fork at a shared parent (Merchant Guard) with mixed `a01`+`b01` armor representing the choice point.
- Single-variant `_slim` female-fit auto-derivation via `has_gender_variations="true"` on the base mesh (no separate item entry).

---

## File reference — what gets created/modified for a new culture

| Layer | Files | New / Modified |
|---|---|---|
| **Armor authoring** | `tools/generate_<culture>_armor.py`, `tools/<culture>_armor_meshes.txt` | new |
| **Armor data** | `<armory>/ModuleData/LOTRLOME_items/<culture>/{head,body,leg,arm,shoulder}_armors.xml` | new (5 files) |
| **Armor registration** | `<armory>/SubModule.xml` | modified (1 `<XmlNode>`) |
| **Troop authoring** | `tools/generate_<culture>_troops.py` | new |
| **Troop data** | `Main/_Module/ModuleData/troops/troops_<culture>.xml` | new |
| **Troop registration** | `Main/_Module/SubModule.xml` | modified (1 `<XmlNode>`) |
| **Voice map** | `Main/_Module/ModuleData/voice_accents/voice_accents.json` and `VoiceAccentMap.Default()` | modified (+ a row; gated by `ShippedConfig_EveryDefinedCultureHasARow`) |
| **Bearer banner** | `Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json` and `BannerBearerConfig.cs` | modified (+ a `CultureBanners` row; gated by `ShippedConfig_MapsEveryInfantryTroopCultureToABanner`) |
| **Crown guard** | `Main/_Module/ModuleData/crown_guard/crown_guard_cultures.json` and `CrownGuardMap.Default()` | modified when the culture rules a kingdom (+ a row; gated by `ShippedConfig_KeysEqualTheKingdomCultures`) |
| **Hall of Fire** | `Main/_Module/ModuleData/hall_of_fire/hall_of_fire.json` and `HallOfFireMap.Default()` | modified when the culture owns a settlement (+ a row; gated by `ShippedConfig_EveryConversionTargetCultureHasARowOrIsVanilla`, `ShippedConfig_EqualsTheBuiltInDefault` and the live `EveryCultureOwningANonHideoutSettlement_HasARowOrIsVanilla`) |
| **Culture wiring** | `Main/_Module/ModuleData/spcultures.xslt` | modified (Culture[@id='X'] block — ~9 military attrs minimum) |
| **Party templates** | `Main/_Module/ModuleData/taom_partyTemplates.xml` | modified (+10 templates) |
| **Recruitment** | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | modified (+ `InitializeXxxCulture` minimum) |
| **Tests** | `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | modified (+ low/high/fallthrough tests) |
| **Feature doc** | `docs/features/<culture>.md` | new |
| **RCA (if any Codex findings)** | `docs/reviews/rca-<culture>-<date>.md` | new |

If you author a sub-culture line (e.g., crossbowmen as a parallel to bowmen, or Riverman line as a new royal-tier branch), add to the relevant generator and re-run; the existing wiring (XSLT, party templates) usually doesn't need changes unless you add new entry-tier troops the XSLT references (`basic_troop` / militia / etc.).

---

## See also

- [`.claude/rules/troops.md`](../../.claude/rules/troops.md) — mandatory 7-step checklist + tier-2 patterns
- [`.claude/rules/xml-data.md`](../../.claude/rules/xml-data.md) — ID conventions, `equipmentType="Civilian"` rule
- [`docs/cultures.md`](../cultures.md) — net-new culture in `taom_spcultures.xml` (Phase 0 prerequisite if applicable)
- [`docs/features/dale.md`](../features/dale.md) — concrete worked example from this session
- [`docs/ai-includes/taleworlds-research-guide.md`](taleworlds-research-guide.md) — decompile workflow for vanilla weapon/culture/troop API verification

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/lord-skills-authoring.md](./lord-skills-authoring.md)
- [docs/features/dale.md](../features/dale.md)
- [docs/features/lord-skills.md](../features/lord-skills.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/cultures.md](../modding/cultures.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/recipe-add-a-culture.md](../modding/recipe-add-a-culture.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reviews/rca-gondor-lord-review-2026-05-26.md](../reviews/rca-gondor-lord-review-2026-05-26.md)

<!-- backlinks-end -->
