using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The armoury's half of the field fake (phase4-plan.md Slice 5): the gear swap answers as the test sets it, and the bot's errand moves the lord the way the engine's scripted walk would, only when the test lets him arrive.
/// </summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>Every loadout the director asked to put on the lord's body, in order.</summary>
    public List<LdLoadout> Swaps { get; } = new();

    /// <summary>What a swap answers; by default it was done, with no armour read.</summary>
    public LdSwap SwapAnswer { get; set; } = new(true, "", "A", new LdArmour(1f, 2f, 3f, 4f, 5f), new LdArmour(6f, 7f, 8f, 9f, 10f), new LdArmour(6f, 7f, 8f, 9f, 10f), 100f, 100f, 100f, 100f, false, false, "sword", 0);

    /// <summary>When true a started errand brings the lord to the place at once, as a walk that arrived would; false leaves him where he stands.</summary>
    public bool ErrandArrives { get; set; } = true;

    /// <summary>False makes the field refuse a start, as it does for a body a person controls: nobody is held then.</summary>
    public bool ErrandStartOk { get; set; } = true;

    /// <summary>The places an errand was started for, in order.</summary>
    public List<LdPlace> ErrandStarts { get; } = new();

    /// <summary>The places an errand was teleported to, in order.</summary>
    public List<LdPlace> ErrandTeleports { get; } = new();

    /// <summary>The walk is held: set by a start, cleared by an end.</summary>
    public bool ErrandHeld { get; private set; }

    /// <summary>What the armour read answers.</summary>
    public LdArmour ArmourRead { get; set; } = new(6f, 7f, 8f, 9f, 10f);

    public LdSwap SwapGear(LdLoadout loadout)
    {
        Calls.Add("Swap:" + loadout.ClassId);
        Swaps.Add(loadout);
        return SwapAnswer;
    }

    public LdErrandMove StartErrand(LdPlace place)
    {
        Calls.Add("ErrandStart:" + F(place.X));
        ErrandStarts.Add(place);
        if (!ErrandStartOk)
            return LdErrandMove.Refused("person-controlled");

        ErrandHeld = true;
        if (ErrandArrives)
            Lord = Lord with { At = place.Point };
        return new LdErrandMove(true, "", Lord.HasMount, true, false, true, 12f);
    }

    public LdErrandMove TeleportErrand(LdPlace place)
    {
        Calls.Add("ErrandTeleport:" + F(place.X));
        ErrandTeleports.Add(place);
        if (!ErrandHeld)
            return LdErrandMove.Refused("none-held");

        if (TeleportOk)
            Lord = Lord with { At = place.Point };
        return new LdErrandMove(TeleportOk, TeleportOk ? "" : "teleport-refused", Lord.HasMount, true, false, true, 40f);
    }

    public LdErrandMove EndErrand()
    {
        Calls.Add("ErrandEnd");
        var held = ErrandHeld;
        ErrandHeld = false;
        return new LdErrandMove(held, held ? "" : "none-held", Lord.HasMount, true, false, false, 0f);
    }

    public LdArmour ReadLordArmour()
    {
        Calls.Add("ReadArmour");
        return ArmourRead;
    }
}
