# Plan 030: Always-on mission diagnostics stop paying per frame, per agent and per hit for lines they never write

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. This plan runs on
> its OWN branch: no other executor commits on it while you work. The branch may start past the
> planned-at commit `dffdf879` (the program branch carries docs and plans commits); that alone is
> expected and is not a STOP. Step 1 records your starting commit as `<START>`
> (`git rev-parse HEAD`); every later check that compares against your own work uses `<START>`,
> never `dffdf879`. The orchestrator keeps `plans/README.md`; do not edit it.
>
> **Shell**: Git Bash (the Bash tool). Start every command with `cd "<your worktree>" && ` or use
> `git -C "<your worktree>"`. Write and edit files with the Write and Edit tools, never `sed -i` (the
> repo has CRLF files). Prefix every `dotnet` command with the `TEMP="..." TMP="..."` your dispatch
> rules give.
>
> **Drift check (run first)**:
> `git diff --stat dffdf879..HEAD -- Main/Features/MissionDiagnostic Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs Main/Features/TrollBruteForce Main/Features/CreatureBandits/Diagnostics Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs Main/Features/BannerColorPersistence Main/SubModule.cs TAOM.Tests/Features/MissionDiagnostic TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs TAOM.Tests/Features/TrollBruteForce TAOM.Tests/Features/CreatureBandits/CreatureDiagLedgerTests.cs TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs TAOM.Tests/Features/BannerColorPersistence/AgentColorStoreTests.cs TAOM.Tests/Composition/FeatureModulesTests.cs docs/features/mission-diagnostic.md docs/features/career-system.md docs/features/dev-console.md docs/features/troll-brute-force.md docs/features/creature-bandits.md docs/features/companion-tactics.md docs/features/banner-color-persistence.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/patch-targets.md`
> Run it before any edit, so `HEAD` is still `<START>`. Expected: no output. Any output means a
> commit between `dffdf879` and your start touched an in-scope path (another plan's work, or drift):
> STOP and report the listed files and `git log --oneline dffdf879..HEAD -- <those files>`.

## Status

- **Priority**: P2
- **Effort**: L (six independent items, about 25 files, most edits a few lines; one mechanical
  conversion of 22 call sites)
- **Risk**: MED (two Harmony patch members and one patch class are deleted, `Main/SubModule.cs` is
  edited, and 22 diagnostic call sites are rewritten; no save data, no XML, no MCM setting)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472); failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (untranslated keys; the paid
  translator run waits on the maintainer). Python suite: not touched by this plan, not run.
- **Issue**: drafted by the orchestrator; filed on the maintainer's word, possibly after execution.
  If your prompt gives no issue number, write none in the commit bodies.

## Why this matters

Six pieces of always-on diagnostic or dead code run in every battle whether or not the thing they
watch is present. The action-set census marshals a native string, formats an agent name and builds
an interpolated key for every agent on every frame of each mission's first 5 seconds, to log a
handful of lines. The career-perk hit lines format a DEBUG string on every hit by or on a party
whose leader holds a troop pip, the exact cost an earlier fix removed from `CareerPassiveService`
because "A large battle turned that into thousands of allocations and log lines per minute". The
troll trackers scan every agent twice a second in battles with no troll; the creature-bandit
diagnostics do two concurrent-map lookups per hit in battles with no creature and format lines its
budget then throws away; an empty Harmony postfix runs on every `Mission.OnTick`; and a per-agent
colour store keyed on the recycled `Agent.Index` is filled on every spawn and never read. After this
plan each of them costs a field read (or nothing) until its subject appears, and the log carries the
same diagnostic information: the census lines are byte-identical, and the career hit evidence becomes
one line per battle for each hero or party leader, hit mask and set of perks that moved the number.

## Current state

All excerpts were read at `dffdf879`. Every claim below is re-checked by a step before it is relied on.

### Conventions that bind every item

- **ADR-002**: entry points (mission behaviors, Harmony patches) stay under 150 lines and delegate
  decisions to a service. `MissionDiagnosticBehavior.cs` is 108 lines, `TrollBruteForceMissionBehavior.cs`
  117, `CreatureBanditDiagnosticsBehavior.cs` 121; none may reach 150.
- **ADR-007**: services take no sealed TaleWorlds type. Every new service member in this plan takes
  `int`, `bool`, `string` or a TAOM enum only.
- **ADR-008** (`.claude/rules/csharp-architecture.md` "Test Coverage Requirements"): services 100%,
  hooks 80%+, entry points (Harmony patches, mission behaviors) tested in game. The engine-bound lines
  in a behavior are the only "not tested" lines this plan allows.
- **`.claude/rules/csharp-architecture.md` "Mission-scope agent handles and the engine's threads"**:
  never key mission state on `Agent.Index` (the engine recycles it); no engine callback is main-thread
  by contract (`OnAgentHit`, `OnAgentRemoved`, `OnAgentDeleted` and others are raised off the main
  thread in player logs), so a store such a callback reads takes a lock, a concurrent collection or a
  volatile read.
- **`.claude/rules/harmony-patches.md`**: changing or deleting a patch updates its entry in
  `docs/reference/harmony-patch-registry.md`.
- **`.claude/rules/tests.md`**: MSTest, NSubstitute, names `Method_State_Expected`, failing test first.
- No `#region`, no `[Obsolete]`, no `#if DEBUG`. No em or en dash in any comment, doc line or commit
  body you write.
- Read the `.claude/rules/*.md` files whose `paths:` match the files you touch, from your worktree
  (path-scoped rules do not load automatically outside the main checkout): at least
  `csharp-architecture.md`, `csharp-patterns.md`, `harmony-patches.md`, `tests.md`, `provenance.md`
  (it matches `Main/**/*.cs` and `docs/features/**/*.md`; this plan adds no third-party derivation).

### Engine facts (v1.5.3, decompiled with `pwsh tools/taom-src.ps1 path MBActionSet` and `path Agent`)

`TaleWorlds.MountAndBlade.MBActionSet` is a struct over one engine index; the index field is
`internal`, but `GetHashCode()` returns it and `Equals` compares it, so the hash is the action set's
identity, and `GetName()` is the native string marshal:

```csharp
public struct MBActionSet
{
	[CustomEngineStructMemberData("ignoredMember", true)]
	internal readonly int Index;
	public bool IsValid => Index >= 0;
	public bool Equals(MBActionSet a) { return Index == a.Index; }
	public override int GetHashCode() { return Index; }
	public string GetName()
	{
		if (!IsValid) { return "Invalid"; }
		return MBAPI.IMBActionSet.GetNameWithIndex(Index);
	}
```

`Agent` (v1.5.3 `TaleWorlds.MountAndBlade.Agent.cs`):

```csharp
public MBActionSet ActionSet => new MBActionSet(MBAPI.IMBAgent.GetActionSetNo(GetPtr()));   // :722
public string Name { get { if (MissionPeer == null) { return _name.ToString(); } return MissionPeer.Name; } }   // :782, a TextObject format
public BasicCharacterObject Character { get { return _character; } ... }   // :1426, a plain field read
```

`BasicCharacterObject.Race` (`public int Race { get; set; }`, :76) and `IsFemale`
(`public virtual bool IsFemale { get; set; }`, :78) are managed properties.

### Item A: the action-set census

Registered in every mission with no MCM gate, `Main/SubModule.cs:2128-2132`:

```csharp
        var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
        var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
        var diagLogger = IoC.Resolve<IModLogger>();
        if (diagSvc != null && raceMgr != null && diagLogger != null)
            AddTaomBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
```

`Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`: line 19
`private float _actionSetWindowSecondsLeft = 5f;`; lines 41-45 call `CaptureActionSetsFromAgents()`
on every `OnMissionTick` while the window is open; the loop, lines 76-92:

```csharp
            foreach (var agent in mission.Agents)
            {
                if (agent == null) continue;
                // GetName() returns the engine-side string id (e.g. "as_human_warrior").
                var actionSetName = agent.ActionSet.GetName();
                var raceId = agent.Character?.Race ?? -1;
                var raceName = raceId >= 0 ? (_raceManager.GetRaceNameFromId(raceId) ?? $"id={raceId}") : "<none>";
                var agentName = agent.Name ?? "<unnamed>";
                // (four comment lines on why character and monster ids are logged)
                var isFemale = agent.Character?.IsFemale ?? false;
                var characterId = agent.Character?.StringId;
                var monsterId = agent.Monster?.StringId;
                _service.LogActionSetSeen(actionSetName, raceName, isFemale, agentName, characterId, monsterId);
            }
```

`Main/Features/MissionDiagnostic/MissionDiagnosticService.cs`: line 15
`private readonly HashSet<string> _seenActionSets = new HashSet<string>(StringComparer.Ordinal);`, and
lines 169-188:

```csharp
    public void LogActionSetSeen(string actionSetName, string raceName, bool isFemale, string agentName, string characterId, string monsterId)
    {
        if (string.IsNullOrEmpty(actionSetName)) return;
        // (six comment lines: the key is (actionSet, race, sex))
        var key = $"{actionSetName}|{raceName}|{isFemale}";
        if (!_seenActionSets.Add(key)) return;
        _logger.LogInfo(
            $"[MissionDiag] ActionSet '{actionSetName}' used by race='{raceName}' female={isFemale} " +
            $"monster='{monsterId ?? "<null>"}' (first agent: '{agentName}' char='{characterId ?? "<none>"}')");
    }

    public void ResetForNewMission()
    {
        _seenActionSets.Clear();
    }
```

So the line is keyed on (action set name, race name, sex); `monsterId`, `agentName` and `characterId`
come from the first agent with that key. `Main/Core/Domain/RaceManager.cs:129-150`
`GetRaceNameFromId` caches per id and maps an unknown id to the human race name (logging one WARNING
per unknown id), so two race ids can share a name.

`Main/Features/MissionDiagnostic/IMissionDiagnosticService.cs:21-24` (the comment is :21-23, the
method :24; line 20 is blank; the `ResetForNewMission` comment is :26 and the method :27):

```csharp
    // Called from the same boundary for each unique action_set name seen on an
    // agent in the first 5 seconds. Service deduplicates internally — no need
    // to gate per-agent at the boundary.
    void LogActionSetSeen(string actionSetName, string raceName, bool isFemale, string agentName, string characterId, string monsterId);
```

**Design (decided here, a deliberate departure from the audit brief's "look at each agent once"):**
an agent's action set can change inside the 5 s window (`Agent.SetActionSet`, `Agent.cs:2608`), and
the current code would log the new combination, so skipping an agent after its first look could drop
a line. Instead the behavior keeps scanning every agent each frame but reads only three cheap values
(`ActionSet.GetHashCode()`, `Character.Race`, `Character.IsFemale`) and asks the service whether that
integer key is new this mission; only a new key reads names and calls `LogActionSetSeen`, which keeps
its own string dedupe. A cheap key maps to exactly one string key (one action set index has one name;
one race id has one cached race name), so a cheap key already seen means its string key was already
logged, and the first agent to reach `LogActionSetSeen` for any string key is the same agent as
before. Output is byte-identical, including when two unknown race ids share the human name (both cheap
keys pass, the string dedupe still logs once). The brief's suggested monster id is NOT added to the key:
it is not in today's dedupe key, and adding it would log extra lines.

### Item B: per-hit `[CareerPerks]` DEBUG lines

`Main/Core/Logging/FileLogger.cs:85-95`: `LogDebug` has no level gate; every call formats
`DateTime.Now`, interpolates again and enqueues:

```csharp
    public void LogDebug(string message) => Enqueue("DEBUG", message, durable: false);
    ...
    private void Enqueue(string level, string message, bool durable)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _queue.Enqueue($"[{timestamp}] [{level}] {message}");
        if (durable) Drain();
    }
```

`Main/Features/CareerSystem/CareerPassiveService.cs:122-131` (the earlier removal of the same cost):
"The old `if (magnitude != 0f) LogDebug($"...")` line allocated on EVERY such call ... A large battle
turned that into thousands of allocations and log lines per minute, which also buries the
crash-triage log TAOM relies on for native CTDs."

The per-hit lines were added in `7ede923b` ("fix(career): v2.0.29 - typed resistances, ammo, perk
diagnostics (#613)"; `git log -S "hit amp for"` names it).
`Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs`:

- lines 26-34: the existing dedupe state, `_lastStatLog`, `_lastMountLog` (keyed by hero id) and
  `private readonly object _logGate = new object();` ("A hero's stat update can arrive on the AI
  thread ... the dedupe state takes a lock").
- lines 72-79:

```csharp
    public void ResetDiagnostics()
    {
        lock (_logGate)
        {
            _lastStatLog.Clear();
            _lastMountLog.Clear();
        }
    }
```

- lines 175-211, `CalculateDamageAmplification`: builds `string? terms` by concatenation per term
  (`terms += " ArmorPenetration " + Pct(armorPen);` at :183, `" Damage "` at :188,
  `" TroopDamage "` at :203), then at :207-208:

```csharp
        if (terms != null && _logger != null)
            _logger.LogDebug($"[CareerPerks] hit amp for '{attackerHeroId ?? attackerTroopLeaderHeroId}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({terms.TrimStart()})");
```

- lines 213-253, `CalculateDamageReduction`: terms `" Resistance "` (:221),
  `" self buff reduction "` (:227, from `heroBuff.DamageReductionBonus`), `" TroopResistance "` (:236),
  `" ally buff reduction "` (:245, from `CareerAbilityBuffTracker.GetAllyBuff(victimAgentIndex.Value)`),
  then at :249-250:

```csharp
        if (terms != null && _logger != null)
            _logger.LogDebug($"[CareerPerks] hit reduction for '{victimHeroId ?? troopLeaderHeroId ?? victimAgentIndex?.ToString()}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({terms.TrimStart()})");
```

The only production callers are `Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs:30`
and `:40` (campaign only; registered from `SubModule.cs` near :1292). `ResetDiagnostics` runs at the
end of every campaign mission (`Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:209`).
`TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs` is tagged
`[TestCategory("RequiresGame")]` (it constructs `AgentDrivenProperties`) and already pins
`CalculateDamageAmplification_PassiveMovedTheNumber_LogsDebugWithBaseAndResult` (:296-304),
`CalculateDamageReduction_PassiveMovedTheNumber_LogsDebug` (:306-315) and
`CalculateDamageAmplification_NothingApplied_DoesNotLog` (:317-324); its `Setup` (:26-34) builds
`_sut = new CareerAgentStatService(_passives, _logger)` with NSubstitute fakes and calls
`CareerAbilityBuffTracker.ClearAll()`.

**Design (from the maintainer-approved brief, refined here):** log each combination once per mission,
as `LogOnChange` already does for the stat lines, and build no string before the check. The key is
(direction, subject, hit mask, which terms fired). The subject is the hero or party-leader id only:
the ally-buff-only subject today is `victimAgentIndex?.ToString()`, an `Agent.Index`, which recycles,
so those hits share a null-subject key (the line still prints the first such agent's index). The hit
mask stays in the key so the career doc's "with the hit mask (`Melee, Blunt`)" evidence survives for
each mask. Magnitudes are not in the key: within a battle a passive's magnitude does not change, and a
buff whose bonus changes mid-battle is still visible in the once-per-change `agent stats` INFO line.

### Item C: troll trackers in every mission

`Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs` is added to every mission
(`Main/SubModule.cs:2060`, `AddTaomBehavior(new Features.TrollBruteForce.TrollBruteForceMissionBehavior());`).
Constructor, lines 33-41:

```csharp
    public TrollBruteForceMissionBehavior()
    {
        _service = IoC.Resolve<ITrollBruteForceService>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new CreatureTreeTracker(TreeName, "[TrollBruteForce]",
            a => _service.IsBruteForceTroll(a.Monster?.StringId), _logger);
        _spacing = new TrollFormationSpacingTracker(_service, _logger);
        _clips = new TrollClipTrace(_service, _logger);
    }
```

Tick, lines 73-96 (the two trackers run unconditionally at :87-88):

```csharp
    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized) Initialize();

            if (!_treesAdded)
            {
                _treesAdded = true;
                int count = _tracker.AttachAll(Mission.Current.AllAgents);
                _logger.LogInfo($"[TrollBruteForce] Attached behavior trees to {count} troll(s)");
            }

            _tracker.PruneDead();
            _spacing.Tick(Mission.Current);
            _clips.Tick(Mission.Current);   // diagnostic last: a throw here must not skip the spacing
        }
        catch (Exception ex)
        { ... }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach: only after Initialize registered the tree (first OnMissionTick).
        if (_treesAdded)
            _tracker.TryLateAttach(agent);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo(...);
        _tracker.Clear();
        _spacing.Clear();
        _clips.Clear();
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
```

`TrollFormationSpacingTracker.Tick` (:40-97) walks `mission.Agents` every 0.5 s writing a dictionary
entry per agent, then copies `TrollFormationSpacingStore.Keys` (a `ConcurrentDictionary.Keys`, which
locks and copies) at :64. `TrollClipTrace.Tick` (:37-57) walks `mission.Agents` every 0.5 s and reads
each listed troll every frame. In a mission with no troll neither writes anything: every formation's
`FormationUnitDiameter(vanilla, units, trollCount: 0, ...)` returns null
(`TrollBruteForceService.cs:41`, `if (unitCount <= 0 || trollCount <= 0 || ...) return null;`) and
`TrollFormationSpacingStore.Set(formation, null)` on an absent key returns false
(`TrollFormationSpacingStore.cs:68-70`), and the clip trace lists no troll. So gating both on "a troll
has been built in this mission" changes nothing in a troll-less mission and nothing once a troll
exists. `CreatureTreeTracker`'s own comment (`Main/Features/AdvancedCombat/CreatureTreeTracker.cs:13-15`)
records that custom-battle deployment spawns creatures AFTER the first tick, through `OnAgentBuild`.
The predicate is `ITrollBruteForceService.IsBruteForceTroll(string? monsterId)`
(`TrollBruteForceService.cs:13-14`: `monsterId is not null && ActionSetsByMonster.ContainsKey(monsterId)`),
and `new TrollBruteForceService()` is pure and constructible in a test
(`TAOM.Tests/Features/TrollBruteForce/TrollBruteForceServiceTests.cs:17`). `TrollClipTrace` is marked
temporary (`TrollClipTrace.cs:16-18`); deleting it is the maintainer's call, not this plan's.

### Item D: creature-bandit diagnostics in every battle

`Main/Features/CreatureBandits/CreatureBanditsModule.cs:30-35` adds
`new Diagnostics.CreatureBanditDiagnosticsBehavior()` to every mission ("Temporary diagnostics").
`Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagnosticsBehavior.cs:44-92`: seven engine
callbacks, each starting with `CreatureBanditDiag.SerialOf(...)` lookups, for example:

```csharp
    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow,
        in AttackCollisionData attackCollisionData)
    {
        int victim = CreatureBanditDiag.SerialOf(affectedAgent);
        int attacker = CreatureBanditDiag.SerialOf(affectorAgent);
        if (victim == 0 && attacker == 0) return;
        CreatureDiagCapture.Hit(victim, attacker, affectedAgent, affectorAgent, blow, attackCollisionData);
    }
```

The others are `OnAgentRemoved` (:53-59, two lookups), `OnAgentDeleted` (:61-67), `OnAgentPanicked`
(:69-73), `OnAgentFleeing` (:75-79), `OnAgentMount` (:81-86, keyed on `agent.MountAgent`) and
`OnAgentAlarmedStateChanged` (:88-92). The class comment (:8-9) says "battles without creatures pay
one lookup per event".

`Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiag.cs`: `SerialOf` (:56) is a
`ConcurrentDictionary<Agent, int>` lookup; `Reserve` (:65) registers a ledger record on the main
thread before a creature spawns (`CreatureBanditSpawner.cs:61`), and `Bind` (:68-72) fills the serial
map after; `ResetForMission` (:286-298) resets the ledger. The line writer, :91-106:

```csharp
    /// <summary>Main thread: write a line if the budget allows it.</summary>
    internal static void Write(int serial, string kind, string line, bool warning = false)
    {
        var logger = Logger;
        if (logger == null) return;
        switch (Ledger.TryTakeLine(serial, kind))
        {
            case LineVerdict.Write:
                if (warning) logger.LogWarning(line); else logger.LogInfo(line);
                break;
            case LineVerdict.CapReached:
                logger.LogWarning(CreatureDiagFormat.Line("cap", 0f, 0, "missionCap", CreatureDiagFormat.I(MissionCap),
                    "note", "further diag lines this mission are counted, not written"));
                break;
        }
    }
```

Every caller builds the full `Line(...)` string before `Write` can refuse it. The 22 callers (all in
the Diagnostics folder; `git grep -n "Write(" -- Main/Features/CreatureBandits` minus
`WriteSummary`, `WriteSides`, `WriteEvent`, `WriteFormations` and `LogWarning` lines lists them):

- `CreatureBanditDiag.cs`: :150 (`hunt`), :154 (`hunt`), :178 (`attack`), :219 (`engage`), :246 (`declined`).
- `CreatureBanditDiagTicker.cs`: :127 (`hit-taken`), :134 (`bite`), :140 (`kill`), :144 (`removed`),
  :151 (`backstop`), :155 (`outcome`), :163 (`outcome`), :167 (`event`), :196 (`outcome`), :291
  (`outcome`), :307 (`outcome`), :314 (`outcome`), :376 (`snap`), :418 (`kind`), :455 (`sides`),
  :483 (`sides`), :516 (`formation`).

One of them reads the budget it is about to spend: the snap line at `CreatureBanditDiagTicker.cs:376-380`
reports `"lines", I(CreatureBanditDiag.Ledger.MissionLines)`, which today is read before
`TryTakeLine` increments it. Moving the check first would print one more; the conversion captures the
count first (Step 11).

`Main/Features/CreatureBandits/Diagnostics/CreatureDiagLedger.cs`: fields :92-97
(`private int _nextSerial;` at :95), constructor :99; `internal int Count => _nextSerial;` (:107, the ticker's own
every-frame gate at `CreatureBanditDiagTicker.cs:64` is `if (ledger.Count == 0 && CreatureBanditDiag.Events.IsEmpty) return;`);
`Register` increments `_nextSerial` (:113-118); `TryTakeLine` (:123-148) is main-thread only and
mutates the budget; `Reset` (:153-160) sets `_nextSerial = 0`. `TAOM.Tests/Features/CreatureBandits/CreatureDiagLedgerTests.cs`
tests the ledger without engine types (no test category). `CreatureBanditDiag` is a static class whose
static field `Serials` is a `ConcurrentDictionary<Agent, int>`, so a test that touches it loads the
engine's `Agent` type: tag such a test class `[TestCategory("RequiresGame")]` (hosted CI runs on
metadata-only reference assemblies and skips that category). `CreatureBanditLog.Logger`
(`Main/Features/CreatureBandits/CreatureBanditLog.cs`, `internal static IModLogger? Logger;`) is the
logger `CreatureBanditDiag.Logger` returns; tests may set it (internals are visible to `TAOM.Tests`).

### Item E: an empty per-frame Harmony postfix

`Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` (37 lines: usings
and a doc comment at :1-14, the class from its first attribute at :15 to its closing brace at :37):

```csharp
[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
public static class Patch35_Mission_OnTick
{
    private static ICompanionTacticsSettingsProvider _settings;

    [HarmonyPostfix]
    public static void Postfix(float dt, float realDt, bool updateCamera, bool doAsyncAITick)
    {
        if (_settings == null)
        {
            try { _settings = IoC.Resolve<ICompanionTacticsSettingsProvider>(); }
            catch { return; }
        }
        if (_settings == null) return;
        if (!_settings.EnableFormationPresets) return;

        // Body intentionally empty for now: hotkey-driven preset I/O moved to UI buttons.
        ...
    }
}
```

The only state it writes is its own private `_settings`. The category `Patch35_CompanionTactics` is
applied at `Main/SubModule.cs:1816` (`TryPatchCategory("Patch35_CompanionTactics");`) and keeps six
other classes (`Patch35_OOBUIHandler_Finalize`, `Patch35_OOBUIHandler_Tick`,
`Patch35_OrderOfBattleVM_Ctor`, `Patch35_OrderOfBattleVM_Finalize`, `Patch35_OOBHeroItem_RefreshValues`,
`Patch35_PartyCharacterVM_RefreshValues`), so the `SubModule.cs` line stays. `git grep -n Patch35_Mission_OnTick dffdf879`
finds the class itself plus docs only: `docs/features/companion-tactics.md:149` and `:195`,
`docs/reference/harmony-patch-registry.md:1057`,
`docs/reference/taleworlds-api-snapshot/patch-targets.md:94`, and historical records under
`docs/archive/`, `docs/audits/`, `docs/reviews/` and `plans/_audit/` (left as they are). The registry
also describes it without its name at `harmony-patch-registry.md:271` (the Target list ends
`` `Mission.OnTick(float,float,bool,bool)` (Postfix) ``) and `:273` (the last FormationPresets clause).

### Item F: a dead per-agent store keyed on `Agent.Index`

`Main/Features/BannerColorPersistence/AgentColorStore.cs:6-16`:

```csharp
public class AgentColorStore : IAgentColorStore
{
    private readonly Dictionary<int, ClanColorInfo> _colors = new();

    public void Register(int agentIndex, ClanColorInfo info) => _colors[agentIndex] = info;

    public bool TryGetColors(int agentIndex, out ClanColorInfo info) =>
        _colors.TryGetValue(agentIndex, out info);

    public void Clear() => _colors.Clear();
}
```

`git grep -n "TryGetColors" dffdf879` finds only the store, its interface
(`IAgentColorStore.cs:8`) and `TAOM.Tests/Features/BannerColorPersistence/AgentColorStoreTests.cs`
(4 test methods). Nothing in production reads the store. Its writers:

- `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs`: field `_colorStore`
  (:15), `Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter, IAgentColorStore colorStore)`
  (:17-22), and the whole `[HarmonyPostfix]` (:68-78), whose only effect is
  `_colorStore?.Register(__result.Index, info.Value);` (:77). The `[HarmonyPrefix]` (:54-66) is what
  recolours troops (`agentBuildData.ClothingColor1(...).ClothingColor2(...)`, :64) and stays.
- `Main/Features/BannerColorPersistence/Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs`, the whole
  file (37 lines): its only patch method is a `[HarmonyPrefix]` (:23-36) that reads
  `_service.IsAgentVisualColorsEnabled()`, `_heroAdapter.GetClanColorInfo(character)` and
  `_service.ShouldUseClanColor(info.Value)`, then calls `_colorStore?.Register(__instance.Index, info.Value);`
  (:35). Those three reads have no side effect (`Main/Adapters/BannerHeroAdapter.cs:8-19` builds a
  `ClanColorInfo` from `hero.Clan`; `BannerColorService.cs:16-22` and :48 read the config), so with
  the `Register` call gone the prefix does nothing and the class is deleted with it.
  `docs/features/banner-color-persistence.md:26` claims it "covers hero agents only": it never wrote
  to the agent, so that sentence is corrected.

Its lifetime wiring:

- `Main/Features/BannerColorPersistence/AgentColorStoreCleanupBehavior.cs` (whole file): a
  `MissionBehavior` whose only member clears the store (`protected override void OnEndMission() => _store.Clear();`, :13).
- `Main/Features/BannerColorPersistence/BannerColorPersistenceIoC.cs:13`:
  `container.Register<IAgentColorStore, AgentColorStore>(Reuse.Singleton);`
- `Main/SubModule.cs:635-637` (in `OnSubModuleLoad`):

```csharp
        var agentColorStore = IoC.Resolve<IAgentColorStore>();
        Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
        Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
```

- `Main/SubModule.cs:2113-2119` (in the mission behavior registration):

```csharp
        AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());

        var colorStore = IoC.Resolve<IAgentColorStore>();
        if (colorStore != null)
            AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));

        // Feature modules' mission behaviors: after the feature behaviors above, before the kernel tail
```

- `TAOM.Tests/Composition/FeatureModulesTests.cs:169-170` pins the feature-module runner call after
  the cleanup behavior:

```csharp
        AssertOnceBetween(code, "new AgentColorStoreCleanupBehavior(colorStore)",
            "FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);", "new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(");
```

  Its intent (the runner call sits after the last kernel feature behavior) survives with the anchor
  moved to `BattleActionBarMissionView`, the statement that becomes the last one before it
  (`Main/SubModule.cs:2113`, which occurs once in the file).

`TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs:208` classifies
`Mission_SpawnAgent_Patch` by class name; that class stays, so the entry stays.
`docs/reference/taleworlds-api-snapshot/patch-targets.md` lists both classes (:25 and :38) and has
268 rows under its header line `Patches: 268.`

### Blast radius

`python tools/graphify_taom.py affected "<Type>" --depth 2` against a graph refreshed at `dffdf879`
(the graph's C# call edges are incomplete: it misses `SubModule.cs` constructing these behaviors, so
the `git grep` lines above are the authority):

- `MissionDiagnosticService`, `MissionDiagnosticBehavior`, `TrollBruteForceMissionBehavior`,
  `CreatureBanditDiagnosticsBehavior`, `Patch35_Mission_OnTick`, `AgentColorStoreCleanupBehavior`:
  "No affected nodes found."
- `IMissionDiagnosticService`, `IAgentColorStore`: "No unique node match".
- `CareerAgentStatService`: only `CareerAgentStatServiceTests` (36 test methods listed).
- `CreatureBanditDiag`: `CreatureDiagCapture` (Hit, Mounted, RemovedCore, Simple), `Patch93_CreatureBandits`
  prefixes and postfix, `CreatureHuntTask.Execute`, `SpiderAttackTaskBase.Execute`,
  `SpiderEngageDecorator.Evaluate`, `CreatureBanditSpawner` (TrySpawn, Wire),
  `CreatureBanditMissionBehavior.OnAgentRemoved`, the diagnostics behavior's callbacks and the ticker's
  writers. The spider and hunt callers use `NoteAttack`, `NoteEngage`, `NoteHunt`, whose signatures do
  not change.
- `CreatureDiagLedger`: `CreatureBanditDiag`, the ticker, `SpiderEngageDecorator.Evaluate` (the static
  classifiers), `CreatureDiagLedgerTests`.
- `CreatureBanditDiagTicker`: `CreatureBanditDiagnosticsBehavior` only.
- `AgentColorStore`: `AgentColorStoreTests` only.
- `Mission_SpawnAgent_Patch`, `Agent_EquipItemsFromSpawnEquipment_Patch`: `SubModule.OnSubModuleLoad`
  (:636, :637) only.

## Step 0: the maintainer's edit

None. This plan touches no protected file (`.claude/settings.json`, `.claude/settings.local.json`,
`Directory.Build.props`, `docs/adrs/*.md`).

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's failure set only (see Step 14 for the totals) |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Docs | `python tools/lint_docs.py --fail-on-drift` | the same exit code and findings as the Step 1 run, no new finding |
| Data | `python tools/validate_moduledata.py` | not needed: this plan touches no ModuleData |

Prefix every `dotnet` command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules (quoted).
Both `-p:` flags go on build AND test. Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you create, modify or delete):

- Item A: `Main/Features/MissionDiagnostic/IMissionDiagnosticService.cs`,
  `Main/Features/MissionDiagnostic/MissionDiagnosticService.cs`,
  `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`,
  `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticServiceTests.cs` (new),
  `docs/features/mission-diagnostic.md`.
- Item B: `Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs`,
  `Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs` (the `ResetDiagnostics` doc
  comment only), `TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs`,
  `docs/features/career-system.md`, `docs/features/dev-console.md`.
- Item C: `Main/Features/TrollBruteForce/TrollPresence.cs` (new),
  `Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs`,
  `TAOM.Tests/Features/TrollBruteForce/TrollPresenceTests.cs` (new),
  `docs/features/troll-brute-force.md`.
- Item D: `Main/Features/CreatureBandits/Diagnostics/CreatureDiagLedger.cs`,
  `Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiag.cs`,
  `Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagnosticsBehavior.cs`,
  `Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagTicker.cs`,
  `TAOM.Tests/Features/CreatureBandits/CreatureDiagLedgerTests.cs`,
  `TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs` (new),
  `docs/features/creature-bandits.md`.
- Item E: `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` (delete),
  `docs/features/companion-tactics.md`.
- Item F: `Main/Features/BannerColorPersistence/AgentColorStore.cs` (delete),
  `Main/Features/BannerColorPersistence/IAgentColorStore.cs` (delete),
  `Main/Features/BannerColorPersistence/AgentColorStoreCleanupBehavior.cs` (delete),
  `Main/Features/BannerColorPersistence/Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs` (delete),
  `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs`,
  `Main/Features/BannerColorPersistence/BannerColorPersistenceIoC.cs`,
  `TAOM.Tests/Features/BannerColorPersistence/AgentColorStoreTests.cs` (delete),
  `TAOM.Tests/Composition/FeatureModulesTests.cs` (one anchor string),
  `docs/features/banner-color-persistence.md`.
- Items E and F: `docs/reference/harmony-patch-registry.md`,
  `docs/reference/taleworlds-api-snapshot/patch-targets.md`.
- `Main/SubModule.cs` (single-owner): exactly the Item F edits in Step 13, nothing else.

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/TAOM.csproj` (the SDK project globs `*.cs`, so added and deleted files need no
  csproj edit). If the build says otherwise, STOP.
- Any other line of `Main/SubModule.cs`, including the `Patch35_CompanionTactics` apply line (:1816),
  the troll registration (:2060) and the MissionDiagnostic registration (:2128-2132).
- `Main/Core/Logging/FileLogger.cs` and any log level: no INFO line becomes DEBUG, no DEBUG line is
  removed outright. The spider, troll-smash and signature-hero INFO lines and
  `EnableHowdahDiagnostics` are the maintainer's decision, not this plan's.
- Any MCM setting or default.
- Deleting `TrollClipTrace`, `TrollClipLog` or the creature-bandit Diagnostics folder (temporary
  diagnostics; the maintainer strips them after sign-off).
- `Main/Features/CareerSystem/Diagnostics/CareerPerkConsumerMap.cs` (its "hit lines (DEBUG)" text stays
  true).
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`.
- `CHANGELOG.md` (generated at release; the commit bodies are the changelog), `plans/README.md`.
- Historical records: `docs/archive/`, `docs/audits/`, `docs/reviews/`, `docs/changelog-archive/`, `plans/`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. The one sanctioned test edits are the new
  tests, the deletion of `AgentColorStoreTests.cs` (it tests deleted code) and the one anchor string in
  `FeatureModulesTests.cs` named above. Anything else: STOP and report.

## Git workflow

- Commit on your branch; never push, merge, rebase or open a PR.
- One commit per item (six commits), staging explicit paths only (never `-A`, `-u` or `.`). For a
  deleted file, stage it with `git rm <path>` (or `git add <path>` after deleting it with the shell).
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, where `<version>` is the
  `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at planning time;
  a hook refuses any other). Suggested subjects (68 characters or fewer with `v2.0.32`):
  - A: `perf(mission-diag): <version> - census reads names only for new combos`
  - B: `perf(career): <version> - log each perk hit combination once a battle`
  - C: `perf(trolls): <version> - troll trackers wait for the first troll`
  - D: `perf(creature-bandits): <version> - diag skips work it would not log`
  - E: `perf(companion-tactics): <version> - remove the empty OnTick postfix`
  - F: `perf(banner-colors): <version> - delete the unread agent colour store`
- The body is the changelog entry for a reader of the release note: what changed and why, wrapped at
  72, no em or en dash. Name the issue number only if your prompt gave one. Write it to a file under
  your scratch folder and run `git commit -F "<file>"`.
  No `Co-Authored-By` or other AI attribution. Add a `Not-tested:` trailer naming the engine-bound
  lines each commit could not unit test (listed per item in "Test plan"). Never `--no-verify`; if a
  hook denies, read its reason, fix the cause if it is yours, and if the hook judged the main checkout
  instead of your worktree, STOP and return BLOCKED with its text.

## Steps

### Step 1: record the base

Before any edit, record where you start, run the drift check from the top of this plan, then the
full suite and the doc linter:

```
git rev-parse HEAD
git log --oneline dffdf879..HEAD
git status --porcelain
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
python tools/lint_docs.py --fail-on-drift; echo "lint exit=$?"
```

Write into your report: the full SHA `git rev-parse HEAD` printed (this is `<START>`; shell variables
do not survive between calls, so paste the SHA itself wherever this plan writes `<START>`), the
`git log` lines (commits the branch carries past the planned-at commit; expected to be docs and plans
commits only), the `git status --porcelain` output (call it the Step 1 status; it may list files you
did not create, which you leave alone), both totals lines, the failing test names, the lint exit code
and its finding count.

**Verify**: the drift check printed nothing; the dotnet totals line reads
`Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` and the one failure is
`EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is explained in your report (a
different failure set is a STOP condition).

### Step 2 (Item A, RED): pin the census line and the new integer key

Create `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticServiceTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionDiagnostic;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The action-set census logs one line per (action set, race, sex) per mission. The boundary pre-filters every agent
// on an integer key (action set index, race id, sex) so that only a combination not yet seen pays for the native
// name marshal and the agent name; the line itself and its first-sighting order must not change.
[TestClass]
public class MissionDiagnosticServiceTests
{
    private IModLogger _logger = null!;
    private MissionDiagnosticService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new MissionDiagnosticService(_logger);
    }

    [TestMethod]
    public void TryMarkActionSetKey_FirstSighting_TrueThenFalse()
    {
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
        Assert.IsFalse(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
    }

    [TestMethod]
    public void TryMarkActionSetKey_AnyPartDiffers_IsANewKey()
    {
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
        Assert.IsTrue(_sut.TryMarkActionSetKey(13, 3, isFemale: false), "another action set");
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 4, isFemale: false), "another race");
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: true), "the other sex");
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, -1, isFemale: false), "no character");
    }

    [TestMethod]
    public void ResetForNewMission_ForgetsTheKeys()
    {
        _sut.TryMarkActionSetKey(12, 3, isFemale: false);

        _sut.ResetForNewMission();

        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
    }

    [TestMethod]
    public void LogActionSetSeen_FirstAgentOfACombination_WritesTheUnchangedLineOnce()
    {
        _sut.LogActionSetSeen("as_human_warrior", "elf", true, "Legolas", "elf_archer", "elf_monster");
        _sut.LogActionSetSeen("as_human_warrior", "elf", true, "Tauriel", "elf_scout", "elf_monster");

        _logger.Received(1).LogInfo(Arg.Any<string>());
        _logger.Received(1).LogInfo("[MissionDiag] ActionSet 'as_human_warrior' used by race='elf' female=True " +
            "monster='elf_monster' (first agent: 'Legolas' char='elf_archer')");
    }
}
```

The last test is a characterization pin: it passes at the base and must keep passing (it proves the
line text is untouched). Re-derive its expected string from `MissionDiagnosticService.cs:180-182`
before running; if it differs, the excerpt in Current state is stale: STOP.

Run: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MissionDiagnosticServiceTests"`

**Verify**: the build fails with error `CS1061` naming `TryMarkActionSetKey` on
`MissionDiagnosticService`, and no other error. Quote it in your report.

### Step 3 (Item A, GREEN): the integer key in the service

1. `IMissionDiagnosticService.cs`: replace the three-line comment above `LogActionSetSeen` (lines
   21-23; the method is line 24 and stays as it is) and add the new member after the method:

```csharp
    // Called from the same boundary for an agent whose TryMarkActionSetKey was true. Logs once per
    // (action set name, race name, sex) per mission, naming the first agent seen with it.
    void LogActionSetSeen(string actionSetName, string raceName, bool isFemale, string agentName, string characterId, string monsterId);

    // Called from the boundary for every agent in the action-set window before any name is read: true the
    // first time this mission sees the (action set index, race id, sex) combination. The boundary reads the
    // names and calls LogActionSetSeen only on true, so a combination already seen costs no native string
    // marshal. One index has one name and one race id one cached race name, so this filter never hides a line.
    bool TryMarkActionSetKey(int actionSetIndex, int raceId, bool isFemale);
```

   Also change the `ResetForNewMission` comment (:26, today
   `// Resets per-mission state (action-set dedup set, first-tick flag).`) to
   `// Resets per-mission state (both action-set dedup sets).` (the service holds no first-tick flag:
   `_seenActionSets` is its only per-mission field today, cleared at :187).

2. `MissionDiagnosticService.cs`: add the field beside `_seenActionSets` (line 15), the method, and the
   clear:

```csharp
    private readonly HashSet<(int ActionSet, int Race, bool Female)> _seenActionSetKeys = new();
```

```csharp
    public bool TryMarkActionSetKey(int actionSetIndex, int raceId, bool isFemale) =>
        _seenActionSetKeys.Add((actionSetIndex, raceId, isFemale));
```

```csharp
    public void ResetForNewMission()
    {
        _seenActionSets.Clear();
        _seenActionSetKeys.Clear();
    }
```

   `LogActionSetSeen` does not change.

**Verify**: the Step 2 filter now reports `Passed!` with 4 tests, 0 failed.

### Step 4 (Item A): the behavior reads names only for a new key, then commit

Not unit-testable (it reads `Agent`, a native-backed engine type): this is the `Not-tested:` line for
commit A.

In `MissionDiagnosticBehavior.cs`, replace the loop body (lines 76-92) with:

```csharp
            foreach (var agent in mission.Agents)
            {
                if (agent == null) continue;
                // Cheap pre-filter with no string marshal: MBActionSet.GetHashCode() is its engine index, and
                // race id plus sex select the race name and sex the service keys its line on. Only a combination
                // not yet seen this mission reads the names below.
                var actionSet = agent.ActionSet;
                var character = agent.Character;
                var raceId = character?.Race ?? -1;
                var isFemale = character?.IsFemale ?? false;
                if (!_service.TryMarkActionSetKey(actionSet.GetHashCode(), raceId, isFemale)) continue;

                // GetName() returns the engine-side string id (e.g. "as_human_warrior").
                var actionSetName = actionSet.GetName();
                var raceName = raceId >= 0 ? (_raceManager.GetRaceNameFromId(raceId) ?? $"id={raceId}") : "<none>";
                var agentName = agent.Name ?? "<unnamed>";
```

followed by the four comment lines at base `MissionDiagnosticBehavior.cs:84-87`, moved here byte for
byte (they start `// Character id + Monster id turn "a dwarf is running as_human_warrior" into an`
and the fourth ends `the census names a symptom and nothing else.`; copy them from the file, not
from this plan), then:

```csharp
                var characterId = character?.StringId;
                var monsterId = agent.Monster?.StringId;
                _service.LogActionSetSeen(actionSetName, raceName, isFemale, agentName, characterId, monsterId);
            }
```

Base line 88 (`var isFemale = agent.Character?.IsFemale ?? false;`) is gone: `isFemale` is now read
in the pre-filter above.

Leave the `try`/`catch` around the loop, the window arithmetic and `OnEndMissionInternal` as they are.

Then update `docs/features/mission-diagnostic.md`:

- line 26: replace everything from "logs every unique `(actionSetName, raceName)` combo seen" to the
  end of that sentence ("...doesn't need a per-agent gate.", which today contains a dash this plan
  does not copy) with "logs every unique
  `(actionSetName, raceName, sex)` combination seen. Each agent is first checked on an integer key
  (the action set's engine index, the race id and the sex, `TryMarkActionSetKey`); only a combination
  not yet seen this mission reads the action set name, the race name and the agent name, so the
  window costs three managed reads and one native index read per agent per frame."
- line 57: "4 methods" becomes "5 methods" and add `TryMarkActionSetKey` to its list.
- line 58: "Holds the action-set dedup `HashSet`" becomes "Holds the two action-set dedup sets (the
  integer key and the logged line's string key)".
- line 84: replace "Service-level `HashSet<(actionSet, race)>` dedup keeps log volume bounded" with
  "The integer pre-filter keeps the per-agent cost to managed reads, and the service-level
  `(actionSet, race, sex)` dedup keeps log volume bounded".
- line 70 (Tests): replace the whole paragraph, which begins "No service-level unit tests yet." and
  becomes false with this step, with "`TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticServiceTests.cs`
  covers the action-set integer key (first and repeat sighting, each key part, the per-mission reset)
  and pins the census line text. The behavior's agent loop reads the engine's `Agent` and is exercised
  in game on every launch."
- line 86 (Memory): "the dedup `HashSet` resets per-mission" becomes "both action-set dedup sets
  reset per mission".

Run the build, the filtered `MissionDiagnosticServiceTests`, and the `FeatureModulesTests` filter (it
pins the `MissionDiagnosticBehavior(` constructor text in `SubModule.cs`, which must not change).

**Verify**: build exit 0; both filters `Passed!` with 0 failed; `git status --porcelain` lists the
five Item A files and otherwise only what the Step 1 status listed. Commit A (stage those five paths), body explaining the census now reads native names
only for a new (action set, race, sex) combination and logs the same lines; trailer
`Not-tested: MissionDiagnosticBehavior's agent loop (engine Agent; unit tests cover the service key and the line text)`.

### Step 5 (Item B, RED): one hit line per combination per battle

Append to `TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs`, after
`CalculateDamageAmplification_NothingApplied_DoesNotLog` (ends at line 324), inside the logging
section:

```csharp
    [TestMethod]
    public void CalculateDamageAmplification_SameHeroAndTerms_LogsOncePerMission()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0.10f);

        _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee | AttackTypeMask.Cut, 50f);
        _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee | AttackTypeMask.Cut, 40f);

        _logger.Received(1).LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void CalculateDamageAmplification_TroopDamageOnManyHits_ScalesEveryHitButLogsOnce()
    {
        // The case #613 made hot: every blow by a non-hero troop whose party leader holds a TroopDamage pip.
        _passives.GetPassiveMagnitude("lord1", PassiveEffectType.TroopDamage).Returns(0.10f);

        for (int i = 0; i < 50; i++)
            Assert.AreEqual(55f, _sut.CalculateDamageAmplification(null, "lord1", AttackTypeMask.Melee | AttackTypeMask.Cut, 50f), 0.01f);

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("lord1") && s.Contains("TroopDamage")));
    }

    [TestMethod]
    public void CalculateDamageReduction_SameHeroAndTerms_LogsOncePerMission()
    {
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee | AttackTypeMask.Blunt).Returns(0.10f);

        _sut.CalculateDamageReduction("hero1", null, null, AttackTypeMask.Melee | AttackTypeMask.Blunt, 40f);
        _sut.CalculateDamageReduction("hero1", null, null, AttackTypeMask.Melee | AttackTypeMask.Blunt, 30f);

        _logger.Received(1).LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void CalculateDamageReduction_AllyBuffOnTwoAgents_LogsOnce_NeverKeyedOnTheAgentIndex()
    {
        // Agent.Index recycles, so the ally-buff-only subject shares one key; the line names the first agent.
        CareerAbilityBuffTracker.SetAllyBuff(42, new ActiveBuffs { DamageReductionBonus = 0.20f });
        CareerAbilityBuffTracker.SetAllyBuff(43, new ActiveBuffs { DamageReductionBonus = 0.20f });

        Assert.AreEqual(40f, _sut.CalculateDamageReduction(null, 42, null, AttackTypeMask.Melee | AttackTypeMask.Cut, 50f), 0.01f);
        Assert.AreEqual(40f, _sut.CalculateDamageReduction(null, 43, null, AttackTypeMask.Melee | AttackTypeMask.Cut, 50f), 0.01f);

        _logger.Received(1).LogDebug(Arg.Any<string>());
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("'42'") && s.Contains("ally buff reduction")));
    }

    [TestMethod]
    public void CalculateDamageAmplification_NewMaskOrSubject_LogsAgain()
    {
        _passives.GetPassiveMagnitude("lord1", PassiveEffectType.TroopDamage).Returns(0.10f);
        _passives.GetPassiveMagnitude("lord2", PassiveEffectType.TroopDamage).Returns(0.10f);

        _sut.CalculateDamageAmplification(null, "lord1", AttackTypeMask.Melee | AttackTypeMask.Cut, 50f);
        _sut.CalculateDamageAmplification(null, "lord1", AttackTypeMask.Ranged | AttackTypeMask.Pierce, 50f);
        _sut.CalculateDamageAmplification(null, "lord2", AttackTypeMask.Melee | AttackTypeMask.Cut, 50f);
        _sut.CalculateDamageAmplification(null, "lord1", AttackTypeMask.Melee | AttackTypeMask.Cut, 50f);

        _logger.Received(3).LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ResetDiagnostics_ClearsTheHitDedupe_SoTheNextBattleLogsAgain()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0.10f);
        _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, 50f);

        _sut.ResetDiagnostics();
        _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, 50f);

        _logger.Received(2).LogDebug(Arg.Any<string>());
    }
```

Before running, confirm against the code you read: `ActiveBuffs` has a settable
`DamageReductionBonus` (used at `CareerAgentStatService.cs:224-226`), `CareerAbilityBuffTracker.SetAllyBuff(int, ActiveBuffs)`
exists (`Main/Features/CareerSystem/Abilities/CareerAbilityBuffTracker.cs:36`), and the reduction
subject expression is `victimHeroId ?? troopLeaderHeroId ?? victimAgentIndex?.ToString()` (:250), so
the first agent's line contains `'42'`.

Run: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CareerAgentStatServiceTests"`

**Verify**: exactly five of the six new tests fail, each with NSubstitute's `ReceivedCallsException`
reporting more matching `LogDebug` calls than expected:
`CalculateDamageAmplification_SameHeroAndTerms_LogsOncePerMission` (2 for 1),
`CalculateDamageAmplification_TroopDamageOnManyHits_ScalesEveryHitButLogsOnce` (50 for 1),
`CalculateDamageReduction_SameHeroAndTerms_LogsOncePerMission` (2 for 1),
`CalculateDamageReduction_AllyBuffOnTwoAgents_LogsOnce_NeverKeyedOnTheAgentIndex` (2 for 1, on its
`Arg.Any` assertion) and `CalculateDamageAmplification_NewMaskOrSubject_LogsAgain` (4 for 3).
`ResetDiagnostics_ClearsTheHitDedupe_SoTheNextBattleLogsAgain` passes at the base (there is no dedupe
to reset yet); Step 6 makes it fail before it adds the reset. Every pre-existing test in the class
passes. Quote the failing names and counts.

### Step 6 (Item B, GREEN): the dedupe, then commit

In `CareerAgentStatService.cs`:

1. Add, after `_logGate` (line 34):

```csharp
    // #613 per-hit evidence: one DEBUG line per mission for each (direction, subject, hit mask, terms that fired).
    // The subject is the hero or party-leader id only; an ally-buff-only victim is known by Agent.Index, which
    // recycles, so those hits share the null subject. Guarded by _logGate (the damage path can run off the main
    // thread); cleared by ResetDiagnostics at mission end.
    private readonly HashSet<(bool Reduction, string? Subject, AttackTypeMask Mask, int Terms)> _loggedHits = new();
```

   Do NOT touch `ResetDiagnostics` yet: sub-step 5 proves the reset test fails without the clear.

2. Add the private helpers (near `LogOnChange`):

```csharp
    private bool FirstHitOfItsKind(bool reduction, string? subject, AttackTypeMask hitMask, int terms)
    {
        lock (_logGate) return _loggedHits.Add((reduction, subject, hitMask, terms));
    }

    private static string AmpTerms(float armorPen, float damage, float troopDamage)
    {
        string? terms = null;
        if (armorPen != 0f) terms += " ArmorPenetration " + Pct(armorPen);
        if (damage != 0f) terms += " Damage " + Pct(damage);
        if (troopDamage != 0f) terms += " TroopDamage " + Pct(troopDamage);
        return terms!.TrimStart();
    }

    private static string ReductionTerms(float resistance, float selfReduction, float troopResistance, float allyReduction)
    {
        string? terms = null;
        if (resistance != 0f) terms += " Resistance " + Pct(resistance);
        if (selfReduction != 0f) terms += " self buff reduction " + Pct(selfReduction);
        if (troopResistance != 0f) terms += " TroopResistance " + Pct(troopResistance);
        if (allyReduction != 0f) terms += " ally buff reduction " + Pct(allyReduction);
        return terms!.TrimStart();
    }
```

3. `CalculateDamageAmplification`: keep every existing comment block; replace the `terms` string with
   three float locals declared at the top (`float armorPen = 0f, damage = 0f, troopDamage = 0f;`),
   assign them where `var armorPen`, `var damage` and `var troopDamage` are declared today, keep each
   `if (x != 0f) result *= (1f + x);` multiply, and replace lines 206-208 with:

```csharp
        // #613: the per-hit evidence, on the async DEBUG lane, once per battle for each subject, hit mask and set of
        // terms; a combination already logged formats nothing.
        int terms = (armorPen != 0f ? 1 : 0) | (damage != 0f ? 2 : 0) | (troopDamage != 0f ? 4 : 0);
        if (terms != 0 && _logger != null)
        {
            var subject = attackerHeroId ?? attackerTroopLeaderHeroId;
            if (FirstHitOfItsKind(reduction: false, subject, hitMask, terms))
                _logger.LogDebug($"[CareerPerks] hit amp for '{subject}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({AmpTerms(armorPen, damage, troopDamage)})");
        }
```

4. `CalculateDamageReduction`: the same shape with four locals
   (`float resistance = 0f, selfReduction = 0f, troopResistance = 0f, allyReduction = 0f;`). For the two
   buffs, read the bonus once into the local inside the existing null check, for example:

```csharp
            var heroBuff = CareerAbilityBuffTracker.GetBuff(victimHeroId!);
            if (heroBuff != null && heroBuff.DamageReductionBonus != 0f)
            {
                selfReduction = heroBuff.DamageReductionBonus;
                result *= (1f - selfReduction);
            }
```

   and replace lines 249-250 with:

```csharp
        int terms = (resistance != 0f ? 1 : 0) | (selfReduction != 0f ? 2 : 0)
            | (troopResistance != 0f ? 4 : 0) | (allyReduction != 0f ? 8 : 0);
        if (terms != 0 && _logger != null && FirstHitOfItsKind(reduction: true, victimHeroId ?? troopLeaderHeroId, hitMask, terms))
            _logger.LogDebug($"[CareerPerks] hit reduction for '{victimHeroId ?? troopLeaderHeroId ?? victimAgentIndex?.ToString()}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({ReductionTerms(resistance, selfReduction, troopResistance, allyReduction)})");
```

   The multiplication order, the gating conditions (`!string.IsNullOrEmpty(...)`,
   `victimAgentIndex.HasValue`) and the return value do not change. A NaN magnitude still sets its flag
   (`NaN != 0f` is true), as it appended its term before.

5. The reset's RED. Run the build, then
   `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ResetDiagnostics_ClearsTheHitDedupe_SoTheNextBattleLogsAgain"`.
   **Verify**: exactly that one test runs and fails with NSubstitute's `ReceivedCallsException`
   reporting fewer matching `LogDebug` calls than expected (1 for 2): the dedupe now holds the first
   hit across the reset. Quote the failure. A filter that runs no test proves nothing.

6. In `ResetDiagnostics`, add `_loggedHits.Clear();` inside the existing `lock (_logGate)` block,
   after `_lastMountLog.Clear();`.

7. Update the class comment at lines 26-29 to mention the hit set (one entry per combination per
   mission). In `ICareerAgentStatService.cs`, replace lines 73-74, which today read

```csharp
    /// <summary>Clears the <c>[CareerPerks]</c> dedupe state so the next mission's first stat
    /// application logs again. Called from the mission behavior's end-of-mission teardown (#613).</summary>
```

   with

```csharp
    /// <summary>Clears the <c>[CareerPerks]</c> dedupe state (the stat and mount lines and the
    /// per-hit lines) so the next mission logs again. Called from the mission behavior's
    /// end-of-mission teardown (#613).</summary>
```

8. Docs: in `docs/features/career-system.md:461`, replace "`hit amp` / `hit reduction` at DEBUG on
   every hit a passive moved, with the hit mask (`Melee, Blunt`), the base and the result and the
   terms" with "`hit amp` / `hit reduction` at DEBUG once per battle for each hero or party leader, hit
   mask (`Melee, Blunt`) and set of passives that moved the number, giving that first hit's base,
   result and terms (troops whose only term is an ally buff share one line per mask)". In
   `docs/features/dev-console.md:307`, replace "per-hit amplification and reduction at DEBUG" with
   "per-hit amplification and reduction at DEBUG, once per combination per battle".

**Verify**: build exit 0; the `CareerAgentStatServiceTests` filter reports `Passed!`, 0 failed, with
the six new tests among them. `git grep -n "terms +=" -- Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs`
lists only lines inside `AmpTerms` and `ReductionTerms`. Commit B (the five Item B paths); trailer
`Not-tested: the TaomAgentApplyDamageModel call path in a live battle`.

### Step 7 (Item C, RED): a troll latch

Create `TAOM.Tests/Features/TrollBruteForce/TrollPresenceTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce;

namespace TAOM.Tests.Features.TrollBruteForce;

// The troll mission behavior runs its formation-spacing and clip trackers (two agent scans twice a second) only once
// the mission has built a Brute Force troll. The latch decides that; a mission with no troll never sets it.
[TestClass]
public class TrollPresenceTests
{
    private TrollPresence _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new TrollPresence(new TrollBruteForceService());

    [TestMethod]
    public void Seen_BeforeAnyAgent_IsFalse()
    {
        Assert.IsFalse(_sut.Seen);
    }

    [TestMethod]
    public void Note_OtherMonsters_LeaveItUnset()
    {
        _sut.Note(null);
        _sut.Note("human");
        _sut.Note("cave_troll_settlement");

        Assert.IsFalse(_sut.Seen);
    }

    [TestMethod]
    public void Note_ABattleTroll_SetsIt_AndALaterHumanDoesNotClearIt()
    {
        _sut.Note("hill_troll");
        _sut.Note("human");

        Assert.IsTrue(_sut.Seen);
    }

    [TestMethod]
    public void Clear_AtMissionEnd_UnsetsIt()
    {
        _sut.Note("cave_troll");

        _sut.Clear();

        Assert.IsFalse(_sut.Seen);
    }
}
```

Check first that `cave_troll` and `hill_troll` are the keys of `TrollBruteForceConfig.ActionSetsByMonster`
and `cave_troll_settlement` is not (`TrollBruteForceServiceTests.cs:18-37` pins both).

Run: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TrollPresenceTests"`

**Verify**: the build fails with error `CS0246` naming `TrollPresence`, and no other error.

### Step 8 (Item C, GREEN): the latch and the gate, then commit

1. Create `Main/Features/TrollBruteForce/TrollPresence.cs`:

```csharp
namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Whether the current mission has built a Brute Force troll (a Monster <see cref="ITrollBruteForceService.IsBruteForceTroll"/>
/// accepts). <see cref="TrollBruteForceMissionBehavior"/> ticks its formation-spacing tracker and clip trace only once
/// this is set, so a mission with no troll pays for neither agent scan; both write nothing in such a mission anyway
/// (no formation reaches the troll share, no troll is listed). A latch: a troll's death does not clear it, only
/// <see cref="Clear"/> at mission end. Set from OnAgentBuild and the first tick, read on the main thread.
/// </summary>
public sealed class TrollPresence
{
    private readonly ITrollBruteForceService _service;
    private volatile bool _seen;

    public TrollPresence(ITrollBruteForceService service) => _service = service;

    public bool Seen => _seen;

    public void Note(string? monsterId)
    {
        if (!_seen && _service.IsBruteForceTroll(monsterId)) _seen = true;
    }

    public void Clear() => _seen = false;
}
```

2. `TrollBruteForceMissionBehavior.cs` (engine-bound; the `Not-tested:` line for commit C):
   - add the field `private readonly TrollPresence _presence;` beside `_clips`, and in the constructor
     `_presence = new TrollPresence(_service);` after `_clips` is built;
   - in `OnMissionTick`, inside the `if (!_treesAdded)` block, before `AttachAll`, note every agent
     already built (an agent built before this behavior was added never raised its `OnAgentBuild`):

```csharp
                foreach (Agent agent in Mission.Current.AllAgents)
                    _presence.Note(agent?.Monster?.StringId);
```

   - replace lines 87-88 with:

```csharp
            if (_presence.Seen)
            {
                _spacing.Tick(Mission.Current);
                _clips.Tick(Mission.Current);   // diagnostic last: a throw here must not skip the spacing
            }
```

   - in `OnAgentBuild`, after `base.OnAgentBuild(agent, banner);`, add
     `_presence.Note(agent?.Monster?.StringId);` (before the `_treesAdded` check, so a troll built
     before the first tick still counts);
   - in `OnRemoveBehavior`, add `_presence.Clear();` beside `_clips.Clear();`.

   `_tracker.PruneDead()` and the tree attach stay unconditional. The file must stay under 150 lines
   (`wc -l`).

3. Docs, `docs/features/troll-brute-force.md`:
   - lines 63-64 (the phrase wraps across them): after "`TrollFormationSpacingTracker` counts the
     formations twice a second on the main thread" insert ", from the first tick after the mission builds a troll (`TrollPresence`; a mission
     with no troll never scans)";
   - line 112 (the `TrollBruteForceMissionBehavior.cs` row): "ticks the spacing tracker and the clip
     trace" becomes "ticks the spacing tracker and the clip trace once the mission has built a troll
     (`TrollPresence`)";
   - line 113 (the `TrollClipTrace.cs` row): append "Like the spacing tracker, it runs only once the
     mission has built a troll.";
   - add a Key Files row after the `TrollFormationSpacingStore.cs` row:
     `| `Main/Features/TrollBruteForce/TrollPresence.cs` | The per-mission latch: set when a Brute Force troll is built, it gates the spacing tracker and the clip trace |`;
   - in the Tests section, add `TrollPresenceTests` (the latch: unset before any troll, set by either
     battle troll, kept after, cleared at mission end) to the list.

**Verify**: build exit 0; `TrollPresenceTests` filter `Passed!` 4 tests, 0 failed; the
`FullyQualifiedName~TrollBruteForce` filter `Passed!` with 0 failed (it includes the existing
`LiveInstall` wiring tests, which may report Inconclusive only if the Armory is missing: record that).
Commit C; trailer `Not-tested: the gate in TrollBruteForceMissionBehavior (engine Mission and Agent; the latch is unit tested)`.

### Step 9 (Item D, RED): the callback gate and the budget-first line

1. Append to `TAOM.Tests/Features/CreatureBandits/CreatureDiagLedgerTests.cs`, after
   `Reset_ClearsRecordsBudgetsAndSerials`:

```csharp
    [TestMethod]
    public void AnyRegistered_FalseUntilACreatureRegisters_AndAgainAfterReset()
    {
        // The diagnostics behavior's engine callbacks exit on this before any serial lookup, so a battle with no
        // creature pays one field read per hit, removal or alarm.
        var ledger = new CreatureDiagLedger(perCreatureCap: 5, missionCap: 100);
        Assert.IsFalse(ledger.AnyRegistered);

        ledger.Register("a", 0f, 1);
        Assert.IsTrue(ledger.AnyRegistered);

        ledger.Reset();
        Assert.IsFalse(ledger.AnyRegistered);
    }
```

2. Create `TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CreatureBandits;
using TAOM.Features.CreatureBandits.Diagnostics;

namespace TAOM.Tests.Features.CreatureBandits;

// Creature Bandits diagnostics (#692): a caller takes a line from the budget BEFORE it formats the line, so a
// suppressed line costs no string. TakeLine keeps Write's old rules: no logger spends nothing, the first refusal at
// the mission cap writes the one cap WARNING.
[TestClass]
[TestCategory("RequiresGame")]   // CreatureBanditDiag's static Serials map is keyed on the engine's Agent type
public class CreatureBanditDiagTests
{
    private IModLogger? _saved;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _saved = CreatureBanditLog.Logger;
        CreatureBanditDiag.ResetForMission();
        _logger = Substitute.For<IModLogger>();
        CreatureBanditLog.Logger = _logger;
    }

    [TestCleanup]
    public void Teardown()
    {
        CreatureBanditDiag.ResetForMission();
        CreatureBanditLog.Logger = _saved;
    }

    [TestMethod]
    public void TakeLine_NoLogger_ReturnsFalse_AndSpendsNoBudget()
    {
        CreatureBanditLog.Logger = null;

        Assert.IsFalse(CreatureBanditDiag.TakeLine(0, "snap"));
        Assert.AreEqual(0, CreatureBanditDiag.Ledger.MissionLines);
    }

    [TestMethod]
    public void TakeLine_WithinBudget_ReturnsTrue_SpendsOneLine_AndWritesNothingItself()
    {
        Assert.IsTrue(CreatureBanditDiag.TakeLine(0, "snap"));

        Assert.AreEqual(1, CreatureBanditDiag.Ledger.MissionLines);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void TakeLine_PastTheMissionCap_WritesOneCapWarning_ThenRefuses()
    {
        for (int i = 0; i < CreatureBanditDiag.MissionCap; i++)
            Assert.IsTrue(CreatureBanditDiag.TakeLine(0, "snap"));

        Assert.IsFalse(CreatureBanditDiag.TakeLine(0, "snap"));
        Assert.IsFalse(CreatureBanditDiag.TakeLine(0, "snap"));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("missionCap")));
    }

    [TestMethod]
    public void Emit_RoutesByTheWarningFlag()
    {
        CreatureBanditDiag.Emit("info line");
        CreatureBanditDiag.Emit("warning line", warning: true);

        _logger.Received(1).LogInfo("info line");
        _logger.Received(1).LogWarning("warning line");
    }
}
```

   It needs `using System.Linq;` for `.Count()`; add it. Check `CreatureBanditDiag.MissionCap` is
   `internal const int MissionCap = 1500;` (`CreatureBanditDiag.cs:32`) and `"snap"` is not an outcome
   kind (`CreatureDiagLedger.IsOutcomeKind`, :151).

Run the two filters `FullyQualifiedName~CreatureDiagLedgerTests` and
`FullyQualifiedName~CreatureBanditDiagTests`.

**Verify**: the build fails with `CS1061` naming `AnyRegistered` on `CreatureDiagLedger`, and `CS0117`
naming `TakeLine` and `Emit` on `CreatureBanditDiag`, and no other error.

### Step 10 (Item D, GREEN): `AnyRegistered`, `TakeLine`, `Emit` and the callback gate

1. `CreatureDiagLedger.cs`: add `using System.Threading;` and, after `Count` (:107):

```csharp
    /// <summary>Any thread: whether this mission has registered a creature yet. The diagnostics behavior's callbacks
    /// exit on false before any serial lookup.</summary>
    internal bool AnyRegistered => Volatile.Read(ref _nextSerial) > 0;
```

2. `CreatureBanditDiag.cs`: add `TakeLine` and `Emit` directly above `Write` (do not delete `Write`
   yet, so the tree builds until Step 11 converts its callers):

```csharp
    /// <summary>
    /// Main thread: takes one line of <paramref name="kind"/> from the budget BEFORE the caller formats it. False with
    /// no logger (the budget is untouched, as Write did) or when the budget refuses; the refusal that first reaches
    /// the mission cap writes the one cap WARNING here. Format the line only on true, then pass it to <see cref="Emit"/>.
    /// </summary>
    internal static bool TakeLine(int serial, string kind)
    {
        var logger = Logger;
        if (logger == null) return false;
        switch (Ledger.TryTakeLine(serial, kind))
        {
            case LineVerdict.Write:
                return true;
            case LineVerdict.CapReached:
                logger.LogWarning(CreatureDiagFormat.Line("cap", 0f, 0, "missionCap", CreatureDiagFormat.I(MissionCap),
                    "note", "further diag lines this mission are counted, not written"));
                return false;
            default:
                return false;
        }
    }

    /// <summary>Main thread: writes a line <see cref="TakeLine"/> granted.</summary>
    internal static void Emit(string line, bool warning = false)
    {
        var logger = Logger;
        if (logger == null) return;
        if (warning) logger.LogWarning(line); else logger.LogInfo(line);
    }
```

3. `CreatureBanditDiagnosticsBehavior.cs`: make the FIRST statement of each of the seven callbacks
   (`OnAgentHit`, `OnAgentRemoved`, `OnAgentDeleted`, `OnAgentPanicked`, `OnAgentFleeing`,
   `OnAgentMount`, `OnAgentAlarmedStateChanged`):

```csharp
        if (!CreatureBanditDiag.Ledger.AnyRegistered) return;
```

   and change the class comment (:8-9) from "Every callback's first statement is a serial lookup (a
   small concurrent map keyed by agent reference), so battles without creatures pay one lookup per
   event;" to "Every callback first reads whether the mission has registered a creature
   (`CreatureDiagLedger.AnyRegistered`), so a battle without creatures pays one field read per event,
   then looks up serials (a small concurrent map keyed by agent reference);". `OnCreated`,
   `OnMissionTick`, `OnMissionResultReady` and `OnRemoveBehavior` do not change. This early return is
   exact: with no creature registered no agent has a serial, so every lookup it skips returned 0 and the
   callback returned (`OnAgentHit` and `OnAgentRemoved` at `victim == 0 && ... == 0`, the others at
   `serial == 0` or `serial > 0`).

**Verify**: build exit 0; both Step 9 filters `Passed!`, 0 failed (5 new tests, plus the existing
ledger tests).

### Step 11 (Item D): convert the 22 writers to budget-first, delete `Write`, then commit

For every one of the 22 call sites listed in Current state, rewrite

```csharp
CreatureBanditDiag.Write(serial, "kind", Line(...), warning: X);   // or Write(...) inside CreatureBanditDiag.cs
```

as

```csharp
if (CreatureBanditDiag.TakeLine(serial, "kind"))
    CreatureBanditDiag.Emit(Line(...), warning: X);
```

keeping the `Line(...)` arguments, the kind string and the `warning` flag exactly as they are
(inside `CreatureBanditDiag.cs` call `TakeLine` and `Emit` unqualified, with `CreatureDiagFormat.Line`
as today). Inside the `WriteEvent` switch the `if` goes before each case's `break;`. Rules:

- Leave every statement BEFORE the old `Write` call where it is (for example `record.LastTargetKey = key;`,
  `record.TargetChanges++;`, `record.FirstTargetedLogged = true;`, `++_formationLines`): those are state
  updates and counters that must still run when the budget refuses the line.
- Before converting a site, read its `Line(...)` arguments: if any argument mutates state (an `++`, an
  assignment, a call to a `Note*` or `Set*` method) or reads the ledger's budget, STOP and report the
  site, except the one known case below.
- The snap line (`CreatureBanditDiagTicker.cs:376-380`) reports the budget it is about to spend.
  Capture it first so the printed count is unchanged:

```csharp
        int linesBefore = CreatureBanditDiag.Ledger.MissionLines;   // the count this line reported when it was formatted before the budget check
        if (CreatureBanditDiag.TakeLine(0, "snap"))
            CreatureBanditDiag.Emit(Line("snap", now, 0, "alive", I(alive), "known", I(CreatureBanditDiag.Ledger.Count),
                "targetedByNow", I(targetedNow), "aimingSoldiers", I(CreatureBanditDiag.SoldiersAimingAtCreatures.Count),
                "dmgDealt", I(dealt), "dmgTaken", I(taken),
                "queue", I(CreatureBanditDiag.Events.Count), "lines", I(linesBefore),
                "suppressed", I(CreatureBanditDiag.Ledger.MissionSuppressed)));
```

  (`MissionSuppressed` only changes on a refusal, when nothing is written, so it needs no capture.)
- Not a budget read: the formation line (`CreatureBanditDiagTicker.cs:516-520`) prints
  `"lines", I(_formationLines) + "/" + I(FormationLineCap)`. `_formationLines` is the ticker's own
  formation counter, incremented at :515 before the call (that `++` stays where it is), not the
  ledger budget, so it moves inside the `if` unchanged; it is no reason to STOP.

Then delete `Write` (`CreatureBanditDiag.cs:91-106`).

Docs, `docs/features/creature-bandits.md`: change line 271 "The diagnostics are budgeted (see above)
and write nothing per frame." to "The diagnostics are budgeted (see above), take a line from the
budget before formatting it (`CreatureBanditDiag.TakeLine`, then `Emit`), and write nothing per frame;
in a battle with no creature every engine callback exits on one field read
(`CreatureDiagLedger.AnyRegistered`)." Check the "Stripping the Diagnostics" section (from line 328):
if it names `CreatureBanditDiag.Write`, rename it to `TakeLine`/`Emit`; it lists call sites outside
the Diagnostics folder, and this plan adds none. In the Tests section, the bullet at lines 239-240
(`CreatureDiagFormatTests.cs` and `CreatureDiagLedgerTests.cs`): after "the line budget and its
exemptions," insert " `AnyRegistered` (the callbacks' no-creature gate),"; then add a bullet after it:
"- `TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs` (`RequiresGame`): `TakeLine` takes
the budget before a line is formatted (no logger, within budget, the one cap WARNING) and `Emit`
routes by level."

**Verify**:
- `git grep -n "CreatureBanditDiag.Write(" -- Main TAOM.Tests` returns nothing, and
  `git grep -nE "^[[:space:]]*Write\(" -- Main/Features/CreatureBandits` returns nothing.
- `git grep -cE "CreatureBanditDiag\.TakeLine\(|if \(TakeLine\(" -- Main/Features/CreatureBandits` prints
  17 for `CreatureBanditDiagTicker.cs` and 5 for `CreatureBanditDiag.cs` (22 in all).
- Build exit 0; the `FullyQualifiedName~CreatureBandit` and `FullyQualifiedName~CreatureDiag` filters
  `Passed!` with 0 failed (record any `LiveInstall` Inconclusive).
- Commit D (the seven Item D paths); body notes the snap line's count is captured before the budget
  check so it prints as before; trailer
  `Not-tested: the seven early returns in CreatureBanditDiagnosticsBehavior and the 22 call sites (engine callbacks and Mission reads; TakeLine, Emit and AnyRegistered are unit tested)`.

### Step 12 (Item E): delete the empty postfix, then commit

A pure deletion: its RED is the evidence in your report, not a permanent test.

1. Prove nothing else reads it. Run `git grep -n "Patch35_Mission_OnTick" -- Main TAOM.Tests` and
   quote the output: expected exactly one line, the class declaration in
   `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs`. Run
   `git grep -n "HarmonyPatchCategory(\"Patch35_CompanionTactics\")" -- Main` and confirm at least six
   other files still carry the category. If either differs, STOP.
2. Delete `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs`.
3. Docs:
   - `docs/features/companion-tactics.md`: delete the table row at line 149 (the
     `Patch35_Mission_OnTick.cs` "HOT PATH" row) and the Performance bullet at line 195.
   - `docs/reference/harmony-patch-registry.md`: line 271, delete the text
     `` , `Mission.OnTick(float,float,bool,bool)` (Postfix) `` from the Target list; line 273, delete
     the final FormationPresets clause beginning "; the `Mission.OnTick` Postfix is a near-no-op hot
     path" up to (not including) ". See `docs/features/companion-tactics.md`."; line 1057, delete the
     sentence "Composes with `Patch35_Mission_OnTick` (postfix on `Mission.OnTick`, which runs inside
     the frame probe) and Patch37's finalizer on `Mission.Tick`." and replace it with "Composes with
     Patch37's finalizer on `Mission.Tick`."
   - `docs/reference/taleworlds-api-snapshot/patch-targets.md`: delete the
     `TAOM.Features.CompanionTactics.FormationPresets.Hooks.Patch35_Mission_OnTick` row (line 94), and
     change `Patches: 268.` to `Patches: 267.` Check first that
     `grep -c '^| `TAOM' docs/reference/taleworlds-api-snapshot/patch-targets.md` printed 268 before the
     edit and prints 267 after.

**Verify**: build exit 0; `git grep -n "Patch35_Mission_OnTick" -- Main TAOM.Tests docs/features docs/reference`
returns nothing; the `FullyQualifiedName~CompanionTactics` filter `Passed!` with 0 failed. Commit E;
body: the postfix's body was empty since the preset hotkeys moved to buttons, and it still cost a
Harmony detour, a settings read and a PatchShield finalizer on every mission frame. Trailer
`Not-tested: Harmony apply of Patch35_CompanionTactics without the class (in game)`.

### Step 13 (Item F): delete the colour store, then commit

A deletion: its RED is the proof in your report.

1. Prove nothing reads the store: `git grep -n "TryGetColors" -- Main TAOM.Tests` must list only
   `AgentColorStore.cs`, `IAgentColorStore.cs` and `AgentColorStoreTests.cs`; and
   `git grep -n "AgentColorStore" -- Main TAOM.Tests` (it also matches `IAgentColorStore`) must list
   only the files named in Current state "Item F". Re-read `Agent_EquipItemsFromSpawnEquipment_Patch.cs` and
   `Mission_SpawnAgent_Patch.cs`: if the EquipItems prefix or the SpawnAgent postfix does anything
   besides computing `info` and calling `_colorStore?.Register(...)`, STOP. If anything reads the store,
   STOP.
2. Delete `AgentColorStore.cs`, `IAgentColorStore.cs`, `AgentColorStoreCleanupBehavior.cs`,
   `Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs` (all under
   `Main/Features/BannerColorPersistence/`) and `TAOM.Tests/Features/BannerColorPersistence/AgentColorStoreTests.cs`.
3. `Mission_SpawnAgent_Patch.cs`: delete the `_colorStore` field (:15) and the whole `[HarmonyPostfix]`
   method (:68-78); the initializer becomes:

```csharp
    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }
```

   `ResolveColors` and the `[HarmonyPrefix]` stay byte-identical.
4. `BannerColorPersistenceIoC.cs`: delete line 13 (`container.Register<IAgentColorStore, AgentColorStore>(Reuse.Singleton);`).
5. `Main/SubModule.cs` (single-owner; these edits and no others):
   - delete line 635 `var agentColorStore = IoC.Resolve<IAgentColorStore>();`;
   - line 636 becomes `Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter);`;
   - delete line 637 `Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);`;
   - delete lines 2115-2117 (`var colorStore = IoC.Resolve<IAgentColorStore>();`, `if (colorStore != null)`,
     `AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));`) and the blank line after them,
     so `AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());`
     is followed by one blank line and the `// Feature modules' mission behaviors:` comment.
   - Leave the `using TAOM.Features.BannerColorPersistence;` lines (:65-66) unless the build reports
     them as errors (it will not: an unused using is not a compiler error).
6. `TAOM.Tests/Composition/FeatureModulesTests.cs:169`: change the anchor string
   `"new AgentColorStoreCleanupBehavior(colorStore)"` to
   `"AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());"`.
   Nothing else in that test changes: it still asserts the runner call sits after the last kernel
   feature behavior and before the MissionDiagnostic registration.
7. Docs:
   - `docs/features/banner-color-persistence.md`: lines 26-27, replace "`Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs`
     covers hero agents only; the `SpawnAgent` prefix is what covers ordinary troops." with "The
     `SpawnAgent` prefix is the only spawn-time recolour (an agent whose caller already chose its
     equipment keeps the colours it was given)."; delete the table rows for `IAgentColorStore.cs` /
     `AgentColorStore.cs` (:104), `AgentColorStoreCleanupBehavior.cs` (:105) and
     `Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs` (:112); `BannerColorPersistenceIoC.cs`
     "Registers all 4 singletons" becomes "Registers all 3 singletons"; the tests row "6 test files, 33
     test methods" becomes "5 test files, 29 test methods" (recount with
     `grep -c "\[TestMethod\]\|\[DataTestMethod\]" TAOM.Tests/Features/BannerColorPersistence/*.cs`
     after the deletion and use the real numbers); delete the `AgentColorStoreTests.cs` row (:134).
     The sentence at :140, "6 files, 33 `[TestMethod]` as of 2026-09-02.", becomes "5 files, 29
     `[TestMethod]` as of <the date you commit>." with the same recounted numbers. Leave the dated
     history line (:167) as it is.
   - `docs/reference/harmony-patch-registry.md`: line 172, delete `` `Agent.EquipItemsFromSpawnEquipment`, ``
     from the Patch23 Target list; line 325, change "Phases coexist with the pre-existing Patch16 /
     Patch23 hooks on the shared targets." to "Phases coexist with the pre-existing Patch16 hook on the
     shared target (`Mission.Initialize`)." (Patch23 no longer patches `Agent.EquipItemsFromSpawnEquipment`;
     confirm Patch16's only target is `Mission.Initialize` at registry line 118 first).
   - `docs/reference/taleworlds-api-snapshot/patch-targets.md`: delete the
     `TAOM.Features.BannerColorPersistence.Hooks.Agent_EquipItemsFromSpawnEquipment_Patch` row (:25)
     and change `Patches: 267.` to `Patches: 266.`; the row count must then be 266. Keep the
     `Mission_SpawnAgent_Patch` row (the class still patches `Mission.SpawnAgent`).

**Verify**:
- Build exit 0.
- `git grep -nE "AgentColorStore|TryGetColors|Agent_EquipItemsFromSpawnEquipment_Patch" -- Main TAOM.Tests docs/features docs/reference`
  returns exactly one line: the dated 2026-04-05 history entry in
  `docs/features/banner-color-persistence.md` (kept as history). The battle-load class
  `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch` does not contain that substring; check it is
  still present with `git grep -c Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch -- Main`.
- The filters `FullyQualifiedName~FeatureModulesTests`, `FullyQualifiedName~BannerColorPersistence`
  and `FullyQualifiedName~CoopVetoClassificationTests` report `Passed!`, 0 failed.
- `git diff <START> -- Main/SubModule.cs` shows exactly the four edits above.
- Commit F; body: the store was filled on every spawn, keyed on the engine's recycled `Agent.Index`,
  and never read; the EquipItems prefix and the SpawnAgent postfix existed only to fill it; troop
  colours come from the unchanged SpawnAgent prefix. Trailer
  `Not-tested: Harmony apply of Patch23 without the deleted members and armour tint in a battle (in game)`.

### Step 14: the full suite and the docs

```
dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
python tools/lint_docs.py --fail-on-drift; echo "lint exit=$?"
```

Expected totals: the base's 12348 minus the 4 deleted `AgentColorStoreTests` plus the 19 new tests
(4 `MissionDiagnosticServiceTests`, 6 in `CareerAgentStatServiceTests`, 4 `TrollPresenceTests`, 1 in
`CreatureDiagLedgerTests`, 4 `CreatureBanditDiagTests`) = `Total: 12363`, `Failed: 1`,
`Passed: 12360`, `Skipped: 2`, the one failure still `EveryLanguage_DeclaresARowForEveryEnglishKey`.
If you wrote a different number of tests than this plan, the arithmetic must still balance; explain
it.

The new tests not tagged `RequiresGame` (`MissionDiagnosticServiceTests`, `TrollPresenceTests`,
`AnyRegistered_*`) must also pass on hosted CI's reference assemblies. Run the CI unit step for them,
from your worktree, with the game paths unset:

```
env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR TEMP="<tmp>" TMP="<tmp>" dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=
env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "(FullyQualifiedName~MissionDiagnosticServiceTests|FullyQualifiedName~TrollPresenceTests|FullyQualifiedName~CreatureDiagLedgerTests)&TestCategory!=RequiresGame"
```

Expected `Passed!`, 0 failed. If a test there fails with a type-load error from an engine assembly,
tag that test class `[TestCategory("RequiresGame")]` with a one-line comment naming the engine type,
amend nothing: make it a new commit (`test(...)`), and report it. If the RefAsm restore cannot
download the reference assemblies (network or feed), record "RefAsm step not run: <error>" and
continue: it is an environment gap, not a STOP. Afterwards rebuild normally
(`dotnet build Main/TAOM.csproj ...` then the full `dotnet test`) so the last totals you quote come from
a normal build.

**Verify**: the totals line matches the arithmetic; the lint exit code and findings match Step 1's
(no new finding in a file this plan touched); `git status --porcelain` prints exactly the Step 1
status (everything of yours is committed); `git log --oneline <START>..HEAD` shows the six commits
(seven if Step 14 added a tag), all yours.

## Test plan

- **New tests**
  - `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticServiceTests.cs` (4): the integer key's first
    and repeat sighting, each key part, the reset, and a characterization pin of the census line text.
  - `TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs` (+6): once per combination for
    amplification and reduction, TroopDamage across 50 hits (every hit still scaled), the
    ally-buff-only subject not keyed on the agent index, a new mask or subject logging again, and the
    reset.
  - `TAOM.Tests/Features/TrollBruteForce/TrollPresenceTests.cs` (4): unset at start, unset by other
    Monsters, set by a battle troll and kept, cleared.
  - `TAOM.Tests/Features/CreatureBandits/CreatureDiagLedgerTests.cs` (+1): `AnyRegistered` across
    register and reset.
  - `TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs` (4, `RequiresGame`): `TakeLine` with
    no logger, within budget, past the mission cap; `Emit` routing.
- **Patterns**: model on `CareerAgentStatServiceTests.cs` (NSubstitute logger, `Received(n)`) and
  `CreatureDiagLedgerTests.cs` (plain construction, no engine types).
- **Deletions** (Items E and F) prove RED in the report with the Step 12 and Step 13 `git grep` output
  and the green build after; no permanent "absent" test (`AgentColorStoreTests.cs` goes with the code
  it tested).
- **Not unit-testable (engine-bound lines only, for the `Not-tested:` trailers)**: the agent loop in
  `MissionDiagnosticBehavior` (reads `Agent.ActionSet`, `Agent.Name`); the gate lines in
  `TrollBruteForceMissionBehavior` (`Mission.AllAgents`, `OnAgentBuild`); the seven early returns in
  `CreatureBanditDiagnosticsBehavior` and the 22 rewritten call sites (their `Line(...)` arguments read
  `Agent` and `Mission`); the Harmony apply of `Patch35_CompanionTactics` and
  `Patch23_BannerColorPersistence` after the deletions. Every decision those lines delegate to is
  unit tested above.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` reports `Total: 12363`,
      `Failed: 1` (`EveryLanguage_DeclaresARowForEveryEnglishKey` only), or totals whose difference
      from that is explained test by test in the report.
- [ ] `git grep -nE "Patch35_Mission_OnTick|AgentColorStore|TryGetColors|Agent_EquipItemsFromSpawnEquipment_Patch|CreatureBanditDiag\.Write\(" -- . ':!docs/archive' ':!docs/audits' ':!docs/reviews' ':!docs/changelog-archive' ':!plans'`
      returns exactly one line: the dated 2026-04-05 history entry in
      `docs/features/banner-color-persistence.md`.
- [ ] `git grep -n "_seenActionSetKeys" -- Main` shows the field, the `Add` and the `Clear`;
      `git grep -n "TryMarkActionSetKey" -- Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`
      shows exactly one call, on a line before the loop's `actionSet.GetName()`.
- [ ] `git grep -n "terms +=" -- Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs` lists
      only lines inside `AmpTerms` and `ReductionTerms`.
- [ ] `git grep -n "_presence" -- Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs` shows
      the field, the constructor, the first-tick note, the `if (_presence.Seen)` gate, the
      `OnAgentBuild` note and the `Clear`.
- [ ] `git grep -c "CreatureBanditDiag.Ledger.AnyRegistered" -- Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagnosticsBehavior.cs`
      prints 7.
- [ ] `grep -c '^| `TAOM' docs/reference/taleworlds-api-snapshot/patch-targets.md` prints 266 and the
      header says `Patches: 266.`
- [ ] `wc -l` on `MissionDiagnosticBehavior.cs`, `TrollBruteForceMissionBehavior.cs` and
      `CreatureBanditDiagnosticsBehavior.cs` each prints under 150.
- [ ] `git diff --stat <START>..HEAD` lists only in-scope files; `git log --oneline <START>..HEAD`
      shows only this plan's commits; `git status --porcelain` prints exactly the Step 1 status.
- [ ] `python tools/lint_docs.py --fail-on-drift` matches its Step 1 exit code and findings.
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (say so per item in the report).

## STOP conditions

Stop and report (do not improvise) if:

- The code at any "Current state" location does not match its excerpt, or the drift check prints a file.
- A commit between `dffdf879` and `<START>` touches any drift-check path (the drift check prints a
  file), or a commit you did not make appears in `<START>..HEAD` while you work. Commits past
  `dffdf879` that touch only other paths (the program branch's docs and plans commits) are expected.
- Step 1's failure set differs from the baseline's.
- A RED step fails for a reason other than the one named (another compile error, a different assertion),
  or a GREEN step's verification fails twice after a reasonable fix.
- `MBActionSet.GetHashCode()` in the installed engine does not return its index (re-check with
  `pwsh tools/taom-src.ps1 path MBActionSet` if the build's engine version differs from v1.5.3): the
  census pre-filter's identity rests on it.
- Anything reads `AgentColorStore` or `IAgentColorStore`, or the EquipItems prefix or the SpawnAgent
  postfix does anything besides filling the store (Step 13.1).
- `git grep` finds a reader of `Patch35_Mission_OnTick` besides its own file, or fewer than six other
  classes keep the `Patch35_CompanionTactics` category (Step 12.1).
- A creature-bandit call site's `Line(...)` arguments mutate state or read the budget, other than the
  snap line (Step 11).
- The work seems to need `Main/IoC.cs`, `Main/TAOM.csproj`, a protected file, an MCM setting, a log
  level change, or a `Main/SubModule.cs` edit beyond Step 13.5.
- A behavior file would reach 150 lines.

## Orchestrator steps (not the executor's)

- Issue: draft it (perf: always-on mission diagnostics' per-frame, per-agent and per-hit cost) and file
  it on the maintainer's word, before the branch lands on a trunk. If it is filed before dispatch, give
  its number to the executor for the commit bodies; otherwise the bodies carry none.
- `/localize`: none; no player-facing text changes (every changed string is a debug-log line or a doc).
- Feature docs: updated in place by the executor (`mission-diagnostic.md`, `career-system.md`,
  `dev-console.md`, `troll-brute-force.md`, `creature-bandits.md`, `companion-tactics.md`,
  `banner-color-persistence.md`); no new feature, so no new `docs/features/` file or feature-map row.
- `/deep-review` over the six commits before merge; the binding gate (`/verify-bindings`) after merge,
  since two Harmony patch members are gone.
- After merge, label the issue `triage-needs-ingame` for the in-game checks below.

## After merge: the maintainer's actions

- Pull. No hook, setting or MCP change, so no session restart.
- In-game checks (one battle each): a campaign field battle with clan-coloured troops still shows clan
  armour tint (Patch23's prefix); a battle with a cave or hill troll logs `[TrollSpacing]` lines as
  before and a troll-less battle logs none; a battle with a career hero holding a TroopDamage pip logs
  one `hit amp` DEBUG line per party leader and mask, not one per hit; the first 5 s of a mission log
  the same `[MissionDiag] ActionSet` lines as before; the companion-tactics formation-preset buttons
  still work.

## Maintenance notes

- **Census key**: if a future change adds the monster id (or anything else) to the census line's key,
  add it to `TryMarkActionSetKey` too, or the pre-filter will hide a line. The pre-filter's identity
  rests on `MBActionSet.GetHashCode()` returning the engine index; re-check it on every engine bump.
- **Career hit lines**: magnitudes are not in the dedupe key. If a future buff can change its
  `DamageReductionBonus` mid-battle and the per-hit evidence must show it, add the rounded magnitude to
  the key rather than going back to per-hit lines. The dedupe resets only at the end of a campaign
  mission (`CareerPerkMissionBehavior` is campaign-only), which is the only place the damage model runs.
- **Troll latch**: a new troll Monster joins the gate automatically through
  `TrollBruteForceConfig.ActionSetsByMonster`. When the maintainer deletes `TrollClipTrace`, the gate
  still guards the spacing tracker.
- **Creature diagnostics**: a new diagnostic line must use `TakeLine` then `Emit`; there is no
  one-call writer any more. When the Diagnostics folder is stripped, `AnyRegistered` goes with the
  ledger.
- **Review probes**: the snap line's `lines=` value (captured before the budget check); the seven
  early returns (each must be the first statement); `FirstHitOfItsKind` under `_logGate`; the
  `FeatureModulesTests` anchor; the two `Patches:` count edits.
- **Deferred**: `CareerPerkConsumerMap` still says "hit lines (DEBUG)", which stays true; the
  maintainer may want "first hit per combination" there (a console string, out of scope). The INFO
  lines and `EnableHowdahDiagnostics` named in Scope go to the maintainer.

## Amendment (orchestrator, 2026-10-02 night; binding)

The maintainer's instruction (DECISIONS D6): taom_debug.log is a critical log; cost reductions keep every
piece of information. Item B (the per-hit `[CareerPerks]` lines) therefore changes from "once per
(hero, passive kind) per mission" to: the first occurrence of each (hero or troop leader, passive kind)
per mission logged in full as today, PLUS a per-mission aggregate written at mission end (one INFO line
per combination seen, or a short block): count of hits affected, and the minimum, average and maximum
multiplier or amount applied, so nothing the per-hit lines showed is lost. No string is built per hit;
the aggregate keeps numbers only, in a small per-mission table owned by the service (thread-safe: these
calculations run on whichever thread native raises the hit callback). Every other item already keeps
its lines identical; state that in the commit body item by item.
