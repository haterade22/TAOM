# RCA: minor-faction colours (deep-review of spclans.xslt, 2026-11-14)

## Top-line

`spclans.xslt` now sets a lore `color`/`color2` on 14 minor factions that kept vanilla colours. The tier 2
deep-review (Agent 7 XML, Agent 5 data flow, both at `effort: "high"`) found no defect and no CRITICAL or
HIGH finding. It confirmed five points, all LOW or MEDIUM, and two of them came from my own edit: a doc
line my change made false, and a banner that no longer matches its armour. Codex was not authorized for
this run (tier 2 asks once per session).

Proof the change is what it claims: the old and the new stylesheet were each run over the installed vanilla
`SandBox/ModuleData/spclans.xml`. Exactly the 14 expected ids differ, only in `color` and `color2`, every
value matches `FF[0-9A-F]{6}`, and no new pair repeats a pair in `characters/clans.xml` or in the other
transformed factions.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | The 14 factions keep their vanilla `banner_key`, so banners, shields and the encyclopedia show vanilla colours while cloth shows the new pair (Serpent Guard: cloth red and black, banner gold). `clan-heraldry.md` keeps Gondor's pair derived from its `banner_key`. | Coupling between two stores of one colour | The request named troop colour only, and I edited `color`/`color2` without opening the doc that couples them to `banner_key`. | Before a clan colour change, read `docs/features/clan-heraldry.md` and compare the clan's `banner_key` colours. Lesson entry below. |
| F2 | LOW | `clan-heraldry.md` Known Limitations said these factions were "left at vanilla colours". The change made that false. | Documentation drift | I did not grep `docs/features/` for the limitation my change removes. | Fixed in this change. The lesson entry covers it. |
| F3 | LOW | The 14 pairs live only in the XSLT. The 73 older coloured templates are each owned by a `clan_heraldry/<culture>.json` spec, so the generator and `test_clan_heraldry_specs.py` do not see these 14. | Pipeline blindness | I edited the output file, not the spec the doc names as the author of colours. | Optional colours-only spec, like `bandits.json`. See the follow-up below. |
| F4 | LOW | `tools/clan_registry.py` `parse_xslt` reads only `culture` and `default_party_template`, so `docs/reviews/_clan_registry.json` lists vanilla colours for all 14 (and `null` for older XSLT-coloured clans). It was true before this change. | Pre-existing tool gap | Nobody reads the registry at runtime, so no gate asked. | See the follow-up below. |
| F5 | LOW | `Clan.Color` and `Clan.Color2` are saved properties, and `MinorClanMigrationService` migrates culture and leader templates only. An existing save keeps the vanilla pair. | Save compatibility | Already documented as "new campaign only" for recolours. | Added to `clan-heraldry.md` Known Limitations and to the commit body. |

## Root-cause pattern

F1, F3 and F4 share one cause: a clan colour is stored in four places (the XSLT, the `banner_key`, the
heraldry spec and the registry), and the change touched the first one. The build, the validators and the
tests all passed, because none of them compares those four stores.

## Why each lens did not stop the change earlier

Both lenses found the points above, so no lens missed one. The builder (me) missed F1 and F2 before the
review, for the reasons in the table.

## Follow-ups

- FOLLOW-UP: no issue: a colours-only `clan_heraldry/minor.json` for the 14 ids would bring them under the spec gate. It is optional and the maintainer decides; the generator only upserts spec ids, so the hand edits are safe until then.
- FOLLOW-UP: no issue: `tools/clan_registry.py` `parse_xslt` should read `color` and `color2`. It is a registry that nothing reads at runtime.
- FOLLOW-UP: no issue: give the 14 factions lore `banner_key` values so banners match cloth, or accept the split on purpose. This is a design call for the maintainer.
