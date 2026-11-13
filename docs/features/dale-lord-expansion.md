# Dale Lord Expansion

## Overview

Dale (`Kingdom.sturgia`) grows from 9 clans and 48 adult lords to 14 clans and 75 adult lords, which gives it 30 party slots at campaign start instead of about 18. The change is data only: five new clans, 27 new lords, two tier raises and a new castle distribution. It needs a new campaign, because Clans, Heroes and settlement owners load at campaign start.

## Why This Exists

- **Vanilla behavior:** a clan fields as many lord parties as `DefaultClanTierModel.GetPartyLimitForTier` allows: tier 0 to 2 one party, tier 3 and 4 two, tier 5 and 6 three, plus one if the clan leader has the Talent Magnet perk (Leadership 250; no Dale leader reaches it at start). `TaomClanTierModel` inherits the rule unchanged. A slot fills only with an adult lord who can fight (some combat skill of 100 or more) and has no party.
- **TAOM requirement:** Dale had 9 clans, 48 adult lords and about 18 lord parties, against 130 adult lords for Dol Guldur and 120 for Rhun. The major-kingdom median is 75. Dale needed to field armies at a comparable scale.
- **Without this feature:** Dale stays the thinnest major kingdom on the map and loses wars of attrition to kingdoms with several times its parties.

## Architecture

### Design Challenge

Lord count is not party count. Because party slots follow clan tier, the fix has to add clans or raise tiers. Extra lords in an existing clan raise no limit; they only keep that clan's slots filled when lords are captured, because spare fighting lords refill a slot.

### Solution Approach

Pure data, written once by `tools/oneoff/author_dale_lords.py` (a finished one-off: it refuses to run without `--rerun-retired`, and the XML is now the source of truth). It adds five clans `clan_sturgia_10` to `clan_sturgia_14` and 27 lords `lord_S10_*` to `lord_S14_*` to `characters/clans.xml`, `characters/lords.xml` and `characters/heroes.xml`, and `spclans.xslt` raises `clan_sturgia_8` and `clan_sturgia_9` to tier 3. Eleven skill pins remain in `CULTURES['dale']['canonical']` in `tools/apply_culture_skills_traits.py`, the ids whose archetype the age and sex default would not derive.

- **Bowmen:** they carry the `a` or `b` equipment roster, the only Dale battle rosters with a bow.
- **Faces:** each new lord's face is a vanilla Vlandian or Imperial lord face that `lords.xslt` replaces, so no campaign character wears it. Whether they read as Bardings needs an in-game look.
- **Banner:** all five clans fly the Dale banner key that clans 2 to 9 use.
- **Custom Battle:** `custom_battle_commanders.json` curates three Dale commanders: `lord_S10_1` Halvard, `lord_S11_1` Ketil and `lord_S13_1` Starkad.
- **Marker blocks:** the Dale block sits in front of the Arthedain generator's block in `clans.xml`, `lords.xml` and `heroes.xml`. Since #947 `generate_new_faction_kingdoms.py` replaces its block where it stands, so the order no longer matters to a re-run.
- **Castles:** after the deploy, `python tools/Assign-SettlementOwners.py --apply` seats Dale by the tool's own distribution (`castle_S1` clan 11, `castle_S2` clan 5, `castle_S3` clan 12, `castle_S4` clan 10, `castle_S5` clan 6, `castle_S6` clan 8, `castle_S7` clan 9; towns unchanged). 12 of the 14 clans hold one fief. The tool is again the record of Dale's owners.

## Configuration

### Current Values

| Clan | Name | Tier | Initial home |
|------|------|------|--------------|
| `clan_sturgia_10` | House of the Running River | 4 | `castle_S4` |
| `clan_sturgia_11` | House of the Carnen | 4 | `castle_S1` |
| `clan_sturgia_12` | House of Ravenhill | 3 | `castle_S3` |
| `clan_sturgia_13` | House of the Forest River | 3 | `town_S4` |
| `clan_sturgia_14` | House of the Long Marshes | 3 | `town_S5` |

The home column is a starting value only: the engine re-picks every clan's home settlement weekly. The initial homes match what `tools/Assign-SettlementOwners.py` seats for Dale.

| Measure | Before | After |
|---------|--------|-------|
| Clans | 9 | 14 |
| Adult lords | 48 | 75 |
| Party slots | about 18 | 30 |

**How the numbers were counted:** on the merged vanilla plus TAOM data, lords with occupation `Lord`, a living `Hero` row and age 18 or more; party slots by the tier rule above, summed over the clans.

Average adult OneHanded is 146.6 (Gondor 141.4, Rohan 173.1). Dale skill sets equal the Gondor and Rohan sets. The women keep lady sets by design (stewardship, Steward 240 and 170); fighters use `dale_lord`, `dale_knight`, `dale_bowman` and `dale_young_lord`.

## Key Files

| File | Purpose |
|------|---------|
| `tools/oneoff/author_dale_lords.py` | Finished one-off that wrote the clans and lords |
| `tools/apply_culture_skills_traits.py` | Skill and trait pass; Dale canonical ids pinned |
| `tools/Assign-SettlementOwners.py` | Settlement owner spec; the record of Dale's owners |
| `Main/_Module/ModuleData/characters/lords.xml` | Lord characters |
| `Main/_Module/ModuleData/characters/heroes.xml` | Heroes for the new lords |
| `Main/_Module/ModuleData/characters/clans.xml` | The five new clans |
| `Main/_Module/ModuleData/clan_heraldry/dale.json` | Dale clan heraldry |
| `Main/_Module/ModuleData/taom_clan_name_strings.xml` | Clan name strings |
| `Main/_Module/ModuleData/taom_lord_name_strings.xml` | Lord name strings |
| `Main/_Module/ModuleData/Languages/*/std_taom_clan_name_strings_*.xml`, `std_taom_lord_name_strings_*.xml` | The 24 language files for those names |
| `docs/reviews/_clan_registry.json` | Clan registry |
| `Main/_Module/ModuleData/spclans.xslt` | Tier raise for `clan_sturgia_8` and `clan_sturgia_9` |
| `Main/_Module/ModuleData/custom_battle/custom_battle_commanders.json` | Curated Dale commanders for Custom Battle |
| `tools/tests/test_dale_lord_expansion.py` | Shipped-data gates: bow rosters, banner, names and homes, unique faces |
| Live `TAOM_Map/ModuleData/settlements.xml` | Settlement owners (unversioned) |

## Dependencies

- `DefaultClanTierModel` (engine), inherited unchanged by `TaomClanTierModel`: maps clan tier to party count.
- The live TAOM_Map module: the settlements the new clans own. **TAOM and TAOM_Map must ship together.** A TAOM_Map that names `clan_sturgia_10`, `clan_sturgia_11` or `clan_sturgia_12` as an owner, paired with an older TAOM build, points castles at undefined clans. The per-channel check is in [release-process.md](../reference/release-process.md).

## Tests

- `CharacterFaceCoverageTests`, `LordIdentityConsistencyTests`, `LordNameAndSexConsistencyTests`, `LordFamilyTransformTests`, `LordTemplateRosterTests` and `LordInlineSkillParityTests` cover the new lords' faces, identity, names, families, templates and inline skills.
- `ShippedHeraldryBannerKeyTests` checks the banner key's shape only. `tools/tests/test_banner_icon_ids.py` (#951) checks that every icon and colour id in the clan, kingdom and culture banner keys exists in Native's or TAOM's `banner_icons.xml`.
- `LocalizationKeyConsistencyTests` covers the name keys; `CustomBattleCommandersShippedDataTests` covers the Custom Battle entries.
- `tools/tests/test_dale_lord_expansion.py` pins the bow rosters, the shared banner, the clan names and homes, and the face keys.
- `tools/tests/test_generator_regions_in_sync.py` catches a hand edit inside the Arthedain generator regions.
- `python tools/validate_moduledata.py` resolves `Faction.X` refs since #949 (`BROKEN_FACTION_REF`: a settlement `owner=` or Hero `faction=`), so a Dale house that names a missing clan fails the commit hook. It still does not resolve `Hero` or `Settlement` references, and the `faction` attributes `heroes.xslt` writes are not swept.

## How to Add More Dale Lords

The XML is the source of truth and the generator is retired. A future change edits the XML and runs these steps:

1. Raise or add tiers in `spclans.xslt`, or add the clan and lords inside the Dale marker block in `clans.xml`, `lords.xml` and `heroes.xml`.
2. Pin any id whose archetype the age and sex default would not derive in `CULTURES['dale']['canonical']` in `tools/apply_culture_skills_traits.py`.
3. Edit the SPEC in `tools/Assign-SettlementOwners.py`, take a backup, dry-run until the diff is exactly the intended change, then run it with `--apply`.
4. Run `python tools/generate_name_localization_strings.py --apply --category clan` and `--category lord`, then seed the rows.
5. Update `docs/reviews/_clan_registry.json` with `tools/clan_registry.py`.
6. Post a comment on #763 for the paid translation.
7. If the goal is more armies, raise a clan tier or add a clan; more lords in a clan add no slot.

## Known Gaps

- Faces need an in-game look (see Architecture).
- The 27 new heroes have no biography text; the 55 vanilla-derived Dale heroes get one from `heroes.xslt`.
- The new clans reuse the party templates of clans 1, 4, 2, 3 and 6 and the shared Dale banner. A heraldry pass owes both.
- The new names are translated in all 12 languages (2026-11-12, #763).
- New campaign only.
- In-game check owed.

## Changelog

- 2026-10-20: five clans and 27 lords added, `clan_sturgia_8` and `_9` raised to tier 3, Dale's castle distribution moved into `Assign-SettlementOwners.py`.

## GitHub Issue

- **Issue:** #943
- **Status:** Open
- **Review:** [rca-dale-lord-expansion-2026-10-20.md](../reviews/rca-dale-lord-expansion-2026-10-20.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dale.md](./dale.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/release-process.md](../reference/release-process.md)
- [docs/reviews/lessons/data-content-cultures.md](../reviews/lessons/data-content-cultures.md)

<!-- backlinks-end -->
