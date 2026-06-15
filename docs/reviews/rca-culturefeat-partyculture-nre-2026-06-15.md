# RCA — `CultureFeatAdapter.ResolvePartyCulture` NRE (PartyBase.Culture on a faction-less party)

**Date:** 2026-06-15
**Feature:** CulturalFeats (party-culture feat resolution)
**Severity of shipped bug:** HIGH (campaign-map hard crash)
**Fix commit:** 0046eaf (null-safe `ResolvePartyCulture` + Harmony Prefix on `PartyBaseHelper.HasFeat`; pushed to `bannerlord-1.4.5`). **In-game confirmed 2026-06-15** — no crash recurrence in normal play. Issue #281 closed.

## Top-line summary

A `NullReferenceException` crashed the campaign map tick during `Army.OnSiegeStarted` → `IsWaitingForArmyMembers` → per-member-party `EstimatedStrength` → `GetPowerOfParty` → party `Morale`/`PartySizeLimit`. Two reported stack traces both terminated at [CultureFeatAdapter.cs](../../Main/Features/CulturalFeats/CultureFeatAdapter.cs) line 68, which called `party.Culture` directly.

`PartyBase.Culture` is `MapFaction.Culture` with **no null guard** (PartyBase.cs:255; `MapFaction` returns null when both `MobileParty` and `Settlement` lack a faction, PartyBase.cs:236-250). The crashing party `lord_1_3_party_1` ("Gorwulf, The Boar") had `LeaderHero == null`, `MapFaction == null`, `Owner != null`. So the engine getter dereferenced a null `MapFaction` and threw **inside the getter** — the TAOM `if (party.Culture != null)` guard was useless because the getter throws *before* it can return.

The fix routes all party-culture resolution through a single null-safe `?.` chain (`party.LeaderHero?.Culture ?? party.MapFaction?.Culture ?? party.Owner?.Culture ?? party.Settlement?.Culture`) and migrates the one other direct-getter caller (`TaomBattleRewardModel`) onto it.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `ResolvePartyCulture` called `party.Culture` (= `MapFaction.Culture`, unguarded) → NRE when `MapFaction == null` | Null-safety / computed-getter | The code copied vanilla `PartyBaseHelper.HasFeat`'s precedence *verbatim*, including its `if (party.Culture != null)` line — assuming vanilla's guard was sufficient. It is not: the guard reads a property whose getter throws. And the latent vanilla bug never fires in vanilla because vanilla never calls `HasFeat` per-party in the party-size/morale models. | Route every party-culture resolution through the single `ResolvePartyCulture` chokepoint; never call `PartyBase.Culture` directly. `.claude/rules/adapters.md` already mandates `?.` on computed getters — this is that rule applied to `PartyBase.Culture`. New feedback memory codifies the "vanilla helper called on more entities than vanilla does" angle. |
| 2 | LOW | `TaomBattleRewardModel` had the same `winnerParty.Culture` exposure as a `??` fallback, plus old `Owner ?? Culture` precedence skipping `LeaderHero` | Consistency / null-safety | It was the single remaining caller using the pre-chokepoint inline pattern (the gap Codex review 43 flagged for other models but didn't sweep here). | Migrated to `FromOrNull(winnerParty)`. Data-flow review confirmed no other inline `PartyBase.Culture` caller remains in `Main\`. |

## Root-cause pattern

**TAOM invokes vanilla resolution logic on a broader set of entities than vanilla itself does, surfacing latent vanilla NREs that vanilla never triggers.** Vanilla `PartyBaseHelper.HasFeat` calls `party.Culture`, but vanilla only calls `HasFeat` in narrow contexts. TAOM's GameModels (`TaomPartySizeModel`, `TaomPartyMoraleModel`, `TaomPartySpeedModel`, `TaomFoodConsumptionModel`, `TaomPartyTroopUpgradeModel`) call it on **every** party-size/morale/speed/food/upgrade calc — including faction-less lord parties mid-army-siege-start, which vanilla's code path never reaches. Copying vanilla's logic verbatim therefore inherited a latent crash that only TAOM's broader call frequency could trigger.

This is the same shape as `feedback_ported_data_upstream_bugs_vanilla_baseline.md` (1-for-1 ports inherit upstream bugs) generalized from data to *control flow*: a verbatim logic copy inherits the original's unstated preconditions, and TAOM violates those preconditions by calling it more widely.

## Why each deep-review agent's rule set is relevant

This crash was found in production (live debug session), not by the review — the review was run on the *fix*. For the record, of the 5 agents:

- **Agent 1 (Standards):** would not have caught the original — calling `party.Culture` is not an ADR violation; the boundary adapter is the correct place to touch sealed types.
- **Agent 2 (API Compat):** would catch it only if its prompt asked "does this computed getter throw internally?" — which it now does (it verified `Hero.Culture`/`Settlement.Culture` are safe fields but `PartyBase.Culture` is an unguarded computed getter). This is the agent best positioned to catch the *class* of bug going forward.
- **Agent 3 (Efficiency):** out of scope.
- **Agent 4 (Completeness):** out of scope (it checks tests/docs/issue, not getter safety).
- **Agent 5 (Data Flow):** the right agent — its "remaining direct `PartyBase.Culture` exposures across `Main\`" trace is exactly the sweep that confirms the blast radius is closed. Had this trace been run before the fix shipped originally, it would have flagged the unguarded call.

## Preventive rule / memory

- **`.claude/rules/adapters.md`** already mandated `?.` on computed properties; strengthened (2026-06-15) with the concrete named trap — `PartyBase.Culture => MapFaction.Culture`, the computed-getter-vs-plain-field distinction, and "route through one null-safe chokepoint, never inline" — so it fires for every future adapter edit. The original rule existed but wasn't applied when the verbatim vanilla precedence was copied.
- **New feedback memory** `feedback_taleworlds_computed_getter_nre_route_through_chokepoint.md` — codifies (a) the computed-getter-throws-before-null-check trap for `PartyBase.Culture` specifically, and (b) the systemic "TAOM calls a vanilla helper on more entities than vanilla does → latent vanilla NRE goes live" pattern, with the chokepoint-resolution fix.
- **Follow-on hardening (2026-06-15):** swept the three remaining Owner-only party-culture callers — `TaomArmyManagementModel` (influence award + cost), `TaomRaidModel` (raid damage), `TaomPartyWageModel` line 49 (party wage) — onto the `ResolvePartyCulture` chokepoint, so **all 9** party-culture feat models now resolve identically (LeaderHero-first, null-safe). They never hit the throwing `party.Culture` (they used `Owner?.Culture`, so no crash), but inline resolution left the door open for a future `?? party.Culture` fallback to reintroduce the NRE — uniformity closes that. Garrison wage (`TaomPartyWageModel` line 82, settlement-owner-scoped) and per-hero `StringId` passives are correctly excluded. Verified pre-edit (API type + adversarial semantics) and post-edit (diff-correctness + full uniformity sweep) by 2-agent workflows; build clean, 3169 tests green. Behavior shift documented in [`cultural-feats.md`](../features/cultural-feats.md).

## Tests

`ResolvePartyCulture(PartyBase)` is engine-boundary (sealed `PartyBase`, requires live `Campaign.Current`) — not unit-testable in the MSTest harness (ADR-008 "test via game"). Verified by: full build clean (0 errors, `-p:ModuleId=` to skip the game-folder copy while the game was running), 3169 tests green, and in-game confirmed 2026-06-15 (no crash recurrence in normal play).

## Phase 3e addendum — Codex caught the residual exposure my fix and 7 review agents missed (2026-06-15)

The Codex adversarial review of the fix found a deeper finding none of the prior passes did (5 deep-review agents + 2 verification-workflow agents all missed it): **making TAOM's own culture resolution null-safe does NOT protect the vanilla `base.XxxMethod()` calls that every `TaomXxxModel` invokes first.** Several vanilla base methods call `Helpers.PartyBaseHelper.HasFeat(party, vanillaFeat)`, and `HasFeat`'s `if (party.Culture != null)` line dereferences the same unguarded `PartyBase.Culture => MapFaction.Culture` — so the NRE can still fire *inside the vanilla base call*, on the same faction-less-party shape, before TAOM's null-safe code ever runs.

Verified against the v1.4.6 decompile (2-researcher + 1-design workflow):

| # | Vanilla base method (called by) | Reaches `party.Culture`? | Verdict |
|---|---|---|---|
| 1 | `DefaultArmyManagementCalculationModel.DailyBeingAtArmyInfluenceAward` (TaomArmyManagementModel) — `HasFeat(EmpireArmyInfluenceFeat)` | yes | **CONFIRMED HIGH** |
| 2 | `DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost` (TaomArmyManagementModel) | no — line 64 derefs `LeaderHero` first; if `HasFeat` runs, `LeaderHero` is non-null → safe branch | **DISPUTED** (Codex over-attributed) |
| 3 | `DefaultBattleRewardModel.CalculateRenownGain` (TaomBattleRewardModel) — `HasFeat(VlandianRenownMercenaryFeat)` | yes (gated by `IsMobile`, but the crash party is mobile) | **CONFIRMED MED** |
| 4 | `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed` (TaomPartySpeedModel) — `HasFeat(Battanian forest / Aserai desert)` | yes | **CONFIRMED MED** |
| 5 | `DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade` (TaomPartyTroopUpgradeModel) — `HasFeat(KhuzaitRecruitUpgradeFeat)`, mounted only | yes | **CONFIRMED MED** |
| — | `DefaultPartySizeLimitModel` / `DefaultPartyMoraleModel` / `DefaultMobilePartyFoodConsumptionModel` / `DefaultRaidModel` base methods | no `HasFeat` at all | swept clean (confirms the original crash was TAOM-side, not base) |

**Root fix (one patch, not per-model):** `Main/Features/CulturalFeats/Hooks/PartyBaseHelper_HasFeat_Patch.cs` — a Harmony Prefix on the actual buggy vanilla method `Helpers.PartyBaseHelper.HasFeat`, replacing its body with `CultureFeatAdapter.ResolvePartyCulture(party)?.HasFeat(feat) ?? false`. Behaviorally identical to vanilla for every non-crashing input (same precedence), returns `false` instead of NREing for a faction-less party, and fixes **all** current + future `HasFeat` callers (vanilla base methods, TAOM, third-party) at the source — not Codex's suggested per-model "inline the vanilla calculation," which would duplicate and drift vanilla logic across N models every engine bump. Reuses the `Patch18_CulturalFeats` category (no SubModule.cs change). Plus a LOW doc-comment fix in `IWageModifierService.cs`.

### Findings table (Phase 3e)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 3 | HIGH/MED | Vanilla `base.XxxMethod()` calls `PartyBaseHelper.HasFeat` → throwing `party.Culture`; TAOM overrides call `base` first, so the NRE can fire in the base call despite TAOM's null-safe resolution | Missing vanilla gate — "fixed my boundary, not the vanilla code I call into" | Every prior pass scoped the search to TAOM code (`ResolvePartyCulture`, direct `party.Culture` in `Main/`). The throwing call lives in **vanilla** `PartyBaseHelper.HasFeat`, invisible to a `Main/`-only grep. The mental model "all 9 models route through the chokepoint → safe" was wrong: `base.XxxMethod()` bypasses the chokepoint entirely. | Patch the vanilla method at the source (done). Generalized rule below + memory extension: when overriding a GameModel that calls `base`, the base runs vanilla code on the same (possibly degenerate) inputs — trace what the base dereferences, not just your override body. |

### Why each prior agent missed it

- **Deep-review Agent 5 (data flow)** swept "remaining direct `PartyBase.Culture` exposures across `Main/`" and correctly found zero — but the throwing call is in `Helpers.PartyBaseHelper` (vanilla), outside `Main/`. The sweep's scope boundary was the defect.
- **The 2 post-edit verification-workflow agents** confirmed "all 9 party-culture models route through the chokepoint" — true for TAOM's *own* feat calls, but they didn't trace *into* the `base.XxxMethod()` invocations to see vanilla's own `HasFeat` use.
- **Codex** caught it precisely because it decompiled the vanilla base methods rather than trusting the TAOM-side closure claim. This is the value of the independent adversarial pass.

### Generalizable rule (the "never again")

**When you override a GameModel/engine method and call `base.X(...)`, the base runs vanilla code on the same inputs — including degenerate ones your feature newly produces. Fixing your own boundary (adapter/service) does not protect the base call. Decompile the base method and audit what it dereferences on the degenerate input, or patch the shared vanilla helper at the source.** A data-flow sweep scoped to `Main/` is blind to crashes inside the vanilla methods `Main/` calls into. Codified in `feedback_taleworlds_computed_getter_nre_route_through_chokepoint.md`.

**Tests:** the `HasFeat` Prefix can't be unit-tested (Harmony doesn't apply in the MSTest host; `HasFeat` takes a sealed `PartyBase`) — ADR-008 "test via game." Verified: build clean (0 errors); full suite green except 9 pre-existing `GetVolunteerTroopId_DolGuldur*` failures caused by an unrelated working-tree `TEMP-SPIDER-TEST` weight bump (`taom_spider_creature` 1→40, marked "REVERT before commit"), not this change (2835 passed with that class excluded). In-game confirmed 2026-06-15 — no crash recurrence in normal play (the exact `Army.OnSiegeStarted` siege-start path was not force-reproduced). Committed 0046eaf; issue #281 closed.
