# RCA: the Bannerlord v1.5.2 compile commit, deep review, 2026-09-14

Scope: the first green commit on the new `bannerlord-1.5.x` line, cut from trunk at v2.0.28 (last
built on v1.4.8) and compiled against the installed v1.5.2 engine. Nine compile errors in two waves,
the Execution feature re-homed from the deleted `ExecutionRelationModel` / `OnLordExecuted` seam onto
the blood-feud seam, the banner-colour and party-icon-scale transpilers relocated with the engine's
party-visual code, the ten-phase `OnCharacterCreationIsOverEvent` guards, two engine-internal test
helpers re-derived. Build 0 errors; suite 9,085 passed, 2 skipped, 0 failed of 9,087 before the
review round.

Seven review agents ran: standards, API compatibility, efficiency, completeness, cross-system data
flow, an adversarial audit of the Execution seam, and an IL-level audit of the two transpilers
against the real v1.5.2 method bodies. Every finding below was re-read against the v1.5.2 decompile
or the repo before being accepted, per `.claude/rules/evidence-over-claims.md` A.4.

## Findings

| # | Sev | Finding | Category | Why missed | Action |
|---|---|---|---|---|---|
| 1 | HIGH | `ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch` built the executor from `IPlayerContextAdapter` on every call, but `GetBloodFeudStartRelationPenaltyToOtherClan(Hero dyingHero, Clan otherClan)` is also reached when an AI clan executes a member of the player's clan (`OnHeroKilled` -> `OnPlayerClanMemberExecuted` -> `StartBloodFeudWithClanByAIExecutingPlayerRelative` -> the same loop in `OnBloodFeudStateChanged`). There the victim and the assumed executor both resolve to the player's side, so the kinslaying 1.5x fired against the bereaved player for a kill the player did not commit | Harmony & IL: a seam that lacks the actor | The re-seam arrived at the static method from `OnPlayerExecutedHero`, which is player-only, and wrote "only the player executes through this path" into the patch doc, the registry and the feature doc without enumerating the method's callers. The v1.4.8 design had met the same problem (`OnLordExecuted()` carried no actors) and bridged it with a snapshot; the port kept the conclusion and dropped the question. Found independently by the data-flow agent (trace A) and the Execution audit | The hook decides the path: `IOnExecutionAction.IsPlayerTheBereaved(victimClanId, playerClanId)`, and the postfix leaves vanilla's number alone when the victim's clan is the player's clan. `IPlayerContextAdapter.GetPlayerClanId()` added. Three hook tests. Patch doc, registry, `execution.md` and `alignment-aware-execution.md` corrected. Lesson in `lessons/harmony-il.md`; `harmony-patches.md` Research First asks for the caller list |
| 2 | MEDIUM | None of the four ten-phase guards (`PlayerPossessionBehavior`, `SpecialResourcesBehavior`, `StartupResourcesBehavior`, `KingdomJoinOfferBehavior`) had a test pinning the phase, and the fourth had no guard at all until the compiler refused the parameterless handler | Testing & QA | The guards were ported file by file from the archived 1.5.0 branch; `KingdomJoinOfferBehavior` joined trunk after the fork and was not on that list. "It compiles" was the only gate, which catches a missing parameter and nothing about the index | Handlers made `internal`; a `_BeforeTheLastPhase_` DataRow test (0..8) per subscriber, plus the positive, all-ten-phases and swallowed-exception tests where the handler needs no engine (`KingdomJoinOfferBehaviorTests`). Lesson in `lessons/testing-qa.md` |
| 3 | LOW | `RefugeClanScreenPatch.cs` crossed the ADR-002 ceiling (153 lines) through comments added while adapting it to the abstract `ClanPartyItemVM` | Standards | Comment growth during a fix, measured by nobody until the standards agent counted | Trimmed to 149; the role-callback rationale lives on the declaration line |
| 4 | LOW | `IExecutionRelationService` XML doc still described `GetRelationChangeForExecutingHero` and its `showQuickNotification` out-value, both deleted at v1.5.0 | Docs | The interface was not in the port's diff, so nobody reopened it | Rewritten for the v1.5.x seam |
| 5 | LOW | `execution.md`, `alignment-aware-execution.md`, `banner-color-persistence.md`, `party-icon-scale.md`, `configs-balance.md`, `feature-map.md` and `ai-includes/patterns.md` described the v1.4.8 seams (thread-local `ExecutionContext`, the `AddCharacterToPartyIcon` postfix, two scale sites, `governorDifferentCultureLoyaltyEffect`) | Docs | The plan deferred documentation to a final docs commit; a commit that deletes a seam must carry the doc that named it, or the doc contradicts the tree for the life of the branch | All seven updated in this commit; `alignment-aware-execution.md` keeps its v1.4.8 record under a status banner and gains the v1.5.x chain |
| 6 | LOW | No offline test fed the real engine IL through the two transpilers; `TranspilerSiteBindingTests` existed only on the archived branch, and `PartyIconScaleTranspilerTests` already named it | Testing & QA | The port's plan filed the gate under a later "test gates" commit; the transpiler audit had to do by hand what the test does | Ported and green on v1.5.2 (3 tests: 2 + 1 + 2 rewrites, no fail-safe warning) |
| 7 | LOW | `PartyIconScaleTranspiler`'s `Scale` / `ApplyScaleLocal` / `mul` matchers match by method name only, while `BannerColorTranspiler` also checks the declaring type | Harmony & IL | Asymmetry between two transpilers written a month apart | No change: each method has exactly one such call on v1.5.2 and the binding test now pins the rewrite counts, so a second same-named call would fail the gate rather than misfire silently |

## Deferred, and why

The efficiency agent reported five MEDIUMs on code this changeset did not introduce:
`RefugeClanScreenPatch` creates its selection delegate per refresh and scans `MobileParty.All` per
refuge, `Mission_SpawnAgent_Patch` resolves the clan colours in both prefix and postfix, and
`TaomSettlementLoyaltyModel` reads `Campaign.Current?.Options?.IsHighRebellionEnabled` three times per
daily loyalty tick. All are per-refresh or per-day, none per-frame, and all predate this commit
(the loyalty model's reads are two field hops each). Per `.claude/rules/working-discipline.md`, quality
findings on untouched behaviour are queued behind the gate, not folded into an engine bump. Filed here
so they are not lost; none needs an issue.

The data-flow agent could not locate the parser for `<DependedModuleMetadata version="v1.5.2.*">`
in the decompiled engine assemblies (it found `IncompatibleModules` / `Module Id` in
`ModuleInfo.LoadWithFullPath` and confirmed the XML parses). The element is read by the launcher, not
the engine; the same wildcard form shipped for v1.4.8 and is unchanged in shape.

## Root-cause pattern

Findings 1, 2 and 6 are one pattern: **the port verified what it was replacing and not what it was
moving to.** The Execution re-seam enumerated the callers of `OnLordExecuted` (one, player-only) and
not the callers of `GetBloodFeudStartRelationPenaltyToOtherClan` (two paths, one of them an AI
executor). The ten-phase guards covered the subscribers the 1.5.0 port knew about and not the one
trunk added later. The transpiler gate was written and then left on the branch it was written on.
In each case the evidence was one grep away on the installed engine or on `Main/`, and the review
found it in a single pass because the agents were pointed at the destination rather than the origin.

The 2026-09-06 lesson in `lessons/harmony-il.md` already says "enumerate callers across the installed
assemblies rather than reasoning from where you found the method". This is that lesson one level up:
the callers were enumerated, for the wrong method. The preventive action is therefore a rule change,
not another lesson alone: `harmony-patches.md` Research First now asks for the caller list whenever a
patch supplies an actor the target's signature does not carry.

## Why each agent missed finding 1 (or did not)

- **Standards:** not its lens; it checks layering, not call paths. Correctly silent.
- **API compatibility:** verified every signature and parameter name and answered the four ordering
  questions it was asked (ten phases, `GetBanner`, `MapEvent.Component`, clan destruction before
  `OnHeroKilled`). It confirmed "the patch comment is accurate" for the kingdom gate, which it is, and
  was never asked who the executor is. A signature audit cannot see an actor the signature omits.
- **Efficiency:** not its lens.
- **Completeness:** found the stale docs (finding 5) and the missing guard tests (finding 2); did not
  read the engine.
- **Data flow:** found it, by tracing every caller of the patched static method from the decompile.
- **Execution audit:** found it, by testing the claim "only the player reaches this path" against the
  `OnHeroKilled` branches.
- **Transpilers:** not in scope.

Two agents finding the same HIGH independently is the review working as designed; the miss was in
the port, a month earlier, where the claim was written as design reasoning rather than as a fact to
be checked.

## Feedback memories to codify

None new. The pattern is covered by the lessons entries and the `harmony-patches.md` bullet; a memory
file would duplicate the rule that now loads with every patch file.
