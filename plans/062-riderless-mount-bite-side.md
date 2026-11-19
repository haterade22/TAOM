# Plan 062: Spider and warg bites resolve a riderless mount's side before the friendly check

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat db5680c8a..HEAD -- Main/Adapters/AgentAdapter.cs Main/Adapters/IAgentAdapter.cs Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs Main/Features/AdvancedCombat/MountRiderSides.cs Main/Features/AdvancedCombat/CreatureBiteSides.cs Main/Features/Spider/SpiderAttackService.cs Main/Features/Warg/WargAttackService.cs TAOM.Tests/Adapters/AgentAdapterSideTests.cs TAOM.Tests/Features/AdvancedCombat/MountRiderSidesTests.cs TAOM.Tests/Features/AdvancedCombat/CreatureBiteSidesTests.cs TAOM.Tests/Features/AdvancedCombat/AdvancedCombatBehaviorMountSideWiringTests.cs TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs TAOM.Tests/Features/Spider/SpiderStrikeCapTests.cs TAOM.Tests/Features/Warg/WargAttackServiceTests.cs docs/features/spider.md docs/features/warg-combat.md`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (mission combat: which agents a spider or warg bite damages)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `db5680c8a`, 2026-11-19
- **Baseline at that commit**: build exit 0; dotnet 43028 passed, 3 failed, 9 skipped. The three
  failures are language-file tests that fail on the base for reasons unrelated to this plan:
  `EveryKey_IsSeededInAllTwelveLanguageFiles`, `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`. Python suite: 10549 ran, 0 failed, 13 skipped (this plan
  touches no Python).
- **Issue**: #594 (open, label `bug`)

## Why this matters

A spider or warg bite decides "friend or foe" by comparing `Agent.Team`, and the engine never gives a
mount a team. So a riderless mount (its rider killed or unhorsed) counts as an enemy of everyone: in the
player session of 2026-09-13 a Dol Guldur Giant Spider bit a Fell Warg on its own side for 73 damage
(issue #594), and a warg's charge bites a friendly loose horse the same way. The maintainer approved this
rule on 2026-11-19 (option A): a riderless mount takes its side from its last rider, recorded when that
rider spawned on it or mounted it, and a riderless mount whose side cannot be resolved counts as friendly
(no bite). After this plan both creatures spare a friendly loose mount, still bite an enemy one, and
never bite a mount of unknown side; one shared rule decides it for both.

## Current state

### Files and their roles

- `Main/Adapters/AgentAdapter.cs`: the adapter over the sealed `Agent`; `IsSameTeam` at lines 72 to 78
  is the only team comparison the bites use.
- `Main/Adapters/IAgentAdapter.cs`: the adapter interface (line 33 `bool IsSameTeam(IAgentAdapter other);`).
  `AgentAdapter` is its only implementation (`git grep -n ": IAgentAdapter"` finds only
  `Main/Adapters/AgentAdapter.cs:13`); tests fake it with NSubstitute.
- `Main/Features/Spider/SpiderAttackService.cs`: the spider bite. `IsSameSide` (lines 195 to 202) is the
  team rule; `IsStrikeable` (178 to 191) calls it for both strike paths; `StrikeArc` (123 to 145) also calls
  it to count allies for the creature-bandit diagnostics.
- `Main/Features/Warg/WargAttackService.cs`: the warg bite. Its team rule is inline at lines 42 to 45.
- `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` (90 lines): the `MissionLogic` every creature
  feature shares, registered unconditionally (`Main/SubModule.cs:2318`
  `AddTaomBehavior(() => new AdvancedCombatBehavior());`). It already owns per-agent lifecycle
  (`OnAgentBuild`, `OnAgentDeleted`). It has no `OnAgentMount` override today.
- `Main/Features/AdvancedCombat/AgentSlotIdentity.cs`: the exemplar for a small `internal static` boundary
  class keyed on the `Agent` object (doc comment ends "Boundary code (raw Agent, Mission.Current);
  game-tested per ADR-008."). Match its shape for the new `MountRiderSides`.

### The code as it is (db5680c8a)

`Main/Adapters/AgentAdapter.cs:72-78`:

```csharp
    public bool IsSameTeam(IAgentAdapter other)
    {
        if (other is not AgentAdapter otherImpl) return false;
        var otherAgent = otherImpl.GetUnderlyingAgent();
        if (_agent == null || otherAgent == null) return false;
        return _agent.Team != null && otherAgent.Team != null && _agent.Team == otherAgent.Team;
    }
```

`Main/Features/Spider/SpiderAttackService.cs:133-140` (inside `StrikeArc`):

```csharp
            if (held == null)
            {
                if (HandleSpiderTargetHit(attackerAdapter, targetAdapter, boneId, strike)) struck++;
            }
            // A capped strike (a creature bandit's) holds soldiers only: a riderless mount in the arc is on nobody's side and
            // would take the slot of the soldier who opened the engage gate, which skips mounts (Codex 2026-09-28 F2).
            else if (!IsLooseMount(targetAdapter) && IsStrikeable(attackerAdapter, targetAdapter))
                held.Add((attackerAdapter, targetAdapter, boneId));
```

`Main/Features/Spider/SpiderAttackService.cs:193-202`:

```csharp
    private static bool IsLooseMount(IAgentAdapter target) => target != null && target.IsMount && target.RiderAgent == null;

    // RIDDEN-mount team rule (warg pattern): if the victim is a mount, attribute its team to its rider; the
    // spider's own side is decided by ITS rider when present (the spider is the mount).
    private static bool IsSameSide(IAgentAdapter attacker, IAgentAdapter target)
    {
        var victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target;
        var attackerTeamSource = attacker.RiderAgent ?? attacker;
        return attackerTeamSource.IsSameTeam(victimTeamSource);
    }
```

So the capped path (a creature bandit's strike) already skips every loose mount through `IsLooseMount`,
but the uncapped path (the ridden spider, `held == null`) sends a loose mount to `HandleSpiderTargetHit`,
whose `IsStrikeable` compares `IsSameTeam(mount)`: false for a teamless mount, so it is bitten.

`Main/Features/Warg/WargAttackService.cs:35-45`:

```csharp
    public void HandleWargTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId)
    {
        if (target == null || !target.IsActive() || target.IsFadingOut())
            return;
        if (attacker == null)
            return;

        // Warg-specific victim-team rule: if the victim is a mount, attribute the team to its rider.
        var victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target;
        if (attacker.RiderAgent != null && attacker.RiderAgent.IsSameTeam(victimTeamSource))
            return;
```

A riderless warg compares no team at all (the `attacker.RiderAgent != null` guard). Keep that: this plan
does not change the riderless warg's rule for soldiers; it adds only the unknown-side mount rule.

`Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:54-77`:

```csharp
    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Inside Mission.SpawnAgent's unguarded loop over behaviors: never let an exception out (#595).
        try
        {
            _adapterFactory.OnAgentBuilt(agent);
        }
        catch (Exception ex)
        {
            if (!_buildFailureLogged)
            {
                _buildFailureLogged = true;
                _logger.LogError($"[AdvancedCombat] OnAgentBuild threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        base.OnAgentDeleted(affectedAgent);
        _adapterFactory.Evict(affectedAgent);
        SpatialGrid.Instance?.Remove(affectedAgent);
    }
```

### Engine facts (installed v1.5.7, decompiled with `pwsh tools/taom-src.ps1 path Mission` and `... path Agent`)

- `Mission.SpawnAgent` gives the RIDER his team and origin and gives the mount neither:
  - `TaleWorlds.MountAndBlade.Mission.cs:4209` `agent.SetTeam(agentBuildData.AgentTeam, sync: false);`
  - `:4213` `agent.Origin = agentBuildData.AgentOrigin;`
  - `:4318` `agent2 = CreateHorseAgentFromRosterElements(agentSpawnEquipment[EquipmentIndex.ArmorItemEndSlot], ...);`
  - `:4325` `agent.SetMountAgentBeforeBuild(agent2);`
  - `:4602-4611` `private Agent CreateHorseAgentFromRosterElements(...)`: `CreateAgent(horseComponent.Monster, isFemale: false, 0, Agent.CreationType.FromHorseObj, 1f, forcedAgentMountIndex, (int)mount.Weight, null)`,
    then `SetInitialFrame`, hit points and `SetMountInitialValues`; no `SetTeam`, no `Origin`.
  - A search of every decompiled file in the v1.5.7 cache (all 16 module folders) for a
    `mount...SetTeam(` or `horse...SetTeam(` call found none. So the issue's alternative, "derive the side
    from `Agent.Origin`", is not available: a mount's `Origin` is null (also recorded in
    `docs/reviews/lessons/adapters-taleworlds-api.md`, "A battle mount has no Origin and no Character").
- The rider's mount is readable when behaviors see his build: `:4359`
  `bool flag2 = agent.MountAgent != null && agent.MountAgent.RiderAgent == agent;` runs before
  `:4380` `BuildAgent(agent, agentBuildData);` and `:4391`
  `missionBehavior3.OnAgentBuild(agent, agentBuildData.AgentBanner ?? agentBuildData.AgentTeam?.Banner);`.
  The mount's own build comes first (`:4374` `BuildAgent(agent2, agentBuildData);`, `:4377`
  `missionBehavior2.OnAgentBuild(agent2, null);`), when it has no team either. TAOM's shipped
  `ElephantMissionBehavior.TryInstantiateHowdah` reads `agent.MountAgent` inside `OnAgentBuild` and works
  in game.
- `TaleWorlds.MountAndBlade.Agent.cs:905` `public Team Team { get; private set; }` (a plain auto-property);
  `:644` `public bool IsMount => (GetAgentFlags() & AgentFlag.Mountable) != 0;` (reads native flags, so
  never call it on a bare test agent); `:1031-1036` `public Agent MountAgent { get { return GetMountAgentAux(); } ... }`,
  and `:5324-5326` `GetMountAgentAux()` returns the managed field `_cachedMountAgent` (declared `:518`
  `private Agent _cachedMountAgent;`).
- Mounting: `Agent.cs:1773` `internal void OnMount(Agent mount)` runs on the RIDER and, only
  `if (HasBeenBuilt)`, calls `Mission.OnAgentMount(this)` (`:1789`), which calls every behavior's
  `OnAgentMount(agent)` with the rider (`Mission.cs:5084-5090`). The behavior signature is
  `TaleWorlds.MountAndBlade.MissionBehavior.cs:154` `public virtual void OnAgentMount(Agent agent)`.
  Whether the rider's `MountAgent` is already set when that callback runs is NOT provable from managed
  code (native fills `_cachedMountAgent` through the `UpdateMountAgentCache` callback, `Agent.cs:1848`).
  TAOM's shipped `CreatureBanditDiagnosticsBehavior.OnAgentMount` relies on it
  (`int serial = CreatureBanditDiag.SerialOf(agent.MountAgent);`). If it is null there, the remounted
  horse keeps its earlier record; see Maintenance notes.
- `Mission.cs:2995` (`OnAgentDeleted`) `affectedAgent.SetTeam(null, sync: false);`: a deleted agent's team
  is null too. The bites already reject inactive targets before the team check.
- Threading: native may raise mount and dismount callbacks off the main thread (a v1.4.8 player log caught
  `OnAgentDismount` off it, #634; `Main/Features/Warg/WargMissionBehavior.cs:66-72` defers for that
  reason, and `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:328` checks `OffMainThread` in its
  `OnAgentMount` too). The new recording reads two managed fields and writes a `ConditionalWeakTable`, which is
  thread-safe, so it needs no deferral.
- `Team` overrides neither `==` nor `Equals` (`TaleWorlds.MountAndBlade.Team.cs`), so team comparison is by
  reference, as today.

### Conventions that bind this change

- ADR-007 (adapters): services never take sealed TaleWorlds types; `SpiderAttackService` and
  `WargAttackService` see only `IAgentAdapter`. The new shared rule (`CreatureBiteSides`) takes only
  `IAgentAdapter`. The ledger that holds `Agent` and `Team` (`MountRiderSides`) is boundary code beside
  `AgentSlotIdentity`, used by the adapter and the mission behavior, never by a service.
- ADR-008 (testability): services are unit-testable with NSubstitute fakes; boundary code is game-tested,
  and its decision is pulled into a type a test can drive (here the ledger, driven with bare agents).
- ADR-002 (thin entry points under 150 lines, gate `EntryPointLineCountGateTests`):
  `AdvancedCombatBehavior` is 90 lines; this plan adds about 20.
- `.claude/rules/adapters.md`, "Agent handles: identity is the object, liveness is the slot": never key an
  agent store by `Agent.Index` (the engine recycles indices, #592). Key by the `Agent` object.
- `.claude/rules/csharp-architecture.md`: constructor injection only in services; "Outcome lines" (an
  empty catch calls `Swallowed` or carries `// swallow-ok: <reason>`); no `#region`, no `[Obsolete]`, no
  `#if` (`BannedConstructsGateTests`).
- `.claude/rules/tests.md`: MSTest plus NSubstitute, names `Method_State_Expected`, the failing test first.
  Tests that build NSubstitute fakes of `IAgentAdapter` or touch `Agent`/`Team` carry
  `[TestCategory("RequiresGame")]`, like `SpiderAttackServiceTests` and `MissionAdapterFactoryTests`.
- NSubstitute auto-substitutes interface-typed properties: an unconfigured `target.RiderAgent` returns a
  non-null fake. Every new test that means "no rider" must say `.RiderAgent.Returns((IAgentAdapter)null)`.
- Every file you edit uses CRLF line endings; keep them (the Edit tool does).

### Blast radius

`python tools/graphify_taom.py status --brief` reported the graph MISSING in the plan writer's worktree,
so callers come from `git grep -n -w "<Type>" -- "*.cs"` at db5680c8a:

- `AgentAdapter`: built in `Main/Adapters/MissionAdapterFactory.cs:24`; type-tested in
  `Main/Features/AdvancedCombat/BoneCheck.cs:155`; cast in `SpiderAttackService.cs:69-70` and
  `WargAttackService.cs:84-85`; text-pinned by `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionSeamPinTests.cs:106`
  (`"is AgentAdapter"` in BoneCheck.cs, untouched here).
- `IAgentAdapter` (adding a member): referenced in 20 files; the only implementation is `AgentAdapter`, and
  every test double is an NSubstitute fake, so a new member breaks no compile.
- `IAgentAdapter.IsSameTeam` (semantics widened to a riderless mount's recorded side): called only in
  `SpiderAttackService.cs:201` and `WargAttackService.cs:44` in `Main`.
- `SpiderAttackService`: registered in `Main/Features/Spider/SpiderIoC.cs:10`; tests
  `SpiderAttackServiceTests.cs`, `SpiderStrikeCapTests.cs`.
- `WargAttackService`: registered in `Main/Features/Warg/WargIoC.cs:9`; tests `WargAttackServiceTests.cs`.
- `AdvancedCombatBehavior`: registered at `Main/SubModule.cs:2318`; pinned by
  `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs:137` (the registration line, unchanged)
  and reflected in `TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs:63` (`OnMissionTick`,
  unchanged); named in `CoopMissionRoles.cs:78`.

## Commands you will need

Prefix every dotnet command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | 43064 passed, 3 failed (the three baseline language-file tests), 9 skipped |
| One test class | the same, plus `--filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you create or modify):
- `Main/Features/AdvancedCombat/MountRiderSides.cs` (new)
- `Main/Features/AdvancedCombat/CreatureBiteSides.cs` (new)
- `Main/Adapters/IAgentAdapter.cs`
- `Main/Adapters/AgentAdapter.cs`
- `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs`
- `Main/Features/Spider/SpiderAttackService.cs`
- `Main/Features/Warg/WargAttackService.cs`
- `TAOM.Tests/Features/AdvancedCombat/MountRiderSidesTests.cs` (new)
- `TAOM.Tests/Adapters/AgentAdapterSideTests.cs` (new)
- `TAOM.Tests/Features/AdvancedCombat/CreatureBiteSidesTests.cs` (new)
- `TAOM.Tests/Features/AdvancedCombat/AdvancedCombatBehaviorMountSideWiringTests.cs` (new)
- `TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs`
- `TAOM.Tests/Features/Spider/SpiderStrikeCapTests.cs`
- `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs`
- `docs/features/spider.md`
- `docs/features/warg-combat.md`

`Main/TAOM.csproj` and `TAOM.Tests/TAOM.Tests.csproj` are SDK-style with no `<Compile Include>` items, so
new `.cs` files need no project edit. `Main/IoC.cs` and `Main/SubModule.cs` need no edit: the recording goes
in `AdvancedCombatBehavior`, which SubModule already registers.

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` (single-owner). If the work seems to need them,
  STOP and report the exact line.
- The engage decorators that read `agent.RiderAgent?.Team ?? agent.Team`
  (`Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs:21`,
  `PeriodicallyCheckIfCanAttackAnyone.cs:32,77`, `Main/Features/Spider/BehaviorTreeElements/SpiderEngageDecorator.cs:59`):
  they choose whom to chase, not whom a bite damages. A follow-up, not this plan.
- The capped strike's `IsLooseMount` skip in `SpiderAttackService.StrikeArc`: it stays (a creature bandit
  never spends a slot on any loose mount, by design, Codex 2026-09-28 F2). Only its comment changes.
- The riderless warg's rule for soldiers (it compares no team today; keep that).
- Elephant, mumak, war ram and other creature attack paths.
- `Mission`, `Agent` or `Team` state: never call `SetTeam` on a mount (it would add the mount to the
  team's agent lists, which vanilla counts and orders).
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. STOP and report instead.
- `CHANGELOG.md` (generated at `/release`), `plans/README.md`.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (it was `v2.0.40` at planning).
  Use: `fix(creatures): <version> - spare a riderless mount on the biter's side`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. A draft (re-check every claim against your diff before you use it):

  ```
  A spider or warg bite treated every riderless mount as an enemy,
  because the engine never gives a mount a team: a Giant Spider bit a
  Fell Warg on its own side (#594). A mount now carries the side of its
  last rider, recorded when he spawns on it or mounts it, and both bites
  share one rule: a riderless mount on the biter's side is spared, an
  enemy one is still bitten, and one whose side is unknown (never
  ridden) is spared.

  Not-tested: in game, the side recorded at spawn and on a remount
  (AdvancedCombatBehavior.OnAgentBuild and OnAgentMount read the
  rider's MountAgent); a Custom Battle check is owed on #594.
  ```

- Write the message to a file and run `git commit -F "<file>"`. Stage explicit paths only (the 16 in-scope
  paths you changed). No AI attribution trailer. Never `--no-verify`.

## Steps

### Step 1: record the base

Run the build and the full test suite before any edit and write the totals and failing test names into
your report.

**Verify**: build exit 0; the dotnet totals line reads 43028 passed, 3 failed, 9 skipped, and the three
failures are exactly `EveryKey_IsSeededInAllTwelveLanguageFiles`,
`EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems`. Any other
difference: explain it in the report before going on, or STOP if you cannot.

### Step 2: RED, a loose mount of unknown side is bitten today (4 tests)

These four tests compile against the base code: they rely on NSubstitute's default `false` for the
`HasKnownSide` member that Step 4 adds, and they do not name it.

1. In `TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs`, after
   `HandleSpiderTargetHit_VictimIsMountWithRider_UsesVictimRiderForTeamCheck`, add:

   ```csharp
   [TestMethod]
   public void HandleSpiderTargetHit_LooseMountOfUnknownSide_DoesNotDamage()
   {
       // #594: the engine gives a mount no team, so a riderless mount nobody ever rode has no side; it is spared.
       var attacker = Substitute.For<IAgentAdapter>();
       var rider = Substitute.For<IAgentAdapter>();
       var target = EnemyTarget();
       target.IsMount.Returns(true);
       target.RiderAgent.Returns((IAgentAdapter)null);
       attacker.RiderAgent.Returns(rider);
       rider.IsSameTeam(target).Returns(false);   // what AgentAdapter answers for a teamless mount

       _sut.HandleSpiderTargetHit(attacker, target, 0, strike: SpiderStrikeProfile.Ridden);

       target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
       target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
   }
   ```

2. In `TAOM.Tests/Features/Spider/SpiderStrikeCapTests.cs`, after `CappedStrike_ALooseHorseNearer_NeverTakesTheSoldiersSlot`, add:

   ```csharp
   [TestMethod]
   public void RiddenStrike_ALooseMountOfUnknownSide_IsNotStruck()
   {
       // #594: the uncapped strike has no loose-mount skip of its own; the side rule spares a mount of unknown side.
       var spider = Spider();
       var looseHorse = HorseOf(rider: null!, 1.0f);
       var soldier = InArc(spider, 1.4f);

       _sut.StrikeArc(spider, SpiderStrikeProfile.Ridden, countAllies: false, Reports(spider, looseHorse, soldier));

       Assert.IsTrue(Struck(soldier), "the enemy soldier is still struck");
       Assert.IsFalse(Struck(looseHorse), "a riderless mount of unknown side is never bitten");
   }
   ```

3. In `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs`, after
   `HandleWargTargetHit_VictimIsMountWithRiderOnSameTeamAsAttackerRider_DoesNotDamage`, add a helper and
   two tests:

   ```csharp
   private static IAgentAdapter LooseMount()
   {
       var mount = Substitute.For<IAgentAdapter>();
       mount.IsActive().Returns(true);
       mount.IsFadingOut().Returns(false);
       mount.State.Returns(AgentState.Active);
       mount.IsMount.Returns(true);
       mount.HasMount.Returns(false);
       mount.RiderAgent.Returns((IAgentAdapter)null);
       return mount;
   }

   [TestMethod]
   public void HandleWargTargetHit_LooseMountOfUnknownSide_DoesNotDamage()
   {
       // #594: a riderless mount nobody ever rode has no side; the ridden warg spares it.
       var attacker = Substitute.For<IAgentAdapter>();
       var attackerRider = Substitute.For<IAgentAdapter>();
       attacker.RiderAgent.Returns(attackerRider);
       var target = LooseMount();
       attackerRider.IsSameTeam(target).Returns(false);   // what AgentAdapter answers for a teamless mount

       _sut.HandleWargTargetHit(attacker, target, 0);

       target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
   }

   [TestMethod]
   public void HandleWargTargetHit_RiderlessWargAndLooseMountOfUnknownSide_DoesNotDamage()
   {
       // A riderless warg compares no team with soldiers, but a mount of unknown side is still spared (#594).
       var attacker = RiderlessAttacker();
       attacker.Health.Returns(100);
       var target = LooseMount();

       _sut.HandleWargTargetHit(attacker, target, 0);

       target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
   }
   ```

   `RiderlessAttacker()` already exists in that file (it returns a fake whose `RiderAgent` is null).

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "Name~LooseMountOfUnknownSide"`
reports Total 4, Failed 4, Passed 0. The three `HandleSpider...`/`HandleWarg...` failures are NSubstitute
`ReceivedCallsException`s naming `GetBaseArmorEffectivenessForBodyPart`; the `RiddenStrike_...` failure is
the `Assert.IsFalse` message "a riderless mount of unknown side is never bitten". A failure of any other
kind (a compile error, a different assertion) is a STOP.

### Step 3: RED, the ledger and the adapter's resolved side (compile failure)

1. Create `TAOM.Tests/Features/AdvancedCombat/MountRiderSidesTests.cs`:

   ```csharp
   using System.Reflection;
   using System.Runtime.Serialization;
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using TAOM.Features.AdvancedCombat;
   using TaleWorlds.MountAndBlade;

   namespace TAOM.Tests.Features.AdvancedCombat;

   /// <summary>
   /// The side a mount keeps after its rider is gone (#594). Bare uninitialized agents and teams: <c>Agent.Team</c> is
   /// a plain auto-property and <c>Agent.MountAgent</c> reads the managed field <c>_cachedMountAgent</c> (v1.5.7
   /// Agent.cs:518, :5324), so nothing here reaches native code.
   /// </summary>
   [TestClass]
   [TestCategory("RequiresGame")]
   public class MountRiderSidesTests
   {
       private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
       private static Team BareTeam() => (Team)FormatterServices.GetUninitializedObject(typeof(Team));

       private static Agent AgentOn(Team team)
       {
           var agent = BareAgent();
           typeof(Agent).GetProperty(nameof(Agent.Team))!.GetSetMethod(nonPublic: true)!.Invoke(agent, new object[] { team });
           return agent;
       }

       private static void Seat(Agent rider, Agent mount)
       {
           var field = typeof(Agent).GetField("_cachedMountAgent", BindingFlags.Instance | BindingFlags.NonPublic);
           Assert.IsNotNull(field, "Agent._cachedMountAgent is gone (an engine update); Agent.MountAgent read it in v1.5.7.");
           field.SetValue(rider, mount);
       }

       [TestMethod]
       public void SideOf_NullAgent_IsNull() => Assert.IsNull(MountRiderSides.SideOf(null));

       [TestMethod]
       public void SideOf_LooseMountNeverRidden_IsNull() => Assert.IsNull(MountRiderSides.SideOf(BareAgent()));

       [TestMethod]
       public void SideOf_MountWithARecordedRider_IsThatRidersTeam()
       {
           var mount = BareAgent();
           var team = BareTeam();
           MountRiderSides.Record(mount, team);
           Assert.AreSame(team, MountRiderSides.SideOf(mount));
       }

       [TestMethod]
       public void Record_ALaterRider_ReplacesTheEarlierRidersTeam()
       {
           var mount = BareAgent();
           var first = BareTeam();
           var last = BareTeam();
           MountRiderSides.Record(mount, first);
           MountRiderSides.Record(mount, last);
           Assert.AreSame(last, MountRiderSides.SideOf(mount));
       }

       [TestMethod]
       public void Record_NullTeam_KeepsTheEarlierTeam()
       {
           var mount = BareAgent();
           var team = BareTeam();
           MountRiderSides.Record(mount, team);
           MountRiderSides.Record(mount, null);
           Assert.AreSame(team, MountRiderSides.SideOf(mount));
       }

       [TestMethod]
       public void Record_NullMount_DoesNotThrow() => MountRiderSides.Record(null, BareTeam());

       [TestMethod]
       public void SideOf_AgentWithItsOwnTeam_IsItsOwnTeamOverARecord()
       {
           var own = BareTeam();
           var agent = AgentOn(own);
           MountRiderSides.Record(agent, BareTeam());
           Assert.AreSame(own, MountRiderSides.SideOf(agent));
       }

       [TestMethod]
       public void RecordRider_RiderOnAMount_RecordsHisTeamAgainstTheMount()
       {
           var team = BareTeam();
           var rider = AgentOn(team);
           var mount = BareAgent();
           Seat(rider, mount);

           MountRiderSides.RecordRider(rider);

           Assert.AreSame(team, MountRiderSides.SideOf(mount));
       }

       [TestMethod]
       public void RecordRider_NullRider_DoesNotThrow() => MountRiderSides.RecordRider(null);
   }
   ```

2. Create `TAOM.Tests/Adapters/AgentAdapterSideTests.cs`:

   ```csharp
   using System.Runtime.Serialization;
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using NSubstitute;
   using TAOM.Adapters;
   using TAOM.Core.Logging;
   using TAOM.Features.AdvancedCombat;
   using TAOM.Features.AdvancedCombat.Services;
   using TaleWorlds.MountAndBlade;

   namespace TAOM.Tests.Adapters;

   /// <summary>
   /// The side <see cref="AgentAdapter.IsSameTeam"/> compares (#594): an agent's own team, or, for a mount the engine
   /// gave none, its last rider's. Bare agents: the adapter constructor stores the reference and touches nothing, and
   /// IsSameTeam and HasKnownSide read only <c>Agent.Team</c> and the ledger.
   /// </summary>
   [TestClass]
   [TestCategory("RequiresGame")]
   public class AgentAdapterSideTests
   {
       private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
       private static Team BareTeam() => (Team)FormatterServices.GetUninitializedObject(typeof(Team));

       private static Agent AgentOn(Team team)
       {
           var agent = BareAgent();
           typeof(Agent).GetProperty(nameof(Agent.Team))!.GetSetMethod(nonPublic: true)!.Invoke(agent, new object[] { team });
           return agent;
       }

       private static AgentAdapter Adapt(Agent agent) =>
           new AgentAdapter(agent, Substitute.For<IMissionAdapterFactory>(), Substitute.For<IModLogger>(),
               () => Substitute.For<IBoneCollisionService>());

       [TestMethod]
       public void IsSameTeam_LooseMountWhoseLastRiderWasOnTheSoldiersTeam_ReturnsTrue()
       {
           var team = BareTeam();
           var soldier = Adapt(AgentOn(team));
           var mountAgent = BareAgent();
           MountRiderSides.Record(mountAgent, team);
           var mount = Adapt(mountAgent);

           Assert.IsTrue(soldier.IsSameTeam(mount), "the soldier sees the mount on his side");
           Assert.IsTrue(mount.IsSameTeam(soldier), "and the mount sees the soldier on its side");
       }

       [TestMethod]
       public void IsSameTeam_LooseMountWhoseLastRiderWasOnAnotherTeam_ReturnsFalse()
       {
           var soldier = Adapt(AgentOn(BareTeam()));
           var mountAgent = BareAgent();
           MountRiderSides.Record(mountAgent, BareTeam());
           Assert.IsFalse(soldier.IsSameTeam(Adapt(mountAgent)));
       }

       [TestMethod]
       public void IsSameTeam_LooseMountNeverRidden_ReturnsFalse()
           => Assert.IsFalse(Adapt(AgentOn(BareTeam())).IsSameTeam(Adapt(BareAgent())));

       [TestMethod]
       public void IsSameTeam_TwoAgentsOnOneTeam_ReturnsTrue()
       {
           var team = BareTeam();
           Assert.IsTrue(Adapt(AgentOn(team)).IsSameTeam(Adapt(AgentOn(team))));
       }

       [TestMethod]
       public void HasKnownSide_LooseMountNeverRidden_ReturnsFalse()
           => Assert.IsFalse(Adapt(BareAgent()).HasKnownSide);

       [TestMethod]
       public void HasKnownSide_LooseMountWithARecordedRider_ReturnsTrue()
       {
           var mountAgent = BareAgent();
           MountRiderSides.Record(mountAgent, BareTeam());
           Assert.IsTrue(Adapt(mountAgent).HasKnownSide);
       }

       [TestMethod]
       public void HasKnownSide_AgentWithItsOwnTeam_ReturnsTrue()
           => Assert.IsTrue(Adapt(AgentOn(BareTeam())).HasKnownSide);
   }
   ```

**Verify**: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails, and its errors name only
`MountRiderSides` (does not exist) and `HasKnownSide` (no definition on `AgentAdapter`). Any other error is
yours to fix before going on.

### Step 4: GREEN, the ledger and the adapter's resolved side

1. Create `Main/Features/AdvancedCombat/MountRiderSides.cs`:

   ```csharp
   using System.Runtime.CompilerServices;
   using TaleWorlds.MountAndBlade;

   namespace TAOM.Features.AdvancedCombat;

   /// <summary>
   /// The side a mount keeps once its rider is gone (#594). The engine never gives a mount a team:
   /// <c>Mission.SpawnAgent</c> calls <c>SetTeam</c> on the rider only (v1.5.7 Mission.cs:4209) and builds the mount
   /// through <c>CreateHorseAgentFromRosterElements</c> (:4602) with no team and no origin. So the rider's team is
   /// recorded against his mount when he is built on it and each time he mounts one; the last write is the last
   /// rider's. Keyed by the <see cref="Agent"/> object, never its index (the engine recycles indices, #592). A
   /// <see cref="ConditionalWeakTable{TKey,TValue}"/> lets a dead mount's entry go with it and is safe from the
   /// mount callbacks native may raise off the main thread (#634).
   /// Boundary code (raw Agent and Team); the bites read it only through AgentAdapter.
   /// </summary>
   internal static class MountRiderSides
   {
       private sealed class Side
       {
           public volatile Team? Team;
       }

       private static readonly ConditionalWeakTable<Agent, Side> Sides = new();

       /// <summary>Records <paramref name="riderTeam"/> as <paramref name="mount"/>'s side. A null either way changes nothing.</summary>
       public static void Record(Agent? mount, Team? riderTeam)
       {
           if (mount == null || riderTeam == null) return;
           Sides.GetOrCreateValue(mount).Team = riderTeam;
       }

       /// <summary>Records a rider's team against the mount he sits on, if any.</summary>
       public static void RecordRider(Agent? rider)
       {
           if (rider == null) return;
           Record(rider.MountAgent, rider.Team);
       }

       /// <summary>
       /// The team that stands for <paramref name="agent"/>: its own, else the last team recorded for it as a mount,
       /// else null. Only mounts are ever recorded, so a soldier answers with his own team or null.
       /// </summary>
       public static Team? SideOf(Agent? agent)
       {
           if (agent == null) return null;
           if (agent.Team != null) return agent.Team;
           return Sides.TryGetValue(agent, out var side) ? side.Team : null;
       }
   }
   ```

   `SideOf` deliberately does not call `agent.IsMount` (it reads native flags, `Agent.cs:644`): only
   `Record` writes the table, and only mounts are passed to it.

2. In `Main/Adapters/IAgentAdapter.cs`, after line 33 `bool IsSameTeam(IAgentAdapter other);`, add:

   ```csharp
       /// <summary>True when this agent has a side <see cref="IsSameTeam"/> can compare: its own team, or, for a
       /// mount the engine gave none, its last rider's (#594).</summary>
       bool HasKnownSide { get; }
   ```

3. In `Main/Adapters/AgentAdapter.cs`, replace `IsSameTeam` (lines 72 to 78) with:

   ```csharp
       // Each agent's side is its own team or, for a mount, which the engine never gives a team, its last
       // rider's (MountRiderSides, #594). A side either agent lacks is never "the same".
       public bool IsSameTeam(IAgentAdapter other)
       {
           if (other is not AgentAdapter otherImpl) return false;
           var otherAgent = otherImpl.GetUnderlyingAgent();
           if (_agent == null || otherAgent == null) return false;
           Team? mine = MountRiderSides.SideOf(_agent);
           Team? theirs = MountRiderSides.SideOf(otherAgent);
           return mine != null && theirs != null && mine == theirs;
       }

       public bool HasKnownSide => MountRiderSides.SideOf(_agent) != null;
   ```

   `AgentAdapter.cs` already has `using TAOM.Features.AdvancedCombat;` and `using TaleWorlds.MountAndBlade;`.

**Verify**:
- the Build command exits 0;
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MountRiderSidesTests|FullyQualifiedName~AgentAdapterSideTests"`
  reports Total 16, Passed 16;
- the Step 2 filter (`Name~LooseMountOfUnknownSide`) still reports Total 4, Failed 4 (the services do not
  use the new member yet).

### Step 5: RED, the shared rule and the known-side guards

1. Create `TAOM.Tests/Features/AdvancedCombat/CreatureBiteSidesTests.cs`:

   ```csharp
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using NSubstitute;
   using TAOM.Adapters;
   using TAOM.Features.AdvancedCombat;

   namespace TAOM.Tests.Features.AdvancedCombat;

   /// <summary>
   /// The side rule the spider and warg bites share (#594): a ridden mount is its rider's side, a riderless mount of
   /// unknown side is spared, and everything else compares the agent's own side.
   /// </summary>
   [TestClass]
   [TestCategory("RequiresGame")]
   public class CreatureBiteSidesTests
   {
       private static IAgentAdapter LooseMount(bool knownSide)
       {
           var mount = Substitute.For<IAgentAdapter>();
           mount.IsMount.Returns(true);
           mount.RiderAgent.Returns((IAgentAdapter)null);
           mount.HasKnownSide.Returns(knownSide);
           return mount;
       }

       private static IAgentAdapter Soldier()
       {
           var soldier = Substitute.For<IAgentAdapter>();
           soldier.IsMount.Returns(false);
           soldier.HasKnownSide.Returns(false);   // the unknown-side rule is for mounts only
           return soldier;
       }

       [TestMethod]
       public void Spares_LooseMountOfUnknownSide_ReturnsTrue()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var mount = LooseMount(knownSide: false);
           biter.IsSameTeam(mount).Returns(false);
           Assert.IsTrue(CreatureBiteSides.Spares(biter, mount));
       }

       [TestMethod]
       public void Spares_LooseMountOfUnknownSideAndNoBiterSide_ReturnsTrue()
           => Assert.IsTrue(CreatureBiteSides.Spares(null, LooseMount(knownSide: false)));

       [TestMethod]
       public void Spares_LooseMountOnTheBitersSide_ReturnsTrue()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var mount = LooseMount(knownSide: true);
           biter.IsSameTeam(mount).Returns(true);
           Assert.IsTrue(CreatureBiteSides.Spares(biter, mount));
       }

       [TestMethod]
       public void Spares_LooseMountOnTheOtherSide_ReturnsFalse()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var mount = LooseMount(knownSide: true);
           biter.IsSameTeam(mount).Returns(false);
           Assert.IsFalse(CreatureBiteSides.Spares(biter, mount));
       }

       [TestMethod]
       public void Spares_LooseMountOfKnownSideAndNoBiterSide_ReturnsFalse()
           => Assert.IsFalse(CreatureBiteSides.Spares(null, LooseMount(knownSide: true)));

       [TestMethod]
       public void Spares_RiddenMount_TakesItsRidersSide()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var rider = Substitute.For<IAgentAdapter>();
           var mount = LooseMount(knownSide: false);
           mount.RiderAgent.Returns(rider);
           biter.IsSameTeam(rider).Returns(true);
           biter.IsSameTeam(mount).Returns(false);
           Assert.IsTrue(CreatureBiteSides.Spares(biter, mount));
       }

       [TestMethod]
       public void Spares_RiddenMountOfAnEnemyRider_ReturnsFalse()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var rider = Substitute.For<IAgentAdapter>();
           var mount = LooseMount(knownSide: true);
           mount.RiderAgent.Returns(rider);
           biter.IsSameTeam(rider).Returns(false);
           biter.IsSameTeam(mount).Returns(true);   // the mount's own side must not be consulted
           Assert.IsFalse(CreatureBiteSides.Spares(biter, mount));
       }

       [TestMethod]
       public void Spares_SoldierOnFootOfTheBitersSide_ReturnsTrue()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var soldier = Soldier();
           biter.IsSameTeam(soldier).Returns(true);
           Assert.IsTrue(CreatureBiteSides.Spares(biter, soldier));
       }

       [TestMethod]
       public void Spares_SoldierOnFootOfTheOtherSide_ReturnsFalse()
       {
           var biter = Substitute.For<IAgentAdapter>();
           var soldier = Soldier();
           biter.IsSameTeam(soldier).Returns(false);
           Assert.IsFalse(CreatureBiteSides.Spares(biter, soldier));
       }
   }
   ```

2. Add the known-side guards (they name `HasKnownSide`, which Step 4 added; the Step 6 run proves them,
   and they pin that the new rule spares no enemy and still spares a friend):

   - `SpiderAttackServiceTests.cs`, after the Step 2 test:

     ```csharp
     [TestMethod]
     public void HandleSpiderTargetHit_LooseMountOnTheRidersSide_DoesNotDamage()
     {
         var attacker = Substitute.For<IAgentAdapter>();
         var rider = Substitute.For<IAgentAdapter>();
         var target = EnemyTarget();
         target.IsMount.Returns(true);
         target.RiderAgent.Returns((IAgentAdapter)null);
         target.HasKnownSide.Returns(true);
         attacker.RiderAgent.Returns(rider);
         rider.IsSameTeam(target).Returns(true);

         _sut.HandleSpiderTargetHit(attacker, target, 0, strike: SpiderStrikeProfile.Ridden);

         target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
     }

     [TestMethod]
     public void HandleSpiderTargetHit_LooseMountOfTheOtherSide_Damages()
     {
         var attacker = Substitute.For<IAgentAdapter>();
         var rider = Substitute.For<IAgentAdapter>();
         var target = EnemyTarget();
         target.IsMount.Returns(true);
         target.RiderAgent.Returns((IAgentAdapter)null);
         target.HasKnownSide.Returns(true);
         attacker.RiderAgent.Returns(rider);
         rider.IsSameTeam(target).Returns(false);

         _sut.HandleSpiderTargetHit(attacker, target, 0, strike: SpiderStrikeProfile.Ridden);

         target.Received(1).GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest);
     }
     ```

   - `SpiderStrikeCapTests.cs`, after the Step 2 test:

     ```csharp
     [TestMethod]
     public void RiddenStrike_ALooseMountOfTheOtherSide_IsStruck()
     {
         var spider = Spider();
         var looseHorse = HorseOf(rider: null!, 1.0f);
         looseHorse.HasKnownSide.Returns(true);
         spider.IsSameTeam(looseHorse).Returns(false);

         _sut.StrikeArc(spider, SpiderStrikeProfile.Ridden, countAllies: false, Reports(spider, looseHorse));

         Assert.IsTrue(Struck(looseHorse), "an enemy's loose mount is still bitten");
     }
     ```

   - `WargAttackServiceTests.cs`, after the Step 2 tests:

     ```csharp
     [TestMethod]
     public void HandleWargTargetHit_LooseMountOnTheRidersSide_DoesNotDamage()
     {
         var attacker = Substitute.For<IAgentAdapter>();
         var attackerRider = Substitute.For<IAgentAdapter>();
         attacker.RiderAgent.Returns(attackerRider);
         var target = LooseMount();
         target.HasKnownSide.Returns(true);
         attackerRider.IsSameTeam(target).Returns(true);

         _sut.HandleWargTargetHit(attacker, target, 0);

         target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
     }

     [TestMethod]
     public void HandleWargTargetHit_LooseMountOfTheOtherSide_Damages()
     {
         var attacker = Substitute.For<IAgentAdapter>();
         var attackerRider = Substitute.For<IAgentAdapter>();
         attacker.RiderAgent.Returns(attackerRider);
         var target = LooseMount();
         target.HasKnownSide.Returns(true);
         attackerRider.IsSameTeam(target).Returns(false);

         _sut.HandleWargTargetHit(attacker, target, 0);

         target.Received(1).GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest);
     }
     ```

**Verify**: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails, and its errors name only
`CreatureBiteSides` (does not exist).

### Step 6: GREEN, one rule both bites call

1. Create `Main/Features/AdvancedCombat/CreatureBiteSides.cs`:

   ```csharp
   using TAOM.Adapters;

   namespace TAOM.Features.AdvancedCombat;

   /// <summary>
   /// Whether a creature bite spares its target (#594), shared by the spider and warg bites. A ridden mount is its
   /// rider's side. The engine gives a mount no team, so a riderless mount's side is the one the adapter resolves
   /// from its last rider; a riderless mount whose side is still unknown is spared, never bitten.
   /// </summary>
   internal static class CreatureBiteSides
   {
       /// <param name="biterSide">The agent whose team stands for the biter (the spider's rider, or the spider), or
       /// null when the biter compares no team (a riderless warg).</param>
       /// <param name="target">The agent the bite reached.</param>
       public static bool Spares(IAgentAdapter? biterSide, IAgentAdapter target)
       {
           IAgentAdapter? rider = target.IsMount ? target.RiderAgent : null;
           if (target.IsMount && rider == null && !target.HasKnownSide) return true;
           if (biterSide == null) return false;
           return biterSide.IsSameTeam(rider ?? target);
       }
   }
   ```

2. In `Main/Features/Spider/SpiderAttackService.cs`, replace lines 195 to 202 (the comment and
   `IsSameSide`) with:

   ```csharp
       // The side rule the spider shares with the warg (CreatureBiteSides, #594). The spider's own side is its
       // rider's when it has one (the spider is the mount).
       private static bool IsSameSide(IAgentAdapter attacker, IAgentAdapter target) =>
           CreatureBiteSides.Spares(attacker.RiderAgent ?? attacker, target);
   ```

   and replace the comment at lines 137 and 138 with:

   ```csharp
               // A capped strike (a creature bandit's) holds soldiers only: a riderless mount in the arc is not the
               // soldier who opened the engage gate, which skips mounts, and would take his slot (Codex 2026-09-28 F2).
   ```

   `SpiderAttackService.cs` already has `using TAOM.Features.AdvancedCombat;`.

3. In `Main/Features/Warg/WargAttackService.cs`, replace lines 42 to 45 with:

   ```csharp
           // The side rule the warg shares with the spider (CreatureBiteSides, #594). A riderless warg compares no
           // team with soldiers (null biter side), but still spares a riderless mount of unknown side.
           if (CreatureBiteSides.Spares(attacker.RiderAgent, target))
               return;
   ```

   `WargAttackService.cs` already has `using TAOM.Features.AdvancedCombat;`.

4. The section comment in `TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs` describes the old inline
   rule. Replace its lines 238 and 239:

   ```csharp
       //   victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target
       //   friendly skip    = (attacker.RiderAgent ?? attacker).IsSameTeam(victimTeamSource)
   ```

   with:

   ```csharp
       //   friendly skip    = CreatureBiteSides.Spares(attacker.RiderAgent ?? attacker, target): a ridden mount is
       //                      its rider's side; a riderless mount of unknown side is spared (#594)
   ```

5. The comment in `TAOM.Tests/Features/Spider/SpiderStrikeCapTests.cs` inside
   `CappedStrike_ALooseHorseNearer_NeverTakesTheSoldiersSlot` says a loose horse is on nobody's side, which
   is no longer true. Replace its lines 127 and 128 (above the Step 2 insertion, so they do not shift):

   ```csharp
           // Codex 2026-09-28 F2: the arc reports every live agent, and a teamless loose horse is on nobody's side, so a
           // nearer one used to take a one-target bite from the soldier who opened the engage gate.
   ```

   with:

   ```csharp
           // Codex 2026-09-28 F2: the arc reports every live agent, and a capped strike holds soldiers only, so a
           // nearer loose horse used to take a one-target bite from the soldier who opened the engage gate.
   ```

**Verify**:
- the Build command exits 0;
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CreatureBiteSidesTests|FullyQualifiedName~SpiderAttackServiceTests|FullyQualifiedName~SpiderStrikeCapTests|FullyQualifiedName~WargAttackServiceTests"`
  reports Passed 89, Failed 0, Skipped 2. The count, from the `[TestMethod]` attributes at db5680c8a:
  SpiderAttackServiceTests 42 + 3, SpiderStrikeCapTests 5 + 2, WargAttackServiceTests 26 + 4 of which 2 are
  `[Ignore]`d (the two `WargAttack_...` engine tests, skipped), CreatureBiteSidesTests 9. If the numbers
  differ, explain the difference in your report;
- the Step 2 filter (`Name~LooseMountOfUnknownSide`) now reports Total 6, Passed 6: the four Step 2
  tests plus `Spares_LooseMountOfUnknownSide_ReturnsTrue` and
  `Spares_LooseMountOfUnknownSideAndNoBiterSide_ReturnsTrue`, whose names also match.

### Step 7: record the rider's side in the shared mission behavior (RED, then GREEN)

1. RED: create `TAOM.Tests/Features/AdvancedCombat/AdvancedCombatBehaviorMountSideWiringTests.cs`. This is
   a wiring pin only (the decision lives in `MountRiderSides`, tested in Step 3); the callbacks run inside
   the engine, which no unit test reaches.

   ```csharp
   using System;
   using System.Text.RegularExpressions;
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using TAOM.Tests.Infrastructure;

   namespace TAOM.Tests.Features.AdvancedCombat;

   /// <summary>
   /// Wiring pin (#594): the shared creature behavior records a rider's side against his mount when he is built on it
   /// and when he mounts one, so a bite can resolve a riderless mount's side. Comments are stripped, so a commented-out
   /// call does not pass.
   /// </summary>
   [TestClass]
   public class AdvancedCombatBehaviorMountSideWiringTests
   {
       private const string Record = "MountRiderSides.RecordRider(agent)";

       private static string Member(string signatureFragment)
       {
           var source = RepoPaths.ReadSource("Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs", stripComments: true);
           var start = source.IndexOf(signatureFragment, StringComparison.Ordinal);
           Assert.IsTrue(start >= 0, $"'{signatureFragment}' is missing from AdvancedCombatBehavior.");
           var next = Regex.Match(source.Substring(start + signatureFragment.Length), @"\n    (?:public|private|protected|internal)\b");
           return next.Success ? source.Substring(start, signatureFragment.Length + next.Index) : source.Substring(start);
       }

       [TestMethod]
       public void OnAgentBuild_RecordsTheRidersSide()
           => StringAssert.Contains(Member("public override void OnAgentBuild(Agent agent, Banner banner)"), Record);

       [TestMethod]
       public void OnAgentMount_RecordsTheRidersSide()
           => StringAssert.Contains(Member("public override void OnAgentMount(Agent agent)"), Record);
   }
   ```

   Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~AdvancedCombatBehaviorMountSideWiringTests"`:
   Total 2, Failed 2 (`OnAgentBuild_...` on the `StringAssert.Contains`, `OnAgentMount_...` on
   "is missing from AdvancedCombatBehavior").

2. GREEN: in `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs`:

   - inside `OnAgentBuild`'s `try`, after `_adapterFactory.OnAgentBuilt(agent);` (so a throw in the recording
     can never skip the #592 adapter-cache bookkeeping):

     ```csharp
                 // A rider built on his mount: the engine gives the mount no team, so his side is recorded against it (#594).
                 MountRiderSides.RecordRider(agent);
     ```

   - a field beside `_buildFailureLogged`: `private bool _mountFailureLogged;`

   - a new override after `OnAgentBuild`:

     ```csharp
         // The engine raises this for the rider who just mounted. Recording his side against the mount keeps it once
         // he is gone (#594). Native may raise it off the main thread (#634): RecordRider reads two managed fields and
         // writes a ConditionalWeakTable, which any thread may do. Mounting is a native callback: no exception leaves.
         public override void OnAgentMount(Agent agent)
         {
             base.OnAgentMount(agent);
             try
             {
                 MountRiderSides.RecordRider(agent);
             }
             catch (Exception ex)
             {
                 if (!_mountFailureLogged)
                 {
                     _mountFailureLogged = true;
                     _logger.LogError($"[AdvancedCombat] OnAgentMount threw {ex.GetType().Name}: {ex.Message}");
                 }
             }
         }
     ```

**Verify**: the Build command exits 0; the wiring filter above reports Total 2, Passed 2;
`wc -l Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` prints fewer than 150.

### Step 8: the feature docs

Add one entry at the top of the `## Changelog` list in each doc, dated the day you commit, with no em or
en dash:

- `docs/features/spider.md` (the list starts with `- 2026-11-09: Arachnophobia Mode (#1110) ...`):

  `- <date>: a bite spares a riderless mount on the spider's side (#594). The engine gives a mount no team, so `MountRiderSides` records a rider's team against his mount when he spawns on it or mounts it, and `CreatureBiteSides.Spares`, shared with the warg, compares that side; a riderless mount whose side is unknown is spared. The creature bandits' capped strike still skips every loose mount.`

- `docs/features/warg-combat.md` (the list starts with `- 2026-11-04: SwitchMainAgentController(false) ...`):

  `- <date>: a bite spares a riderless mount on the warg's side (#594), through `CreatureBiteSides.Spares`, shared with the spider: a mount carries its last rider's team (`MountRiderSides`), and a riderless mount whose side is unknown is spared, by a riderless warg too. A riderless warg still compares no team with soldiers.`

Re-read each sentence against your diff before you keep it.

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0.

### Step 9: the full suite

Run the full build and test suite.

**Verify**: build exit 0; dotnet totals 43064 passed (43028 plus the 36 new tests: 4 in Step 2, 9 + 7 in
Step 3, 9 + 5 in Step 5, 2 in Step 7), 3 failed (the same three language-file tests), 9 skipped. A failure
the base did not have is yours.

### Step 10: commit

Stage the in-scope paths you changed by name and commit with the message from "Git workflow" (re-check the
body against the diff).

**Verify**: `git status --porcelain` is empty; `git log -1 --format=%s` shows the subject, at most 72
characters.

## Test plan

- `MountRiderSidesTests` (9): the ledger's record, replace, null handling, own-team precedence and
  `RecordRider` through the rider's mount field.
- `AgentAdapterSideTests` (7): `IsSameTeam` and `HasKnownSide` over a loose mount with a recorded friendly
  rider, an enemy one, none, and two soldiers on one team (the unchanged case).
- `CreatureBiteSidesTests` (9): one test per (target kind x branch) cell: loose mount of unknown side (with
  and without a biter side), loose mount on each side, loose mount of known side with no biter side, ridden
  mount (rider's side wins, both ways), soldier on foot (both ways, including a soldier with no known side,
  which keeps today's rule).
- Service tests (Step 2 and Step 5, 9): the spider's `HandleSpiderTargetHit` and uncapped `StrikeArc`, and
  the warg's `HandleWargTargetHit` (ridden and riderless warg), each against a loose mount of unknown,
  friendly and enemy side.
- `AdvancedCombatBehaviorMountSideWiringTests` (2): source pins that both callbacks call `RecordRider`.
- Pattern to model after: `TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs` (NSubstitute fakes) and
  `TAOM.Tests/Adapters/MissionAdapterFactoryTests.cs` (bare `Agent` objects).
- Not unit-testable, for the `Not-tested:` trailer: whether the engine has set the rider's `MountAgent`
  when `OnAgentBuild` and `OnAgentMount` reach `AdvancedCombatBehavior` in a live battle. The in-game check:
  a Custom Battle where a friendly cavalryman dies beside an AI-ridden spider or warg; his horse is not
  bitten, and an enemy's riderless horse still is.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The build command exits 0.
- [ ] The test command reports 43064 passed, 3 failed (the three baseline language-file tests), 9 skipped.
- [ ] `dotnet test ... --filter "Name~LooseMountOfUnknownSide"` reports Passed 6, Failed 0.
- [ ] `git grep -n "victimTeamSource" -- Main TAOM.Tests` returns nothing (both inline rules are gone; the
      shared rule replaced them).
- [ ] `git grep -n "nobody's side" -- Main TAOM.Tests` returns nothing (the two stale code and test
      comments were rewritten in Step 6).
- [ ] `git grep -n "MountRiderSides.RecordRider(agent)" -- Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs`
      returns exactly 2 lines.
- [ ] `git grep -n "SetTeam" -- Main/Features/AdvancedCombat/MountRiderSides.cs Main/Features/AdvancedCombat/CreatureBiteSides.cs Main/Adapters/AgentAdapter.cs`
      returns nothing.
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0.
- [ ] `git status --porcelain` is empty after the commit, and `git show --stat HEAD` lists only in-scope
      paths.
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes.

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- A step's verification fails twice after a reasonable fix.
- A Step 2 test passes on the base code (the unknown-side bite is not reproduced by the fakes), or fails
  for a reason other than the named assertion.
- `Agent.Team` is no longer an auto-property with a private setter, `Agent._cachedMountAgent` no longer
  exists, or `Agent.MountAgent` no longer reads it, in your build's TaleWorlds reference
  (`pwsh tools/taom-src.ps1 path Agent`); report the mismatch.
- The fix seems to need `SetTeam` on a mount, an `Agent.Index`-keyed store, a Harmony patch, or an edit to
  `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` or a protected file.
- An existing spider, warg, creature-bandit or AdvancedCombat test fails after Step 6 or Step 7: the shared
  rule changed a case this plan did not intend. Report which test and why; do not edit it.
- `AdvancedCombatBehavior.cs` reaches 150 lines.

## Orchestrator steps (not the executor's)

- Issue: #594 already exists. After merge and review, close it with the label `triage-needs-ingame` (the
  Custom Battle check in "Test plan" is owed).
- Deep review: tier 1 (mission combat) before merge.
- No new player-facing text: no `/localize`.
- No new feature doc or feature-map row: the change is recorded in `docs/features/spider.md` and
  `docs/features/warg-combat.md`.

## After merge: the maintainer's actions

- The in-game check from "Test plan", then close #594's `triage-needs-ingame`.

## Maintenance notes

- Assumption to watch in game: the rider's `MountAgent` is already set when `OnAgentMount` runs for a
  remount. Spawn is supported, not proven: Mission.cs:4325 sets it before the rider's `OnAgentBuild`
  through a native setter (`Agent.cs:1037-1041`, `:5329-5333`), native fills the managed cache through
  `UpdateMountAgentCache` (`:1848-1851`), and vanilla reads it at Mission.cs:4359, as the shipped Elephant
  howdah does (`ElephantMissionBehavior.cs:70`). The remount order is native and unproven. If it is null there, a remounted horse keeps the
  side of the rider it spawned under (usually the same side). The next step, if the in-game check shows
  stale sides, is a dismount-side capture (`OnAgentDismount`, or a Harmony prefix on
  `Agent.UpdateRiderAgentCache`, which sees the outgoing rider), not this plan.
- `IsSameTeam` now resolves BOTH agents' sides, so a spider whose rider died also spares its dead rider's
  side. The spider tree sleeps when riderless ("has rider" gate in `SpiderBehaviorTree`), so this reaches
  only a strike already under way. Creature bandits are unaffected: they carry their own team
  (`CreatureBanditSpawner` calls `SetTeam`) and are not `IsMount` after route A clears `Mountable`.
- `StrikeArc` counts an unknown-side loose mount in a capped arc toward `allies` (the creature-bandit
  diagnostics' `alliesInArc`), since the side rule now spares it. Only diagnostics read that count.
- Review should probe: the off-thread `OnAgentMount` path (only managed reads and the
  `ConditionalWeakTable`); that `SideOf` never calls `Agent.IsMount` (native); that no index-keyed store was
  added; that the riderless warg still bites soldiers with no team comparison. `_mountFailureLogged` is a
  plain `bool` written from a possibly off-thread callback; a race only logs the failure twice.
- Follow-up, deferred: the engage decorators (`NoEnemyCloseDecorator`, `PeriodicallyCheckIfCanAttackAnyone`,
  `SpiderEngageDecorator`) read `RiderAgent?.Team ?? Team` and could use the same resolved side when choosing
  a target. Not needed for #594, which is about whom a bite damages.
