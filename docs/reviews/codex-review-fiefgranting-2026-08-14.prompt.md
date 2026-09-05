Adversarial code review of a new TAOM (Bannerlord 1.4.8 total-conversion mod) feature. Repo root is the working directory. Be skeptical and specific: every finding must name a file, a line, and a concrete failure scenario. Say "no finding" rather than padding.

# What was built (issue #458)

Captured settlements were concentrating in one clan per kingdom. TAOM previously shipped NO code on this pipeline. The new feature subclasses vanilla's fief-grant election and swaps the instance in.

Read these files:

- `Main/Features/FiefGranting/FiefGrantPolicyService.cs`
- `Main/Features/FiefGranting/IFiefGrantPolicyService.cs`
- `Main/Features/FiefGranting/FiefGrantCandidateFacts.cs`
- `Main/Features/FiefGranting/FiefGrantSettingsProvider.cs`
- `Main/Features/FiefGranting/IFiefGrantSettingsProvider.cs`
- `Main/Features/FiefGranting/TaomSettlementClaimantDecision.cs`
- `Main/Features/FiefGranting/FiefGrantSaveableTypeDefiner.cs`
- `Main/Features/FiefGranting/FiefGrantingIoC.cs`
- `Main/Features/FiefGranting/Hooks/Patch70_FiefGrantDecisionSwap.cs`
- `TAOM.Tests/Features/FiefGranting/FiefGrantPolicyServiceTests.cs`
- `TAOM.Tests/Features/FiefGranting/Patch70FiefGrantDecisionSwapBindingTests.cs`
- `tools/apply_starting_fief_spread.py`
- The fief-grant block in `Main/Features/TaomSettings.cs` (search "FiefGrant")
- The one-line additions to `Main/IoC.cs` and `Main/SubModule.cs` (search "FiefGranting" / "Patch70")

Vanilla engine source for comparison is decompiled at `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\`. The relevant vanilla files are `TaleWorlds.CampaignSystem.Election\SettlementClaimantDecision.cs`, `KingdomElection.cs`, `KingdomDecision.cs`, `SettlementClaimantPreliminaryDecision.cs`, `TaleWorlds.CampaignSystem.CampaignBehaviors\SettlementClaimantCampaignBehavior.cs`, and `TaleWorlds.CampaignSystem\Kingdom.cs`. Installed DLLs are authoritative if you can reach them.

# Claims made by the implementer that you should try to REFUTE

1. **"Subclassing is safe because no engine path keys on `GetType()`."** The claim is that all consumers use `is` pattern matching, which matches subclasses, and the only `typeof(SettlementClaimantDecision)` is vanilla's `SaveableCampaignTypeDefiner`. Verify exhaustively across the whole decompile, including UI assemblies and `SandBox`/`StoryMode` modules. A single `GetType() ==` or a `Dictionary<Type,...>` keyed on the concrete type breaks the feature silently.

2. **"`IsEnforced` is set BEFORE `AddDecision` on the annexation path, so the swap can copy it."** Verify in `SettlementClaimantPreliminaryDecision.ApplyChosenOutcome`. If the ordering is the other way, the swap silently downgrades an enforced annexation.

3. **"Merit alone decides the winner, so `DetermineSupport` does not need touching."** The argument: every finalist self-votes at ~40x what it gives a rival, so all three reach `FullyPush`, tie at 3 points, and `MaxBy` (strictly-greater) keeps element 0 of a merit-sorted list. Check the arithmetic in `DetermineSupport` and `DetermineSupportOption` and the tally in `DetermineOfficialSupport`. **Is there a realistic case where the finalists do NOT tie** (for example a clan too poor to afford `FullyPush` at 100 influence, or `Supporter.SupportWeights` ordering making the points different from 1/2/3)? If so, the merit multiplier is diluted and the feature is weaker than claimed.

4. **"The replacement is safe because neither producer retains a reference."** Check both producers.

5. **"The save definer is required and base id 726900901 / localId 101 is free."** Cross-check against every other `SaveableTypeDefiner` in `Main/` and against vanilla's ids. A collision throws at `Module.Initialize`.

# Specific things to hunt for

- **The lazy `Policy` property in `TaomSettlementClaimantDecision`.** The object is reconstructed by the save system without a constructor. Is the lazy resolve correct in that path? Is there any call that can run before the IoC container exists? Is swallowing the resolve exception hiding a real misconfiguration forever (the field stays null and retries every call)?

- **`CountFortifications` excludes `Settlement` (the contested fief) but `CountKingdomFortifications` also excludes it.** Is that ratio coherent? Consider the case where the contested settlement is the kingdom's ONLY fortification (division by zero, or a share of 0/0).

- **`IsKingsVoteAllowed` is a property read by `KingdomElection.GetAiChoice`.** Confirm it is read at the right time and that returning false there does not break anything else (for example `HandleInfluenceCosts`, which separately recomputes the popular outcome, or any UI that assumes the king can always choose).

- **Merit multiplier polarity.** `CalculateMeritOfOutcome` returns a value that is DIVIDED by owned settlement value in vanilla. Confirm multiplying the result by an additional `1/(1+n*k)` does not double-count or invert anything, and that a returned 0 or negative would not scramble `OrderByDescending`.

- **`NarrowDownCandidates` calls `CalculateMeritOfOutcome` for every clan, and `ShouldBeCancelled` calls `NarrowDownCandidates` again.** Count how many times the override runs per election and whether `CountFortifications` iterating `clan.Settlements` per candidate is a real cost for a 22-clan kingdom on a daily tick.

- **Co-op gating.** The swap is skipped when `ICoopSessionProvider.ShouldDeferToHost`. Is skipping the RIGHT behavior, or does a client running vanilla scoring while the host runs TAOM scoring cause a desync worse than both running TAOM? Read `Main/Features/CoopInterop/CoopSessionPolicy.cs` for the truth table.

- **`tools/apply_starting_fief_spread.py`** writes to the LIVE game install at `Modules/TAOM_Map/ModuleData/settlements.xml`, which is NOT tracked by this repo. Check: BOM preservation, CRLF preservation, non-ASCII preservation (the file contains Tengwar-ish names with accents), idempotency, the regex's ability to partial-match a larger token, backup correctness, and whether `--apply` can corrupt the file on a partial write.

- **MCM range duplication.** The ranges live in BOTH `TaomSettings.cs` attributes and `FiefGrantSettingsProvider.cs` clamps. Verify they agree exactly, knob by knob. `.claude/rules/csharp-architecture.md` records a shipped bug where a JSON invariant and an MCM clamp drifted.

- **Defaults sanity.** With the shipped defaults, does the ruling clan actually stop hoarding, and does any single term dominate so hard that another is meaningless? Do the arithmetic for a concrete case: a tier-6 ruling clan holding 6 of 10 kingdom fortifications versus a landless tier-2 clan of the settlement's own culture, both non-capturers.

# Output

Group findings by severity: P1 (breaks or silently disables the feature, crashes, save corruption), P2 (wrong behavior in a realistic case), P3 (maintainability, style, test gaps). For each: file, line, what is wrong, the concrete scenario in which it bites, and the minimal fix. If a claim above survives your attempt to refute it, say so explicitly, because that is useful signal too.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
