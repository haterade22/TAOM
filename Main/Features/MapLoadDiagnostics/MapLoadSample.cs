namespace TAOM.Features.MapLoadDiagnostics;

/// <summary>
/// One heartbeat's readings, captured at the engine boundary so the service stays free of
/// TaleWorlds types (ADR-007) and fully testable without a running game.
///
/// <para>
/// Built ONLY on frames that actually emit. The census walks every mobile party, which is the very
/// cost being investigated, so doing it per frame would distort the measurement it exists to take.
/// </para>
/// </summary>
public readonly struct MapLoadSample
{
    public readonly int PartyCount;
    public readonly int LordParties;
    public readonly int Villagers;
    public readonly int Caravans;
    public readonly int Bandits;
    public readonly int Militia;
    public readonly int Garrisons;
    public readonly int OtherParties;
    public readonly int SettlementCount;
    public readonly int HeroCount;
    public readonly int ClanCount;
    public readonly double CampaignTime;
    public readonly bool IsLoadingWindowActive;
    /// <summary>Mean milliseconds spent inside Campaign.RealTick over the window.</summary>
    public readonly double TickMsAvg;
    /// <summary>Type name of GameStateManager.ActiveState.</summary>
    public readonly string ActiveState;
    /// <summary>
    /// The WHOLE game-state stack, bottom to top. The single most useful field here: a state left
    /// pushed above MapState would hold the overlay while the map runs happily underneath, which is
    /// exactly the observed shape.
    /// </summary>
    public readonly string StateStack;
    /// <summary>Type name of ScreenManager.TopScreen.</summary>
    public readonly string TopScreen;
    /// <summary>Campaign time control mode, plus whether it is locked.</summary>
    public readonly string TimeControl;

    public MapLoadSample(int partyCount, int lordParties, int villagers, int caravans, int bandits,
                         int militia, int garrisons, int otherParties, int settlementCount,
                         int heroCount, int clanCount, double campaignTime,
                         bool isLoadingWindowActive, double tickMsAvg,
                         string activeState, string stateStack, string topScreen, string timeControl)
    {
        PartyCount = partyCount;
        LordParties = lordParties;
        Villagers = villagers;
        Caravans = caravans;
        Bandits = bandits;
        Militia = militia;
        Garrisons = garrisons;
        OtherParties = otherParties;
        SettlementCount = settlementCount;
        HeroCount = heroCount;
        ClanCount = clanCount;
        CampaignTime = campaignTime;
        IsLoadingWindowActive = isLoadingWindowActive;
        TickMsAvg = tickMsAvg;
        ActiveState = activeState;
        StateStack = stateStack;
        TopScreen = topScreen;
        TimeControl = timeControl;
    }
}
