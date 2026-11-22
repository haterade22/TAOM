using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The body the player has now: the lord's own, a soldier he took over after a fall, or none (he spectates until he takes one or the wave clears).</summary>
internal enum LordBody
{
    Own,
    Taken,
    None,
}

internal enum GeneralSeatKind
{
    Lord,
    Hero,
    None,
}

/// <summary>Who holds the defenders' general seat: the player's body, a standing hero of the shop (by his id), or nobody.</summary>
internal readonly record struct LdGeneralSeat(GeneralSeatKind Kind, string HeroId = "")
{
    public static LdGeneralSeat Lord { get; } = new(GeneralSeatKind.Lord);

    public static LdGeneralSeat Empty { get; } = new(GeneralSeatKind.None);
}

/// <summary>
/// D41 (phase4-plan.md; the Marshal it named was replaced by the shop's heroes, #1337): the seat is the player's body, his own or a taken one. While he has none it goes to the first standing hero,
/// else to nobody, and never stays on a dead body. Slice 4 writes the seat to the engine. In this mission it will change only how much its holder's fall or rout moves the defenders' morale: the team AI commands while the player is down either way.
/// </summary>
internal static class GeneralSeat
{
    public static LdGeneralSeat Choose(LordBody body, IReadOnlyList<string> standingHeroes) =>
        body != LordBody.None ? LdGeneralSeat.Lord : standingHeroes.Count > 0 ? new LdGeneralSeat(GeneralSeatKind.Hero, standingHeroes[0]) : LdGeneralSeat.Empty;
}

/// <summary>One body of the player's that fell: the wave, whether it was the lord's own or a taken soldier, its troop, and whether the test option <c>lord_falls</c> felled it.</summary>
internal sealed record LordDeath(int Wave, LordBody Body, string Troop, bool Forced);

/// <summary>One soldier taken over after a fall: the wave, who took him (the unattended bot, else a person) and his troop.</summary>
internal sealed record LordTakeover(int Wave, bool ByBot, string Troop);

/// <summary>One rise at a clear: the wave it precedes, where (<c>respawn</c> for today's respawn into the formation, <c>home</c> for a mounted lord; <c>recorded</c> or <c>scene</c> for the keep once Slice 4 raises him there) and whether a taken soldier was handed back to the AI.</summary>
internal sealed record LordRise(int BeforeWave, string At, bool HandedBack);

/// <summary>The rise the run orders at a clear: whether a taken soldier is to be handed back, and whether the lord rises at his home point (a mounted class, D48) and not at the keep.</summary>
internal sealed record LordRiseOrder(bool HandBack, bool AtHome);

/// <summary>What the report lists of the lord: every body that fell, every soldier taken and every rise, each in order.</summary>
internal sealed record RunLord(IReadOnlyList<LordDeath> Deaths, IReadOnlyList<LordTakeover> Takeovers, IReadOnlyList<LordRise> Rises)
{
    public static RunLord None { get; } = new(new LordDeath[0], new LordTakeover[0], new LordRise[0]);
}

/// <summary>
/// The death rule's state (D40): the lord fights in his own body; a fall leaves him with none; he may take a soldier, as often as one qualifies; at the clear he rises in a body of his own whenever
/// he is not in one. Plain state: the run feeds it the falls and each tick's reading of the lord, and reads back the rise it owes.
/// </summary>
internal sealed class LordLife
{
    /// <summary>The mission seconds after a fall at which the unattended bot takes a soldier (D44a), and again every so often while no body stands.</summary>
    internal const float BotTakeoverSeconds = 5f;

    /// <summary>The seconds into the fight of the <c>lord_falls</c> wave at which the lord is felled (D47).</summary>
    internal const float ForcedFallSeconds = 20f;

    private readonly List<LordDeath> _deaths = new();
    private readonly List<LordTakeover> _takeovers = new();
    private readonly List<LordRise> _rises = new();
    private float _downSeconds;
    private float _fightSeconds;
    private bool _forcedAsked;

    public LordBody Body { get; private set; } = LordBody.Own;

    /// <summary>True when the clear must raise the lord: he has no body, or the body is a soldier's.</summary>
    public bool RiseOwed => Body != LordBody.Own;

    /// <summary>True when the rise hands a taken soldier back to the AI.</summary>
    public bool HandBack => Body == LordBody.Taken;

    public RunLord Summary() => new(_deaths.ToList(), _takeovers.ToList(), _rises.ToList());

    /// <summary>
    /// A body of the player's fell. It was the lord's own when he stood in it; any body that falls after that, until he rises, is a soldier's. <paramref name="forced"/> is a fact the field
    /// reports: the removal is the one the <c>lord_falls</c> blow produced, because the adapter marked the body it struck. The run infers nothing from the ask, so an ask nobody carried out marks no death.
    /// </summary>
    public void Fell(int wave, string troop, bool forced)
    {
        var own = Body == LordBody.Own;
        _deaths.Add(new LordDeath(wave, own ? LordBody.Own : LordBody.Taken, troop, forced));
        Body = LordBody.None;
        _downSeconds = 0f;
    }

    /// <summary>
    /// This tick's reading of the lord: no body, his own, or a soldier's (a new one is a take-over). A tick that read nothing says nothing. A body that left with no fall (a rout) starts the
    /// bot's take-over clock afresh, as a fall does.
    /// </summary>
    public void Observe(LdLordFacts? lord, int wave, bool byBot)
    {
        if (lord == null)
            return;

        if (!lord.Present)
        {
            if (Body != LordBody.None)
                _downSeconds = 0f;

            Body = LordBody.None;
        }
        else if (lord.Own)
        {
            Body = LordBody.Own;
        }
        else
        {
            if (Body != LordBody.Taken)
                _takeovers.Add(new LordTakeover(wave, byBot, lord.Id));

            Body = LordBody.Taken;
        }
    }

    /// <summary>The field raised the lord: he is in a body of his own again.</summary>
    public void Rose(int beforeWave, string at, bool handedBack)
    {
        _rises.Add(new LordRise(beforeWave, at, handedBack));
        Body = LordBody.Own;
    }

    /// <summary>Counts the seconds the player has had no body; true once each <see cref="BotTakeoverSeconds"/>, when the bot should take a soldier. A <paramref name="dt"/> that is no time adds nothing.</summary>
    public bool BotTakeoverDue(float dt)
    {
        if (Body != LordBody.None || !(dt > 0f) || float.IsInfinity(dt))
            return false;

        _downSeconds += dt;
        if (_downSeconds < BotTakeoverSeconds)
            return false;

        _downSeconds = 0f;
        return true;
    }

    /// <summary>
    /// Counts the seconds of the fight in the <c>lord_falls</c> wave; true once, at <see cref="ForcedFallSeconds"/>, when the lord stands in his own body. It is the ask only: the death is booked forced when
    /// the field reports the removal of the body its blow struck (<see cref="Fell"/>).
    /// </summary>
    public bool ForcedFallDue(float dt, bool fighting)
    {
        if (_forcedAsked || !fighting || Body != LordBody.Own || !(dt > 0f) || float.IsInfinity(dt))
            return false;

        _fightSeconds += dt;
        if (_fightSeconds < ForcedFallSeconds)
            return false;

        _forcedAsked = true;
        return true;
    }
}
