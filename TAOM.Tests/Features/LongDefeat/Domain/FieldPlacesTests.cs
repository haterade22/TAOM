using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The attackers of a field defence come from two places (#1105 slice 2). A new group starts at the place farther from where the defenders stand, so it
/// never appears inside the fight (run 20261121-185849: the fight had moved to the one start place, and the next group spawned in the melee).
/// </summary>
[TestClass]
public class FieldPlacesTests
{
    private static readonly MusterFrame Home = new(0f, 0f, 0f, 0f, 1f);
    private static readonly MusterFrame Ahead = new(0f, 80f, 0f, 0f, -1f);
    private static readonly MusterFrame Behind = new(0f, -80f, 0f, 0f, 1f);

    [TestMethod]
    public void WithOneAttackerPlace_ThatPlaceIsUsed_WhereverTheDefendersStand()
    {
        var places = new FieldPlaces(Home, Ahead);

        Assert.AreSame(Ahead, places.AttackersFartherFrom(new[] { new LdPoint(0f, 79f, 0f) }));
    }

    [TestMethod]
    public void WithTwoPlaces_TheOneFartherFromTheDefendersIsUsed()
    {
        var places = new FieldPlaces(Home, Ahead, Behind);

        Assert.AreSame(Behind, places.AttackersFartherFrom(new[] { new LdPoint(0f, 70f, 0f), new LdPoint(2f, 60f, 0f) }), "the defenders fight at the place ahead");
        Assert.AreSame(Ahead, places.AttackersFartherFrom(new[] { new LdPoint(0f, -70f, 0f) }), "the defenders fight at the place behind");
    }

    [TestMethod]
    public void WithTwoPlaces_AndNoDefenderStanding_TheFirstPlaceIsUsed()
    {
        var places = new FieldPlaces(Home, Ahead, Behind);

        Assert.AreSame(Ahead, places.AttackersFartherFrom(Array.Empty<LdPoint>()));
    }

    [TestMethod]
    public void ANonFiniteDefenderPoint_IsLeftOutOfTheMean()
    {
        var places = new FieldPlaces(Home, Ahead, Behind);

        Assert.AreSame(Behind, places.AttackersFartherFrom(new[] { new LdPoint(float.NaN, 0f, 0f), new LdPoint(0f, 70f, 0f) }));
    }
}
