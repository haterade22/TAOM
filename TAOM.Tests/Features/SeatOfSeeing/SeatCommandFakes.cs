using System.Collections.Generic;
using TAOM.Client.Adapters;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>A scripted engine for the Seat's command mouse: what a pick and a probe find are fields, every write is recorded in order.</summary>
internal sealed class FakeSeatCommand : ISeatCommandAdapter
{
    private static readonly SeatCommandFacts Ready = new(MainAgentExists: true, Deployment: false, EnginePlayerControlled: true, HeroFightsOn: false,
        HasBots: true, HasTroopsUnderThem: true, PlayerGeneralOrSergeant: true, MissionEnding: false, BattleInRetreat: false);

    internal SeatCommandFacts Facts = Ready;
    internal int Selected;
    internal SeatPicked Own;
    internal SeatRightProbe Probe;
    internal bool LineStarts = true;
    internal int FactsReads;

    /// <summary>Every call that reached the engine, as "name" or "name:argument", in order.</summary>
    internal readonly List<string> Calls = new();

    internal (float X, float Y)? LastOwnPick;
    internal (float X, float Y, float RX, float RY)? LastProbe;
    internal (float X, float Y, bool Horizontal)? LastPreview;
    internal (float X, float Y, bool Horizontal)? LastCommit;
    internal bool? LastMoveHorizontal;

    public SeatCommandFacts ReadFacts()
    {
        FactsReads++;
        return Facts;
    }

    public int SelectedCount => Selected;

    internal bool LineShown { get; private set; }

    public SeatPicked PickOwn(float pixelX, float pixelY)
    {
        LastOwnPick = (pixelX, pixelY);
        Calls.Add("pick-own");
        return Own;
    }

    public void Select(SeatSelectAction action) => Calls.Add("select:" + action);

    public SeatRightProbe ProbeRight(float pixelX, float pixelY, float rangedX, float rangedY)
    {
        LastProbe = (pixelX, pixelY, rangedX, rangedY);
        Calls.Add("probe");
        return Probe;
    }

    public void Charge() => Calls.Add("charge");

    public void UseOrderable() => Calls.Add("orderable");

    public void MoveToProbed(bool horizontal)
    {
        LastMoveHorizontal = horizontal;
        Calls.Add("move");
    }

    public bool LineBegin(float rangedX, float rangedY)
    {
        Calls.Add("line-begin");
        return LineStarts;
    }

    public void LinePreview(float rangedX, float rangedY, bool horizontal)
    {
        LineShown = true;
        LastPreview = (rangedX, rangedY, horizontal);
        Calls.Add("line-preview");
    }

    public void LineCommit(float rangedX, float rangedY, bool horizontal)
    {
        LineShown = false;
        LastCommit = (rangedX, rangedY, horizontal);
        Calls.Add("line-commit");
    }

    public void LineCancel()
    {
        LineShown = false;
        Calls.Add("line-cancel");
    }

    public void Drop()
    {
        LineShown = false;
        Calls.Add("drop");
    }

    internal int Count(string call)
    {
        var n = 0;
        foreach (var c in Calls)
            if (c == call)
                n++;
        return n;
    }
}
