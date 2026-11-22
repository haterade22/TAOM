# Codex pre-review: career tier trust on the co-op server (2026-11-22)

You are an adversarial senior reviewer for TAOM, a Bannerlord v1.5.7 mod (.NET Framework 4.7.2). Read AGENTS.md
first. Review only; edit nothing. Report findings as P1 (wrong behaviour, crash, data loss), P2 (likely defect),
P3 (minor), each with file:line, the failure scenario and the evidence you read. Say UNVERIFIED for any engine
claim you could not check in the installed TaleWorlds DLLs.

## Scope: the uncommitted diff in this worktree (`git diff` and the three new files)

(A) Career tier passive filter. A co-op player's career reaches the co-op server through career sync
(`Main/Features/CoopInterop/CareerSync/`, docs/features/coop-interop.md "Career sync"). A tier opens at level
1, 10, 20 or through a career quest, which runs only on the player's own machine, so the server cannot prove a
quest unlock. The maintainer decided (2026-11-22): the server keeps the choices as sent, but its passive cache
counts a choice only when the hero's level on the server opens the choice's group tier.
- New `Main/Features/CareerSystem/ICareerTierTrust.cs`, `Main/Features/CoopInterop/CareerSync/CoopCareerTierTrust.cs`
  (registered in `CareerSyncModule`).
- `CareerPassiveService.RefreshCache` asks the trust for every non-root choice; the root always counts.
- `CareerCampaignBehavior.OnHeroLeveledUp` now rebuilds the cache for any hero with a career.
- Tests: `TAOM.Tests/Features/CareerSystem/CareerPassiveServiceTests.cs`,
  `TAOM.Tests/Features/CoopInterop/CareerSync/CoopCareerTierTrustTests.cs`, `SpecialResourcesWiringTests.cs`.

(B) A comment pass (ADR-009) over comments in 13 files; comments became `///` summaries or were deleted. Check
only that no code changed by accident.

## Known suspects (check each)
1. Is "co-op server" = `ICoopSessionProvider.IsSessionActive && IsAuthority` right for a Host/Client server, a
   dedicated server, a client, and single player with the Coop module enabled but no session?
2. Every path that rebuilds the cache on the server (`graphify` callers: CareerCampaignBehavior session launch,
   hero killed, level up; CareerCreationHandler; CareerLifecycleService; CareerScreenVM; CareerSyncClient;
   CareerSyncHost). Is any passive read path not fed by this cache, so a refused tier still applies?
3. Does `HeroLevelledUp` fire on the co-op server for a player's hero? If not, what is the effect?
4. Thread safety: RefreshCache runs from campaign events; the trust reads the session provider and
   `Campaign.Current.CampaignObjectManager` (`CareerSyncHeroAdapter`). Can it be called off the main thread or
   before a campaign exists (session launch on the server)?
5. DI: `CareerPassiveService` now needs `ICareerTierTrust`, registered only by the CareerSync feature module.
   Can anything resolve the passive service where that module did not register?
6. On the server, a hero with a career whose level is below a tier: the passive is gone, but does anything else
   (abilities, `CareerAgentStatService`, troop counters) still apply the choice?
