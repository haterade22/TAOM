# RCA: Realm Borders parchment map deep review (#698 follow-up, 2026-10-01)

## Top line

The six-lens `/deep-review` of the parchment map at full zoom-out (uncommitted on bannerlord-1.5.x over
`6b00881b`) found no CRITICAL or HIGH finding, 6 MEDIUM and a tail of LOW. The most consequential sat beside
the new code rather than in it: the look session had made `vertex_color_blend_after_postfx_mat` the first
"Automatic" border material, and that material is `TwoSided`, so every border tile's second winding drew too and
every border and tint pixel blended twice (the 0.3 tint judged in game drew at about 0.51). Two more MEDIUMs were
the parchment map hiding what it exists to show: with two of the four MCM materials the late-pass sheet covered
the borders, and the only way to turn it off was the developer console. Mike approved the four behaviour changes
(one winding on a two-sided material with the tint's default at 0.5, every border material in the late pass, an
MCM switch, the tuning commands retired); the rest was fixed without asking. Nine regression tests were proven
RED by restoring each defect in turn and running the test, then GREEN with the fix back (every file's hash
checked after the run).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Automatic's first material carries `TwoSided` (`taom_debug_2026-10-01_08-36-01.log:262`); `IMesh.SetMaterial` turns that into no culling, and `SetTile` still added both windings, so each pixel blended twice | Engine material contract | Both windings came from the Kingdom Borders recipe, written for `vertex_color_mat`, which culls back faces (its log line has no `TwoSided`). Changing the default material in the look session changed that premise; the flags were logged but nobody read them against the winding | `NeedsSecondWinding(material.Flags)` decides per material; `NeedsSecondWinding_OnlyWhenTheMaterialCullsBackFaces`; the tint default moved to 0.5 to keep the judged look |
| 2 | MEDIUM | With MCM Border Material on `vertex_color_mat` or `vertex_color_lighting`, the late-pass sheet covered the borders and the tint | Setting interaction | The order was proven in game only for the late-pass material the session was using; the other dropdown entries were never walked | `BorderFlags()` puts every border material in the late pass; `BorderFlags_EveryBorderMaterialDrawsInTheLatePass_SoTheParchmentNeverCoversIt`; a checklist row per material |
| 3 | MEDIUM | No player switch for a full-screen overlay on by default: only the console turned it off, and "Show Realm Borders" off was the only other way out | Player control | Built as a look-session spike behind console switches; nobody re-asked how a player turns it off once it became the shipped default | MCM "Parchment Map at Full Zoom-Out", presentation in `CoopSettingsRelevance`; `OnMapFrame_ParchmentMapOff_BuildsNothing` and two more |
| 4 | MEDIUM | The first zoom-out stalled one frame for 203 to 599 ms (the game's own logs, all from the one-layer build), and the two-layer code added since built the ink by repeating the paper mesh's whole drape | Efficiency | Built for correctness while the material was unknown | The ink mesh is a copy of the paper mesh, so the second layer does not double the stall; the picture's load is timed on its own line; slicing waits for the first two-layer measurement, owed in game |
| 5 | MEDIUM | `RealmBordersCheats.cs` grew from 95 to 261 lines, over ADR-002's 150 | Standards | Console knobs were added one at a time during a live session, each small | The parchment commands moved to `RealmAtlasCheats.cs` (81 lines), the tuning knobs went, the fade parses through `DevConsoleArgs` |
| 6 | MEDIUM | The feature doc was false in places (Automatic's order, "every look control is in MCM", "no texture", tables and counts) and had no section for the parchment map | Doc drift | The look session changed code faster than the doc, and no doc pass ran before the review | The "Parchment map" section, with what the session learned, and every stale line fixed |
| 7 | LOW | `RealmBorderService.Alpha` could stay above 0 with the master switch off after a failed repaint, and the parchment map reads it as "the borders are showing" | New reader of old state | Its only old reader, the names layer, was covered by an empty label list; the parchment map became a second reader without that cover | `Alpha` is 0 while the feature is off; `Alpha_FeatureSwitchedOff_IsNothingWhateverWasDrawnLast` |
| 8 | LOW | The failed-build latch never reset in the process, a frame with no scene latched as a failure, an exception in `SetSheet` escaped the latch, and a rebuild kept the failure text | Singleton latch lifecycle | The session-reset rule was read as being about campaign data, not a renderer's latch | `OnMapScreenClosed`, the `IsAvailable` gate, a logged catch, the status reset; four tests |
| 9 | LOW | The borders' render order was set to 5, below the engine default 128 (`FUN_18005e690` writes `0x80` to the field `SetMeshRenderOrder` sets), changing their order against every default mesh | Engine default | Set while chasing the sheet's order, before the default was known | The borders keep the default; the sheet draws at 126 and 127; `SheetRenderOrders_InkUnderPaperUnderTheBordersEngineDefault` |
| 10 | LOW | `SetVectorArgument(1, 1, 0, 0)` overwrote the material's own vector, `Color2` (the unused stroke colour) was set, and the picture was preloaded before being marked always valid, the reverse of vanilla | Engine parity | Trial lines left after the session found the working setup | Dropped; vanilla's order (`TwoDimensionEngineResourceContext`); a comment that the colour precedes `SetAlpha`, which writes the same native float |
| 11 | LOW | Comments and the MCM hint misstated the engine: "brightness times the vertex colour", "white quads", Automatic as "the first that exists" | Comment drift | Comments written at guess time were not revisited after the game answered | Corrected against the decompile and the session's results |
| 12 | LOW | Two tests could not fail: they set `_built = false` themselves, doing the code's job | Test validity | The substitute's `RemoveSheet` modelled nothing | The substitute clears the flag itself; the tests assert `RemoveSheet` |
| 13 | LOW | Skip guards untested (switch off, borders hidden), `SetFade`'s tests fed NaN only to the start (the code refused both), the atlas service's registration and the view's calls untested, `UseRenderOrder` untested | Test gaps | Tests were written for the path the session exercised | Tests added; `UseRenderOrder` went with its knob |
| 14 | LOW | The art's record lived only on the desktop, said nothing was in the repo yet, and did not name the shipped derivation (result 3b, Lanczos to 2048, RGBA) | Provenance | Written before the art shipped | The `atlas_parchment` entry in `tools/realm_border_art/provenance.json`; the desktop record points at it |
| 15 | LOW | `CandidateMaterials` was dead, and the sheet's fallback list named two banner materials never tried | Dead code, untested claims | Left from the knob era | Deleted; one sheet material; `MaterialChoices` pinned literally |
| 16 | LOW | Console help typed constants by hand ("5", "0x20000200", the colours); `taom.realm_atlas_flags 0` claimed the normal pass while the ink layer kept the late pass; the texture slot was bounded only by the console, against a 16-entry native array | Console drift and safety | Hand-written while tuning | Those commands retired; the remaining help reads the constants |

**Not applied, with reasons:** slicing the build across frames (measure the copied ink mesh first); 4 by 32
tiles (unmeasured); an asynchronous picture load (unverified that it binds, and a once-per-process cost); a
smaller picture format or an RGB re-encode (follow-up once the look settles); Design's one-winding orientation
variant (it needs orientation logic and risks the lit material's normals); a comment on #698 (public, Mike's
word); a check of `AtlasSheet.TerrainSize` against TAOM_Map (optional; the art is authored for that square).

## Convergence pass

One reviewer on the applied fixes returned PASS for HIGH and MEDIUM, with five LOW findings, all fixed:

- A build that threw partway left the tiles it had added, and the next frame showed that patchwork at full
  opacity, because `HasSheet` was already true. The service now removes the sheet on any failed build, and
  the adapter refuses a sheet with a tile the engine gave no entity for, logged
  (`OnMapFrame_BuildThrowsPartway_RemovesWhatItBuiltAndShowsNothing`, proven RED first). The test the first
  pass wrote could not see it: its fake threw before building anything, the lesson this RCA appends.
- The doc and row 4 tied the measured 203 to 599 ms to the ink layer, but every logged build had one layer;
  the doc also read as if released builds had blended twice, and row 13 misdescribed `SetFade`. Corrected.
- The winding, late-pass and render-order tests guarded the helpers, not their call sites; a source test now
  pins the call sites and the two remaining `SetMeshRenderOrder` calls.
- Two in-game checks added: the two materials now in the late pass at mid zoom, and "Draw Borders Through
  Hills" off against the material's own flags.
- The "off in MCM" status line had no test; it has one.

## Root-cause patterns

**A look-session spike became the shipped feature without a ship pass** (findings 3, 5, 6, 10, 11, 15, 16).
The console knobs, the comments, the doc and the player's control all still reflected the session's open
questions after the session had answered them. When a look session settles, each knob becomes a constant or a
setting, every comment written at guess time is re-read against the answer, and the doc gets its section,
before the review.

**A recipe's premise changed under it** (findings 1, 2, 9). Two windings and the default render order were
right for `vertex_color_mat`; the session moved the default to a two-sided, late-pass material, and nothing keyed
to the old material was re-checked. Key such choices on the property that justifies them (the material's flags),
not on the material that happened to be in use.

**A new reader of old state** (finding 7): the parchment map read `Alpha`, whose invariant had only ever been
checked for the names layer.

## Why each lens caught or missed what it did

- **Standards** found the file size, the knobs widening the adapter, the latch, the missing wiring test, the
  hint and the desktop-only provenance. It did not look at material flags, which belong to the engine lens.
- **Engine compatibility** found the double blend by reading the logged flags against the native culling path,
  plus the render order, the vector argument, the colour and alpha order and the wrong comments. Unit tests
  could never see it: every renderer test fakes the adapter.
- **Efficiency** measured the stall from the game's logs and proposed the mesh copy; it left slicing to a
  measurement.
- **Completeness** found the doc errors, the missing switch and the two tests that could not fail.
- **Data flow** found the stale opacity, the sheet over the borders on two materials, and the dead list.
- **Design** proposed retiring the knobs, one sheet material, the switch and the late pass for every material;
  its one-winding variant was declined for the lighting risk.

## Lessons appended

- `lessons/adapters-taleworlds-api.md`: a winding choice keyed on a material's `TwoSided` flag, not on the
  material in use.
- `lessons/testing-qa.md`: a substitute must model the side effect a test relies on, or the test does the
  code's job.
- `lessons/build-tooling-workflow.md`: a look session's knobs get a ship pass before the review.
