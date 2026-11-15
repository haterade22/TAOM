# RCA: five failing trunk tests and their fix (#1284)

Trunk at c142ba2c9 failed five tests: two C# language-row tests, two banner icon tests and the Ered Luin
generator check. The fix seeds 91 English rows into each of the 12 languages, repoints four banner icons
that the v2.0.40 repack (9a409deac, PR #1265) deleted while two clans still flew them, lowers the icon
registry floor from 500 to 400, and syncs the House of Uri key into `generate_new_faction_kingdoms.py`.
The review ran at tier 2 (a gate test is in scope): lens 5 (data flow), lens 7 (XML) and the tooling lens
at `effort: "high"`, plus a Codex pre-review at `xhigh` on Mike's word. No lens or Codex found a defect
that blocks the change; two LOW items were fixed, one MEDIUM was not applied (reason below), and the
review traced the repack's lost icons to a parse fault that has now appeared three times.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Nos Galengrim's replacements change its sigil's look: 15009 is Rivendell's realm icon, 10017 is also flown by `clan_shaghana_8` and `_9`, and 10017's solid disc hides the centre layer `503.44.116.75.75` (lens 7; lens 5 as LOW) | Visual substitution | I picked by look from sprite previews and counted other users only after picking | NOT APPLIED. Rivendell flies 15009 as a large ring (435) behind its compass 15008 (583); Nos Galengrim flies it as a small ring (150) inside a sun, so the two do not read alike. Unused hollow Elven sets (15006, 16007, 15004 or 16005) were rendered and look further from the lost sun. The look is owed in game (`banner-injection.md`, "How to verify in game") |
| 2 | LOW | `docs/features/anduin-vale-angaladh.md:131` still named 17006 to 17008 (lens 5, tooling) | Stale doc twin | After the repoint I grepped `tools/` for the old ids, not `docs/` | Fixed. Existing lesson `build-tooling-workflow.md`, "When a change alters what an artifact CONTAINS, grep the docs for that artifact's inventory" |
| 3 | LOW | `tools/tests/test_banner_icon_ids.py:124` lost the space after `400,` (tooling, lens 7, lens 5) | Edit slip | My Edit strings ended in a space, and the result dropped it | Fixed. One-off: anchor an edit on the next token, never on trailing whitespace |
| 4 | LOW | The icon floor of 400 lets a partial loss pass (up to 72 icons; Codex: Native's 48-icon Handmade group can vanish with every test green), and the colour floor of 100 cannot see a lost palette (tooling, Codex) | Coarse gate | The floor is a broken-parse guard, not a completeness check, before and after this change | FOLLOW-UP no issue: the existence tests beside it fail on any lost icon or colour a shipped key names, which is the data the floor protects; a per-file parse check (tooling lens finding 2) is offered to Mike as a separate change |
| 5 | LOW | `translate_with_claude.py:857-858` and `:890-893` refuse a lone `\r` and a document that no longer parses, and no test covers either refusal (tooling) | Untested guard, pre-existing | Not introduced here; this change's seed run kept every terminator and every file parses | FOLLOW-UP no issue: pre-existing code this change does not modify; offered to Mike with the tooling lens's two test shapes |
| 6 | P2 | Banners the save keeps and no `clans.xml` row restores (player clan, player-edited banners, Realms coronation banners, clans created in play) still name the removed icons; the layer is dropped without a crash (Codex native read; lens 5) | Save compatibility, inherited from PR #1265 | Out of this diff's scope: `BannerInjectionService` restores only clans with a configured key that the player has not edited | FOLLOW-UP #1285 |
| 7 | n/a | The repack listed the four icons as unused. Both keys that flew them hold negative rotations (`-134`, `-18`); of 243 banner keys at 9a409deac^, 9 hold a minus sign, and a digits-only parse misses exactly these four TAOM icons (found by the orchestrator while checking finding 1) | Parser that does not match the key format | Third time: Angáladh (rca-anduin-vale-angaladh-2026-10-28.md finding 12), PR #1265, and this session's own icon gallery script, which first called 15007 and 10500 unused | `banner-icon-generation.md` now names the gate to run before deleting an `<Icon>` row; lesson appended to `lessons/data-content-cultures.md`. The prune's own tool is not in the repo, so the cause is likely, not proven |

## Root-cause pattern

Findings 1 and 7 share one cause: a banner key was read by something other than the parse the gate uses.
The gate (`test_every_shipped_banner_icon_exists`, whose key regex accepts negative tokens) caught the
deleted icons on the first run; every miss came from a side script with its own regex. Twice the side
script was written in the same session that relied on it.

## Why each reviewer caught or missed these

- Lens 7 caught finding 1 because it decoded the old and new sprites and counted every banner that flies
  each replacement; it also ran `data_gates.py` (9 PASS; the 2 failures, `armour-classes` from another
  session's live Armory edits and `loc-orphans` from eafb9af54, are outside this change).
- Lens 5 caught finding 2 by grepping the whole repo for the old ids, and traced finding 6 to the save
  fields and `BannerInjectionService`'s skip conditions.
- The tooling lens caught findings 3 to 5 and confirmed the writers kept BOM and terminators.
- Codex settled the open native question in finding 6 (the layer is omitted, no default emblem, no crash)
  and disputed that the English placeholders would be read as translated: discovery returns all 91 ids as
  untranslated in all 12 languages. It could not run the full suites (SDK access, no pytest or numpy in its
  sandbox); the session's runs stand as the evidence.
- None of them saw finding 7, which needed the state before the repack.

## Feedback memories to codify

None. The lesson and the doc line carry the rule.
