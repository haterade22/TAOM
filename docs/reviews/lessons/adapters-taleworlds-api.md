# Lessons — Adapters & TaleWorlds API

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Adapters & TaleWorlds API lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Add a mission behavior to a freshly-opened mission via `OnMissionBehaviorInitialize`, never `Mission.Current.AddMissionBehavior` from a game manager's `OnLoadFinished`
A custom `MBGameManager`/`CustomGameManager.OnLoadFinished` override that opens a battle (`CustomBattleHelper.StartGame` / `MissionState.OpenNew`) does NOT leave `Mission.Current` pointing at the new battle mission when the override's own body continues — so `Mission.Current?.AddMissionBehavior(new MyBehavior())` on the next line **silently no-ops** (the `?.` swallows it) and the behavior never registers. It compiles, throws nothing, and is simply absent at runtime. The engine's dedicated hook `MBSubModuleBase.OnMissionBehaviorInitialize(Mission mission)` hands the mission in directly and is where TAOM adds all its working mission behaviors — add there, gated on whatever scopes it (e.g. a static `IsWalkInProgress`).
- **Why missed:** the first cut of the ShaderPrecompilation 1.4.7 guard (#336) added it from `TaomShaderGameManager.OnLoadFinished`; unit tests can't exercise mission registration, and a deep-review reviews the *corrected* code. Only the in-game `[MissionDiag]` behavior dump (guard absent from the [0]→[83] list) + the NRE still firing revealed it. Moving the add to `SubModule.OnMissionBehaviorInitialize` fixed registration immediately.
- **Prevent:** to add a behavior to a mission you just opened, use `OnMissionBehaviorInitialize`, not `Mission.Current.AddMissionBehavior` from the game-manager callback. If you must add post-open, log-and-assert the behavior is in `Mission.Current.MissionBehaviors` rather than trusting `?.`. When verifying in-game, grep the MissionDiag behavior dump for your class name — absence = it never registered.
- **Source:** docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md (in-game caught; #336)

### Resolve a campaign Kingdom/Clan/Hero by StringId via `Kingdom.All.FirstOrDefault`, NOT `MBObjectManager.GetObject<T>`
`Kingdom.All => Campaign.Current.Kingdoms` (the `CampaignObjectManager`), and every vanilla + TAOM site resolves a kingdom by id with `Kingdom.All.FirstOrDefault(k => k.StringId == id)`. `MBObjectManager.Instance.GetObject<Kingdom>(stringId)` does NOT reliably resolve campaign kingdoms (runtime-created ids via `CampaignObjectManager.FindNextUniqueStringId`, and the campaign object graph is not the raw MBObjectManager registry) — it returns null. A null kingdom then silently degrades: strength lookups return 0, banner/name lookups return blank.
- **Why missed:** a `/deep-review` efficiency finding flagged `Kingdom.All.FirstOrDefault` as an "O(n) linear scan" on a per-battle/daily path and I "optimized" it to `MBObjectManager.GetObject<Kingdom>` — an UNVERIFIED API swap (violating "Research First / Verify Before Reference"). It compiled, all unit tests passed (they mock `IKingdomStrengthAdapter`), and shipped two live-only regressions: the WotR momentum popup's Leaders/Allies banners were blank (kingdom → null) and the daily Relative-Strength award was stuck at 0/0 (every side strength → 0). The scan was never actually hot (~30 kingdoms, daily tick / per battle), so the optimization traded correctness for an imperceptible gain — exactly what `simplicity-criterion.md` rejects.
- **Prevent:** to resolve a campaign object by StringId use the `Xxx.All.FirstOrDefault(o => o.StringId == id)` idiom (Kingdom/Clan/Hero/Settlement) — reserve `MBObjectManager.GetObject<T>(id)` for XML-defined data objects (items, character templates, cultures), and VERIFY it against a decompile before swapping. An efficiency "optimization" that changes an API is a behavior change: verify the new API returns the same thing, or don't make it. A per-battle/daily scan over a small collection is not a hot path.
- **Source:** docs/reviews/rca-wotr-momentum-2026-07-03.md (banner/strength regression, in-game caught)

### Classify a computed getter's safety by reading its body — then BAN confirmed-throwing getters at the IL level
`PartyBase.get_Owner` is `_customOwner ?? (IsMobile ? MobileParty.Owner : Settlement.Owner)`, and `Settlement.Owner => OwnerClan.Leader` has no guard — it throws for any settlement that is neither Village/Town/Hideout (TAOM_Map's `retirement_retreat`). `party?.Owner` at 7 TAOM sites shipped as the deterministic v2.0.8.0 new-campaign CTD (crash 0b462fd8): the settlement daily tick feeds every `settlement.Party` into the healing model. Third shipping instance of the throwing-computed-getter class (#281 `party.Culture`; then the #281 fix itself put `party.Owner?.Culture` INTO the null-safe chokepoint it was building).
- **Why missed:** safety was classified by call-site syntax (`?.` present → "handled") and by analogy to siblings (`Hero.Culture` is a field, so `Owner` must be too) instead of by reading the getter body. The adapters.md rule existed but its example list (`party.Culture`, `MapFaction`) was treated as the complete danger set. A 6-dim deep-review + Codex CLEAN both accepted `party?.Owner`. Text grep for `party.Owner` also missed the 7th site (`attackerSide?.LeaderParty?.Owner` — different receiver name).
- **Prevent:** (1) The classifier is "decompile the member definition", never syntax or sibling analogy. (2) When a getter is confirmed throwing, add it to the assembly-wide IL ban test (`PartyOwnerGetterBanTests` pattern — raw IL walk of every method body in TAOM.dll, immune to receiver naming) instead of re-teaching reviewers per pass. Safe replacements: `MobileParty.Owner` (`=> _partyComponent?.PartyOwner`) via `CareerPassiveHero.ResolveId` for career passives; `MobileParty?.Owner?.Culture` inside `ResolvePartyCulture` for culture.
- **Source:** docs/reviews/rca-party-owner-getter-nre-2026-07-02.md (crash bundle 0b462fd8; regression commit 9034e5dc)

### Check whether a collection-returning API includes the caller
Before using any TaleWorlds API that returns a collection of entities (agents, allies, "nearby X"), decompile at least one vanilla call site and check what post-filtering vanilla applies — in particular, whether the *caller/source* is in the returned set. For "nearby X" APIs, assume the caller IS included unless the name/docs say otherwise; grep call sites at `E:/Decompiled_Bannerlord/`, note what vanilla does AFTER the call (filtering, de-dup, excluding the source), and mirror it or justify the deviation in a comment.
- **Why missed:** Codex review caught that `Mission.GetNearbyAllyAgents(position, radius, team, list)` returns ALL allies in radius *including the source agent*; vanilla callers filter the source out after the call. TAOM didn't, double-buffing the activating hero (once via the hero-buff path, once via the ally-buff path) on every Ranged/Cavalry AoE activation.
- **Prevent:** Treat the *existence* of an API as cheap to verify but its *semantics* (inclusion, ordering, filtering) as something that needs decompilation of a real vanilla call site or actual testing. Same class as `feedback_engine_scale_research.md`.

### Before reusing a shared TAOM decision service, read its implementation AND check for a sibling that already consumes it — its policy semantics differ by caller intent
`IAlignmentService.AreEnemyAlignments(a,b)` reads like "are these two at odds?" but encodes `Neutral is an enemy of everyone` (`AlignmentService.cs:49-50`) — correct for execution-relation penalties, wrong for trade/recruitment permissiveness. CaravanTrade's `SameAlignmentAndNeutral` policy (the shipped default) used `!AreEnemyAlignments`, so it silently BLOCKED trade for every Neutral kingdom (Umbar/battania/shaghana/abanissa) — the exact opposite of its documented "neutrals always tradeable."
- **Why missed:** (1) I reused a shared `IAlignmentService` method by its NAME without reading its body; its Neutral semantics are asymmetric and non-obvious. (2) The **sibling AlignmentRecruitment feature had already hit this exact trap and left a signpost** (`RecruitmentAlignmentService.cs:8-10` doc + `docs/features/alignment-recruitment.md`) — I didn't grep for a prior consumer before reusing the service. (3) The unit test **mocked `AreEnemyAlignments` directly**, so it validated my *assumed* contract, not the shipped logic — a green suite masked the inversion (see Testing & QA sibling rule).
- **Prevent:** Before reusing a shared decision service (`IAlignmentService`, cost/relation/permission models), read the method body AND `grep -rl "IServiceName" Main/Features` for an existing consumer — a prior feature has often already documented the semantic gotcha and the work-around. For alignment specifically: resolve `GetKingdomSide` yourself and branch on `FactionSide.Neutral` per YOUR feature's intent; do not delegate the Free/Evil/Neutral policy to `AreEnemyAlignments`/`AreSameAlignment` unless their exact Neutral handling matches.
- **Source:** docs/reviews/rca-caravan-trade-2026-07-04.md (deep-review data-flow agent caught HIGH; fixed in-session)

### A mount-aware consumer must read the mount-aware ORIGIN, not just the mount-aware agent
`AttackInformation` stores two parallel origins: `VictimAgentOrigin` (= the struck agent's `Origin`) and `VictimRiderAgentOrigin` (= the rider's `Origin`, set only when the victim has a rider). A battle mount spawns via `CreateHorseAgentFromRosterElements` with **no `Origin`** (null), so when the HORSE is struck `VictimAgentOrigin` is null. Any code that resolves the victim's *identity* through the mount branch (`info.IsVictimAgentMount ? VictimAgent.RiderAgent : VictimAgent`) MUST resolve the *origin* (and thus the owning party/leader) through the same branch — `info.IsVictimAgentMount ? VictimRiderAgentOrigin : VictimAgentOrigin`.
- **Why missed:** implementation verified that `AttackInformation.VictimAgentOrigin` *exists* (decompiled it, confirmed the field + the `BattleCombatant` cast) but never traced that the struct keeps the rider's origin in a *separate* field for the mount case. Verifying a member exists ≠ verifying it holds what you need for the branch you're in. The result: `TaomAgentApplyDamageModel.GetVictimTroopLeaderHeroId` used the mount-aware agent but the mount-blind origin, so TroopResistance silently dropped on every horse-body hit of non-hero cavalry — a common, intermittent under-application with no crash.
- **Prevent:** when a sealed struct/engine type exposes a "thing" and a "rider-thing" pair (`VictimAgent`/`RiderAgent`, `VictimAgentOrigin`/`VictimRiderAgentOrigin`), route ALL derivations for the same logical victim through one mount branch — don't mix a mount-aware field with a mount-blind one. Decompile the consumer's ctor (here `AttackInformation`) to find the parallel field, not just the one you first reached for.
- **Source:** docs/reviews/rca-career-phantom-passives-2026-06-26.md
- **Source:** memory/feedback_collection_api_inclusion.md

### `IMbEvent<T>` has no public Remove-one listener API — never suggest `RemoveNonSerializedListener`
`IMbEvent` (non-generic) and `IMbEvent<T>` (all generic arities) in v1.3.15 expose ONLY `AddNonSerializedListener(owner, action)` and `ClearListeners(owner)` (which clears ALL listeners owned by that object). There is NO `RemoveNonSerializedListener`. If a class has one listener on an event, `ClearListeners(this)` is the correct "remove that one." If it has multiple and needs surgical removal, give each listener its own owner-proxy object (per-listener static fields) so `ClearListeners(proxy)` removes only that one.
- **Why missed:** Codex/audit findings routinely recommend "use `RemoveNonSerializedListener` to avoid clearing other listeners" — invalid for v1.3.15 (and likely v1.2/v1.4). Verified via `ilspycmd ... TaleWorlds.CampaignSystem.dll -t TaleWorlds.CampaignSystem.IMbEvent` and `MbEvent`1`; the suggested switch failed to compile.
- **Prevent:** Flag any review suggestion to call `RemoveNonSerializedListener` as an invalid-API finding in TAOM reviews. Document the constraint inline at the call site (as done in `MessengerCampaignBehavior.cs:CleanUpSettlementEncounter`).
- **Source:** memory/feedback_imbevent_remove_one_unavailable.md (Phase 9b #123 Messengers fix, 2026-05-13)

### Vanilla static delegates read parameter-state fields the caller must set first
When invoking a TaleWorlds vanilla static delegate (`SPItemVM.ProcessSellItem`, `ItemVM.ProcessEquipItem`, etc.) from an adapter/service, decompile the receiver body to find what state it reads off the parameter object, and set that state BEFORE invoke. A "sell this item" delegate may read a transaction-count field; a "transfer this stack" delegate may read flag fields (`IsEntireStackModifierActive`) defaulting to false. Surface the required field on the adapter interface (e.g. `IInventoryItemAdapter.StackAmount`), have the adapter set it on the VM before invoke.
- **Why missed:** Codex review #36 (QuickActions, 2026-05-06) caught a `TrySellItem` adapter calling `SPItemVM.ProcessSellItem(spItem, cameFromTradeData: true)`. With `cameFromTradeData=true` vanilla reads `item.TransactionCount` (defaults to 1 from the `SPItemVM` ctor), so a 50-arrow stack sold 1 unit and reported "1 sold" while leaving 49. Invisible to unit tests because the mocks bypassed real `SPItemVM` semantics — the adapter abstraction hid it. Fix: `spItem.TransactionCount = item.StackAmount;` before `del.Invoke(spItem, true)`.
- **Prevent:** Before invoking a vanilla static delegate, decompile it and look for any read off the parameter object (`TransactionCount`, `IsLocked`, `IsEquipableItem`). Tests must assert behavior on real-VM-shaped data (e.g. "stack of 50 reports 50 units affected"), not just `bool` mock returns. If the body branches on internal flags, document which branches your call falls into in a comment.
- **Source:** memory/feedback_static_delegate_reads_param_state.md (companion to `feedback_adapter_modifier_preserving_overload.md`; siblings `feedback_vanilla_reentry_via_bypass_flag.md`, `feedback_route_via_engine_command_when_ui_active.md`)

### Prefer the `(EquipmentElement, int)` overload over `(ItemObject, int)` — the simpler form drops ItemModifier
TaleWorlds inventory/equipment APIs frequently expose parallel `(ItemObject, int)` and `(EquipmentElement, int)` overloads. The simpler form internally calls `new EquipmentElement(item)`, silently discarding `ItemModifier` (durability, quality prefix like "Sharp"/"Damaged", cosmetic item, quest-item flag). When the adapter touches a slot vanilla treats as `EquipmentElement`-shaped, use the richer overload and carry the full `EquipmentElement` (not a string ID or bare `ItemObject`) through the adapter's internal data and snapshot tokens.
- **Why missed:** SiegeDismount's `PartyMountInventoryAdapter` used `roster.AddToCounts(ItemObject, 1)` to deposit the player's mount during a siege; Codex review #34 caught that `roster.AddToCounts(EquipmentElement, 1)` exists in v1.3.15 and preserves the modifier. A "Sharp" horse came back stock — silent persistent equipment-data loss that had been documented as a "known limitation" rather than fixed.
- **Prevent:** Any time an adapter is about to call a `(ItemObject, ...)` API, check for an `(EquipmentElement, ...)` overload of the same method and prefer it. APIs to audit: `ItemRoster.AddToCounts`, the `Equipment[EquipmentIndex]` setter (takes `EquipmentElement` — lossless), `EquipmentHelper.AssignHeroEquipmentFromEquipment`, anywhere `new EquipmentElement(item)` would be the simpler overload's internal expansion. Treat any "modifier/quality/cosmetic lost on round-trip known limitation" as a smell — verify it's inherent before documenting it.
- **Source:** memory/feedback_adapter_modifier_preserving_overload.md (SiegeDismount Codex review #34)

### Mutate hero equipment from inventory only through vanilla `InventoryLogic.TransferCommand`
When a feature swaps items between hero equipment slots and party inventory (load preset, swap loadouts, equip a quest item), route through `InventoryLogic.TransferCommand` + `AddTransferCommands` — NOT direct `equipment[index] = element` mutation, even with the modifier-preserving setter. Build a `TransferCommand.Transfer(amount, fromSide, toSide, elementToTransfer, fromEquipmentIndex, toEquipmentIndex, character)` list and pass it to `inventoryLogic.AddTransferCommands(commands)`. To clear a slot, transfer from the equipment side to `PlayerInventory` with `toEquipmentIndex = EquipmentIndex.None`.
- **Why missed:** Codex review of EquipPresets (2026-05-07) flagged this CRITICAL; Claude's first deep-review missed it because the per-file review focused on modifier preservation and ADR-007 boundary compliance — both of which the direct-mutation code passed. The bug surfaces only when you trace what vanilla does on the same slot op (decompiled `InventoryLogic.TransferItem`, 2026-05-07). Direct assignment conjures the item without consulting the roster (duplication), loses displaced equipment (no auto reverse-transfer), skips `AfterTransfer` slot-VM/mount-harness refresh, and bypasses the `IsItemEquipmentPossible`/`IsItemFitsToSlot`/mount-harness slot-fit gates.
- **Prevent:** Any feature that WRITES hero equipment slots from a flow involving the player's inventory must go through `AddTransferCommands`. Adapters wrapping `SPInventoryVM` expose a single `LoadEquipment(...)` that builds the command list internally so the service stays TaleWorlds-free. Reads via `equipment[i]` indexer (captures) are fine. Narrow exception: one-time equip with no open inventory screen (no `InventoryLogic` to route through) — direct assignment acceptable, document why. Add to AGENTS.md "Bugs Codex catches that Claude misses."
- **Prevent (the `??` corollary):** the `(EquipmentElement, int)` overload is correct only WITHIN an `InventoryLogic` transfer flow — direct `Equipment[i] = element` is the wrong layer of "lossless."
- **Source:** memory/feedback_inventory_mutations_via_vanilla_inventorylogic.md (EquipPresets Codex review, 2026-05-07; sisters `feedback_adapter_modifier_preserving_overload.md`, `feedback_replicate_vanilla_safety_gates_in_prefix.md`)

### Look heroes up via `CampaignObjectManager.Find<Hero>`, not `MBObjectManager.GetObject<Hero>`
Look up a `Hero` by `StringId` with `Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroStringId)`. NEVER use `MBObjectManager.Instance.GetObject<Hero>(stringId)` — it returns `null` for every hero, including the player. The `Hero` ctor (`Hero.cs:1450-1452`, `1455-1463`) registers the hero exclusively in `Campaign.Current.CampaignObjectManager` (via `AddHero(this)`); nothing in `Hero.ctor` or `HeroCreator.CreateBasicHero` (`HeroCreator.cs:294`) touches `MBObjectManager`. Vanilla uses `CampaignObjectManager.Find<Hero>` (`HeroCreator.cs:244`, `Hero.FindHero` at `Hero.cs:2086`).
- **Why missed:** `MBObjectManager` has a typed `Hero` lookup table that compiles, returns the correct type signature, and silently returns null at runtime — invisible at compile time, invisible to adapter-mocking unit tests, surfaces only in-game. Two prior Codex reviews on EquipPresets (modifier-preservation and InventoryLogic-transfer) both missed it because neither asked "is this lookup against the right object manager?" Incident: EquipPresets `Save New` gave "No active hero on the inventory screen" while the modal correctly showed `Hero: main_hero` (the header reads `_currentCharacter` off the live `SPInventoryVM`, bypassing any object manager); fix was a one-line swap on each of two call sites (discovered 2026-05-20 via screenshot).
- **Prevent:** Memorize the split — **MBObjectManager** = XML-defined static catalog (`ItemObject`, `ItemModifier`, `CharacterObject` templates, `PartyTemplateObject`, `CultureObject`, factions, settlements); **CampaignObjectManager** = runtime campaign entities (`Hero`, `Clan`, `Kingdom`, `MobileParty`, `KillRecord`). If you write `MBObjectManager.GetObject<Hero/Clan/MobileParty>`, stop and switch. Add to `docs/reviews/REVIEW-GUIDE.md`: "Every `MBObjectManager.GetObject<T>`/`CampaignObjectManager.Find<T>` uses the correct manager for `T`."
- **Source:** memory/feedback_hero_lookup_via_campaignobjectmanager.md (EquipPresets, 2026-05-20)

### When two agents disagree on a TaleWorlds API signature, re-run `ilspycmd` — don't trust the more confident one
When two agents or two review passes disagree on a TaleWorlds API signature, re-decompile via `ilspycmd` against the installed DLL. Do not pick the more confident/detailed agent. The contradiction itself is the signal to re-verify. Treat any review agent's reported API signature as a hint, not a fact — especially for sealed-type internals: fallback singletons, computed properties, anything using `?.`/`??`.
- **Why missed:** Codex review (2026-05-06, porting LOTRAOM `StartingEquipmentGold` to TAOM 1.3.15) caught a P1 in `PlayerEquipmentAdapter.cs` the Claude `/deep-review` missed. The `taleworlds-researcher` agent confidently reported both `BattleEquipment` and `CivilianEquipment` falling back to `Campaign.Current.DeadBattleEquipment`; the proposed "fix" was applied on that basis. Codex re-decompiled: `CivilianEquipment` actually falls back to `DeadCivilianEquipment` (two separate singletons). The civilian guard never tripped (comparing `DeadCivilianEquipment` against a `DeadBattleEquipment` reference); calling `FillFrom` on an uninitialized-civilian hero would have corrupted the shared `DeadCivilianEquipment` for the session.
- **Prevent:** Verify yourself, e.g. `ilspycmd "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t TaleWorlds.CampaignSystem.Hero | grep -E "BattleEquipment|CivilianEquipment"`. Pair with `feedback_taleworlds_vm_setter_decompile.md` (read the body, not just the signature).
- **Sibling lesson ("may be intentional" dismissal):** the same review caught `shaghana` + `abanissa` missing from `startup_resources_config.xml`. They are full independent kingdoms in Harad (`taom_spkingdoms.xml`, 9 + 8 NPC lords, rulers Taskral/Châjaphân, CC-selectable cultures), not Aserai sub-cultures. The Claude data-flow agent dismissed them as "may be intentional zero-gold cultures"; Codex pushed back and they were genuinely missing. Treat any "may be intentional" without evidence as an open question, and decompile/grep the kingdom XML before classifying unfamiliar IDs, not after.
- **Source:** memory/feedback_codex_caught_api_misread.md (Codex review #34, 2026-05-06)

### A shared cache keyed by `object.GetHashCode()` cross-contaminates instances — key by identity (`ConditionalWeakTable`)
A process-wide cache keyed on `partyBase.GetHashCode()` (or any sealed-type instance's hashcode) is unsafe: `object.GetHashCode()` is not unique per instance, so two different entities can collide and read each other's cached value. If the cache-validity key is *also* non-unique across instances (e.g. `MemberRoster.VersionNo`, which many parties share), the collision produces silent cross-instance value bleed. Use `ConditionalWeakTable<TInstance, TBox>`: reference-keyed (no collision), GC-evicting (no unbounded growth), internally synchronized (safe off the main thread, but not free there: on .NET Framework every lookup takes a lock, and yotthani measured 845 lookups a frame on about 18 worker threads at 7.5 ms of CPU per frame, MithrilForge `docs/engine/perf.md`, nameplate section of 2026-10-03, v1.5.3; decide on the main thread and hand workers a plain array when the lookup sits in a parallel loop). This line runs on the .NET Framework CLR (a 1.4.8 player log records `CLR: 4.0.30319.42000`).
- **Why missed:** the two TroopWeight count-getter hooks shipped a `Dictionary<int GetHashCode(), …>` cache in 2026-03; the sibling display path was migrated to `ConditionalWeakTable` in the 2026-06-07 phantom-wounded RCA, which explicitly flagged the count hooks as "a latent leak, not urgent" (preventive action #2) — and the back-port was never done. It manifested as a plausible-but-wrong lead in the campaign-map "200↔20" flicker investigation (it was a real defect, just not that symptom's cause).
- **Prevent:** never key a cross-entity cache on `GetHashCode()`. When caching per-sealed-instance, reach for `ConditionalWeakTable` first (TAOM already uses it in `TroopWeightService._healthCache`). Extract the cache to a shared, generic, unit-testable helper (`WeightedCountCache<TKey>`) and pin the isolation with a colliding-hashcode test double.
- **Source:** troop-count-display investigation (2026-07-11); `docs/features/troop-weight-system.md` Performance section; RCA `rca-troopweight-phantom-wounded-2026-06-07.md` §2.

### Before blaming a display symptom on your patch, verify which getter the UI surface actually reads
A UI number that looks wrong is not evidence that *your* patch on a same-sounding getter is the cause. Decompile the exact VM refresh path and confirm the property it reads before attributing the symptom. The campaign-map party nameplate reads RAW `NumberOfHealthyMembers` via `SandBoxUIHelper.GetPartyHealthyCount` — NOT the TroopWeight-patched `NumberOfAllMembers`/`NumberOfRegularMembers` — so the on-map count is untouched by the weighting, and three agents' "it's the weighted cache" hypothesis was refuted by reading `PartyNameplateVM.RefreshDynamicProperties` + `GetPartyHealthyCount`. When static analysis can't pin a live display symptom, ship a **sample-gated diagnostic that classifies the mechanism** (army-sum vs cache-poison vs raw-change) rather than guessing — 38 captured events settled it in one session.
- **Why missed:** the obvious feature (TroopWeight re-weights counts) looks like the culprit; it took decompiling the actual `SandBox.ViewModelCollection.dll` nameplate path to prove the number is raw. The 1.4.6 taom-src cache had the type under `-sandbox-vmc`, not the default DLL set — `taom-src` alone reported "not found," which nearly stalled the trace.
- **Prevent:** for any "the count/number is wrong on screen X" report, first answer "what property does X's VM bind, and what getter does that read?" before touching a patch. Diagnostics classify; they don't guess.
- **Source:** troop-count-display investigation (2026-07-11); memory `features/troop-count-display.md`.

### Replacing a gated Harmony-patched getter with an explicit call must replicate the GATE, not just the value
A Harmony getter-patch often carries a toggle (`if (!EnableX) return;`) so the whole feature reverts when off. When you delete that patch and rewire a consumer to call the underlying computation explicitly, you preserve the value but LOSE the toggle — the consumer now behaves as "always on," silently breaking the "off = vanilla" promise for that seam.
- **Why missed:** the TroopWeight rework removed the `NumberOfAllMembers` getter patch (which gated its weighting on `EnableTroopWeight`) and rewired SpecialResources' battle-reward scaling to `CalculateWeightedMemberCount` — which has no toggle awareness. With the feature off, rewards stayed weighted instead of reverting to raw. The author replicated the getter's weighted *value* but forgot the getter's *toggle*. Data-flow deep-review agent caught it (2026-07-11).
- **Prevent:** when replacing a gated patched getter with a direct service call, grep the removed patch for its gate and replicate it at the new call site (`weightOn ? weighted : raw`). Extends the MCM master-toggle-fold check (Agent 5 rule 2b) to the *patch-removal* case, not just GameModel overrides.
- **Source:** `docs/reviews/rca-troopweight-count-to-limit-2026-07-11.md` finding #2.
### `Formation.GetFirstUnit()` is not a culture/identity owner -- it is literally `Arrangement.GetAllUnits()[0]`
BannerBearers (2026-07-16) resolved a formation's culture with `formation.GetFirstUnit()?.Character?.Culture?.StringId` to pick its banner. `GetFirstUnit()` is `GetUnitWithIndex(0)`, which reads `Arrangement.GetAllUnits()[0]` -- an arrangement slot, carrying no semantic meaning about the formation. In a single-culture formation it is right by accident; in a mixed-culture one (an allied Gondor+Rohan army, or a mercenary-heavy player party) the whole formation flies whichever standard happened to be arranged into slot 0, and can differ between deployments. Fixed with a majority-culture vote in the service plus an ordinal tie-break so the result never depends on arrangement order.
- **Why missed:** the code needed *a* culture per formation and took the cheapest one that compiled. "First unit" reads as a reasonable proxy and is correct for the common case, so it looks fine in isolation -- it only misbehaves in mixed formations, which no unit test can construct (no live `Formation`). All 5 deep-review agents accepted it; the API-compatibility agent even verified `GetFirstUnit()`'s null-return behaviour without ever questioning its *semantics*. Signature correctness and semantic correctness are different reviews.
- **Prevent:** when you need a per-formation identity (culture, faction, tier), do NOT sample one unit. Aggregate across `UnitsWithoutLooseDetachedOnes` (majority, with a deterministic tie-break), or take it from a real owner -- `formation.Captain`, `formation.Team.GeneralAgent`, or the party. Generalises: any `GetFirstX()` / `[0]` / `.First()` on an engine collection is a SAMPLE, not a representative; if the code treats it as representative, it is only correct while the collection is homogeneous. Ask "what happens when this collection is mixed?" before using index 0.
- **Source:** docs/reviews/rca-banner-bearers-2026-07-16.md (Codex C1, MED).
### `Type.GetType("T, SimpleAssemblyName")` cannot resolve LoadFrom-context module assemblies in-game
Bannerlord loads module DLLs via `Assembly.LoadFrom` from each module's own bin folder. A partial-display-name bind (`Type.GetType("SandBox.Issues.X, SandBox")`) probes only the appbase (`bin\Win64_Shipping_Client`, which holds no module DLLs), and the engine's `AssemblyResolve` handler (`TaleWorlds.Library.AssemblyLoader.OnAssemblyResolve`) matches **exact FullName only** — so the resolution silently returns null in-game even though the assembly is loaded. Resolve instead by scanning `AppDomain.CurrentDomain.GetAssemblies()` for the simple name and calling `Assembly.GetType(fullName)`.
- **Why missed:** LotrIssues used `Type.GetType` for the 7 SandBox issue behaviors so the list would build in the SandBox-less unit-test host, and `SuppressAll` degraded gracefully (a logged warning) when they didn't resolve. The test host masked the failure in the opposite direction (SandBox.dll IS loadable there), and the in-game "only 36/43 resolved" warning was never read during verification — so all 7 SandBox vanilla issues stayed live in every campaign for a month, until a player CTD'd accepting the daughter quest (its ctor NREs on TAOM's XSLT-deleted vanilla bandit clans).
- **Prevent:** (1) never use `Type.GetType` with an assembly qualifier for module/engine types — scan loaded assemblies by simple name; (2) a graceful-degradation log line that means "feature partially OFF" must be `LogError`, and the in-game verification checklist must include reading it; (3) pin runtime-resolved type names against the real DLL in a test (`ResolveTypesFromLoadedAssemblies_RealSandBoxAssembly_ResolvesAll7`) so an engine bump rename fails the suite instead of silently degrading.
- **Source:** 2026-07-21 player crash report (`report[1].txt`, TAOM v2.0.12) — suppression-gap fix in `LotrIssueSuppression`.
### Broadening an engine call to a wider entity set re-opens every precondition its vanilla callers relied on
Vanilla runs an engine method only where a precondition holds by construction. When a TAOM feature drives that same method on a wider set — all teams instead of the player, AI parties instead of hero-captained ones, custom factions instead of native — the engine's implicit preconditions do NOT extend with it. `BannerBearerLogic.SetFormationBanner` is only called by vanilla for player-side, hero-captained (or OoB-screen) formations, which always have heraldry. BannerBearers (#351) broadened it to **every team's** formations at deployment; a custom-faction garrison with no heraldry gives its bearer a null `Banner`, and the native tableau rebuild (`UpdateSpawnEquipmentAndRefreshVisuals`) access-violates (`0xC0000005 @ TaleWorlds.Native.dll+0x28ac0e`) — a 100%-repro siege CTD, invisible to BUTR (native AV, no managed exception).
- **Why missed:** the feature's reviews (`rca-banner-bearers-2026-07-16.md`) considered a native crash and concluded "the real risk is the MixedFormations interaction, not a native crash" — reasoning about field battles and the player team, never a siege with a custom-faction defender garrison where the null-heraldry precondition first bites. Static review cannot see a native precondition that only a specific data shape (a party with no clan/kingdom banner) violates. The mirror of the twin RCA's lesson: gating a feature *off* requires enumerating every path to an engine write; gating it *on* for a wider entity set requires enumerating every precondition the engine's own callers relied on, and re-proving each for the wider set.
- **Prevent:** before driving an engine method on entities vanilla never applied it to, decompile vanilla's OWN callers, list what they guaranteed about the target (here: player-side + heraldry-backed + hero-captained), and re-establish each guarantee — checking the engine's *actual* selection, not a proxy. The guard's first cut checked slot 0; the engine picks the bearer by priority across ALL candidates (`FindBannerBearableAgents`), so a mixed-origin formation could pass slot-0 and still render a null-banner bearer — **the exact repeat of the "`GetFirstUnit()` is a sample, not a representative" lesson above** (same feature, one week apart). A sample is only valid on a homogeneous collection; a safety guard over a set the engine filters/ranks must cover the whole set. Fixed by sampling every candidate and requiring all renderable; the per-troop read is exception-safe because `PartyAgentOrigin.Banner` is a computed getter that can throw (see the computed-getter rule).
- **Source:** `docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md` (root cause + finding #1); resolves the offset `rca-siege-guards-2026-07-16.md` was blocked on.
### Grep `_shipping_build`, not the dump root, when deciding what vanilla already ships
`E:\Decompiled_Bannerlord\` holds a dual `{_shipping_build,_editor_build}` pair, so a grep across the tree returns editor-only APIs that do not exist in the game TAOM ships against. CLAUDE.md already warns about the *strip* direction — "absent from the dump != doesn't exist" — but the **inverse is equally wrong and easier to act on**: present in the tree != present in shipping. DevConsole Phase 0 inventoried vanilla's console commands with a tree-wide grep and wrote `mission.list_agent_ids` into the feature doc's "already exists, do not reimplement" list; it has **zero** occurrences in `_shipping_build/TaleWorlds.MountAndBlade.cs`. The same grep undercounted the `mission.*` group as 6 when the shipping build has 10.
- **Why missed:** the inventory was a single `grep -r` over the dump root, and the result *looked* authoritative because it was real decompiled source. Nothing in the output distinguishes an editor-only command from a shipping one — the build is encoded in the path, and the path was not read. The count was then written into prose from a glance rather than from a counted command.
- **Prevent:** (1) scope engine-inventory greps to `_shipping_build/` explicitly, or grep both and diff them when the question is "does vanilla have X"; (2) any count that lands in a doc comes from a command run that turn (`| sort -u | wc -l`), never from eyeballing a list; (3) the reviewer-side catch is cheap — a `/deep-review` data-flow brief that says "spot-check the doc's engine claims against the decompile" found this, so keep that instruction in the brief whenever a doc asserts engine facts. Generalises `evidence-over-claims.md` §C from counts/hashes/diffs to **explanatory prose and `///` comments**: "we guard here because X can throw" is a factual claim about the engine and carries the same evidentiary burden as "47 broken refs".
- **Source:** `docs/reviews/rca-devconsole-phase0-2026-07-31.md` findings #1, #2, #6.
### A comment claiming two call sites are "the same" must be made true structurally, not asserted
When a diagnostic reports on what another code path does — a save payload's size, a model's computed value, a limit another service applied — the diagnostic must call the SAME expression, not a copy of it. `MomentumCheats.BuildPayload` duplicated `SyncData`'s `JsonConvert.SerializeObject(store.Serialize())` while its own doc comment claimed it was "the same helper `SyncData` uses". Identical today; nothing prevents divergence. The failure mode is silent and inverted: if the save serialization later gains settings (camelCase, an envelope, `NullValueHandling`), `print_momentum` keeps measuring the OLD, smaller shape and reports comfortable headroom while the real save crosses the int16 archive-entry cliff — the exact false negative the command exists to prevent.
- **Why missed:** the comment described the intent ("shared") rather than the code (a copy), and was written in the same pass as the code rather than after re-reading it. This is the DevConsole Phase 0 root-cause pattern — prose asserting a relationship inferred rather than verified — recurring **one day after** it was written into a lessons file. Writing the lesson down did not prevent the repeat; the review brief naming the specific question ("is this a shared call or a copy, and can they drift?") is what caught it.
- **Prevent:** when you are about to write "the same X as Y" in a comment, extract X so the sentence cannot become false — one expression, two callers. Contrast with the safe case in the same changeset: `TroopWeightCheats` recomputes `ComputeSizePenalty` in its formatter, which is fine precisely because it calls the service's own `public static` method rather than re-deriving the arithmetic. Duplicated *formulas* (the `SettlementEconomy` equilibrium target, re-derived in the cheat from two config floats) carry the same drift risk and want a service accessor.
- **Source:** `docs/reviews/rca-devconsole-phase0-2026-07-31.md` addendum finding A3 (2026-08-01).

### An engine method's COST is a decompile question, never an inference from its name

`FaceGen.GetRaceNames()` returns `(string[])_raceNamesArray.Clone()` — a fresh 15-element array on
every call. `FaceGen.GetBaseMonsterNameFromRace(int)` indexes the same array and allocates nothing.
The names give no hint which is which, and on an agent-spawn path the difference is 648 allocations
per battle load versus zero.

- **Why missed:** the API whose *name* matched the intent ("I want race names") was used without
  opening its three-line body. Reviewing it, the Efficiency agent asked the right question and then
  marked it *"INVESTIGATE — implementation not visible in this codebase"* rather than decompiling —
  while spending its confidence on a different, unverified HIGH. The agent that decompiled
  (Data Flow) found a real defect; the agent that reasoned from plausibility found none and invented
  one.
- **Prevent:** when writing OR reviewing an engine call on a per-agent / per-tick / per-frame path,
  read the method body (`pwsh tools/taom-src.ps1 path <Type>`) before asserting or deferring on its
  cost. Look specifically for `.Clone()`, `.ToArray()`, `new`, and string building inside what looks
  like a plain accessor. An unverified cost claim is reported UNVERIFIED, never HIGH.
- **Source:** `docs/reviews/rca-battleload-agentbuild-2026-08-03.md` finding #1.

### Moving a diagnostic stamp from INFO to DEBUG is a feature change, not a perf tweak

TAOM's `FileLogger` drains INFO synchronously with a flush on the calling thread and leaves DEBUG on
an async queue — precisely so a hard native CTD preserves the tail. A crash-localisation stamp
downgraded to DEBUG disappears from exactly the logs it exists to produce.

- **Why missed:** a deep-review Efficiency agent recommended downgrading `AgentBuildDone` to DEBUG
  on the strength of an unverified disk-cost estimate (it assumed `StreamWriter.Flush()` calls
  `FlushFileBuffers`; it does not — it flushes to the OS file cache). Measured on the live log:
  1287 durable stamps in **145 ms**, ~0.11 ms each, ≈0.5 % of a 9.3 s load. Acting on the finding
  would have silently destroyed the feature while appearing to optimise it.
- **Prevent:** any proposal to change a `[BattleLoad]` / `[MissionDiag]` stamp's log level must
  state what happens to that stamp in a hard CTD. Second time this contract has needed defending —
  `LogTaomBehaviorAdded` carries a code comment for the same reason.
- **Source:** `docs/reviews/rca-battleload-agentbuild-2026-08-03.md` (refuted-HIGH section).

### A hero's battlefield formation ignores `default_group` — the mount decides

`CharacterObject.GetFormationClass()` (`:818-839`, v1.4.7) overrides the base and, when `IsHero`, never
reads `DefaultFormationClass` at all: it returns Cavalry for any `HasHorseComponent` item in
`EquipmentIndex.Horse`, HorseArcher if that hero also carries a Bow/Crossbow. For a **troop**, the base
`BasicCharacterObject.GetFormationClass()` (`:543`) *does* return `DefaultFormationClass`. So the same XML
attribute is authoritative for one kind of character and inert for the other.

- **Why it matters:** the obvious way to enforce "these lords never fight mounted" is to audit
  `default_group`. On lords that check is decorative — it governs the party-screen icon and tooltips, not
  the battle. A lord tagged `Infantry` holding a horse still spawns mounted, in the Cavalry formation.
- **Prevent:** for any never-mounted rule, audit the **equipment** (Horse slot), and treat the enum as a
  separate, UI-level concern worth fixing but never sufficient. Two further traps in the same code path:
  `EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are both `10` (the innocuous-looking armor
  read *is* the horse read), and `CharacterObject.Equipment` resolves to live `HeroObject.BattleEquipment`
  for heroes — so a mount acquired at runtime is invisible to any static XML validator. `Mission`'s unused
  `GetAgentTroopClass_Override` event (`:1555`) is the patch-free hook if that hole must be closed.
- **Also worth knowing:** an unparseable `default_group` deserializes to `-1`, not to the Infantry default
  (`BasicCharacterObject.cs:534`) — an undefined enum every downstream `switch` falls through silently.
  This is what the validator's `INVALID_ENUM` check is actually protecting against.
- **Source:** dwarf-lord formation audit, 2026-08-04 — decompile via `taom-src` against installed v1.4.7.
  Process doc: `docs/reference/engine/formations-and-team-ai.md` "Which formation a spawned agent joins";
  resulting gate: `MOUNTED_DWARF` in `docs/features/moduledata-validation.md`.

---

### Never drive a one-way engine state backward through a setter that allows it (Fords of War towers, 2026-10-31)
On the 1.5.x line, Fords of War tower regeneration raised `DestructableComponent.HitPoint` on a damaged tower. The engine derives the shown
damage stage from `HitPoint` on every hit (`CalculateNextDestructionLevel`) (v1.4.8 `DestructableComponent.cs:399-412`, the read at `:498`), so a healed tower computed stage 0 and
`ReplaceEntityWithBrokenEntity` read `_destructionStates[-1]` after it had hidden the model and turned its physics
off. The exception was swallowed and the tower stayed invisible and unhittable.
- **Why missed:** the setter is public and takes any value, so the adapter treated `HitPoint` as plain data. The
  stage swap is one-way (debris is detached and thrown), which only the method bodies show. This is the EFFECT form
  of this file's member-name lesson, the third time in this feature (RCA rows D2, D6).
- **Prevent:** before a TAOM write raises or resets an engine value, read every engine method that reads it and
  name the state it drives; if that state only moves one way, use the engine's own hook (here the public
  `OnCalculateDestructionStateIndex`, which `CastleGate` uses) or do not write. Rule: `.claude/rules/adapters.md`
  "A destructible's shown stage must only move forward".
- **Source:** 1.5.x `docs/reviews/rca-fords-of-war-2026-10-31.md`, night addendum, row R2 (not on this line); plan 144.

### The engine's gates ignore visibility, so read a scene's hidden gates before you trust a siege lane

`TeamAISiegeComponent` takes `InnerGate` and `OuterGate` by tag from all active gates, with no visibility check, and a hidden `CastleGate` returns from `OnTick`, so it never opens (taom-src `TeamAISiegeComponent.cs:54-57`, `:184`; `CastleGate.cs:609-622`; `BehaviorAssaultWalls.cs:122`). On the 1.5.x line a Helm's Deep scene hid an inner gate at height 32.5. The Middle lane therefore stays closed, and `BehaviorAssaultWalls` stays in `AttackEntity` with soldiers standing at a gate that nothing draws. I had filtered hidden gates out of my own log line, which hid the cause, and I told a reader the cause was constant team power.

- **Why missed:** the log filter came from a correct wish, to show only real gates. The false reason sounded right, and it did remove the other ways out.
- **Reach (the 1.5.x line's scan of the desktop's live `TAOM_Map`, 2026-11-04; not re-scanned on this laptop, in-game check 3):** 5 of the 30 scenes with castle gate prefabs hide an inner gate: `taom_rohan_castle_helms_deep_forceatmo` and `taom_gondor_castle_001_forceatmo` have no other inner gate, and `taom_gondor_castle_002_forceatmo`, `taom_gondor_castle_003_forceatmo` and `taom_gondor_town_lossarnach_forceatmo` have a second, visible one. Nobody has checked what a campaign siege does on these scenes.
- **Prevent:** before a siege feature trusts a scene, list every `CastleGate` in it with its visibility and tag. Log what the engine uses, not only what the player sees. Do not delete a hidden inner gate: a null `InnerGate` throws in `BehaviorAssaultWalls`.
- **Source:** 1.5.x `docs/reviews/rca-long-defeat-spike-2026-11-03.md`, rows 31 and 37 (not on this line); plan 144.

### `SetDoNotMakeNewDecisions(true)` does not stop a party's own initiative everywhere (#870)
v1.4.8 `MobilePartyAi.GetBehaviors` (from `:477`) applies the initiative result despite the flag when the best
initiative target is in a map event at, or besieging, the party's `TargetSettlement` (`MobilePartyAi.cs:486`; it then needs a score above 1,
`:488`). On the 1.5.x line a pinned Fire and Tribute host attacked a friend this way. On this line `SupplyCaravanService.PinAi`
and `RefugeService` pin parties with the flag; a supply caravan whose destination is besieged can therefore take initiative
despite its pin (UNVERIFIED in game; a refuge holds position, so its `TargetSettlement` is probably null).
- **Why missed:** "makes no new decisions" was taken from the pin call without reading the branch that consumes the flag.
- **Prevent:** before relying on `DoNotMakeNewDecisions` to silence a party, read `MobilePartyAi.GetBehaviors`; a rule about a
  pair of parties in a `MobilePartyAIModel` checks both directions.
- **Source:** 1.5.x `docs/reviews/rca-fire-and-tribute-own-side-2026-10-27.md` finding 4 (not on this line); plan 144.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)
- [docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md](../rca-banner-bearers-siege-ctd-2026-07-23.md)

<!-- backlinks-end -->
### Prove a count/consume adapter pair ranges over the same set — decompile BOTH members

**Why missed:** Enlistment's food-delivery duty (2026-08-05) paired `CountPlayerFood()` →
`ItemRoster.TotalFood` with `ConsumePlayerFood()` → removes `Item.IsFood` stacks. The names look
symmetrical and both call real engine members, so every review that checked "do these APIs exist and
are they used correctly" passed. But vanilla's `TotalFood` folds livestock in via
`item.HorseComponent.MeatCount` (`ItemRoster.cs:452`) while the consume side can only touch `IsFood`
items — so a player driving cattle satisfied the requirement and handed over nothing, completing the
duty for free. Caught by the Codex pass, which decompiled the property instead of the method calling it.

**Prevent:** when an adapter exposes a count/consume, read/write, or check/apply pair over the same
engine collection, decompile BOTH members and prove they range over the same set. A reader that is a
SUPERSET of what the writer can act on is a silent free-completion bug; a reader that is a subset
silently blocks a legitimate action. Where practical, make the writer return what it actually did and
gate completion on that return value rather than on the earlier read — then the two can't disagree.

**Source:** `docs/reviews/rca-enlistment-content-2026-08-05.md` Codex finding C2.

### Pinning an engine callback's SIGNATURE is not understanding its PARAMETERS — state what each flag means for your feature

**Why missed:** The career damage-attribution override pinned `MissionBehavior.OnScoreHit` against
the installed DLL character-for-character (the two `in` params silently no-op a wrong override, so
the signature got real scrutiny) — but `isSiegeEngineHit` rode through unexamined. The dispatch site
(`Mission.OnAgentHit`) sets it for siege-engine missiles whose affector IS the operating player, so
the un-filtered override would have printed "+N from ability" on ballista hits the agent-stat buff
never touched. Signature verification answered "will this override bind?"; nobody asked "what does
each parameter mean for THIS feature?"

**Prevent:** when overriding any engine callback, enumerate its parameters and write one line per
flag/edge parameter stating how the feature handles it (act on it, ignore it deliberately, or
return early). A parameter you can't classify is research owed before shipping. Counterpart caution
from the same review: do NOT blind-adopt a reviewer's "vanilla normalizes X" claim — the quoted
mount→rider normalization did not exist in the installed `Mission.cs` (Codex C1b, disputed).

**Source:** `docs/reviews/rca-career-ux-arc-2026-08-05.md` Codex finding C1a.

### `MBObjectManager.GetObject<Hero>` cannot resolve a hero built by `HeroCreator` at runtime

`HeroCreator.CreateSpecialHero` ends in `new Hero(stringId, character, birthDay, deathDay)`, whose
constructor calls `Campaign.Current.CampaignObjectManager.AddHero(this)`. `AddHero` hand-assigns
`hero.Id = new MBGUID(32u, GetNextUniqueObjectIdOfType<Hero>())` and appends to
`CampaignObjectManager`'s own `_aliveHeroes` list. It never calls `MBObjectManager.RegisterObject`,
which is the only thing that populates the `StringId`-keyed dictionary `GetObject<T>(string)` walks.
So the lookup compiles, reads correctly, and returns null for every runtime-created hero, forever.

**Why missed:** the call site was a validity predicate (`IsHeroAliveAndValid`) whose failure mode is
silence — it just reported every promoted companion invalid, and the load-time prune obediently
emptied the list. Every OTHER hero-lookup adapter in the repo uses `Hero.AllAliveHeroes`; this one
was the outlier and nobody diffed it against its siblings.

**Prevent:** for any lookup of an entity your own code created at runtime, ask which registry the
CREATION path wrote to, not which registry has the convenient API. When a repo has several adapters
doing the same lookup, an inconsistent one is the finding — grep for the siblings before writing a
new one. `Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id)` is TAOM's convention.

**Source:** `docs/reviews/rca-field-commission-2026-08-07.md` finding 5.

### A plausible engine root cause is still a hypothesis — the fix built on it can be pure waste (Enlistment status board, 2026-08-08)

A design doc explained a frozen wait-menu sentence like this: `GameMenuVM.IsMenuTextChanged`
compares the menu `TextObject`'s `Attributes`; `MBTextManager.SetTextVariable` writes a GLOBAL that
never lands there; therefore the text is structurally frozen and raising the refresh cadence cannot
help. Every individual claim in that chain is TRUE — and the conclusion is wrong.

The method's second check is `_menuTextAttributes.Count` (an `int`) against
`_menuText?.Attributes?.Count` (an `int?`). Our menu text is `new TextObject(text)` with no
attributes, so that comparison is `0 != null` — **always true** under C#'s lifted equality (verified
by compiling and running it, not by reasoning about it). `IsMenuTextChanged` therefore returns true
on every call, the menu re-renders every frame, and `.ToString()` re-resolves the global each time.

Nothing was frozen. The sentence never changed because `RefreshWaitText` ran only at menu init and
its one token was the commander's name. The proposed fix —
`args.MenuContext.GameMenu.GetText().SetTextVariable(...)` — is real, compiles, targets the right
instance, and would have changed nothing.

**The rule:** when a root cause is a chain of engine facts, verify the CONCLUSION against the
installed DLLs, not just the links. Degradation direction is the usual trap: "this comparison can't
see my change" and "this comparison always reports a change" produce opposite fixes from the same
premise. If the answer turns on a language rule (nullable lifting, NaN comparison, integer
overflow), compile it and run it — that is cheaper than a batch of wasted work.

### An approximation of an engine gate must say so, or it becomes a false authority (Enlistment, 2026-08-08)

`PlayerPresenceSnapshot.EncountersBlocked` reproduced `EncounterManager.HandleEncounterForMobileParty`'s
refusal conditions and cited the engine line number — but omitted a whole disjunct (a BESIEGING
party is refused unless `ShortTermBehavior == AssaultSettlement`) and flattened another (the
`MainParty && PlayerEncounter.Current != null` term is nested under
`!IsCurrentlyEngagingParty && !IsCurrentlyEngagingSettlement`, not top-level). It under-reports
sieges and over-reports encounters.

Harmless while nothing branches on it — it exists to make a log line legible — but the line-number
citation makes it read as authoritative, and the next person to need a real gate will reach for it.

**The rule:** a deliberate approximation of engine logic states that it is one, names what it
omits, and says what must be fixed before promotion to a decision input. A precise-looking citation
on an imprecise copy is worse than no citation.

### When you skip an engine initializer, audit what READS the field it would have set

Constructing an engine object through a bare constructor to avoid an initializer's side effects
leaves every field that initializer would have populated at its default. Verifying that the default
makes things INERT is only half the audit, inertness is what the field fails to *drive*. The other
half is what *reads* it, and a reader has no reason to guard a field the engine's own construction
path always fills.

- **Why missed:** `ArmyMembershipAdapter` uses `new Army(...)` rather than `Kingdom.CreateArmy`,
  because `CreateArmy` calls `Gather()` and would march the commander off the battlefield. The review
  confirmed the resulting null `AiBehaviorObject` keeps the army's siege and owner-change handlers
 inert (they all gate on `AiBehaviorObject is Settlement`) and wrote exactly that into the doc
  comment and the feature doc. Nobody grepped for readers. Five of the seven cases in
  `Army.GetLongTermBehaviorTextForAILeadedParty` cast and dereference it unguarded, reached from the
  map party tooltip (`MobileParty.GetBehaviorText`) and the kingdom Armies tab (`KingdomArmyItemVM`);
  `_aiBehaviorObject` is `[SaveableField(16)]`, so the crash survives every reload, and
  `Army.CheckInactivity` *decrements* the inactivity counter for a besieging leader, so the army
  never times out on its own either.
- **Prevent:** after choosing a bare constructor over an engine factory, list the fields the factory
 would have set and grep the engine for every READ of each, not just the writes and the gates. If
  any reader dereferences without a guard, the object must not outlive the narrow window it was
  created for. Pin it with a `[TestCategory("BindingVerification")]` test asserting the unguarded
  member still exists, so an engine bump that adds the guard tells you the workaround can relax.
- **Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #6.

### A field's unguarded readers are not confined to the class that declares it

Grepping the declaring type for reads of a field you are leaving at default finds the readers that
type owns. It does not find the ones in behaviours, view models, conversation conditions, or sibling
engine types, and those are usually the reachable ones, because they sit behind ordinary player
actions rather than internal ticks.

- **Why missed:** the review of TAOM's bare-ctor `Army` enumerated every read of `AiBehaviorObject`
  inside `Army.cs` and concluded the exposure was the map tooltip and the kingdom Armies tab. Two
  more readers lived elsewhere: `LordConversationsCampaignBehavior` (reads
 `Army.AiBehaviorObject.Name` gated only on `IsWaitingForArmyMembers()`, which is true FOREVER for
 a bare-ctor army, so talking to any lord in it is an unconditional CTD) and
  `MobileParty.CheckAiForMapChangeAndUpdateIfNeeded` (branches on the field being null, then
  dereferences it on that same branch). The first is reached by an action the feature's own menu
  offers.
- **Second half: check what WRITES the state you are reasoning from.** The same review argued
  certain switch cases were unreachable-with-a-null because the engine writes the objective whenever
  it sets that behaviour. True for every case but one: `SetPartyAiAction`'s `PatrolAroundPoint`
  branch sets `DefaultBehavior` without writing `AiBehaviorObject`. Reachability arguments built on
  "the engine always sets both" must be checked against the writer, not assumed from the readers.
- **Prevent:** grep the whole decompiled tree for the member name, not the declaring file, then
  classify each hit as guarded or not, and record the list in the feature doc so it is never
  re-derived. And prefer making the invariant a property of the OBJECT (seed the field) over
  enumerating the paths that could observe it violated; the enumeration is only ever as good as your
  grep, while the seed is true by construction.
- **Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #13.

### Heroes and prisoners cross parties only through engine Actions, never raw roster copy+clear

TroopRoster.AddToCountsAtIndex fires OwnerParty.OnHeroAdded/OnHeroRemoved, and Hero.OnRemovedFromParty
sets PartyBelongedTo = null UNCONDITIONALLY, regardless of where the hero was just added. A raw
copy-then-clear therefore re-parents the hero row and then nulls his party binding, and the desync
persists into the save. Regular troops are data; a hero row is an entity binding with unordered
side-channel callbacks. Use AddHeroToPartyAction / the captivity transfer actions.

**Why missed:** the source module did the raw copy and the port was faithful; no rule said
otherwise. **Prevent:** this rule; and in review, for every roster bulk move ask what vanilla does
at its own equivalent site.

**Source:** docs/reviews/rca-yotthani-camps-2026-08-23.md Class 2 (CRITICAL: refuge dismantle).

### A party attached to a MapEvent is engine-owned: never destroy, deliver from, or teleport it

Vanilla always detaches (MapEventSide = null) before DestroyPartyAction on its own parties.
Destroying a party mid-event leaves the event ticking over a removed party. A feature timer that
"completes" a party's job (delivery, timeout) must Continue while the party fights; defeat resolves
honestly through the party ceasing to exist. Corollary: IsRaid is the settlement-raid battle TYPE,
not "is being attacked": a field battle never sets it, so a raid-gated loss branch is dead code.

**Source:** same RCA, Class 2 + Class 6 (delivery-vs-MapEvent; the dead IsRaid input).

### A vanilla action dispatches events; enumerate the OTHER listeners before treating the call as a leaf

The design reasoned carefully about what `ChangePlayerCharacterAction.Apply` itself does and never asked what subscribes to the events it fires. Vanilla SandBox ships `HeirSelectionCampaignBehavior`, which snapshots the old main party's `ItemRoster` and the old hero's battle and civilian equipment on `OnBeforePlayerCharacterChanged` and adds both to the new main party on `OnPlayerCharacterChanged`. Two consequences, both invisible in our code: the throwaway character-creation hero's startup gear survives onto the taken-over lord, and on the adoption path our own roster transfer added items vanilla had already moved, doubling every stack.
- **Why missed:** every reviewer traced TAOM-internal data flow. This is vanilla-internal flow reacting to a TAOM call. `.claude/rules/csharp-architecture.md` "GameModel Cross-Entity Propagation" encodes exactly this instinct but triggers only on a `GameModel` override returning a per-entity value, and this feature has no GameModel.
- **Prevent:** for any vanilla `*Action` call, find the events it dispatches and read every subscriber before assuming the call is self-contained. `CampaignEventDispatcher` makes it mechanical: find the event, find its subscribers, read what they do. Treat this as the non-GameModel form of the cross-entity propagation rule.
- **Source:** docs/reviews/rca-player-switcher-2026-08-27.md finding 7 (#514; Codex P2).

### Subclassing a vanilla ViewModel inherits its unguarded constructor, not just its bindings

A picker row derived from `ClanPartyMemberItemVM` deliberately, so that an engine change removing it would break the build rather than blank a row. That base takes `(Hero, MobileParty)` and its constructor's first statement is `IsLeader = hero == party.LeaderHero;` with no null check. Wanderers have no `PartyBelongedTo` and shipped enabled by default, so the first wanderer threw inside the panel build, the attach patch's try/catch swallowed it, and the entire feature's UI silently never appeared.
- **Why missed:** the binding test asserted the base type was unsealed and had the expected constructor SIGNATURE. Nobody read the constructor BODY. Standards and API-compatibility agents both passed it correctly: the type exists, is unsealed, and its signature is unchanged.
- **Prevent:** before deriving from an engine VM for compile-time safety, read its constructor body for unguarded dereferences of arguments you may legitimately pass as null. Inheritance bought for a build-time gate is worth nothing if the base cannot accept your data. When you drop such an inheritance, pin the reason in a test so the "simplification" is not reintroduced.
- **Source:** docs/reviews/rca-player-switcher-2026-08-27.md finding 5 (#514; Codex P1).

### An early-return guard is evidence about the entity it names, not the category it belongs to

`BuyFoodInternal` opens with `if (mobileParty.IsMainParty) return;`. That was read once and generalised into "vanilla's auto food-buy never runs for the player", which then justified withholding food relief from player clans and was restated in a feature doc, a CHANGELOG entry, a doc-comment and a test comment. The caller, `TryBuyingFood`, has no clan gate at all, so every player-clan COMPANION party runs the auto-buy and starves exactly as an AI party does. `IsMainParty` is not "the player", and the parties the claim was used to reason about were precisely the ones it excluded.
- **Why missed:** the guard was read, the caller was not. Four artifacts then cited the same line number, so the repetitions read as corroboration when they were one unverified read copied forward. A deep-review agent recorded the claim while explicitly noting it had not re-verified it against the engine; that hedge was the signal and it was not acted on.
- **Prevent:** before turning a guard into a claim about a class of entity, open the CALLER and enumerate which members of that class actually reach the guard. And treat a claim repeated across N artifacts as one claim needing one verification, not N corroborations. A subagent's "not independently re-verified" is a request to verify, not a footnote.
- **Source:** docs/reviews/rca-ai-party-size-player-clan-2026-09-01.md finding 4 (#532).

### A doc that re-derives VANILLA behaviour is not documenting TAOM behaviour

`docs/features/clan-heraldry.md` traced the armour-tint chain correctly through `Mission.SpawnTroop`, `PartyBase.PrimaryColorPair` and `Clan.MapFaction`, cited three exact line numbers, and concluded that battlefield armour follows the KINGDOM colour. Every cited fact was true. The conclusion was false, because TAOM prefixes `Mission.SpawnAgent` and rewrites `ClothingColor1/2` from the party leader's clan with no `MapFaction` hop, and that prefix runs last. The doc stood for three months, and the same wrong conclusion was copied into a CHANGELOG entry and a cross-reference in a second reference doc.
- **Why missed:** the research question was "what does the engine do", and it was answered rigorously. Nobody asked the second question, "and what do we do to it". A decompile is only half the evidence when the repo patches the method in question.
- **Prevent:** before writing down what the engine does with a value, grep TAOM's own patch set for the method AND for the value. `grep -rn "MethodName" Main/Features/*/Hooks/` and `docs/reference/harmony-patch-registry.md` each answer it in one command. If there is a hit, the doc must say which writer wins and why, not just what vanilla does.
- **Source:** doc audit 2026-09-02, `docs/features/clan-heraldry.md` vs `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs`.

### Two agents disagreeing about a merge is a question about WHICH LAYER merges first

Auditing TAOM's `banner_icons.xml`, one review concluded Native's palette wins a colliding `<Color id>` (citing `if (!_colorPalette.ContainsKey(key))` in `BannerManager`, first-writer-wins) and another concluded TAOM's wins. Both read real code. The guard is real but never fires: `MBObjectManager` merges every module's XML into ONE document first, and `BannerIcons.xsd` marks `BannerColors` `AlwaysPreferMerge` with `@id` unique, so `MergeElementAttributes` overwrites in place, last-writer-wins, with TAOM loading after Native. By the time `BannerManager` runs there is exactly one node per id, so `ContainsKey` is always false. 46 ids are affected, 15 of them referenced by a live `banner_key`.
- **Why missed:** the loop that looks like the merge is downstream of the merge that actually happened. Reading the consuming class is the obvious move and it is one layer too late.
- **Prevent:** for any "which module's data wins" question, resolve it at the XML merge layer (`MBObjectManager.MergeElements` plus the file's `XmlSchemas/*.xsd`), not at the consuming manager. It is settleable empirically: load `TaleWorlds.ObjectSystem.dll` and call `MergeTwoXmls` on the two real files, then read the merged node back. A rendered screenshot is also valid evidence and agreed with the empirical result here.
- **Source:** Gondor clan-colour pass 2026-09-02; `clan_empire_west_2` was briefly given Native's `FF2A5599` instead of TAOM's `FF30336B`.

### A `FindXxx` engine method may return a formatted ERROR object, not null

`GameTextManager.FindText` never returns null or empty for a missing entry. On a `TryGetText` miss it returns `new TextObject("{=!}ERROR: Text with id " + id + " doesn't exist! Variation: " + variation)`, which renders as that whole sentence. A guard written as `string.IsNullOrEmpty(text) ? fallback : text` around it is therefore dead code, and the fallback can never run. TAOM shipped exactly that guard on the career ability key chip; with a key Native has no `str_game_key_text` entry for (`InputKey.Extended` is one), the mission HUD would have displayed "ERROR: Text with id str_game_key_text doesn't exist! Variation: extended".
- **Why missed:** the guard was written from the method NAME plus an assumption about how a "find" behaves on a miss, and the body was never read. Vanilla's own `GameKeyOptionVM.RefreshValues` calls `FindText(...).ToString()` with no guard at all, so nothing in the surrounding code contradicted the assumption.
- **Prevent:** read the body before writing any null-or-empty fallback against an engine lookup, and prefer the `TryGetXxx` sibling where one exists (`GameTextManager.TryGetText` is public and has an honest success flag). Vanilla calling something unguarded is NOT evidence the unguarded call is safe: Native gets away with it only because it ships the strings for the whole standard keyboard, which is data coverage, not a contract.
- **Source:** #533 rebindable career ability key, 2026-09-03; RCA `docs/reviews/rca-career-keybind-2026-09-03.md` finding 2.

### Vanilla's own "can advance" predicate can pass an EMPTY menu straight into a throwing indexer

`CharacterCreationNarrativeStageVM.CanAdvanceToNextStage()` is `if (SelectionList.Count != 0) return SelectionList.Any(s => s.IsSelected); return true;`, so a narrative menu offering zero options reports that advancing is fine. `CharacterCreationManager.TrySwitchToNextMenu()` then opens with `SelectedOptions[CurrentMenu].OnConsequence(this)`, a dictionary indexer, which throws `KeyNotFoundException` because nothing was ever selected for that menu. An empty narrative menu is thus a latent vanilla crash, and any TAOM code that drives the chain programmatically inherits it in full.
- **Why missed:** the walk was designed around "select an option, then advance" as a matter of leaving `SelectedOptions` populated for the review stage, which is a data-shape argument. That framing makes selecting look like tidiness with a cosmetic downside if skipped, and hides that it is the only thing standing between a sparse culture's menu and a hard throw.
- **Prevent:** never treat an engine `CanXxx` predicate as a safety gate for your own call. Read what the guarded method does on the state the predicate admits, and re-derive the guard from that. When walking any engine state machine, the zero-option / empty-collection hop is the case to write first, and its abort must be a tested path rather than a comment.
- **Source:** Patch78 player-switcher career fast path, 2026-09-03; pinned by `NarrativeCareerFastPathServiceTests.SkipToCareerMenu_NoSuitableOptionMidChain_AbortsAndLogs`.

### A rule written from a crash must say which engine version it was verified against, and name the mechanism rather than a precedent

`.claude/rules/gui-ui.md` said adding to `MapInfoVM.SecondaryInfoItems` throws `IndexOutOfRangeException` in `GauntletMapBarGlobalLayer.HandlePanelSwitchingInput` through positional indexing. On the installed v1.4.8 that method is six `IsGameKeyReleased` branches with no collection access at all, and `SecondaryInfoItems` is referenced by `MapInfoVM` alone across every dump on the machine. The claim was read as fact on 2026-09-04 and nearly drove a rewrite of a working feature to cure a defect that does not exist. The same session justified an apply-timing decision as "the Patch62 reasoning"; Patch62 targets a root `bin` assembly and the new target lives in Native's module `bin`, so the analogy said nothing, even though the timing happened to be safe for a different reason (Native's `SubModule.xml` declares `GauntletUISubModule` from that DLL and TAOM loads Native `LoadBeforeThis`).
- **Why missed:** the rule recorded a crash and a mechanism but not the engine version it was seen on, so nothing in the engine-bump procedure could flag it as stale: `/verify-bindings` checks code bindings, not prose claims about engine behaviour. The precedent failure is the same shape one level down: a justification that names a precedent instead of the mechanism cannot be checked against anything.
- **Prevent:** when a rule, registry entry or comment asserts engine behaviour, name the method, cite the decompiled lines, and stamp the version and date. After an engine bump, grep `.claude/rules/` and `docs/reference/harmony-patch-registry.md` for the previous version string and re-verify each hit. Never justify by precedent name; state the mechanism the precedent relied on and confirm it holds for the new case.
- **Source:** deep-review of Patch79 tooltip diagnostics, 2026-09-04; RCA `docs/reviews/rca-tooltip-diagnostics-2026-09-04.md` findings F4 and F5.

### A verified paragraph does not verify the proper nouns inside it: name the enclosing method from the decompile, not from memory
When documenting an engine hazard, each type/method/field name is a separate assertion needing its own evidence, even when the surrounding prose was carefully verified. Patch82's doc comment, the feature doc and the harmony registry all named `MapEventSide.CheckSimulationSkillUpgrades` as the site of two unguarded `TroopUpgradeTracker` dereferences. The line numbers (:1050, :1056) and the described behaviour (an early `if (BattleObserver == null) return;` then two unguarded derefs) were exactly right; the method does not exist. The real enclosing method is `ApplySimulatedHitRewardToSelectedTroop`. A future crash-triage reader greps the name and finds nothing.
- **Why missed:** the author had just correctly read the dereference SITES and never scrolled up to the enclosing signature, then wrote a plausible name. This is the inverse of the usual fabrication trigger: `.claude/rules/evidence-over-claims.md` §C fires loudest when you know you are ignorant, and this happens when you feel informed. Proximity to verified truth is what makes the guess feel safe.
- **Prevent:** when a doc cites `Type.Method :line`, grep the method name in the decompile before shipping the sentence. One command, and it is the half of the claim a reader will actually navigate by. Cheap tell: if you can quote the body but cannot point at the line where the signature is, you have not read the signature.
- **Source:** docs/reviews/rca-map-event-observer-2026-09-06.md, #551. Caught by the deep-review compatibility agent decompiling `MapEventSide` in full rather than the cited lines.

### In a custom battle, shader coverage is roster membership, not spawn count
`MissionCustomBattlePreloadView.OnPreMissionTick` hands EVERY `CustomBattleCombatant.Characters` entry of every combatant to `PreloadHelper.PreloadCharacters`, which walks every battle equipment set, registers every item mesh, and calls `MetaMesh.PreloadShaders` per unique mesh before the first frame. The Battle Size option caps how many agents spawn; it does not touch what gets preloaded. The 2026-06-17 RCA assumed the opposite ("the engine caps render size, so not every troop compiles") and deferred a fix that was never built, while the real defect was the 3,000-per-side roster cap with soldiers added twice, which overflowed on a 4,600-plus roster and dropped TAOM's own characters because vanilla enumerates first. Two corollaries: adding a character twice buys nothing (the preload dedupes meshes), and a party under 20 entries keeps `CanPlayerSideDeployWithOrderOfBattle()` false, so deployment auto-finishes without the Order of Battle screen (and, in Custom Battle, the base `OrderOfBattleVM`'s `SaveConfiguration()` is empty, so no profile is written on either path; a first draft claimed the opposite from the campaign subclass).
- **Why missed:** the limitation was recorded from a plausible model of the engine instead of from `PreloadHelper`; nobody grepped the mission's view list for what it preloads. A recorded-and-deferred limitation reads as understood, which is exactly when it stops being checked.
- **Prevent:** before capping or batching anything that feeds a mission, read the mission's `[ViewMethod]` list and its `MissionLogic` set for the preload path and ask what it iterates. Log the roster arithmetic (`loaded k of k ids, unresolved, skipped`) so a drop is a WARNING line, not a user report.
- **Source:** #560, 2026-09-11; `Native__TaleWorlds.MountAndBlade.View.cs:26748-26772` and `:3483-3491` (v1.4.8 dump); `docs/reviews/rca-shaderprecompilation-2026-06-17.md` finding 5.

### Attribute an engine side effect to the subclass the mission actually constructs
`OrderOfBattleVM.SaveConfiguration()` is an empty virtual in the base class; the SandBox subclass is the one that writes the player's Order of Battle profile. Custom Battle constructs the base VM (`CustomBattleViews`: `new OrderOfBattleVM()`), so the "force-finishing deployment writes a junk profile" claim that justified part of the #560 battle shape was false for the mission in question. The shape was still right, for the other reason (the deployment view would wait for a click nobody makes), but a false rationale in three docs and two comments is the kind of thing a future reader builds on.
- **Why missed:** the pre-implementation engine-verification agent reported the side effect from the campaign subclass; the orchestrator verified the gate (`OnDeploymentFinalized(bool)`) but not the callee's body in the concrete VM, and relayed the claim into the docs (`.claude/rules/evidence-over-claims.md` A.4). The deep-review compatibility agent then verified the same gate.
- **Prevent:** when a claim rests on a virtual or overridable engine member, find which concrete type the mission's view or logic list constructs (`[ViewMethod]` list, `OpenXxxMission` behavior array, `AddModel` call) and read THAT body. A gate being false is not the reason a side effect is absent if the side effect never existed on that path.
- **Source:** Codex adversarial pass on #560 (GPT-6-Astra, ultra), finding 6; `docs/reviews/rca-shader-precompile-reenable-2026-09-11.md`.

### A Stop order holds each rider where it stands; shape a formation with a Move
`MovementOrder.MovementOrderStop` is `MovementStateEnum.StandGround` (`MovementOrder.cs:143-144`, v1.4.8), and for StandGround `Formation.GetOrderPositionOfUnit` returns `unit.GetWorldPosition()` (`Formation.cs:1262`): each rider's target is its own position, and the arrangement slots that `SetPositioning` just laid out are never consulted. A `Move` is `Hold`, whose per-unit target is the arrangement slot (`Formation.cs:1258-1259`), and `MovementOrder.OnApply(Move)` calls `SetPositioning(position)` with the position only, so a preceding `SetPositioning(position, direction, spacing)` keeps its direction and spacing. SmartCavalryAI v1 issued `SetPositioning` then Stop to "form a line" and its reform; riders froze in place both times, which players reported as "cav stop in the middle of the enemy".
- **Why missed:** "Stop" reads as "hold the formation here", and the order name was taken as its semantics. Nobody opened `GetOrderPositionOfUnit`'s switch on `MovementState`, and the adapter test surface (a mock) could not show that the line never formed.
- **Prevent:** before issuing any `MovementOrder` to shape or place a formation, read `MovementOrder.MovementState` for that order and the matching branch of `Formation.GetOrderPositionOfUnit`; the branch tells you where each unit will actually go. To read where the engine is driving a unit under a Hold order, prefer `Formation.Arrangement.GetWorldPositionOfUnitOrDefault(unit)` over the public `GetOrderPositionOfUnit`, which is prefixed by MixedFormations' Patch30, routes detached units to their detachment frame, and falls back to the unit's own position (distance zero) when the slot is off the navmesh.
- **Source:** docs/reviews/rca-smart-cavalry-2026-09-13.md, #586.

### A deleted Agent is a live handle to whoever inherits its index: never key mission state on Agent.Index
`Agent.State`, `IsActive()`, `IsFadingOut()`, `Position`, `MovementVelocity`, `ActionSet` and `SetActionChannel` all go through native pointers captured in the constructor (`Agent.cs:1529-1533,1566-1571`, v1.4.8); `Mission.OnAgentDeleted` (`Mission.cs:2972-2985`) sets `Deleted`, nulls `Team` and drops the object from `_allAgents`, then the engine hands the index to the next agent it builds. From that point the dead managed object reads and writes the new occupant while `Monster`, `Name`, `Character` and `Team` describe the old one. `MissionAdapterFactory` cached adapters by index, so a reinforcement horse in a dead warg's slot was served the warg's adapter, `IsWarg()` said yes, a warg tree went onto the horse, and the engine logged `as_horse does not contain act_warg_attack_running` in the second a player's game froze (#592). When a live warg inherits a dead agent's slot the aliasing is invisible, because the pointers hit the right slot; that is why the feature looked correct for five months and why the June "IsHuman/IsMount inverted off the freed native struct" crash was filed as teardown timing.
- **Why missed:** the April 2026 Codex HIGH on this cache named cross-mission reuse and was fixed with a mission-end clear; the intra-mission case was written down in `mount-despawn.md` five months later and routed around rather than fixed in the store every creature feature shares. A recorded-and-avoided hazard reads as handled.
- **Prevent:** identity for a mission-scoped engine object is the object reference (`Agent.GetHashCode` is the per-mission creation index; no `Equals` override). Any store keyed by `Agent.Index` evicts in `OnAgentDeleted` and logs the first reuse it sees, so the engine fact is proven in every battle log. A handle held across frames answers `IsActive()` only while `Mission.FindAgentWithIndex(agent.Index)` still returns that object, and `RegisterBlow` addresses agents by index, so the blow chokepoint asks the same question. Before any creature `SetActionChannel`, ask `MBActionSet.CheckActionAnimationClipExists`; a clip requested on the wrong monster is the tell that a handle is stale. When a hazard is documented as "avoided here", grep for every other consumer of the same store the same day.
- **Source:** docs/reviews/rca-warg-clip-on-horse-2026-09-13.md, #592; player logs `taom_debug_2026-09-13_17-53-44.log` and `rgl_log_27236.txt`.

### Agent.Tick runs on the engine's asynchronous AI thread in single-player: an AgentComponent must not register blows or touch mission collections
`MissionState.TickMission` passes `asyncAITick: true` on the normal single-player path (`MissionState.cs:201`, v1.4.8); `Mission.OnTick` runs every behavior's `OnMissionTick` on the main thread, then `TickAgentsAndTeamsAsync`, and native calls back `Mission.TickAgentsAndTeams` (an `[MBCallback]`) on another thread, which runs `Agent.Tick` and every `AgentComponent.OnTick` there while the main thread finishes the frame; the next `OnPreTick` spins in `WaitTickCompletion` until that callback ends. TAOM's creature trees lived in a component's `OnTick`, and the spider's tree registered blows from it (`TakeDamage` -> `Mission.RegisterBlow` -> `HandleBlow` -> `Mission.OnAgentHit` -> `Die` -> `OnAgentRemoved`), so the engine's hit pipeline and `BehaviorTreeMissionLogic`'s dictionaries ran on that thread against the main thread's `OnAgentRemoved` callbacks (`DisposeTree` -> `trees.Remove`). A `Dictionary` read racing a `Remove` can spin forever; the async tick never completes; the main thread waits; the worker pool spins: two player freezes with no exception, no allocation and the process alive (#592). Vanilla never calls `RegisterBlow` from managed code, and its AI components touch only AI state from that tick. The warg, whose damage runs in a bone check from `OnMissionTick`, never froze.
- **Why missed:** the May 2026 change that removed the manual main-thread tick ("2x ticks per frame") kept the engine-driven one, on the assumption that where the engine ticks a component is where mod logic belongs. Nobody opened `TickAgentsAndTeamsImp` to see which thread runs it. The June spider crashes (AV reading 0x3, a horse "mid-teardown") were filed as geometry and bone-index faults; both are also what a data race across two threads looks like.
- **Prevent:** creature and combat logic ticks from `BehaviorTreeMissionLogic.OnMissionTick` (or any `MissionBehavior.OnMissionTick`), never from `AgentComponent.OnTick`/`OnTickParallel`. Anything that registers a blow, plays an action on an agent, or reads a TAOM collection the engine's callbacks write goes through `MissionThreadGuard.NoteCall`, which logs once per site when it runs off the marked thread. Before moving logic into any engine-driven callback, decompile the caller chain up to the thread that invokes it: `[MBCallback]` methods are entered from native, and native decides the thread.
- **Source:** docs/reviews/rca-warg-clip-on-horse-2026-09-13.md, #592; player logs `taom_debug_2026-09-13_20-36-14.log` and `rgl_log_3936.txt`.

### When a defect class is found, hunt the class across the repo the same day; a hazard documented as "avoided here" is not fixed
#592 was one instance of two classes: a store keyed by `Agent.Index` outliving the agent, and mission logic running on the engine's asynchronous tick. A three-reviewer audit of `Main/` the same day found nine more sites of the same two classes (#595): a formation slot map keyed by index and never evicted, a buff-restore closure subtracting by a captured index, a howdah machine following a dead elephant's handle, a dread tracker pruning on the engine's `IsActive()`, a stance store mutated from the async tick behind a team filter, a buff tracker with no lock behind a native callback, an unguarded listener dispatch inside the engine's own unguarded callback loops, six unguarded `OnAgentBuild` bodies inside `Mission.SpawnAgent`. Two of them had been written down before as things to avoid (`mount-despawn.md` for index reuse, Patch35's own comment for the async tick) and left in place elsewhere.
- **Why missed:** the fix was scoped to the instance in the log. Each earlier sighting was recorded next to the feature that dodged it, not applied to the store the other features shared, and the reviewer prompts asked about neither handle lifetime nor thread placement.
- **Prevent:** after an RCA names a mechanism, grep for the mechanism, not the symptom, and file every hit before closing the issue. `/deep-review` Agent 5 rule 5e now asks both questions of every mission-time changeset. `.claude/rules/csharp-architecture.md` "Mission-scope agent handles and the engine's threads" and `.claude/rules/harmony-patches.md` "Which thread runs your target" carry the facts; `AgentSlotIdentity.IsCurrentOccupant` and `MissionThreadGuard.NoteCall` are the two helpers every new holder or off-thread write uses.
- **Source:** docs/reviews/rca-warg-clip-on-horse-2026-09-13.md (Audit section), #592, #595.

### An engine callback runs on its caller's thread, and the caller can be the asynchronous agent tick
`MissionBehavior.OnAgentPanicked` looks like every other mission callback. On v1.4.8 it is a plain managed call from `CommonAIComponent.Panic`, which `CommonAIComponent.OnTick` makes inside `Agent.Tick`, inside the asynchronous agent tick (`CommonAIComponent.cs:119-135`, `Mission.cs:6708-6714`); `OnAgentFleeing`, its twin, comes from `MissionAgentPanicHandler.OnPreMissionTick` on the main thread. The tree logic's `OnAgentPanicked` read its listener map off the main thread while the main thread wrote it. Found by Codex (review 109) after two deep-review passes and a hand-built thread map had called the class main-thread-only.
- **Why missed:** the thread map was built from `Mission.OnTick` and the native `[MBCallback]`s, and a managed `public void OnAgentX` on `Mission` was assumed to be one of them. Nobody opened the callers of the thirteen overrides.
- **Prevent:** for every `MissionBehavior` override a class implements, grep the decompile for the `Mission.OnX(` caller and write its thread next to the override; when the caller is an `AgentComponent` tick, a `Team` or `Formation` tick, or unknown native, the override asks `MissionThreadGuard.IsOnMainThread` and parks itself (`DeferredCallbackQueue`) for the next `OnMissionTick`. The thread map in `.claude/rules/harmony-patches.md` carries both rows.
- **Source:** `docs/reviews/raw/codex-adversarial-creature-handles-2026-09-13.md` O1; `docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`; #595.

### `Hero.IsAlive` is `!IsDead`; name a state flag for the rule it enforces and derive it from the state enum
The clan-leadership repair's "dead" guard read `Hero.IsAlive` and the service doc said "dead or disabled". On v1.5.3 `IsAlive => !IsDead` (Hero.cs:319), so a `Disabled` or `NotSpawned` main hero passed the table and would have reached `ChangeClanLeaderAction.ApplyInternal`, whose own party guard lists prisoner, fugitive, released and traveling and nothing else. `CharacterStates` has six values; a flag that admits some of them has to say which.
- **Why missed:** the flag was named after the first engine boolean whose name matched the intent, and the state table was enumerated from the states the author expected rather than from the enum. Low reachability made it easy not to think about.
- **Prevent:** when an adapter exposes a bool that gates a mutation, name it for the rule (`IsHeroInPlay`, not `IsHeroAlive`), build it from the state enum (`IsAlive && !IsDisabled && !IsNotSpawned`, or the positive list), and write the admitted states into the doc comment. When reviewing a state table, enumerate from the engine enum first and diff against the code; `/deep-review` Agent 5 rule 5 caught this only because it did exactly that.
- **Source:** docs/reviews/rca-player-switcher-clan-leadership-2026-09-15.md finding 1, #550.

### An adapter branch on an engine subtype is dead in a game type that registers the base type (2026-10-02)
`ObjectManagerAdapter.GetAllCultureInfos` filled a culture's troop ids under `c is CultureObject`. Custom Battle, the only consumer, runs as `CustomGame`, which registers cultures as plain `BasicCultureObject` (`CustomGame.OnRegisterTypes`), so the branch never ran there and every TAOM formation default silently came back empty for months.
- **Why missed:** the branch was written against the campaign's type; the service tests mock the adapter, so nothing executed it under the game type that calls it, and its only runtime trace was a DEBUG line that never printed.
- **Prevent:** before an adapter casts to an engine subtype, check what type each calling game type registers (`OnRegisterTypes` of `CustomGame`, `Campaign`). When the base type drops the data, read it back from the merged XML for the current game type (`MBObjectManager.GetMergedXmlForManaged` with `Game.Current.GameType.GameTypeStringId`, as `ObjectManagerAdapter` now does for cultures, parsing with `CultureTroopIdReader`), and log once at INFO that the read ran. Then check the data the read returns against the consumer's own filter: the culture troops TAOM reads fit vanilla's Custom Battle slot filter in only 33 of 88 slots, so the service also checks culture and formation class before offering one.
- **Source:** `docs/reviews/rca-custom-battle-bannerless-factions-2026-10-02.md` "Two older Custom Battle defects", B.

### A co-op battle is not a network session to the engine (2026-11-05)

`GameNetwork.IsSessionActive` is `IsServerOrRecorder || IsClientOrReplay` (v1.4.8 `GameNetwork.cs:381-391`), and those read `MBCommon.CurrentGameType` (`:317-339`). BannerlordCoop has no use of `CurrentGameType` in its source (read at `4ad976c8c`), so a co-op battle reads as single player and a gate on this property alone lets a feature act in one.

- **Why missed:** the name sounds like the question, and a test that feeds the fact by hand never pins where it came from.
- **Prevent:** a gate on online play reads the engine's fact and `ICoopSessionProvider.IsSessionActive`, and a test pins both reads.
- **Source:** the 1.5.x line's review, carried by hand in `plans/_audit/2026-11-04-port148/delta-974e60bb2.md`.


### An engine cache keyed on a different version counter than the one you bump (2026-10-14)
`PartyBase.PartySizeLimit` caches its value against `MemberRoster.VersionNo` (v1.4.8 `PartyBase.cs:343-355`). The hunt start and end invalidated
the party speed (`UpdateVersionNo`) and relied on the top-up changing the roster; a start that added no troops,
and every end, left the old size limit in place, so the hunt's allowance outlived the hunt (Codex F2, #765 M8).
- **Why missed:** the author invalidated the cache they were thinking of (speed). The size cache's key was never
  read, and the tests mocked the adapter, so no test read the limit back.
- **Prevent:** before invalidating an engine cache, read the getter and name the version it compares
  (`taom-src path <Type>`). Bump that counter on every transition (`MemberRoster.UpdateVersion`), not only on those
  that change the data, and pin it with an IL test.
- **Source:** 1.5.x `docs/reviews/rca-eye-of-sauron-2026-10-14.md` F2 (Codex), fixed in `2d17ceba3`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A broad-phase search radius must exceed the exact filter's edge (2026-10-14)
The hunter's frame read searches the locator grid around the player with radius `1.5 S`, and the service accepts
the hunter at `d <= 1.5 S`. The engine's locator skips a locatable at exactly its radius (`>=` on the squared
distance, v1.4.8 `LocatorGrid.cs:131` and `:139`), so a hunter exactly on the edge was never found. The edge had just moved there
by a maintainer decision (E09) and the adapter's radius was copied from it (Codex F4, #765 M8).
- **Why missed:** the tests handed the service a reading directly; none passed through the locator's half-open
  comparison.
- **Prevent:** a spatial broad phase searches slightly past the exact edge (here 1%) and the exact filter stays in
  the service. When a decision moves an edge, grep every search radius that feeds the old one. Pin the radius with
  an IL or boundary test.
- **Source:** 1.5.x `docs/reviews/rca-eye-of-sauron-2026-10-14.md` F4 (Codex), fixed in `2d17ceba3`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Call a long all-bool engine signature with named arguments
`CustomAttacksUtils.TakeDamage` builds a `CombatLogData` through its 17-argument constructor, fifteen of them bools, and passed them by position. The fifteenth is `crushedThrough`, which prints "Crushed through!" (`CombatLogData.cs`); TAOM passed `knockDown` there since the helper was ported, so every creature knockdown the player saw claimed a broken guard. A same-day review of the same struct fixed its `DamageType` and never mapped the other arguments.
- **Why missed:** a positional list of bools reads as correct in any order, and a reviewer checks the argument they came for.
- **Prevent:** name every argument of an engine constructor or factory that takes more than a few bools of one type (`crushedThrough: false`), at least from the first one TAOM sets on purpose; when reviewing such a call, map each position to the decompiled signature. `AttackCollisionData.GetAttackCollisionDataForDebugPurpose` (twelve leading bools) in the same method is the next candidate.
- **Source:** 1.5.x `docs/reviews/rca-elk-delta-2026-09-23.md`, Codex F1 (#643), on `origin/bannerlord-1.5.x` (ported by WP142).

### An engine field with the right name may be corrected in a local one call earlier, and a capped engine value is set through the engine's own producer
`AttackCollisionData.DamageType` is the struct's raw kind; `MissionCombatMechanicsHelper.GetAttackCollisionResults:200` computes a corrected local (bare hand, off-attach-bone, kick or bash, fall, horse charge become Blunt) and never writes it back, then `Mission.GetAttackCollisionResults` hands the same uncorrected struct to `AgentApplyDamageModel.CalculateDamage`. A model that keys a "blunt resistance" pip on the raw field misses a trample. Likewise `Agent.SetWeaponAmountInSlot` sets an amount the engine caps at `ModifiedMaxAmount` everywhere else (pickups merge only up to it, `Agent.cs`); vanilla's extra-ammo perks raise the max with the amount through `MissionEquipment.SetAmountOfSlot(slot, amount, addOverflowToMaxAmount: true)` in `InitializeMissionEquipment`, before build. The career ammo refill used the wrong setter for three months.
- **Why missed:** the field and the setter both existed with the expected name and type; nobody read the engine's own consumer of the field or its own producer of the value. Third instance of the class in one day (#611: a property written where nothing reads it; #613: a value read where the engine reads a corrected copy, and a value written where the engine caps it).
- **Prevent:** before keying on an engine value, find where the ENGINE reads it on that path and read that, and before writing an engine value, find how the ENGINE writes it for the same effect (a vanilla perk that does the same thing is the best map) and use that seam. A public helper vanilla uses for the rule (`IsCollisionBoneDifferentThanWeaponAttachBone`) beats a re-implementation.
- **Source:** 1.5.x `docs/reviews/rca-career-perks-2026-09-17.md` findings 4 and 5, #613, on `origin/bannerlord-1.5.x` (ported by WP142).

### When a change clears an engine flag on an agent, grep every TAOM reader of that flag the same day (2026-09-28)
Route A clears `Mountable` on the creature bandit after its build so native targeting sees an enemy. Code written
earlier for "a riderless mount" went stale at once: the routed-count backstop assumed SandBox skips the creature
(it skips only an `IsMount` agent, `BattleAgentLogic.cs`), and the guard comments, the registry and the feature
doc described a `Mountable` creature. The backstop then fired for every routed creature with a misleading warning.
- **Why missed:** graphify follows TAOM types, not engine flags, so the blast-radius step never listed `IsMount`'s
  readers; the change was reviewed as a targeting fix.
- **Prevent:** after a change that sets or clears an `AgentFlag` (or anything behind `IsMount`, `IsHuman`,
  `CanWieldWeapon`), `git grep` the property and the flag across `Main/` and the docs, and re-read each hit as if
  the agent had the new flags. List the ones that change behaviour in the commit body.
- **Source:** 1.5.x `docs/reviews/rca-creature-bandits-2026-09-28.md`, findings 9 and 12, on `origin/bannerlord-1.5.x` (ported by WP142).

### Before a shared component's presence means a kind, list every site that attaches it (Lost Footing, 2026-10-20)
The census called an agent a creature when it carried a `BehaviorTreeAgentComponent`. Race Abilities attaches the same
component to every soldier whose race or culture has a profile, on by default, so nearly every soldier would have read as
a creature.
- **Why missed:** the plan defined the kind from the creature features, the component's best-known users.
- **Prevent:** before using a component, flag or marker as a classifier, grep every `AddComponent`/attach site of that type
  and classify by what the component holds (here the tree type), not by its presence.
- **Source:** 1.5.x `docs/reviews/rca-lost-footing-2026-10-20.md` rows 1 and 14, on `origin/bannerlord-1.5.x` (ported by WP142).

### Read an engine getter's body and its callers before measuring with it (Lost Footing, 2026-10-20)
The census read `Agent.GetTargetPosition()` as the AI move destination. Natively it is the movement-lock target, written
only by `SetTargetPosition*` (ladders, siege engines, cheering) and (0,0) for an ordinary soldier; vanilla reads it only
while `MovementLockedState` is set. The AI destination is `GetAIMoveDestination()`.
- **Why missed:** the name fitted, and the plan's engine table listed it without reading it.
- **Prevent:** for every engine member that feeds a gate or a measurement, read its body (native when it is an icall) and
  at least one vanilla caller, and write the meaning into the plan with the citation.
- **Source:** 1.5.x `docs/reviews/rca-lost-footing-2026-10-20.md` row 2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A drift guard pins an engine dependency at the strength the code relies on it (2026-10-02)
Patch99 re-implements `CreateMergedXmlFile`'s loop, copies `ApplyXslt`, and drops the `ToXDocument`/`ToXmlDocument`
round trip, yet its IL tests compared an unordered set of callee names and its watched-method test checked names while
the adapter resolves exact parameter lists. A reordered loop, a changed XSLT setting or a new conversion option would
have changed the merged document with no exception, no fallback and a green `/verify-bindings`. This repeats the
2026-10-01 entry "pin that order from its IL" for a re-implemented body.
- **Why missed:** the guard copied the shape of the existing binding tests ("which helpers exist") instead of the
  shape of the dependency ("this exact sequence, these exact signatures").
- **Prevent:** for every engine body TAOM copies or whose effect it removes, pin its ordered calls, with their
  overloads and the constants that feed them (`IlCallScanner.Fingerprint`; a name-only call list misses
  `keepDuplicates: false` becoming `true`), in a
  `BindingVerification` + `RequiresGameIL` test with re-pin instructions in the failure message
  (`XmlMergeBindingTests.MirroredEngineBodies_CallExactlyThePinnedSequence`); resolve watched or bound members in the
  test by the same parameter list the adapter uses, from one shared table.
- **Source:** 1.5.x `docs/reviews/rca-xml-merge-load-time-2026-10-02.md` findings 3 and 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### In OnAgentBuild the gear an agent wears is `agent.SpawnEquipment`, not `Character.Equipment` (#627, 2026-09-19)
`Character.Equipment` is the troop's default (first) roster; `Mission.SpawnAgent` rolls a battle roster, stores it with `InitializeSpawnEquipment` before `OnAgentBuild`, and builds the mount from that roster's Horse and HorseHarness. A gate that reads the character's equipment answers for a roster the agent may not be wearing.
- **Why missed:** the troop had one roster, so both reads agreed and nothing failed.
- **Prevent:** read `agent.SpawnEquipment[slot]` in any build-time gate about what the agent carries or rides.
- **Source:** 1.5.x `docs/reviews/rca-howdah-prefab-review-2026-09-19.md` (delta addendum), #627; `Mission.cs`, `:4309`, `:4324-4328`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Mirror the engine's own partition rule, not the nearest public helper
To decide which parties count toward the player's ally team, the doctrine used `MissionCombatantsLogic.SupportsAllyTeamOnPlayerSide(out ally)`, which is public and returns the FIRST non-commanded party with troops. The engine places every troop by `Mission.GetAgentTeam` (`Mission.cs`): on the player's side a troop lands on the ally team iff its origin is neither under the player's command nor in the player's army. With two allied lords the second one's troops fought on the ally team while their culture and Tactics skill were counted into the player team.
- **Why missed:** the helper's name described the question being asked, it was public, and the design critique had recommended it. Nobody opened the engine's consumer of the same decision.
- **Prevent:** when a TAOM rule must agree with how the engine assigns, groups or partitions something, decompile the engine's consumer (the method that performs the assignment) and apply its predicate per element; a public accessor that returns one element is a convenience for the engine's own UI path, not the rule.
- **Source:** 1.5.x `docs/reviews/rca-culture-doctrine-2026-09-16.md` finding 3 (data-flow agent), #608, on `origin/bannerlord-1.5.x` (ported by WP142).

### Model an engine gate where the engine applies it, and expect the tool's own fixtures to break (2026-10-03)
The first review of plan 038 named slot fit (`Equipment.IsItemFitsToSlot`), noted that it affected one skirt, and left it unmodelled. Codex found the case: two TAOM troops put a BodyArmor in the Cape slot and the audit counted it. Running the engine rule over every assignment in the five modules (135,359 of them) refused exactly those two, so the deferral kept a visible defect for a rule that is two tables and a few short functions. The gate sits in `Equipment.DeserializeNode`, once per assignment, so a refused override has to keep the earlier item: the tool now assembles each set assignment by assignment instead of filling a dict and judging it afterwards. The tool's own tests then broke, because five fixture assignments (a Goods item in `Body`, a BodyArmor in `Leg`, a HeadArmor in `Body`) were valid test data only while nothing checked them.
- **Why missed:** the rule was deferred as an approximation without a count, so its size looked like "one skirt" until someone ran it; and fixtures are written to exercise the tool, not to be valid engine data.
- **Prevent:** before deferring an engine rule, run it over the real data and write the count beside the deferral. Model a gate at the engine's call point (per assignment, in order), report each refusal as its own reason row, and make the existing fixtures obey the rule before the red test, with every expected number unchanged, so the green run proves the rule and not the fixtures.
- **Source:** 1.5.x `docs/reviews/rca-battle-equipment-memory-audit-2026-10-02.md` Codex pass, X1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Mirror an engine's type test, not the XML element name that usually satisfies it (plan 038 convergence, 2026-10-04)
`ItemObject.Deserialize` replaces an item's Type whenever `WeaponComponent != null`, and the audit applied the override when it found a `<Weapon>` element. `BannerComponent` derives from `WeaponComponent` and a `<Banner>` element builds one, so the engine gave a banner the type of its weapon class and the tool did not: it refused a `Type="Goods"` banner the engine equips and accepted a classless `Type="Banner"` item the engine gives Invalid. Nothing in the data showed it, because all 46 real banners carry the matching Type and class. `PreloadHelper.AddItemObject` gates its holster and flying meshes on the same test, so the same gap sat there.
- **Why missed:** the model was read from the `case "Weapon"` arm and its data, and a census showing every real item agrees turned a gap into a non-finding.
- **Prevent:** when a model cites an engine type test (`X != null`, `is X`), list the subclasses of X and the XML elements that construct each (here the `switch` in `ItemObject.Deserialize`), give each a fixture, and route every consumer of that test through one flag. Read "the real data agrees" as proof the gap is latent, not absent.
- **Source:** 1.5.x `docs/reviews/rca-battle-equipment-memory-audit-2026-10-02.md` convergence of the Codex pass, Y2, on `origin/bannerlord-1.5.x` (ported by WP142).

### Read the native body behind a name before trusting what the name implies
- **Why missed:** `Scene.DoesPathExistBetweenPositions` only compares two faces' navmesh island ids (native 0x4C7A60) and takes no agent, so it ignores per-agent face exclusion, and islands merge when a ladder is raised; `Scene.GetGroundHeightAtPosition` returns 0, not NaN, on a miss (0x4C4D80). The siege design trusted both names and a copied vanilla fallback order, and scripted trolls behind shut gates.
- **Prevent:** for every engine call whose result gates a decision, read the native or managed body for what it actually computes, its miss value and whether it is agent-aware, and write that into the adapter's interface doc. Prefer calls that return a hit flag (`RayCastForClosestEntityOrTerrain`) over ones with a sentinel value.
- **Source:** 1.5.x `docs/reviews/rca-siege-forces-2026-10-05.md` F2, F4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A native capacity claim reads every writer of the resource, not only the registrar
- **Why missed:** the per-agent face exclusion capacity was derived from the registry's per-face mark byte (8 bits, bit 7 the navmesh boundary), but the face-ability setter (0x401B20) re-marks sets only from index 1 because bit 0 belongs to the engine, so the safe cap is 6, not 7.
- **Prevent:** before fixing a cap from native bookkeeping, list every function that writes the field (xrefs) and check which bits or slots each owns; cap under the most conservative reading when one stays unverified.
- **Source:** 1.5.x `docs/reviews/rca-siege-forces-2026-10-05.md` F3, on `origin/bannerlord-1.5.x` (ported by WP142).

### Verify the override the engine dispatches, not the base member you expected (generated lord kits, 2026-10-06)
- **Why missed:** the donor adapter read `character.BattleEquipments` expecting the lord's authored roster (`BasicCharacterObject`), but `CharacterObject` overrides it to return the hero's live `HeroObject.BattleEquipment`, which falls back to the engine's shared `DeadBattleEquipment` once the hero dies. Dead lords donated that kit, and two deaths made it a "shared" kit.
- **Prevent:** before relying on a virtual engine member, `grep override` its name in the most-derived type you hold (`pwsh tools/taom-src.ps1 path <Type>`), and read that body. When a getter has a live and an authored meaning, say which one the adapter needs in its comment. For a lord's authored kit, read the roster `BasicCharacterObject.Deserialize` registers under the character's id.
- **Source:** 1.5.x `docs/reviews/rca-generated-lord-kits-2026-10-06.md` F1, F2, on `origin/bannerlord-1.5.x` (ported by WP142).

### Mounts carry no Team: list every agent kind an engine callback passes before a rule reads its fields (Race Abilities, 2026-10-04)
Race Abilities credited a kill when the victim died at the hand of a live soldier with an ability on another team. A horse killed under its rider passed: `Mission.SpawnAgent` sets the rider's team (`Mission.cs`) and builds the horse through `CreateHorseAgentFromRosterElements` without one, so `killer.Team == null` was false and the dead horse extended Bloodlust, healed the killer and spread fear around the carcass. Vanilla's own `KillCount` counts horses too; its morale-on-kill skips every non-human.
- **Why missed:** the credit was modelled on `KillCount`, and the same-team gate was assumed to stop a horse; nobody listed what the removal callback carries.
- **Prevent:** for every engine callback or query a rule reads (removals, hits, proximity scans), write down the agent kinds that arrive (soldier, mount, riderless creature, the player) and the value of each field the rule reads for each: a horse is `Character == null` and `Team == null`. Put the kind in the rule's inputs and pin each kind with a test row.
- **Source:** 1.5.x `docs/reviews/rca-race-abilities-2026-10-04.md` R14 (lens 2 F1, lens 5 L7), on `origin/bannerlord-1.5.x` (ported by WP142).

### `CustomAttacksUtils.TakeDamage` bypasses armor: a creature's damage band is a post-armor number
It sets `blow.InflictedDamage` directly with `DamageCalculated = true`, so the engine skips the armor step. Measured
2026-09-18: 976 war ram head-butts on Armored Trolls averaged 23.1, exactly the raw 18-28 roll, and the goats floored
level-51 trolls all battle.
- **Why missed:** the band was set beside weapon swings quoted BEFORE armor (a mid-tier sword 25-35), which reads as
  weaker than a sword while landing every point.
- **Prevent:** compare a band routed through `TakeDamage` with weapon damage after armor, and say so next to the
  constant. Applies to every caller: elephant and mumakil tramples, the war ram, warg and spider bites, signature strikes.
- **Source:** 1.5.x #618, 2026-09-18 Custom Battle logs; `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Read a range check's body before quoting its bounds
`Agent.IsInBeingStruckAction` tests `MBMath.IsBetween(type, StrikeBegin, StrikeEnd)` with 48 and 52, and a 2026-08-28
review wrote that anything from 48 to 52 reads as being struck, so `actt_mount_strike` (52) "sits inside the band".
`IsBetween` is half-open (`value >= min && value < max`): 52 is outside. The claim went into a lesson, two handbook
pages, the creature authoring guide and a CLAUDE.md trap row before an engine review read the body (2026-09-18).
- **Why missed:** the constants are named Begin and End, which reads as inclusive, and the reviewer reasoned from
  the names; a finding that passed review then gained authority with every copy.
- **Prevent:** before stating that a value falls inside or outside an engine range, open the comparison's body
  (`MBMath.IsBetween`, `MathF.Clamp`, a hand-written `<`/`<=`) and quote the operator. Treat a band quoted in a
  lesson as a claim to re-check, not a fact, when it decides a design.
- **Source:** 1.5.x `docs/reviews/rca-creature-collision-review-2026-09-18.md` finding 3; `TaleWorlds.Library.MBMath.cs`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A binding gate's fallback must not let TAOM's own types satisfy an engine row (plan 025, 2026-09-24)
The `ReflectionSiteBindingTests` row `TaleWorlds.Engine.PathReuseCache._store` named no engine type. It passed from
`41258657` (2026-05-28) until plan 025 removed it, because `ResolveType`'s simple-name fallback
(`ReflectionSiteBindingTests.cs`) searches every loaded assembly, TAOM.dll included, and found TAOM's own
`PathReuseCache`.
- **Why missed:** the fallback was written for engine namespace moves; nobody asked which assemblies it searches, and
  a mislabelled row looks the same as a good one when it is green.
- **Prevent:** restrict the fallback to engine assemblies (`TaleWorlds.*`, `SandBox*`, `StoryMode*` and the other
  shipped modules) or fail when the resolved type lives in a TAOM assembly. Reflection on TAOM's own types belongs in
  Category D of `reflection-sites.md`, never in this gate. Still open: the fix changes the gate's behaviour and is
  pre-existing test code, so plan 025 did not apply it (plan 008 works on the same gate).
- **Source:** 1.5.x `docs/reviews/rca-delete-unreachable-scaffolds-2026-09-24.md` F2 and FOLLOW-UP 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Before adding "missing" behaviour, trace what the live path's own vanilla calls already do (#971, 2026-10-23)
#971 said the career button's click sound never plays, because the mixin method that held it was never added to the
view model. The live handler, the Patch27 postfix, already called `CharacterDeveloperVM.ExecuteDone`, whose close callback
(`GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen`) plays the same `event:/ui/default`. The first fix
added a second call and doubled the sound.
- **Why missed:** the issue proved the dead copy was dead (the UIExtenderEx attribute filter, the brush) and inferred
  the effect was absent, without reading the vanilla methods the live path calls.
- **Prevent:** when an issue says "X never happens", find the path that runs and read each vanilla call it makes, one
  level into callbacks, before adding X. An in-game check on the old build settles it in seconds; ask for one when the
  trace is not conclusive.
- **Source:** 1.5.x `rca-career-click-sound-971-2026-10-23.md` finding 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Promote a rule from its lesson's Prevent line, every branch kept (2026-10-16)

The morale lesson's Prevent has two branches: floor the morale, or say in the feature doc that its routs spread.
The one-line rule promoted into `.claude/rules/csharp-patterns.md` kept only the first, so it read as condemning
three shipped fear writes that the maintainer had chosen to keep unfloored. It was the second time in two days
that a relay dropped a condition (the first was the contagion's "when fewer than 10 stand within 4 m").
- **Why missed:** the line was copied from the RCA's proposal, a summary that had already lost the branch, not
  from the lesson.
- **Prevent:** write a promoted rule from the lesson's own Prevent text, keep every branch and condition, and
  check the rule against the code it will load on: a rule that flags shipped, accepted code is wrong or the code
  is.
- **Source:** 1.5.x `docs/reviews/rca-dread-aura-local-2026-10-15.md` S1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A morale effect that routs a troop is not local (2026-10-15)

A troop that routs raises `AgentMoraleInteractionLogic.OnAgentFleeing`, and vanilla gives the morale loss
to up to 10 allies: those within 4 m first, then, for every slot still empty, random members of the fleeing
agent's whole formation at any distance. So a rout in a thinned or loose formation reaches past any radius
the effect was written with, and in a dense block it lowers the neighbours' morale, so they break sooner.
- **Why missed:** the Dread Aura design welcomed the contagion ("vanilla's own contagion carries the rout
  outward") without tracing how far it reaches. The formation fill was never read, and Mike then saw dread
  act "across the whole battlefield".
- **Prevent:** a TAOM effect meant to stay local lowers morale to a floor above 0.01 and leaves the rout to
  combat, or it accepts the formation-wide spread in its doc. Before claiming a radius for any morale write,
  read `ApplyMoraleEffectOnAgentIncapacitated` to its end.
- **Source:** 1.5.x `docs/features/dread-aura.md` "Why the aura does not rout by itself"; v1.5.5 `AgentMoraleInteractionLogic.cs`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A remote conversation is an encounter: audit its setup as well as its lines (2026-10-23)

The messenger session rules hid every dialog line that moves a party, and the review then found that Messengers'
own setup moved one. For a target in a settlement it calls `PlayerEncounter.EnterSettlement`, and
`EnterSettlementAction.ApplyForParty` attaches a main party that is in an army it does not lead to that army when the
leader is in the settlement; `PlayerEncounter.Finish(true)` then leaves the attached party in place. No dialog line was
involved. The same pass found three more lines that hand things over or move a hero with no presence check (clan
troop and inventory exchange, companion recall, a gift line).
- **Why missed:** the plan listed the dialog consequences that move a party and treated the encounter the
  conversation runs in as neutral.
- **Prevent:** before a feature opens a conversation the player is not physically part of (a messenger, a remote
  hail), list what its encounter setup does (`PlayerEncounter.Start`, `SetupFields`, `EnterSettlement`, the
  mission it opens and `Finish`), read each engine body for party, army, position and roster writes, and then sweep
  every reachable dialog consequence for the same writes. Gate the feature's lines on one shared "remote
  conversation" flag rather than one check per feature.
- **Source:** 1.5.x `docs/reviews/rca-messenger-session-rules-2026-10-23.md` findings 1 and 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### Read the engine's fallback before compensating for a missing role (enlisted survival redirect, 2026-10-26)
The healing model redirected an enlisted player's survival roll to his commander's party, on the premise that his own
one-hero party had no surgeon. Vanilla's effective surgeon falls back to the party leader, so the player already rolled
with his own Medicine and Cheat Death, and the redirect made some players more likely to die.
- **Why missed:** the comment stated the premise. The 2026-08-08 review did read the effective-role getters and their
  leader fallback, but applied them to the commander's party and never to the player's own party.
- **Prevent:** before code that makes up for an engine gap (a missing role, perk or bonus), read the engine getter that
  fills that role and quote its fallback in the comment. If the fallback already covers the case, delete the workaround.
- **Source:** 1.5.x `rca-camp-work-2026-10-26.md` Follow-ups, on `origin/bannerlord-1.5.x` (ported by WP142).

### A guard on TAOM's call does not guard the engine's call: pin its precondition in data (Lord Banners, 2026-10-19)
TAOM checks every formation's heraldry before its own `SetFormationBanner`, because the native banner-tableau rebuild
crashed on a troop with an empty heraldry banner (`rca-banner-bearers-siege-ctd-2026-07-23.md`). Lord Banners (#823)
gave TAOM lords banner items, so vanilla's `GeneralsAndCaptainsAssignmentLogic` now banners their formations first,
AI teams included, and TAOM's guarded path skips them. Nothing failed: every clan and kingdom `banner_key` parses to
at least one layer. But nothing pinned that, so one malformed key would have reopened the crash with no guard in
front.
- **Why missed:** the plan traced the overridden model's reader, not where the value it produces is consumed
  downstream, and so not which TAOM guard sat on the old consumer.
- **Prevent:** when a change routes data onto another path to an engine call that TAOM guards only at its own call
  site, the guard no longer applies there: make its precondition hold in the shipped data and pin that with a test
  (`ShippedHeraldryBannerKeyTests`). Search the TAOM code for guards on the engine method the new path reaches.
- **Source:** 1.5.x `docs/reviews/rca-lord-banners-2026-10-19.md` F1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Read a campaign behavior's RegisterEvents before stating when it acts (Lord Banners, 2026-10-19)
The Lord Banners doc said existing saves would get banners over several days through the daily tick.
`BannerCampaignBehavior` registers `GiveBannersToHeroes` on `OnGameLoadFinishedEvent` as well as on new game, so
every eligible lord gets one as the save finishes loading, prisoners included.
- **Why missed:** research read the give methods and inferred their triggers instead of reading `RegisterEvents`.
- **Prevent:** before writing when or how often a vanilla behavior does something, read its `RegisterEvents` and list
  every event that reaches the method.
- **Source:** 1.5.x `docs/reviews/rca-lord-banners-2026-10-19.md` F3, on `origin/bannerlord-1.5.x` (ported by WP142).

### A singleton adapter holds no engine objects between calls (#952, 2026-10-20)
The face adapter cached every `Hero` by id to avoid a linear `Hero.Find`, and as a process singleton it kept the last created campaign's heroes alive after the player loaded another save.
- **Why missed:** the cache was a performance shortcut and nothing cleared it.
- **Prevent:** carry the engine object inside the snapshot as an opaque handle (the `MountSnapshot` pattern), so the adapter stays stateless and references live only for one call.
- **Source:** 1.5.x `docs/reviews/rca-followups-949-952-2026-10-20.md` F4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A roster write during a party-screen conversation is reverted by Escape (2026-10-19, second occurrence)

A conversation opened from the party screen (`PartyVM.cs`) returns to that screen, which still holds
its pre-conversation snapshot. Escape, Cancel or Reset runs `PartyScreenData.ResetUsing`, which clears the live
prison, member and item rosters and re-adds every snapshot row (`PartyScreenData.cs`). A hero killed in
the conversation comes back as a dead prisoner; loot added to the inventory disappears.
- **Why missed:** the same `ResetUsing` behaviour was recorded on 2026-10-17 (`lessons/harmony-il.md`, the
  roster-loss diagnostic), scoped to diagnostics, so nothing loaded it for a feature that writes rosters from a
  dialog consequence.
- **Prevent:** after any roster, inventory or hero-state write made from a conversation, commit an open party
  screen the way vanilla does when a companion leaves (`GauntletPartyScreen.OnCompanionRemoved` calls
  `PartyScreenLogic.DoneLogic(true)`): `GameStateManager.Current?.LastOrDefault<PartyState>()?.PartyScreenLogic?.DoneLogic(true)`.
  Trap index row "Party-screen conversations".
- **Source:** 1.5.x `docs/reviews/rca-hard-questions-2026-10-19.md` H4, on `origin/bannerlord-1.5.x` (ported by WP142).

### Never null-check `PartyBase.MainParty`: with a campaign, its getter dereferences the stored main party (2026-10-14)
`PartyBase.MainParty` returns null only when there is no campaign. With one, it returns
`Campaign.Current.MainParty.Party` (v1.4.8 `PartyBase.cs:208-218`), so a null check on it throws in exactly the
state it was written for. `MessengerCampaignBehavior.IsPlayerAvailable` carried one (1.5.x `91f369984`), from the
Messenger port (`03a41b624`, 2026-05-06).
- **Why missed:** the computed-statics paragraph in `.claude/rules/adapters.md` named `Hero.MainHero` and
  `MobileParty.MainParty`, not this getter, whose failure is the reverse. The first source scan matched only
  `== null` and missed a `!= null` guard.
- **Prevent:** check `Campaign.Current?.MainParty`. `MainPartyNullCheckScanTests` rejects both polarities and the
  reversed and `is` forms; it does not see a `PartyBase.MainParty?.` read.
- **Source:** 1.5.x `docs/reviews/rca-review-followups-2026-10-14.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### `Formation.GetHashCode` dereferences its Team: key engine types by reference, and a hot getter's postfix must not throw (2026-09-26)
Patch92's store keyed `Formation` in a `ConcurrentDictionary` with the default comparer. `Formation.GetHashCode` is `Team.TeamIndex * 10 + FormationIndex` (v1.4.8 dump `Formation.cs:2423-2426`), and the order preview lays formations out on `new Formation(null, -1)`
copies, so the first time the player dragged the troll line the `UnitDiameter` postfix threw a
NullReferenceException inside `OrderTroopPlacer.OnMissionScreenTick` (Mike's debugger caught it).
- **Why missed:** a class that overrides nothing looked safe as a key, and nobody opened `GetHashCode`; the
  simulation copies were not known to exist.
- **Prevent:** key a map of engine objects with a reference comparer unless you have read the type's `GetHashCode`
  and `Equals`: `ReferenceIdentity.Instance` (`Main/Core/Collections/ReferenceIdentity.cs`, `ReferenceEquals` plus
  `RuntimeHelpers.GetHashCode`) is the shared one, and `TrollFormationSpacingStore` and `TrollFormationSpacingTracker`
  key on it (the Patch92-private `FormationIdentity` is gone). A postfix on a getter the engine calls from its UI, AI
  and worker threads must not throw, so build it only from reads that cannot: since the 2026-09-26 review Patch92's
  postfix reads the reference-keyed concurrent map and the public readonly `Formation.Team` field, and carries no
  try/catch.
- **On this tree:** This incident ran on the 1.5.x line, and `TrollFormationSpacingStore`, `TrollFormationSpacingTracker`, `FormationIdentity` exist only there; the rule holds here.
- **Source:** 1.5.x `docs/reference/harmony-patch-registry.md` Patch92; `Main/Core/Collections/ReferenceIdentity.cs`; `Main/Features/TrollBruteForce/TrollFormationSpacingStore.cs`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Fetch an engine NativeObject wrapper once per tick, and range-gate before fetching it (plan 015, 2026-09-24)
`MBAgentVisuals.GetSkeleton()` returns a new managed `Skeleton` on every call (v1.4.8 dump `ScriptingInterfaceOfIMBAgentVisuals.cs:776-786`): a native ref-count increase, a process-wide lock, a `NativeObjectKeeper` and a `GCHandle` (`NativeObject.cs`), then a finalizer that calls native again. `BoneCheck` fetched the attacker's skeleton twice per tick and every captured target's before testing the 4.5 m gate. Other `NativeObject` getters built through the scripting interface behave the same.
- **Why missed:** a getter reads as a field read, and an adapter that passes straight through (`AgentVisualsAdapter.GetSkeleton`) hides the allocation.
- **Prevent:** fetch a NativeObject wrapper once per tick and pass it down; run every cheap managed test (range, liveness) before the fetch.
- **Source:** 1.5.x plan 015 Step 11; `docs/reviews/rca-warg-tick-costs-2026-09-24.md` F9, on `origin/bannerlord-1.5.x` (ported by WP142).

### Test a NativeObject reference with `is null` in code a unit test reaches (plan 015, 2026-09-24)
`== null` on a `Skeleton` binds to `NativeObject.operator ==` (v1.4.8 dump `NativeObject.cs:221-227`), a static member, so the first call runs `NativeObject`'s static constructor, which calls `LibraryApplicationInterface.IManaged`, null in the test host. The `TypeInitializationException` poisons `NativeObject` for the rest of the test run. In game the operator returns `(object)a == null` for a null right side, so `is null` gives the same result.
- **Why missed:** the operator is invisible at the call site, and NSubstitute returns null for a sealed return type, so the null path looks test-safe.
- **Prevent:** write `is null` / `is not null` for any `NativeObject` subclass (check the type's base with `taom-src`; `Skeleton` is one) on a line a unit test can reach.
- **Source:** 1.5.x plan 015 orchestrator amendment, commit `7577894d`; `docs/reviews/rca-warg-tick-costs-2026-09-24.md` F9, on `origin/bannerlord-1.5.x` (ported by WP142).

### A perf commit's win names the cost it removes, read from the old path's body too (plan 033, 2026-10-02)
Commit `d45774a5` replaced the spider hunt's scan of hostile agents with the engine's nearby query, claiming to save "up to three native reads per hostile agent". On 1.5.x `Agent.IsHuman`, `IsActive()` and `Position` are `[AggressiveInlining]` unsafe loads through pointers cached at agent creation (`AgentHelper`), while `GetNearbyAgentsAux` walks the same fields for every used slot under a static lock and adds a locked `GetManagedObjectWithId` per result. v1.4.8 differs, `Agent.IsHuman` reads `GetAgentFlags()` (`Agent.cs:640`), so price it again on this engine. The plan decompiled the new side four times and the old side never; the change was reverted.
- **Why missed:** a recurrence of "An engine method's COST is a decompile question" (above): the replaced calls were costed by their names.
- **Prevent:** before a perf plan claims a saving, open the body of every engine member on the path it removes, not only on the path it adds, and state both costs in the commit body. A saving argued from call counts alone ships only with a measurement.
- **Source:** 1.5.x `docs/reviews/rca-creature-battle-allocations-2026-10-02.md` finding R1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Gate a non-formation agent's scripted moves on the deployment signals: AI ticking off, and a scripted move teleports (2026-09-28)
The deployment controller sets `Mission.AllowAiTicking = false` for the whole deployment
(v1.4.8 dump `DeploymentMissionController.cs:31`, back on at `:75`) and, for the Order of Battle screen,
`Mission.IsTeleportingAgents = true`, under which `Agent.SetScriptedPosition` teleports the agent to the target when
its XY differs (`Agent.cs:2420-2422`). Vanilla pauses and hides only formation members and humans, and TAOM's behaviour trees tick regardless (`BehaviorTreeMissionLogic.OnMissionTick`). A
riderless creature bandit, in no formation, would have hunted during deployment and landed on the paused army.
- **Why missed:** the spike spawned creatures from the console after deployment, and every engine AI component checks
  `AllowAiTicking` itself, so nothing in the framework carries that check for a tree.
- **Prevent:** any tree or task that moves an agent vanilla does not pause (no formation, not human) gates on
  `AllowAiTicking && !IsTeleportingAgents` and holds the agent meanwhile (`CreatureMayFightDecorator`,
  `CreatureHoldTask`). Formation members are paused by vanilla; everything else is yours.
- **On this tree:** This incident ran on the 1.5.x line, and `CreatureMayFightDecorator`, `CreatureHoldTask` exist only there; the rule holds here.
- **Source:** 1.5.x `docs/reviews/rca-creature-bandits-2026-09-28.md`, finding 2, on `origin/bannerlord-1.5.x` (ported by WP142).

### An engine type with no id gets a per-object adapter; a class that gains injection and tests is a service (2026-10-06)
The stuck-battle guard's adapter returned `IReadOnlyList<MapEvent>` and took `MapEvent` in every member, and the
orchestration that used them sat in `StuckBattleSweep`, an injected `Reuse.Singleton` under `Hooks/` with unit tests over
a substituted adapter and `null!` handles. The "key by id" remedy above does not apply: a 1.5.x `MapEvent` inherits
`StringId` but nothing ever assigns it, and its constructor is internal. Second occurrence of the sealed-token class in nine days.
- **Why missed:** the builder copied `AutoResolveDiagnostics`, where an entry point holds the `MapEvent`, then moved the
  logic into an injected class to test it. The move made it a service; the sealed-type rule was not re-checked, and the
  entry above was not read before writing the adapter.
- **Prevent:** for an engine type with no id, the adapter hands out one small adapter per object
  (`IStuckBattleAdapter.LiveBattles()` returning `IStuckBattleEventAdapter`), and the service drives those. A class that
  takes constructor injection, is registered `Reuse.Singleton` and is unit-tested is a service, whatever folder it sits in.
- **On this tree:** This incident ran on the 1.5.x line, and `StuckBattleSweep` exists only there; the rule holds here. a fact marked 1.5.x was read on that engine, not on v1.4.8, so check it before relying on it here.
- **Source:** 1.5.x `docs/reviews/rca-stuck-battle-guard-2026-10-06.md` row 1 (Standards, adversarial Step 2b), on `origin/bannerlord-1.5.x` (ported by WP142).

### After a programmatic batch of inventory transfers, refresh the panel with UpdateRightCharacter and RefreshInformationValues (Quick Actions, 2026-10-19)
`InventoryLogic.AddTransferCommands` raises `SPInventoryVM.AfterTransfer` per command, which updates the item rows and
the shown set's slots only. The paper doll, armour, weight, capacity, the no-saddle warning and the Done button are
refreshed by the private `RefreshInformationValues`, which vanilla's single-transfer handlers call after their one
transfer (v1.4.8 `SPInventoryVM.cs:1645-1647`). That is not enough for a batch: `AfterTransfer` clears the equipment
dirty flag on every command, so a batch whose last command moved nothing leaves the doll stale. Run
`UpdateRightCharacter` first, as vanilla does when the shown outfit or character changes. Quick Actions Unequip All shipped without the pair twice (May 2026 direct writes, then a
transfer route with no panel refresh).
- **Why missed:** both fixes read the event (`AfterTransfer`) as the screen's refresh and never opened the vanilla
  handler that issues transfers to see what it calls after `AddTransferCommands`.
- **Prevent:** before calling an engine command API from TAOM, read the vanilla callers of that API end to end, note
  what each refreshes after the call, and check that it still holds for TAOM's batch (one command or many). For
  inventory transfers, call `IInventoryVMAdapter.RefreshDisplay()`, which runs the pair through
  cached delegates pinned by `ReflectionSiteBindingTests`. EquipPresets still needs the same (follow-up).
- **Source:** 1.5.x `docs/reviews/rca-quickactions-unequip-2026-10-19.md` row 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### An agent spawned beside the troop supplier needs its own origin, never a supplier-backed one (#627, 2026-09-19)
The howdah crew were built with `AgentBuildData.TroopOrigin(mahout.Origin)`. In a campaign battle that is a `PartyGroupAgentOrigin`, which latches per instance: the first crew casualty went through its `SetKilled`, removing the Harad elephant rider from the party roster while he still rode, moving the troop supplier's `NumRemovedTroops` (so `MissionBattleSideSpawnContext.NumberOfActiveTroops` read a lost unit and a side could be declared beaten while its elephant fought), and the crew's hits paid the rider XP. A null origin is no escape: `BattleObserverMissionLogic.OnAgentBuild` reads `agent.Origin.BattleCombatant` unguarded.
- **Why missed:** the spawn was parked code, re-enabled by removing a comment, and the flip was reviewed as a flag rather than as the path it switches on. The June port also deviated from its own reference (ADOD_Beasts built a fresh origin per crewman).
- **Prevent:** an agent the troop supplier did not spawn gets a per-agent `IAgentOriginBase` whose casualty and score calls do nothing and whose `BattleCombatant`, colours and command forward to the parent's (`HowdahCrewAgentOrigin` is the pattern, unit-tested with `Substitute.For<IAgentOriginBase>()`). Grep every `TroopOrigin(` for an origin that came from another agent.
- **On this tree:** This incident ran on the 1.5.x line, and `HowdahCrewAgentOrigin` exists only there; the rule holds here.
- **Source:** 1.5.x `docs/reviews/rca-howdah-prefab-review-2026-09-19.md` (delta addendum), #627; `PartyGroupAgentOrigin.cs`, `MissionBattleSideSpawnContext.cs`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A hand-built `AgentBuildData` gets none of what `Mission.SpawnTroop` adds (#627, 2026-09-19)
The crew builder set team, position, direction, origin and formation. Vanilla's `Mission.SpawnTroop` (v1.4.8 `Mission.cs:4430`) also sets the banner, both clothing colours and `NoHorses`, and calls `WieldInitialWeapons()` after the spawn; without them the crew wore the builder's default colours (which TAOM's colour persistence also skipped) and might stand with nothing in hand.
- **Why missed:** the builder was written from what the spawn needed to work, not from what vanilla does to every troop.
- **Prevent:** when spawning outside the troop supplier, open `Mission.SpawnTroop` and match their builder calls and post-spawn steps, or say in a comment why one is left out.
- **Source:** 1.5.x `docs/reviews/rca-howdah-prefab-review-2026-09-19.md` (delta addendum), #627; `Mission.cs`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Before relying on another mod's setting or table, find every reader and writer across all of its DLLs (#740, 2026-10-06)
- **Why missed:** the research grepped ButterLib and MCM for the setting and concluded that a player could keep the Distance Matrix on with ButterLib's own `Options.json` key. MCM's `MBOptionScreen` owns ButterLib's settings page, stores a second key in the same file, applies it before TAOM's main-menu hook and rewrites the file without other keys on save; it enumerates `IEnumerable<ISubSystem>` and builds the key at runtime as `Id + " Enabled"`, so a name grep finds nothing. An earlier design also missed that ButterLib's public `DistanceMatrix<Kingdom>.Create()` reads the table it deferred.
- **Prevent:** a name grep is not enough. Also grep every installed DLL (MCM and the version-specific implementation DLLs included) for the interfaces the type implements (here `ISubSystem`) and the settings folder id (here `ButterLib`), and read every settings page or container that enumerates them; trace each reader and writer before writing the design or the doc.
- **Source:** 1.5.x `docs/reviews/rca-butterlib-distance-matrix-2026-10-06.md` F1, F2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A member reached by name joins the reflection binding gate in the same change (#646, 2026-09-23)
`MonsterSizeCatalogAdapter` reaches `HorseComponent.BodyLength`'s private setter, `ItemObject.CalculateEffectiveness`
and the `Effectiveness` setter by name. None had a row in `ReflectionSiteBindingTests` or `reflection-sites.md`, so an
engine update that renamed one would have left every sized mount at 1.0x (or its tournament rating stale) with no
failing test, and the recompute's absence was not even logged.
- **Why missed:** the size pass was designed inside the first review's fix round, and its convergence pass checked
  that the members exist today, not that the offline gate would see them tomorrow; no lens asked.
- **Prevent:** every `AccessTools` / `GetMethod` / `GetField` / `TypeByName` by literal name lands with its DataRow and
  catalogue row (`/verify-bindings`); the engine-compatibility lens now reports a missing row.
- **Source:** 1.5.x `docs/reviews/rca-animalia-2026-09-23.md` "Final review", finding F3, on `origin/bannerlord-1.5.x` (ported by WP142).
