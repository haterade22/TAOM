using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The pure half of the Fords of War start flow: what each main-menu prompt offers, and how loose text (the console command's
/// arguments) becomes a <see cref="FordsMatchSetup"/>. No engine type, so the rows and the defaults are tested without a game.
/// Ids match case-insensitively, like the config catalog.
/// </summary>
internal static class FordsSetupChoices
{
    private const string NormalProfile = "normal";
    private static readonly string[] ProfileOrder = { "easy", "normal", "hard" };

    /// <summary>The menu entry shows only when there is a faction and a map to start with.</summary>
    internal static bool HasContent(FordsConfigCatalog catalog) => catalog.Factions.Count > 0 && catalog.Maps.Count > 0;

    internal static IReadOnlyList<FordsChoice> Factions(FordsConfigCatalog catalog) =>
        catalog.Factions.Select(f => new FordsChoice(f.FactionId, catalog.FactionDisplayName(f.FactionId))).ToList();

    internal static IReadOnlyList<FordsChoice> Maps(FordsConfigCatalog catalog) =>
        catalog.Maps.Select(m => new FordsChoice(m.SceneId, m.DisplayName)).ToList();

    /// <summary>Easy, normal and hard first (in that order), then any profile a file added, alphabetically.</summary>
    internal static IReadOnlyList<FordsChoice> Difficulties(IReadOnlyDictionary<string, FordsAiProfile> profiles)
    {
        var ids = profiles.Keys
            .OrderBy(id => IndexOfShipped(id))
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ids.Select(id => new FordsChoice(id, DifficultyLabel(id))).ToList();
    }

    /// <summary>The map's own lane count is the match's lane count.</summary>
    internal static FordsMatchSetup Compose(MapEntry map, string westFaction, string eastFaction, string aiProfile) =>
        new(map.SceneId, westFaction, eastFaction, aiProfile, map.Options.LaneCount);

    /// <summary>
    /// A setup from optional text. Missing or blank means the default: the first faction for West, the first other faction for
    /// East (the same one when only one exists), <c>normal</c> (else the first profile) and the first map. A named value must
    /// exist; the error names the valid ids.
    /// </summary>
    internal static bool TryResolve(FordsConfigCatalog catalog, string? west, string? east, string? profile, string? scene,
        out FordsMatchSetup setup, out string error)
    {
        setup = null!;
        error = "";
        var factions = catalog.Factions;
        if (factions.Count == 0)
        {
            error = "Fords of War has no factions configured (fords_of_war/factions.json).";
            return false;
        }

        if (catalog.Maps.Count == 0)
        {
            error = "Fords of War has no maps configured (fords_of_war/maps.json).";
            return false;
        }

        var factionIds = factions.Select(f => f.FactionId).ToList();
        if (!TryPick(west, factionIds, factionIds[0], "faction", out var westId, out error))
            return false;

        var eastDefault = factionIds.FirstOrDefault(id => !SameId(id, westId)) ?? westId;
        if (!TryPick(east, factionIds, eastDefault, "faction", out var eastId, out error))
            return false;

        var profileIds = Difficulties(catalog.Match.AiProfiles).Select(c => c.Id).ToList();
        var profileDefault = profileIds.FirstOrDefault(id => SameId(id, NormalProfile)) ?? profileIds.FirstOrDefault() ?? NormalProfile;
        if (!TryPick(profile, profileIds, profileDefault, "AI profile", out var profileId, out error))
            return false;

        var sceneIds = catalog.Maps.Select(m => m.SceneId).ToList();
        if (!TryPick(scene, sceneIds, sceneIds[0], "scene", out var sceneId, out error))
            return false;

        catalog.TryGetMap(sceneId, out var map);
        setup = Compose(map, westId, eastId, profileId);
        return true;
    }

    private static bool TryPick(string? text, IReadOnlyList<string> valid, string fallback, string what, out string picked, out string error)
    {
        error = "";
        picked = fallback;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var match = valid.FirstOrDefault(id => SameId(id, text!.Trim()));
        if (match != null)
        {
            picked = match;
            return true;
        }

        error = $"Unknown {what} '{text!.Trim()}'. Valid: {string.Join(", ", valid)}.";
        return false;
    }

    private static bool SameId(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static int IndexOfShipped(string id)
    {
        var index = Array.FindIndex(ProfileOrder, shipped => SameId(shipped, id));
        return index < 0 ? ProfileOrder.Length : index;
    }

    private static string DifficultyLabel(string id) => id.ToLowerInvariant() switch
    {
        "easy" => "{=TAOM_FOW_Difficulty_Easy}Easy",
        "normal" => "{=TAOM_FOW_Difficulty_Normal}Normal",
        "hard" => "{=TAOM_FOW_Difficulty_Hard}Hard",
        _ => "{=!}" + id,
    };
}
