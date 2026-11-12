# RCA: Battle Action Bar dims the battle (deep review, 2026-11-12)

## Top-line

Mike reported that with the MCM setting Show Battle Action Bar on, a field battle renders dimmed, and that turning the
setting off removes the dim (#1119). The cause was in `Main/_Module/GUI/PreFabs/BattleActionBar.xml`: the "Background
Panel" was `StretchToParent` on both axes, with `Color="#00000099"`, inside a `CoverChildren` container. In v1.5.7
`Widget.OnMeasure` passes a `CoverChildren` widget's measure size through to a stretched child, so the container
measured the full layer. Bottom alignment with `MarginBottom="120"` then put its top at y = -120. The panel dimmed all
of the screen except the bottom strip of about 112 px, and the buttons and the formation name sat above the top edge.
Lenses 5 and 7 both read this geometry from the installed engine source.

The fix moves the sprite and colour onto the container and deletes the panel. It also removes the file's row from
`GauntletStretchInCoverRatchetTests.Known`. The tier 2 `/deep-review` ran Lens 5 (Data flow) and Lens 7 (XML) at
`effort: "high"`. Mike declined the Codex pass. The lenses found no defect in the fix and four LOW findings, all fixed
before the commit.

Repeat check: `docs/reviews/lessons/localization-ui.md` holds this exact shape under #866 and its Fords of War
recurrence (2026-10-31). This is the third recurrence, and the first one in a file the gate already listed.

## Findings and root causes

| # | Sev | Finding | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | The button `ListPanel` had no `HorizontalAlignment`, so with 0 or 1 buttons the row sat left under a centred name (Lens 7) | Layout; alignment default (`Left`) | The flaw dates from the port. The old layout drew the bar above the screen, so nobody ever saw the row | `HorizontalAlignment="Center"` on the `ListPanel` |
| 2 | LOW | A formation with no action showed an empty dark box holding only its name (Lenses 5, 7) | UI state; visible with nothing to show | Same as 1: the bar was never on screen, so the empty state was never seen | Mike chose to hide it. `BattleActionBarVM.UpdateForFormation` hides the bar when the action list is empty; `BattleActionBarVMTests` pins both paths |
| 3 | LOW | The new doc and lesson sentences said the dim "covered the whole battle". The geometry leaves the bottom 112 px clear, and needs a selected formation (Lens 5) | Docs claim not read from the source | I wrote the claim from the lesson's general rule and did not compute this prefab's layout | Reworded from the traced geometry, in the doc, the lesson and #1119 |
| 4 | LOW | The lesson called both remaining known-list rows "under a `Fixed` ancestor". `PreBuildCharacterSelection.xml` is `Fixed` on width only; its height is the full column by design (Lens 5) | Docs claim broader than the code | I checked the width axis and generalised | Reworded per file |

Findings 3 and 4 repeat `rca-ballot-fief-count-2026-10-23.md` finding 5 ("Docs claim not read from the source"). The
standing rule (`.claude/rules/evidence-over-claims.md` section C) already covers it; the lens caught it before commit.

## Why the original defect shipped

- **2026-05-07, port (55950378a):** the panel arrived with the bar. The bar's view never ticked until #970, so the
  layer never attached and nobody could see the defect.
- **2026-10-23, #970 (40385aa5b):** the view became a `[DefaultView]` and the layer attaches. The setting went off by
  default and the feature was parked. The in-game check `BattleActionBarChecks.RecordAttach` proves that the movie
  loaded, not what it draws.
- **2026-10-31, the ratchet (e6dbea3e0):** the gate counted this file as one of five older offenders and allowed a
  count of 1. Nobody checked whether each listed parent was bounded.
- **Earlier investigation:** Mike said the dim was looked into before. UNVERIFIED: no issue, RCA or lesson records it
  (searched `gh issue list --state all` for dark, dim, darker, brightness, black screen, action bar).

The lesson's new "Recurred" bullet says what to check: what bounds each listed parent. The two rows left
(`MomentumView.xml`, `PreBuildCharacterSelection.xml`) were checked by hand and are bounded by design.

## Why each agent missed these

- **The builder (this session)** fixed the panel but wrote the doc claims from the lesson, not from this prefab's
  geometry (findings 3, 4). It did not ask whether the bar had ever been on screen, which is how findings 1 and 2 hid.
- **Lens 5 (Data flow)** found 2, 3 and 4, and traced the old layout off screen.
- **Lens 7 (XML)** found 1 and 2, and traced the same layout independently.
- **Lenses 1, 2, 3, 4 and 6** did not run (skipped by tier 2), so they missed nothing.

## What proves it

- RED: `UpdateForFormation_FormationWithNoActions_HidesTheBar` failed before the view model change.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CompanionTactics|FullyQualifiedName~GauntletStretchInCover|FullyQualifiedName~Prefab"`:
  449 passed, 0 failed.
- `GauntletStretchInCoverRatchetTests` now holds `BattleActionBar.xml` to zero offenders.
- FOLLOW-UP: the in-game check (no dim, the box at the bottom centre, the empty-formation case hidden) is owed on #1119.
- FOLLOW-UP: no issue: a gate rule for "a `CoverChildren` parent with no bounded ancestor" would add complexity for no
  current case; the two remaining known rows were checked by hand and are bounded.

## Feedback memories to codify

None. The rule is in `localization-ui.md` under #866, with this recurrence.
