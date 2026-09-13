# RCA: the Supply Lines cross-market search deep review (#587, 2026-09-13)

**Top line.** Five review agents (standards, engine API, efficiency, completeness, cross-system
data flow) and then a Codex pass (GPT-6-Astra at ultra) reviewed the search box added to the supply order screen, the trade-goods widening and
the removal of the 14-row cap. No HIGH. One MED was confirmed by decompiling the installed
`ItemRoster` and fixed: the goods consume counted stock with an item-only lookup and deducted with
an element-equality lookup, and never read the deduction's return, so a modifier-carrying stack
would have been billed and delivered without leaving the settlement. It is unreachable on today's
data (every food and trade good in vanilla, the Armory and TAOM is typed Goods and none carries a
modifier group; checked file by file), which is exactly why the invariant now lives in code. Two
efficiency MEDs: one fixed (the get-only `IsSearchActive` folded the query on every binding read,
three per keystroke), one accepted with the reasoning recorded (the first-keystroke catalogue
build, estimated 15 to 30 ms, unverified, deliberately lazy so a player who never searches never
pays it). Three LOWs fixed: a status line whose count read as places when it counts listings, and
two untested transitions that now have tests. A design-time stress test (a Plan agent run before
implementation) had already removed four defects from the plan; those are listed at the end
because the review that catches a bug before it is written is the cheap one.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `ConsumeGoodsFromSettlement` computed `present` with `GetItemNumber(item)` (`FindIndexOfItem`, first stack of any modifier) and deducted with `AddToCounts(item, -take)`, which wraps `new EquipmentElement(item)` and finds only the UNMODIFIED stack, returning -1 and touching nothing when it is absent (`ItemRoster.cs:185-208`, installed 1.4.8). The return was never read; `result.Goods[id] = take` was recorded either way. | Lookup parity | Pre-existing port code. The port review verified "goods ARE deducted" (the source module never deducted at all) but not that the count and the deduction address the same stack. This session's plan stated "trade goods carry no modifier" as a data fact and stopped; the fact was true and enforced by nothing. | Count and deduct on the same element (`FindIndexOfElement` + `GetElementNumber`), check `AddToCounts >= 0` before recording, log and skip otherwise. Lesson in `campaign-mechanics.md`. |
| 2 | MED | `IsSearchActive => SupplyGoodsSearch.IsActive(_searchText)` folded the query (two `string.Normalize` passes) on every read; the prefab binds it three times, so each keystroke's notify cost three folds plus the one inside `Search`. | Per-keystroke cost | The per-keystroke path was reviewed for the large costs (catalogue build, row churn, sort) and the get-only property looked free. | Cached in `RefreshSearch`; the property returns a field. A first attempt at this fix derived the flag from `Search`'s result and would have kept the search active after a backspace below the floor; re-reading the diff caught it before the test run, and `SearchText_Null_TreatedAsEmpty` would have caught it after. |
| 3 | MED, UNVERIFIED | The catalogue is built on the first qualifying keystroke: one `GetGoods` per orderable settlement (~800 on TAOM_Map), each pricing every trade good through `Town.GetItemPrice` (villages use `item.Value`). The efficiency agent estimated 15 to 30 ms and could not measure it. | One-time stall | Not missed; a design decision. | Kept lazy: building at screen open would charge every player for a search most never run, and the screen already pays a full-map distance pass at open. Recorded as an in-game smoke item in the feature doc; if the hitch is visible, the mitigation is a name-only catalogue with pricing deferred to the hit rows. |
| 4 | LOW | "Found {COUNT}, nearest first." counts (source, good) pairs, so "gr" at a town stocking grain and grapes counts 2 and reads as two places. | Wording | The label was written from the engine's count semantics, not the player's reading. | "Matches: {COUNT}, nearest first." and "Matches: {COUNT}, showing the nearest {SHOWN}." Plural-neutral, so the 12 languages need no agreement. Updated in the C#, the registry and the 12 seeded rows together (the harvester is idempotent by design and would not have re-lifted the change). |
| 5 | LOW | `PopulateGoods` with a preferred id absent from the fresh `GetGoods` read silently shows no promoted row. Impossible while the campaign is frozen under the pushed state; untested. | Test coverage | Only the always-present case was tested. | `SearchHitSelect_PreferredGoodAbsentFromFreshList_NoPromotionNoThrow` pins the accepted degradation. |
| 6 | LOW | A hit picked, then a plain settlement row picked WITHOUT retyping: the hit highlight clears through `RefreshHitHighlights` inside `SelectSource`, but only the retype variant was tested. | Test coverage | Same. | `SourceRowSelect_AfterHitPick_ClearsTheHitHighlightWithoutRetyping`. |

## Root-cause pattern: a parity that lives in the data

Finding 1 is the one with a general shape. Two lookups on one roster, one keyed on the item and
one on the element, agree today because no trade good carries a modifier, and the comment in the
code said so. A comment is a claim about the data; the data can change (a modded good, a future
DLC item, an `ItemModifier` reaching a stack by any path) and nothing in the code would notice.
The fix is not to distrust the comment but to make the code true regardless of it: count and
mutate through the same lookup, and read the mutation's return. The same shape as
`csharp-architecture.md` "Lookup functions with fallbacks": a lookup that reports a plausible number
for the wrong stack is a fallback in disguise.

## Why each agent missed these

- **Standards** (haiku): all checks passed; findings 1 to 6 are behaviour, not convention.
- **Engine API** (sonnet): verified 23 signatures against the installed DLLs and raised the one
  thing in its scope, that `IsFood` is a data flag with no engine tie to `ItemType.Goods`; the
  orchestrator then scanned every item XML and both code-registered foods and found the widening a
  strict superset. It also recorded that `EditableTextWidget`, `ScrollbarWidget` and `Widget` live
  in `TaleWorlds.GauntletUI.BaseTypes`, which `taom-src` only finds by full-DLL fallback.
- **Efficiency** (haiku): found 2 and 3, the only two in its scope, by reading the per-keystroke
  path end to end and decompiling `Town.GetItemPrice`.
- **Completeness** (haiku): tests, doc, issue, localization, prose all present; it cannot see a
  wrong lookup.
- **Data flow** (sonnet): found 1 by decompiling `ItemRoster.AddToCounts` and `GetItemNumber` side
  by side, then checked the item XML itself rather than trusting the code comment. Found 4, 5 and
  6 by enumerating the selection state machine and the status-text semantics.

## Codex pass (GPT-6-Astra at ultra, review 106)

Dispatched against an isolated worktree (HEAD 0e371c6b plus exactly this change, 8836 green)
after the deep-review fixes. No P1. Nine known suspects, seven disputed with decompiled evidence
(initial wrapper visibility is pushed at `GauntletMovie.LoadMovie` through `GauntletView.RefreshBinding`;
the float, bool and int widget events are all subscribed by `GauntletView` and a boxed `Single`
reaches a `float` setter; the raw-notify story is right and vanilla's `EncyclopediaNavigatorVM`
does the same; the locks hold under the synchronous call order; `TextObject.ToString` resolves the
plural markup before the name is stored, checked with a managed probe; the LINQ sort is stable by
`EnumerableSorter.CompareKeys`'s index tiebreak; `MBObjectManager.GetObject` is a dictionary, not a
scan). Two confirmed, both fixed in the session:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 7 | P2 | `GoodsScrollValue = 0` resets the scrollbar VALUE but not the pane's wheel MOMENTUM: `ScrollablePanel.UpdateScrollablePanel` reads the zero (`:553-557`) and then adds `_verticalScrollVelocity` (`:588-598`), which a repopulate never clears while both lists overflow. A wheel notch followed at once by a hit pick coasts the pane off the promoted row again; Codex ran the update equations (residual velocity -12 at 1080p: 12 px after one frame, 115 px after decay, against a 150 px pane). | Engine state the binding cannot reach | The deep review's stress test found the OFFSET half (the Plan agent, A2) and the fix was written to the property the binding could see. Momentum is private state with one public reset, `ResetTweenSpeed()`, and nothing in the VM can call it. | The screen finds the pane by id after `LoadMovie` and hands the VM `ResetTweenSpeed` (`ResetGoodsScroll`, an `Action`, never a widget); `PopulateGoods` invokes it before the value reset. Two tests pin the order and the no-hook fallback. Lesson widened in `localization-ui.md`. |
| 8 | P3 (doc) | The feature doc claimed vanilla's inventory also closes on Escape while its search box has focus. `GauntletInventoryScreen.OnFrameTick` returns before any hotkey poll when `_gauntletLayer.IsFocusedOnInput()`, so vanilla does nothing on Escape there. | Attribution | The research report had quoted that very guard as "what vanilla does that TAOM's screen does not yet", and the doc sentence was written from memory of the conclusion, not the quote. | TAOM now takes vanilla's rule: `OnFrameTick` returns on `IsFocusedOnInput()`, so Escape cannot discard a pending order mid-typing. Doc corrected; smoke item reworded. |
| 9 | Observation | A modified stack of a trade good can exist through the console (`campaign.add_item_to_player_party grain \| lame_horse \| 2`, then a sale: `CampaignCheats` builds the element with no compatibility check and `InventoryLogic` transfers it whole). `GetGoods` listed every roster element, so such a stack appeared as a row the consume (unmodified element only, after finding 1) could not take: the order failed closed, not open. | Console-only state | Finding 1 fixed the deduction side; the listing side was left symmetric with the old code. | `GetGoods` skips any element with an `ItemModifier`, so the list only ever shows what the consume can take. |

Codex also corrected two small statements in the prompt (the binding-path type lives in
`TaleWorlds.Library`, and the cached `IsSearchActive` removes the per-binding-read folds, not the
one inside `Search`) and built and tested the worktree itself (8836 green).

## Design-time findings (Plan agent, before implementation)

Four defects were removed from the plan before a line was written, by a stress-test agent given the
design and asked to break it: a same-source hit pick would have wiped a pending order (the lock
now covers every hit); the goods scrollbar keeps its offset across a repopulate, so a promoted row
could sit above the viewport (a two-way `ValueFloat` binding reset to 0); `IsSearchActive` with a
public setter would have taken write-backs from both `IsHidden` and `IsVisible` bindings (get-only);
and routing hit locks through `Recompute` would have erased a confirm failure per keystroke (locks
at construction). Each of those would have survived the unit tests as written and shown up only in
the smoke.

## Lessons codified

- `docs/reviews/lessons/campaign-mechanics.md`: count and mutate a roster through the same lookup
  and read the mutation's return; a data-enforced parity is not enforced.
- `docs/reviews/lessons/localization-ui.md`: a VM property bound to `IsHidden` or `IsVisible` is
  get-only; a two-way text binding notifies the raw value; a `CoverChildren` button cannot hold a
  `StretchToParent` overlay; a `ScrollablePanel` keeps its offset AND its wheel momentum across a
  repopulate, and only its own `ResetTweenSpeed` clears the latter.

## Not findings

- Vanilla's inventory search filter compares a lower-cased name against an unlowered query
  (`SPInventoryVM.UpdateFilteredStatusOfItem`), so an upper-case keystroke never matches there. The
  encyclopedia's rule (diacritics stripped, `InvariantCultureIgnoreCase`) was copied instead.
- Per-keystroke `MBBindingList` clear-and-rebuild of up to 60 rows: the encyclopedia does the same
  with no cap (`EncyclopediaNavigatorVM.RefreshSearch`).
