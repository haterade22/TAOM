using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BanditVoices;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.BanditVoices;

/// <summary>
/// #900: the choice of a clan's own greeting and the vanilla ultimatum that goes with it. Every gate except the row check
/// (the toggle, the sea, a repeat meeting, a dedicated server, a messenger conversation, no faction) returns before the dice
/// are touched; the row check comes after the roll, because the roll picks the row. A fault anywhere leaves vanilla's text.
/// Since Encounter Lines (Patch149) one roll also covers the bucket greeting: below 0.25 the legacy row, then (or when that
/// row is missing) below 0.75 the bucket row, else vanilla. The tests without a bucket source are the pre-Patch149 behaviour.
/// </summary>
[TestClass]
public class BanditVoicesServiceTests
{
    private const string Clan = "dunland_raiders";

    private IBanditVoicesSettingsProvider _settings = null!;
    private IRandomSource _random = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IMessengerConversationState _messenger = null!;
    private IBanditEncounterTextAdapter _text = null!;
    private BanditVoicesService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBanditVoicesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _random = Substitute.For<IRandomSource>();
        _random.NextDouble().Returns(0.0);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _messenger = Substitute.For<IMessengerConversationState>();
        _text = Substitute.For<IBanditEncounterTextAdapter>();
        _text.EncounteredFactionId.Returns(Clan);
        _text.EncounteredAtSea.Returns(false);
        _text.IsFirstMeeting().Returns(true);
        _text.TrySetGreeting(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _service = new BanditVoicesService(_settings, _random, _dedicated, _messenger, _text);
    }

    private void AssertNothingWritten()
    {
        _text.DidNotReceiveWithAnyArgs().TrySetGreeting(default!, default!);
        _text.DidNotReceiveWithAnyArgs().SetUltimatum(default!);
    }

    private void AssertDiceUntouched()
    {
        AssertNothingWritten();
        _random.DidNotReceive().NextDouble();
    }

    [TestMethod]
    public void Apply_ToggleOff_WritesNothing_AndNeverRolls()
    {
        _settings.Enabled.Returns(false);

        _service.Apply();

        AssertDiceUntouched();
    }

    [TestMethod]
    public void Apply_AtSea_WritesNothing_AndNeverRolls()
    {
        _text.EncounteredAtSea.Returns(true);

        _service.Apply();

        AssertDiceUntouched();
    }

    [TestMethod]
    public void Apply_OnADedicatedServer_WritesNothing_AndNeverRolls()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        _service.Apply();

        AssertDiceUntouched();
    }

    [TestMethod]
    public void Apply_WhileAMessengerConversationIsOpen_WritesNothing_AndNeverRolls()
    {
        _messenger.IsOpen.Returns(true);

        _service.Apply();

        AssertDiceUntouched();
    }

    [TestMethod]
    public void Apply_WithNoFaction_WritesNothing_AndNeverRolls()
    {
        _text.EncounteredFactionId.Returns((string?)null);

        _service.Apply();

        AssertDiceUntouched();
    }

    [TestMethod]
    public void Apply_WhenTheGreetingRowIsMissing_WritesNoUltimatum()
    {
        _text.TrySetGreeting(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        _service.Apply();

        _text.Received(1).TrySetGreeting("str_taom_bv_greeting", "dunland_raiders_harmless");
        _text.DidNotReceiveWithAnyArgs().SetUltimatum(default!);
    }

    [TestMethod]
    public void Apply_ForAFactionWithNoRows_WritesNoUltimatum()
    {
        _text.EncounteredFactionId.Returns("looters");
        _text.TrySetGreeting("str_taom_bv_greeting", "looters_harmless").Returns(false);

        _service.Apply();

        _text.DidNotReceiveWithAnyArgs().SetUltimatum(default!);
    }

    [TestMethod]
    public void Apply_AsksOnlyAboutTheVariationItPicked()
    {
        _random.NextDouble().Returns(0.2);

        _service.Apply();

        _text.Received(1).TrySetGreeting(Arg.Any<string>(), Arg.Any<string>());
        _text.Received(1).TrySetGreeting("str_taom_bv_greeting", "dunland_raiders_brothers");
    }

    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(0.05)]
    [DataRow(0.1249)]
    public void Apply_ARollInTheLowerHalfOfTheChance_GivesHarmless_WithVanillasHarmlessUltimatum(double roll)
    {
        _random.NextDouble().Returns(roll);

        _service.Apply();

        _text.Received(1).TrySetGreeting("str_taom_bv_greeting", "dunland_raiders_harmless");
        _text.Received(1).SetUltimatum(Arg.Is<string>(s => s.StartsWith("{=A5IJmN0X}", StringComparison.Ordinal)));
    }

    [DataTestMethod]
    [DataRow(0.125)]
    [DataRow(0.2)]
    [DataRow(0.2499)]
    public void Apply_ARollInTheUpperHalfOfTheChance_GivesBrothers_WithVanillasBrothersUltimatum(double roll)
    {
        _random.NextDouble().Returns(roll);

        _service.Apply();

        _text.Received(1).TrySetGreeting("str_taom_bv_greeting", "dunland_raiders_brothers");
        _text.Received(1).SetUltimatum(Arg.Is<string>(s => s.StartsWith("{=ASRpFaGF}", StringComparison.Ordinal)));
    }

    [DataTestMethod]
    [DataRow(0.25)]
    [DataRow(0.5)]
    [DataRow(0.9999)]
    public void Apply_ARollAtOrAboveTheChance_WithNoBucketSource_WritesNothing(double roll)
    {
        _random.NextDouble().Returns(roll);

        _service.Apply();

        AssertNothingWritten();
    }

    [TestMethod]
    public void Apply_NotAFirstMeeting_EvenOnALowRoll_WritesNothing_AndNeverRolls()
    {
        // The first-meeting flag is wired through: a service that passed true here would write on a met party.
        _text.IsFirstMeeting().Returns(false);
        _random.NextDouble().Returns(0.0);

        _service.Apply();

        AssertDiceUntouched();
    }

    private IEncounterBucketSource WithBuckets(string? bucket = "foe")
    {
        var buckets = Substitute.For<IEncounterBucketSource>();
        buckets.BanditBucket(Arg.Any<string?>()).Returns(bucket);
        _service = new BanditVoicesService(_settings, _random, _dedicated, _messenger, _text, buckets);
        return buckets;
    }

    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(0.2499)]
    public void Apply_WithBuckets_ARollBelowAQuarter_GivesTheLegacyGreeting_AndNoBucketGreeting(double roll)
    {
        var buckets = WithBuckets();
        _random.NextDouble().Returns(roll);

        _service.Apply();

        _text.Received(1).TrySetGreeting(Arg.Any<string>(), Arg.Any<string>());
        _text.Received(1).TrySetGreeting("str_taom_bv_greeting", Arg.Any<string>());
        buckets.DidNotReceiveWithAnyArgs().BanditBucket(default);
    }

    [TestMethod]
    public void Apply_WithBuckets_ALegacyRollWhoseRowIsMissing_FallsToTheBucketGreeting()
    {
        WithBuckets();
        _random.NextDouble().Returns(0.1);
        _text.TrySetGreeting("str_taom_bv_greeting", Arg.Any<string>()).Returns(false);

        _service.Apply();

        _text.Received(1).TrySetGreeting("str_taom_enc_bandit_greet", "dunland_raiders_foe_1");
        _text.Received(1).SetUltimatum(BanditVoicesService.HarmlessUltimatum);
    }

    [DataTestMethod]
    [DataRow(0.0, "_1")]
    [DataRow(0.1249, "_1")]
    [DataRow(0.125, "_2")]
    [DataRow(0.2499, "_2")]
    public void Apply_WithBuckets_ALegacyRollWhoseRowIsMissing_SplitsItsQuarterEvenly(double roll, string n)
    {
        WithBuckets();
        _random.NextDouble().Returns(roll);
        _text.TrySetGreeting("str_taom_bv_greeting", Arg.Any<string>()).Returns(false);

        _service.Apply();

        _text.Received(1).TrySetGreeting("str_taom_enc_bandit_greet", "dunland_raiders_foe" + n);
    }

    [DataTestMethod]
    [DataRow(true, 0.25)]
    [DataRow(false, 0.0)]
    public void BucketVariant_SplitsTheWholeBucketBandEvenly_WithAndWithoutLegacyRows(bool clanHasLegacyRows, double bandStart)
    {
        const int steps = 10000;
        int ones = 0, twos = 0;
        for (var i = 0; i < steps; i++)
        {
            var roll = bandStart + (BanditVoicesService.BucketChance - bandStart) * i / steps;
            if (BanditVoicesService.BucketVariant(roll) == 1)
                ones++;
            else
                twos++;
        }

        Assert.AreEqual(ones, twos, clanHasLegacyRows ? "a clan with legacy rows" : "a clan without legacy rows");
    }

    [DataTestMethod]
    [DataRow(0.25, "_1")]
    [DataRow(0.4999, "_1")]
    [DataRow(0.5, "_2")]
    [DataRow(0.7499, "_2")]
    public void Apply_WithBuckets_ARollFromAQuarterToThreeQuarters_GivesTheBucketGreeting(double roll, string n)
    {
        var buckets = WithBuckets("kin");
        _random.NextDouble().Returns(roll);

        _service.Apply();

        buckets.Received(1).BanditBucket(Clan);
        _text.Received(1).TrySetGreeting(Arg.Any<string>(), Arg.Any<string>());
        _text.Received(1).TrySetGreeting("str_taom_enc_bandit_greet", "dunland_raiders_kin" + n);
        _text.Received(1).SetUltimatum(Arg.Is<string>(s => s.StartsWith("{=A5IJmN0X}", StringComparison.Ordinal)));
    }

    [DataTestMethod]
    [DataRow(0.75)]
    [DataRow(0.9999)]
    public void Apply_WithBuckets_ARollAtOrAboveThreeQuarters_WritesNothing(double roll)
    {
        WithBuckets();
        _random.NextDouble().Returns(roll);

        _service.Apply();

        AssertNothingWritten();
    }

    [TestMethod]
    public void Apply_WithBuckets_NoBucketForTheClan_WritesNothing()
    {
        WithBuckets(null);
        _random.NextDouble().Returns(0.5);

        _service.Apply();

        AssertNothingWritten();
    }

    [TestMethod]
    public void Apply_WithBuckets_AMissingBucketRow_WritesNoUltimatum()
    {
        WithBuckets();
        _random.NextDouble().Returns(0.5);
        _text.TrySetGreeting("str_taom_enc_bandit_greet", Arg.Any<string>()).Returns(false);

        _service.Apply();

        _text.DidNotReceiveWithAnyArgs().SetUltimatum(default!);
    }

    [TestMethod]
    public void Apply_WithBuckets_NotAFirstMeeting_NeverAsksForABucket()
    {
        var buckets = WithBuckets();
        _text.IsFirstMeeting().Returns(false);
        _random.NextDouble().Returns(0.5);

        _service.Apply();

        AssertDiceUntouched();
        buckets.DidNotReceiveWithAnyArgs().BanditBucket(default);
    }
}
