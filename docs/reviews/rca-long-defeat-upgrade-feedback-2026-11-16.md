# RCA: the Long Defeat's upgrade feedback (#1388) and the archers no upgrade could reach (#1401)

The maintainer reported from play that a bought troop upgrade "does not seem to upgrade the troops you currently have on the field". The upgrade of #1161 did replace soldiers. This file records why it could look as if it did nothing, and the findings of the review of the fix.

## Why a working upgrade looked broken

| # | Cause | Evidence | Fix |
|---|---|---|---|
| 1 | A successful buy showed no message. `LongDefeatHudVM.Upgrade` showed only a refusal text, and the text for a success is empty. The swap has no effect and no sound | The code path; no log can show an absent message | A notice on the run's view, said once: the step, the count and what is left |
| 2 | The row read "15 gold each" and "100 standing, gold for 40". It never said that one click takes all 40, nor the total. A new run has 600 gold: 40 of 100 militia, or 20 after a hero | `shop.json`; bot runs with 5000 gold still show part orders (`ordered=47 found=110`, run 20261116-055255) | The row states the deal: "Upgrades 40 of 100 for 600 gold" |
| 3 | A click while the garrison musters took only the batches that stood. The foot arrive 20 at a time, 4 seconds apart | Run 20261116-055255: the first order took 20 militia; 110 more arrived after it as militia | No upgrade is sold, and no row is listed, until the last muster batch stands |
| 4 | The booking after an order subtracted from the last read and ignored what the field found, so soldiers the order could not reach were offered again until the next half-second read | Run 20261116-024002: `ordered=65 found=44`, then 8 tries `ordered=21 found=0`, one a tick | The order books what the field found less what it replaced |
| 5 | #1401: with the cavalry class the engine's castle tactic splits the archers into the lowest empty formation indices, and the mounted lord holds index 2, so a third of them land in index 4. That is the formation of TAOM's tower post archers, and the upgrade filter skipped every soldier in it, posted or not | Run 20261116-024002: the wave line reads `4:21`; foot class runs read `4:0` | The filter keeps the post registry test only |

Why nobody saw 1 to 4 before the maintainer played: the proof of #1161 was a bot run with 5000 gold. The bot reads no screen, buys every row in the same second and had gold for every soldier that stood, and its check compared the replaced count with the smaller of ordered and found, never with the size of the line.

## Findings of the review

Tier 1 review of the uncommitted work on ad4a8854b. First wave: lenses 5 (data flow), 2 (engine compatibility), 7 (XML and ModuleData) and Tooling. A HIGH in the evidence files sent the corrected tree to a second wave: lenses 1, 3, 4 and 6. The Codex pass did not run: the maintainer said to skip it while the account has no usage.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The proof row for #1401 passed on `found=65` alone. A line with `ordered=65 found=65 replaced=44`, the very failure the fix could cause (the 21 archers found but not replaced), read PASS | A proof row that does not read the value the fix changes | The row was written from the symptom of the old bug (`found=44`), not from what the fix must now do. Its test fed it only good lines and the old bad line | The pass pattern requires replaced equal to ordered; fail patterns for a short replacement and for a full first order that finds another number. Tests feed each. Lesson 1 |
| 2 | MED | The #1401 fail pattern named one run's number (`found=44`) and any troop's none-found skip. A regression of 20 or 22 soldiers read NOT REACHED; an unrelated skip read FAIL | A pattern copied from one log | Same cause as 1 | The patterns name the troop and compare with the expected 65 |
| 3 | MED | A refund row in the #1388 manifest was unreached by design, so the issue could never read PASS | A row for a case no proof run makes | The plan listed a row for every item without asking whether a run can reach it | The row is a human claim; a test passes every row on a good run's lines |
| 4 | MED | Both manifests named the branch base as `fixed_in`. Trunk builds without the fix contain that commit, and three rows read FAIL on them | A pin that precedes the fix | The builder had no fix commit to name | The manifests are pinned to the fix commit in the commit after it |
| 5 | MED | Retiring the old row key by deleting its English row left five language rows with English text and no source row; a third localization test went red | A key retired where only the project lead may remove its language rows | The plan read the orphan gate, which looks at English rows only, and not the text integrity test | The old key stays in use for a row the gold pays nothing for; no key is retired |
| 6 | MED | The HUD view resolved the outcome log from the container on every screen tick | A lookup on a hot path | A move of the method to keep the view under its line limit put the resolve at the call site | The view keeps the log in a field |
| 7 | LOW | The proof run named for #1401 could not reach the skirmisher row: with the muster gate the bot spends on the whole foot line first | A proof command not re-derived after the behaviour changed | The command was copied from the run that showed the old bug | 30000 start gold, with the reason in the manifest, the doc and the issue |
| 8 | LOW | Test and comment items: a source grep where a behaviour test was possible; no test of what the director passes as found; a duplicate assert; a comment with numbers the data can change; a comment that explained absent code; real-log tests that read a temporary folder | First-draft gaps | Each is small | Fixed in place |

Not applied:

- The design lens proposed a second field read on an order's tick in place of the found-based booking. Both remove the stale count. The booking is plain arithmetic in the run and does not depend on what the engine reports for a soldier faded in the same tick, so it stays.
- One notice slot: a refund can replace the buy's line within one refresh, and a bot that buys several rows in one refresh gets one check line. A player's click refreshes at once, so he reads the buy's line first. No queue is built.
- An error report from the field (a step whose new troop does not resolve) now books 0 standing until the next read. No shipped step can produce it.

FOLLOW-UP (#1401): the defender split still skips formation index 4, so the same 21 archers are not in its two archer groups. Whether they should be is a question of where soldiers stand, and it is the maintainer's decision.
FOLLOW-UP (#1402): the upgrade check can fail on a height difference at a replacement (`read=64` of 65, one spawn 1.2 m below the old place).

## Root-cause pattern

The feature was proven by a bot that cannot be confused. Every cause in the first table is something a person meets and a bot does not: no message, a row that does not state the deal, a click in the first seconds, a class the proof runs had not used. The review findings 1 to 3 repeat it one level up: the proof rows were written from the old bug's symptom, and nobody asked what a failed fix would look like on the same line.

## Lessons recorded

Two entries in `docs/reviews/lessons/testing-qa.md`, dated 2026-11-16 with this file as their source.
