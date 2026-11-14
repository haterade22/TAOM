using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines.Models;

namespace TAOM.Features.EncounterLines.Hooks;

/// <summary>
/// The one door the Encounter Lines conversation tags use (Patch149). The engine creates the tags itself with
/// <c>Activator.CreateInstance</c> (ConversationManager.InitializeTags), so they cannot take services: they ask here.
/// A missing service or any fault answers false, which leaves the row out and vanilla's line stands; the first fault
/// warns once.
/// </summary>
internal static class EncounterTagGate
{
    private static bool _reported;
    private static IModLogger? _logger;
    private static Func<CharacterObject?, LordFacts?>? _describe;

    internal static EncounterLinesService? Service { get; private set; }

    internal static void Initialize(EncounterLinesService service, Func<CharacterObject?, LordFacts?> describe, IModLogger logger)
    {
        Service = service;
        _describe = describe;
        _logger = logger;
        _reported = false;
    }

    /// <summary>True when <paramref name="character"/> speaks for the speaker group <paramref name="group"/>.</summary>
    internal static bool Speaker(CharacterObject? character, string group) =>
        Ask(character, (service, lord) => service.IsSpeaker(lord, group));

    /// <summary>True when the player stands in <paramref name="bucket"/> towards <paramref name="character"/>.</summary>
    internal static bool Bucket(CharacterObject? character, string bucket) =>
        Ask(character, (service, lord) => service.IsLordBucket(lord, bucket));

    /// <summary>True when today's surrender demand from <paramref name="character"/> is the <c>_2</c> row.</summary>
    internal static bool AltLine(CharacterObject? character) =>
        Ask(character, (service, lord) => service.IsAltLine(lord));

    private static bool Ask(CharacterObject? character, Func<EncounterLinesService, LordFacts?, bool> question)
    {
        var service = Service;
        var describe = _describe;
        if (service == null || describe == null)
            return false;

        try
        {
            return question(service, describe(character));
        }
        catch (Exception ex)
        {
            if (!_reported)
            {
                _reported = true;
                _logger?.LogWarning("[EncounterLines] a conversation tag failed, vanilla's lines stand: " + ex.Message);
            }
            return false;
        }
    }
}
