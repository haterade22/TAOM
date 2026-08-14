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

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
