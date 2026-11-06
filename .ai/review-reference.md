# TAOM Architecture and Review Reference

The detailed TAOM rules and historical exceptions formerly in root AGENTS.md
are maintained here. Paths in command examples are repository-root-relative.
Markdown links are relative to this file.

These review procedures apply when assigned the reviewer role. Builders must
follow the architecture, research, test and data standards below, but are not
made reviewers by reading this reference. Any AI provider can perform either
role. Start with [AGENTS.md](../AGENTS.md) and [shared policy](policy.md).
Historical model names, patch counts and review totals are context, not live facts.

### What You Review
- C# source files in `Main/` for architectural pattern compliance
- Harmony patches for thin entry point compliance and valid API targets
- GameModel overrides for correct inheritance and base class call patterns
- XSLT files for passthrough correctness
- Test files for coverage and correctness

### Severity Ratings
- **CRITICAL**: ADR-007 (sealed type in service; a protected-virtual boundary seam that meets ADR-007 "Exceptions" is not one), ADR-002 (fat entry point), Harmony target method does not exist in the installed engine (the version pinned in `.claude/pinned-game-version.txt`)
- **HIGH**: Missing test coverage for service, incorrect base class for GameModel, XSLT dropping vanilla attributes
- **MEDIUM**: Performance issue in hot path, missing IoC registration, interface not segregated
- **LOW**: Style violation, missing comment explaining non-obvious behavior

### Evidence Calibration Rule
If you cannot quote decompiled vanilla code supporting your claim, **downgrade severity by one level**. "I believe vanilla does X" is not evidence: read the installed source with `pwsh tools/taom-src.ps1 path <Type>`, or the dump at `E:\Decompiled_Bannerlord\` naming its version, and include the relevant code in your finding. Prior reviews produced false positives when vanilla behavior was assumed rather than verified (e.g., `characterObject.IsMounted` was flagged as a bug but matches vanilla `KhuzaitRecruitUpgradeFeat` exactly).

### Output Format

The finding line, grouping, totals and verdict are in [report-format.md](report-format.md).

### Lessons From Prior Reviews, distilled

- **Feeds a new filter or gate the input that should fail it** (2026-09-30, KEYforce art, 2 of 2 LOW
  real): a troop whose spider sat only in its second battle set (the filter kept it), and three XML
  fragments the new siege-scene gate misjudged. For a new filter or gate, ask for that input.
- **What the deserializer assembles, not the XML's shape** (2026-09-30, KEYforce art: a Codex miss that three scoped Claude lenses caught in the fix review). Codex's fix for a first-roster-only mount read named every battle roster and battle `EquipmentSet` references, but not the `<equipment>` written directly under `<Equipments>`, which `MBEquipmentRoster.AddOverriddenEquipments` writes over its slot in every set; 88 troops mount that way. When a finding models what a troop carries, list every path `BasicCharacterObject.Deserialize` takes.

**What Codex does especially well (2026-09-01 memory-diagnostics review: 4/4 HIGH real, 0 false positives).**
- **Deletes and reorders a call the unit tests cannot reach** (2026-10-08, War of the Ring Phase 2 wars
  #772, 3 of 3 findings real): handed a host-only handler that reads `Campaign.Current`, it ran the real
  fixtures in memory, then removed `ReconcileDeclaredWars()` and moved it after the phase check; both
  mutants kept all six behavior tests green. For a handler with a client gate, ask for the deletion and
  reorder mutants of the host call.
- **Mutation-checks a new guard in memory** (2026-10-08, War of the Ring session reset #764, 1 of 1
  MEDIUM real): it rewrote the guarded line into a cached construction as a string and showed every
  predicate of the new source-text pin still held. For a new wiring or source-text test, ask for the
  cheapest regression that keeps it green.
- **Follows a bypassed trigger to what its own firing would have reset** (2026-09-15, Player
  Switcher clan leadership review 113): handed a seam that closed a decision window at once through
  vanilla `ExecuteDone`, it read the popup widget's `OnLateUpdate` and found that the bind had already
  armed the five-second timer, that only the timer's own `ExecuteFinalDone` resets it, that hidden
  widgets still receive late updates (`EventManager.cs:1044-1051`), and that the late `FinalDone`
  would run `ExecuteDone` again; it then opened `GauntletQueryManager.CreateQuery` and
  `InquiryData.HasSameContentWith` to say exactly what the duplicate does. It disputed six of seven
  handed suspects with decompiled lines, including the prompt's own premise that a session-launch
  message has no subscriber (`GauntletChatLogView` is a global layer, `MPChatVM.cs:537-547`). It
  stopped one ordering short: the same timer reaching the NEXT live item, an NRE. When a seam fires
  early what vanilla fires from a timer or an edge, expect Codex to open the timer; write the second
  ordering (a different item bound by then) into the suspect yourself.
- **Opens the class that owns the input, not the one that owns the flag** (2026-09-15, Gondor
  Castar costs review 112): handed a recruit gate that greyed the Done button from a postfix on
  `RecruitmentVM.RefreshPartyProperties`, it read `GauntletMenuRecruitVolunteersView.OnFrameTick`
  and found the Confirm hotkey calling `ExecuteDone` with no look at `IsDoneEnabled`, then
  `OnDone` rechecking gold alone; every `recruit_cost` troop had been recruitable past the gate by
  hotkey since Patch51 shipped. Six Claude agents had read the VM and the patch. When a fix sets a
  UI flag, expect Codex to list every path that commits without reading it; write the gate on the
  commit method first.
- **Refutes the prompt's premises before answering its questions** (2026-09-13, Gondor volunteer
  pools review 110): two of eight handed suspects restated the author's own assumptions (Morlad's
  pool "bowman-only" when the file holds bowman 50 / scout 50; the clan pools "only for unmapped
  settlements" when a converted fief resolves its settlement culture's `CultureMap` entry first,
  `VolunteerRecruitmentService.cs:92`). Codex reopened the JSON and the cascade, corrected both,
  then answered the corrected question with a Markov estimate from the real roots and a per-case
  conversion table; it also re-counted the live map (97, not the doc's 93) and recomputed every
  mirror share by hand. When a suspect names a file, expect Codex to read the file before the
  suspect; write suspects that survive that read, and treat a disputed premise as a finding.
- **Runs the production allocator to print the collision it claims, and opens the caller of every
  override a thread map lists** (2026-09-13, creature handles review 109): handed a slot map whose
  eviction had just been added, it compiled `LayoutPositioner` and its model types into a PowerShell
  `Add-Type` harness, forgot one index the way the new code does, and printed `first ranged=(1, -5);
  first replacement=(1, -5); collision=True`. It then refuted the review's own thread map by opening
  the caller nobody had opened: `Mission.OnAgentPanicked` is a plain managed call from
  `CommonAIComponent.OnTick`, inside the asynchronous agent tick. It disputed five of nine handed
  suspects with decompiled lines, including the objection that had held a registration swap back,
  and reported that its sandbox could not evaluate MSBuild instead of claiming a run. When a fix
  edits one side of an allocator, expect Codex to drive the other; when a thread map lists
  callbacks, expect it to open each one's caller.
- **Reads the widget's update loop past the bound field** (2026-09-13, supply search review 106):
  handed a scroll reset that wrote 0 into a two-way `ScrollbarWidget.ValueFloat`, it read
  `ScrollablePanel.UpdateScrollablePanel` after the value read and found the private wheel momentum
  added on the next line, cleared only when the new content does not overflow, then executed the
  equations to size the drift. When a fix targets the state a binding can see, expect Codex to
  look for the state it cannot.
- **Walks the post-condition of an engine call, not its signature** (2026-09-13, SmartCavalryAI
  v2, #586): three P1s in one pass, all about what a call leaves for the NEXT frame or the NEXT
  call. `Formation.SetMovementOrder` ends by clearing the native target its own `ChargeToTarget`
  had just set (`Formation.cs:714`), so the feature's charge was a free charge at anyone;
  `Formation.Tick` re-applies the retained `FacingOrder` through `SetPositioning` every tick
  (`:2311-2314`), so a line's direction lasted one tick; and the player's targeted charge is a
  plain Charge FOLLOWED by `SetTargetFormation` (`OrderController.cs:812-817`), so a postfix on
  the order alone charged the nearest enemy instead of the chosen one. Five agents and the author
  had verified every signature and none of the three post-conditions. It also compiled and ran a
  Harmony probe to settle a `__state` question, disputed three of seven handed suspects with
  decompiled lines, and reported that its sandbox could not build instead of substituting a stale
  run. When a design says "we issue X", expect Codex to read what the engine does right after X
  returns, and who calls X right before.
- **Reads the concrete close path down to the manager layer, not the layer the code subscribed
  to** (2026-09-12, memory instruments review 101): handed "the engine never collects on a plain
  `PopScreen`", it opened `InventoryManager.CloseInventoryPresentation`,
  `PartyScreenHelper.CloseScreen` and `GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen`,
  followed each to `GameStateManager.PopState`, and showed `OnPopState` ends with
  `Common.MemoryCleanupGC()` (`:306`, `OnPushState` `:278`). A heap-release class that five Claude
  agents had passed was removed on that reading, and the measurement it was built on inverted:
  memory that survives the engine's own collection is rooted, not garbage. When a briefing states
  what the engine "never" does, expect Codex to look for where it does.
- **Proves a coverage claim by running the repo's own scanner on a synthetic body** (2026-09-12,
  Return to Army review 98): handed "the IL drift guard cannot see a branch change", it wrote the
  drifted body, ran `IlCallScanner` over both, and pasted the output, instead of agreeing. It also
  opened the installed co-op mod's patches to test a "peers agree" rationale and found the
  membership handler applies asynchronously. When a rationale names another mod, expect Codex to
  read that mod; write the rationale so it survives that read.
- **Asks what can change while a modal waits** (2026-09-12, camp wait menu review 99): the fix
  exited the menu the picker was opened from; Codex traced the unpaused picker's lifetime, found
  the query layer usable over an incoming enemy's map conversation, and showed the exit would
  destroy the enemy's `encounter_meeting`. It then refuted the in-house Enlistment trace by
  following the persisted menu id past the runtime check into the load order. When a callback
  navigates menus, ask which menu will be current when it fires, not which was current when it
  was armed.
- **Reads the engine at the RAISE site, not the subscribe site.** Found that `ScreenManager` raises
  `OnPushScreen` AFTER `HandleInitialize` and `OnPopScreen` AFTER `HandleFinalize`, so a diagnostic
  could report `+0 MB` for the screen actually responsible. Nine Claude agents missed it.
- **Audits whether a claimed fix was actually applied.** Given an RCA of 19 fixes it found two
  documented-but-not-made. ALWAYS hand Codex the RCA and ask it to check the remediation too.
- **Catches defects introduced BY fixes.** 3 of its 4 HIGHs were in code written during the
  preceding review; fixes get no automatic scrutiny.
- **Refuses to inflate:** disputed 2 of 6 handed suspects, with arithmetic.

Full worked-example catalog — every rolling essay + the complete "bugs Codex misses / false
positives / what Codex does well / run-mode caveats" lists: **[`docs/reviews/codex-track-record.md`](../docs/reviews/codex-track-record.md)**
(older essays: `docs/reviews/agents-md-review-lessons-archive.md`). Read it when a review touches
an area with prior history. That file is frozen: a new false-positive pattern or missed category
gets one line here, and durable patterns go into `docs/reviews/lessons/<category>.md`.

**Look harder here — what a scoped per-feature pass structurally misses (Codex's edge):**
- **Semantic correctness of a SAMPLE from an engine collection.** `GetFirstUnit()` / `[0]` / `.First()` is a sample, not the owner — correct only while the collection is homogeneous. Ask "what if this is MIXED?" (culture / faction / tier).
- **Cross-feature contention for one engine decision.** A blanket `return false` Prefix is a silent monopoly on that decision; every later feature relying on the vanilla path breaks with no error. Grep every TAOM Prefix on the contended engine method.
- **Trace a crash INTO the `base.X()` an override calls** — the base runs vanilla code on the same (possibly degenerate) inputs the override accepts; decompile the base, don't stop at the `Main/` boundary.
- **Whole-file config omissions when a faction / culture / kingdom is added** — ENUMERATE every id-keyed config (`alignment.json`, recruitment pools, cultures/clans/diplomacy) for a row matching the new id; don't only audit data that's present.
- **Clone-leftover DISPLAY text** — a script-cloned faction keeps the SOURCE name in `name=` / `text=` / notable names / `{=key}default` strings (distinguish from intentionally-preserved technical ids).
- **Observation-state-machine clock-source** — verify WHEN the elapsed clock starts relative to when the observed phenomenon begins, not just the count transitions.
- **Numeric enum-cast values** — verify `(SomeEnum)1` against the actual decompiled enum, not the assumed ordering.
- **Code written to FIX a prior review** (2026-09-01, #525). Two of Codex's six findings that day were defects in the fix round produced by an 11-dimension internal review hours earlier: a progression floor that scored armour by one stat per item (a body piece contributes body AND leg armour, so a promotion lost all four hit zones while the score rose), and a repair flag that could no longer restore the rosters it exists for. A fix is verified by re-running the gate, which proves the symptom is gone and nothing about the mechanism. When a changeset contains remediation, review the remediation as new code.
- **A test that derives its own expected set from the artefact under test** (2026-09-01, #525). A coverage test parsed its culture list out of the roster file it was auditing, so deleting a culture's rows removed it from the test's own input and stayed green, as did renaming them to an invalid StringId. Ask of any coverage test: what happens if I DELETE a row?
- **A gate made only of prohibitions.** #525 shipped 15 rosters with no weapon in them past four green gates, because every rule said what a kit must NOT contain and none said what it MUST. When a gate exists for a defect, look for the defect's negation stated positively; if it is absent, the gate cannot fail on the thing it was written for.
- **A ratchet or suppression list with no multiplicity.** Keyed on `(owner, item)` alone, 10 entries were suppressing 13 occurrences, so an already-listed roster gaining a SECOND copy of the same bad item filed as old debt.
- **A load-time engine method called at another time** (2026-11-02, hollow parent repair #1076, a Codex catch). At load a later system puts
  back what the method overwrites; `InitializeHeroCharacterOnAfterLoad` during a birth runs after `RacePersistenceService` has finished, so it
  replaced a restored race. List each field such a call writes, and who else writes that field, and in what order.
- **Every syntax that guards a throwing getter** (2026-10-14, review follow-ups #792: a Codex miss a
  later Claude docs audit caught). Codex showed the `PartyBase.MainParty` scan missed `!= null`, the
  reversed and the `is` forms, but not `?.`; seven `PartyBase.MainParty?.` reads in two adapters
  stayed. When a scan bans a guard on a getter that throws, list `?.` and `??` among the forms.

**False positives to NOT repeat + the Evidence Calibration Rule above** (downgrade a claim you cannot back with quoted decompiled vanilla): full list in the track record. When two agents disagree on a TaleWorlds API, re-run `taom-src` rather than siding with confidence.

### Intentional Patterns (Do NOT flag these)
- `IoC.Resolve<T>()` in Harmony patch classes — approved service locator usage in entry points only
- `IoC.ResolveAll<T>()` for hook dispatch — intentional multi-hook pattern
- `base.Method()` in GameModels accepting sealed params — adapter conversion happens inside the method body before calling the service
- `SubModule.cs` and `IoC.cs` accessing TaleWorlds types directly — these ARE the boundary layer
- GameModel constructors receiving services via `IoC.Resolve<>()` — registration pattern in `SubModule.cs`
- `IoC.Resolve<T>()` in a UIExtenderEx `[ViewModelMixin]` constructor: the library builds the mixin with `Activator.CreateInstance(type, viewModel)` (UIExtenderEx 2.13.2 `ViewModelComponent`), so it cannot take constructor injection. A resolve in the mixin's `OnRefresh`, tick or command handlers is still a violation
- `/investigate` SKILL.md re-declaring `/freeze`'s PreToolUse hook in its own frontmatter — intentional hook reuse so debugging auto-engages scope-lock; copying the inline hook block to other skills must be a deliberate choice, not a casual paste
- `PlayerPossessionService.TryConsumePossession` gating on `ICoopPresenceProvider.IsCoopActive` and then mutating the hero — presence is the wrong predicate for a world-mutating path everywhere else (`ShouldDeferToHost` / `IsAuthority` own that), but here it is the **heir-succession discriminator**: `Hero.MainHero` also changes in solo play when the player continues as an heir, and this gate keeps a solo heir out of the re-grant path. One of three independent guards, any one sufficient — presence, single consumption, and the `SyncData` per-hero marker `_taom_possessionReconciledHeroes`. Named as the sanctioned exception in `docs/features/coop-interop.md` ("One consumer breaks that shape and is not a violation")
- `DedicatedServerProvider` deriving server-ness from `Assembly.GetExecutingAssembly().Location` containing `Win64_Shipping_Server` rather than from co-op role — deliberate. A CLIENT-HOSTED session's host also reports `IsServer` while being a real player who must keep earning, so role cannot answer this question; the binaries folder Bannerlord loaded the module from is a fact about the process and cannot change mid-run. It fails to "not a server" because every gate built on it only ever suppresses behaviour
- Debug builds ship on purpose because crash reports need every frame: no Release finding without a measured player-facing gain (maintainer, 2026-09-30)
- No naval DLC: a naval-only path is out of scope in one line with no follow-up issue, unless vanilla CampaignSystem itself runs that branch (maintainer, 2026-10-20)
- No new `DependedModule` row on a TAOM `SubModule.xml`, and no issue proposing one, because the Kit force-loads each one (maintainer, 2026-09-21); the rows #371 added stay

### When reviewing `.claude/` harness changes (not C# features)
- Check whether claims about Claude Code's load semantics are verified — official docs at https://code.claude.com/docs/en/skills and /docs/en/hooks and /docs/en/memory are authoritative.
- Skill bodies are NOT in the eager startup context; only frontmatter is. An auditor or linter that counts SKILL.md line-count or full-file tokens as startup overhead is wrong.
- Hooks declared in skill frontmatter only fire while that skill is invoked. Writing a hook's state file from a non-hook-bearing context does NOT activate the hook.
- Rules with ANY `paths:` field are conditional. Always-load rules omit `paths:` entirely. `paths: ["**/*"]` is still conditional under the loader.
- `triggers:` is not in the documented Claude Code skill schema — flag any new skill that uses it as a port-from-other-suite drift.

---

## Project Overview

TAOM is a .NET Framework 4.7.2 mod for Bannerlord v1.4.8. It uses Harmony patches, GameModel overrides, and CampaignBehaviors to implement LOTR-themed game mechanics.

**Build and test:** use the [non-deploying verification commands](verification.md).
Build the solution and test in the same configuration with both copy-suppression
properties. **Framework:** MSTest + NSubstitute.

---

## Architecture

```
HarmonyPatch / GameModel / CampaignBehavior   <-- THIN (<150 lines, no logic)
                    | delegates to
              Service (IXxxService)            <-- ALL business logic here
                    | uses
              Adapter (IXxxAdapter)            <-- wraps sealed TaleWorlds types
                    | wraps
         TaleWorlds Engine (Hero, Agent...)    <-- sealed, never cross boundary
```

**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `Service` -> `IAdapter` (sealed types); an `IOnXxx` hook interface sits between patch and service only when the patch needs a narrow seam or a test fake

---

## Critical Rules (NEVER VIOLATE)

| Rule | Details |
|------|---------|
| **TDD Mandatory** | RED -> GREEN -> REFACTOR. Test first, always. |
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `ICareerHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior — decompile first |

---

## Key Paths

| Component | Path |
|-----------|------|
| Mod code | `Main/` (.NET Framework 4.7.2) |
| Mod tests | `TAOM.Tests/` (MSTest + NSubstitute) |
| Features | `Main/Features/` |
| Adapters | `Main/Adapters/` |
| Core | `Main/Core/` |
| XML config | `Main/_Module/ModuleData/` |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| ModuleData XML validator | `python tools/validate_moduledata.py` resolves `Item.`/`NPCCharacter.`/`Culture.`/`PartyTemplate.` refs across every XML under `Main/_Module/ModuleData` (259 today) and `LOTRLOME_Armory`; ref `docs/features/moduledata-validation.md` |
| TaleWorlds DLLs | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client` |
| Doc-graph tool | `tools/graph_query.py` — query/audit the docs link graph (`explain`/`path`/`metrics`); ref `docs/features/doc-graph.md` |

---

## ADR Rules, IoC Lifetimes, Test Coverage and Feature Layout

The non-negotiable ADR rules, IoC lifetimes, ADR-008 coverage table and feature file layout are in
[csharp-architecture.md](../.claude/rules/csharp-architecture.md); the decisions are indexed in
[docs/adrs/README.md](../docs/adrs/README.md).

---

## Adapter Pattern Rules (ADR-007)

The rules are in [adapters.md](../.claude/rules/adapters.md).

---

## Harmony Patch Rules

The rules are in [harmony-patches.md](../.claude/rules/harmony-patches.md).

---

## GameModel Override Rules

The rules are in [gamemodels.md](../.claude/rules/gamemodels.md); the current overrides, with each
model's vanilla base and purpose, are in [gamemodel-registry.md](../docs/reference/gamemodel-registry.md).

---

## C# Design Patterns

The hook, strategy and GameModel patterns are in [csharp-patterns.md](../.claude/rules/csharp-patterns.md).

### Anti-Patterns (Flag these)
- Business logic in Harmony patches (must delegate to services)
- Sealed TaleWorlds types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.`)
- Multiple responsibilities in one service (split it)

---

## XSLT Rules

The rules are in [xslt.md](../.claude/rules/xslt.md): the authoritative source, passthrough and the
identity transform.

---

## Testing Rules (TDD Mandatory)

The rules are in [tests.md](../.claude/rules/tests.md); the AAA structure is in
[testing-guide.md](../docs/ai-includes/testing-guide.md).

---

## Harmony Patch Categories (Known Intentional Patches)

These are all registered, intentional patches — do not flag them as unauthorized modifications. Current-as-of 2026-07-12; per-patch rationale/history/RCAs: `docs/reference/harmony-patch-registry.md` (single maintained source — this table is a routing snapshot).

| Category | Feature | Target | Status |
|----------|---------|--------|--------|
| `Patch0_BattleScenes` | Battle scenes | `Campaign.InitializeScenes` | DISABLED |
| `Patch1_FirstTimeInit` | First-time initialization | Various | active |
| `Patch2_RefreshTableau` | Banner tableau refresh | Various | active |
| `Patch3_SetRace` | Race assignment | Various | active |
| `Patch4_CharacterSpawner` | Character spawning | Various | active |
| `Patch5_FaceGen` | Face generation | Various | active |
| `Patch6_BannerEditor` | Banner editor | Various | active |
| `Patch7_FactionMap` | Faction map | Various | active |
| `Patch8_SiegeCampGuard` | Siege camp guard | Various | active |
| `Patch9_RaceFilter` | Culture-restricted race dropdown on CC | `FaceGenVM.Refresh` | active |
| `Patch10_WeatherBoundsGuard` | Weather bounds clamping | `DefaultMapWeatherModel` | active |
| `Patch11_Diplomacy` | Diplomacy system | Various | active |
| `Patch12_WarOfTheRing` | War of the Ring | Various | active |
| `Patch13_RaceAge` | NOP vanilla's same-race birth assert (mixed-race births are normal in TAOM) | `HeroCreator.DeliverOffSpring` (Transpiler) | active |
| `Patch14_Execution` | Execution system | Various | active |
| `Patch15_BannerLayerLimit` | Banner layer limit | Various | DISABLED (engine-native since v1.4.7) |
| `Patch16_AtmospherePersistence` | Forced-atmosphere scenes | `Mission.Initialize` | active |
| `Patch17_TroopWeight` | TroopWeight shed-on-upgrade (elite tax lives in `TaomPartySizeModel` since 2026-07-11) | `PartyUpgraderCampaignBehavior.UpgradeReadyTroops` (Postfix) | active |
| `Patch18_CulturalFeats` | Custom culture feat registration | `Campaign.InitializeDefaultCampaignObjects` | active |
| `Patch19_CustomBattles` | Custom battle TAOM factions/commanders/troops | `CustomBattleData`, `CustomBattleHelper`, `BannerlordMissions` | active |
| `Patch20_NarrativeHorseGuard` | Suppress CC narrative horse crashes for no-mount cultures | `CharacterCreationCampaignBehavior`, `CharacterCreationNarrativeStageView` | active |
| `Patch21_ShaderPrecompilation` | Loading-screen shader progress text | `LoadingWindowViewModel` | active |
| `Patch22_ArmyTargeting` | Border proximity floor for priority-list targets | `AiMilitaryBehavior` | active |
| `Patch23_BannerColorPersistence` | Player clan colors everywhere (UI + 3D battle + conversation) | 16 targets across `CampaignUIHelper`/`SandBoxUIHelper`/party+inventory VMs/`Mission`/`Banner`/`AgentVisuals.Create`/`MapConversationTableau` — full list in the registry | active |
| `Patch24_BannerDriftGuard` | Block vanilla banner color drift during War of the Ring | `Clan.UpdateBannerColorsAccordingToKingdom`, `Clan.UpdateBannerColor` | active |
| `Patch25_LocalizationOverride` | Let English module_strings overrides of vanilla `{=ID}` tokens apply | `MBTextManager.GetLocalizedText` (Prefix) | active |
| `Patch26_SpecialResources` | Per-kingdom resource gating + transactional spending | `PartyCharacterVM.InitializeUpgrades`, `PartyScreenLogic.UpgradeTroop`, `PartyScreenLogic.AddCommand` | active |
| `Patch27_CareerSystem` | Career screen opening + ability V-key activation | `ViewModel.ExecuteCommand`, `AgentStatCalculateModel.UpdateAgentStats` | active |
| `Patch28_SettlementGuards` | Per-settlement guard injection + per-culture spear mapping | `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (manual), `GuardsCampaignBehavior.GetSuitableSpear` (manual) | active |
| `Patch29_CCBodyProperties` | Per-culture default BodyProperties on CC + body re-apply | `CharacterCreationContent.SetSelectedCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationNarrativeStageView.RefreshAgentVisuals` | active |
| `Patch30_MixedFormations` | Mixed ranged/melee formation layout (hot path, vanilla fall-through) | `Formation.GetOrderPositionOfUnit` (Prefix) | active |
| `Patch31_SmartCavalryAI` | Player-cavalry coordinated line-charge state machine | `Formation.SetMovementOrder` (Postfix, deferred — see `Patch_MissionTime_SetMovementOrder`) | active |
| `Patch33_EquipPresets` | Equipment-preset overlay on the inventory screen | `SPInventoryVM.RefreshValues` (Postfix), `GauntletInventoryScreen.OnInitialize` (Postfix) / `.OnFinalize` (Prefix) | active |
| `Patch34_QuickActions` | Inventory "Sell All" multi-action menu | `SPInventoryVM.ExecuteSellAllItems` (Prefix), `SPInventoryVM` ctor (Postfix), `SPInventoryVM.RefreshCallbacks` (Postfix), `SPInventoryVM.OnFinalize` (Postfix) | active |
| `Patch35_CompanionTactics` | Companion role prefixes (party/OOB) + OOB formation-preset overlay | `PartyCharacterVM.RefreshValues`, `OrderOfBattleHeroItemVM.RefreshValues`, `OrderOfBattleVM` ctor/finalize, OOB UI handler tick/finalize (+ manual tooltip Postfix; movement postfix in the shared deferred category) | active |
| `Patch36_FiefManagement` | F6 fief-management screen (custom GameState) | `MapScreen.OnFrameTick` (Postfix), `GameStateScreenManager.CreateScreen` (Prefix) | active |
| `Patch37_CrashReport` | Crash-capture pipeline (Priority-800 Finalizers -> `CrashReportPatchHelper`) | 9 engine-lifecycle Finalizers (`Managed.ApplicationTick`, `ScreenManager.Tick`, `Mission.Tick`, ...) | active |
| `Patch38_SettlementNameplateFade` | Distance-based settlement nameplate fade (hot path ~3000/s) | `SettlementNameplateWidget.DetermineTargetAlphaValue` (Postfix) | active |
| `Patch39_BanditPartySize` | Scale bandit initial rosters by PlayerProgress (cap = stack MaxValue) | `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` (Postfix) | active |
| `Patch40_HideoutDescription` | Themed LOTR hideout encounter descriptions | `HideoutCampaignBehavior.game_menu_hideout_place_on_init` (private, Postfix) | active |
| `Patch41_McmLayoutFix` | Flip MCM options screen to top-to-bottom layout (#252) | UIExtenderEx `WidgetFactoryManager.CreateAndRegister` (Postfix) | active |
| `Patch42_CastleRecruitment` | Castle troop recruitment, AI half | `AiVisitSettlementBehavior.AiHourlyTick` (Transpiler) | active |
| `Patch43_BattleLoadDiagnostics` | `[BattleLoad]` phase stamps: attack->playable + mission-exit lifecycle + stall watchdog | 11 hooks (`PlayerEncounter.Start`, `MissionState.OpenNew`, `Mission.EndMission`, `MapState.OnTick`, ...) | active |
| `Patch44_CCNameAutofill` | Pre-fill CC Review-stage name field (culture-appropriate) | `CharacterCreationReviewStageVM..ctor` (Postfix) | active |
| `Patch46_TournamentDwarfDismount` | Dwarf tournament dismount (race-keyed) | `TournamentFightMissionController.PrepareForMatch` (Postfix) | active |
| `Patch47_SpiderDeathDismount` | Spider rider-death native-AV guard | `Agent.Die` (Prefix) | active |
| `Patch48_SpiderHitDismountGuard` | Spider surviving-rider dismount-AV guard (Patch47 sibling) | `Agent.HandleBlowAux` (private, Prefix) | active |
| `Patch49_ArmyGatheringNreGuard` | Army-gathering map-tick NRE guard + `[SiegeDiag]` diagnostics | `Army.FindBestGatheringSettlementAndMoveTheLeader` (private, Finalizer) | active |
| `Patch50_DropFlaggedItemGuard` | Warg-on-warg bite NRE guard | `Agent.CheckToDropFlaggedItem` (public, Finalizer) | active |
| `Patch51_RecruitmentResourceGate` | Special-resource affordability gate on the recruit Done button | `RecruitmentVM.RefreshPartyProperties` (Postfix) | active |
| `Patch53_PartyIconScale` | Campaign-map party-icon figure/mount scale (MCM slider) | `MobilePartyVisual.AddCharacterToPartyIcon` (private, Transpiler) | active |
| `Patch54_NavalTravelBoatVisual` | NavalTravel at-sea boat mesh | `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents` (Postfix ×2, SandBox.View) | PARKED 2026-06-26 (#120/#296) |
| `Patch55_BasicTableauRaceGuard` | Render-safe race coercion for Save/Load preview (custom-race native AV, #295) | `BasicCharacterTableau.RefreshCharacterTableau` (private, Prefix) | active |
| `Patch56_SceneNotificationVisualGuard` | Become-king cinematic CTD guard (null AgentVisuals) | `GauntletSceneNotification.OpenScene` (private, Finalizer) + `.OnTick` (Postfix, deferred close) + `PopupSceneSpawnPoint.InitializeWithAgentVisuals` (diagnostic Prefix) | active |
| `Patch57_NavalAtSeaLandRescueGuard` | At-sea land-pathfind native-AV guard | `AIMoveToNearestLandBehavior.AiHourlyTick` (internal, Prefix) | PARKED 2026-06-26 (#120/#296) |
| `Patch58_SkipCampaignIntro` | Skip vanilla campaign intro video on NEW game (always-on) | `SandBoxGameManager.OnLoadFinished` (public override, Prefix) | active |
| `Patch59_CaravanTrade` | Caravan range/war-gate/basket levers | `CaravansCampaignBehavior.CanTradeWith` + `.GetTradeScoreForTown` + `.GetDistanceLimitVeryFarAsDaysForNavigationType` + `.CalculateBudgetFactor` (all private, Postfix ×4) | active |
| `Patch60_TournamentExitMovieRelease` | Tournament-exit movie release (#331 round 1; canary `ReleaseMovie=Nms`) | `MissionGauntletTournamentView.OnMissionScreenFinalize` (public override, SandBox.GauntletUI.dll, Prefix+Postfix) | active |
| `Patch61_SaveLoadDiagnostics` | Always-on `[SaveLoad]` lifecycle logging (15 hooks) | save/load pipeline Finalizers/Postfixes (see the feature doc) | active |
| `Patch61_SaveLoadDiagnostics_ArchiveParse` | Archive-chunk parse-fault stamps (truncation vs corruption) | `ArchiveDeserializer.LoadFrom` (internal, void Finalizer, Priority.First) | active |
| `Patch61_SaveLoadDiagnostics_BehaviorData` | Names WHICH behavior's SyncData failed | `CampaignBehaviorDataStore.LoadBehaviorData`/`.SaveBehaviorData` (internal, void Finalizer) | active |
| `Patch61_SaveLoadDiagnostics_ContainerFill` | Container (dict/list SyncData) load-fault stamps | `ContainerLoadData.InitializeReaders`/`FillCreatedObject`/`Read`/`FillObject` (internal, void Finalizers) | active |
| `Patch_MissionTime_SetMovementOrder` | Shared deferred category — ANY postfix with `MovementOrder` in its signature MUST use it | `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2) | active |
| `Late_ActionSetOverride` | Race-aware action-set name resolution (null monster -> human; vanilla fall-through) | `ActionSetCode.GenerateActionSetNameWithSuffix` (Prefix) | active |
| `Late_Transpiler` | Race-appropriate `_facegen` action set in the face-gen preview | `BodyGeneratorView.RefreshCharacterEntityAux` (Transpiler) | active |

---

## Commit Conventions

Every commit subject carries the module version: `<type>[(scope)]: vX.Y.Z - <description>`, with
`vX.Y.Z` exactly the `<Version>` in `Main/_Module/SubModule.xml` as committed (user rule
2026-09-13; the version moves only in a `/release` commit, whose subject names the new one).
Subject at most 72 characters, body wrapped at 72. No AI attribution. A subject without the label
or with the wrong version is a defect; `.claude/hooks/check-commit-subject-version.sh` refuses it
at commit time in Claude Code.

Example: `feat(recruitment): v2.0.28 - Glanhir recruits the Ringlo Vale line`

**Optional trailers** (each on its own line after blank line):

| Trailer | When to use |
|---------|------------|
| `Constraint:` | TaleWorlds limitation blocked the ideal solution |
| `Rejected:` | Alternative approach considered and dropped |
| `Not-tested:` | Parts that can't be unit tested |
| `Research:` | What was decompiled to inform this change |
| `Save-compat:` | Save file impact |

---

## TaleWorlds Research — Lookup Order

Engine concepts come from the [engine process docs](../docs/reference/engine/); signatures come only from the
installed DLLs (`pwsh tools/taom-src.ps1 path <Type>`, AGENTS.md "Research first"). The decompilation workflow and
the native tools are in [taleworlds-research-guide.md](../docs/ai-includes/taleworlds-research-guide.md).
A native claim with no `python tools/native_decompile.py` output behind it is UNVERIFIED.

> ⚠️ **The decompiled source at `E:\Decompiled_Bannerlord\` is the SHIPPING-CLIENT build — it strips editor-only code.** Editor-only types (`MBEditor`, `AnimalSpawnSettings`, FBX-import / animation authoring) exist ONLY in `Win64_Shipping_wEditor` DLLs. "Absent from the dump" ≠ "doesn't exist." If a class is missing, check the editor build at `E:\Decompiled_Bannerlord\_editor_build\` before concluding it's native. See [bannerlord-engine-and-toolchain.md](../docs/reference/bannerlord-engine-and-toolchain.md).

### Key engine process docs for reviewers

| Reviewing... | Read first |
|---|---|
| Harmony patch (registration, patch kind, deferred apply) | [submodule-lifecycle-and-harmony.md](../docs/reference/engine/submodule-lifecycle-and-harmony.md) — deferred `MovementOrder` gotcha; Prefix/Postfix/Transpiler; `PatchCategory`; managed vs native boundary |
| Mission behavior (lifecycle, behavior type, `MissionLogics` NRE) | [mission-and-missionbehavior-lifecycle.md](../docs/reference/engine/mission-and-missionbehavior-lifecycle.md) — `MissionLogic` vs `MissionBehavior` distinction; tick ordering |
| Creature / non-humanoid agent spawn crash | [agent-spawn-and-render-pipeline.md](../docs/reference/engine/agent-spawn-and-render-pipeline.md) — `FromCharacterObj` vs `FromHorseObj` (skips `AddSkinMeshes`); `AgentVisuals` native boundary |
| Mount / rider / howdah seating | [mount-and-rider-runtime.md](../docs/reference/engine/mount-and-rider-runtime.md) — two-phase `EventControlFlag` mount; `RiderSitBone`; three TAOM seating modes |
| Formation / team AI / `AutoGenerated.dll` DivideByZero | [formations-and-team-ai.md](../docs/reference/engine/formations-and-team-ai.md) — count-division sites; `_MT` threading; spider DivideByZero lead |
| Campaign behavior / DailyTick / party AI | [campaignevents-and-campaignbehavior.md](../docs/reference/engine/campaignevents-and-campaignbehavior.md) + [campaign-tick-time-and-party-ai.md](../docs/reference/engine/campaign-tick-time-and-party-ai.md) — event fan-out; staggered `TickPartialHourlyAi` |
| Campaign objects (Hero/Clan/Kingdom/Settlement) | [campaign-object-graph.md](../docs/reference/engine/campaign-object-graph.md) — `Settlement.Culture` not engine-saved; castle `.Village==null` NRE |
| Campaign→mission seam / encounter / `MissionState.OpenNew` | [campaign-to-mission-bridge.md](../docs/reference/engine/campaign-to-mission-bridge.md) — the single managed↔native handoff; AI auto-resolve without a Mission |
| GauntletUI / ViewModel / screen push | [gauntletui-viewmodel-screen.md](../docs/reference/engine/gauntletui-viewmodel-screen.md) — `CreateState<T>()` + `PushState` mandatory; `IGameStateListener`; layer input wiring |

If a type is in neither the installed DLLs nor the dump, mark the API usage as `UNVERIFIED` rather than guessing.
