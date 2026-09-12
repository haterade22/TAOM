# Adversarial review: FiefGranting #565, siege participation record

You are reviewing an UNCOMMITTED changeset in this working tree. Review only the files listed below. The tree also carries an unrelated uncommitted BanditManagement change (Patch86, hideout boss fight, party templates) from another session: ignore it entirely.

Feature: TAOM's fief-grant election (#458, `Main/Features/FiefGranting/`) multiplies vanilla's `SettlementClaimantDecision.CalculateMeritOfOutcome` by TAOM terms. Until now its "capturer" term read `Town.LastCapturedBy`, one sticky clan stamp, so players won fiefs from sieges they never joined. #565 replaces it with a per-clan record of the winning assault (share of `MapEventParty.ContributionToBattle`), a share-scaled bonus, a new Absent From Siege Factor, and renames the player exemption knob so its flipped default reaches existing installs. Issue: https://github.com/haterade22/TAOM/issues/565

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur". This feature carries no ID data; the cheatsheet is for orientation only.

READ FIRST:
- docs/features/fief-granting.md (the design and every claim below)
- docs/reviews/rca-fiefgranting-2026-08-14.md (the first review of this feature; C2 refuted "merit decides outright")
- .claude/rules/csharp-architecture.md sections "Config Providers MUST Validate", "Singleton Services Holding Per-Campaign State", "Engine-Float Decision Gates"
- docs/reviews/lessons/campaign-mechanics.md and docs/reviews/lessons/state-lifecycle-save.md (search "persisted MCM default", "session-reset story", "Same shape as the sibling")

KNOWN SUSPECTS. Confirm or dispute each with code and a concrete reachable scenario. A claim you cannot break is reported as SURVIVED with the evidence you used.

1. Record-before-read ordering. Claim: `CampaignEventDispatcher.Instance.OnMapEventEnded` (MapEvent.FinalizeEventAux, v1.4.8 MapEvent.cs:2079) is dispatched before every `SiegeCompleted` dispatch (:2093, :2115), and `KingdomManager.SiegeCompleted` is the only caller of `ChangeOwnerOfSettlementAction.ApplyBySiege` in TaleWorlds.CampaignSystem, SandBox and StoryMode. Attack: find any capture path that changes ownership BySiege without a same-finalize MapEventEnded on the capturing side (starvation, blockade, a naval path, a StoryMode scripted capture), or any listener order that lets the vanilla `SettlementClaimantCampaignBehavior` create the decision before the record exists.
2. Capture gate. Claim: `FiefGrantingCampaignBehavior.SideThatCapturesOnVictory` mirrors `KingdomManager.SiegeCompleted`'s battle-type gate (KingdomManager.cs:238-240: Siege with attacker victory; SallyOut and BlockadeSallyOutBattle with defender victory), and deliberately excludes SiegeOutside, which captures nothing. The first cut mirrored the loot handler and wrote a phantom record on a SiegeOutside win that `KingdomManager.RelinquishSettlementOwnership`'s election could read. Attack: is there a battle type in the gate that does NOT transfer ownership, or one outside the gate that DOES (check the isWin argument and `KingdomManager.SiegeCompleted`'s early return for each type)?
3. Keep and forget. Claim: `OnSettlementOwnerChanged` keeps the record on BySiege and forgets it on every other `ChangeOwnerOfSettlementDetail`, so the grant itself (ApplyByKingDecision), barter, gift, rebellion, clan destruction and faction leave all clear it, and a stale record can never reach an election. Attack: walk all three producers of `new SettlementClaimantDecision(` (the daily tick, `SettlementClaimantPreliminaryDecision.ApplyChosenOutcome`, `KingdomManager.RelinquishSettlementOwnership`) and name any moment a record from an earlier assault is still present when `CalculateMeritOfOutcome` runs. Include the player's own kingdom, where the decision waits up to 48 hours in `_unresolvedDecisions` and a second assault may land in that window.
4. Eligibility mirror. Claim: skipping clans with `Clan.IsUnderMercenaryService` at record time mirrors the only record-time-knowable filter of `SettlementClaimantDecision.DetermineInitialCandidates` (`ClanToExclude`, mercenary service, `IsEliminated`, `Leader.IsDead`); the other three are per-decision or later-life states. Attack: name a winning-side party whose clan is recorded but can never be a candidate (a different kingdom's party on the same side? a minor faction? a rebel clan? the player's clan while it is a mercenary?), or a candidate clan whose parties are NOT recorded (companion-led parties, caravans in a siege, a clan whose `ActualClan` is null). Read `MobileParty.ActualClan` and `MobileParty.Owner` (the fallback; `PartyBase.Owner` is deliberately never called because it throws for settlement parties and an IL test bans it).
5. No-record parity. Claim: with no record for the settlement both participation terms stay out of the multiplier, so the ranking is unchanged rather than uniformly rescaled, and the absolute merit magnitudes vanilla's `KingdomDecision.DetermineSupportOption` compares against the 20/60/100 influence thresholds (SettlementClaimantDecision overrides `GetInfluenceCostOfSupportInternal`) are untouched. Attack: with a record present, the Absent From Siege Factor (default 0.5) halves absent clans' merit; show what that does to `DetermineSupportOption`'s `num6` for a non-finalist supporter and whether it can flip a vote tier in a way the docs do not disclose.
6. MCM rename. Claim: renaming `FiefGrantApplyPenaltiesToPlayerClan` (default false = exempt) to `FiefGrantExemptPlayerClanFromPenalties` (default false = scored like any clan) reaches existing installs because MCM's settings loader ignores an orphaned key in TAOM.json, and the provider inverts it into the policy's unchanged `ApplyPenaltiesToPlayerClan`. Attack: read the MCM v5 AttributeGlobalSettings persistence path if you can reach it (Bannerlord.MBOptionScreen / MCM assemblies under the game Modules folder), confirm an orphaned key is ignored and a missing key takes the compiled default, and confirm the co-op `SettingsFingerprint` counts (222 reflected, 177 covered) match `TaomSettings`.
7. Share arithmetic. Claim: `FiefSiegeParticipationService.GetContributionShare` returns clanContribution over the top clan's contribution in (0, 1], never NaN, never above 1, and `FiefGrantPolicyService` still gates on finiteness and a positive requirement. Attack: overflow of the long sum, an int.MaxValue contribution, a record whose every entry is filtered out, a snapshot string hand-edited to a huge number, `long.TryParse` accepting a sign or whitespace, a clan id containing the separators (refused at record time; is it refused at restore time too?).
8. Save round trip. Claim: `SyncData` persists `Dictionary<string,string>` under `_taomFiefSiegeParticipation`; a pre-#565 save loads as empty; a fresh campaign resets the singleton via `ResetIfNoLoadedRecord` from both OnSessionLaunched and OnGameLoaded; a save pass never launders a stale record into "synced". Attack: the second campaign in one process, a load with the same settlement ids, a co-op client loading the host's save.

FILES (all uncommitted in the working tree):
Production:
- Main/Features/FiefGranting/IFiefSiegeParticipationService.cs (new)
- Main/Features/FiefGranting/FiefSiegeParticipationService.cs (new)
- Main/Features/FiefGranting/Hooks/FiefGrantingCampaignBehavior.cs (new)
- Main/Features/FiefGranting/FiefGrantCandidateFacts.cs
- Main/Features/FiefGranting/FiefGrantPolicyService.cs
- Main/Features/FiefGranting/FiefGrantFactsBuilder.cs
- Main/Features/FiefGranting/TaomSettlementClaimantDecision.cs
- Main/Features/FiefGranting/IFiefGrantSettingsProvider.cs
- Main/Features/FiefGranting/FiefGrantSettingsProvider.cs
- Main/Features/FiefGranting/FiefGrantingIoC.cs
- Main/Features/FiefGranting/Hooks/Patch70_FiefGrantDecisionSwap.cs (comment-only change; the swap itself is prior work)
- Main/Features/TaomSettings.cs, the "Kingdom Politics / Fief Grants" block only (search FiefGrant)
- Main/SubModule.cs, the one AddBehavior block (search "FiefGranting (#565)")
Tests:
- TAOM.Tests/Features/FiefGranting/FiefSiegeParticipationServiceTests.cs (new)
- TAOM.Tests/Features/FiefGranting/FiefGrantPolicyServiceTests.cs
- TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorSessionResetTests.cs (new)
- TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorCaptureGateTests.cs (new)
- TAOM.Tests/Features/FiefGranting/FiefSiegeParticipationBindingTests.cs (new)
- TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs (three count changes)
Docs:
- docs/features/fief-granting.md, docs/reference/feature-map.md (FiefGranting row), docs/reference/harmony-patch-registry.md (Patch70 section), CLAUDE.md (one Traps row), docs/features/coop-interop.md and docs/features/bannerlord-together-compat.md (counts)

REQUIRED SECTIONS in your output:

1. VANILLA CODE. Decompile from the INSTALLED v1.4.8 DLLs (E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/ and Modules/*/bin/Win64_Shipping_Client/) using ilspycmd, or read E:/Decompiled_Bannerlord/_categories_v1.4.8/ and say which you used. Paste as code blocks: MapEvent.FinalizeEventAux (the dispatch order), KingdomManager.SiegeCompleted, ChangeOwnerOfSettlementAction.ApplyInternal and ApplyBySiege, SettlementClaimantCampaignBehavior (both handlers), SettlementClaimantDecision.CalculateMeritOfOutcome and DetermineInitialCandidates, KingdomDecision.DetermineSupportOption, KingdomElection.DetermineOfficialSupport and GetAiChoice, MobileParty.ActualClan and MobileParty.Owner, and Kingdom.AddDecision.
2. SCENARIO WALKS. For each Known Suspect, one concrete campaign scenario with named actors, the exact event sequence with method names, and the record contents at each step.
3. CONFIG CROSS-REFERENCE. The ten MCM attributes in TaomSettings.cs against the clamps in FiefGrantSettingsProvider.cs and the table in docs/features/fief-granting.md: label, range, default, property name, knob by knob. The hint texts against the code: every promise a hint makes must be true.
4. FINDINGS OR OBSERVATIONS. Severity P1 (wrong fief awarded, crash, save corruption), P2 (wrong weighting in a reachable scenario, a false doc claim about behaviour), P3 (dead code, naming, comments). Each with file:line, the reproduction, and the fix you would make. Also list what you checked and found clean.

QUALITY GATES:
- Every finding cites TAOM source lines AND the vanilla code it depends on.
- Do not flag vanilla-matching behaviour as a bug; say "matches vanilla" instead.
- A claim about MCM persistence must cite the MCM code you read or say UNVERIFIED.
- No fixes, no edits, no commits: report only.

PRIOR REVIEW LESSONS:
SUCCESSES: on this same feature the first Codex pass refuted the central design claim (merit decides outright) by working a concrete counter-example against DetermineSupportOption; it also found the non-atomic live-file write and a partial service resolution. Config ID cross-ref caught rohan/dol_guldur mismatches elsewhere. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex once proposed switching a co-op gate to IsAuthority where the code already explained why that fails for BannerlordTogether.

Write your review to stdout; it will be captured to docs/reviews/raw/codex-adversarial-fiefgranting-participation-2026-09-11.md.
