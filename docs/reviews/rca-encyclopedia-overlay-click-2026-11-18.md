# RCA: encyclopedia click falls through to the screen below (PR #1436, 2026-11-18)

## Top-line

The maintainer review of PR #1436 (the bar root accepts events again, and five preview overrides
return false) ran `/deep-review` tier 2: lens 5 data flow and lens 7 XML, each at effort `high`.
Neither found a CRITICAL, HIGH or MEDIUM. Both found the same LOW test gap, fixed in the review
commit. Codex did not run: it is out of usage.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | LOW | `BarRoot_PassesEveryMouseEventOn` checked only that the five preview overrides exist on `EncyclopediaOverlayWidget`. An override changed to `=> true` would pass it while the root took every page click (lens 5, lens 7). | Test checks shape, not value | A real `Widget` needs a `UIContext`, so the test stopped at reflection. | The test now builds the widget with `FormatterServices.GetUninitializedObject` and asserts each override returns false. |
