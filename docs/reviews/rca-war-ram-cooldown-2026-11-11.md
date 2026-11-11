# RCA: war ram cooldown retune (deep review, 2026-11-11)

## Top line

Mike asked for the war ram's head-butt cooldown to move from 10 s to 35 s. The change is one constant, one test line and the prose that quotes the value. A tier 2 `/deep-review` found no code defect. It found prose that still gave 10 s as the current value: four passages in the files I had edited, fixed in this change, and two comments in other features, left for the maintainer to decide.

## Review run

- **Tier 2:** a tuning constant read by a mission behaviour tree, plus one test edit. No patch, model, save, sync or native call changed.
- **Lens:** Data flow, at high effort. The other lenses were skipped by tier. Step 4 (improvements) did not apply, because neither the Efficiency lens nor the Design lens ran.
- **Codex:** not authorized at tier 2, so that pass is owed before merge.
- **Blast radius:** `graphify affected WarRamConfig` returned no nodes, and graphify has no node for a constant, so it cannot name a reader of one. A repo-wide grep of `AttackCooldownSeconds` found one production reader, `WarRamBehaviorTree.cs:60`, plus the ram's own tests.
- **Live files:** none changed after my first edit.
- **Re-checked by me before acting on the lens report:** the stale passages, the two other-feature comments, the stamp writer (`ElephantLikeAttackTasks.cs:117`, the only write to `TrampleLastFired`) and the engage node (`ElephantLikeEngageDecorator.cs:35` to `77`, which scans and writes one blackboard float and never steers the ram).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | `WarRamConfig.cs` lines 37 to 39 and 72, and `war-ram.md` lines 148 and 176 to 177, still gave 10 s as the current cooldown. | Documentation rot after a retune | My first grep listed `WarRamConfig.cs` lines 37 and 71 and `war-ram.md` lines 148 and 176. I sorted the hits by file, read them as retune history, and told Mike so. They are present tense. | Fixed in this change. Lesson added to `lessons/misc.md`. |
| F2 | MEDIUM | `ElkConfig.cs` line 52 and `AnimaliaConfig.cs` line 44 give "the ram's 10 s" as the reason for their own 10 s. The ram no longer has 10 s. | Cross-feature comment coupling | The grep covered only files named for the ram, so it could not see a comment in another feature that quotes the ram. | Not changed. Whether the elk and Animalia follow the ram is a balance decision. |

FOLLOW-UP: no issue: the elk and Animalia cooldown comments and values are a balance decision for the maintainer, raised in chat on 2026-11-11.

## Root-cause pattern

Both findings come from one habit. A tuning value is quoted in prose beside the code, in the feature doc and in sibling features. I searched for the old value only where I expected it, and I judged each hit by its file instead of by whether its sentence was dated. This has shipped before: the reports `deep-review-012`, `deep-review-014`, `deep-review-025` and `deep-review-033` each list stale doc or comment lines found after a change.

## Which lens found it

Only the Data flow lens ran, and it found both. The miss was mine, in the grep before the review, so no lens prompt changes. The preventive rule is the lessons entry: search the old value in every prose form across `Main/` and `docs/`, and call a hit history only when its sentence is dated.

## Left alone on purpose

- `docs/reference/lotrlome-war-ram-changes.md` line 216 sits in the dated 2026-09-18 section and describes what the test pinned that day.
- `docs/community/bannerlordmodding-lt/guides/custom_creature_battle.md` lines 200 to 204 tell the retune history, which ends at 10 s.
- `war-ram.md` line 169 ("Retuned 2026-09-04 to a 10s cooldown") is history.
