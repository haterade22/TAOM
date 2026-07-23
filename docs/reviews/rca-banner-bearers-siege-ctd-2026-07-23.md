# RCA — BannerBearers siege native CTD (heraldry guard on SetFormationBanner)

**Date:** 2026-07-23
**Scope:** Root-cause fix for the 100%-repro native CTD when starting a manual siege assault, plus the `/deep-review` of that fix.
**Trigger:** a playtester (engine v1.4.7.117484, TAOM v2.0.13) got a hard CTD every time he began a manual siege assault at Stranding (`sturgia_town_c`) — `0xC0000005` in `TaleWorlds.Native.dll+0x28ac0e`, no managed exception, BUTR captured nothing. The last durable log line was `[Warg] Added behavior trees to 0 wargs`, immediately after custom-skeleton ActionSets were assigned in the deployment phase.

## Top-line

This is the resolved form of the crash [`rca-siege-guards-2026-07-16.md`](rca-siege-guards-2026-07-16.md) was blocked on. That RCA guessed at MixedFormations/SmartCavalryAI and shipped `IsFieldBattle` guards it explicitly labelled *"defensive, not a confirmed root-cause fix — awaits the player's Event Log fault offset."* The offset arrived (`0x28ac0e`) and points at a **different, uncovered path**: TAOM's **BannerBearers** feature (`#351`, shipped after those guards) drives the engine's native `BannerBearerLogic.SetFormationBanner` for **every team's** formations during deployment. The native banner-tableau rebuild (`agent.UpdateSpawnEquipmentAndRefreshVisuals`) access-violates when the bearer's heraldry `Banner` (sourced from `agent.Origin.Banner`) is null or has an empty `BannerDataList` — the state a custom-faction party with no heraldry produces.

Fix: a per-candidate heraldry guard in `BannerBearerAssignmentMissionLogic.TryAssignBanner` that skips `SetFormationBanner` unless **every** bearer-candidate in the formation carries renderable heraldry. `/deep-review` (5 agents) then found **3 MED** issues in the first cut of that fix — all fixed in-session; suite green (**4410 passed, 0 failed, 2 skipped**).

## Root cause

`BannerBearerAssignmentMissionLogic.OnTeamDeployed` runs for **every** team and calls `SetFormationBanner` on every eligible formation. Vanilla almost never runs this path: a formation is only bannered when it has a hero captain carrying a banner item, or via the player's Order-of-Battle screen — i.e. **player-side, heraldry-backed formations only**. TAOM deliberately broadens it (that is the whole feature) so Middle-earth lines march under standards. `SetFormationBanner` → `UpdateBannerBearersForDeployment` → `UpdateAgent` → `UpdateSpawnEquipmentAndRefreshVisuals` rebuilds the bearer's visuals; the `using_tableau` banner cloth renders the agent's heraldry `Banner`, seeded at spawn from `troopOrigin.Banner` (`Mission.cs:4434` — `new AgentBuildData(troop).Banner(troopOrigin.Banner)`). A custom LOTR-faction garrison/militia party whose clan/kingdom carries no heraldry gives its agents a null `Banner` (or one with an empty `BannerDataList`); the native tableau builder dereferences it and faults. The fault instruction (`mov rax,[rdi+0x10]` NULL → `movsxd rcx,[rax+0x1C]` → `shr rcx,5`, a 32-byte-stride array index) is consistent with iterating `BannerData` off a null list. TAOM's *managed* guards were all correct; the fault is in native code TAOM cannot guard from inside, so the fix is to not make the native call when its heraldry precondition is unmet.

## Findings (the `/deep-review` of the fix)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | **MED** | First cut sampled **slot 0 only** for the heraldry check. The engine picks the bearer by priority from ALL candidates (`BannerBearerLogic.FindBannerBearableAgents`, `:371-387`), so in a **mixed-origin** formation slot 0 could pass while a null-banner custom-faction troop is the actual bearer → the same `0xC0000005` survives in the mixed-party subset — precisely the siege/army contexts where the crash lives. | Data flow (representative-sample vs actual-consumer) | I reasoned "formations are near-always single-party, slot 0 represents the whole" and even wrote that into the code comment — a representative-sample assumption about data the *engine*, not TAOM, selects from. The guard must cover the engine's whole candidate set, not a proxy. | Fixed: `CollectCandidateBannerDataCounts` samples every non-detached candidate; `HasRenderableHeraldry` requires **all** renderable (empty set → not renderable). Codified below. |
| 2 | **MED** | `agent.Origin?.Banner?.GetBannerDataListCount()` is not exception-safe: `PartyAgentOrigin.Banner` is a **computed getter** that falls through to `Party.MapFaction.Banner` / `HeroObject.MapFaction.Banner` with no null guard, so it can **throw** (not return null) for a factionless party/hero. `?.` guards `Origin`-null and the Banner *result*, never a throw inside the getter. A guard whose job is to prevent a crash would itself have thrown. | Adapters (`adapters.md` "computed getter throws before your null check") | I treated `?.Banner?.` as "safe against no-banner state." It is safe against a null *result*, not against a getter that dereferences an unguarded inner object. The `adapters.md` rule names exactly this trap; I didn't decompile the getter body before trusting the `?.` chain. | Fixed: `SafeBannerDataCount` wraps the read; any failure → 0 (skip), matching the guard's fail-safe intent. Likely inert (the engine reads `Origin.Banner` at spawn, so a throw would precede deployment) but a crash-guard must not itself throw. |
| 3 | MED | `BannerBearerAssignmentMissionLogic.cs` reached **156 lines**, breaching the ADR-002 150-line entry-point ceiling — the guard comment + inline helper pushed it over. | ADR-002 | Same class as `rca-siege-guards-2026-07-16.md` finding #2: added lines to a file already near the ceiling without checking `wc -l`. | Fixed by extracting the sealed-type traversal to `Hooks/FormationBannerHeraldry.cs` (a boundary helper) rather than only condensing the comment — the extraction also gave finding #1's all-candidates loop a clean home. Entry point now 136. |
| 4 | LOW | `BannerBearerService.IsRaceAllowed` calls `_configProvider.GetConfig()` twice. | Efficiency | n/a — **pre-existing code, not in this changeset.** | **Deliberately NOT fixed.** Editing untouched adjacent code is scope creep (`CLAUDE.md` "Edit scope discipline"). Recorded so the decision is explicit; a future IsRaceAllowed change can fold the two reads. |

## Root-cause pattern: broadening a native call to new entities without re-checking its preconditions

The primary bug and finding #1 are the same mistake at two scales. Vanilla runs `SetFormationBanner` only where a precondition holds by construction (player-side, heraldry-backed, hero-captained formations). TAOM broadened the caller set — first to **every team's** formations (the feature), then the *guard* narrowed its check to **slot 0** (a proxy for the formation). Each broadening step silently inherited a precondition that no longer holds for the new, wider set: AI/custom-faction parties need not have heraldry, and the priority-picked bearer need not be slot 0. **When you extend an engine call to entities the engine never applied it to, the engine's implicit preconditions do not extend with it — you must re-establish each one for the full new set.**

This generalizes the twin RCA's lesson ("gating a feature *off* requires enumerating every path to an engine write") to its mirror: **gating a feature *on* for a wider entity set requires enumerating every precondition the engine's own callers relied on, and re-proving each for the wider set.**

## Why each agent missed these (the fix review)

| Agent | Caught? | Why |
|---|---|---|
| 1 — Standards | Found #3 | Line-count is mechanical; it flagged 156 > 150. Correctly ignored the boundary sealed-type reads as legitimate entry-point conversion. |
| 2 — API Compat | **Found #2** | Its remit — verify signatures against installed DLLs — led it to decompile `PartyAgentOrigin.Banner`, and it read the getter body rather than trusting the `?.` chain. This is the value of "decompile the getter, don't assume the property is a field." |
| 3 — Efficiency | Found #4 | Perf-of-changed-lines; correctly rated the pre-existing double-read LOW and cold-path. |
| 4 — Completeness | Found docs gaps | Confirmed tests present, surfaced the owed RCA/CHANGELOG/feature-doc and that issue #349 already exists. |
| 5 — Data Flow | **Found #1** | Its brief — trace the value to its *actual consumer* — is what caught the slot-0-vs-priority-pick divergence. The other four agents read the guard as self-evidently correct; only the consumer trace exposed that TAOM samples a different agent than the engine renders. Third consecutive review where the data-flow agent found the real bug. |

The **original** native CTD (not the fix) was missed by the feature's own reviews (`rca-banner-bearers-2026-07-16.md`): both static passes plus Codex considered a native crash and concluded *"the real risk is the MixedFormations interaction, not a native crash."* They reasoned about field battles and the player team; none tested a **siege with a custom-faction defender garrison**, where the null-heraldry precondition first bites. Static review cannot see a native precondition that only a specific data shape violates.

## Lessons to codify

**Rule (new): broadening an engine call to a wider entity set re-opens every precondition its vanilla callers relied on.** Before driving an engine method (`SetFormationBanner`, spawn calls, visual rebuilds) on entities vanilla never ran it on — all teams instead of the player, AI parties instead of hero-captained ones, custom factions instead of native — enumerate what the vanilla callers guaranteed (here: a heraldry-backed, player-side formation) and re-establish each guarantee for the full new set, checking the engine's **actual** selection (all candidates), not a representative proxy (slot 0). Appended to [`lessons/adapters-taleworlds-api.md`](lessons/adapters-taleworlds-api.md).

**Reinforced (not new): before trusting a `?.` chain, decompile the getter.** `adapters.md` already says a computed getter throws before your null check — finding #2 is another instance. The durable detection: for any `x?.Prop` where `Prop` crosses into a sealed engine type, read `Prop`'s getter body; if it dereferences an inner object without a guard, `?.` does not protect you.

## Status

- #1 MED — **fixed** ([`FormationBannerHeraldry.CollectCandidateBannerDataCounts`](../../Main/Features/BannerBearers/Hooks/FormationBannerHeraldry.cs) + [`BannerBearerService.HasRenderableHeraldry(IReadOnlyList<int>)`](../../Main/Features/BannerBearers/BannerBearerService.cs)); tests pin the mixed-origin `{3,0,5} → false` case.
- #2 MED — **fixed** (`SafeBannerDataCount` try/catch → 0).
- #3 MED — **fixed** (extracted helper; entry point 136 lines).
- #4 LOW — **rejected with reason** (pre-existing, out of scope).
- Verification: `dotnet test TAOM.Tests` → **4410 passed, 0 failed, 2 skipped**.
- Owed: **in-game siege smoke at Stranding** (the fix is not yet game-verified — the native precondition is confirmed by decompile, not by a live repro run), then close #349.
