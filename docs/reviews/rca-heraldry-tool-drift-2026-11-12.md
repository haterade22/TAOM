# RCA: Heraldry tools behind the new Ered Luin and Lindon banners (#1131, deep review 2026-11-12)

## Top-line

Commits 72715c417 and 7b354bfe0 (ThyrellTTV) gave the Ered Luin and Lindon houses new names and layered banners
in the shipped XML. The tools that generate those files still held the old names and keys, so their `--check` runs
and tests failed on trunk. The fix makes the tools follow the shipped data: the generator takes hand-made banner
keys, the emblem tool finds the sigil under vanilla shapes, and the tests and comments use the new names. The tier 2
deep review (lens 5 data flow and the Tooling lens, at high effort; Codex out of usage, Mike 2026-11-12) confirmed 1
MEDIUM and 6 LOW findings. All are fixed in this change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The shipped Ered Luin faction-screen emblem (`fs_emblem_kingdom_of_ered_duin.png`) still showed sigil 24000; the realm flies 31007 since 72715c417 | Generated asset drift | `make_faction_emblems.py --check` reports it, but no test runs that check: `test_check_is_clean_after_apply` checks a temp folder only | Emblem regenerated; `test_the_shipped_emblems_match_the_shipped_banners` runs `--check` on the shipped files (red before, green after) |
| 2 | LOW | `clan_banner_keys` was keyed by list position, so a reordered `clans` list moved a hand-made banner to another house without a warning | Data keyed by position | The first version followed the existing `clan_icons` list, which is positional | Keyed by clan id; the realm test asserts every key names one of that realm's clans |
| 3 | LOW | `sigil_icon`'s `sprites=None` branch had no production caller | Simplicity | It was kept for an old test | `sprites` is required; the tests pass it |
| 4 | LOW | The comment said the Ered Luin `clan_icons` are kept for the clan count check; they also switch the clan banner rewrite on | Comment accuracy | The gate at the rewrite (`if k.get(key)`) is far from the data | The comment names both uses |
| 5 | LOW | `insert_new_faction_cc_menus.py` still wrote "House of Borin" in the Ered Luin parents text; the shipped text says "House of Delingur" | Source text drift | The tool skips cultures already present, so `--check` stays green while its source is stale | Source text follows the shipped text |
| 6 | LOW | `tools/README.md` said the emblem tool crops the layer-1 icon | Docs drift made by this change | The README describes the old rule | The README names the new rule |
| 7 | LOW | Old house names in `promote_borrowed_cultures.py`, `add_map_fortifications.py`, `ered-luin.md` and `new-factions-misty-mountains-lindon.md` | Docs drift since 72715c417 | A content commit renamed houses and changed no prose that names them | Names corrected; the Lindon banner paragraph names each house's icon and points at `clans.xml` for colours instead of copying them |

## Root-cause pattern

A content commit changed shipped data that several tools generate, and nothing ran those tools' checks before or
after it. Finding 1 is the one with a player effect: the generated PNG drifted and the only test of the tool ran it
on a copy, so the drift had no gate. The same shape as findings 5 and 7: a generator or a doc that describes data
stays green while the data moves under it.

## Why each agent missed these

- **Earlier work (72715c417, 7b354bfe0):** hand edits to the XML, with no generator `--check` run and no doc pass.
- **Lens 5 (data flow):** found findings 1, 4, 5 and 7 by tracing each changed key and name into the shipped files.
- **Tooling lens:** found findings 1, 2 and 3 and confirmed that the new tests fail on the old code.

## Changes

- `tools/generate_new_faction_kingdoms.py`: `kingdom_banner_key` for Ered Luin and `clan_banner_keys` (by clan id)
  for Arthedain and Ered Luin, plus the new Ered Luin house names; the clan comment.
- `tools/promote_borrowed_cultures.py`: the new Ered Luin clan name pool.
- `tools/make_faction_emblems.py`: the sigil is the first layer icon with a TAOM sprite; `sprites` required.
- `Main/_Module/GUI/FactionUI/RuntimeSprites/fs_emblem_kingdom_of_ered_duin.png`: regenerated (sigil 31007).
- `Main/_Module/ModuleData/clan_heraldry/rivendell.json`: Lindon's shipped colours.
- `tools/insert_new_faction_cc_menus.py`, `tools/promote_borrowed_cultures.py`, `tools/add_map_fortifications.py`,
  `tools/README.md`: text and comments.
- Tests: `test_generate_new_faction_kingdoms.py`, `test_make_faction_emblems.py`, `test_lindon_clan_expansion.py`.
- Docs: `docs/features/ered-luin.md`, `docs/features/new-factions-misty-mountains-lindon.md`.

## Verification

- Full verify on the branch at 7087fa633 plus this change: C# 39,250 passed, 0 failed; Python tools 8,582 tests OK
  (13 skipped); canary tripped; API snapshot reproduces.
- `make_faction_emblems.py --check`, `insert_new_faction_cc_menus.py --check` and every generator `--check` exit 0.
- No in-game check: the faction screen shows the regenerated emblem after a build; the banner keys are unchanged.
