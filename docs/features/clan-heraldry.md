# Clan Heraldry & Per-Clan Party Templates

## Overview

Every TAOM clan that the mod authors or renames gets a distinct `color`/`color2` (heraldry tint)
and — where a troop pool exists — its **own** `default_party_template` pointing at a region/archetype-
themed roster. The 8 vanilla-renamed kingdoms were also repainted to lore-accurate palettes. Net:
**192 clans coloured**, **176 per-clan party templates**, **8 kingdoms repainted**.

## Why This Exists

TAOM's armor items use **grayscale (desaturated) textures** so the engine tints them per-faction.
The trigger was "grayscale armor reads gray/wrong in-game." Research against the installed 1.4.5 engine
established **what actually drives the tint**:

| Step | Evidence |
|---|---|
| Agent armor cloth tint = its Team colour | `Mission.cs:4422` → `new AgentBuildData(troop).ClothingColor1(agentTeam.Color).ClothingColor2(agentTeam.Color2)` |
| Team colour = party's `MapFaction` colour | `PartyBase.cs:257` → `PrimaryColorPair` = `(MapFaction.Color, MapFaction.Color2)` |
| `MapFaction` of a kingdom-bound clan = the **Kingdom** | `Clan.cs:338` → returns `Kingdom` when set, else `this` |
| `default_party_template` optional, falls back to culture | `Clan.cs:112` → `Culture.DefaultPartyTemplate` |

**Consequence:** for the ~190 kingdom-bound noble clans, **battlefield armor follows the KINGDOM colour,
not the clan colour.** So the real "gray armor" lever is the kingdom repaint (Phase 0). Clan `color`/`color2`
matters for: encyclopedia/UI heraldry, independent clans, and **minor/bandit factions** (whose `MapFaction`
*is* the clan, so their troop armor does follow clan colour — which is why the 8 bandit clans were recoloured
off their flat gray `FF8B7C73`).

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

**Colour sourcing.** Gondor (14 clans) is **hand-authored** by fiefdom with semi-canonical heraldry
(Dol Amroth azure/silver Swan-Knights, Lossarnach red axemen, Lebennin blue/gold, Lamedon black/gold,
Pinnath Gelin green, Ithilien rangers, Blackroot Vale shadow-archers, …). The other 14 troop-having
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

- `python tools/validate_moduledata.py` → **PASS** (0 broken troop/template refs; 462 party templates).
- All touched XML/XSLT parse as well-formed (`xml.dom.minidom`).
- `dotnet build Main/TAOM.csproj` → 0 errors (data-only; C# unaffected).
- **Human seam (not automatable):** in-game render of the new clan colours, kingdom troop-armor tint,
  and that fiefs/clans field their distinct rosters. Start a campaign and inspect encyclopedia heraldry +
  a few battles.

## How-To

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
