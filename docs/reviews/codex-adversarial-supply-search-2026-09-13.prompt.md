# Codex Adversarial Review -- Supply Lines cross-market goods search (#587), 2026-09-13

You are an independent adversarial reviewer. A 5-agent Claude deep review already ran on this changeset and every confirmed finding was fixed (see docs/reviews/rca-supply-search-2026-09-13.md in the worktree); your job is to find what those agents and their fixes missed. Verify every claim you make against the INSTALLED Bannerlord v1.4.8 DLLs with ilspycmd, never against memory and never against the E:\Decompiled_Bannerlord dump alone.

## Where the code is

Repo root (rules, AGENTS.md, docs): E:\repos\TAOM

The changeset under review is the working tree at:
C:\Users\mikew\AppData\Local\Temp\claude\e--repos-TAOM\ef5323a7-8af3-44fd-85cb-3bd363d1d2f6\scratchpad\wt-supply
That worktree is HEAD (0e371c6b) plus exactly this change and nothing else (26 files: 4 new, 22 modified). READ THE SOURCE FILES THERE. Run dotnet build and dotnet test ONLY there, with: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (the shared tree E:\repos\TAOM carries other sessions' uncommitted edits and must not be built or edited). Current result there: 8836 passed, 0 failed, 2 skipped.

Installed engine DLLs: E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/ (TaleWorlds.GauntletUI.dll, TaleWorlds.GauntletUI.Data.dll, TaleWorlds.Engine.GauntletUI.dll, TaleWorlds.Library.dll, TaleWorlds.Core.dll, TaleWorlds.CampaignSystem.dll, TaleWorlds.ScreenSystem.dll). Decompile a type with: ilspycmd -t TaleWorlds.GauntletUI.BaseTypes.EditableTextWidget "<dll path>". Note the Gauntlet base widgets (EditableTextWidget, ScrollbarWidget, ScrollablePanel, Widget, ListPanel) live in namespace TaleWorlds.GauntletUI.BaseTypes; the binding layer (GauntletView, BindingPath) lives in TaleWorlds.GauntletUI.Data.

## Feature in one paragraph

The Supply Lines order screen (a full-screen Gauntlet ScreenBase pushed as a GameState; campaign time is frozen while it is up) lets the player pick ONE source settlement or friendly lord in a left column and order goods and volunteers from it. This change adds a live search box above the source column: vanilla's EditableTextWidget bound Text="@SearchText" writes every keystroke into the VM's public setter; the VM folds the query once (trim + Helpers.StringHelpers.RemoveDiacritics), builds a catalogue ONCE per screen open from the orderable settlement rows (CanOrder true after the row's own NaN/MaxValue sanitising, not a lord) by calling the existing ISupplySourceService.GetGoods per row, and runs the pure static SupplyGoodsSearch.Search over it: case-insensitive substring on the pre-folded name, nearest first then cheapest then name, stable, capped at 60 with the total reported in a status line. Two characters minimum, one when the first character is CJK. While a query qualifies (IsSearchActive, get-only, notified with OnPropertyChanged) the settlement wrapper is IsHidden and a sibling hits wrapper is IsVisible, both bound to the same flag; each hit row is "{ITEM} at {SOURCE}" plus stock/price/distance and clicking it runs the ordinary source selection with the good promoted to Goods[0] and the goods scrollbar reset through a two-way ValueFloat="@GoodsScrollValue" binding. A pending quantity locks EVERY hit (a hit from the selected source would repopulate the goods and wipe the order). Typing never runs Recompute. Separately, GetGoods now lists every ItemObject.IsTradeGood item (was IsFood only) with no row cap (was the 14 priciest), and the consume path was hardened to count and deduct on the same unmodified EquipmentElement and to check AddToCounts's return before recording the take.

## TAOM ID CHEATSHEET (for any config cross-reference you do)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". This feature has no culture-keyed config; the cheatsheet is here for completeness.

## READ FIRST (all under the worktree root)

- docs/features/supply-lines.md (rewritten for this change; the "Cross-market search (#587)" and "Every trade good, uncapped (#587)" bullets and the Traps section state the invariants this review should try to break)
- docs/reviews/rca-supply-search-2026-09-13.md (what the deep review found and fixed; do not re-report those)
- .claude/rules/gui-ui.md (ViewModel binding rules, brush/sprite rules) and .claude/rules/csharp-architecture.md (Engine-Float Decision Gates, Lookup Functions With Fallbacks)
- GitHub issue #587 body (gh issue view 587)

## KNOWN SUSPECTS -- confirm or dispute each with decompiled evidence

1. Initial visibility of the two toggled wrappers. The prefab binds IsHidden="@IsSearchActive" on the settlement wrapper and IsVisible="@IsSearchActive" on the hits wrapper and on the status text; the VM's IsSearchActive is false at load. Suspect: if the binding layer only pushes VM values on CHANGE notifications and not at movie load, the hits wrapper keeps Widget's default (visible), BOTH StretchToParent wrappers are visible at once, and the vertical StackLayout splits the column between them until the first qualifying keystroke. Decompile GauntletView.RefreshBinding / BindingPath / the DataSource attach path in TaleWorlds.GauntletUI.Data and state whether initial values are pushed into IsHidden/IsVisible at load. If they are not, the fix is a VM-side notify at construction or a prefab default; say which.

2. The widget-to-VM write path and its boxed types. Claim under test: EditableTextWidget.Text's setter fires OnPropertyChanged(string, "Text"), GauntletView.OnViewPropertyChanged maps the widget property to the bound VM path and calls ViewModel.SetPropertyValue(name, value), which invokes the PUBLIC setter via reflection. For ScrollbarWidget.ValueFloat the widget fires OnPropertyChanged(float, "ValueFloat"). Suspect: (a) the float overload of PropertyOwnerObject.OnPropertyChanged raises a DIFFERENT event (floatPropertyChanged) than the generic one, so confirm GauntletView subscribes to the float/bool/int variants too and that the boxed float reaches a float setter without an ArgumentException from MethodBase.Invoke; (b) confirm the re-entrancy story the VM comment relies on: EditableTextWidget.UpdateRealAndVisibleText sets a guard field while assigning Text, so a VM that echoed a TRIMMED value would leave RealText and VisibleText out of sync, while echoing the raw value is a no-op because the Text setter guards on VisibleText != value. Quote the lines.

3. Hotkeys while the text box has focus. GauntletSupplyOrderScreen registers GenericPanelGameKeyCategory and GenericCampaignPanelsGameKeyCategory on the layer and polls only "Exit" in OnFrameTick. Suspect: (a) the ENGINE side (GauntletLayer.Update, the focused EditableTextWidget.HandleInput, InputContext hotkey evaluation) may consume or act on letter keys or Space/Enter/Tab typed into the box in a way the screen does not expect; enumerate the keys in both categories on 1.4.8 and say which, if any, fire an engine action while typing; (b) confirm whether Escape reaches IsHotKeyReleased("Exit") while the widget is focused (the feature doc claims Escape closes the whole screen while typing, vanilla-inventory-style) or whether the widget swallows it; (c) after a GameState deactivate/reactivate (an inquiry popup over the screen), does the widget keep focus and does ScreenManager.TrySetFocus in OnActivate change anything for it.

4. Goods scrollbar reset ordering. PopulateGoods clears the MBBindingList, sets GoodsScrollValue = 0f (notify), then adds rows. Suspect: ScrollbarWidget.ValueFloat's setter early-returns when Locked (line 19830 in the dump: if (Locked || !(Abs(_valueFloat - value) > 1E-05f)) return;). Establish when a ScrollablePanel locks its scrollbar (no overflow?) and whether this sequence can leave the WIDGET at a stale non-zero offset while the VM holds 0, so the NEXT reset (0 to 0, no change) never fires and a promoted row sits above the viewport. Also check what ScrollablePanel.OnLateUpdate does to ValueFloat when the new content is shorter than the old offset (re-clamp vs reset) and whether the two-way write-back of that clamp keeps the VM in step.

5. The consume hardening. SupplySourceService.ConsumeGoodsFromSettlement now does FindIndexOfElement(new EquipmentElement(item)) + GetElementNumber for present, deducts with AddToCounts(element, -take) and records only when the return is >= 0. Suspect: (a) EquipmentElement's default constructor sets IsQuestItem/CosmeticItem fields that IsEqualTo does NOT compare, so this is fine, but confirm IsEqualTo compares exactly Item and ItemModifier on 1.4.8; (b) find ANY vanilla path that adds a Type=Goods item to a SETTLEMENT roster with a non-null ItemModifier (workshop output through GetRandomItemAux uses ItemComponent.ItemModifierGroup, which TradeItemComponent never sets; check village production, caravan trades, quest rewards, loot distribution, and the SandBox "sell goods" flows) -- if one exists, the UI (GetGoods lists every roster ELEMENT, so a modified stack shows as its own row under the same item id) and the consume (dictionary keyed by item id, unmodified stack only) disagree and the order would fail closed with "nothing taken"; (c) the goods dictionary the VM builds on confirm is keyed by ItemId, so two roster rows with the same id (unmodified + modified) collapse to one entry with the LAST row's Qty: reachable or not, given (b)?

6. First-keystroke catalogue build. EnsureCatalogue calls GetGoods for every orderable settlement row (about 800 on TAOM_Map: 221 towns and castles through Town.GetItemPrice per trade good, 616 villages through item.Value). Suspect: Settlement.Find(id) and MBObjectManager.GetObject are dictionary lookups, but confirm; then estimate the build from decompiled TradeItemPriceFactorModel.GetPrice / TownMarketData.GetPrice cost and say whether a visible hitch is plausible. The design keeps it lazy on purpose (a player who never searches never pays); dispute that only if the numbers say a screen-open build is clearly better.

7. The one-source lock and the hit rows. Recompute locks settlement rows with CanClear && row != selected and hits with CanClear; RefreshSearch constructs hits with locked = CanClear. Suspect: a path where a hit is ENABLED while a quantity is pending or LOCKED after ExecuteClear, or where a hit can be clicked between _goods.Clear() and Recompute inside SelectSource (single-threaded VM, so argue from call order). Also: OnHitSelected guards row.CanOrder but the catalogue already excludes !CanOrder rows; is the guard dead, and is dead-but-defensive acceptable here per simplicity-criterion?

8. Localization and text. Six new {=taom_sl_*} keys: taom_sl_search_placeholder, taom_sl_search_none, taom_sl_search_count ("Matches: {COUNT}, nearest first."), taom_sl_search_capped ("Matches: {COUNT}, showing the nearest {SHOWN}. Narrow the search."), taom_sl_hit_row ("{ITEM} at {SOURCE}"), taom_sl_hit_detail ("{STOCK} in stock, {PRICE} denars, {DISTANCE} away"). Suspect: (a) an item name containing TextObject markup (vanilla names are "Grain{@Plural}loads of grain{\@}"): GetGoods stores item.Name?.ToString(), so what does the stored string contain, and does SetTextVariable("ITEM", thatString) re-parse braces if any survive; (b) the search folds the stored Name, so a name whose ToString still carried markup would be searchable by its markup; (c) EditableTextWidget rejects < and > on type and strips them on paste; anything else in a query that could reach TextObject unescaped? (The query itself is never put into a TextObject; confirm.)

9. Engine-float gates. SupplyGoodsCatalogueEntry.Distance comes from SupplySourceRowVM.Distance (sanitised finite non-negative) and only CanOrder rows are catalogued. Confirm no NaN/MaxValue can reach SupplyGoodsSearch's OrderBy or the hit's DistanceText, and that the sort is stable (LINQ OrderBy/ThenBy) so equal keys keep catalogue order across rebuilds.

Known and OUT OF SCOPE (do not report): the paid 12-language translation run for the six keys rides backlog #508 (English fallback rows are seeded); Escape closing the screen while typing is documented behaviour (report only if the CLAIM is wrong); the CHANGELOG entry is written at commit time because CHANGELOG.md is mid-edit by another session; the catalogue is deliberately lazy (see suspect 6); the untranslated-to-CJK single-character floor is vanilla parity.

## FILES (all under the worktree root)

Feature (new):
Main/Features/SupplyLines/SupplyGoodsSearch.cs
Main/Features/SupplyLines/UI/SupplySearchHitRowVM.cs

Feature (modified):
Main/Features/SupplyLines/UI/SupplyOrderScreenVM.cs
Main/Features/SupplyLines/SupplySourceService.cs
Main/Features/SupplyLines/ISupplySourceService.cs

Unchanged but load-bearing (read them):
Main/Features/SupplyLines/UI/GauntletSupplyOrderScreen.cs
Main/Features/SupplyLines/UI/SupplySourceRowVM.cs
Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs
Main/Features/SupplyLines/SupplyOrderService.cs (TryPlaceOrder: consume, spawn, charge; delivery capped by the order)

Prefab:
Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml (the left column: search row, status line, two toggled wrappers, hit rows; the goods scrollbar ValueFloat binding)
Main/_Module/GUI/Brushes/Encyclopedia.xml (Encyclopedia.Search.TextBox, the text box brush)

Localization:
Main/_Module/ModuleData/taom_module_strings.xml (six new rows)
Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml (six seeded English rows each)

Tests:
TAOM.Tests/Features/SupplyLines/SupplyGoodsSearchTests.cs (new, 34)
TAOM.Tests/Features/SupplyLines/SupplyOrderScreenVMTests.cs (30 new tests under "cross-market search (#587)")
TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs (new collection + VM type registered)

Docs:
docs/features/supply-lines.md
docs/reviews/rca-supply-search-2026-09-13.md
docs/reviews/lessons/campaign-mechanics.md, docs/reviews/lessons/localization-ui.md (one appended entry each)

## REQUIRED SECTIONS in your output

### VANILLA CODE
Paste the decompiled bodies (from the installed DLLs) you relied on for suspects 1 to 5: GauntletView's binding refresh and OnViewPropertyChanged, ViewModel.SetPropertyValue, EditableTextWidget.Text setter and UpdateRealAndVisibleText, ScrollbarWidget.ValueFloat setter, the ScrollablePanel update that reads it, EquipmentElement.IsEqualTo, ItemRoster.AddToCounts(EquipmentElement,int) and FindIndexOfElement, and the workshop output path (WorkshopsCampaignBehavior.GetRandomItemAux). Code blocks, with the type and line numbers.

### KNOWN SUSPECTS
One verdict per suspect: CONFIRMED (with the failing scenario and the minimal fix) or DISPUTED (with the evidence). No "probably".

### FEATURE-SPECIFIC DEEP ANALYSIS
Walk these scenarios against the code as written:
(a) Open the screen in a town, type "gr", pick the second hit, set quantity 2, press the "x", press Confirm. State every VM field and every widget-visible state at each step.
(b) Type "gr", set a quantity on the auto-selected source's grain WITHOUT picking a hit, then type "a". What is enabled, what is highlighted, what does the status line say.
(c) A settlement whose roster holds 0 of an item after another order consumed it in the same session (cannot happen, time is frozen -- confirm by reading how GameStateManager.PushState interacts with Campaign tick; if you find a tick that CAN run under this state, that is a finding).
(d) MCM toggle EnableSupplyLines flipped off while the screen is open and a search is active.

### CONFIG CROSS-REFERENCE
The six string ids: registered, seeded in 12 files, referenced from C#, placeholder names match SetTextVariable names exactly. Any key in taom_module_strings.xml under the taom_sl_ prefix referenced by nothing.

### FINDINGS OR OBSERVATIONS
Severity P1 (ships broken), P2 (wrong in a reachable case), P3 (quality). For each: file:line in the worktree, the failing input, the proving decompiled line, and the minimal fix. If you find nothing at a severity, say so explicitly.

## QUALITY GATES
- Every claim about engine behaviour cites a decompiled line from the installed 1.4.8 DLL.
- Do not report vanilla-matching behaviour as a bug; say "vanilla does the same" and move on.
- Do not report doc staleness for anything the READ FIRST docs already describe as owed.
- Run the test suite in the worktree once before you finish and report the count.

## Prior review lessons
SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; decompiling engine consumers caught missing gates and cross-entity propagation; lifecycle tracing caught stale caches; on #560 the pass found that the base class of a VM was the one the mission constructs, not the SandBox subclass.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped the hard sections and padded the easy ones; Codex reported a signature from the dump that the installed DLL had changed.

Output to: docs/reviews/raw/codex-adversarial-supply-search-2026-09-13.md (the dispatcher redirects your stdout there; just write the review).
