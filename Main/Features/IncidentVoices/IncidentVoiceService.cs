using System;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// The engine-free choices behind Incident Voices (Patch176, docs/features/incident-voices.md): after vanilla's own condition
/// said an incident may show, whether the player's people and side may see it, which voice reads it, which place of the
/// player's own side it names, and which TAOM texts replace vanilla's. Every answer is "leave vanilla" for an incident the
/// catalog does not list, a text that has no row, a dedicated server or a co-op session. It holds no per-campaign state.
/// </summary>
internal sealed class IncidentVoiceService
{
    private readonly IIncidentCatalogProvider _catalog;
    private readonly IIncidentVoiceAdapter _adapter;
    private readonly IEncounterGroupsConfigProvider _groups;
    private readonly IAlignmentService _alignment;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly ICoopPresenceProvider _coop;

    public IncidentVoiceService(
        IIncidentCatalogProvider catalog,
        IIncidentVoiceAdapter adapter,
        IEncounterGroupsConfigProvider groups,
        IAlignmentService alignment,
        IDedicatedServerProvider dedicated,
        ICoopPresenceProvider coop)
    {
        _catalog = catalog;
        _adapter = adapter;
        _groups = groups;
        _alignment = alignment;
        _dedicated = dedicated;
        _coop = coop;
    }

    private bool Active => !_dedicated.IsDedicatedServer && !_coop.IsCoopActive;

    /// <summary>
    /// The answer for <c>Incident.CanIncidentBeInvoked</c> once vanilla's condition gave <paramref name="vanillaAllows"/>. A false
    /// stays false and writes nothing. For a true: a closed incident, or one whose place has no candidate on the player's side,
    /// answers false; otherwise the place variables and the texts are written and true is answered.
    /// </summary>
    internal bool Review(IIncidentTexts incident, bool vanillaAllows)
    {
        if (!vanillaAllows || !Active)
            return vanillaAllows;
        var rule = _catalog.GetCatalog().Find(incident.Id);
        if (rule == null)
            return vanillaAllows;
        var player = _adapter.Player();
        if (player == null)
            return vanillaAllows;

        var people = PeopleOf(player);
        var version = IncidentVersionPicker.Pick(SideOf(player), people.Group);
        if (!IncidentEligibility.Allowed(rule, version, people))
            return false;

        foreach (var fix in rule.Places)
        {
            var place = IncidentPlacePicker.Pick(_adapter.Places(fix.Kind), p => PlaceRank(p, player, people, version), _adapter.IncidentSeed, rule.Id);
            if (place == null)
                return false;
            incident.SetPlace(fix.Variable, place.Name);
        }

        if (!rule.VanillaText)
            WriteTexts(rule, version, incident, player.Side.CultureId == IncidentTextKeys.IsengardCulture ? player.Side.CultureId : player.CultureId, people.Group);
        return true;
    }

    /// <summary>The console's reason the incident typed as an id is closed for this player, or null when it is open (or unknown).</summary>
    internal string? ClosedReason(string typedId)
    {
        if (!Active)
            return "Incident Voices stands down on a dedicated server and in a co-op session.";
        var rule = _catalog.GetCatalog().FindLoose(typedId);
        var player = _adapter.Player();
        if (rule == null || player == null)
            return null;
        var people = PeopleOf(player);
        return IncidentEligibility.ClosedReason(rule, IncidentVersionPicker.Pick(SideOf(player), people.Group), people);
    }

    /// <summary>The full id of an incident typed as <c>incident_x</c> or <c>x</c>, or null when the catalog does not list it.</summary>
    internal string? ResolveId(string typedId) => _catalog.GetCatalog().FindLoose(typedId)?.Id;

    private void WriteTexts(IncidentRule rule, IncidentVersion version, IIncidentTexts incident, string? cultureId, string? group)
    {
        var token = TokenFor(rule, version);
        var any = false;
        foreach (var slot in IncidentTextKeys.Slots(Math.Min(rule.Options, incident.OptionCount)))
        {
            var raw = _adapter.Text(IncidentTextKeys.Variation(rule.ShortId, token, slot));
            incident.Set(slot, raw);
            any |= raw != null;
        }
        if (!any)
            return;

        foreach (var word in _catalog.GetCatalog().Vocabulary)
        {
            var raw = VocabText(word, cultureId, group, version);
            if (raw != null)
                incident.SetVariable(word, raw);
        }
    }

    /// <summary>A vocabulary word in the player's own people's words when a row exists, else the voice's row, else null.</summary>
    private string? VocabText(string word, string? cultureId, string? group, IncidentVersion version)
    {
        foreach (var scope in IncidentTextKeys.VocabScopes(cultureId, group, version))
        {
            var raw = _adapter.Text(IncidentTextKeys.VocabVariation(scope, word));
            if (raw != null)
                return raw;
        }
        return null;
    }

    /// <summary>
    /// The text set a player reads: <c>all</c> for a one-text incident; for a split one the voice's own set, or the Free set when
    /// the Shadow set has no title yet (a half-written Shadow version never mixes into a Free one slot by slot).
    /// </summary>
    private string TokenFor(IncidentRule rule, IncidentVersion version)
    {
        if (rule.OneText)
            return IncidentTextKeys.All;
        var token = IncidentTextKeys.VersionToken(version);
        if (version == IncidentVersion.Shadow && _adapter.Text(IncidentTextKeys.Variation(rule.ShortId, token, IncidentTextKeys.Title)) == null)
            return IncidentTextKeys.Free;
        return token;
    }

    private PeopleFacts PeopleOf(PlayerFacts player) =>
        IncidentVersionPicker.Describe(_groups.GetMap(), player.CultureId, player.RaceName);

    private FactionSide? SideOf(PlayerFacts player) =>
        player.Side.FactionId.Length == 0 && player.Side.CultureId.Length == 0 ? null : _alignment.ResolveSide(player.Side);

    /// <summary>1 the player's own realm, 2 the player's own people, 3 the same side and voice, 0 not the player's side.</summary>
    private int PlaceRank(PlaceFacts place, PlayerFacts player, PeopleFacts people, IncidentVersion version)
    {
        if (place.Side.FactionId.Length > 0 && place.Side.FactionId == player.Side.FactionId)
            return 1;
        var map = _groups.GetMap();
        var placeGroup = map.GroupOfCulture(place.CultureId);
        if (placeGroup != null && placeGroup == people.Group)
            return 2;
        var placeSide = place.Side.FactionId.Length == 0 && place.Side.CultureId.Length == 0 ? (FactionSide?)null : _alignment.ResolveSide(place.Side);
        return IncidentVersionPicker.Pick(placeSide, placeGroup) == version ? 3 : 0;
    }
}
