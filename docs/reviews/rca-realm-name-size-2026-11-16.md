# RCA: review of the realm name size slider (#1380)

#1380 adds an MCM slider, Realm Name Size (50% to 250%), that scales the realm names on the campaign map. It also
raises the plain-font base size from 30 to 42, so a Chinese, Japanese or Korean name reads at about the size of an
aniron name. The review ran at tier 2 (UI that reads game state, a new MCM setting, gates and tests): lens 5 (data
flow) and lens 7 (XML), each at `effort: "high"`. Codex did not run: it is out of usage, and Mike said to skip it
(2026-11-16). The tier skipped lenses 3 and 6, so Step 4 had no proposals to apply.

Lens 5 traced 11 flows from the slider to `TextWidget.OnRender` in the installed v1.5.7 Gauntlet and found them
connected. One of them is the order of the constant `Brush=` and the bound `Brush.FontSize`: the binding writes onto
the final per-widget brush clone. Lens 7 ran the data gates: 5 PASS, 1 FAIL and 3 NOT RUN. The FAIL is `loc-orphans`
on three `taom_char_*perk_tier*` rows from v2.0.38, which this change does not touch.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The brushes declare `FontSize` 34 and 42, the same values as `RealmNamesVM.AnironFontSize` and `PlainFontSize`. The binding overwrites the brush size on every label, so an edit to a brush alone changes nothing on screen, and an edit to a constant leaves the brush wrong with no warning. The VM summary claimed the two are equal, and no test checked it (lens 7 finding 1, lens 5 item 11) | Mirror value kept by a comment | The constants became the source of truth, and the brush was edited by hand to match (30 to 42) | Fixed: `RealmNamesPrefabTests.Brushes_DeclareTheViewModelsBaseSizes` compares both brushes with the constants. It failed with the plain brush at 30 (`Expected:<42>. Actual:<30>`) and passes at 42. The brush comment names the test. The category is on file: the lord inline-skills lesson in `lessons/data-content-cultures.md` ("gate the mirror, not the comment"). A GUI entry in `lessons/localization-ui.md` now points to it |
| 2 | LOW | `Realm Names` and `Realm Name Size` both had `Order = 7` in the "Realm Borders" group. It was the only tie among the 116 groups in `TaomSettings.cs` (lens 5 side note) | Display order left to chance | The slider went in after its toggle, and the settings below it kept their numbers | Fixed: the slider takes 8, and the six later settings move down one place (9 to 14). MCM saves values by property name, so no saved value changes, and `check_mcm_default_flips.py` reports OK. No gate: the cost is display order only, and nobody checked how MCM breaks a tie |

Not applied: lens 7 finding 2 (LOW) asked for 預設 in place of 默認 in the CNt hint, because the CNt file used 預設 97
times and 默認 nowhere else. This work adopts the Chinese team's text as the reference, and their hints write 默認：
371 times and 預設： 3 times (counted in their `TAOM_CN` drop of 2026-11-16, CNs the same). The row keeps 默認.

## Why each lens caught these

- Lens 7 found row 1 when it searched the tests for a size comparison and found none.
- Lens 5 found row 1 when it traced the brush value to the binding that overwrites it, and row 2 when it read the
  attributes beside the new setting.

## In-game checks owed

`realm-borders.md` "Not yet run in game": in a Chinese game the names read about as large as in an English game, and
the slider at 50% and 250% resizes them without a restart.
