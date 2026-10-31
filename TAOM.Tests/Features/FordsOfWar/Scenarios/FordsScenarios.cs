using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Scenarios;

/// <summary>
/// The scenarios the tests and the contract table share. Each is built once and cached: a 30 minute match is not rerun per assert.
/// "S1" to "S5" are the working producers; every other name is one fault injected into an otherwise working run.
/// </summary>
internal static class FordsScenarios
{
    private static readonly Dictionary<string, Lazy<FordsScenario>> Cache = new();

    public static FordsScenario Get(string name)
    {
        Lazy<FordsScenario> lazy;
        lock (Cache)
        {
            if (!Cache.TryGetValue(name, out lazy!))
                Cache[name] = lazy = new Lazy<FordsScenario>(() => Build(name));
        }

        return lazy.Value;
    }

    private static FordsScenario Build(string name) => name switch
    {
        "S1" => Clean(),
        "S3-warg" => PausedWargReturn(),
        "S3-exit" => PausedRouteA(),
        "S4" => CameraAtTheEdge(),
        "S5-console" => ConsoleStarts(),
        "S5-tail" => SessionTail(),
        "menu-no-content" => MenuWithoutContent(),
        "flow-fault" => FlowFault(),
        "route-a-slow-close" => RouteA(closeWall: 20.0),
        "route-b" => RouteB(),
        "route-c" => RouteC(),
        "route-c-console" => RouteCFromTheConsole(),
        "notice-unacknowledged" => NoticeNeverAcknowledged(),
        "tower-dropped" => TowerDropped(),
        "missing-id" => With(sc => sc.Field.MissingCharacter = id => id == "footman", 3),
        "spawn-null" => With(sc => sc.Field.SpawnFails = (_, side, lane) => side == MatchSide.West && lane == 2, 80),
        "hero-spawn-null" => HeroSpawnNull(),
        "hero-respawn-lost" => HeroRespawnLost(),
        "frozen" => With(sc => sc.Field.Frozen = true, 80),
        "killer-unknown" => KillerUnknown(),
        "hp-drift" => HpDrift(),
        "towers-dead" => TowersDead(),
        "item-missing" => With(sc => sc.Towers.Item = null, 3),
        "hold-in-fire" => HoldInFire(),
        "over-cap" => OverCap(),
        "agents-over-limit" => With(sc => sc.MaxAgents = 30, 40),
        "camera-dead" => Camera(sc => sc.CameraDead = true, Input(pan: 1f, rotate: 1f, zoom: 1f), 40),
        "camera-unclamped" => Camera(sc => sc.CameraUnclamped = true, Input(right: 1f), 40),
        "camera-failed" => CameraFailed(),
        "photo-unasked" => PhotoUnasked(),
        "options-missing" => OptionsMissing(),
        "gold-refused" => GoldRefused(),
        "feed-silent" => FeedSilent(),
        "result-wrong" => WinQuickly(sc => sc.WrongResult = true),
        "exit-slow" => WinQuickly(closeWall: 20.0),
        "warg-stranded" => WargStranded(),
        "warg-not-returned" => WargNotReturned(),
        _ => throw new ArgumentException("no scenario named " + name),
    };

    internal static FordsCameraInput Input(float pan = 0f, float right = 0f, float rotate = 0f, float zoom = 0f) =>
        new(pan, right, rotate, zoom, 0f, 0f, false);

    /// <summary>A new scenario with the menu used, the match started and the camera ready.</summary>
    internal static FordsScenario Started(Action<FordsScenario>? before = null)
    {
        var sc = new FordsScenario();
        before?.Invoke(sc);
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.CameraInput = Input();
        return sc;
    }

    private static FordsScenario With(Action<FordsScenario> fault, int seconds)
    {
        var sc = new FordsScenario();
        fault(sc);
        return Run(sc, seconds);
    }

    private static FordsScenario Run(FordsScenario sc, int seconds)
    {
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.CameraInput = Input();
        sc.Seconds(seconds);
        return sc;
    }

    private static FordsScenario Camera(Action<FordsScenario> fault, FordsCameraInput input, int seconds)
    {
        var sc = Started(fault);
        sc.CameraInput = input;
        sc.Seconds(seconds);
        return sc;
    }

    // --- the working producers -----------------------------------------------------------------------------------------

    /// <summary>S1: a clean thirty minute match, three lanes, judged at the main menu.</summary>
    internal static FordsScenario Clean()
    {
        var sc = new FordsScenario();
        sc.InstallMenu();
        sc.Click();
        sc.Menu.Cancel();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.CameraInput = Input();

        for (var t = 0; t < 1830; t++)
        {
            Script(sc, t);
            sc.Frame(1f);
        }

        Win(sc, buyBeforeTheKeepFalls: true);
        sc.FinishThroughExit();
        return sc;
    }

    private static void Script(FordsScenario sc, int t)
    {
        var west = sc.Field.Alive.FirstOrDefault(u => u.Side == MatchSide.West);
        switch (t)
        {
            case 6: sc.Order(0, LaneOrder.Push); break;
            case 8: sc.BuyHero(1, FordsScenario.HeroA); break;
            case 9: sc.BuyHero(2, FordsScenario.HeroB); break;
            case 10: sc.EastBuysHero(1, FordsScenario.HeroA); break;
            case 12: sc.BuyPack(0, FordsScenario.Inf); sc.BuyPack(1, FordsScenario.Inf); break;
            case 13: sc.CameraInput = Input(pan: 1f); break;
            case 45: sc.CameraInput = Input(rotate: 1f); break;
            case 75: sc.CameraInput = Input(zoom: 1f); break;
            case 105: sc.CameraInput = Input(); break;
            case 60: sc.Pause(5.0); sc.Report.OptionsRequested(); break;
            case 70: sc.GoldFromConsole("500"); break;
            case 80: sc.RageStart(sc.AddRider()); break;
            case 82: sc.RageEnd(sc.Riders[0]); break;
            case 150: sc.KillHero(MatchSide.West, FordsScenario.HeroA); break;
            case 200: sc.StrikeTower("tower_east_0_1", west?.Handle, false, 100); break;
            case 210: sc.StrikeTower("tower_west_1_0", west?.Handle, false, 100); break;
            case 300: sc.Order(1, LaneOrder.Hold); break;
            case 320: sc.Order(2, LaneOrder.FallBack); break;
            case 360: sc.Order(2, LaneOrder.Push); break;
            case 420: sc.Order(1, LaneOrder.Push); break;
        }
    }

    /// <summary>West breaks East's lane 0 chain and then the keep; a hero bought in the same breath as the keep's fall is never spawned.</summary>
    private static void Win(FordsScenario sc, bool buyBeforeTheKeepFalls = false)
    {
        sc.Destroy("tower_east_0_0");
        sc.Frame(1f);
        sc.Destroy("tower_east_0_1");
        sc.Frame(1f);
        if (buyBeforeTheKeepFalls)
            sc.BuyHero(0, FordsScenario.HeroC);
        sc.Destroy("keep_east");
    }

    private static FordsScenario WinQuickly(Action<FordsScenario>? fault = null, double closeWall = 2.0)
    {
        var sc = Started(fault);
        sc.BuyPack(0, FordsScenario.Inf);
        sc.Seconds(20);
        Win(sc);
        sc.FinishThroughExit(closeWall);
        return sc;
    }

    /// <summary>S3: 90 s of wall time with no mission time while a restored warg rider walks back. The rule reads mission seconds.</summary>
    private static FordsScenario PausedWargReturn()
    {
        var sc = Started();
        sc.Seconds(10);
        var rider = sc.AddRider();
        sc.RageStart(rider);
        sc.Seconds(2);
        sc.RageEnd(rider);
        sc.Seconds(3);
        sc.Pause(90.0);
        sc.Seconds(40);
        return sc;
    }

    /// <summary>S3: route A with a 90 s hitch (wall time, no mission time) before the 3 s exit clock has run down. The exit is timed in mission seconds.</summary>
    private static FordsScenario PausedRouteA()
    {
        var sc = new FordsScenario();
        sc.Scene.HasBoundary = false;
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.Frame(1f);
        sc.Frame(0f, 90.0);
        sc.FinishThroughExit();
        sc.Menu.Acknowledge();
        return sc;
    }

    /// <summary>S4: the camera pushed into the south edge with the real step; the engine's own test is strict on the edge.</summary>
    private static FordsScenario CameraAtTheEdge()
    {
        var sc = Started();
        sc.CameraInput = Input(right: 1f);
        sc.Seconds(40);
        return sc;
    }

    /// <summary>S5: the console starts. An unknown argument, a start with arguments and a start with none, each into a started match.</summary>
    private static FordsScenario ConsoleStarts()
    {
        var sc = new FordsScenario();
        sc.InstallMenu();
        sc.StartFromConsole("zzz");
        sc.StartFromConsole(FordsScenario.Gondor, FordsScenario.Mordor, "hard");
        sc.BeginMission();
        sc.Seconds(10);
        Win(sc);
        sc.FinishThroughExit();
        sc.StartFromConsole();
        sc.BeginMission();
        sc.Seconds(10);
        return sc;
    }

    /// <summary>S5: the session's tail after a finalized run, then a route C start and the menu return that follows it.</summary>
    private static FordsScenario SessionTail()
    {
        var sc = Started();
        sc.Seconds(10);
        Win(sc);
        sc.FinishThroughExit();
        sc.Mark("final", 1);

        sc.Click();
        sc.Menu.Cancel();
        sc.Mark("cancel", 1);

        sc.Click();
        sc.Click();
        sc.Mark("abandon", 1);

        sc.Menu.ChooseThrows = true;
        sc.Click();
        sc.Menu.ChooseThrows = false;
        sc.Mark("fault", 1);

        sc.StartFromConsole("zzz");
        sc.Mark("unknown", 1);

        sc.Factory.ReturnsNull = true;
        sc.StartFromConsole(FordsScenario.Gondor, FordsScenario.Mordor, "hard");
        sc.Mark("routeC", 2);
        sc.ReturnToMenu();
        sc.Mark("menuReturn", 2);
        return sc;
    }

    // --- one fault each ------------------------------------------------------------------------------------------------

    private static FordsScenario MenuWithoutContent()
    {
        var sc = new FordsScenario(factions: "{ }");
        sc.InstallMenu();
        return sc;
    }

    private static FordsScenario FlowFault()
    {
        var sc = new FordsScenario();
        sc.InstallMenu();
        sc.Menu.ChooseThrows = true;
        sc.Click();
        return sc;
    }

    private static FordsScenario RouteA(double closeWall)
    {
        var sc = new FordsScenario();
        sc.Scene.HasBoundary = false;
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.FinishThroughExit(closeWall);
        return sc;
    }

    private static FordsScenario RouteB()
    {
        var sc = new FordsScenario();
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.ReturnToMenu();
        return sc;
    }

    private static FordsScenario RouteC()
    {
        var sc = new FordsScenario();
        sc.Factory.ReturnsNull = true;
        sc.InstallMenu();
        sc.StartFromMenu();
        return sc;
    }

    private static FordsScenario RouteCFromTheConsole()
    {
        var sc = new FordsScenario();
        sc.Factory.ReturnsNull = true;
        sc.InstallMenu();
        sc.StartFromConsole(FordsScenario.Gondor, FordsScenario.Mordor, "hard");
        sc.StartFromConsole();
        return sc;
    }

    private static FordsScenario NoticeNeverAcknowledged()
    {
        var sc = new FordsScenario();
        sc.Scene.HasBoundary = false;
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.FinishThroughExit();
        var first = sc.Report.Run!;
        sc.Scene.HasBoundary = true;
        sc.StartFromMenu();
        sc.Subject = first;
        return sc;
    }

    private static FordsScenario TowerDropped()
    {
        var sc = new FordsScenario();
        sc.Scene.Blocked.Add(new Vec3f(640f, -45f, 0f));
        return Run(sc, 3);
    }

    private static FordsScenario HeroSpawnNull()
    {
        var sc = new FordsScenario();
        sc.Field.SpawnFails = (troop, _, _) => troop == FordsScenario.HeroA;
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.Seconds(8);
        sc.BuyHero(1, FordsScenario.HeroA);
        sc.Seconds(30);
        return sc;
    }

    private static FordsScenario HeroRespawnLost()
    {
        var sc = Started();
        sc.Seconds(8);
        sc.BuyHero(1, FordsScenario.HeroA);
        sc.Seconds(20);
        sc.KillHero(MatchSide.West, FordsScenario.HeroA);
        sc.Field.SpawnFails = (troop, _, _) => troop == FordsScenario.HeroA;
        sc.Seconds(100);
        return sc;
    }

    private static FordsScenario KillerUnknown()
    {
        var sc = Started(s => s.Field.KillerUnknown = true);
        sc.Until(() => sc.Report.Run!.Kills.DeathsEast >= 25, 1500.0);
        return sc;
    }

    private static FordsScenario HpDrift()
    {
        var sc = Started(s => s.Towers.TakesRawDamage = true);
        sc.Seconds(10);
        var west = sc.Field.Alive.First(u => u.Side == MatchSide.West);
        sc.StrikeTower("tower_east_0_0", west.Handle, ranged: true, 1000);
        sc.StrikeTower("tower_west_0_0", west.Handle, ranged: false, 100);
        sc.Seconds(40);
        return sc;
    }

    private static FordsScenario TowersDead()
    {
        var sc = new FordsScenario();
        sc.Field.SpawnFails = (_, side, _) => side == MatchSide.East;
        sc.Field.MeleeRawDamage = 0;
        sc.Towers.NeverFires = true;
        return Run(sc, 260);
    }

    private static FordsScenario HoldInFire()
    {
        var sc = new FordsScenario();
        sc.ConfigTweak = c => c with { Towers = c.Towers with { FireRange = 80f } };
        sc.Field.SpawnFails = (_, side, _) => side == MatchSide.East;
        sc.InstallMenu();
        sc.StartFromMenu();
        sc.BeginMission();
        sc.Seconds(60);
        sc.Order(1, LaneOrder.Hold);
        sc.Seconds(200);
        return sc;
    }

    private static FordsScenario OverCap()
    {
        var sc = new FordsScenario();
        sc.ConfigTweak = c => c with
        {
            Waves = c.Waves with { MaxLivePerSide = 10, MaxLivePerLanePerSide = 10 },
            Heroes = c.Heroes with { MaxAlivePerSide = 0 },
        };
        sc.Field.DoubleSpawns = true;
        return Run(sc, 70);
    }

    private static FordsScenario OptionsMissing()
    {
        var sc = Started();
        sc.Report.OptionsWired(false);
        sc.Seconds(2);
        return sc;
    }

    private static FordsScenario CameraFailed()
    {
        var sc = Started();
        sc.Seconds(2);
        sc.Probe!.Failed();
        return sc;
    }

    private static FordsScenario PhotoUnasked()
    {
        var sc = Started();
        sc.Seconds(2);
        sc.Pause(5.0, askPhoto: false);
        return sc;
    }

    private static FordsScenario GoldRefused()
    {
        var sc = Started();
        sc.Seconds(10);
        sc.Session.Clear();
        sc.GoldFromConsole("100");
        return sc;
    }

    private static FordsScenario FeedSilent()
    {
        var sc = Started(s => s.FeedSilent = true);
        sc.Seconds(5);
        sc.BuyPack(0, FordsScenario.Inf);
        sc.Seconds(15);
        Win(sc);
        sc.FinishThroughExit();
        return sc;
    }

    private static FordsScenario WargStranded()
    {
        var sc = Started();
        sc.Seconds(5);
        var rider = sc.AddRider();
        rider.HasMount = false;
        sc.RageStart(rider);
        sc.Seconds(5);
        return sc;
    }

    private static FordsScenario WargNotReturned()
    {
        var sc = Started();
        sc.Seconds(5);
        var rider = sc.AddRider();
        sc.RageStart(rider);
        sc.RageEnd(rider);
        rider.Stuck = true;
        sc.Seconds(70);
        return sc;
    }
}
