using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Features.Warg;

namespace TAOM.Tests.Features.FordsOfWar.Scenarios;

internal sealed class ScenarioLogger : IModLogger
{
    public List<string> Lines { get; } = new();

    public string? LogFilePath => null;

    public void LogInfo(string message) => Lines.Add("INFO " + message);

    public void LogDebug(string message) => Lines.Add("DEBUG " + message);

    public void LogWarning(string message) => Lines.Add("WARN " + message);

    public void LogError(string message) => Lines.Add("ERROR " + message);

    public void Dispose()
    {
    }
}

internal sealed class ScenarioFactory : IFordsOfWarGameManagerFactory
{
    public bool ReturnsNull;
    public FordsMatchSetup? LastSetup;

    public MBGameManager? Create(FordsMatchSetup setup)
    {
        LastSetup = setup;
        return ReturnsNull ? null : StubGameManager.Create();
    }
}

/// <summary>
/// The scenario harness (the 2c root-cause prevention): the real run report, mission director, match, layout generator, spawn
/// placement, field probe, mission exit with its clock, camera math with the probe, setup prompt and warg rage log, over faithful
/// fakes of the three engine adapters and a fake wall clock. A frame calls what the engine calls, in its order: the telemetry
/// logic's tick (mission clock, 30 second window, 1 Hz warg watch), the mission logic's tick (director, exit), the commander camera
/// and the pause flag, then the engine's own step (units walk and fight, volleys land). The ends of a run, the winner, the result,
/// the close and the menu return, are methods the scenario calls when the engine would.
/// </summary>
internal sealed class FordsScenario
{
    internal const string Gondor = "gondor";
    internal const string Mordor = "mordor";
    internal const string HeroA = "hero_a";
    internal const string HeroB = "hero_b";
    internal const string HeroC = "hero_c";
    internal const string Inf = "inf";
    internal const string Archers = "archers";

    private const string Factions = @"{
  'factions': {
    'gondor': {
      'name': 'Gondor', 'culture': 'gondor',
      'units': [
        { 'id': 'inf', 'troop': 'footman', 'role': 'Infantry', 'packSize': 10, 'price': 100 },
        { 'id': 'archers', 'troop': 'archer', 'role': 'Ranged', 'packSize': 10, 'price': 150 }
      ],
      'heroes': [ { 'id': 'hero_a', 'price': 700 }, { 'id': 'hero_b', 'price': 400 }, { 'id': 'hero_c', 'price': 300 } ],
      'freeWaves': [ { 'fromMinute': 0, 'units': [ { 'troop': 'footman', 'count': 5 } ] } ]
    },
    'mordor': {
      'name': 'Mordor', 'culture': 'mordor',
      'units': [
        { 'id': 'inf', 'troop': 'grunt', 'role': 'Infantry', 'packSize': 10, 'price': 100 },
        { 'id': 'archers', 'troop': 'orc_archer', 'role': 'Ranged', 'packSize': 10, 'price': 150 }
      ],
      'heroes': [ { 'id': 'hero_a', 'price': 700 }, { 'id': 'hero_b', 'price': 400 }, { 'id': 'hero_c', 'price': 300 } ],
      'freeWaves': [ { 'fromMinute': 0, 'units': [ { 'troop': 'grunt', 'count': 5 } ] } ]
    }
  }
}";

    private double _wall;
    private double _wargAt;
    private bool _firstFrame = true;
    private bool _resultSent;

    public readonly ScenarioLogger Logger = new();
    public readonly FakeFordsMenu Menu = new();
    public readonly ScenarioFactory Factory = new();
    public readonly FordsSession Session = new();
    public readonly FaithfulBattlefield Field = new();
    public readonly FaithfulTowers Towers;
    public readonly FaithfulScene Scene = new();
    public readonly List<FaithfulRider> Riders = new();
    public readonly FordsRunReport Report;
    public readonly FordsConfigCatalog Catalog;
    public readonly FordsSetupPrompt Prompt;
    public readonly FordsMenuEntry Entry;

    public Func<FordsMatchConfig, FordsMatchConfig>? ConfigTweak;
    public FordsMatchPlan? Plan;
    public FordsMissionDirector? Director;
    public FordsMissionExit? Exit;
    public FordsCameraProbe? Probe;
    public FordsCameraState Camera;
    public FordsCameraInput? CameraInput;
    public WargRageLog? Warg;
    public FordsRunEvidence? Subject;
    public bool Failed;
    public bool Paused;
    public bool WrongResult;
    public bool FeedSilent;
    public bool CameraDead;
    public bool CameraUnclamped;
    public bool VolleysNeverHit;
    public int MaxAgents = 4096;
    public int EndGamesBefore;
    public readonly Dictionary<string, string> Marks = new();

    public FordsScenario(string? factions = null, string? maps = null)
    {
        Towers = new FaithfulTowers(Field);
        Report = new FordsRunReport(Logger, () => _wall);
        Catalog = FordsConfigCatalog.FromTexts(null, factions ?? Factions, maps ?? FordsConfigFixtures.Maps, Logger);
        Prompt = new FordsSetupPrompt(Catalog, Menu, Factory, Logger, Report);
        Entry = new FordsMenuEntry(Menu, Catalog, Prompt, Report);
        Field.OnRemoved = (affected, affector, killed) => Director?.OnAgentRemoved(affected, affector, killed);
        Field.OnTowerStruck = (towerId, attacker, raw) => StrikeTower(towerId, attacker, ranged: false, raw);
        Field.TowerPosition = Towers.PositionOf;
    }

    public double Wall => _wall;

    public double Mt => Report.MissionSeconds;

    public MatchView View => Session.GetView()!;

    public IReadOnlyList<FordsVerdictRow> Rows => FordsVerdicts.Judge(Report.Session, Subject ?? Report.Run ?? new FordsRunEvidence());

    public FordsResult Result(int check, string part) =>
        Rows.Single(r => r.Check == check).Parts.Single(p => p.Name == part).Result;

    public string Numbers(int check, string part) =>
        Rows.Single(r => r.Check == check).Parts.Single(p => p.Name == part).Numbers;

    // --- reading the log the way a person reads it ------------------------------------------------------------------

    /// <summary>How many final=1 blocks a run wrote (each ends in one total line).</summary>
    public int FinalBlocks(int run) =>
        Logger.Lines.Count(l => l.Contains("[Verdict] run=" + run + " ") && l.Contains(" final=1 total "));

    /// <summary>The word a part got in the newest final=1 block of a run, or null when no such block has the check.</summary>
    public string? FinalPart(int run, int check, string part)
    {
        var line = Logger.Lines.LastOrDefault(l => l.Contains("[Verdict] run=" + run + " ") && l.Contains(" final=1 check=" + check.ToString("00") + " "));
        if (line == null)
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(line, "[| ]" + part + "=([A-Z_]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Remembers, under a name, how many final blocks the runs have and what the check 1 and 2 parts read in the newest.</summary>
    public void Mark(string name, int run)
    {
        Marks[name + ".blocks"] = FinalBlocks(run).ToString();
        foreach (var part in new[] { "installed", "polled", "flow", "cancel" })
            Marks[name + ".1." + part] = FinalPart(run, 1, part) ?? "-";
        foreach (var part in new[] { "named", "unknown", "defaults" })
            Marks[name + ".2." + part] = FinalPart(run, 2, part) ?? "-";
    }

    // --- the main menu -------------------------------------------------------------------------------------------------

    public void InstallMenu()
    {
        Entry.Install();
        Menu.Options[FordsMenuEntry.OptionId].IsHidden();
    }

    public void Click() => Menu.Options[FordsMenuEntry.OptionId].OnChosen();

    /// <summary>The menu entry with the prompts answered: your side, the enemy, the difficulty (one map, so no fourth prompt).</summary>
    public void StartFromMenu()
    {
        Menu.Enqueue(Gondor, Mordor, "hard");
        Click();
    }

    /// <summary>The console command, recorded the way the cheat entry point records it.</summary>
    public string StartFromConsole(params string[] args)
    {
        var reply = FordsOfWarCommands.Start(args, Catalog, Prompt);
        Report.ConsoleCommand("fords_start", args, true, reply);
        return reply;
    }

    public string GoldFromConsole(string amount)
    {
        var args = new[] { amount };
        var reply = FordsOfWarCommands.Gold(args, Session);
        Report.ConsoleCommand("fords_gold", args, true, reply);
        return reply;
    }

    // --- the mission ---------------------------------------------------------------------------------------------------

    private FordsMatchConfig Tuned(FordsMatchConfig c)
    {
        var profiles = c.AiProfiles.ToDictionary(p => p.Key, p => p.Value);
        profiles["idle"] = new FordsAiProfile("idle", 1_000_000f, 1f, 0f, 0);
        var tuned = c with
        {
            CountdownSeconds = 5f,
            Economy = c.Economy with { StartingGold = 5000 },
            Waves = c.Waves with { MaxSpawnsPerTick = 6, FreeWaveIntervalSeconds = 120f },
            Towers = c.Towers with { TowerHitPoints = 800000, InnerTowerHitPoints = 1100000, KeepHitPoints = 1800000 },
            Heroes = c.Heroes with { MaxAlivePerSide = 3 },
            Capture = c.Capture with { Radius = 30f, SecondsToCapture = 5f },
            AiProfiles = profiles,
        };
        return ConfigTweak == null ? tuned : ConfigTweak(tuned);
    }

    /// <summary>The mission's AfterStart: resolve the plan, build the director over the adapters and start it. A failed start is route A.</summary>
    public void BeginMission()
    {
        var setup = Factory.LastSetup ?? throw new InvalidOperationException("no setup reached the game manager factory");
        var resolved = FordsMatchPlan.Resolve(setup, Catalog, Logger) ?? throw new InvalidOperationException("the plan did not resolve");
        Plan = resolved with { Config = Tuned(resolved.Config), AiProfile = "idle" };
        Field.ReportedMaxAgents = MaxAgents;

        var director = new FordsMissionDirector(Plan, Field, Towers, Scene, Session, Logger, new SeededFordsRandom(7), Report);
        Exit = new FordsMissionExit(Menu, Logger, Report);
        EndGamesBefore = Menu.EndGames;
        Failed = !director.Start();
        if (Failed)
        {
            director.Shutdown();
            return;
        }

        Director = director;
        Report.OptionsWired(true);
        Report.PanelAttached();
        Probe = new FordsCameraProbe(Report, StrictInside);
        Camera = FordsCameraMath.Start(Session.CameraStart, Session.CameraLookAt, 0f);
        Subject = null;
    }

    /// <summary>The engine's own point test: strict on an edge, which is why the clamp parks the camera exactly on it.</summary>
    public bool StrictInside(float x, float y)
    {
        var polygon = Scene.Polygon;
        if (FordsCameraMath.DistanceToEdge(polygon, x, y) < 1e-3f)
            return false;

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        return inside;
    }

    /// <summary>One engine frame. <paramref name="wallSeconds"/> defaults to <paramref name="dt"/>; a pause or a hitch passes more.</summary>
    public void Frame(float dt, double? wallSeconds = null)
    {
        _wall += wallSeconds ?? dt;

        Telemetry(dt);
        Director?.Tick(dt);
        Exit?.Tick(dt, Failed, Director?.Winner is { });
        if (Director?.Winner is { } winner && !_resultSent)
        {
            _resultSent = true;
            Report.ResultReady(WrongResult ? winner != MatchSide.West : winner == MatchSide.West);
        }

        CameraAndPause(dt);
        Engine(dt);
    }

    /// <summary>What <c>FordsTelemetryLogic.OnMissionTick</c> does, minus the mission it reads the agent counts from.</summary>
    private void Telemetry(float dt)
    {
        var ms = _firstFrame ? -1.0 : 16.0;
        _firstFrame = false;
        if (Report.MissionTick(dt, ms))
            Report.Window(Field.LiveUnits + Field.GunnerCount, Field.LiveUnits);

        if (Warg == null || Report.MissionSeconds < _wargAt)
            return;

        _wargAt = Report.MissionSeconds + 1.0;
        Warg.Watch();
        Report.Warg(Warg.Counts());
    }

    private void CameraAndPause(float dt)
    {
        var polygon = Session.BoundaryPolygon;
        if (Probe != null && CameraInput is { } input)
        {
            var before = Camera;
            var after = CameraDead ? before : FordsCameraMath.Step(before, input, dt, CameraUnclamped ? new List<Vec3f>() : polygon);
            Probe.Frame(before, input, after, dt, polygon);
            Camera = after;
        }

        Report.Paused(Paused);
    }

    private void Engine(float dt)
    {
        if (Paused)
            return;

        Field.Advance(dt);
        foreach (var rider in Riders)
            rider.Advance(dt);
        if (!VolleysNeverHit)
        {
            foreach (var hit in Towers.PendingHits)
                Report.VolleyHit(hit);
        }

        Towers.PendingHits.Clear();
        if (!FeedSilent)
        {
            foreach (var e in Session.DrainEvents())
            {
                if (e.Kind == MatchEventKind.PackBought)
                    Report.PackBoughtLine();
            }
        }
    }

    public void Seconds(double seconds, float dt = 1f)
    {
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += dt)
            Frame(dt);
    }

    public void Until(Func<bool> done, double limitSeconds, float dt = 1f)
    {
        for (var elapsed = 0.0; elapsed < limitSeconds && !done(); elapsed += dt)
            Frame(dt);
    }

    /// <summary>A pause: the engine ticks with no mission time while the wall clock runs.</summary>
    public void Pause(double wallSeconds, bool askPhoto = true)
    {
        Paused = true;
        if (askPhoto)
            Probe?.PhotoVetoed();
        Frame(0f, wallSeconds);
        Paused = false;
        Frame(0f, 0.0);
    }

    // --- the end -------------------------------------------------------------------------------------------------------

    /// <summary>The mission has ended (EndGame was called): the engine takes <paramref name="wallSeconds"/> to close it.</summary>
    public void Close(double wallSeconds)
    {
        _wall += wallSeconds;
        if (Warg != null)
        {
            WargRageLog.Deactivate(Warg);
            Report.Warg(Warg.Counts());
        }

        Report.MissionClosed();
        Director?.Shutdown();
    }

    public void ReturnToMenu(double wallSeconds = 2.0)
    {
        _wall += wallSeconds;
        Report.MainMenuReturned();
    }

    /// <summary>Plays on until the exit clock calls EndGame, then closes the mission and returns to the menu.</summary>
    public void FinishThroughExit(double closeWall = 2.0, double menuWall = 2.0, float dt = 1f)
    {
        Until(() => Menu.EndGames > EndGamesBefore, 90.0, dt);
        Close(closeWall);
        ReturnToMenu(menuWall);
    }

    // --- what the players and the engine do ----------------------------------------------------------------------------

    public CommandResult BuyPack(int lane, string offerId)
    {
        var result = Session.TryBuyPack(lane, offerId);
        Report.Command("BuyPack", result.ToString());
        return result;
    }

    public CommandResult BuyHero(int lane, string heroId)
    {
        var result = Session.TryBuyHero(lane, heroId);
        Report.Command("BuyHero", result.ToString());
        return result;
    }

    public CommandResult Buyback(string heroId)
    {
        var result = Session.TryBuyback(heroId);
        Report.Command("Buyback", result.ToString());
        return result;
    }

    public CommandResult Order(int lane, LaneOrder order)
    {
        var result = Session.TrySetLaneOrder(lane, order);
        Report.Command("Order:" + order, result.ToString());
        return result;
    }

    /// <summary>The AI commander's purchase, made straight on the match (the scenario keeps the AI idle so it can pick what East fields).</summary>
    public CommandResult EastBuysHero(int lane, string heroId)
    {
        var field = typeof(FordsMissionDirector).GetField("_match", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var match = (FordsMatch)field.GetValue(Director)!;
        return match.TryBuyHero(FordsMissionDirector.AiSlotId, lane, heroId);
    }

    /// <summary>A blow on a tower through the real hit patch's dispatch; the entity then takes what the hook returned.</summary>
    public int StrikeTower(string towerId, object? attacker, bool ranged, int raw, bool weaponless = false)
    {
        var damage = raw;
        FordsTowerHitPatch.Dispatch(Director!, Towers.Of(towerId).Component, attacker, weaponless, ranged, ref damage);
        Towers.Damage(towerId, damage, raw);
        return damage;
    }

    /// <summary>Knocks a tower down with blows from <paramref name="attacker"/>, outer before inner so the chain unlocks.</summary>
    public void Destroy(string towerId, object? attacker = null)
    {
        for (var i = 0; i < 4 && !Towers.Of(towerId).Destroyed; i++)
            StrikeTower(towerId, attacker, ranged: false, 3_000_000);
    }

    public void KillHero(MatchSide side, string heroId)
    {
        var unit = Field.Alive.First(u => u.Side == side && u.Troop == heroId);
        var enemy = Field.Alive.FirstOrDefault(u => u.Side != side);
        Field.Kill(unit, enemy);
    }

    public FaithfulRider AddRider()
    {
        Warg ??= new WargRageLog(Logger, () => Report.MissionSeconds, ReadRider);
        var rider = new FaithfulRider();
        Riders.Add(rider);
        return rider;
    }

    /// <summary>The behavior tree takes the formation away and the rider runs off; the rage began.</summary>
    public void RageStart(FaithfulRider rider)
    {
        Warg!.Started(rider, rider.HasFormation);
        rider.HasFormation = false;
    }

    /// <summary>The reset runs: the rider is given its formation back (or not).</summary>
    public void RageEnd(FaithfulRider rider, bool restore = true)
    {
        Warg!.Ended(rider, restore ? rider.Formation : null);
        rider.HasFormation = restore;
    }

    private static WargRageLog.RiderView ReadRider(object rider, object? target)
    {
        var inactive = new WargRageLog.RiderView(false, false, false, false, 0f, 0f, 0f);
        if (rider is not FaithfulRider r || !r.Active)
            return inactive;

        var inTarget = target != null && r.HasFormation && ReferenceEquals(target, r.Formation);
        return new WargRageLog.RiderView(true, r.HasMount, r.HasFormation, inTarget, target == null ? 0f : r.Distance,
            target == null ? 0f : r.Width, target == null ? 0f : r.Depth);
    }
}
