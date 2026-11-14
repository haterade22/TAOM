# RCA: the Custom Battle Captain start (#1184), 2026-11-13

## Top line

`/deep-review` at risk tier 2 (view model code that starts a battle; no Harmony patch, save data or tick) ran lens 5
Data flow at `high` effort and one Codex pass (`gpt-6-astra` at `xhigh`, dispatched from the worktree, on Mike's word
for this session; prompt [codex-prereview-muster-captain-2026-11-13.prompt.md](codex-prereview-muster-captain-2026-11-13.prompt.md),
output under `raw/`). Both traced the general from Start to the mission on v1.5.7 and found the fix sound: no
CRITICAL, HIGH, MEDIUM or P1. Lens 5 confirmed three LOW findings and Codex one P2; two of them are the same
weakness in the tests. All four are fixed beside this RCA. No second wave. Lenses 1, 2, 3, 4 and 6 were skipped by tier.

The defect itself was found by reading, not by a player: the 2026-11-13 upstream review checked how Custom Battle
finds a sergeant's commander and saw that the muster screen passed a null general. Nothing was run in game.

Blast radius: `python tools/graphify_taom.py affected` (graph refreshed for the worktree) names only `TcbVM.cs` and
the new tests as callers of the new types. Live sweep: this change wrote nothing to the game folder.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | P2, LOW | The two source-text wiring tests stayed green when the defect returned: with the Captain guard turned into a Commander guard, or with the selection and the refusal removed, `general` is null again and every asserted substring is still there (Codex finding; lens 5 gap 8a) | test pins spelling, not behaviour | The decision (who needs a general, when to refuse) was written in the view model, which is game-tested UI, so the only test that could reach it was a text search | Fixed: the whole decision moved into `MusterGeneralChoice.ForStart`, a pure function with five behaviour tests. The view model test now pins that Start takes its decision from it, on the player type and the game type, and the order of decide, refuse, hand over, build, prepare |
| 2 | P2 | The check tests asserted that both outcome calls exist, not that the pass is written after the engine's start call and the fail from the catch (Codex finding) | test pins presence, not order | Same cause | Fixed: the test compares the positions of the start call, the pass, the catch and the fail |
| 3 | LOW | The general could be the enemy's commander, or a hero of the enemy's army, when both sides are the same faction; vanilla's `CustomBattleVM.PrepareBattleData` removes both commanders before it picks (lens 5) | inconsistent with vanilla | The builder read the engine helpers the screen calls and not vanilla's own caller of them, which is where the choice of a general is made | Fixed: the taken ids are the hero units of both armies and the enemy's commander; one more `Pick` test |
| 4 | LOW | `Issue1184Tests` had no "pass beside a fail" case, which the manifest lesson asks for (lens 5) | manifest test gap | The four cases were copied from the smallest sibling, which has none either | Fixed: fixture `muster_captain_pass_and_fail.txt` and its test; the C# test renders the fixture's lines |

## Root-cause pattern

Findings 1 and 2 are one pattern: logic placed in boundary code, then "tested" by searching the source for the right
words. A text search answers "is this call still spelled this way", and the defect was never about spelling: the
original bug was a correct call with a null argument. The repo already says where logic goes (AGENTS.md
"Architecture": patch, model or behaviour, then a service; a thin entry point needs no unit test of its own). The
view model felt too small to split, and that is how a decision with three inputs and a refusal ended up untestable.

This class has shipped before: `lessons/testing-qa.md` and `lessons/build-tooling-workflow.md` hold three earlier
entries on source-text pins (2026-08-23, 2026-10-08, 2026-10-22). A lesson alone did not stop the fourth, so the
rule now sits in `.claude/rules/tests.md`, which loads with every test file.

The rule that follows is narrow: a source-text test may pin wiring (this entry point calls that function with these
inputs, in this order). It may not stand in for the decision. When a text test is the only thing between a
defect and a release, the decision is in the wrong file.

## Why each reviewer missed or found these

- **Lens 5 Data flow** found 3 by decompiling vanilla's own Custom Battle view model and comparing its choice with
  ours, and found 1 and 4 by asking of each test what edit it would not see.
- **Codex** found 1 and 2 by applying four mutations to the view model in memory and running the source assertions
  against each. It also noted that a general added to a siege changes that mission (one more man, spawn position,
  detachability during deployment) without a failure; the doc now says so.
- **The builder** wrote the wiring test last, as a guard for the exact text of the bug, and did not ask what else
  would bring the bug back.

## Improvements (Step 4)

Lenses 3 and 6 did not run (tier 2), so there is no design proposal to apply.

APPLIED:
- `Main/Features/CustomBattles/Muster/MusterGeneralChoice.cs`: `ForStart` holds the decision; `Pick` takes the taken ids.
- `Client/Features/CustomBattles/Muster/TcbVM.cs`: Start calls `ForStart`; `TcbSideVM` exposes `LordIds`, `HeroUnitIds`, `Lord`.
- Tests: six new cases in `MusterGeneralChoiceTests`, the ordering test in `MusterChecksTests`, one fixture and one Python test.
- Proof: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MusterGeneralChoiceTests|FullyQualifiedName~MusterChecksTests"`, Failed 0, Passed 19; `python -m unittest tools.tests.test_ingame_verdict`, Ran 266, OK.

NOT APPLIED:
- A `Skip` outcome line for a refused Start (lens 5, optional): `ingame-verdicts.md` frames Skip for a feature that is off, and the refusal already shows its reason on screen.
- A pin on `MusterArmyBuilder.Rebuild` keeping `old.General` (lens 5 gap 8b): it is game-tested boundary code that this change does not touch; the manifest's human claim ("the other lord, not the player, gives the army its orders") is the guard.

## Lessons codified

- `docs/reviews/lessons/testing-qa.md`: "A source-text test pins wiring, never the decision".
- `.claude/rules/tests.md`: one line with the same rule.
