# RCA: Realm Borders deep review (#698, 2026-09-30)

## Top line

The seven-lens `/deep-review` of Realm Borders (uncommitted, bannerlord-1.5.x) confirmed 2 HIGH, 7 MEDIUM
and a tail of LOW findings, all fixed in the same session; one MEDIUM claim was refuted. The two HIGHs were
invisible to the 146 tests of the time: the realm names could never fade (the prefab bound `AlphaFactor`, which
a `TextWidget` never applies to its text), and switching "Show Realm Borders" off and on left the map blank. Five
regression tests were proven RED by restoring each defect and running the test, then GREEN after the fix. The
review also moved the repaint off the game thread, measured by the efficiency lens at about 37 ms per capture
with the Debug build TAOM ships.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `TaomRealmNames.xml` bound `AlphaFactor="@Alpha"` on both `TextWidget`s; v1.5.3 reads `Widget.AlphaFactor` only on the sprite path, and text alpha comes from the brush (`BrushRenderer.CreateTextMaterial`), so names popped in at full opacity | Engine UI contract | Bound by analogy with plain widgets; `RealmNamesVMTests` covered the view model, and no test read the prefab against what the renderer applies | Bind `Brush.GlobalAlphaFactor` (vanilla's own way, `SPChatLog.xml`); `RealmNamesPrefabTests` forbids `AlphaFactor` on a text widget; lesson in `localization-ui.md` |
| 2 | HIGH | Turning the master setting off called `ForgetDrawn`, which did not re-arm the repaint; on again, nothing redrew until an unrelated capture | State lifecycle | Five other callers set `_dirty` beside `ForgetDrawn`; the sixth, the disabled branch, did not, and the only test switched the setting off, never back on | `_dirty = true` lives inside `ForgetDrawn`, deleting the five pairs; `OnMapFrame_DisabledThenEnabled_RedrawsTheBordersAndNames`; lesson in `state-lifecycle-save.md` |
| 3 | MEDIUM | `ProvinceMap.ProvinceAt` checked bounds as `column < 0 \|\| ... \|\| row >= Rows`, false for NaN, so a NaN party position reached `(int)NaN` and an out-of-range index in the hourly listener; `ProvincePartitioner.TryStartCell` had the same shape, and infinite map bounds passed `maxX > minX` | NaN gate (engine float, float-to-int cast) | The rule in `csharp-architecture.md` was applied to decision gates, not recognised in an index bounds check written the conventional way | Positive gates in both, finite bounds via `FiniteFloatValidator`, a finite check in `TryGetMainPartyPosition`; four NaN/Infinity tests; lesson in `gamemodels-services.md` |
| 4 | MEDIUM | A fief held by a clan outside any kingdom drew as wild land: v1.5.3 raises a rebellion as a clan (`RebellionsCampaignBehavior`), and a leaving clan or an independent player keeps fiefs without a kingdom | Engine fact vs design assumption | The approved palette text assumed rebels found a kingdom; the adapter keyed realms by `OwnerClan.Kingdom` | Realms are keyed by `Settlement.MapFaction` (kingdom, or `clan:<id>`); lesson in `campaign-mechanics.md` |
| 5 | MEDIUM | The palette, a process singleton, removed each reserve colour it handed out, so a second campaign in one process inherited the first's assignments and drained the reserve | Singleton holding per-campaign state | The palette was treated as config; handing out colours in play made it campaign state | A palette per campaign (`NewPalette`, reset in `OnSessionStart`); `OnSessionStart_NextCampaign_HandsOutTheReserveAfresh`; lesson in `state-lifecycle-save.md` |
| 6 | MEDIUM | The partitioner's "no diagonal slip between two walls" rule had no test: deleting it left every test green | Test gap | Every wall in the tests was a straight column or a closed ring | `Partition_OneCellDiagonalBarrier_IsNotSlippedThrough` for walls and water |
| 7 | MEDIUM | Reserve colour `#CFA3E0` sat 9.2 dE from Rivendell's; the palette test gated only the curated colours | Data gate scope | The gate was written for the realms table, not for everything the file can put on the map | `#C878D8`; both palette gates run over curated and reserve colours; lesson in `testing-qa.md` |
| 8 | MEDIUM | The provenance row named only `BorderRenderAdapter`, while `BorderLook` carries the mod's own Heraldic defaults (0.3, 1.05) and the behavior checks its module id | Provenance | The row was written from memory of the renderer work; `BorderLook`'s own comment named the source | Row and detail name all three; the verbatim-port question is put to Mike; lesson in `build-tooling-workflow.md` |
| 9 | MEDIUM | The war map mode drew every war alike, while the approved design said "relative to the player, coloured with `NameplateRelationPalette`" | Spec drift | Built from memory of the proposal, not its text | Rebuilt to vanilla's nameplate relation rule and colours; a departures table in the feature doc; lesson in `build-tooling-workflow.md` |
| 10 | LOW | The M and G keys acted while the feature was off | Gate coverage | The gate sat in `OnMapFrame` only | Both keys check the setting; `Keys_WhileTheFeatureIsOff_DoNothing` |
| 11 | LOW | Neutral realms drew no line in the Free Peoples mode, undocumented | Documentation | The behaviour matched the approved "one front line" but nobody wrote it down | Documented; `AlignmentMode_NeutralRealm_StandsOnNeitherSideOfTheFront` |
| 12 | LOW | Every repaint (about 37 ms, 63 ms the first time) ran on the game thread, including repaints that changed nothing (mercenary contracts, war and peace outside the war mode) and relabelling the whole grid | Efficiency | Built for correctness first; nothing had been measured | Snapshot and skip-when-equal on the game thread, repaint and labels on the worker, labels reused when owners are unchanged; tests for the skip and for a repaint overtaken on the worker |
| 13 | LOW | Per-frame allocations (a formatted look signature, warning text built for good values, two delegates), a height cache cleared on every same-scene clear, alpha re-sent to every tile after each upload, tile hashes computed twice | Efficiency | Written for clarity | Each removed; the alpha change is pinned by `OnMapFrame_UploadAtASteadyCamera_DoesNotReapplyTheFade` |
| 14 | LOW | Names centred on the full screen, where `WorldToScreenInsideUsableArea` answers inside the usable area | Engine UI contract | The usable area was not considered | Centred on `Screen.RealScreenResolution * ScreenManager.UsableArea` |
| 15 | LOW | Dead code (`RealmPaletteConfig.ToPalette`, which also threw on one bad colour; `RealmNameItemVM.Realm`; a self-notification of an unbound property), two silent catches, comments that were wrong about the material packages and about what Kingdom Borders proved, dead `FontColor` on two brushes, an unprefixed brush file name, the Russian and Chinese Shadow terms, two stale co-op counts | Hygiene | Various | Each fixed |
| 16 | LOW | Coverage gaps: the map screen closing, both material outcomes, no scene, the draw-through switch, the hourly guards, session reset of visibility and the crossing memory, saddle and four-province tracer cases, the palette provider's file paths, the glyph table against `aniron.fnt`, terrain classes against the engine enum | Test gap | The first test pass followed the happy paths | One test each |
| R | (MEDIUM) | Refuted: "the Kingdom Borders module id was never read" | | The mod's `SubModule.xml` (`<Id value="KingdomBorders" />`) was read this session, and the engine lens read it too | None |

## Convergence pass

One reviewer on the applied fixes returned PASS for HIGH and MEDIUM: two repaints never share the quad
buffer, a stale or faulted repaint cannot leave the map blank past the next change, the adapter's engine
reads are null-safe, the static relation colours run no engine code on the hosted CI, and releasing the map
scene touches nothing already destroyed. Its four LOW findings were fixed test-first: a stale comment on the
strip's `V`; the palette parse tightened to exactly `#RRGGBB` (the shared parser would also take eight digits
as RRGGBBAA, so `#FFB0231B` written in the code's ARGB order would draw orange, and would pad a short byte
with whitespace); a faulted repaint now logs the whole exception, since off the game thread it no longer
reaches the crash report with its stack, and waits for the next change; a blank realm id gets its own warning.

## Root-cause patterns

**The test proved the model, not the thing the engine does with it (1, 14).** The view-model tests were
green while the prefab asked the renderer for something it never applies. Both UI defects were facts about how
Gauntlet renders, found only by reading the renderer. A prefab test that pins the attributes the renderer
reads is the cheapest guard, and it now exists for this feature.

**A reset that only half resets (2, 5).** Both are state that one path forgot: `ForgetDrawn` cleared the
tiles without asking for new ones, and the session reset cleared the drawing but not the palette's handed-out
colours. In each case the fix moved the invariant into the one place every caller goes through.

**The design text was not re-read (4, 9, and the departures now recorded).** The build followed the approved
design's shape from memory. Where the text and the engine disagreed (rebels) or the text was specific (war
colours), the build drifted silently.

## Why each lens caught or missed what it did

- **Standards** found the dead code, the unpinned session reset and the silent catches, and flagged the
  prefab-casing risk; engine behaviour is outside its checklist.
- **Engine compatibility** verified 62 calls and caught the `AlphaFactor` defect and the usable-area frame,
  but runs no campaign flows, so the rebel-clan fact fell to data flow.
- **Data flow** found the toggle defect by tracing the setting through both transitions, the NaN index, the
  rebel clans and the design departures.
- **XML** caught `AlphaFactor` independently of the engine lens, the reserve colour and the terminology.
- **Completeness** caught the provenance understatement and the untested diagonal rule; its "module id never
  read" claim was refuted with the session's own read of the file.
- **Efficiency** measured the real repaint cost by running the C# in a harness, which turned "move it off the
  thread if needed" into a number.
- **Design** found the per-process palette from the design side and shaped the realm key and relation rule.

## Lessons appended

- `lessons/localization-ui.md`: fade a `TextWidget` through `Brush.GlobalAlphaFactor`.
- `lessons/state-lifecycle-save.md`: the method that forgets drawn state re-arms the redraw; a config object
  that hands out values in play is per-campaign state.
- `lessons/campaign-mechanics.md`: a fief's political owner is `Settlement.MapFaction`.
- `lessons/gamemodels-services.md`: an index bounds check on an engine float is a NaN gate.
- `lessons/testing-qa.md`: a data gate covers everything the file can put on screen.
- `lessons/build-tooling-workflow.md`: re-read an approved design's text before building it, and grep the
  feature for a source's name before writing its provenance row.

No new feedback memory: each pattern has a standing rule or now a lesson; the NaN finding is a named category
of the existing rule, applied to a place it already covers.
