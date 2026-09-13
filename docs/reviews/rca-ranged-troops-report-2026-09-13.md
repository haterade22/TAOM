# RCA: the ranged-troops HTML report and the veteran militia step (#582 follow-ups, 2026-09-13)

**Top line.** Six review agents (standards, engine-claim verification, efficiency, completeness,
data flow, tooling correctness) reviewed the two follow-up commits to #582: the tracked HTML
document of every ranged troop (`b2e058e8`) and the +15 step for the elite militia (`d5e43caf`).
No HIGH. Eight findings were confirmed by re-reading the code or the decompile and fixed in the same
session. The one numeric defect: the page's "Spread" column used the bow's skill factor for
crossbows too; the engine's `CrossbowAccuracy` is -0.0005 per level against the bow's -0.0009
(`DefaultSkillEffects.cs:249,254`), so every crossbow row understated its spread by 6 to 9%. The
tracked page carried a wall-clock timestamp, so every regenerate dirtied the repo, and it had no
document skeleton or charset, unlike every other tracked HTML under `docs/`. The militia change
itself was clean: the 30 ids match the elite bindings exactly, the engine reads every binding TAOM
writes, no skill template shadows them, and the veteran share of a militia spawn is perk and policy
driven, so the bonus does not feed back into how often veterans appear.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `weapon_inaccuracy` applied `1 - 0.0009 x skill` to crossbows; the engine's `CrossbowAccuracy` factor is -0.0005. Every crossbow "Spread" cell was 6 to 9% too tight, growing with skill. | Engine constant per class | The first review verified `BowAccuracy` (the question at the time was about bows) and the report reused that one number for both classes; the tests checked the formula shape, not a per-class value. | `ACCURACY_FACTOR = {"Bow": 0.0009, "Crossbow": 0.0005}`, `weapon_inaccuracy(accuracy, skill, cls)`, a test per class, the page names both. Regenerated. |
| 2 | MED | `docs/reference/ranged-troops.html` embedded `generated <UTC clock>`, so two runs on the same data differed by one line and the worktree was dirty after every regenerate; `.gitattributes` had no eol rule for it. | Reproducibility of a tracked generated file | The clock came from the markdown report's context, where it is harmless (gitignored); the docs copy was added afterwards without asking whether the output is a pure function of its inputs. | No clock in the HTML (the md keeps its timestamp); a test that two renders are byte-identical; `docs/reference/ranged-troops.html text eol=lf` in `.gitattributes`. |
| 3 | MED | The page had no `<!DOCTYPE html>`, `<html>`, `<head>` or `<meta charset="utf-8">`. Opened from disk a browser has no encoding declaration, so `[Rhûn]` and `Beruthiel's` can render as mojibake; every other tracked HTML under `docs/` carries the skeleton. | Document conventions | The page was first written for the artifact host, whose contract asks for page content without a skeleton, and the tracked copy was added without re-checking what its siblings in `docs/` look like. | Full document skeleton in the generated file; a test asserts doctype and charset. The artifact publish strips the skeleton it does not want. |
| 4 | LOW | `troop_rows` picked the fastest launcher across all of a troop's sets and, separately, the strongest ammo across all sets, so a row could pair one set's bow with another set's quiver. Inert today (0 of 227 troops vary the launcher across sets). | Cross-set combination | The loader flattens sets into a union for the ladder rules (correct there: the rule is about reach); the report reused the union for a per-kit number. | Ammo is chosen from the sets that field the chosen launcher; a fixture with a fast bow beside weak arrows and a slow bow beside strong arrows pins it. |
| 5 | LOW | `elite_militia_troop_ids()` raised `KeyError` if something had filled `_militia_ids_cache[root]` directly: `militia_troop_ids()` returned early from its own cache without filling the elite one. Not reachable by any caller or test today. | Two caches, one invariant | The elite cache was bolted beside the basic one; the early return predates it. | `militia_troop_ids()` re-reads unless BOTH entries exist; a test fills the basic cache out of band and expects the real elite ids. |
| 6 | LOW | `load_ranged_troops` skipped a troop file that does not parse with a bare `continue`, so its troops vanished from the report and the gate without a word. Found by this session's own broken test fixture. | Gate that quietly checks nothing | Copied from the armour loader, which has the same shape. | `failures` list, printed as a warning by the tool; a test. |
| 7 | Doc | Three places still said a militia promotion is "flat by design" (the monotonicity test docstring, `troop-skill-balance.md` twice); the C# test's comment claimed the three militia regexes are kept "character-for-character in step" (they never were, and the tool's now captures a group); the page said `difficulty` is read "only by tooltips" (it also gates the player's equip in the inventory screen); the library comment named the wrong side of `Mission.OnAgentShootMissile` as the bonus. | Stale prose | Each statement was true when written and nothing pointed from the code to it. | All corrected; the C# test project rebuilt (0 errors). |
| 8 | Deferred | Six commit subjects (`d20838e4` to `d5e43caf`) run 64 to 78 characters against the 50/72 rule. | Commit convention | Subjects were written for content, not length; the repo's recent history runs 70 to 90 and nothing measures it. | Not rewritten: shortening them needs a rebase over unpushed history while another session holds uncommitted work in this worktree, which the multi-session rules forbid. Subjects stay under 50 from here on. |

## Root-cause pattern: a copy for a new audience is a new artifact

Findings 2 and 3 share one cause. The HTML existed for a page host that adds its own skeleton and
never diffs the file; the tracked copy is opened from disk and diffed on every commit. Copying the
bytes to a second location made it a second artifact with different requirements (a charset, no
clock, a pinned line ending), and none of those were re-derived. The general form: when an output
gains a second consumer, list what that consumer needs before reusing the bytes.

Finding 1 is the older pattern, verify one claim and reuse it for its neighbour: `BowAccuracy` was
checked because the question was about bows, and the crossbow factor rode along unverified.

## Why each agent missed these

- **Standards** (haiku): found the subject-length deferral (finding 8); its rules do not cover
  document skeletons or reproducibility.
- **Engine claims** (sonnet): found finding 1 by decompiling `DefaultSkillEffects` for BOTH classes
  when the prompt asked it to check the crossbow value explicitly; found the two prose slips in 7.
- **Efficiency** (haiku): nothing to find; measured the two index walks at 130 ms.
- **Completeness** (haiku): found the untested edges that became the new tests for 1, 4 and the
  spearman slot; its rule set does not ask about reproducibility.
- **Data flow** (sonnet): found finding 4 by counting how many troops vary launcher and ammo across
  sets; confirmed the militia binding to engine flow end to end.
- **Tooling correctness** (sonnet): found 2, 3 and 5 by asking what the tracked output looks like
  after a second run and what a browser does with the file from disk; that agent is the one the
  deep-review skill adds for file-writing Python, and it earned its place twice in two days.

## Lessons codified

- `docs/reviews/lessons/build-tooling-workflow.md`: a tracked generated file is a pure function of
  its inputs (no clock, a `.gitattributes` pin, a byte-identical test) and carries the conventions
  of its location, not of the host it was first written for.
- `docs/reviews/lessons/testing-qa.md`: a formula that branches on a class gets a verified constant
  and a test per class; verifying one branch and reusing the number for the other is how a
  crossbow got a bow's accuracy curve.

## Not findings

- `AiRangedHorsebackMissileRange` is written managed-side and read only in `TaleWorlds.Native`;
  the page's "Opens m" column states the managed formula and cannot be checked further.
- Umbar, Shaghana and Abanissa bind Harad's militia ids, so `troops_umbar.xml` holds no militia
  and was rightly untouched by the restat.
