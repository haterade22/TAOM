# Lessons — Campaign Mechanics

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Campaign Mechanics lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### `OnSettlementOwnerChanged` fires TWICE per conquest — "start/reset on the event" must be idempotent across repeat fires for one takeover
A single siege capture raises `CampaignEvents.OnSettlementOwnerChangedEvent` **twice**: `ApplyBySiege` at capture (owner = kingdom leader), then `ApplyByKingDecision` at the fief-grant election ~1 in-game day later — for AI kingdoms the election resolves synchronously (`KingdomManager.SiegeCompleted` → `SettlementClaimantCampaignBehavior` → `Kingdom.AddDecision`), verified installed 1.4.6. `ByClanDestruction`/`ByLeaveFaction` produce the same capture→grant pair. A handler that *starts or resets* a per-settlement timer/counter on this event therefore fires N≥2 times per logical takeover; if it resets unconditionally, the value never accumulates. CultureConversion restarted its 45-day hold clock on every fire, so a contested fief queued 16× with zero completions (play-test 2026-07-07, #333). `ApplyInternal` has no owner-unchanged early-out, so even a re-grant to the *same* clan re-fires.
- **Why missed:** the original 2026-06-02 feature and its reviews assumed one fire per conquest; every test seeded a *single* `OnSettlementConquered` call, so the restart-on-refire was invisible to the suite, the deep-review data-flow trace, and the Codex pass.
- **Prevent:** for any `OnSettlementOwnerChanged` (or any CampaignEvent that fires multiple times per logical action) handler that starts/resets state, make the start idempotent — guard on "already pending toward this same target, continue" rather than unconditionally restarting — and write at least one test that fires the event twice for one takeover (capture→grant double-fire). When gating, key on the state that actually changed (owner *culture*, not the transfer `detail` enum — a `ChangeOwnerOfSettlementDetail` whitelist breaks mixed-culture grants + barter/gift/rebellion and is fragile to enum growth).
- **Source:** docs/reviews/rca-culture-conversion-timer-2026-07-07.md (#333).

### `DailyTickPartyEvent` fires for ALL party types — filter on `IsLordParty`/`IsMainParty`, not a "has a hero-led clan" proxy
`CampaignEvents.DailyTickPartyEvent` (and any `MobileParty.All` iteration) yields **every** mobile party: lord field parties, **caravans**, garrison parties, villager parties, militia, bandits. A handler that means "lords' field parties + the player" MUST gate on `party.IsLordParty || party.IsMainParty`. Do NOT use a proxy like "has a hero leader whose clan has a kingdom" — a player/AI **caravan is companion-led**, so its leader's clan + kingdom are non-null and the caravan slips through. Separately, a feature that handles garrisons via `DailyTickSettlementEvent` must exclude garrison parties from the party path (they are not `IsLordParty`) or it double-processes the garrison roster.
- **Why missed:** AlignmentDesertion Codex review (2026-06-27, MEDIUM). `OnDailyTickParty` gated only on `LeaderHero?.Clan?.Kingdom != null`, and the code's own comment asserted "caravans/villagers/militia/bandits have no leader clan" — false for caravans. The 5-agent deep-review's Data Flow agent reasoned about *which clans* are excluded (kingdomless/bandit) but inherited the same false premise instead of enumerating `MobileParty`'s party-type flags. Codex caught it by decompiling `CaravanPartyComponent` (companion `Leader`).
- **Prevent:** for any `DailyTickPartyEvent` / `MobileParty.All` handler, enumerate the party-type flags (`IsLordParty`/`IsMainParty`/`IsCaravan`/`IsGarrison`/`IsVillager`/`IsMilitia`/`IsBandit`) and gate explicitly on the ones you mean. Never trust "only lords are hero-led."
- **Source:** docs/reviews/rca-alignment-desertion-2026-06-27.md (Codex pass C1).

### A settlement game-menu option must gate on ACCESS — vanilla DISGUISE reaches the normal `town` menu while the player is HOSTILE
"Hostile settlements aren't enterable, so a menu option there can't be reached while at war" is FALSE. Vanilla `DefaultSettlementAccessModel` grants `LimitedAccessSolution.Disguise` for a hostile town: a disguised at-war player sneaks in and lands on the **normal `town` menu** (verified v1.4.6, decompiled `DefaultSettlementAccessModel.CanMainHeroDoSettlementAction` + `CanMainHeroTrade`, which gates disguised trade behind the `SmugglerConnections` perk). EliteEmissary's option keyed only on key-settlement + offers, so a player at war with Gondor could disguise into Minas Tirith and buy Gondor elites. (Codex review 2026-06-25 [HIGH]; the in-house 7-agent review's "vanilla interaction" dimension missed it because it assumed hostile = unenterable.)
- **Why missed:** the menu condition copied the "is this a key settlement + are there offers" shape from the data flow and never asked "by what PATHS can the player be standing in this menu?" — disguise is a non-obvious vanilla entry into a hostile town's menu.
- **Prevent:** any TAOM `AddGameMenuOption` on `town`/`castle`/`village` whose action should be relationship-gated (trade, recruit, faction-official interaction) must check access in the condition — at minimum `Campaign.Current.IsMainHeroDisguised` and `settlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)` (or route through `SettlementAccessModel.CanMainHeroDoSettlementAction` for the canonical gate, noting `CanMainHeroRecruitTroops` returns true for towns regardless of war/disguise — only `CanMainHeroTrade` gates disguise). Gate the menu condition (hide) AND the downstream dialog/buy condition.
- **Source:** docs/features/elite-emissary.md (Design Decisions, "Disguise/war access gate") + CHANGELOG 2026-06-25 EliteEmissary Codex review.

### Audit EVERY `GetType()`-keyed engine path when one engine type maps to many config variants
When a TAOM generic template instantiates **one** engine type for **many** logical config variants (LotrIssues: 27 Combat configs → `typeof(CombatLotrIssue)`, 14 Deliver → `typeof(DeliverGoodsLotrIssue)`), the engine's `GetType()`-keyed bookkeeping collapses all variants into a single object. Decompile the engine base type + its manager/behavior and enumerate **every** path that branches on the runtime type (`GetType()`, `.GetType() ==`, `is <Type>`, `Dictionary<Type,...>`, type-name cooldown keys), and confirm the collapsed-to-one-type behavior is acceptable for **each** — finding ONE and stopping is the trap. For `IssueBase` (v1.4.6) the full set is: (1) spawn over-representation score, (2) per-settlement/clan "already has this type" zero-out, (3) **accept gate** (`CheckPreconditions` → `IssueQuestCanBeDuplicated`, default `false`), (4) cooldown (`type.Name`), (5) despawn. (1)/(2)/(4) only throttle *spawning* (acceptable); (3) is a HARD block — default caps the player at ONE active quest per template across all configs until `IssueQuestCanBeDuplicated => true` is overridden.
- **Why missed:** Codex review #61 (2026-06-20) caught a HIGH that all 5 deep-review agents missed. The lifecycle agent read `IssueBase.CheckPreconditions` for the per-type spawn saturation but traced only the over-representation SCORE and stopped — it missed the SEPARATE hard accept-gate **in the same method**. One method, two type-keyed mechanisms; the soft one was found, the hard one shipped.
- **Prevent:** (a) when building/reviewing a generic-template-over-one-engine-type feature, grep the decompiled base type + manager for all `GetType()` comparisons and audit each; (b) for every type-keyed behavior you opt into (e.g. `IssueQuestCanBeDuplicated => true`), add a reflection/invariant test pinning the override so a refactor can't restore the breaking default (see `LotrIssueTemplateInvariantsTests`). Codified in `.claude/rules/csharp-architecture.md` "One Engine Type for Many Config Variants", the `/deep-review` Agent-2 SHARED-ENGINE-TYPE CHECK, and `AGENTS.md` (Codex side).
- **Source:** memory/feedback_shared_engine_type_enumerate_gettype_paths.md + docs/reviews/rca-lotr-issues-wave0-2026-06-17.md (Codex pass section)

### Audit the full downstream call chain for castle-unsafe dereferences when widening a settlement-type gate
Vanilla Bannerlord code paths gated by settlement type (`if (IsTown || IsVillage)`) frequently hide **castle-unsafe dereferences just past the gate**, because a castle's `Settlement.Village` is **null**. The moment you make castles eligible for a behavior they were excluded from, every method the now-castle-eligible objects flow through must be audited. Concrete castle NREs found building CastleRecruitment (2026-05-31): `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` line ~103 (`settlement.IsTown ? settlement.Town : settlement.Village.TradeBound?.Town` — castle `.Village` is null → NRE; the `?.` on `TradeBound` doesn't save the `.Village` deref); `DefaultVolunteerModel.GetBasicVolunteer` line ~113 (`sellerHero.IsRuralNotable && sellerHero.CurrentSettlement.Village.Bound.IsCastle` — NRE if a castle notable is `RuralNotable`). The fix was NOT to widen the vanilla gate + reuse its loop (that crashes), but to **reimplement the loop castle-safe** (use `settlement.Town` directly; pure slot-probability instead of the NRE-ing model method; castle-safe occupations only).
- **Why missed:** Not missed — the fill path was authored castle-safe from the start, so the castle-NRE findings were 0 in the review. The only surviving latent NRE (Finding 6) is the one path the feature deliberately doesn't call. Sibling of replicate-vanilla-safety-gates-in-prefix: this is the mirror — audit what a gate was *protecting* when you remove/widen it.
- **Prevent:** before enabling castle (or any new settlement-type) participation, trace EVERY method the settlement/notable flows through (volunteer model, notable spawn, AI scoring, recruit logic) and grep each for `.Village.`, `IsRuralNotable`, `IsVillage ?`-style assumptions. Research the full chain *before* widening.
- **Source:** memory/feedback_widening_settlement_type_gate_audit.md + docs/reviews/rca-castle-recruitment-2026-05-31.md (root-cause section)

### An `OnNewGameCreated`/`OnGameLoaded` handler that creates engine entities must be fail-safe — an escaped exception loops the loading state machine forever, not CTD
An exception escaping a campaign-loading-phase handler (`OnNewGameCreated`, `OnGameLoaded`) does NOT crash to desktop: `Campaign.DoLoadingForGameType` never advances its state, `GameLoadingState.OnTick` re-runs the SAME state every tick, and every handler re-fires each tick (behaviors re-register events; the same exception recurs ~30×/sec) — the player sees an infinite loading screen and must kill the process. Concretely (tester crash 2026-07-07): `CastleNotableMaintainer.EnsureAllCastles` called `HeroCreator.CreateNotable` with no template pre-check; `GetRandomTemplateByOccupation` returns **null** when the settlement culture's `NotableTemplates` has no entry for the occupation (stale module data on the tester's install), `CreateHero` NRE'd, and the new-game load looped 26,306+ suppressed crashes over 16 minutes. This is the 2nd occurrence of the missing-template NRE class — the 1st (#325) was guarded in `CultureConversionAdapter.ReplaceNotable` but the guard was never propagated to the OTHER `CreateNotable` call site.
- **Why missed:** the #325 lesson was recorded as a CultureConversion fix, not as a contract on `HeroCreator.CreateNotable` itself, so the sibling call site shipped unguarded; and the loading-phase handler was written like a daily-tick handler (throw = one bad tick) when its actual failure mode is a bricked campaign start. Dev data always had full template coverage, so no local repro ever fired it.
- **Prevent:** (a) every `HeroCreator.CreateNotable` call must pre-check the culture's templates with BOTH gates — a null-entry gate (`templates.Any(t => t == null)` → skip the culture: the engine's own occupation filter does NOT null-check, so one malformed `<notable_templates>` ref NREs every CreateNotable for the culture regardless of occupation) AND the occupation-match gate (`templates.Any(t => t.Occupation == occupation)`); a null-*tolerant* `Any(t => t != null && ...)` alone silently diverges from the engine's null-*intolerant* `Where` (deep-review finding, same day). Grep for `CreateNotable` when touching notable spawning; (b) any handler on `OnNewGameCreatedEvent`/`OnGameLoadedEvent` that creates/mutates engine entities wraps per-entity work in try/catch + ERROR log — skip-and-continue always beats a loading loop; (c) when an engine-pitfall guard lands in one call site, grep for the API's other TAOM call sites and propagate in the same change; (d) a pre-check that predicts an engine decision must replicate the engine expression's semantics EXACTLY (null-handling included) — verify by decompiling the engine expression, not by copying a precedent guard (a copied guard inherits the precedent's unverified assumptions; `CreateNotable` never returns null on v1.4.6 — it throws — so `== null` branches on it are forward-guards, not safety nets).
- **Source:** docs/features/castle-recruitment.md (Known Limitations, 2026-07-07) + CHANGELOG 2026-07-07 fix(castle-recruitment) + docs/reviews/rca-castle-recruitment-guard-2026-07-07.md; first occurrence: #325 / docs/features/culture-conversion.md

### Grep ALL ModuleData for collisions before shipping a substring-keyword match against engine strings
When a feature inspects an engine string (scene name, ID, faction key, settlement name) and decides behavior based on substring keyword matches, the keyword list is a correctness/security boundary against the FULL universe of strings the engine will produce — not just the strings seen in test sessions. Grep across all `Main/_Module/ModuleData/*.xml` for substring overlap **before** shipping the keyword list. If overlap exists: prefer an authoritative engine flag (e.g. `Mission.IsSiegeBattle`, `Settlement.IsTown` — most reliable), else a whitelist of EXACT strings (not substrings), else remove the keyword fallback entirely and document the constraint. Add a `[DataTestMethod]`/`[DataRow]` regression test pinning the new contract against vanilla strings AND TAOM-custom strings. Narrowing without removing leaves the same class of bug — re-grep after Codex/deep-review passes.
- **Why missed:** SiegeDismount /deep-review narrowed `SceneSiegeKeywords` from 5 to 3 substrings (`siege/assault/breach`) thinking the false-positive risk was contained to two TAOM castles (`castle_orthanc_gate`, `castle_gundabad_wall`). Codex review #34 grepped the full ModuleData and found 24 vanilla settlement `Location id="center"` rows using names like `empire_siege_001`, `khuzait_castle_siege_001` — those load as non-combat Missions where the engine flag is false; the substring fallback would have falsely triggered on every settlement-center cinematic. Fix: removed the fallback entirely; trust `Mission.IsSiegeBattle` exclusively.
- **Prevent:** for any `sceneName.Contains("X")` / `factionId.StartsWith("Z")` substring/prefix check against engine-produced strings, grep `Main/_Module/ModuleData/*.xml` (all settlement/faction/kingdom/culture XML, not just feature-specific custom XML) for the substring before shipping; switch to an authoritative engine flag or exact-string whitelist if overlap exists. Applies to all 7 features in the external-developer port (SiegeDismount, MixedFormations, SmartCavalryAI, FiefManagement, QuickActions, EquipPresets, CompanionTactics).
- **Source:** memory/feedback_substring_keyword_matches_external_data.md

### Per-hero GameModel: identify the engine's SUBJECT hero before claiming couple/family semantics
A GameModel that receives one `Hero` and returns a per-hero answer implements couple-/family-level behavior ONLY for the hero the engine actually passes in. Decompile the CALLER and find the subject-selection site before writing any rule of the form "X's flag prevents a couple/family outcome." Concrete case (v1.4.6): `PregnancyCampaignBehavior.DailyTickHero:92` gates on `hero.IsFemale` and `RefreshSpouseVisit:120` passes the FEMALE to `PregnancyModel.GetDailyChanceOfPregnancyForHero` — so an `immortal: true` race entry on a male hero (Sauron #321) never gated conception; his human consort (`lord_1_18`, race-unset) rolled at full fertility, and the CHANGELOG's "blocks any future children" claim was false as shipped. Fix: gate symmetrically — `TaomPregnancyModel` now also returns 0 when `hero.Spouse`'s race is immortal.
- **Why missed:** the race entry copied the saruman precedent and trusted the immortal flag's documented semantics ("blocks all fertility") without tracing WHO the engine passes to the model; the two prior immortal races never exposed the hole (wraith spouses stripped by NazgulFamily; Saruman unmarried). The feature doc described the model's internal steps accurately but was silent about the engine's calling convention.
- **Prevent:** for any per-hero model override (pregnancy, age death, wages, relations), decompile the engine caller and name the subject hero in the feature doc; implement flag-driven couple/family effects symmetrically (check both partners) or prove the subject is always the flagged party. TAOM's mixed-race couples make the asymmetric case the NORM, not the edge.
- **Source:** docs/reviews/rca-sauron-race-2026-07-02.md (finding 1)

### Map scene siege-slot tag counts are engine API contracts — 4 ranged per side, 3 attacker melee, or the map CTDs
The campaign map scene defines each fortification's siege-engine slots as tagged child entities of the settlement's map icon (`map_defensive_engine_*`, `map_siege_engine_*`, `map_siege_ram`, `map_siege_tower`), and the engine indexes those **scene-driven frame counts into fixed-size campaign arrays**: `SiegeEvent.SiegeEnginesContainer` hardcodes `DeployedRangedSiegeEngines[4]` per side and attacker `DeployedMeleeSiegeEngines[3]` (rams first, towers at `[ramCount + towerIdx]`; verified identical on 1.4.6 + 1.4.7). At least three unguarded consumer paths iterate scene counts against those arrays every frame of a player-joined siege: `SettlementVisualManager.TickSiegeMachineCircles` (map circles), `SettlementVisual.TickSiegeMachines` (engine visuals/bombardment), and `GauntletMapSiegeOverlayView`→`MapSiegeVM` (deploy UI). One extra tagged entity = guaranteed `IndexOutOfRangeException` CTD for any player siege at that settlement. Concrete case (crash bundle `4d003ae6`, 2026-07-12): town_LN1 (Rivendell town) carried 2 rams + 2 towers = 4 melee frames → `DeployedMeleeSiegeEngines[3]` on a length-3 array. The other 220 fortifications were exactly vanilla-shaped (4 def / 4 atk / 1 ram / 2 towers). The tag *suffix* (`_0.._3`) is NOT part of the contract — it only feeds a stable `OrderBy` sort for slot ordering; duplicate suffixes are cosmetic (16 fortifications had them, counts still 4, no crash).
- **Why missed:** the map was hand-authored/kitbashed outside any validation; nothing checks scene tag counts (validate_moduledata covers ModuleData XML, not `SceneObj/*.xscene`), and the crash only fires when the player *joins* a siege at the one bad settlement — AI sieges there tick fine, so it survived until a player defended Rivendell. The crash stack is 100% vanilla, which initially pointed suspicion at TAOM patches (`TaomSiegeEventModel`) — all exonerated; the log's TAOM `[SiegeDefense]` entries named a different settlement (town_G1) and were a red herring (TAOM's travel-there tracker, not the engine's `PlayerSiege` state).
- **Prevent:** after ANY map-icon edit in TAOM_Map, audit per-fortification tag counts against the caps (def ≤ 4, atk ≤ 4, ram + tower ≤ 3): parse `SceneObj/Main_map/scene.xscene`, walk each `town_*`/`castle_*` entity's `<children>` recursively, count the four tag families (audit script pattern: scratchpad `audit_map_siege_slots.py`, 2026-07-12 session; recreate from the CHANGELOG entry if needed). The counts-must-equal-vanilla-shape check (4,4,1,2) is stricter than the caps and catches under-counts too. A defensive C# clamp (Postfix truncating `SettlementVisual`'s frame arrays to the caps, next free category Patch62) was evaluated and declined 2026-07-12 — the map fix + audit discipline is the chosen prevention; revisit if a second scene-count CTD ships.
- **Source:** player crash bundle `taom_crash_20260712_072448_4d003ae6` + CHANGELOG 2026-07-12 fix(map) + plan `player-provided-the-following-bubbly-narwhal.md`

### Gate the MUTATION, not the EVENT, when standing a co-op client down

A behaviour that must not run on a co-op client usually reaches its world-mutating service method
from MORE THAN ONE registered handler. Gating the tick handler alone leaves every sibling handler as
an open bypass to the identical call.

- **Why missed:** the 2026-08-01 co-op work enumerated *global-tick subscribers* and gated those, which
  is enumerating entry points of one kind rather than paths to the mutation. `WarOfTheRingBehavior`
  also reaches `CheckPhaseTransition` (→ `DeclareWar`) from `OnSessionLaunched`, which fires on every
  peer — and a co-op join IS a save-load, so a joining client issued its own war set on connect.
  `WarOfTheRingMomentumBehavior` had 6 of 8 handlers ungated, one of them (`OnKingdomDestroyed`)
  reaching the same `CheckAndApplyVictory` → `EndWar`/`MakePeace` as the gated tick. `RegisterEvents`
  was read in both files while adding the constructor parameter; only the line being visited was gated.
- **Prevent:** when adding an authority gate, enumerate EVERY handler in the behaviour's
  `RegisterEvents` and follow each to its service calls; any handler reaching the same mutating method
  gets the same gate. Prefer gating inside the service where practical, so there is one chokepoint
  instead of N. Add a client-stands-down test per gated handler — the 2026-08-01 pass shipped 7 gates
  with 1 test until the completeness agent flagged it.
- **Source:** `docs/reviews/rca-coop-authority-gating-2026-08-01.md` (data-flow agent HIGH #1 and #2;
  no other agent found either, despite all five reading the same files)

### Presence is not authority: pick the co-op predicate by what the code DECIDES

TAOM has three co-op predicates and they are not interchangeable. Gating on module PRESENCE
(`ICoopPresenceProvider.IsCoopActive`) is correct only for one-shot startup decisions such as UI
registration. Anything that mutates or decides shared world state must use a session-aware
predicate.

- **Why missed:** presence is the obvious signal and reads naturally ("a co-op mod is running, so
  yield"). But it is process-constant — true whenever the module is merely ENABLED. Eight diplomacy
  and time-acceleration sites used it, which silently disabled TAOM's War of the Ring rules for two
  populations nobody was thinking about: a SOLO player who happened to have the Coop module enabled,
  and the co-op HOST, which is precisely the peer that should be enforcing them. The inverse error is
  just as easy: swapping wholesale to `IsAuthority` breaks BannerlordTogether, because that predicate
  fails open and reports every BT peer authoritative, so nothing gates at all. One reviewer proposed
  exactly that fix; the code already carried a comment explaining why it was wrong.
- **Prevent:** choose by question, not by habit — `IsAuthority` for "may I run this world-mutating
  handler"; `ShouldDeferToHost` for "should I yield this shared-world DECISION" (keys on whether the
  role probe resolved, so it stays safe for co-op mods TAOM cannot probe); `MayWriteSaveBackedState`
  before writing any field that round-trips through a `SyncData` key. That last one exists because
  `SiegeDefenseService.GrantReward` set the save-serialized `RewardClaimed` on every peer, so a
  client claiming its own siege reward wrote per-peer state into the host's save record. When adding
  a co-op gate, state which of the three questions you are answering before writing the condition.
  **A fourth predicate joined the roster on 2026-08-03, on a different axis:**
  `IDedicatedServerProvider.IsDedicatedServer` answers *what KIND of process am I* rather than *what
  ROLE do I hold in this session*, and the two are independent — a client-hosted session's host holds
  the authority role while being an ordinary game client, and must keep earning and deciding
  normally. Do not fold it into the role predicates; ask the process question separately.
- **Gate:** `CoopPresenceReadGateTests` fails a presence read (`.IsCoopActive`, `CoopPresence.IsActive`) in any file not on its allowlist.
- **Source:** `docs/reviews/rca-coop-authority-gating-2026-08-01.md` (Codex pass: 4 HIGH, of which
  the presence/session confusion accounted for two plus the LOW); fourth predicate added from the
  2026-08-03 multiplayer field report (commit c3ee2e22)

### A player-eligibility gate written as "the player COMMANDS X" silently excludes every case where the player is subordinate

Two unrelated features shipped the same mistake and it was found twice in one changeset.
(a) SpecialResources gated earning on the player being the winning side's `LeaderParty.LeaderHero`.
That is a commanding test, not a participating test — join any lord's army and you are not the leader
party's hero, so **every victory you fought in paid nothing, in ordinary single-player**. Multiplayer
only made it total: under a client/server split nobody leads the authoritative side either (one
session logged 33 fought missions producing a single `MapEventEnded`, with state `None`). Replaced by
`SpecialResourceEarnPolicy.IsPlayerVictory`, over the engine's own `MapEvent.PlayerSide` /
`MapEvent.WinningSide`, with `BattleSideEnum.None` on either side failing the gate.
(b) `WarEventSnapshotAdapter.FromSiege` set `PlayerInvolved = capturerParty?.IsMainParty == true`
while the BATTLE snapshot in the same file already used `IsPlayerRelated(party, playerKingdomId)` —
main party OR any party in the player's kingdom. Taking a settlement the normal way, inside someone
else's army, therefore recorded no player event and the War of the Ring victory requirement quietly
failed to advance. Sieges now use the same `IsPlayerRelated` test as battles.
- **Why missed:** "the player did X" reads as one idea and quietly resolves to the narrowest of its
  several meanings, because the case in the author's head is the solo player leading their own party.
  The siege half is worse than an oversight: the correct predicate was already written, in the same
  file, twenty lines above — so consistency-within-a-file was never checked against the file's own
  contents. Both gates also fail SILENTLY (no reward, no event) rather than throwing, so no report
  ever names them.
- **Prevent:** when writing a player-eligibility gate, say out loud what happens when the player is
  subordinate — in an army, in an allied siege, in a party someone else leads. Prefer the engine's own
  participation properties (`MapEvent.PlayerSide`, `IsPlayerMapEvent`) over reconstructing
  participation from leadership. If a sibling snapshot/handler in the same file already answers the
  same question, use ITS predicate rather than writing a second one.
- **Honesty clauses (both still true).** The siege fix is the **single-player half only** — crediting
  remote players in OTHER kingdoms needs a co-op seam TAOM does not have and is not claimed as fixed.
  And the field report's claim that the requirement "can only be satisfied by the authority's
  MainHero" was **inaccurate**: it was already satisfiable by any party in the authority MainHero's
  KINGDOM. Recording the corrected claim, not the reported one, is what makes this entry usable the
  next time a field report arrives.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commits c3ee2e22,
  1554de16

### On a headless/dedicated host `Hero.MainHero` EXISTS but is nobody's character

A dedicated server runs a full campaign, so `Hero.MainHero` resolves to a hero — the idle world-gen
hero the campaign was created around. Any logic that reads MainHero to mean "the player" therefore
acts on that hero there. SpecialResources did: dozens of `[SpecRes] PRISONERS: +N` lines banked
against the server hero while the remote players who fought the battles earned nothing.
`SpecialResourceEarnPolicy.MayCreditMainHero(isDedicatedServer)` now gates all five earn paths
(`OnMapEventEnded`, `OnRaidCompleted`, `OnPrisonerTaken`, `OnHideoutCompleted`,
`OnTournamentFinished`) through a private `CanEarn()` with a log-once.
- **Why missed:** MainHero is non-null and looks entirely healthy on a server, so nothing fails —
  the value is simply meaningless. The mistake is one level below the co-op predicates: those all ask
  what ROLE this peer holds, and the answer here is "authority", which is correct and does not tell
  you there is no local player.
- **Prevent:** treat "is there a local player" and "am I the authority" as different questions with
  different answers — a client-hosted host answers yes to both, a dedicated server answers no then
  yes. Derive the process kind from a fact about THIS process rather than by probing another mod:
  `DedicatedServerProvider` reads `Assembly.GetExecutingAssembly().Location` for
  `Win64_Shipping_Server`, because Bannerlord loads a module's binaries from the folder matching the
  running engine build. It fails to "not a server" on any error, which is safe only because every
  gate built on it purely SUPPRESSES behaviour — check that property before copying the pattern.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit c3ee2e22

### A ModuleData field's NAME is not its semantics: find the engine's READER before retuning it

A lord party template's `max_value` sum reads as "how big this party is". It is not. Verified against
the v1.4.8 decompile, 2026-08-14:

1. `PartyTemplateObject.GetUpperTroopLimit()` / `GetLowerTroopLimit()` (`PartyTemplateObject.cs:62`,
   `:72`) are plain sums of the stacks' `MaxValue` / `MinValue`, and **nothing in
   `TaleWorlds.CampaignSystem` calls either one**. Grepping the whole Campaign tree returns exactly the
   two definitions and no call site. (`_modules_build/NavalDLC__NavalDLC.cs` does call both; the base
   campaign never does.) The accessor that looks authoritative is informational.
2. The sum's real consumer is `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty`
   (`:427`), which builds the roster a party receives AT SPAWN by filling each stack to
   `MBRandom.RoundRandomized(min + (max - min) * r)`.
3. `r` comes from `GetInitialPartySizeRatioForMobileParty` (`:390`). For a lord party (not bandit, not
   player caravan, not patrol) it falls through to `party.RandomFloat()` (`:412`), which is
   `PartyBase.RandomValue / 2.1474836E+09f` over a value drawn once per party at construction, with no
   reference to the template at all. So the expected spawn roster is the MIDPOINT of the min and max
   sums, and raising the max sum raises it linearly.
4. It is not the steady state. `PartySizeLimit` still governs recruitment, so a party spawned above its
   limit cannot recruit and bleeds back down.
5. The same field also decides WHICH troops, on a path the sums never appear in: on a new game only,
   `HeroSpawnCampaignBehavior.SpawnLordParty` (`:262`-`:276`) tops the party up toward `PartySizeLimit`
   and picks each added man with `MBRandom.ChooseWeighted` over `(stack.MinValue + stack.MaxValue) / 2f`.

- **Why missed:** the request arrived in the units of the data file ("make Mordor's lord parties
  bigger"), the file has a field whose sum is exactly the number under discussion, and the engine
  ships a method named `GetUpperTroopLimit` that returns it. Three things agreeing is not evidence
  when all three are the same assumption. Nothing throws either way, so a retune aimed at the wrong
  quantity ships and reads as done.
- **Prevent:** before changing a ModuleData number, grep the decompile for the field's engine READER,
  and separately for CALLERS of the accessor that looks authoritative; an accessor that exists is not
  an accessor that runs. Then state the gap between what the field actually governs and what the
  request assumed, in the same message that reports the change, and repeat it in the tool's docstring.
  `tools/rebalance_party_template_maxes.py` opens by saying it moves the spawn roster and not the
  steady-state size, so the next session does not re-derive it.
- **Source:** 2026-08-14 culture balance pass (193 templates retargeted, 2,485 stacks changed). Full
  engine write-up with per-line citations:
  [`docs/reference/party-template-sizing.md`](../../reference/party-template-sizing.md). Companion
  lesson: "Two features writing to one `ExplainedNumber` can cancel each other out" in
  [gamemodels-services.md](./gamemodels-services.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/namegiving.md](../../features/namegiving.md)
- [docs/modding/party-templates.md](../../modding/party-templates.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)
- [docs/reviews/lessons/gamemodels-services.md](./gamemodels-services.md)

<!-- backlinks-end -->
### When a design rests on "these always tie", do the arithmetic for the case where they do not

The fief-grant rebalance (#458) was built on the claim that every finalist in a vanilla
`SettlementClaimantDecision` reaches `FullyPush`, ties at 3 points, and hands the win to the
highest-merit candidate. The self-vote arithmetic that motivates it is real (a clan backs itself at
roughly 40x what it gives a rival), but the conclusion does not follow: support costs 20/60/100
influence, `KingdomDecision.DetermineSupportOption` downgrades a vote the clan cannot afford, and
points are 1/2/3. A top-merit finalist holding 59 influence casts one point and loses to two poorer
finalists casting three each. Every non-mercenary clan votes as well, not just the three finalists.

- **Why missed:** the downgrade loop had actually been read. It was dismissed in one sentence ("the
  finalists are high-merit clans, so they can afford it") without writing down what "afford" costs.
  Re-reading the same file would never have caught it, because the file was read correctly and the
  error was in the inference layered on top.
- **Prevent:** any design premised on "these values always tie", "this branch is always taken", or
  "that case cannot happen" owes one worked counter-example before anything is built on it. Name the
  threshold (100 influence), name an input that crosses it (a clan with 59), and state what happens.
  If the counter-example is reachable, the design is a bias and not a guarantee: say so in the doc.
- **Source:** `docs/reviews/rca-fiefgranting-2026-08-14.md` finding C2, caught by the Codex pass after
  six Claude agents missed it.

### Making a hardcoded value configurable re-opens the correctness of every consumer, including untouched ones

TAOM's fast-forward branch set `SpeedUpMultiplier` and deliberately never touched the time mode. That
was correct for years for one unstated reason: its key was Space, which is also vanilla
`MapTimeTogglePause` (GameKey 63), so vanilla's handler performed the mode transition on the same
press. `Campaign.TickMapTime` applies `SpeedUpMultiplier` ONLY in the three fast-forward modes and
ignores it in `StoppablePlay` / `UnstoppablePlay` / `Stop` / `FastForwardStop`.

The moment the key became rebindable, a key rebound off Space set the multiplier onto a Play or Stop
mode and did nothing at all, with no error and no log line.

- **Why missed:** the branch was unchanged, so a diff-based review classified it as "no behavioural
  delta" and moved on. That verdict was true of the code and false of the system. The dependency was
  on the key's VALUE, and the whole point of the change was to stop that value being fixed.
- **Note the fix has two directions.** Unconditionally taking over the mode is also wrong: on the
  shared default it would force fast-forward on every press and the vanilla toggle would never toggle
  back. The condition is "my key differs from vanilla's", not "I am rebindable now".
- **Prevent:** when a constant becomes configurable, list every consumer of that value and re-derive
  its behaviour under a value that has never been used before, not under the shipped default. Ask
  specifically what OTHER system used to observe the same input, and what stops observing it once the
  value moves. Add at least one test that exercises a non-default value end-to-end.
- **Source:** 2026-08-22 rebindable Time Acceleration keys.
  `docs/reviews/rca-timeacceleration-keybinds-2026-08-22.md`.

### A standard game menu stops time and is persisted into the save; never leave the player in one after starting a timed process (camps field test, 2026-08-23)

The first field camp ever established read as a frozen game: establish switched into the camp
sub-menu (exactly as the source module did), a standard GameMenu stops campaign time
unconditionally with dead time controls, so "Raising 0%" could never progress, and MapState
persists the open menu id, so saving there made every load resume "frozen". Three static
hypotheses (inquiry pause leak, listener loop, camera lock) died against the decompile; a live
state dump (taom.time_status: MenuContext taom_fc_camp, AtMenu true, everything else clean)
settled it in one screenshot. Two rules: after starting anything timed, land the player on the
map or in a WAIT menu (AddWaitGameMenu; Enlistment precedent), never a standard one; and a
source-parity port of a UX trap is still a defect, so smoke the first-run player flow of every
port before calling it verified. Recovery in the field: taom.rescue_time.

### An invariant that holds only because two tuned numbers do not overlap is not enforced
Quitting an enlisted battle never paid standing, but nothing forbade it. It merely happened that the
best score a walkout could reach (45) landed in a band paying zero trust. The moment that band's
trust value changed by one, the protection disappeared with no test failing anywhere, because the
protection had never been written down. The reasoning even existed as a code comment, and the comment
was itself wrong: it computed the walkout ceiling as 45 "without kills" while the scorer counts kills,
so the sentence was true of one walkout and false of every other.
- **Why missed:** emergent guards are invisible by construction. Neither number looks load-bearing on its own, and the relationship between them lives in no file. A reviewer reading either side sees a legal value.
- **Prevent:** when a rule matters ("leaving the field must never pay standing"), state it in code at the point of payment, not in the tuning that currently happens to satisfy it. Here that is one ternary in the payout service, independent of where any band boundary sits, plus a test pinning the shipped numbers from both directions. Treat a comment asserting a numeric ceiling as a claim owed a re-derivation, not as documentation.
- **Source:** docs/reviews/rca-enlistment-standing-2026-08-28.md finding H1 and its root-cause section (#520).

### A condition on a vanilla dialogue line is a precondition on everything its consequence does; lifting it is widening a gate
Vanilla's companion fire line (`companion_talk_fire`) hides itself whenever `Settlement.CurrentSettlement`
is set, so it only ever runs on the world map. The #540 dismissal registered a broader line that ends in
the same engine call (`KillCharacterAction.ApplyByRemove`) and works inside keep and tavern scenes. What the
vanilla gate had been protecting: inside a scene the hero has a live `Agent`, and the removal path only
drops the `LocationCharacter` (next entry's spawn list), so the removed companion keeps standing there and
`MissionConversationLogic.IsThereAgentAction` never asks whether the hero is alive. Fixed by removing the
agent first (`Agent.FadeOut(hideInstantly: true, hideMount: true)`, the passage mechanism in the instant
form vanilla uses for a departing multiplayer peer), behind the engine's own ending-state guard. The
visible fade was tried first and rejected in the second review round: a fading agent stays `Active` for
the length of the fade and `IsThereAgentAction` never checks `IsFadingOut`, so a click in those frames
would open a conversation with a dead, un-clanned hero.
- **Why missed:** the gate looked like UX (why the player could not find the line in a town) rather than
  mechanics, so the existing widening-a-gate lesson (castle recruitment, above) was never applied to it. The
  question was written into the smoke list as "the path to watch" and deferred to a play test, although
  three decompiles of the installed SandBox assembly answered it.
- **Prevent:** when registering a line that reaches the same consequence as a vanilla line with a narrower
  condition, list the engine state the vanilla condition excluded and check each item against the
  consequence before shipping. And never defer to a smoke run a question a decompile can answer now.
- **Source:** `docs/reviews/rca-field-commission-dismiss-2026-09-04.md` finding 1 (#540).

### A party template sizes a party; the mission built from it has its own sizing code, so read that before promising a data fix
The #564 request was "cut the boss party templates so the hideout boss fight is 1 boss + 3-4". The
templates were 67-97 bodies and the fight was boss + 20..40, so the two looked like cause and effect.
They were not: `MapEventHelper.GetPriorityListForHideoutMission` (v1.4.8 :147-172) sends
`min(floor(0.8 * total), FirstFightMax)` of the WHOLE hideout to phase 1 and everything else to the
boss, and `HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight` (:478-555) spawns every
troop that was not a sentry, its `Clamp(pop / 2, 4, 20)` being a floor. The template only sized the
boss PARTY parked in the hideout, and mattered to the fight solely because
`ArrangeHideoutTroopCountsForMission` :615 never trims a boss party, so a fat one overflowed the cap.
A template-only change would have shipped, passed every test, and changed nothing the player saw on a
hideout at the cap.
- **Why missed (nearly):** the data and the symptom were both large, and the culture attribute is
  literally named `bandit_boss_party_template`. Nothing in the name says the mission does not read it.
- **Prevent:** before promising that a `PartyTemplate`, roster or spawn-list data change fixes a
  MISSION-side symptom (how many spawn, who stands where, what the boss brings), open the mission
  controller and the `MapEventHelper`/`TroopSupplier` path that builds the mission from the party, and
  find the line that decides the number. If it is a formula over the whole party set, the template is
  not the lever. Two routes into the same mission (assault, sneak-in) size themselves differently;
  read both.
- **Source:** #564, `docs/features/bandit-management.md` "Hideout boss fight: boss + N",
  `docs/reviews/rca-hideout-boss-fight-2026-09-11.md`.

### A record that feeds a later decision mirrors THREE engine sites, not the handler that fires beside it

The #565 siege participation record was first gated like vanilla's loot handler
(`SiegeAftermathCampaignBehavior.OnMapEventEnded`), which counts every won siege battle. Three
review rounds each found it one engine site short: the loot handler also counts a `SiegeOutside`
relief victory that captures nothing (`KingdomManager.SiegeCompleted` transfers ownership only for
Siege, SallyOut and BlockadeSallyOutBattle); the ballot admits only the capturing kingdom's
non-mercenary clans (`SettlementClaimantDecision.DetermineInitialCandidates`), while allies at war
with the defender may join the assault (`SiegeEvent.CanPartyJoinSide`) and would have set the top
share; and vanilla opens a claim only for a fortification taken by a kingdom with more than one clan
(`SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged` lines 39-42), so a capture that opens
no claim gets no grant and nothing ever clears its record.

- **Why missed:** the gate looked mechanical ("same test as vanilla's handler"), so the sibling was
  mirrored instead of the mechanism. Each round asked one question ("does it capture?", "who may
  win?", "will a grant follow?") and the previous round's fix answered only that one. The lesson
  "Same shape as the sibling is a design statement, not a correctness one" was already on file.
- **Prevent:** before writing any record that a later decision consumes, name three engine sites
  and derive the write and keep rules from all of them: the method that makes the state change real
  (transfer), the consumer's admission rule (ballot), and the condition under which the consuming
  decision will exist at all (claim opening). Put each rule in its own method with the site cited,
  as `FiefSiegeCaptureRules` does, and test each on bare engine objects. A handler that merely fires
  at the same moment is evidence of timing, never of scope.
- **Source:** `docs/reviews/rca-fiefgranting-participation-2026-09-11.md` (deep-review gaps 1 and
  2, Codex F1 and F2), #565.

### A persisted stamp remembers the LAST event, not the CURRENT one, and your own store is no exception

`Town.LastCapturedBy` is written only by `ApplyBySiege`, to the assault leader's clan, and never
cleared. TAOM read it as "who fought for this fief", so army members got nothing and a clan that
stormed a place months ago kept a 2.5x claim on every re-election. The replacement store then
reproduced the same defect twice in its first day: a record written on a battle that transferred no
ownership, and a record written on a capture that would never be granted, each readable by an
unrelated election later because nothing expired it.

- **Why missed:** a stamp reads like a fact about the settlement rather than a memory of one event.
  The original read had shipped once (#458) with a doc sentence explaining the stamp was "the only
  surviving record"; that sentence described the storage, not what it meant.
- **Prevent:** for every persisted per-entity value read at decision time, state which event wrote
  it, which events clear it, and whether the event being decided is the one it remembers. If any
  reachable path decides on a different event than the one that wrote it, either expire the value on
  that path or key the record to the event. A parser for the persisted form treats the writer's
  invariants as untrusted: duplicates, signs and magnitudes are validated on restore, not assumed.
- **Source:** `docs/reviews/rca-fiefgranting-participation-2026-09-11.md`, #565 diagnosis and Codex
  F2 and F3.

### "In an army" is two states, and vanilla's settlement menus only handle the merged one

`MobileParty.Army != null` says the party belongs to an army. `AttachedTo != null` says it has
physically merged into the leader's stack. Vanilla's `PlayerTownVisitCampaignBehavior` keys on the
first and assumes the second: "Leave" is hidden for every non-leader member (:994-1006), and
"Return to Army" (:368-376) switches to the army wait menu and leaves only for a village. For a
merged member that is right, since the leader's own `LeaveSettlementAction` (:13-26) finishes the
player's encounter when the army moves. For a member who is NOT merged the wait menu's first tick
re-routes through `GetGenericStateMenu()` to "You are waiting in X" (foreign faction, :295-298) or
to a wait nothing ends (own faction, :291-294), and the town has no exit. TAOM's Player Switcher
takeover inherits exactly that unmerged state from any lord marching to join an army (#566).
- **Why missed:** the report read as "the option waits instead of leaving", which sounded like a
  time-control or menu bug, and both of those have TAOM history (`taom.time_status`, the camps
  freeze). The first hour went to ruling TAOM out. The real question was one property read away:
  is the party ATTACHED, or merely a member? The vanilla flow for the unmerged case had never been
  traced because vanilla itself reaches it only through one conversation line.
- **Prevent:** when a player says they are "in an army", ask which of the two states before reading
  any menu code; the answer picks the whole flow. When a TAOM feature changes who the player IS
  (Player Switcher, enlistment, co-op), enumerate the party-level state the new identity brings
  with it (`Army`, `AttachedTo`, `CurrentSettlement`, `MapEvent`, AI behaviour) and ask what vanilla
  offers a player in each combination, not just what it offers the character-creation player. The
  fix for a vanilla dead end is a prefix that runs vanilla's own exit for the one row that traps,
  never a replacement of the whole method (`Patch87_ReturnToArmy`).
- **Source:** #566, `docs/features/return-to-army.md`.
- **Recurrence (2026-10-20, Far Watch):** a map sweep skipped every party with `Army != null && Army.LeaderParty != party`,
  so members that had joined but not merged (still moving on their own) vanished from the scout's report, and the
  leader's row took its composition from its own roster only. A party that moves on its own is `AttachedTo == null`;
  an army row sums the leader and each `AttachedParties[i]`. Source: `docs/reviews/rca-far-watch-2026-10-20.md`
  findings 2 and 3.

### A wait menu's condition delegate hides EVERY option, Leave included, and nothing exits the menu when it turns false

`GameMenu.GetMenuOptionConditionsHold` (1.4.8) evaluates a wait-menu option as
`option.condition && menu.OnCondition`. The delegate passed to `AddWaitGameMenu` is therefore not
"is this menu still valid"; it is a visibility gate over the whole panel, and the engine neither
ends the wait nor leaves the menu when it returns false. The camp sub-menu registered
`args => _camps.PlayerCamp != null`. Refuge founding, an option on that same menu, breaks the camp,
opens the garrison deposit screen, and returns: the player got "Choose how to make camp here." with
zero options, time controls alive, and `MapState` saved the open menu id, so every load resumed
there (#567, every release from v2.0.24 to v2.0.28).

- **Why missed:** the fix that introduced it (811a3429) closed the Class 9 freeze one option over
  and was reviewed against the forage and break paths on the sub-menu; the refuge option on the
  same menu belongs to another feature and was not walked. The parameter name (condition) read as
  a validity hook, and the Enlistment precedent uses the same shape with `IsEnlisted`, so it looked
  idiomatic. No test opened the menu with the guarded state absent.
- **Prevent:** pass `args => true` (vanilla's shape) unless the panel genuinely must hide every
  option; options gate themselves. When an action on a wait menu can remove the state the menu
  describes, that action lands on the map, and a wiring test pins the exit before any screen it
  opens. Before calling a menu-shape change verified, walk every option registered on that menu
  id, including options other features insert at reserved indexes. Enlistment's wait menu carried
  the same `IsEnlisted` delegate; #828 fixed it the same way.
- **Source:** #567, #828, `docs/features/field-camp.md` "Menus and time", `docs/features/refuge.md`
  Traps, `docs/reviews/rca-yotthani-camps-2026-08-23.md` Class 12.

### A modal opened from a game menu must pause the game, and its callback must revalidate the menu it came from

`MBInformationManager.ShowMultiSelectionInquiry` and `InformationManager.ShowInquiry` default
`pauseGameActiveState` to false, and the query layer (order 19501) stays usable over anything the
map pushes underneath, including the conversation an incoming enemy opens. The warden picker ran
unpaused; a bandit party reaching the camp replaced `taom_fc_camp` with `encounter_meeting` under
it, and confirming then ran `Found` and `GameMenu.ExitToLast()` against the ENEMY's menu.
`ExitToLast` exits whatever context is current, and with it gone the conversation's end had no
menu to switch to the encounter from (Codex review 98, F1; the #567 follow-up commit).

- **Why missed:** the fix that added the exit reasoned about the menu the picker was opened from,
  not about the menu that would be current when the callback fired. Five review agents and the
  author all walked "confirm, then exit"; none asked what can change while a modal waits. Vanilla's
  own pickers from game menus pass `pauseGameActiveState: true` (AlleyHelper).
- **Prevent:** pass `pauseGameActiveState: true` on any inquiry opened from a game menu whose
  callback mutates state or navigates menus, and make the callback refuse, before anything
  irreversible, when `CurrentMenuId` is no longer the originating menu or an encounter is live.
  Pin both beside the test that pins the exit. The pause is safe with a screen push inside the
  callback: the query manager registers and unregisters its disable request against its own
  instance (`GauntletQueryManager.HandleQueryCreated/Closed`, installed 1.4.8).
- **Source:** `docs/reviews/rca-yotthani-camps-2026-08-23.md` Class 12 (Codex table),
  `docs/reviews/REVIEW-LOG.md` Review 98, `RefugeMenuController.OnWardenChosen`.

### A guard that is correct in campaign time is wrong in real time once the multiplier is a player setting, and an adapter that raises the edge it subscribes re-enters its own reconciler
`ServiceAttachmentService.Assess` admitted `EnlistedBattle` and fell through to `AttachRequired` inside `PlayerEncounter.Finish`, after `FinalizeEvent` had taken the party out of its map event but before the encounter was cleared. The reconciler parked the party with no encounter guard; the park cleared `AttachedTo`, which pulls an active party off its `MapEventSide`; "Send troops" then indexed `BattleSimulation.SelectedTroops[-1]` (#577). The trigger was TAOM's own army disband: `LeaveArmy` and `CreateArmyLedBy` disperse the army they raised, `Army.DisperseInternal` nulls the commander's `Army`, the setter dispatches `OnPartyLeftArmy`, and that is the edge the maintenance behavior subscribes, so a full reconcile ran from inside the join or inside `PlayerEncounter.Finish`. At x64 Extra Fast Forward a campaign hour is about 52 ms of wall clock, the hourly tick fires once per crossed hour in a `while` loop, and the wait menu re-arms fast-forward at the retained multiplier, so the unguarded pass ran roughly 16 times more often than at 4x and every campaign-time discriminator in the feature (the 1-hour stale-latch window "no loot screen survives", the 1-hour retry budget, the 6-hour dwell) collapsed to tens of milliseconds.
- **Why missed:** `noBattleAnywhere` (with its `!HasCurrent` term) guarded `LeaveArmy` and the state demotion 160 lines above the `Assess` switch, so the author knew the hazard and applied the guard to two of three consumers. The loot-window regression test asserted the STATE and never that no park happened. `_reconcileInFlight` covered re-entry from a running pass, not from the adapter. Time acceleration appeared in no enlistment doc, issue or test, and the one calibration on record ("1 campaign hour in about 3 real seconds") was measured at 4x.
- **Prevent:** when a state has a window the engine owns (captivity, a live encounter), express it as one verdict in the single pure assessment (`Blocked(BattleEncounterOpen)`, placed by precedence) rather than as guards on individual arms, and pin the negative on every mutation (`DidNotReceive().ParkNear`), not only the state. When an adapter's mutation dispatches an engine event the feature subscribes, expose `IsMutating` and refuse the pass at the common entry. For every campaign-time threshold that models what the PLAYER experiences, ask what it becomes at the maximum multiplier the settings allow, and measure it in real time (`IRealTimeProvider`, the #436 precedent) or clamp it.
- **Source:** #577, the x64 detach; `docs/features/enlistment.md` "The presence hold".

Addendum, same review: the fix above keyed four new consumers on `EnlistmentState.EnlistedBattle`, and the deep review's data-flow pass showed the persistence layer coerces that state to `EnlistedAttached` on save with a re-derivation at load that had never been written, so a save at the encounter menu reopened #576 and reached #577's park by reload. `state-lifecycle-save.md` had carried this exact rule since 2026-08-08 ("coercing a transient state at save time silently breaks its own re-derivation contract"), filed under save; the work was filed under battle. A state that stands in for an engine fact is only as good as its persistence: re-derive it at load from the engine's own bits (`EnlistmentLoadNormalizer` now does), or key the gate on those bits. And a "consequence to watch in-game" written into a doc is an open issue, not a note.

### Count and mutate a roster through the same lookup, and read the mutation's return
`ItemRoster.GetItemNumber(item)` matches on the item alone (`FindIndexOfItem`, the first stack of any modifier) while `AddToCounts(item, n)` wraps `new EquipmentElement(item)` and finds only the unmodified stack, returning -1 and touching nothing when it is absent (`ItemRoster.cs:185-208`, v1.4.8). The supply consume counted with the first and deducted with the second and never read the return, so a modifier-carrying stack would have been billed and delivered without leaving the settlement. It was unreachable only because no trade good carries a modifier group, a fact the code stated in a comment and enforced nowhere.
- **Why missed:** the port review verified that goods are deducted (the donor module never deducted at all) but not that the count and the deduction address the same stack; the widening session restated the data fact and stopped there. A comment about the data is a claim the data can falsify.
- **Prevent:** count through `FindIndexOfElement` + `GetElementNumber` on the element you will deduct, deduct that element, and record the take only when `AddToCounts` returns an index. When two lookups on one roster must agree, make the code true regardless of the data, then write the data fact down as context, not as the guard.
- **Source:** deep review of #587, `docs/reviews/rca-supply-search-2026-09-13.md` finding 1.

### A cost on the upgrade path does not gate a troop a notable hands out; a pooled troop with upkeep needs a recruit cost too
`upgrade_cost` in `troop_resource_costs.xml` fires only on the party-screen upgrade (Patch26). `TaomVolunteerModel.GetBasicVolunteer` seeds an EMPTY notable slot straight from the `VolunteerRecruitmentService` pools, and vanilla's `MaxVolunteerTier` cap in `RecruitmentCampaignBehavior` gates only the growth of an OCCUPIED slot (`Tier < MaxVolunteerTier`, the `else if`), never the seed, so a pooled capstone arrives at any level and never passes Patch26. The Ithilien Ranger (10 percent at Minas Tirith and both Osgiliaths) and the Fountain Guard (`clan_empire_west_1`, and the vassal reward) shipped for a day with upgrade and upkeep costs and no `recruit_cost`: free to acquire, charged only to keep.
- **Why missed:** the justifying comment reasoned from the Ironpass rams, whose pooled rung is the L16 herder that CLIMBS into the gated breaker, and generalised that to "L41+ is upgrade-only"; the new test asserted the cost the author had added (`upgrade_cost > 0`) rather than a cost on every acquisition path; nobody grepped the pools for the 16 ids.
- **Prevent:** when gating a troop, enumerate its acquisition paths (upgrade, notable pick, emissary, prisoner, vassal reward, party template) and price each that should cost; a pooled troop pairs `recruit_cost` with `upgrade_cost`, priced at the rung (the upgrade cost), because the same attribute is charged per unit on ungated prisoner recruits (#563). `EveryVolunteerPooledUpkeepTroop_CarriesRecruitCost` (`TroopResourceCostDataTests`) unions `AllPooledTroopIds()` with every `recruitment_pools/*.json` and fails on a pooled upkeep row without one. A comment that says "covers every path" is a claim about paths the author has not listed.
- **Source:** deep review of #600, `docs/reviews/rca-gondor-castar-costs-2026-09-15.md` finding 1; agents 2 and 5 found it independently from the installed decompile.

### A multiplier applied after a vanilla brake cancels the brake; decide which one wins
`TaomPregnancyModel.ComputeBaseChance` reproduced vanilla's pregnancy chance, clan population brake included (`min(1, (2*cap - aliveLords)/cap)`), then multiplied by the race `fertilityMod`. At x2 an orc clan at 1.5x its cap (brake 0.5) bred at exactly vanilla's unbraked rate, so orc clans ran to the `2*cap` ceiling and stayed there (#628). Nothing errored; the numbers only looked generous.
- **Why missed:** "vanilla formula, then times the race factor" reads as a neutral composition. A brake that works by scaling toward zero is undone by any factor above 1 over half its range, and nobody asked what the multiplier did to it.
- **Prevent:** when a TAOM factor multiplies a vanilla result that already contains a limiter (a cap, a population brake, a clamp), decide which one wins and encode it. Here a bonus above 1.0 applies only when an NPC clan is at or under its cap (`aliveLords <= clanCap`); past it the modifier is clamped to 1, and a penalty below 1.0 applies everywhere. Pin the property as a test ("a bonus race never out-breeds the human rate at the same fill"), not only point values.
- **Source:** #628, `docs/reviews/rca-race-fertility-2026-09-19.md` item A.

### "Never recruited" is a claim about every roster transfer, not about one gate
Creature Bandits refused spiders, then bandit trolls, as prisoners in `TaomBattleRewardModel.CanTroopBeTakenPrisoner` and called them never recruitable. But with the Roguery perk Partners in Crime, "serve under my command" to a talked-down bandit party hands the player every bandit party that joins the encounter (`BanditInteractionsCampaignBehavior.OpenRosterScreenAfterBanditEncounter`, v1.5.3 :420-460), members not prisoners, so the prisoner gate is never asked; and a winning bandit party takes in freed bandit prisoners (`DefaultBattleRewardModel.GetLootPrisonerChances`), which broke "a band is trolls only".
- **Why missed:** the contract was proved at the one seam its author knew (capture), and the no-parley patch seemed to close the encounter surface, though it only sees the party the player talks to. Bandit clans are not at war with each other, so any nearby band joins the talked-down party's side.
- **Prevent:** before stating that a troop can never reach a party, enumerate the engine's roster transfers and check each: capture (`CaptureDefeatedPartyMembers`), freed-prisoner loot (`LootDefeatedPartyPrisoners`), the bandit join and surrender dialogues, prisoner recruitment, volunteers and party templates. A contract about a party's makeup ("trolls only") also covers who can join it after a battle.
- **Source:** #694, `docs/reviews/rca-troll-bandits-2026-09-28.md` findings 1 and 4; lenses 2 and 5 found the join path independently.

### Measure the live data before recommending a threshold, and never ship a threshold-only CareerQuest
"Heavy armour at Barracks level 1" was recommended to Mike as a gate; every live town starts at level 1 or more
(19 at 1, 33 at 2, 26 at 3), so it gated nothing. Separately, a CareerQuest whose objectives are all thresholds
(skill, renown, gold) completes inside `QuestBase.StartQuest` when the hero already meets them:
`CareerQuest.OnStartQuest` seeds the thresholds and calls `CompleteQuestWithSuccess` before the engine adds the quest
to `QuestManager`, which then keeps the finished quest in every later save.
- **Why missed:** the threshold was chosen from the design, not the data; the shell's ordering was invisible because
  no earlier quest was threshold-only.
- **Prevent:** before proposing a level, count, or chance to Mike, measure its distribution in the live install and
  put the numbers in the question. Give every CareerQuest at least one counted objective.
- **Source:** `docs/reviews/rca-armour-acquisition-2026-09-27.md` rows 6 and 8 (Data flow A and B, Engine).

### A resource's producer stops, or says why not, when its only consumer is done
Lord's materials kept dropping after the last ladder rung was claimed, when nothing could spend them: the roll
checked the win and the culture, never whether the hero still climbed.
- **Why missed:** the drop was designed and tested from the producer's side; no test asked what consumes a find.
- **Prevent:** for every new resource, name its consumers and test the producer once each consumer is exhausted.
- **Source:** `docs/reviews/rca-lords-gear-ladder-2026-09-28.md` row 3 (Data flow A and B).

### A new campaign creates its first issues before character creation: re-check a player filter while an offer waits
The new game's first issues are created while the campaign loads (traced with ilspycmd:
`IssuesCampaignBehavior.OnNewGameCreatedPartialFollowUpEnd`), and character creation starts only after the load
(`SandBoxGameManager.OnLoadFinished`, SandBox.SandBoxGameManager.cs:123-144, 186). Until then the main hero is
SandBox's `main_hero`, `Culture.battania` (SandBox `lords.xml`:11), so a filter on the player's culture at the offer
filters on the placeholder: "The Deep Seam" for Rhûn and Khand (cultures khuzait, battania) could open for any
player. A Player Switcher change moves the player to another culture the same way.
- **Why missed:** the filter was tested at the offer against a giver adapter that always held the final player
  culture; nobody asked when the first offers are made.
- **Prevent:** a filter on the player's state also goes in `IssueStayAliveConditions`, which the engine runs each
  day and when the player enters the settlement, and which drops only untaken offers (IssueManager.cs:262, 516).
- **Source:** `docs/reviews/rca-lords-gear-ladder-2026-09-28.md` convergence finding C3.

### "Is the player in a siege" is `PlayerSiege.PlayerSiegeEvent`, not `MobileParty.SiegeEvent`
`MobileParty.SiegeEvent` is `BesiegerCamp?.SiegeEvent` (v1.5.3 `MobileParty.cs:1164`): it sees only a besieging party. A player defending inside a besieged town or castle, between assaults, has no `BesiegerCamp` and no `MapEvent`, so a guard on those two lets him through. Vanilla's own question is `PlayerSiege.PlayerSiegeEvent`, which falls back to `MainParty.CurrentSettlement.SiegeEvent` (`PlayerSiege.cs:16-31`).
- **Why missed:** the member's name promised "the party's siege"; its one-line body was not read.
- **Prevent:** for the player, ask `PlayerSiege.PlayerSiegeEvent`; for another party, check both its `BesiegerCamp` and its `CurrentSettlement?.SiegeEvent`. Read the body of any engine member whose name is the whole of a guard.
- **Source:** `taom.spawn_creature_band`, `docs/reviews/rca-troll-bandits-2026-09-28.md` follow-up F2; lenses 1, 2, 5 and 6 found it independently.

### A fief's political owner is `Settlement.MapFaction`, not `OwnerClan.Kingdom` (2026-09-30)
v1.5.3 raises a town rebellion by creating a clan, not a kingdom (`RebellionsCampaignBehavior`), a clan leaving
its kingdom with rebellion keeps its fiefs, and an independent player can hold a castle with no kingdom. For all
three `OwnerClan.Kingdom` is null. `Settlement.MapFaction` (the owner clan's kingdom, or the clan itself) is the
engine's own answer, and it is what the settlement nameplates colour by.
- **Why missed:** Realm Borders' approved design assumed rebels found a kingdom, and the adapter keyed realms by
  `OwnerClan.Kingdom`, so rebel land drew as wild land.
- **Prevent:** key "who holds this" by `MapFaction`; when a design names "a kingdom created in play", check which
  engine path creates it.
- **Source:** `docs/reviews/rca-realm-borders-2026-09-30.md` finding 4.

### A claim about campaign options enumerates every loaded options provider (2026-10-01)
Skipping the character-creation options stage for a takeover (#704) was documented as "all but Iron Man can be changed in the campaign", read from vanilla's `DefaultCampaignOptionsProvider`. The official optional module Birth and Aging Options registers its own provider, whose life and death cycle is `DisabledLater` during creation and `Disabled` in the campaign, so it locks too whenever that module is loaded.
- **Why missed:** the claim was derived from the one provider in the core assemblies; providers from official optional modules live in their own DLLs, outside the decompile dump.
- **Prevent:** before stating what can be changed after creation, list every `ICampaignOptionProvider` in the load order (ilspycmd on each module DLL) and read each option's `CampaignOptionEnableState` in both its creation and gameplay lists.
- **Source:** `docs/reviews/rca-faction-ui-takeover-2026-10-01.md` T4.

### A fix keyed on an outcome enumerates the paths that produce it (2026-10-02)
Giving a taken-over lord their treasury back (Player Switcher) read "the handover took effect" from the session record, which is true on both handover paths. On the takeover path the record holds the lord's own treasury; on the adoption path it held the wanderer's purse plus the created character's, which `KillCharacterAction` hands to the new clan leader. Applied to both, the fix gave an adopted wanderer those two purses in place of the 1,000 plus culture gold every new character gets, while every test exercised the takeover path only.
- **Why missed:** the design was written from Mike's sentence about lords, and the record it read did not say which path it came from.
- **Prevent:** before a fix acts on an outcome, list every path that yields it (here `SwitchPlanner`: AssumeIdentity and AdoptIntoPlayerClan) and trace the value through each; key the fix on the path it is meant for (`IPlayerSwitchSession.LordTakenOver`), with one test per path.
- **Source:** `docs/reviews/rca-takeover-treasury-2026-10-02.md` G1.

### A value replaced at one event has one owner (2026-10-02)
The taken-over lord's treasury restore first ran as its own phase-9 `OnCharacterCreationIsOver` listener beside StartupResources' grant. Both replaced the engine's 1,000 gold at the same event, so the grant needed a skip whose only job was to keep the two apart, and the new listener copied StartupResources' start-type read. The design lens folded the restore into StartupResources, which deleted a behavior, its registration, its test file and a flag parameter.
- **Why missed:** the fix was built as a Player Switcher change, so it got a Player Switcher listener, though the decision it changed already had an owner in another feature.
- **Prevent:** before adding a listener that overrides a value another listener sets at the same event, extend that listener and have it call your feature's service. A skip that exists only to keep two listeners from colliding is the sign.
- **Source:** `docs/reviews/rca-takeover-treasury-2026-10-02.md` G9.

### A map event that cannot reach a round never picks a winner (2026-10-06)
v1.5.4 `MapEvent.Update` simulates a round only while both sides have healthy troops, and sets `BattleState` only inside a round, so a side that reaches 0 healthy between rounds leaves the event open for good. A destroyed party left attached adds a second lock: `CanPartyJoinBattle` refuses every joiner while any party is inactive. A player hit both in two campaigns and found the cause before TAOM did.
- **Why missed:** nothing in TAOM watched map events for age, and code that removes troops from a party (daily desertion, for one) never asks whether that party is in a battle.
- **Prevent:** the hourly `StuckBattleModule` sweep is the backstop. Code that removes or wounds a party's troops outside a mission should ask whether the party is in a map event before it runs.
- **Source:** player report "NPC battle that appears to be stuck" (#748, follow-up #750); [map-event-guard.md](../../features/map-event-guard.md#stuck-ai-battles).

### A sweep that decides other parties' outcomes asks who controls each one, not only whether this peer may tick (2026-10-06)
The stuck-battle sweep copied its siblings' `IsAuthority` gate, true on a BannerlordCoop host. But Coop's
`MapEvent.Update` prefix never runs vanilla for a non-raid event holding a party this instance does not control, and the
sweep's player-battle test saw only the local player, so on a host it would award a remote player's battle that then
never finishes. `IsAuthority` also fails open under BannerlordTogether.
- **Why missed:** `IsAuthority` answers "may this peer run campaign logic", the question the sibling hourly behaviors
  face (double-running on clients). Deciding a battle's winner is a decision about other players' parties.
- **Prevent:** a world-mutating pass over shared entities (battles, parties, settlements) either asks who controls each
  entity or stands down while `IsSessionActive || ShouldDeferToHost`, and says which in `coop-interop.md`'s gated table.
- **Source:** `docs/reviews/rca-stuck-battle-guard-2026-10-06.md` row 2 (Engine and Data-flow lenses, independently).
- **Repeat (2026-11-12):** LordRetinue shipped its first draft with `Clan.PlayerClan`, `MobileParty.MainParty` and `IsHumanPlayerCharacter` while the sibling it was copied from (`AlignmentDesertionBehavior`) used `IPlayerControlQuery`. Both lenses of the review found it. No gate scans a new behavior for local-peer statics; the follow-up is named in [rca-lord-retinue-2026-11-12.md](../rca-lord-retinue-2026-11-12.md).

### A map event with a winner is not always a battle, and RaidCompleted is not always a loot
A looting raid ends as an attacker victory, so `OnMapEventEnded` paid the battle credit on every raid;
`RaidCompleted` reports the attacker as winner for a won militia fight too, so a resisted raid paid
`per_raid` twice. The first fix keyed the credit on the event type and then zeroed real lord fights the
engine folds into raid and extortion events.
- **Why missed:** each handler trusted its event's name. War of the Ring momentum trusts the same event
  (#774).
- **Prevent:** key payouts on the engine's own verdict (`Village.VillageState == Looted`, set just
  before `RaidCompleted`) and on who was beaten (a field party with healthy men at the start), not on
  the event type; when an event turns out to mean more than its name, grep every listener of it.
  The same holds for completions: a hideout "send troops" clear raises `OnHideoutBattleCompleted`
  twice for one event (#790), so a payout keyed on an event checks a state the engine sets once (here
  `MapEvent.IsFinalized`), not a field the payout remembers, which a save and load would reset.
- **Source:** `docs/reviews/rca-career-e1e3-2026-10-08.md` (#770, #774).

### A resume path must reuse the start path's availability checks (2026-10-14)
The Eye of Sauron hunt's stand-off (the player rides in another lord's army) released the hunter and re-pinned
him an hour after the player left. The start path refused a hunter in an army, a battle, a siege or a town, or
under another order; the resume path checked only for a map event, so a hunter whose own AI led or joined an army
meanwhile was claimed and recorded as hunting while attached (vanilla attached members skip path movement and
the speed model). The save-load side repeated it: a stand-off save loaded into co-op released a pin the hunt no
longer owned.
- **Why missed:** the decision that added the second way into the state (resume) was tested from the player's
  side only, and the hunter's side was assumed to be as the stand-off left it.
- **Prevent:** a path that re-enters a state calls the same availability check as the path that first enters it,
  one method, not a copy. A release reads the live engine flag (the pin) before acting, not a flag the feature
  set earlier. After a maintainer decision adds a transition, re-run the lens that reads state transitions.
- **Source:** `docs/reviews/rca-eye-of-sauron-2026-10-14.md` F1 and F3 (Codex), fixed in `2d17ceba3`.

### A clan that changes kingdom moves its fiefs with no owner-change event (2026-10-14)
`ChangeKingdomAction` join, defection (`JoinKingdomByDefection`) and create set `clan.Kingdom` (v1.5.5
`ChangeKingdomAction.cs:65`), and rebellion (`LeaveWithRebellion`) sets it to null (`:76`). The clan keeps its
settlements, so each fief's map faction changes without `OnSettlementOwnerChangedEvent`. Only a plain
`LeaveKingdom` hands the fiefs over through `ChangeOwnerOfSettlementAction` (`:95-98`). The chronicle's open
siege windows noticed such a change only at their close (C02).
- **Why missed:** the design took one engine event as the ownership signal.
- **Prevent:** treat the owner event as a fast path, and re-check the owner of every watched settlement in the
  daily step, or also listen to `OnClanChangedKingdom`.
- **Source:** `docs/reviews/rca-war-chronicle-events-2026-10-14.md` C02, fixed in `7e8451555`.
- **Repeated (Namegiving, 2026-10-20):** the lore-name reconcile listened only to the owner event, so a fief
  whose clan changed kingdom kept the wrong side's name. Prevent at design time: any feature that reacts to which
  side holds a fief names both events in its brief, and its wiring test pins both. Source:
  `docs/reviews/rca-namegiving-2026-10-20.md` finding 3.

### A war veto gates where the engine prices or offers the war, not only where it declares it (#818, 2026-10-18)
The War of the Ring phase rule first sat only on `DeclareWarAction.ApplyInternal`. The engine charges before that:
`Kingdom.AddDecision` takes 200 influence from an AI proposer before `IsAllowed` cancels the decision, a declined
call-to-war offer costs 50 relation, a one-clan player kingdom gets a map notice without `IsAllowed`, and
`ChangeCrimeRatingAction` takes 10 relation before each refused declaration, every day the rating stays high. The war
never started, and every cost was still paid.
- **Why missed:** the design traced where wars start, not what each path costs before the start.
- **Prevent:** when you add a veto on an engine action, walk back from the action to every stage that charges a cost or
  shows the action to the player, and gate the earliest one: the score model for AI proposals
  (`GetScoreOfDeclaringWar`), `IsAllowed` for decisions, the notice itself for direct offers. Keep the action prefix as
  the backstop.
- **Source:** `docs/reviews/rca-wotr-war-rule-2026-10-18.md` findings 4 to 6 and 9.

### Before hiding a menu exit, check each remaining option is ENABLED, not just shown (#827, 2026-10-18)
Enlistment hid vanilla 'Leave' and 'Abandon army' in the commander's battle on the claim that 'Send troops' always
remains. 'Send troops' is shown whenever a healthy troop stands on the soldier's side, and it does not read the
hooked predicate, so both checks passed. But `MenuHelper.CheckEnemyAttackableHonorably` disables it, and 'Attack',
for a player outside an army who holds a safe-passage truce with the enemy faction. The no-army soldier was left with
two greyed options, and the player's own map event advances only through that menu, so the battle could never end.
- **Why missed:** the remaining options were walked for visibility and for whether they read the patched predicate,
  never for the paths that set `args.IsEnabled = false`. The #567 lesson above says to walk every option on the menu;
  this change walked them on one axis only.
- **Prevent:** when a change hides or disables a menu option, list every other option on that menu id with both its
  visibility condition and every `IsEnabled = false` path in it (honour, morale, raft, wounded), and name the one that
  stays enabled in each state the change can reach. If none is guaranteed, release the hidden option in the states
  where nothing else is enabled, and ask vanilla's own check rather than copy its conditions.
- **Source:** `docs/reviews/rca-enlistment-battle-exit-2026-10-18.md` F1.

### Read the raise path before you add a co-op authority gate; a per-entity periodic tick needs none (#886, 2026-10-18)
The castle recruitment listener on `HourlyTickPartyEvent` was built with `ICoopSessionProvider.IsAuthority` as its
first check, on a plan claim that the old `HourlyTickParty` postfix had run on co-op clients. The event is raised only
from `CampaignPeriodicEventManager.TickPeriodicEvents`, which BannerlordCoop's `PartyTickPatch` skips on a client, so
the gate was always true where it ran. Under an enabled co-op module it allocated and invoked by reflection for every
party every in-game hour.
- **Why missed:** the claim came from Harmony semantics (a postfix runs after a skipping prefix), not from the raise
  site. The lesson above ("enumerate EVERY handler and gate it") has no counterweight for handlers a client never runs.
- **Prevent:** before you add an authority gate, find the event's raise site in the installed engine and check what
  the co-op mod skips on that path (`docs/research/bannerlordcoop-internals.md`, `docs/features/coop-interop.md`). A
  per-entity periodic tick (`HourlyTickPartyEvent`, `DailyTickSettlementEvent` and their kind) gets no gate; say so in a
  comment next to the sibling handlers' co-op note. On any hot path, put a needed authority check after the cheap guards.
- **Source:** `docs/reviews/rca-castle-recruitment-seam-2026-10-18.md` finding 1.

### A toggle that says "AI stops doing X" must cover every engine consumer of the resource (#886, 2026-10-18)
"AI Recruits From Castles" gates TAOM's scoring gate and hourly listener, but vanilla's `OnBeforeSettlementEntered`
recruits from castle notables on every entry and `GarrisonRecruitmentCampaignBehavior` takes slot 0 daily, so AI keeps
taking castle volunteers with the toggle off. New docs in the same change also claimed a bind miss stops AI castle
recruiting.
- **Why missed:** the 2026-05-31 design listed the vanilla gates that block castles, not every reader and writer of a
  castle notable's `VolunteerTypes`. The model held only the path TAOM changed.
- **Prevent:** before a toggle, a hint or a doc says what the AI does or stops doing, list every engine consumer of
  the resource (grep the installed engine source for the field, for example `VolunteerTypes`), and either gate each one
  or word the promise to the paths actually gated.
- **Source:** `docs/reviews/rca-castle-recruitment-seam-2026-10-18.md` findings 4 and 5.

### An hourly party handler filters on the party kind before any probe, timer or service call (#822, 2026-10-18)
`HourlyTickPartyEvent` fires once per party per hour for all of `MobileParty.All`, 2,000 to 2,500 parties on a late
save. Watchful Holds first called the co-op probe, a timer and its service for every one of them, then scanned 234
forts with `foreach` over an `IReadOnlyList`, which boxes an enumerator per lord. Measured with the shipped Debug JIT,
its own 1 ms hourly budget held only below about 110 to 160 eligible lords. The same build copied the `IsAuthority` gate
of read-only siblings although it disorganizes other parties: the 2026-10-06 lesson above ("A sweep that decides other
parties' outcomes") already said such a pass stands down while `IsSessionActive || ShouldDeferToHost`.
- **Why missed:** the brief named the event and a budget but no measurement, and it took the co-op gate from sibling
  hourly behaviors instead of from the lesson for passes that change other parties.
- **Prevent:** in the behavior, return on the cheapest engine field (`IsLordParty || IsMainParty`) before anything
  else; scan an array by index; cache per-campaign lists in the service's reset; measure a budget in the Debug JIT the
  players run before writing it down. Choose the co-op gate by what the handler changes, and add the feature's row to
  `coop-interop.md`'s gated table in the same change.
- **Source:** `docs/reviews/rca-watchful-holds-2026-10-18.md` findings 1 and 5.

### An option inserted into another feature's menu registers after every session listener (Host Review, 2026-10-19)
`GameMenu.AddOption` inserts at the live index, and `OnSessionLaunched` listeners run newest first, so a feature
module's option is added before the hand-wired behaviors build the menu around it. Host Review's index 5 landed before
Leave only because of that order; moving Refuge into the module list would have put it after Leave.
- **Why missed:** both listener orders were walked and gave the right answer; nobody asked why.
- **Prevent:** an option added to a menu that another feature owns registers from `CampaignEvents.OnAfterSessionLaunchedEvent`,
  which runs after every session listener (`Campaign.cs:754-755`; vanilla `VillageHostileActionCampaignBehavior` does
  the same).
- **Source:** `docs/reviews/rca-host-review-2026-10-19.md` row 7.

### A menu consequence that can fail part way must know what menu state each earlier step left (#828, 2026-10-19)
Enlistment's "Go on your way" exit (an isLeave option on the service wait menu) first treated "return" and
"exit to the map" as neutral on every failure branch. They were not: the engine calls `EndWait()` before an
isLeave consequence on a wait menu (v1.5.5 `GameMenu.cs:284-287`), so a branch that keeps the menu must re-enter
it; `PlayerEncounter.Finish` exits the menu itself when a context exists, so a later "keep the menu" must re-open
it; `ExitToLast` without a context does nothing. The owed in-game check made the same mistake one level up: it
expected the service menu after a load in a town, but the load-time discharge's own `Finish(true)` clears
`MapStateData.GameMenuId` when no menu context exists (`PlayerEncounter.cs:988-991`).
- **Why missed:** the plan reasoned forward from the defect report and listed the happy path; nobody wrote down
  the menu state after each engine call on each failure branch, or walked the discharge's own release branch
  during `OnGameLoaded`.
- **Prevent:** for every menu consequence with more than one branch, write the menu and wait state after each
  engine call it makes (EndWait, Finish, ExitToLast, SwitchTo) and end every branch in a stated state: a re-entered
  menu, an opened menu, or the map with no live encounter. Write an in-game check only after tracing what the load
  path does to the saved menu id for that exact save shape.
- **Source:** `docs/reviews/rca-enlistment-discharged-menu-2026-10-19.md` F1 to F5.

### Before adding a reward for an engine event, list what vanilla already pays at that event (#861, 2026-10-18)
Bonds of Battle paid relation after battles without reading what vanilla pays there. Vanilla's thank-you conversation
after a win already pays the side leader the player came to help (`PlayerEncounter.DoPlayerVictory`,
`BattleRewardModel.GetPlayerGainedRelationAmount`), and `HideoutCampaignBehavior.SetCleanHideoutRelations` pays +2 to
one notable in each nearby village after a hideout. Player relation also does not necessarily land on the lord named: under
`DefaultDiplomacyModel.GetHeroesForEffectiveRelation` it lands on both clan leaders (a clan leader, a clanless hero or a companion
receives it himself), so one grant per lord stacks per clan, and `ChangeRelationAction.ApplyPlayerRelation` drops its `affectRelatives` argument.
- **Why missed:** the design started from the event and the new rule, not from the engine's existing consumers.
- **Prevent:** before a new relation, gold or influence reward on a campaign event, grep the decompile for every
  `ChangeRelationAction`, `GiveGoldAction` or `GainKingdomInfluenceAction` call reached from that event and state in the
  feature doc which ones the new rule stacks with. Decide per feature whether a grant counts per hero named or per
  effective recipient (`GetHeroesForEffectiveRelation`); Bonds of Battle decided it on 2026-10-19: once per effective recipient (one per lord with Personal Relations engaged, one per clan without it).
- **Source:** `docs/reviews/rca-bonds-of-battle-2026-10-18.md` finding 1 and the open design items.

### A settlement's own PartyBase sits in hideout, siege and raid map events (#861, 2026-10-18)
`MapEvent.Initialize` sets `defenderParty.MapEventSide = DefenderSide`, and a hideout battle's defender is
`Settlement.CurrentSettlement.Party`, which has no `MobileParty` and no leader. A rule "every enemy party is a bandit"
was therefore false in every hideout battle.
- **Why missed:** the adapter assumed every party in `PartiesOnSide` is a mobile party.
- **Prevent:** a predicate over `MapEvent.PartiesOnSide` skips parties whose `MobileParty` is null, or states why it
  counts them; branch on `MapEvent.IsHideoutBattle`, `IsSiegeAssault` or `IsRaid` explicitly when the rule differs.
- **Source:** `docs/reviews/rca-bonds-of-battle-2026-10-18.md` finding 1.

### Before raising a relation cost, list every hero the loop can reach and every listener on a negative change (#862, 2026-10-19)
Personal Relations raised the cost of disbanding an AI army to 13 to 17 for each member. AI armies can call
player-clan companion parties (`DefaultArmyManagementCalculationModel` excludes only the main party, the faction leader
and `!CanJoinArmy`), `DisbandArmyAction` charges every non-main party leader, and
`CompanionRolesCampaignBehavior.OnHeroRelationChanged` removes a companion for good when a negative change leaves the
relation below -10.
- **Why missed:** the plan read the cost's only caller, not who can be in the collection it loops over, nor what reacts
  to the change it causes.
- **Prevent:** for any new or larger negative relation change, list the heroes the engine loop can pass (player clan,
  companions, family) and grep the decompile for `HeroRelationChanged` listeners. Exclude player-clan heroes or keep
  vanilla's amount for them.
- **Source:** `docs/reviews/rca-personal-relations-2026-10-19.md` finding 1.
- **Repeat (#874, 2026-10-24):** The King's Muster planned a TAOM filter over the same candidate list to keep
  player-clan parties out of AI armies, from a reading of `DefaultArmyManagementCalculationModel.cs:131` (v1.5.5) that missed `!CanJoinArmy`. That flag is a saved
  per-party switch the player sets in the clan screen (`PartyConfiguration`, "Allow joining other armies"), so the
  filter overrode the player's choice and was deleted. Before adding a filter over an engine candidate list, quote
  every condition of the engine's own filter line in the plan, and check `PartyConfiguration` (`CanJoinArmy`,
  `CanRaid`, `CanDonateTroopsToGarrison`) for an existing player switch. Source:
  `docs/reviews/rca-kings-muster-2026-10-24.md` finding 1.

### An override that changes who owns a relation must be checked against every writer that assumes the old owner (#862, 2026-10-19)
Keeping the player's relations personal broke three engine paths that use the clan leader to mean the clan:
`ChangeClanLeaderAction` (the heir takes 0.7 x the old leader's raw relation), the blood feud and execution penalties in
`ExecutionCampaignBehavior`, and battle and raid aftermath between clan leaders.
- **Why missed:** the plan reviewed readers of `GetHeroesForEffectiveRelation`, not writers that name `clan.Leader`.
- **Prevent:** before changing the effective-relation mapping, grep the decompile for `Clan.Leader` and `.Leader` next
  to `ChangeRelationAction`, `SetPersonalRelation` and `GetRelationChangeAfterClanLeaderIsDead`, and decide each one:
  override, document, or follow up.
- **Source:** `docs/reviews/rca-personal-relations-2026-10-19.md` findings 2 and 5.

### Leave a wait menu inside a settlement through the engine's state menu, never with a bare ExitToLast

`GameMenu.ExitToLast` closes the menu context only. Inside a settlement the party keeps `CurrentSettlement`
and its player encounter, so it cannot move or start a new encounter, and `Campaign.Tick`'s "no menu open"
safety net asks `EncounterGameMenuModel.GetGenericStateMenu()`, which has no case for a village: the party is
stranded, and a save keeps the state. In a besieged town or castle, landing on the plain `town` or `castle`
menu is the opposite fault: its Leave has no siege check, so the party walks out without a break-out.
Vanilla's own settlement wait menus route through `GetGenericStateMenu()` every tick
(`PlayerTownVisitCampaignBehavior.SwitchToMenuIfThereIsAnInterrupt`).
- **Why missed:** From the Ashes copied FieldCamp's exits. FieldCamp's `ExitToLast` is safe because a camp
  menu has no settlement behind it; the brief named the shape and not that condition. Second feature in two
  days where a copied pattern lost the reason it was safe (`rca-postern-gate-2026-10-18.md`).
- **Prevent:** a TAOM wait menu inside a settlement leaves through one helper: `GetGenericStateMenu()` when it
  names a menu, else the settlement's own menu, else `IEncounterAdapter.Finish(forcePlayerOutFromSettlement:
  true)`; `ExitToLast` only when the party is in no settlement. Pin it with a source test. When a brief names
  a pattern to copy, it names the condition that makes the pattern safe.
- **Source:** `docs/reviews/rca-from-the-ashes-2026-10-19.md` findings 1 and 2.

### Size a skill gate from the XP the hero actually gains, not the grant (#936, 2026-10-19)
Enlistment sized its Leadership promotion gates from the v1.5.5 XP table and the daily grant (10 XP), and the
feature doc said the learning rate "only makes a gate easier". Every grant through `HeroDeveloper.AddSkillXp`
(default `isAffectedByFocusFactor: true`) is multiplied by `CalculateLearningRate`: `1.25 * (1 + 0.4*attribute +
focus)`, with a factor `-1 - 0.1*(skill - limit)` above the learning limit `max(0,(attribute-1)*10) + 30*focus`,
floored at 0. A new hero (attribute 2, no focus) gains 22.5 XP a day up to skill 10, then less, and stops at 18;
attribute 1 with no focus stops at 4. The old gates of 20 and 50 were unreachable for such a hero.
- **Why missed:** the plan read the XP table and the grant size and skipped the scaling between them; the test then
  pinned the derived numbers instead of re-deriving them.
- **Prevent:** before gating on a skill level that a passive grant must reach, simulate the grant through the learning
  rate and limit for the weakest normal hero, from the shipped config, in a test that a raised gate turns red. Say in
  the doc which hero the gates were sized for.
- **Source:** `docs/reviews/rca-enlistment-rank-ladder-2026-10-19.md` F1, F2.

### A record taken on `TickEvent` misses every state that stops time

Hidden Hosts stamped the exact troop counts of hosts the player meets, talks to or shares a settlement with from
`CampaignEvents.TickEvent`. `Campaign.Tick` raises that event only while `_dt > 0` (or in the first three ticks), and
every plain game menu sets the time control to Stop, so a town, castle, village, encounter or conversation visit was
never stamped, while the player-facing popup promised it. The stamp throttle also summed `TickEvent`'s dt, which is
campaign time (0.25 x real time at play speed), while the doc said real time. The fix stamps on `GameMenuOpened`
(raised by `GameMenu.RunOnInit` for every menu, after its `OnInit`) and keeps a real-time throttled tick for waits.
- **Why missed:** the trap line "Menus stop time" and `docs/reference/engine/campaign-tick-time-and-party-ai.md` both
  held the fact, but the trap line did not name `TickEvent`, and the behaviour tests called the handler by hand.
- **Prevent:** before subscribing to `TickEvent` for anything the player can trigger from a menu, list the states the
  record must cover and the event that fires in each; menu time is `GameMenuOpened`. Pin the engine's gate with an IL
  test, and throttle in real time (`Stopwatch`), never on the tick's dt.
- **Source:** `docs/reviews/rca-hidden-hosts-p23-2026-10-19.md` finding 1.

### Decide "did he fight" from one owner, and let every other signal only narrow it (#830, 2026-10-19)
Enlistment's sit-out gate first paid a battle as fought whenever a merit sample existed and as sat out
whenever the simulation flag was set and a one-slot ledger was empty or held any verdict. Clicking Attack,
retreating at once and then letting the others fight left a sample, so the soldier kept renown and paid no
trust; an empty ledger was read as "forced open", so a battle he led could be paid as a sit-out.
- **Why missed:** three signals (mission sample, `MapEvent.IsPlayerSimulation`, the ledger) each answered a
  slightly different question, and the payout combined them without naming which one owns the answer.
- **Prevent:** when a reward depends on what the player chose, record the choice at the moment he makes it (the
  verdict on the option he clicked), give "nothing recorded" no meaning beyond "no choice", and make every other
  signal a narrowing of that record. Test the walkout and the led-battle paths explicitly.
- **Source:** `docs/reviews/rca-enlistment-sit-out-2026-10-19.md` F1, F2.

### A map event's type is not its fight: trace every way a party enters a running event (#831, 2026-10-20)
Enlistment first skipped any Raid event its commander led as attacker, on the reasoning that a raid is
looting, not a battle. The engine reuses one Raid event for the militia fight, for a lord who comes to
relieve the village (`StartBattleAction.Apply` keeps the type `Raid` and adds the relief lord to the
running event) and for the looting, so the soldier would sit out a real lord battle.
- **Why missed:** the plan traced how a raid starts (`ApplyStartRaid`), not how other parties join a
  running one; the event type was read as a description of what is happening inside it.
- **Prevent:** before a rule keys on `MapEvent.EventType` or a side, grep every caller that adds a
  party to an existing event (`StartBattleAction.ApplyInternal`, `MapEventSide` setters) and list each
  fight shape the event can hold. State in the doc which shapes the rule covers and which are a known
  gap; test each listed shape.
- **Source:** `docs/reviews/rca-enlistment-village-raids-2026-10-20.md` F1, F2.

### A new-game pass over heroes runs after the follow-ups, at adult age (#952, 2026-10-20)
The face pass first ran on `OnNewGameCreatedEvent` and rolled each hero at the hero's own age. Initial children are cloned after that event (follow-up 0), so they copied the shared faces, and the native roll picks its face table by age, so young heroes had hair and beard reset to `Bald` and `Cleanshaven`.
- **Why missed:** the design copied vanilla's roll call but not its input (vanilla always rolls an adult range) or its timing.
- **Prevent:** hook a pass that must see every new-game hero to `OnNewGameCreatedPartialFollowUpEndEvent`, and roll faces at `BodyProperties.DefaultAge` (30).
- **Source:** `docs/reviews/rca-followups-949-952-2026-10-20.md` F2, F3.
- **Resolved (#954, 2026-10-20):** relief lords are joined; see docs/features/enlistment.md.
### Before you gate on an engine event argument, trace every raise site and read what it holds at that moment (#864, 2026-10-20)
Good Deeds gated its release reward on `HeroPrisonerReleased` with `party == MainParty`, on a plan claim that this
covered the conversation and the party screen. The party screen's transfer runs `Hero.OnRemovedFromPartyAsPrisoner`,
which nulls `PartyBelongedToAsPrisoner`, before `EndCaptivityAction.ApplyInternal` reads it, so that release raised the
event with a null party and paid nothing. A conversation release in the player's own dungeon carried the settlement's
party, and the gate paid nothing there either. Related to the #886 entry above, which is about the same habit for an
authority gate.
- **Why missed:** the argument's name said "the party the prisoner left", and the plan trusted the name. No one opened
  the party-screen raise site, and the tests fed the adapter a party.
- **Prevent:** before gating on an event argument, list every raise site in the installed engine (grep the action and
  the screens that call it) and write down what the argument holds at each. Read the raw facts in the adapter (`ReleaseSource`
  and the active `PartyState`'s `RightOwnerParty`), accept a settlement whose `OwnerClan` is the player's clan, and never accept a
  player-clan mobile party: `MobileParty.RemoveParty` releases its prisoners `ReleasedByChoice`. Give each raise site its own test.
- **Source:** `docs/reviews/rca-good-deeds-2026-10-20.md` finding 1 and 2.
### Work that an encounter with the camp must stop listens to `OnPartyEncounterEvent`, not `MapEventStarted`

Far Watch ended a running scout ride on `MapEventStarted`. An attack on the player's camp first opens
`encounter_meeting` (`DefaultEncounterGameMenuModel` answers a plain meeting with no battle started), so the map event,
and the event, come only if the meeting turns hostile. A party talked down or given safe passage left the ride running
while the docs said any encounter ends it. The fix also listens to `CampaignEvents.OnPartyEncounterEvent`
(`IMbEvent<PartyBase, PartyBase>`, raised as the last call of `EncounterManager.StartPartyEncounter`, on the main
thread), gated on a running ride and on the main party being attacker or defender, and switches no menu, because the
engine is opening its own.
- **Why missed:** the design cited this file's lesson "A modal opened from a game menu must pause the game" for its
  pause rule; the same entry records that a bandit reaching the camp replaces the camp menu with `encounter_meeting`,
  and that fact was not carried into the design's list of end conditions. The IL pin proved `PlayerEncounter.Init`, not
  the attack path.
- **Prevent:** for "an encounter ends X", subscribe to `OnPartyEncounterEvent` and keep `MapEventStarted` for the battle
  that may follow; pin the raise site and the meeting answer with IL tests. When a design cites a lesson, list the
  engine facts in its body that touch the new feature, each with the test that pins it.
- **Source:** `docs/reviews/rca-far-watch-2026-10-20.md` finding 4.
### Before you claim a co-op hazard in TAOM code, read the co-op mod's patches on the engine method TAOM calls (#920, 2026-10-20)
A review found that TAOM's hourly castle recruit excludes only `MobileParty.MainParty`, and an issue was filed saying a
remote player's party could recruit on a BannerlordCoop host. It could not: BannerlordCoop prefixes
`RecruitmentCampaignBehavior.CheckRecruiting` with `!mobileParty.IsPlayerParty()`, and a Harmony prefix runs for any call
of the patched method, TAOM's bound delegate included.
- **Why missed:** the claim was marked `[Likely]` and filed as fact; the check covered TAOM's guard, not the co-op mod's
  patches on the method TAOM calls.
- **Prevent:** before you file or fix a co-op hazard, grep the co-op mod's source (`E:\repos\BannerlordCoop`) for the
  engine method TAOM calls, not only for the TAOM caller. A delegate bound with `Delegate.CreateDelegate` or a reflective
  call goes through every Harmony patch on that method. Verify a `[Likely]` finding before it becomes an issue.
- **Source:** `docs/reviews/rca-castle-recruitment-seam-2026-10-18.md` addendum, finding A2.

### A mercenary contract sets `Clan.Kingdom`, so a hired company is not kingdomless (#957, 2026-10-20)
Comments and docs called a mercenary player or a hired minor clan "kingdomless" and said its row in `alignment.json` decides. `StartMercenaryServiceAction.ApplyStart` sets `clan.Kingdom` to the employer, so a hired company's map faction is the employer and its heroes side by the employer's pair. Only an independent clan, and an enlisted player (enlistment deliberately does not join the commander's kingdom), have no kingdom.
- **Why missed:** "mercenary" reads as "serves no kingdom" and nobody opened the action.
- **Prevent:** read the action (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Actions.StartMercenaryServiceAction`) before writing what a status does to `Clan.Kingdom`. A minor clan's own row applies only while it serves no kingdom.
- **Source:** `docs/reviews/rca-tokens-of-esteem-sides-2026-10-20.md`, wave 1, comments.

### A save keeps `Clan.Culture` and the template list, and the Factions file does not reload on a load (#957, 2026-10-20)
`Clan.Culture` is a `SaveableProperty` and `Clan._minorFactionCharacterTemplates` a `SaveableField`. `SandBoxManager.InitializeSandboxXMLs(isSavedCampaign: true)` skips the Kingdoms and Factions files, while `NPCCharacters` always reloads. So an XSLT change to a clan's culture or templates reaches a new campaign only, and an old save pairs retagged troops with a clan on its old culture: the Grey Company (clan culture `empire`, Evil) preferred Dunland contracts while its `arthedain` troops deserted under Dunland.
- **Why missed:** the first doc said "the clan's own culture is part of its definition and also changes at once", which is true of a new campaign and false of every save.
- **Prevent:** for any data edit to a saved object (a clan, a hero, a settlement), name which fields the save holds and which file the load skips; a data-only change to a saved field needs a load migration or a documented new-campaign-only limit. `MinorClanMigration` is the pattern: a pure service decides from raw facts, an adapter applies, a behavior runs it on `OnGameLoadedEvent`, and a test pins its targets to the shipped data.
- **Source:** `docs/reviews/rca-tokens-of-esteem-sides-2026-10-20.md`, wave 2, finding D1.

### Act on diplomacy from a tick, not from inside the war and peace events (#832, 2026-10-20)
Enlistment first followed the commander's wars from `WarDeclared`, `MakePeace` and
`OnClanChangedKingdomEvent`. Those events fire inside `ChangeKingdomAction`, which clears a clan's
kingdom and then re-declares the old wars one event at a time, so each handler saw a half-built
enemy list: the soldier made peace with every mirrored faction and declared war again, and a
declaration against the kingdom whose war list the engine was iterating could throw.
- **Why missed:** the plan checked that each event fires after its own state change, not what the
  enclosing action does around it.
- **Prevent:** a TAOM rule that declares war or makes peace in answer to world diplomacy runs from a
  campaign tick (hourly or daily) and reads settled state; never from an engine war, peace or kingdom
  event. Bridge the lag where it matters (here a join-time top-up). Keep such steps off the load path,
  which runs before `MapEventManager.OnAfterLoad`. No gate enforces the 150-line entry-point limit
  (ADR-002); check `wc -l` against HEAD when a behavior grows.
- **Source:** `docs/reviews/rca-enlistment-wars-during-service-2026-10-20.md` F1, F2.
### A daily job that must win against a vanilla daily job runs on the next hourly tick (Oaths of Service, 2026-10-19)
The named-companion home anchor ran on `DailyTickEvent`, and vanilla `CompanionsCampaignBehavior.SwapCompanions`
moves unhired wanderers on the same event. Campaign listeners run newest-first, so vanilla's (added in
`SandBoxManager.Initialize`, before TAOM's behaviors) ran last and could undo the anchor for a day.
- **Why missed:** the rule that listeners run newest-first was already written (`state-lifecycle-save.md`), but
  the plan's research asked only whether vanilla moves wanderers, not which daily listener runs last.
- **Prevent:** when a TAOM daily listener must have the last word over a vanilla daily listener, raise a flag on
  the daily tick and act on the next `HourlyTickEvent`; pin daily-alone, hourly-once and hourly-twice.
- **Source:** [rca-oaths-of-service-2026-10-19.md](../rca-oaths-of-service-2026-10-19.md) finding 3.

### Before designing around a flag, read what its consumer does with it (co-op player attribution, 2026-10-21)
Batch B passed `isPlayerOwned = true` for remote co-op players to "protect" their troops from alignment desertion.
`AlignmentDesertionService.ShouldEvaluate` uses the flag only to choose which MCM toggle applies, and both toggles
default on, so nothing was protected, and with "Apply To AI" off an unreadable registry made every AI clan desert.
- **Why missed:** the flag's name was read as its effect; the service was not opened.
- **Prevent:** before a design sets a flag to get an outcome, quote the consumer lines that turn the flag into that
  outcome, under the default settings and each toggle combination.
- **Source:** [rca-coop-player-attribution-2026-10-21.md](../rca-coop-player-attribution-2026-10-21.md) finding 2.
### Coercion without resistance fires no map event: hook GameMenuOptionSelectedEvent on the option ids, pinned by a binding test (Long Memory, #865, 2026-10-21)
A forced taking whose villagers give way applies vanilla's coercion action straight from the menu consequence
(`force_supplies_no_resist_continue`, `force_volunteers_no_resist_continue`, `VillageHostileActionCampaignBehavior.cs:145,151`)
and raises no `ForceSuppliesCompletedEvent` or `ForceVolunteersCompletedEvent`. Those two events fire only when the villagers
resist and a fight follows, so a listener on them alone misses every forcing where the villagers give way.
- **Why missed:** the completed events carry the names of the acts, and the no-resist route was only found by tracing the menu
  consequences instead of the events.
- **Prevent:** when a rule must cover an act whose route skips the event, trace every consequence that ends in the same engine
  action. `CampaignEvents.GameMenuOptionSelectedEvent` fires after the option's consequence (`GameMenu.cs:288-291`), so it is a
  Harmony-free signal keyed on the option ids. The ids are strings the engine owns, so a `BindingVerification` test reads the
  installed engine's `AddGameMenus` IL and asserts each id sits on its menu (`LongMemoryBindingTests`); a rename then reddens
  the gate instead of silently stopping the rule.
- **Source:** `docs/features/long-memory.md`; the `/deep-review` of #865 (lens 2).

### RaidDamage is hit points removed, not a share of full health (Long Memory, #865, 2026-10-21)
`RaidEventComponent.RaidDamage` adds the same `_nextSettlementDamage` that is taken off `SettlementHitPoints`, capped at 1
(`RaidEventComponent.cs:361-362`), and resets when the map event initializes. It is the hit points this event removed on the
village's 0 to 1 health scale, so it exceeds the health the village had when the raid began by at most about one hit (0.05), and the events of one raid
add up to at most 1 plus about one hit (0.05). The first Long Memory design read it as "a share of full health" and added a floor of a quarter plus a
"looting began" rule, which charged at least a quarter of the amount for an event that did almost nothing.
- **Why missed:** the field name and the 0 to 1 range suggested a share of full health, and nobody read the line that adds it.
- **Prevent:** before scaling a reward or penalty by an engine accumulator, read where it is written (reset, add, cap) and say in
  the doc what one unit means. Scale by the value alone; an event that did nothing pays nothing, with no floor.
- **Source:** `docs/features/long-memory.md`; the `/deep-review` of #865 (lens 6).
### A player's deal with a party acts at once, not at the party's next step (#870, 2026-10-21)
Fire and Tribute's tribute payment only recorded the spared clan, which the host read at its next hourly step. An
engage order on the player's party, or a raid order on a spared village, given earlier in the same hour, kept running
and could land before then. Vanilla's own safe-passage bribe sets `MobilePartyAi.SetDoNotAttackMainParty` at the moment
of payment (`SafePassageBarterable` ~174).
- **Why missed:** the payment was modelled as data for the next tick, not as an event that changes the party now.
- **Prevent:** when the player pays, bribes or bargains with a party, re-issue that party's orders in the same call and
  set the engine's own protection flag if one exists; test an order given in the same hour as the deal.
- **Source:** `docs/reviews/rca-fire-and-tribute-envoy-2026-10-21.md` finding 1.

### An ownership change lists what each owned object keeps only on its owner (co-op mission gating, 2026-10-21)
C7 made elephant and mumakil crew spawn only on the peer that owns the creature, which ended duplicate crews. The
owner seats each crew agent with `seat.OnUse`, a link no other peer has, and co-op's spawn message carries no seat
identity. After a battle-host migration the new host adopts the crew as AI with no seat link.
- **Why missed:** the reviews asked which peer may act on an object, not what the object loses when its owner leaves.
- **Prevent:** for every owner-only action, list the local state it creates (a seat, a cache, a timer) and say how a
  new owner rebuilds it after a handoff, or record the gap as an owed check.
- **Source:** [rca-coop-mission-gating-2026-10-21.md](../rca-coop-mission-gating-2026-10-21.md) finding 10 (Codex F1).

### Before adding a check at a call site, read the gate the site already has (co-op mission gating, 2026-10-21)
C7 added a co-op ownership check to four morale paths, Smart Cavalry and the banner bearers. Each already had a gate
that rejects a co-op puppet: `DreadAgentGate.CanAffect` needs an AI-controlled human with a `CommonAIComponent`, Smart
Cavalry reads only the player team, and banner promotion needs `IsAIControlled`. The five checks were deleted.
- **Why missed:** the plan gated every site in its table one by one.
- **Prevent:** read the site's existing guard first; if it already decides the case, pin the invariant with a test and
  name it in the doc row instead of adding a second check.
- **Source:** [rca-coop-mission-gating-2026-10-21.md](../rca-coop-mission-gating-2026-10-21.md) finding 5.

### Before a design says what vanilla already does, list the callers, the perks and the settlement case

Under the Eaves (#849) was designed on the claim that vanilla forest already hides the player's party, so only seeing
out of the trees was new. `DefaultMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange` has one caller,
`CalculateMobilePartyVisibilityAndInspected`: how far the PLAYER sees other parties. No AI path reads forest for
spotting, and forest costs the player 20 % speed (#614 made the same misreading). Four more facts were each one read
away: the Keen Sight perk's text promises half the sight penalty for traveling in forests and vanilla applies it only
to the target side; 111 settlement gates stand on forest faces (a party in a settlement sits at its gate), so the rule
applied inside Caras Galadhon and Rivendell; the TAOM map paints the Old Forest and the other Eriador woods as plain;
Khand (`battania`) keeps vanilla's forest feat as well as Dunland.
- **Why missed:** the design read the method it planned to sit beside and inferred its neighbours; its lessons table
  cited the right RCAs without listing the callers and data each claim rested on.
- **Prevent:** a design that states what vanilla does names the engine callers of the method it cites (one grep of the
  decompile, one line each); before adding a penalty or bonus, search the perk and feat texts for one that names the
  same effect; a rule keyed on map position states the inside-a-settlement case (`CurrentSettlement`, gate position)
  and checks the painted terrain offline before a doc names regions (TAOM_Map `SceneObj/Main_map/navmesh.bin`: an
  `RNM1` header, an LZ4 block from offset 48 holding `NMG9` geometry, 24-byte edge records, then face records whose third
  trailing u32 is the face group, which matches `TerrainType`; observed on this one file, 2026-10-23).
- **Source:** `docs/reviews/rca-under-the-eaves-2026-10-23.md` findings 1 to 5.

### Check every ChangeKingdomActionDetail a flow can raise before keying on one (Sworn Anew, #869, 2026-10-24)
The planned change vetoed vanilla's "join us" line on lore and alignment only. A mercenary-clan prisoner's join
raises `JoinAsMercenary`, not the detail the commit was armed on, so the change would have reopened that road and the
commit would never have fired. Lens 5 caught it before it was built.
- **Why missed:** the flow was read for the detail it usually raises. The other members of `ChangeKingdomActionDetail`
  that the same dialog can reach were not listed.
- **Prevent:** before keying a gate, latch or commit on one `ChangeKingdomActionDetail`, list every detail the flow can
  raise (read the action's callers in the decompile) and say what each does to the feature.
- **Source:** `docs/reviews/rca-sworn-anew-2026-10-24.md` finding 6.

### A veto that only hides a line locally runs on every co-op peer (Sworn Anew, #869, 2026-10-24)
The feature's co-op suppression switched the whole feature off on peers, including the veto that hides a vanilla line.
The co-op server checks no alignment, so a client with the veto off could recruit across sides.
- **Why missed:** the co-op switch was applied to the feature as one unit, and the three co-op questions (loaded, peer
  authority, dedicated server) were not asked of each part.
- **Prevent:** split a feature's parts by what they do. A step that only hides an option locally has no authority to
  conflict with and runs everywhere; a step that writes campaign state keeps the co-op suppression.
- **Source:** `docs/reviews/rca-sworn-anew-2026-10-24.md` finding 3.
### ItemRoster.TotalFood counts the herd: measure the stores without it (Provender-master, 2026-10-23)
`ItemRoster.OnRosterUpdated` adds `count * MeatCount` to `TotalFood` for every live head, and `GetNumDaysForFoodToLast`
divides that total, so turning a head into meat never moves the engine's days of food. A slaughter order keyed on it fires
every day until each herd is at its floor. Measure the food items plus `RemainingFoodPercentage / 100` against the burn.
- **Why missed:** the plan read the getter, not the roster code that builds the total (a repeat of the Lost Footing lesson
  in adapters-taleworlds-api.md, three days earlier).
- **Prevent:** for every engine figure a rule measures, cite the line that builds it and one vanilla caller in the plan.
- **Source:** `docs/reviews/rca-provender-2026-10-23.md` row 1.

### An automatic spend keeps a reserve the player cannot see it take (Provender-master, 2026-10-23)
An order that buys on its own (food on every market visit) spent down to 0 gold; the next wage tick then pays short and
applies the unpaid-wage morale penalty (`DefaultClanFinanceModel`). Keep N days of `MobileParty.TotalWage` out of any
automatic budget; a purchase the player clicks, with its price shown, needs no reserve.
- **Why missed:** the reference rule ("paid from the leader's gold") was copied without asking what an automatic spend
  must never take.
- **Prevent:** for every automatic spend, name what it must leave untouched before writing the budget.
- **Source:** `docs/reviews/rca-provender-2026-10-23.md` row 3.
### Skip the dedicated server only when the feature's subject is the local player (Bond of Hire, 2026-10-24)

The dedicated-server gate exists for code that credits or charges `Hero.MainHero`, which on a server is nobody's
character. Bond of Hire copied it into a service that writes the saved contract date of AI clans, where the server is
the authority and the one peer that should act. A server-hosted campaign then bound no clan, and its
`BondOfHireMinimumDays` server key was settable and had no effect.
- **Why missed:** the plan's test list named "co-op peer and dedicated server skipped" as one line, copied from
  sibling features that pay the player. The maintainer's later decision (the server binds) reached the plan notes and not
  the builder's brief.
- **Prevent:** for each part of a feature, ask who its subject is. The local player: gate on `IsDedicatedServer`. AI
  clans or world state: gate on `ShouldDeferToHost` only, so the authority acts and clients defer. A server settings key
  for a part that is skipped on the server is a sign of the wrong gate.
- **Source:** `docs/reviews/rca-bond-of-hire-2026-10-24.md` finding 1.
### Moving work to another caller moves it in the engine's frame: check the frame order, not only the rate (#841, 2026-10-24)
The service upkeep had two feeders, the campaign tick and the wait-menu tick. Removing the menu feed while
time ran was judged by cost alone, but it also moved every join the upkeep raised on a running menu from
before the frame's time advance to inside it, where `MapEventManager.Tick` still updates a raid that is the
player's own event (`MapEventManager.cs:59-61`, `Campaign.cs:980-982`).
- **Why missed:** both feeders were treated as interchangeable sources of one budget; the raid window was
  already in the feature doc, but nobody tied it to which feeder runs.
- **Prevent:** when a change moves an action from one engine callback to another (a menu tick, `TickEvent`,
  an hourly or daily event, a mission tick), read the engine's frame order for both and list what runs
  between them; an action that joins, starts or ends a map event belongs where time does not advance first.
- **Source:** [rca-enlistment-trace-and-pump-clock-2026-10-24.md](../rca-enlistment-trace-and-pump-clock-2026-10-24.md) F1.

### A claim about which parties reach a seam reads every gate on the way in, the models included (#912, 2026-10-26)
The Harvest Road hint promised a "bought" notice when a caravan buys food. `BuyFoodInternal`'s caller,
`TryBuyingFood`, also asks `MobilePartyFoodConsumptionModel.DoesPartyConsumeFood`, which is false for every caravan
(`DefaultMobilePartyFoodConsumptionModel.cs:89`), so no caravan ever arrives. A comment in the same feature said the food
seam runs in towns only; the caller also accepts villages.
- **Why missed:** the plan read the caller's settlement test and stopped; the model it consults was one call deeper.
- **Prevent:** before text or a comment says who triggers a patched method, list every condition on each caller's path
  in, including the GameModel methods it calls, and check TAOM's overrides of those models. Write the claim from that
  list, and say which gate drops the rest.
- **Source:** [rca-harvest-road-2026-10-26.md](../rca-harvest-road-2026-10-26.md) findings 1 and 5.
### A TAOM action beside a vanilla one applies the vanilla action's own gate (#910, 2026-10-26)
Make Tools let a player press at stamina 6 to 10, where the vanilla Refine button beside it is greyed with "You must rest":
every smithy action needs stamina above 10 (`CraftingVM.cs:1034-1041`), and the co-op host refuses at 10 or less.
- **Why missed:** the plan derived the rule from the cost model (stamina at least the cost) and never read the screen's
  enable rule.
- **Prevent:** before a new action goes on a vanilla screen, read the enable rule of the vanilla action next to it
  (`RefreshEnableMainAction` here) and apply the same conditions, then add the action's own.
- **Source:** [rca-smiths-measure-2026-10-26.md](../rca-smiths-measure-2026-10-26.md) finding 2.
### A guard justified by engine ordering cites the decompiled lines, and its fixtures come from them (#796, 2026-10-26)
An engine-ordering claim (`MapEventSide.Clear()` nulls the main party's map event before the encounter closes) was copied between comments until it became a rule, R1b, that nobody tested against the engine. In v1.5.5 the main party stays in its map event through the loot and aftermath screens; on that path vanilla removes it inside `PlayerEncounter.Finish` (`MapEvent.cs:1743`, `MapEventSide.cs:434`). Other exits remove it with the encounter still open: a defender's repulsed assault (`SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle`), an army leader leaving the event (`PartyBase.cs:323-326`), and `LeaveBattle`. The rule was redundant where it was meant to act and harmful had it acted (it never ran).
- **Why missed:** every test built the snapshot by hand with the claimed shape, so no test could disagree with the engine.
- **Prevent:** a guard justified by engine ordering cites the decompiled lines, and its test fixtures are built from the shapes those lines produce.
- **Source:** [rca-enlistment-battle-encounter-rule-2026-10-26.md](../rca-enlistment-battle-encounter-rule-2026-10-26.md) F1.
### Read the co-op predicate table before gating a world mutation, not the nearest precedent (career quests, 2026-10-27)
A repeat of "Presence is not authority" above. A sweep that cancels a former player hero's career quest gated on
`ICoopPresenceProvider.IsCoopActive`, so a solo player with the Coop module enabled never got the fix, and a dedicated
server was protected only by the presence probe. The single-player predicate is `CoopSessionPolicy.IsSinglePlayerAuthority`.
- **Why missed:** the gate was copied from `PlayerPossessionService`, whose presence check is a documented heir
  discriminator, not an authority decision; neither this lesson nor the coop-interop "four questions" table was read.
- **Prevent:** before a co-op gate on code that changes the world, read `docs/features/coop-interop.md` "Which predicate
  to use" and pick by what the code decides; a precedent's predicate answers that precedent's question.
- **Source:** [rca-career-quest-owner-change-2026-10-27.md](../rca-career-quest-owner-change-2026-10-27.md) finding 1.
### A join seeded from inside the raided village is interrupted by the engine, so read the trace before opening a fix (#1019, 2026-10-26)
A review worried that a raid reaching the village an enlisted soldier stands in could leave him stranded. The v1.5.5 trace says it cannot: `EncounterManager.StartSettlementEncounter` (:222-243) finishes his encounter and starts the raid, TAOM's battle-start join seeds an encounter, and `StartBattleAction.ApplyInternal` (:34-36) interrupts it unless the event is a sally-out: `InterruptEncounter` (:1671-1679) leaves the battle and opens `encounter_interrupted`. For the raid itself, `StartSettlementEncounter` then, when he shares the village's faction, calls `PlayerEncounter.Start()` (which only creates the encounter) and `Current.Init(...)`, which joins the defender side (:655-661); otherwise it walks him out of the village with `LeaveSettlementAction.ApplyForParty` (:232-242).
- **Why missed:** the review read the seeding call and not the engine call that runs after it.
- **Prevent:** before a fix for an encounter or menu hazard, follow the engine call that runs after TAOM's seam; log the case (here one ungated INFO line in `EnsureEncounterAgainst`) and let a run decide.
- **Source:** [enlistment.md](../../features/enlistment.md), "Inside the raided village (#1019)".
### On a co-op guest every local book is the whole save's book: gate where the book is written, and prove the gate with a mutation (#1086, 2026-11-06)
A guest loads the server's save, so a TAOM book that means "the player's refuges" or "the player's orders" holds every player's entries there. A Harmony menu redirect read that book and opened another player's refuge menu (upgrade gold, dismantle), and a supply method called from another feature's camp tick destroyed the host's caravans.
- **Why missed:** the audit walked campaign behaviors and their handlers; a Harmony prefix and a service method called from a second feature are not handlers. The first gates went into the behaviors, so each caller of the service needed its own gate. The tests were checked only by removing the gate, which a test that reads the flag and ignores it still passes.
- **Prevent:** for each book a feature keeps, list every reader that acts for "the player" (patches and menus included), not just the behavior's handlers. Gate inside the service that writes the world, so every caller is covered. Before calling a gate test done, run it with the gate removed and with the predicate inverted or ignored. Include `Infrastructure` in filtered runs: the entry-point line gate only shrinks.
- **Source:** [rca-coop-audit-1086-2026-11-06.md](../rca-coop-audit-1086-2026-11-06.md) rows 1, 4, 6 and 7.
### Never put a menu over the player's own open battle: only the battle menu closes it (#1099, 2026-11-08)
`MapEventManager.Tick` skips the player's non-raid map event, so `PlayerEncounter.Update`, driven from the `encounter` menu, is all that advances it. `EnlistmentMenuBehavior.OnConversationEnded` opened the service wait menu after a capture conversation with that event still open, time ran, and the battle stayed open until a sally check's `Finish` closed it and opened a meeting with an emptied party: a CTD.
- **Why missed:** the state gate (`EnlistedAttached`) looked like the guard, and the twin rule at `EnlistmentMenuService.cs:123` covered redirects only, not a direct `Activate`.
- **Prevent:** any code that opens a service or wait menu tests `IsInMapEvent` first, whatever the state machine says. A crash stack with no TAOM frame can still have a TAOM cause: read the menu lines before it in `rgl_log.txt`.
- **Source:** [rca-sally-empty-party-2026-11-08.md](../rca-sally-empty-party-2026-11-08.md), row 1.

### A state repaired at load misses the battle that starts after the load check (#1099, 2026-11-08)

`EnlistmentLoadNormalizer` restored `EnlistedBattle` only for a party already in a map event at `OnGameLoaded`. A saved encounter with no battle yet passed that check, a battle started when the menu ran, and `OnMapEventStarted` promoted the state only when the commander was in the event. **Rule:** a state derived from an engine fact needs a writer on the edge that creates the fact, as well as a repair at load.

### A claim that a guard covers a route names the line that sends the route there (#1099, 2026-11-08)

**Why missed:** a `DoMeeting` prefix was described as covering the player's sally button because the button starts a party encounter. `DefaultEncounterGameMenuModel.cs:164-178` sends that encounter to `encounter`, so the prefix never sees it. The same slip produced two more wrong claims in one review, and an earlier finding in the same RCA.

**Prevent:** before a comment or doc says a guard covers a route, follow the route to the line that reaches the guard, and cite it. Encounter routes go through `GetEncounterGameMenu`. A bool prefix that returned false also skips every later prefix on that method.

**Source:** `docs/reviews/rca-sally-empty-party-2026-11-08.md`, findings 14, 18, 19, 20.

### `GameMenu.SwitchToMenu` runs the new menu's init before it returns (#1099, 2026-11-08)

**Why missed:** a reopen of `encounter` used `EnsureMenuOpen`, which treats "the menu is not current afterwards" as a failed open and activates again. `MenuContext.SwitchToMenu` calls `HandleStates`, which runs on_init at once (`MenuContext.cs:56-59,89`). On a decided battle that init can finish the encounter or open another menu.

**Prevent:** when the target menu's init can move on by itself, use `SwitchTo` and accept where it lands. `EnsureMenuOpen` fits menus whose init does nothing.

**Source:** `docs/reviews/rca-sally-empty-party-2026-11-08.md`, finding 15.

### A crash hypothesis names the check that fires first on its route (#1104, 2026-11-09)
The Patch73 defect was ranked first for a crash at supply delivery, because a player might click the caravan as it arrives. Delivery fires at 1.2 map units and runs before the encounter check each frame. Outside an army the meeting radius is `Max(0.5, dt * 10)`, so such a single player with the feature on reaches the defect only on a large frame step.
- **Why missed:** the ranking came from what a player might do, and the review that rated the defect HIGH read only the encounter chain.
- **Prevent:** before ranking a hypothesis, list the checks that can end the route first, compare their numbers and their order in the tick, and write the condition under which the route is reached.
- **Source:** `docs/reviews/rca-supply-caravan-meeting-2026-11-09.md`.

### A guard on "is in a map event" asks who put the party there (#1099, 2026-11-09)
A failed-join guard skipped the army undo for any party in an event. `MobileParty.SetAttachedToInternal` copies the commander's event side onto a merged party, so the join's own merge made every such rollback read as in-event and the undo never ran.
- **Why missed:** the unit tests stubbed the flag, and the merge left no trace in the fake.
- **Prevent:** before gating on an engine state, list every writer of that state on the path, the current method included. Model a writer in the test fake when the method itself can set the state.
- **Source:** `docs/reviews/rca-supply-caravan-meeting-2026-11-09.md`.

### A limit read from an engine formula covers every branch of that method (#1104, 2026-11-09)
`PartyBase.GetEncounterTargetPoint` has an army branch. Two review lenses and the RCA quoted only the branch for a party outside an army, so the docs said a single player reaches Patch73 only on a large frame step. A player who leads five or more attached parties reaches it at normal speed, because the army radius is `0.5 * sqrt(attached parties + 1)`.
- **Why missed:** each reader stopped at the first formula that explained the number in hand. The same formula had already been corrected once in this review, and that correction did not prompt a read of the whole method.
- **Prevent:** before a doc says "only when X" from an engine formula, read the whole method, then cover each branch or scope the claim to the branch you read.
- **Source:** `docs/reviews/rca-supply-caravan-meeting-2026-11-09.md`, Codex pass.

### A guard that waits for a later day names the engine path that ends the wait (#1110, 2026-11-09)
Arachnophobia Mode skipped a spider brood that held prisoners and said it would disband on a later day. Nothing removes regular prisoners from a bandit party under its prisoner limit: `PrisonerReleaseCampaignBehavior.HourlyPartyTick` releases only above the limit, selling needs a friendly fortification, and prisoner recruitment serves lord parties. A brood that won one fight stayed on the map.
- **Why missed:** the guard copied a cautious default, and the plan stated the retry as a fact. Nobody traced what clears the condition.
- **Prevent:** a skip-and-retry guard cites the decompiled line that clears its condition. With no such line, narrow the guard to the state that needs it (here a hero prisoner: an NPC hero has a daily escape chance in `DailyHeroTick`, and the player leaves through player captivity) and let the engine's own destroy handle the rest (`MobileParty.RemoveParty` frees every hero but the player and clears the roster). A chance is not a deadline: write "until the hero leaves", not "on a later day".
- **Source:** [rca-arachnophobia-mode-2026-11-09.md](../rca-arachnophobia-mode-2026-11-09.md), finding 2.

### A state a patch sets and restores is traced to its writers first: the map event was already a defender victory (#1121, 2026-11-12)
Patch146 forced `DefenderVictory` around the private loot steps and restored the old state, because vanilla's retreat handler sets the winner to none on its next line. On the real path the mission end has already written `DefenderVictory` (`MissionResult.CreateSuccessful`, `CampaignMissionComponent.OnMissionResultReady`, `PlayerEncounter.SetPlayerVictorious`), so both writes did nothing; on a stale `EnemyRetreated` result from the reused siege encounter the state is `None`, and the write ran `OnBattleWon` and created rewards for a fight nobody fought.
- **Why missed:** the plan took the state at the handler from the handler's own later line. Nobody listed what writes `BattleState` before the seam. The rule "Writers before gates" names a gate that reads a flag, not a patch that writes one.
- **Prevent:** before a patch writes engine state around a call, list every writer that runs before the seam, with citations. When the state already holds the wanted value, delete the write and gate on the state: that also closes the stale-result path.
- **Source:** [rca-siege-retreat-loot-2026-11-12.md](../rca-siege-retreat-loot-2026-11-12.md), findings 2 and 8.

### A vanilla step copied onto another path keeps its assumption about the loser (#1121, 2026-11-12)
The first Patch146 ran vanilla's three loot steps (casualties, items, prisoners). In a win the loser is destroyed next (`CaptureDefeatedPartyMembers`), so taking its whole cargo and prisoners is free. After an attacker retreat the besiegers survive and keep the siege, so the item step took about half of their baggage (`DefaultBattleRewardModel.cs:313`) on every repulse. Vanilla itself captures no one from a retreating side (`MapEvent.cs:1799`).
- **Why missed:** the three steps were copied as a unit from `CalculateAndCommitMapEventResults`, and the tests faked the adapter, so no test could disagree with the engine.
- **Prevent:** state what each reused step assumes about the loser, and copy only the steps whose assumption holds on the new path. A counted field needs a producer: trace every writer of the roster it counts.
- **Source:** [rca-siege-retreat-loot-2026-11-12.md](../rca-siege-retreat-loot-2026-11-12.md), findings 3 and 5.
### A feature that puts state into a container vanilla never fills inherits every vanilla reader and cleanup of it (LordRetinue, 2026-11-12)
LordRetinue put a clan hero into an AI lord party's roster and gave the hero a party role. Vanilla AI parties carry only their leader, so the disband merge, the commander search, the role effects, the wage and size models all assume nothing else is aboard. The merge copies the whole roster into the garrison and then clears it, which left a hero listed in the garrison with no party. A hero aboard is never a commander candidate, so a clan that gained a party slot found no reserve leader.
- **Why missed:** the handover said vanilla "mostly handles" the release, the plan wrote it down as "left to vanilla", and 12 green unit tests covered only the pure decision service. No step listed the consumers of the new state.
- **Prevent:** before the first test, list every vanilla reader and cleanup of the state the feature creates (a roster entry, a role, a flag) with a quoted line each: who removes it, who counts it, who skips an entity that has it. Release it on every path that ends its host (disband, death, capture, marriage). Test each removal path in game before closing the issue.
- **Source:** [rca-lord-retinue-2026-11-12.md](../rca-lord-retinue-2026-11-12.md), findings 1, 4, 5 and 8 (Engine and Data-flow lenses, independently).

### A slot that falls back to its occupant is already filled: compare the candidate with the occupant, not with zero (LordRetinue, 2026-11-12)
`MobileParty.EffectiveScout`, `EffectiveSurgeon`, `EffectiveEngineer` and `EffectiveQuartermaster` return the leader when no role is set, and vanilla never sets a role on an AI party. The first LordRetinue draft treated "no holder" as an empty slot and gave the role to any hero with a positive skill, so a relative with Steward 30 replaced a leader with Steward 120 and cut the party size limit by about 22 (the Steward effect reads only the role holder).
- **Why missed:** the plan modeled the role as empty. The fallback was known and quoted in the first message of the session, yet every test fixture started from a leader with no skills.
- **Prevent:** a decision that fills a slot with a fallback compares the candidate's value with the fallback's value, and its fixtures give the fallback a higher value than the candidate. The service takes the fallback's values as input.
- **Source:** [rca-lord-retinue-2026-11-12.md](../rca-lord-retinue-2026-11-12.md), finding 2.

### What TAOM's own gate refuses is not what a co-op mod can still write: read the mod's path before stating a co-op world rule (#1139, 2026-11-13)
The #1139 doc said two players of the same side cannot go to war, because TAOM's `DeclareWarAction` prefix refuses that pair on the server. Coop's hostile demand calls `DeclareWarAction`, accepts the refusal, then sets the war stance itself (`VillageHostileFactionStanceHelper.ApplyWarStance`) and starts the battle. The same session's root defect was the older form of this mistake: "the player" read as `Hero.MainHero`, which on the Coop server is the server's own hero (see the 2026-08-03 entry above).
- **Why missed:** the claim came from an audit agent and was corrected by reading TAOM's veto alone. A veto is proof of what TAOM refuses, not of what the world can reach.
- **Prevent:** a sentence about what players can or cannot do in a co-op session cites the co-op mod's code path that would do it, not only TAOM's gate on it. Under Coop, "the player" is `IPlayerControlQuery`, never `Hero.MainHero` (#1141 audits the rest).
- **Source:** [rca-coop-pvp-capture-2026-11-13.md](../rca-coop-pvp-capture-2026-11-13.md), findings F0 and F1.

### Vanilla parity under Coop means vanilla plus Coop's patches: an override that skips `base` hides them (#1141, 2026-11-13)
The #1141 audit marked `TaomPregnancyModel`'s `Hero.MainHero` test as vanilla parity. Coop prefixes the vanilla method and swaps that test for `IsPlayerHero`, but TAOM's override never calls `base`, so the prefix never ran and guests' marriages got the AI rules. The same audit grepped `Hero.MainHero` only and missed the War Chronicle rally's `RulingClan == Clan.PlayerClan` test.
- **Why missed:** parity was checked against the decompiled vanilla body, not against what runs on a Coop server.
- **Prevent:** before calling a TAOM site "vanilla parity" for co-op, grep the Coop source for a patch on that vanilla method. An override that skips `base` must port the patch's rule (#1151 audits the other models). Audit "the player" as `Hero.MainHero`, `Clan.PlayerClan`, `MobileParty.MainParty` and locals copied from them.
- **Source:** [rca-coop-mainhero-audit-2026-11-13.md](../rca-coop-mainhero-audit-2026-11-13.md), findings R1 and R2.

### Before you document a limit, ask whether a repair that already exists closes it (#1163, 2026-11-13)
#1149 let the War of the Ring's master switch lift the peace block. Switched off and on again in Full War, a Hostile pair that had made peace was at war in the host's model only: no declaration, no war start date, a stale `Kingdom.FactionsAtWarWith`, and a co-op client that still read peace. A review lens reported it. The main session asked the maintainer whether the war should resume, wrote the undeclared state into the feature doc as a limit "until the next session launch", and merged. Codex then showed that the repair which runs at session launch, `ReconcileDeclaredWars`, is idempotent and closes the gap when the host's daily tick runs it too.
- **Why missed:** the finding was read as a product question, so the work went into wording the limit. A test from #772 pinned "the daily tick does not run the repair" and was taken as a decision; its RCA records no reason, and until #819 nothing could undeclare a Hostile pair inside a session.
- **Prevent:** before a "Limits" sentence is written for state that a toggle, a load or a tick can leave behind, list the repairs that already exist for that state and ask what stops each from running sooner. A pin test proves what the code did when the test was written; read its RCA row before you treat it as a reason. When a daily call replaces a per-launch one, check every warning on its path for a per-day repeat.
- **Source:** [rca-finalizer-rethrow-and-wotr-repair-2026-11-13.md](../rca-finalizer-rethrow-and-wotr-repair-2026-11-13.md) rows 1 and 7; Codex first pass F1.

### A deferred check that also runs on a synchronous event meets every raiser of that event (#1177, 2026-11-13)
Player Possession's join check ran on the hourly tick. #1177 also ran it on `OnPlayerCharacterChangedEvent`, so a Coop joiner gets the package at the switch. TAOM's own Player Switcher raises that event inside character creation, before phase 9 captures the new picks, and the hourly check never ran there: phase 9 follows in the same call. With a co-op module enabled, a second new campaign in one process gave the chosen lord the first character's race, career and resource seed.
- **Why missed:** the draft traced the state at Coop's raiser only. The hourly check's safety came from what always ran between a raiser and the next tick, and the new listener removed that gap.
- **Prevent:** before a check moves from a tick or a later event to a synchronous one, list every raiser of the new event (engine, TAOM, the co-op mods) and read the check's inputs at each one. A value kept across a boundary on purpose needs the event that ends its life.
- **Source:** [rca-possession-switch-2026-11-13.md](../rca-possession-switch-2026-11-13.md), finding 1.
