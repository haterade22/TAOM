using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;

namespace TAOM.Features.BanditVoices;

/// <summary>
/// The engine-free choice behind Bandit Voices (#900): one roll on a first meeting picks the clan's legacy greeting (under
/// <see cref="Chance"/>), an Encounter Lines bucket greeting (under <see cref="BucketChance"/>, or a legacy roll whose row is
/// missing), or vanilla's text, and sets the vanilla ultimatum that answers the greeting.
/// </summary>
internal sealed class BanditVoicesService
{
    /// <summary>The chance of a clan's legacy greeting on a first meeting. Compiled, not an MCM setting.</summary>
    internal const double Chance = 0.25;

    /// <summary>A roll below this and at or above <see cref="Chance"/> asks for an Encounter Lines bucket greeting.</summary>
    internal const double BucketChance = 0.75;

    /// <summary>The game text the Encounter Lines bucket greetings live under; the variation is <c>&lt;clanId&gt;_&lt;bucket&gt;_&lt;1|2&gt;</c>.</summary>
    internal const string BucketGreetingTextId = "str_taom_enc_bandit_greet";

    /// <summary>The game text the greeting rows live under; the variation is <c>&lt;clanId&gt;_harmless</c> or <c>_brothers</c>.</summary>
    internal const string GreetingTextId = "str_taom_bv_greeting";

    /// <summary>Vanilla's reply to its own "harmless travellers" greeting, key and default verbatim.</summary>
    internal const string HarmlessUltimatum = "{=A5IJmN0X}I think not, brigand. Surrender or die!";

    /// <summary>Vanilla's reply to its own "dozens more of us hiding" greeting, key and default verbatim.</summary>
    internal const string BrothersUltimatum = "{=ASRpFaGF}Nice try, brigand. Surrender or die!";

    private readonly IBanditVoicesSettingsProvider _settings;
    private readonly IRandomSource _random;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IMessengerConversationState _messenger;
    private readonly IBanditEncounterTextAdapter _text;
    private readonly IEncounterBucketSource? _buckets;

    /// <param name="buckets">Encounter Lines' bucket source; null when that module is not registered, and then no bucket greeting is tried.</param>
    public BanditVoicesService(
        IBanditVoicesSettingsProvider settings,
        IRandomSource random,
        IDedicatedServerProvider dedicated,
        IMessengerConversationState messenger,
        IBanditEncounterTextAdapter text,
        IEncounterBucketSource? buckets = null)
    {
        _settings = settings;
        _random = random;
        _dedicated = dedicated;
        _messenger = messenger;
        _text = text;
        _buckets = buckets;
    }

    /// <summary>
    /// On a first meeting on land, rolls once. Below <see cref="Chance"/> it tries the clan's legacy greeting; when that row is
    /// missing, or the roll is below <see cref="BucketChance"/>, it tries the bucket greeting <c>&lt;clan&gt;_&lt;bucket&gt;_&lt;n&gt;</c>
    /// (n is 1 in the lower half and 2 in the upper half of the band the roll fell in, see <see cref="BucketVariant"/>). A greeting and the matching vanilla ultimatum are set together; a missing row, no bucket or a
    /// higher roll leaves vanilla's text.
    /// </summary>
    internal void Apply()
    {
        if (!_settings.Enabled || _dedicated.IsDedicatedServer || _messenger.IsOpen)
            return;
        if (_text.EncounteredAtSea || !_text.IsFirstMeeting())
            return;

        var factionId = _text.EncounteredFactionId;
        if (factionId == null)
            return;

        var roll = _random.NextDouble();
        if (roll < Chance)
        {
            var harmless = roll < Chance / 2;
            if (_text.TrySetGreeting(GreetingTextId, factionId + (harmless ? "_harmless" : "_brothers")))
            {
                _text.SetUltimatum(harmless ? HarmlessUltimatum : BrothersUltimatum);
                return;
            }
        }
        if (roll >= BucketChance)
            return;

        var bucket = _buckets?.BanditBucket(factionId);
        if (bucket == null)
            return;
        if (_text.TrySetGreeting(BucketGreetingTextId, factionId + "_" + bucket + "_" + BucketVariant(roll)))
            _text.SetUltimatum(HarmlessUltimatum);
    }

    /// <summary>
    /// The bucket greeting's number: 1 in the lower half of the band the roll fell in, 2 in the upper half. A roll from
    /// <see cref="Chance"/> to <see cref="BucketChance"/> splits at their midpoint; a roll below <see cref="Chance"/> only
    /// reaches here when the legacy row is missing, and splits at half of <see cref="Chance"/>. So a clan with legacy rows
    /// gets each number on a quarter of all meetings, and a clan without them on three eighths.
    /// </summary>
    internal static int BucketVariant(double roll) =>
        roll < Chance
            ? (roll < Chance / 2 ? 1 : 2)
            : (roll < (Chance + BucketChance) / 2 ? 1 : 2);
}
