# RCA: Settlement Nameplate Relation Colours (#591), 2026-09-13

## Top-line

`/deep-review` (five agents) on the new `SettlementNameplateRelation` feature returned three
findings before commit: one standards, one performance, one data flow. All three were re-read
against the source and confirmed, and all three are fixed in the same session by one change,
extracting the widget's paint and mirror bodies into `SettlementPlatePresenter`. The Codex pass
that followed (second table below) found one P2, two P3 and one test gap, all fixed. Nothing
shipped. The changeset's engine bindings (27 members) verified clean against the installed v1.4.8 DLLs.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `TaomSettlementPlateWidget.cs` was 190 lines, over the ADR-002 entry-point ceiling of 150. The logic was already delegated (palette, policy), the excess was twelve XML colour properties plus the paint and mirror bodies. | Standards (ADR-002) | The widget was written as one class because the FieldCamp precedent (`PartyNameplateCampIconPatch` + `CampNameplateIconPresenter`) was read for its never-throw posture, not for its split. Line count was never measured before the review. | Presenter extraction. `wc -l` on every entry point before `/deep-review`; the rule is a ceiling, and a widget with a dozen bindable properties reaches it fast. |
| 2 | LOW | The banner alpha compare read `_banner.Brush.GlobalAlphaFactor`; the `Brush` getter clones the brush on first access, while `ReadOnlyBrush` (available: `MaskedTextureWidget` derives from `BrushWidget`) does not. One allocation per plate per session, already forced by vanilla's own per-frame `Brush.GlobalAlphaFactor` write, so no runtime change, but inconsistent with the text path two lines above. | Performance / consistency | The class hierarchy was assumed (`TextureWidget` with its own `Brush`) instead of read; the grep for `ReadOnlyBrush` stopped at the first hit. | Both compares read `ReadOnlyBrush`. When two sibling lines touch the same engine surface, read the type's base chain once and use the same accessor on both. |
| 3 | MED | `MirrorPlate` returned early on a non-finite plate alpha BEFORE calling `PlateAlphaPolicy.TextAlphaFor`, whose only production caller that was. The policy's tested NaN branch (`TextAlphaFor_NonFinite_ReturnsOne`) was unreachable, so a poisoned frame would leave the name text at whatever vanilla wrote instead of restoring full alpha as the doc comment promised. | Data flow / NaN gate reachability | A second guard was added in the entry point "for safety" after the policy already carried one. Unit tests cover the policy in isolation; the widget cannot be constructed in tests (needs a live `UIContext`), so nothing exercised the caller's path. | The guard is gone; the presenter calls the policy unconditionally and the policy owns the answer for garbage. Lesson appended to `docs/reviews/lessons/testing-qa.md`: a NaN/finite guard lives in exactly one layer, and when the tested layer's branch has a single untestable caller, read that caller for a pre-filter that makes the branch dead. |

## Codex adversarial pass (gpt-6-astra, ultra, after the three fixes above)

Codex verified all three fixes present, confirmed 27 engine bindings against the installed DLLs,
executed the Patch38 postfix through Harmony 2.4.2 against the real private method (0.35 to 0.5
for an untracked enemy, 0.25 at the fade midpoint, 1.0 for tracked outside the window), and
disputed two of the prompt's own worries (the initial neutral 0 IS pushed by
`GauntletView.RefreshBinding` without a change notification; the two `@Relation` bindings cannot
ping-pong because both setters and the VM setter reject unchanged values). It then found:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 4 | P2 | Making the plate fade exposed three siblings vanilla never fades: the tracked ring (`IsVisible="@IsTracked"`, alpha 1, and tracking bypasses the VM's distance cull), the party icon grid and the event icon list (both handed vanilla's pre-late-update lerp value each frame). A far tracked settlement kept an opaque ring after its plate, name and banner had faded. | Composition / data flow | The design traced the four visuals it changed and stopped there. The engine's own per-frame writes to the grid and events (`SetGlobalAlphaRecursively(num)`) were read for the text alpha value, not for what ELSE they drive. The ring has no alpha binding at all, so no review of bindings found it. | The presenter drives the ring, grid and events from the same text alpha through `NeedsWrite`; the ring got an Id and a path attribute in all three prefabs, pinned by the prefab test. Lesson appended: when a change makes one element of a composite fade or hide, enumerate every sibling the engine leaves opaque. |
| 5 | P3 | `Math.Abs(brushAlpha - target) > tolerance` is false when the brush already holds NaN, so the recovery write the RCA fix relied on could never land on a poisoned destination. Proved by executing the presenter body against the installed widget types; a gameplay path that poisons the brush is unverified. | NaN gate (destination side) | Finding 3 fixed the INPUT side of the same write and the test covered the policy; the destination compare was written as a tolerance and never asked "what if the current value is NaN". Seventh shape of the NaN class: the gate on the value you are about to overwrite. | `PlateAlphaPolicy.NeedsWrite(current, target)` returns true for a non-finite current value, four tests, every alpha write in the presenter goes through it. |
| 6 | P3 | The feature doc, the palette comment and two test comments said a 7-character colour "blanks the whole nameplate movie with no log". The attribute loader (`WidgetExtensions.SetWidgetAttributeFromString`) catches per attribute with `Debug.FailedAssert` and keeps the previous value; the override is silently ignored, the movie renders. | Documentation accuracy | The throw was verified (`Substring(7, 2)`); the consequence was inferred from the sprite-name rule ("renders blank, no log") without reading the loader's catch. | All four statements corrected. State a failure's consequence only after reading the code that catches it. |
| 7 | obs | The prefab tests could not fail on a re-nesting that pushes the item widget beyond `MaxAncestorDepth` (4); Codex proved it with an in-memory mutation that passed all five predicates while production mirroring would silently stop. | Test gap | The tests pinned what the widget reads by path and not what it reads by walking parents. | `PlateWidget_AllSizes_ItemAncestorWithinProductionDepth` walks the XML parents to the item widget and asserts the hop count against the widget's (now internal) constant. |

## Root-cause pattern

Findings 1 and 3 share a cause: the widget carried code that belonged one layer down. The
line-count breach was the visible symptom; the duplicated finite guard was the invisible one. The
NaN-gate class has now shipped six times in TAOM (see `csharp-architecture.md`), always because the
rule was one category narrower than the bug. This instance is a seventh shape, not a seventh
shipment: the gate was correct and tested, and the caller made it unreachable. The category name
for it is "guard duplicated across layers", and the check is reachability, not polarity.

## Why each agent missed (or caught) these

- **Standards:** caught #1 by measuring the file.
- **Compatibility:** not in scope; verified every engine member (27) against the installed DLLs.
- **Performance:** caught #2 by reading `BrushWidget.Brush` and `ReadOnlyBrush` bodies; the
  hierarchy detail (`MaskedTextureWidget : TextureWidget : ImageWidget : BrushWidget`) was
  confirmed by the compatibility agent, which is why the fix is safe.
- **Completeness:** not in scope; confirmed tests, docs, issue, CHANGELOG, IoC order.
- **Data flow:** caught #3 by tracing `TextAlphaFor` to its single call site and asking which
  inputs could reach it. The per-file agents could not: the policy file is correct on its own.

## Feedback memories to codify

None new. The reachability lesson goes to the lessons file (cross-feature record); the ADR-002
measurement habit is already a rule and needed applying, not writing.

## Files

- Fixed: `Main/Features/SettlementNameplateRelation/TaomSettlementPlateWidget.cs` (now under 150 lines,
  plus the tracked ring reference), new `Main/Features/SettlementNameplateRelation/SettlementPlatePresenter.cs`
  (ring, party grid and event list follow the text alpha), `PlateAlphaPolicy.NeedsWrite`, the three
  prefabs (`Id="TrackedRingWidget"` + `TrackedRingWidget=` path), the prefab and policy tests.
- Docs: `docs/features/settlement-nameplate-relation.md` (Key Files, loader behaviour, composition),
  `docs/reviews/lessons/testing-qa.md`, `docs/reviews/lessons/localization-ui.md`.
- Codex artifacts: `docs/reviews/codex-adversarial-settlement-nameplate-relation-2026-09-13.prompt.md`
  (raw transcript untracked by convention).
