# Clan Heraldry & Per-Clan Party Templates

## Overview

Every TAOM clan that the mod authors or renames gets a distinct `color`/`color2` (heraldry tint)
and — where a troop pool exists — its **own** `default_party_template` pointing at a region/archetype-
themed roster. The 8 vanilla-renamed kingdoms were also repainted to lore-accurate palettes.
As originally shipped (2026-06-04): 192 clans coloured, 176 party templates, 8 kingdoms repainted.
Those counts have drifted with later hand edits, so re-measure before quoting them.

## Why This Exists

TAOM's armor items use **grayscale (desaturated) textures** so the engine tints them per-faction.
The trigger was "grayscale armor reads gray/wrong in-game." Two chains matter and they give different answers: what the engine does, and what TAOM ships on top.

**Vanilla chain** (re-verified against installed v1.4.8):

| Step | Evidence |
|---|---|
| Agent armor cloth tint = its Team colour | `Mission.cs:4434` → `new AgentBuildData(troop).ClothingColor1(agentTeam.Color).ClothingColor2(agentTeam.Color2)`, applied at `Mission.cs:4128-4129` |
| Team colour = party's `MapFaction` colour | `PartyBase.cs:257` → `PrimaryColorPair` = `(MapFaction.Color, MapFaction.Color2)` |
| `MapFaction` of a kingdom-bound clan = the **Kingdom** | `Clan.cs:338` → returns `Kingdom` when set, else `this` |
| `default_party_template` optional, falls back to culture | `Clan.cs:112` → `Culture.DefaultPartyTemplate` |

**What TAOM ships, which overrides the middle two rows.** `Mission.SpawnTroop` builds the
`AgentBuildData` with the team (so kingdom) colours and then calls `Mission.SpawnAgent`. TAOM
prefixes `Mission.SpawnAgent`
(`Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs`, category
`Patch23_BannerColorPersistence`) and rewrites `ClothingColor1/2` from the spawning party's LEADER's
clan. The adapter it reads through (`Main/Adapters/BannerHeroAdapter.cs`) returns `clan.Color` and
`clan.Color2` with **no `MapFaction` hop**, so a clan inside a kingdom is not redirected to the
kingdom. The prefix runs after vanilla has already set the team colours, so the clan colour wins.

**Consequence:** for every noble clan, kingdom-bound or not, **battlefield armour follows the CLAN
colour.** A clan's `color`/`color2` is the direct lever on its troops' armour tint. Three conditions
gate it: `EnableColorPersistence` must be on (`configs/banner_color_config.json`), both colours must
be non-zero (`ClanColorInfo.HasCustomColors`), and the agent must carry a party origin with a
`LeaderHero`. When any of those fails the patch falls through and vanilla's kingdom colour applies.
That is the only path on which the older "armour follows the kingdom" claim still holds, and it is
why a leaderless garrison or militia still fields kingdom colours.

Minor and bandit factions reach the clan colour by both routes, which is why the 8 bandit clans were
recoloured off their flat gray `FF8B7C73`.

**Save-compat. Read this before concluding that a colour edit "did not work".** `Clan.Color` and
`Clan.Color2` are `[SaveableProperty(76)]` and `(77)`, so an existing save keeps the colours it was
created with. A recolour shows up on a **new campaign only**. Vanilla
`Clan.UpdateBannerColorsAccordingToKingdom` recolours the `Banner` object, never `Color`/`Color2`.

**Avoid a pure-white primary.** `FFFFFFFF` is `uint.MaxValue`, which is the engine's own "unset"
value for cloth colour (`AgentVisualsData` initialises both `ClothColor1Data` and `ClothColor2Data`
to it). `AgentVisuals_Create_Patch` therefore reads a white primary as "no clan colour set", returns
early, and never suppresses the engine's HSB colour randomness. The blast radius is small, because
the only shipped caller that enables randomness is the campaign-map party icon and that path reads
`MapFaction.Color`, so it bites only when a player founds a kingdom from such a clan
(`KingdomManager` seeds the new kingdom from `founderClan.Color`). Use `FFFEFEFE` instead, one step
off white and visually identical. `color2` is unaffected: the guard reads `ClothColor1Data` only.

## Architecture

Pipeline (tooling-first, idempotent, dry-run/apply):

```
tools/repaint_kingdom_colors.py   ── Phase 0: 8 XSLT kingdoms' color/color2 → lore palette
tools/clan_registry.py            ── exact 209-clan inventory (id, culture, source, current color)
                                      → docs/reviews/_clan_registry.json
tools/build_clan_specs.py         ── auto-compose per-culture specs:
                                       · color = lore base + deterministic per-clan HSL variation
                                       · roster = archetype (balanced/inf/ranged/cav/elite/skirmisher)
                                         composed from that culture's troops_*.xml (comments stripped)
                                      → Main/_Module/ModuleData/clan_heraldry/<culture>.json
tools/generate_clan_heraldry.py   ── consume specs, idempotently edit 3 files:
                                       A. characters/clans.xml      (source=xml  → add attrs)
                                       B. spclans.xslt              (source=xslt → per-clan override, passthrough kept)
                                       C. taom_partyTemplates.xml   (upsert <MBPartyTemplate> rosters)
```

**Colour sourcing.** Gondor (14 clans) is **hand-authored**, and since 2026-09-02 each clan's
`color`/`color2` is derived from its own `banner_key`: `color` is the layer-0 background palette
colour, `color2` is the layer-1 icon colour, both resolved through `<BannerColors>` in
`banner_icons.xml`. Keep the pair in step whenever the banner changes.

Resolve a palette id against **TAOM's `banner_icons.xml` first**, falling back to Native's only when
the id is absent there. TAOM redefines 46 ids that Native already owns, and TAOM's value is the one
that renders: the engine merges the two documents before `BannerManager` ever sees them
(`MBObjectManager.MergeElements`, with `BannerColors` marked `AlwaysPreferMerge` and `@id` unique in
`BannerIcons.xsd`), and that merge is last-writer-wins with TAOM loading after Native. The
`if (!_colorPalette.ContainsKey(key))` guard in `BannerManager` looks like first-writer-wins but
never fires, because the merged document already holds exactly one `Color` node per id. Reading
Native's table for a colliding id is how `clan_empire_west_2` briefly got `FF2A5599` rather than
`FF30336B`. The other 14 troop-having
cultures are **auto-composed**: a per-culture lore base colour with deterministic per-clan hue/lightness
variation (each clan distinct, in-family), and archetype-rotated rosters. This is the only feasible way to
reach ~190 clans; refine any clan by editing its spec JSON and re-running the generator.

**Troopless cultures.** shaghana + abanissa field **Harad (aserai)** rosters (their culture config points
there); Lothlorien fields **Rivendell**; **Khand (battania)** has no TAOM troop pool → **colours only**
(no per-clan template, keeps its existing fallback) — authoring a Khand troop tree is a future task.

## Configuration

| File | Purpose |
|---|---|
| `Main/_Module/ModuleData/clan_heraldry/gondor.json` | hand-authored Gondor fiefdom spec |
| `Main/_Module/ModuleData/clan_heraldry/<culture>.json` | auto-generated specs (15 cultures + 4 troopless) |
| `Main/_Module/ModuleData/clan_heraldry/bandits.json` | colours-only recolour of 8 bandit clans |

Spec entry: `{ id, source: xml|xslt, template_id?, theme, color, color2, roster: [{troop,min,max}] }`.
An empty/absent `roster` ⇒ colours-only (no template, no `default_party_template` override).

## Key Files (edited by the pipeline)

| File | Change |
|---|---|
| `spkingdoms.xslt` | 8 kingdoms' `color`/`color2` repainted |
| `characters/clans.xml` | 119 Factions gained `color`/`color2` (+ `default_party_template` where templated) |
| `spclans.xslt` | 73 per-clan `color`/`color2`/`default_party_template` overrides (passthrough preserved) |
| `taom_partyTemplates.xml` | 176 new `kingdom_hero_party_<faction>_<clan>_template` blocks |

## Tests / Verification

- `python tools/validate_moduledata.py` → **PASS**, 0 errors (no broken troop/template refs).
- `python tools/check_external_xslt.py` → **PASS** on all 17 stylesheets across the three modules.
- Transform `spclans.xslt` over the installed `SandBox/ModuleData/spclans.xml` with lxml and assert
  the emitted `color`/`color2` plus the passthrough attributes. Reading the stylesheet text cannot
  prove passthrough survived, which is the failure mode `/xslt-check` alone would miss.
- All touched XML/XSLT parse as well-formed (`xml.dom.minidom`).
- `dotnet build Main/TAOM.csproj` → 0 errors (data-only; C# unaffected).
- **Human seam (not automatable):** in-game render of the new clan colours, kingdom troop-armor tint,
  and that fiefs/clans field their distinct rosters. Start a campaign and inspect encyclopedia heraldry +
  a few battles.

## How-To

> **Do not run the generator on Gondor.** `clan_heraldry/gondor.json` has drifted from the shipped
> `spclans.xslt` on `template_id` for clans 2, 5, 6, 7, 8 and 9. The shipped XSLT holds the correct
> mapping, from the deliberate `fix(gondor-clans): reconcile clan to default_party_template bindings`
> pass recorded in `docs/changelog-archive/CHANGELOG-2026-H1.md`. `--all --apply` globs every spec
> file, so it would silently revert that fix. Reconcile the spec's `template_id` half first, or edit
> `spclans.xslt` and `characters/clans.xml` by hand. The same trap applies to `mordor.json`.

**Re-colour or re-roster a clan:** edit its entry in `clan_heraldry/<culture>.json`, then
`python tools/generate_clan_heraldry.py --spec <culture> --apply` (idempotent — replaces, never dupes).

**Regenerate all auto-specs** (e.g. after adding troops): `python tools/build_clan_specs.py` then
`python tools/generate_clan_heraldry.py --all --apply`.

**Add a brand-new faction:** add it to `CULTURE_TABLE` (or `TROOPLESS_TABLE`) in `build_clan_specs.py`.

## Known Limitations

- `clan_empire_south_10-15` (Mordor-named, in `Kingdom.empire_s`) were re-cultured from the stale
  `Culture.empire` to `Culture.mordor`, so they now field Mordor troops + heraldry.
- Vanilla minor mercenary/outlaw factions (ghilman, skolderbrotva, wolfskins, …) left at vanilla colours.
- Khand per-clan rosters await a dedicated Khand troop tree.
- Non-Gondor clan colours are systematic (lore base + variation), not individually hand-named heraldry.

## Changelog

- 2026-09-02: repointed all 14 Gondor clans' `color`/`color2` to derive from their own `banner_key`, and corrected the "armour follows the kingdom" claim above: `Patch23_BannerColorPersistence` makes it follow the clan.
- 2026-06-04 — Gave 192 clans distinct heraldry `color`/`color2`, 176 clans their own `default_party_template`, and repainted all 8 vanilla-renamed kingdoms to lore palettes via 4 new tools + per-culture spec files; `validate_moduledata` PASS (462 party templates), data-only.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
