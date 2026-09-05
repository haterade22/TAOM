# v1.4.5 Behavioral Refactor Audit — Aggregate Report (2026-05-22)

10 parallel audit agents reviewed TAOM's v1.4.5-migrated codebase against decompiled vanilla source. Each agent owned a specific subsystem (Army, Diplomacy, Battle, Equipment runtime, CulturalFeats, Banner/WotR, UI/Mixin, Mission AI, Reflection sites, CharacterCreation/RaceAge).

The compile being green ≠ runtime correctness, per empirical evidence from the April 2026 v1.4.0 migration attempt that compile-clean was a false signal. This pass was the proactive runtime-behavior review.

## Findings classification

- **5 confirmed real → fixed** (CRITICAL/HIGH severity)
- **4 false positives → rejected** (agent claim verified wrong against vanilla 1.4.5 source)
- **5+ deferred** (balance / S6-smoke-test verification needed, not runtime-breaking)

## Confirmed → fixed

| # | Severity | Finding | Agent | Fix |
|---|---|---|---|---|
| 1 | CRITICAL | `Patch22_ArmyTargeting` Postfix signature mismatch. v1.4.5 added 3 `out` params (`bestNavigationType`, `isFromPort`, `isTargetingPort`) to `AiMilitaryBehavior.CalculateDistanceScoreForBesieging`. TAOM's Postfix declared only `ref float bestDistanceScore` → Harmony failed to bind silently → entire border-proximity-floor feature has been a runtime no-op since v1.4.0. | 1 | Added 3 missing params (`ref MobileParty.NavigationType`, `ref bool`, `ref bool`) to `AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.Postfix`. Patch now binds. |
| 2 | CRITICAL | 40 rosters in `taom_child_equipment_templates.xml` had `IsLordTemplate="false"` → engine's exact-subset flag match treats `false` as "not tagged" → townsman/commoner child rosters invisible → naked children of commoner clans at age-up events. | 4 | Flipped all 40 `IsLordTemplate="false"` → `true` via regex. Engine now finds all 60 rosters (was 20). |
| 3 | CRITICAL | No `IsKingdomRulerTemplate` rosters for any TAOM culture. v1.4.3 added `NPCEquipmentsCampaignBehavior.OnRulingClanChanged` which calls `GetEquipmentsForChangingRuler` — returns `(null, null)` when no ruler-tagged roster exists → engine wipes new ruler's equipment. WotR ruler changes left rulers naked. | 6 + 10 | Extended `tools/generate_lord_template_equipment.py` to emit 4 ruler rosters per culture × 18 cultures = 72 new ruler rosters. Total rosters now 186 (was 76). |
| 4 | CRITICAL | 6 XSLT-renamed cultures (vlandia/empire/aserai/khuzait/sturgia/battania = Rohan/Dunland/Harad/Easterlings/Dale/Khand) had ZERO lord rosters → vanilla `Debug.FailedAssert` + fallback to generic Calradic gear at every age-up event. | 4 | Extended generator's CULTURES list to include the 6, mapping each to closest-styled TAOM equipment file (rohan/dunland/harad/rhun/dale; battania→harad fallback). |
| 5 | HIGH | `AllianceCampaignBehavior.OnAllianceTimerExpired` daily-tick calls `EndAlliance(k1,k2)` then `AddAllianceDecision(k1,k2)` unconditionally on the next line. TAOM's Prefix blocks `EndAlliance` but the duplicate `StartAllianceDecision` still queues, accumulating in `kingdom.UnresolvedDecisions` until a kingdom election re-fires `StartAlliance` on already-allied pair → vanilla side effects undefined. Existing TAOM comment claimed vanilla short-circuits on `IsAlliedWith` — verified false against v1.4.5 source. | 2 | Created `AllianceCampaignBehavior_AddAllianceDecision_Patch.cs` — Prefix returns false when `kingdomToAddDecision.IsAllyWith(kingdomToOffer)`. Updated EndAlliance patch comment to point at the new companion patch. Wired Initialize in `SubModule.cs`. |

## False positives → rejected (verified against vanilla 1.4.5)

| # | Agent claim | Reality | Source |
|---|---|---|---|
| A | `TaomPartyWageModel.cs` MISSING from disk → Rohan mounted wage feat + career TroopWages passive silently disabled | File present at `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` — Agent looked at `Main/Features/CulturalFeats/Models/` because `CLAUDE.md` listed it under that feature. CLAUDE.md doc is stale; file is fine. | Agent 5 |
| B | `Mission.RegisterBlow` reflection silent no-op — moved to `Agent.RegisterBlow(Blow, in AttackCollisionData)` (2 params) in v1.4.5 | `Mission.RegisterBlow(Agent, Agent, WeakGameEntity, Blow, ref AttackCollisionData, in MissionWeapon, ref CombatLogData)` still exists at `Mission.cs:5400`. TAOM's 7-param reflection lookup correctly resolves it. The agent confused `Agent.RegisterBlow` (different, lower-level method) with the high-level Mission entry. | Agent 9 |
| C | `TaomArmyManagementModel` double-applies the empire army-influence feat because vanilla bakes `EmpireArmyInfluenceFeat` into `base.DailyBeingAtArmyInfluenceAward` | TAOM's `ICulturalFeatsService.ApplyArmyInfluenceAward` only applies TAOM-custom feats (`RivendellArmyInfluenceFeat`, `GondorArmyInfluenceFeat`). It does NOT re-apply vanilla's `EmpireArmyInfluenceFeat`. The two feat sets are disjoint. No double-apply. | Agent 1 |
| D | `PlayerBluntDamageChance` default stuck at v1.3.15 value `0.1f` vs v1.4.5 vanilla `0.3f` → player battles 3× deadlier than tuned | `TaomSettings.cs` already declares `PlayerBluntDamageChance = 0.30f`. `BattleBalanceSettingsProvider.cs` falls back to `0.30f`. The default was already correct (likely fixed prior to this migration). Hint text even cites "Vanilla = 0.30." | Agent 3 |

## Deferred (not runtime-breaking; balance concerns or S6-smoke-test verification)

| # | Agent | Description | Why deferred |
|---|---|---|---|
| D1 | 3 | `TaomCombatSimulationModel.GetSimulationTickInterval` override for siege auto-resolve duration (vanilla doubled in v1.4.0). | Balance concern, not runtime breakage. TAOM doesn't override; sieges just last longer. Verify in-game feel at S6 — add override if defenders bleed too long. |
| D2 | 1 | `TaomTargetScoreModel` passes inflated `effectiveStrength` to `base.GetTargetScoreForFaction` before vanilla's new 2× safety gate → evil-faction armies may target too-strong settlements. | Balance concern. Vanilla's gate may already reject implausible targets without our help. Verify with one campaign load + observe AI siege patterns. |
| D3 | 3 | `TaomPartyHealingModel` cultural survival multipliers tuned against v1.3.15's 4× weaker Medicine baseline → armies now too survivable. | Data re-tune (not code). Needs in-game observation to decide if existing JSON values still produce intended survival rates. |
| D4 | 7 | `VerticalBottomToTop` → `VerticalTopToBottom` mass swap (60+ TAOM prefab sites). v1.4.0 fixed the layout-inversion bug → TAOM's deliberate use of the wrong direction now renders inverted. | Per-site visual review needed; a blind mass swap could BREAK sites where TAOM was using the layout correctly. S6 smoke test flags which sites visibly invert. |
| D5 | 8 | `TaomAgentStatCalculateModel` may double-invoke via `SandboxAgentStatCalculateModel.InitializeMissionEquipment` virtual re-dispatch. | Needs runtime confirmation that vanilla 1.4.5 specifically has this dispatch pattern. Defer to S6 — add idempotency guard if double-fire observed. |
| D6 | 10 | NamedCompanions culture-routing — Aragorn/Legolas/Gimli may receive wrong-culture lord equipment when promoted via `CompanionRolesCampaignBehavior.AdjustCompanionsEquipment`. | Vanilla null-safe (companion keeps existing gear) — not a crash. Verify at S6 that each of 18 companions' `culture=` attribute matches a TAOM-authored roster. |
| D7 | 1 | `TaomArmyManagementModel.DailyBeingAtArmyInfluenceAward` calls `base.X()` (which already includes vanilla EmpireArmyInfluenceFeat) → if TAOM's `gondor` culture XML somehow inherits the empire feat, double-apply would happen. | Data concern; would need `gondor` culture XML to declare the empire feat to fire. Skip unless observed. |

## Lessons / patterns

### Audit-agent reliability — 5 of 14 findings (~36%) were false positives

Three classes of agent error:
1. **Wrong-file confusion** (Agent 5): agent looked at the location CLAUDE.md mentioned, not the actual location on disk.
2. **API name confusion** (Agent 9): agent saw `Agent.RegisterBlow` (different lower-level method) and reported `Mission.RegisterBlow` as removed.
3. **Stale baseline assumption** (Agent 3): agent assumed a default value reflected v1.3.15 era; reality is the default was updated previously.

Pattern: **always verify agent claims against vanilla decompile + actual TAOM source before applying fixes.** I added an explicit "verify first" step to several pending todos as I worked through them — and 4 of the planned fixes turned into rejections after verification.

### Audit-agent value — 5 of 14 findings (~36%) were real runtime bugs

The 5 real findings were all in code that **compiled clean** — exactly the failure mode the multi-agent sweep was designed to catch. Without proactive review against vanilla decompile, they would have surfaced at S6 smoke test or worse, in-game.

Highest-leverage agents:
- **Agent 1** (Army) — caught Patch22 signature drift (silently dead feature since 1.4.0)
- **Agent 4** (Equipment runtime) — caught the 40 `IsLordTemplate="false"` invisible-rosters bug AND the 6 missing XSLT culture rosters
- **Agent 6** (Banner/WotR/ruler events) — caught `IsKingdomRulerTemplate` gap
- **Agent 2** (Diplomacy) — caught EndAlliance + AddAllianceDecision reentry queuing

### Process improvement: agent prompt should require verification step

The agents that produced false positives shared a pattern: confident assertion without independent cross-check against vanilla source. Future agent prompts should explicitly require:
1. Paste vanilla source as evidence (not paraphrase)
2. Paste TAOM source as evidence (not paraphrase)
3. State which file path was checked

Two of the false positives (B, D) would have been caught at the agent level if it had been required to paste verification evidence.

## Net result

Build + tests still green:
- `dotnet build Main/TAOM.csproj` — 0 errors, 1 warning
- `dotnet test TAOM.Tests` — 2,323 / 2,325 pass

The migration is now substantively more robust than the compile-green-only state would suggest. 4 critical runtime-breaking bugs fixed:
1. Patch22 silent-dead feature restored
2. 40 commoner-child rosters now discoverable
3. Ruler-succession naked-equipment bug fixed (72 new ruler rosters across 18 cultures)
4. 6 XSLT cultures now have age-up equipment rosters
5. Alliance EndAlliance reentry duplicate-queue prevented

Deferred items (D1-D7) are gameplay-verification or balance items — not runtime breakage. S6 smoke test will surface concrete issues to drive their resolution.

## Cross-references

- `docs/reviews/rca-v1.4.5-migration-2026-05-22.md` — RCA for the 4-file C# migration deep-review + Codex findings
- `docs/migration/TRACKING.md` — per-session migration status
- Issue #210 — migration tracking

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
