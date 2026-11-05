# RCA: Messengers send-messenger brushes (#989, 2026-10-24)

> **1.4.x port note (plans/131, 2026-11-04).** Ported with `2c7f7fae9`. On 1.4.x the prefab extension
> lives in `Main/Features/Messengers/UI/` and the gate scans `Main/` only. The brush counts below are the
> 1.5.x desktop's. On the installed v1.4.8, `Popup.Button.Base` and `Popup.Button.Text` are declared in
> `Native/GUI/Brushes/Popup.xml` (lines 2 and 195) and neither old name is declared by any module. The
> follow-up on TAOM's own `GUI/PreFabs` brushes is WP140's.

## Top-line

The Messengers "send messenger" button on the encyclopedia hero page named two brushes no module defines
(`Popup.PartySelection.Confirm`, `Popup.PartySelection.Button.Text`), so Gauntlet drew it with the default brush: no
button art and no state looks, with nothing logged. The fix swaps in vanilla's `Popup.Button.Base` and
`Popup.Button.Text` and adds a gate test. The tier 2 review (data flow and XML lenses; Codex not run, as no paid
dispatch was authorized) found no CRITICAL or HIGH, 2 MEDIUM and 4 LOW findings in the fix and its test, all fixed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 0 | (the defect) | Two undefined brush names on the Messengers button. | Silent engine fallback | `BrushFactory.GetBrush` returns null with no log and `BrushWidget` falls back to the default brush, so nothing failed; no gate checked brush names. | `PrefabExtensionBrushTests`; lesson in `localization-ui.md`. |
| 1 | MEDIUM | The button's label did not dim when the button was disabled: `ButtonWidget` passes its state to children only with `UpdateChildrenStates="true"`, which the Messengers button lacked (both lenses). | Gauntlet state propagation | The fix changed only the brush names; the Family Tree buttons beside it set the attribute, and nobody compared the two. | Attribute added and asserted in `MessengerEncyclopediaPrefabTests`; lesson in `localization-ui.md`. |
| 2 | MEDIUM | The new gate counted every brush file of every installed module (2,507 names) while the engine loads only TAOM's dependency modules, top-level files keyed by name with the last module winning (about 2,300): a brush that draws unstyled in game could pass (both lenses). | Gate models the wrong registry | The sibling gate `PrefabExtensionBindingTests` already states these rules; the new test was written from the file system, not from the loader. | The gate now models the loader and pins one excluded name; lesson in `testing-qa.md`. |
| 3 | LOW | The gate read only `Brush=`, not other brush-typed attributes such as `IconBrush`, and did not skip Gauntlet `!Constant` or `*Parameter` values. | Gate scope | | Regex widened. |
| 4 | LOW | The test rebuilt the repo root and file filters instead of using `RepoPaths`. | Duplicated infrastructure | | Uses `RepoPaths`. |
| 5 | LOW | A comment edit joined two comment lines into one. | Edit slip | | Split back. |
| 6 | LOW | The gate was not named in the GUI gate tables reviewers run, and two docs overstated or missed the change. | Gate not registered | CLAUDE.md asks for "the gate, then one line naming it". | Named in `moduledata-validation.md` and `gui-ui.md`; docs corrected. |

FOLLOW-UP (no issue: filing one needs Mike's word): TAOM's own `GUI/PreFabs` name 9 brushes no loaded module
defines (in `CareerSystem/CareerScreen.xml`, `PresetsOverlay.xml`, `BattleActionBar.xml`, `CustomBattle/ArmyComposition.xml`
and the unused `EventXml.xml`), plus two vanilla-parity names. Extending the gate over `GUI/PreFabs` would turn it red on
those, so it waits for its own change: fix or allowlist each name, and correct the stale count in `gui-ui.md`.

## Root-cause pattern

Findings 0, 1 and 2 share one cause: Gauntlet resolves names and states silently, so a wrong name or a missing
attribute draws something plausible instead of failing. The only defence is a test that models exactly what the
engine does, and the first version of the test modelled the file system instead.

## Why each reviewer caught what it did

- **Data flow (5):** traced the button's states and the loader's module list.
- **XML (7):** decompiled the button and image widgets for state propagation, measured the loaded brush set, and
  scanned TAOM's own prefabs.
- **Codex:** not run (no word for paid dispatch on this fix).

## Feedback memories to codify

None beyond the lessons.
