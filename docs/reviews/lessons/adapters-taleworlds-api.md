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
A process-wide cache keyed on `partyBase.GetHashCode()` (or any sealed-type instance's hashcode) is unsafe: `object.GetHashCode()` is not unique per instance, so two different entities can collide and read each other's cached value. If the cache-validity key is *also* non-unique across instances (e.g. `MemberRoster.VersionNo`, which many parties share), the collision produces silent cross-instance value bleed. Use `ConditionalWeakTable<TInstance, TBox>` — reference-keyed (no collision), GC-evicting (no unbounded growth), internally synchronized (safe off the main thread).
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
