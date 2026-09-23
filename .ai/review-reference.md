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
- **CRITICAL**: ADR-007 (sealed type in service), ADR-002 (fat entry point), Harmony target method does not exist in the installed engine (v1.5.2)
- **HIGH**: Missing test coverage for service, incorrect base class for GameModel, XSLT dropping vanilla attributes
- **MEDIUM**: Performance issue in hot path, missing IoC registration, interface not segregated
- **LOW**: Style violation, missing comment explaining non-obvious behavior

### Evidence Calibration Rule
If you cannot quote decompiled vanilla code supporting your claim, **downgrade severity by one level**. "I believe vanilla does X" is not evidence — read the decompiled source at `E:\Decompiled_Bannerlord\` and include the relevant code in your finding. Prior reviews produced false positives when vanilla behavior was assumed rather than verified (e.g., `characterObject.IsMounted` was flagged as a bug but matches vanilla `KhuzaitRecruitUpgradeFeat` exactly).

### Output Format

```
[SEVERITY] path/to/file.cs:line — Rule — Issue — Fix
```

Group findings by severity. End with a summary:

```
CRITICAL: N | HIGH: N | MEDIUM: N | LOW: N
VERDICT: CLEAN / ISSUES FOUND
```

### Lessons From Prior Reviews (85 reviews, 188+ bugs found), distilled

**What Codex does especially well (2026-09-01 memory-diagnostics review: 4/4 HIGH real, 0 false positives).**
- **Drives a hook the way the harness does, and times it** (2026-09-23, ADR-011 harness review,
  8 of 8 findings real): handed nine rewritten PreToolUse gates, it built fixtures outside the
  repo, sent 54 hostile commands and parsed every output as JSON, and timed the tracked-files gate
  at 6 s against its 5 s registration. It found the commit gate misreading `$'...'`, attached
  `-m"..."` and option-shaped prose, the import scan closing a four-backtick fence early, and a
  malformed rule frontmatter counted as scoped, each with a reproduction and the documentation it
  rests on. It declined to run the one suite that commits to a temporary repo, saying so. For a
  harness or tooling review, name the parser each gate relies on and ask for hostile input to it.
- **Follows a stand-down to every consumer of the state it leaves behind** (2026-09-16,
  SignatureStrikes review 114): handed a `MissionLogic` that disables itself after a caught
  exception, it asked what ELSE reads the roster that logic stopped stamping, found the GameModel
  probing the same singleton from inside `CreateMeleeBlow`, and wrote the sequence in mission
  seconds: old stamp at 10, stand-down, overhead at 30 granted, overhead at 31 granted, every
  overhead granted thereafter with no ring. It disputed six of ten handed suspects with decompiled
  lines (the collision damage is zeroed before the model sees a blocked hit; a crafted Mace is
  `OneHandedWeapon` via `Crafting.cs:852`; mission-end teardown never dispatches `OnAgentDeleted`)
  and kept three engine claims UNVERIFIED rather than voting. When a feature has a self-disable
  path, write the suspect as "what shares state with the thing that stopped".
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
an area with prior history. Convention: add each new essay to the top of the track-record file,
archive the 6th-oldest, harvest durable patterns into `docs/reviews/lessons/<category>.md`.

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

**False positives to NOT repeat + the Evidence Calibration Rule above** (downgrade a claim you cannot back with quoted decompiled vanilla): full list in the track record. When two agents disagree on a TaleWorlds API, re-run `ilspycmd` rather than siding with confidence.

### Intentional Patterns (Do NOT flag these)
- `IoC.Resolve<T>()` in Harmony patch classes — approved service locator usage in entry points only
- `IoC.ResolveAll<T>()` for hook dispatch — intentional multi-hook pattern
- `base.Method()` in GameModels accepting sealed params — adapter conversion happens inside the method body before calling the service
- `SubModule.cs` and `IoC.cs` accessing TaleWorlds types directly — these ARE the boundary layer
- GameModel constructors receiving services via `IoC.Resolve<>()` — registration pattern in `SubModule.cs`
- `/investigate` SKILL.md re-declaring `/freeze`'s PreToolUse hook in its own frontmatter — intentional hook reuse so debugging auto-engages scope-lock; copying the inline hook block to other skills must be a deliberate choice, not a casual paste
- `PlayerPossessionService.TryConsumePossession` gating on `ICoopPresenceProvider.IsCoopActive` and then mutating the hero — presence is the wrong predicate for a world-mutating path everywhere else (`ShouldDeferToHost` / `IsAuthority` own that), but here it is the **heir-succession discriminator**: `Hero.MainHero` also changes in solo play when the player continues as an heir, and this gate keeps a solo heir out of the re-grant path. One of three independent guards, any one sufficient — presence, single consumption, and the `SyncData` per-hero marker `_taom_possessionReconciledHeroes`. Named as the sanctioned exception in `docs/features/coop-interop.md` ("One consumer breaks that shape and is not a violation")
- `DedicatedServerProvider` deriving server-ness from `Assembly.GetExecutingAssembly().Location` containing `Win64_Shipping_Server` rather than from co-op role — deliberate. A CLIENT-HOSTED session's host also reports `IsServer` while being a real player who must keep earning, so role cannot answer this question; the binaries folder Bannerlord loaded the module from is a fact about the process and cannot change mid-run. It fails to "not a server" because every gate built on it only ever suppresses behaviour

### When reviewing `.claude/` harness changes (not C# features)
- Check whether claims about Claude Code's load semantics are verified — official docs at https://code.claude.com/docs/en/skills and /docs/en/hooks and /docs/en/memory are authoritative.
- Skill bodies are NOT in the eager startup context; only frontmatter is. An auditor or linter that counts SKILL.md line-count or full-file tokens as startup overhead is wrong.
- Hooks declared in skill frontmatter only fire while that skill is invoked. Writing a hook's state file from a non-hook-bearing context does NOT activate the hook.
- Rules with ANY `paths:` field are conditional. Always-load rules omit `paths:` entirely. `paths: ["**/*"]` is still conditional under the loader.
- `triggers:` is not in the documented Claude Code skill schema — flag any new skill that uses it as a port-from-other-suite drift.

---

## Project Overview

TAOM is a .NET Framework 4.7.2 mod for the Bannerlord version on AGENTS.md's `Target:` line. It uses Harmony patches, GameModel overrides, and CampaignBehaviors to implement LOTR-themed game mechanics.

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

**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)

---

## Critical Rules (NEVER VIOLATE)

The invariants live in one place, [AGENTS.md "Always"](../AGENTS.md): TDD, evidence, the
architecture chain, banned constructs, research first, verify before reference, human prose.
Review against that table; this reference adds reviewer-specific detail below.

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

## Non-Negotiable ADR Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service |
| No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero` |
| Constructor injection only | No service locator in services |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check |

### IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

### Test Coverage Requirements (ADR-008)

| Component | Required | Notes |
|-----------|----------|-------|
| Services | 100% | Must be mockable via constructor injection |
| Engines | 100% | Pure functions — easy to test |
| Hooks | 80%+ | Use `NSubstitute` mocks for adapters |
| Entry Points | Not required | Harmony/GameModel — test via game |

### Feature File Layout

```
Main/Features/MyFeature/
    IMyFeatureService.cs
    MyFeatureService.cs
    MyFeatureIoC.cs          <-- Reuse.Singleton registrations
    Models/
        TaomMyModel.cs       <-- GameModel override (if needed)
    Hooks/
        MyPatch.cs           <-- Harmony patch (if needed)
Main/Adapters/
    IMyTypeAdapter.cs
    MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
    MyFeatureServiceTests.cs
```

---

## Adapter Pattern Rules (ADR-007)

### Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

### Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

### Property Guidelines
- Identify read-only vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

### Testing
- Adapters are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests

---

## Harmony Patch Rules

### Research First (MANDATORY)
ALWAYS decompile the target method before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in Bannerlord v1.5.2

### Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.

### Architecture Requirements
- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface`
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- Use thread-local state pattern for multi-patch coordination

### Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/` directory
- Name: `{TargetClass}{TargetMethod}Patch.cs`

### Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern

---

## GameModel Override Rules

TAOM has 31+ GameModel overrides. All follow the same pattern.

### Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;

    public TaomFooModel(IFooService service)
    {
        _service = service;
    }

    public override float SomeCalculation(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        var taomResult = _service.Calculate(adapter);
        return taomResult ?? base.SomeCalculation(param);
    }
}
```

### Rules
1. **Research first** — Always decompile `DefaultXxxModel` before overriding
2. **Inherit from `Default*`** — Never override `GameModel` directly
3. **Call `base.Method()`** — Unless deliberately replacing behavior, fall through for unhandled cases
4. **Thin model class** — Entry point (<150 lines). All logic in Service
5. **Adapter boundary** — Convert sealed params to adapters immediately
6. **JSON/XML config** — Configurable values in `Main/_Module/ModuleData/configs/`, not hardcoded
7. **Register in SubModule.cs** — via `CreateGameModels()` / `OnGameStart()`
8. **Tests** — Service logic fully unit-tested. Model class itself is thin enough to skip

### Existing Overrides

The current list, with each model's vanilla base and purpose, is
[gamemodel-registry.md](../docs/reference/gamemodel-registry.md).

---

## C# Design Patterns

### 1. Hook Pattern (Harmony -> Hook Interface -> Service)

```
HarmonyPatch (thin)
    -> IOnXxx hook interface
        -> XxxHook implementation
            -> IXxxService (business logic)
```

- Harmony patch resolves `IOnXxx` hooks via `IoC.ResolveAll<IOnXxx>()`, iterates, delegates
- Hook implementation builds context, calls service
- Service contains all logic — uses adapters, fully testable

### 2. Strategy Pattern

For per-culture or per-faction variants:

```csharp
public interface ICultureStrategy
{
    string CultureId { get; }
    float Calculate(IContextAdapter context);
}
// One class per culture, registered as a collection
// Service resolves all and dispatches by CultureId
```

### 3. GameModel Override Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;
    public TaomFooModel(IFooService service) => _service = service;

    public override float Calculate(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        return _service.Calculate(adapter) ?? base.Calculate(param);
    }
}
```

### Anti-Patterns (Flag these)
- Business logic in Harmony patches (must delegate to services)
- Sealed TaleWorlds types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.`)
- Multiple responsibilities in one service (split it)

---

## XSLT Rules

### Authoritative Source
- **SandBoxCore/ModuleData/** is the authoritative reference for vanilla XML structure
- NEVER use SandBox/ModuleData/ — it has different element names the engine ignores
- Example: SandBoxCore uses `<notable_templates>` (engine reads), SandBox uses `<notable_and_wanderer_templates>` (engine ignores)
- TAOM's other 8 XSLT are LIVE under `<game>/Modules/{TAOM_Map,LOTRLOME_Armory}/ModuleData`, in no checkout and in no CI job

### Passthrough Requirements (CRITICAL)
- Always pass through ALL vanilla attributes: `<xsl:apply-templates select="@*"/>`
- Always pass through unmodified child elements: `<xsl:apply-templates select="*[not(...)]"/>`
- Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped
- Only override the specific attributes/elements you intend to change

### Identity Transform
Every XSLT file must include:
```xml
<xsl:template match="@*|node()">
  <xsl:copy>
    <xsl:apply-templates select="@*|node()"/>
  </xsl:copy>
</xsl:template>
```

### Common Mistakes
- Overly broad `xsl:template match` catching unintended elements
- Hardcoding attribute values that should be passed through from vanilla
- Missing `xsl:output` declaration
- Forgetting to handle child elements when overriding a parent

---

## Testing Rules (TDD Mandatory)

### Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

### Naming Convention
`MethodName_StateUnderTest_ExpectedBehavior`

### Structure: AAA Pattern
```csharp
[TestMethod]
public void MethodName_State_Expected()
{
    // Arrange
    var mock = Substitute.For<IMyAdapter>();

    // Act
    var result = _sut.DoSomething();

    // Assert
    Assert.AreEqual(expected, result);
}
```

### Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

### Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`

---

## Harmony Patch Categories (Known Intentional Patches)

Every category registered in `Main/SubModule.cs` is an intentional patch: do not flag one as an
unauthorized modification. The list, with feature, target, status and history, is
[harmony-patch-registry.md](../docs/reference/harmony-patch-registry.md); grep the category there.
Any postfix with `MovementOrder` in its signature must join the shared deferred category
`Patch_MissionTime_SetMovementOrder`.

---

## Commit Conventions

Subject `<type>[(scope)]: vX.Y.Z - <description>` with the version from
`Main/_Module/SubModule.xml`; the full conventions and optional trailers are in
[git-and-commits.md](../docs/ai-includes/git-and-commits.md).

---

## TaleWorlds Research — Lookup Order

Engine concepts come from the process docs; signatures come only from the installed DLLs
(AGENTS.md "Research first").

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](../docs/reference/engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does this process work" questions |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | Decompiles the installed DLLs (version auto-detected) | Authoritative signatures; the dump can lag an engine bump |
| 2. **Read decompiled source** | Read or search `E:\Decompiled_Bannerlord\` | Browsing namespaces and patterns |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_assembly` / `mcp__ilspy__list_types` | Only if the type is in neither |

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

### Pre-Decompiled Source (`E:\Decompiled_Bannerlord\`)

The entire Bannerlord v1.5.2 codebase is pre-decompiled and organized by category (`E:\Decompiled_Bannerlord\_categories_v1.5.2`; older trees beside it):

| Folder | Contents |
|--------|----------|
| `Campaign/` | `TaleWorlds.CampaignSystem` — GameModels, behaviors, actions (1,556 files) |
| `MountAndBlade/` | `TaleWorlds.MountAndBlade` — missions, agents, game logic (1,977 files) |
| `Modules/` | `SandBox`, `StoryMode` — module behaviors, views, all `Default*Model` classes (1,362 files) |
| `Core/` | `TaleWorlds.Core`, Library, SaveSystem, Localization (666 files) |
| `Engine/` | Engine, InputSystem, ScreenSystem, Navigation (386 files) |
| `UI/` | GauntletUI, PrefabSystem, PSAI (285 files) |
| `Network/` | Diamond, Network, PlayerServices (147 files) |
| `Platform/` | PlatformService, Achievements, ModuleManager (69 files) |
| `Launcher/` | Launcher.Library, Launcher.Steam (40 files) |
| `ThirdParty/` | Newtonsoft.Json, Steamworks.NET, jose-jwt (1,081 files) |

### Quick Lookup Examples

```bash
# Find a class
find "E:/Decompiled_Bannerlord/" -name "DefaultPartyWageModel.cs"

# Search for a method across all decompiled source
grep -r "GetCharacterWage" "E:/Decompiled_Bannerlord/Campaign/"

# Browse a namespace
ls "E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/"
```

### When to Look Up TaleWorlds Source

1. **Harmony patches** — Verify the target method exists with the exact signature (name, params, return type, access modifier)
2. **GameModel overrides** — Verify the base class method you're overriding exists and has the expected signature
3. **Adapter interfaces** — Verify the TaleWorlds properties/methods being wrapped actually exist
4. **Any API call you're uncertain about** — TaleWorlds renames/removes APIs between engine versions

### ILSpy MCP Fallback

If a type is not in the decompiled source, use the `ilspy` MCP tool:

```
mcp__ilspy__decompile_assembly(
  assembly_path: "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\SandBox.dll",
  type_name: "SandBox.GameComponents.DefaultPartyWageModel"
)
```

**DLL path:** `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

| DLL | Contains |
|-----|----------|
| `TaleWorlds.CampaignSystem.dll` | Campaign, Hero, Clan, Kingdom, Settlement, MobileParty |
| `TaleWorlds.Core.dll` | BasicCharacterObject, ItemObject, Banner, FeatObject, GameModel base classes |
| `TaleWorlds.MountAndBlade.dll` | Agent, Mission, MissionBehavior, FormationClass |
| `SandBox.dll` | All `Default*Model` classes, SandboxAgentApplyDamageModel |
| `SandBox.View.dll` | MobilePartyVisual, MapScreen, view-layer classes |
| `StoryMode.dll` | StoryMode campaign behaviors |

If neither source is available, mark API usages as `UNVERIFIED` rather than guessing.
