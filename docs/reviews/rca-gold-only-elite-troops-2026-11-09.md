# RCA: gold-only elite troops (#1108, `/deep-review`, 2026-11-09)

## Top-line

The tier 1 review of the uncommitted #1108 change ran in two waves, then one Codex `ultra` pass. It found no CRITICAL, no HIGH and no engine incompatibility. The first wave (lenses 5 Data flow, 2 Engine compatibility and 7 XML) confirmed seven findings at MEDIUM or LOW, all in comments, docs, a manifest or a test. On the maintainer's word, lenses 1, 3, 4 and 6 then ran as a second wave and Codex reviewed the whole change. Codex found the one runtime defect, row 8: at cheat-scale gold the emissary could grant unpaid troops. All confirmed findings are fixed and the three design proposals are applied, in the same change.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `tools/ingame_checks/1108.toml` set `fixed_in` to `d0d95bf27`, the local HEAD, which no remote branch held. An amend or a rebase before the push would make every later session NOT REACHED with "predates fixed_in". Another session pushed that commit at 17:05 the same day, which ended the risk. | Repeat of a known lesson | `build-tooling-workflow.md` said "already on trunk", and the commit was on the local trunk. The rule did not name origin. `test_every_fixed_in_is_a_commit_in_this_clone` cannot see it, because a rewritten commit's old object stays in the clone. | Pinned to `e6edc5729`. `ingame-verdicts.md` step 4 and the lesson now say origin. No gate: a check against origin would fail offline. |
| 2 | LOW | The same manifest's `needs` said "your own faction's capital". Only 12 settlements host an emissary. | Unchecked generalisation | The text widened the issue's Minas Tirith step without a read of `elite_emissary_config.xml`. | Text names Minas Tirith. One-off. |
| 3 | MEDIUM | The comment in `SpecialResourceSettingsProvider.cs` and a sentence in `elite-emissary.md` said a bad rate comes from `taom-server-settings.json`. `ServerSettingsReader.cs:75` refuses that value first. | Claim without reading the producer | The plan stated the reason, and I copied it into the comment and the doc. | Corrected. Lesson in `misc.md`. |
| 4 | LOW | `special-resources.md` said a co-op mismatch "changes balances only". The gates read the client's value, and the authority charges by its own (`RecruitChargePolicy.cs:19`, `SpecialResourceEventAdapter.cs:174`). | Claim without reading the producer | The plan had not traced who charges. | Corrected. Same lesson. |
| 5 | LOW | `special-resources.md` said the option does not change the map bar. The bar's warning needs `HasUpkeepDue` (`SpecialResourceMapBarMixin.cs:119`), and its upkeep rows need upkeep lines. | Claim without reading the producer | The plan's "the map bar stays" meant the amount, and the doc dropped that limit. | Corrected. Same lesson. |
| 6 | LOW | `IEliteEmissaryService.cs` still described a resource-only purchase and omitted the buyer condition. The three DTO comments were updated. | Incomplete sweep | The sweep followed the plan's file list, and the interface was not on it. | Corrected. One-off, under `misc.md` "Sweep docs for the facts a change alters". |
| 7 | LOW | `Registration_GivesTheProviderTheSettings` registered a fake with `IfAlreadyRegistered.Replace`, so it stayed green with the feature's registration deleted. | Test that cannot fail | The test passed, and I never deleted the line it names. | Added `IsRegistered` before the replacement. Proved by mutation: red with the registration removed, green with it restored. Lesson in `testing-qa.md`. |
| 8 | MEDIUM | `EliteEmissaryService` held gold in a `float` for the offer and multiplied price by quantity in an `int`. With the rate at 50 and `int.MaxValue` gold, a price of 300 gave an offered maximum of 7,158,279, one too many, and a total that wrapped to -2,147,483,596. `Gold >= total` passed, the troops were granted, the charge was refused and `Purchase` returned `Success`. | Logic error | I saw the float in the first pass and recorded it as a display problem, because `Purchase` compares ints. I never multiplied the offered maximum by the price. No project sets `CheckForOverflowUnderflow`, and `Hero.ChangeHeroGold` allows `int.MaxValue`. | `PlayerBalance` is a `double`, and `Purchase` compares a `long` total. Four tests were RED first, including the round trip from the offered maximum. Lesson in `gamemodels-services.md`. |
| 9 | MEDIUM | The charged gold price had a test only at the default rate. | Test gap | The list-price test used 137, so the rate looked covered. | `Purchase_GoldOn_GrantsThenChargesGold_AndNoResource` uses rate 137 and asserts 8220. |
| 10 | LOW | Five comments were outside the lens 9a keep-list. | Convention inconsistency | I wrote design reasons into the code that the feature docs already hold. | Deleted or trimmed. `elite-emissary.md` and `special-resources.md` keep the reasons. One-off. |
| 11 | LOW | The class doc of `SpecialResourceGoldOnlyTests` claimed "every reader" and "each charge". | Unchecked generalisation | I wrote it before the test list was final. | Rewritten to what the tests do. One-off. |
| 12 | LOW | Three test names did not follow `MethodName_State_Expected`. | Convention inconsistency | The names described the feature, not the method. | Renamed. One-off. |
| 13 | LOW | The refused-charge test did not assert the returned status, and the one-gold-short test did not assert the name. | Test gap | Both tests stopped at the effect they were written for. | Assertions added. One-off. |
| 14 | LOW | `configs-balance.md` and `file-catalogue.md` said no MCM group touches the cost file, `special-resources.md` Key Files omitted the interface, and three line citations moved. | Incomplete sweep | Same cause as row 6. | Corrected. Covered by `misc.md` "Sweep docs for the facts a change alters". |
| 15 | LOW | `HideEmissaryWhenNoResource` now also hides the option from a buyer of another resource, and the manifest's claim held only at that setting's default. | Incomplete sweep | I traced the gate, and not the setting that reads its result. | `elite-emissary.md` states it and the manifest names the setting. One-off. |
| 16 | LOW | The convergence pass found four small items in the fix pass: this RCA still named `CareerPurseAdapter` after the adapter swap, `file-catalogue.md:315` held a value outside its column's legend, an edit lost the space in `var costs = _settings`, and the class doc of `SpecialResourceSettingsProvider` kept a rationale sentence outside the keep-list. | Incomplete sweep | I swept the code and the feature docs for the swap and did not re-read this RCA. I filled the column from the sibling table in `configs-balance.md`. | All four corrected. One-off. |

## Root-cause pattern: a plan's summary written down as fact

Rows 3, 4 and 5 share one cause. The plan carried a short reason or effect, and the comment and docs repeated it without a read of the code that feeds the value or writes the effect. AGENTS.md "Evidence, never invention" already covers this, and the plan looked like evidence because the maintainer had approved it. Approval covers the design. It does not verify each sentence.

## Why each agent missed these

- **Lens 7 XML** found rows 1 and 2. Rows 3 to 7 are outside its files.
- **Lens 5 Data flow** found rows 3, 6 and 7. It traced `1108.toml` only for its patterns and keys, so it missed rows 1 and 2. Its brief named the feature docs as intent, so it did not audit their sentences for rows 4 and 5.
- **Lens 2 Engine compatibility** found rows 3, 4 and 5. Rows 1, 2, 6 and 7 are outside its rule set.
- **No first-wave lens reported row 8.** This RCA's first version recorded the float as a display issue and said no overspend was possible, which was wrong.
- **Lens 1 Standards** found rows 10 to 13. **Lens 4 Completeness** found rows 9, 11, 13, 14 and 15. The tier skipped both in the first wave. **Lens 3 Efficiency** found nothing in this change.
- **Codex** found row 8 from Known Suspect S5, with an in-memory float32 calculation, and also named row 13. It reported none of rows 9 to 12, 14 or 15. It confirmed the seven first-wave fixes in the source.
- **The convergence pass** found row 16 in the fix pass's own edits, and no runtime or parity defect.

## Improvements applied (Step 4)

- **P1 (lenses 6 and 1):** `EliteEmissaryService` reads the buyer and the purse through the existing `IArmouryPlayerAdapter`. `GetMainHeroFaction` is gone, and both adapter files match the base commit. The hero reads and the charge code are the same.
- **P2 (lens 6):** `EmissaryTroopOffer.MerchantCost` is now `UnitPrice`, because the value is a gold price under the option.
- **P3 (lens 6):** `EmissaryPurchaseResult.ResourceDisplayName` documents when it is null.

## Proposals not applied

- **A required settings parameter on the config provider (lens 1).** The approved plan chose the optional form, lens 6 agrees, and `Registration_GivesTheProviderTheSettings` pins the registration.
- **Nullable annotations (lens 1).** The compiler reports no warning on those lines. The one warning, CS8619 in `PlayerPartyAdapter.cs`, left with P1.
- **New wording for the two hints (lenses 1 and 4, optional).** The present text is accurate, and `elite-emissary.md` carries the exception.

## Found outside the review

`/verify`'s full C# suite failed five tests before this review: `SettingsFingerprintTests` and `ServerSettingsSchemaTests` pin the settings counts, and two new settings moved them. My filtered runs named only the feature's classes. That repeats `testing-qa.md` "A filtered test run while iterating names the feature's classes". The pins and the two docs that quote them are updated.

## Not fixed

- **`taom_emissary_denars` and the four MCM rows are English in the 12 language files.** This is the `/localize` seed state, not a defect. The paid translator pass runs on the maintainer's word, and the keys go on backlog issue #763 after the commit.
- **A refused charge after the grant still returns `Success`.** Row 8 removes the one known way to reach it: `Purchase` checks the purse just before the grant, and `GrantTroop` only adds to the roster. The troops are in the party, so `Success` describes the result, and an ERROR line and a FAIL outcome record the anomaly. A rollback needs a new roster-removal path for a state nothing reaches (simplicity criterion: reject).
- **Codex O1, a quote that goes stale.** `BuildOfferList` and `Purchase` read the option separately, and `Purchase` does not repeat the buyer check. A difference needs an MCM change or a conquest during an open modal inquiry, and Codex marked that sequence UNVERIFIED. A guard for a speculative case needs the maintainer's word (simplicity criterion).
- **The emissary grants past the party limit.** This is older behaviour, and resource prices kept the quantity small. Under gold-only, a rich hero can buy more at once.
- **Codex: the over-limit inquiry calls `OnDone` directly.** No action: the `RecruitmentVM_ExecuteDone_Patch` prefix runs before that inquiry opens.
- **Vanilla's gold line appears beside TAOM's result message,** because `ArmouryPlayerAdapter` passes no `disableNotification`. The adapter predates this change, and the extra line is accurate.
- FOLLOW-UP: `Main/Features/TaomSettings.cs:51-52` and `docs/features/ai-party-size.md:191-193` say MCM sorts top-level groups by `GroupOrder` descending, and lens 2 read an ascending sort in the installed MCM DLL. Both predate this change. no issue: creating a public issue needs the maintainer's word, and the lens could not date the claim against the older DLL.
- FOLLOW-UP: Codex reports from the installed co-op `GameInterface.dll` that `PartyScreenLogicPatches.DoneLogicPrefix` calls `Reset(fromCancel: true)`, so TAOM's `OnPartyScreenReset` (`SpecialResourcesBehavior.cs:265-269`, read) cancels the pending upgrade spend, and the authority does not replay it. Co-op upgrades may then charge no resource, with the option off too. I did not re-read the DLL. no issue: creating a public issue needs the maintainer's word.
- FOLLOW-UP: `SpecialResourceService.cs:34-35` builds a string key only to dedupe a debug log (lens 3, older code). no issue: creating a public issue needs the maintainer's word.

## Not run

- **A second Codex pass on the fixes.** The maintainer asked for one pass. Row 8 is MEDIUM in plain service arithmetic, outside the tier 1 list, so its tests and `/verify` prove it. One `deep-reviewer` convergence pass covered the fixes and the applied proposals.
- **In game:** the claims in `tools/ingame_checks/1108.toml` are owed. No session has run a clean build of this change.
