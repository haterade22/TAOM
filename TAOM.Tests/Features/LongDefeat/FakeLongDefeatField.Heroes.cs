using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The heroes' half of the field fake (#1325): a spawn is recorded and answers a body as the engine makes one, unless a test set its
/// answer. A removal named "H:&lt;id&gt;" is the held hero's, "HP:&lt;id&gt;" the held hero's whose body the player had taken over (FakeLongDefeatField.Classify).
/// </summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>Every hero spawn asked for, in order.</summary>
    public List<(HeroSpawn Hero, LdPlace Place)> HeroSpawns { get; } = new();

    /// <summary>What the next spawn answers instead of a good body, or null for a good body.</summary>
    public LdHeroReport? HeroAnswer { get; set; }

    /// <summary>What <see cref="ReadHeroes"/> answers: the heroes that stand, as a test sets them.</summary>
    public List<LdHeroFacts> HeroFacts { get; set; } = new();

    /// <summary>Whether the heroes' formation is off AI control, as <see cref="ReadHeroes"/> reads it.</summary>
    public bool GroupHeld { get; set; } = true;

    /// <summary>The name of the formation's movement order.</summary>
    public string GroupOrder { get; set; } = "Move";

    /// <summary>Whether that order is still the field's own order at the heroes' place.</summary>
    public bool OwnOrder { get; set; } = true;

    /// <summary>The formation's unit count; null answers the number of heroes in <see cref="HeroFacts"/>.</summary>
    public int? GroupUnits { get; set; }

    /// <summary>The seconds since the field gave its own order.</summary>
    public float SettledSeconds { get; set; } = 600f;

    public LdHeroesRead ReadHeroes()
    {
        Calls.Add("ReadHeroes");
        return new LdHeroesRead(HeroFacts.ToArray(), GroupHeld, GroupOrder, OwnOrder, GroupUnits ?? HeroFacts.Count, SettledSeconds);
    }

    public LdHeroReport SpawnHero(HeroSpawn hero, LdPlace place)
    {
        Calls.Add("SpawnHero");
        HeroSpawns.Add((hero, place));
        if (HeroAnswer is { } answer)
        {
            HeroAnswer = null;
            return answer;
        }

        return new LdHeroReport(true, null, true, false, HeroGroup.FormationIndex, hero.HealthFactor, "sword", Held: true, Strangers: 0, Order: "Move");
    }

    /// <summary>What <see cref="HoldHunters"/> answers on each call.</summary>
    public LdHuntersHold HoldHuntersAnswer { get; set; } = LdHuntersHold.Nothing;

    public LdHuntersHold HoldHunters()
    {
        Calls.Add("HoldHunters");
        return HoldHuntersAnswer;
    }
}
