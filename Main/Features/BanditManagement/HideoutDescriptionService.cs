using System.Collections.Generic;

namespace TAOM.Features.BanditManagement;

/// <inheritdoc cref="IHideoutDescriptionService"/>
public sealed class HideoutDescriptionService : IHideoutDescriptionService
{
    // Biome-parallel to the five vanilla descriptions: Dunland (forest) / Gundabad (mountain) /
    // Harad (desert) / Rhûn (steppe) / Umbar (coast), plus the Wave-2 dwarf hideout (Blacklocks,
    // id kept as erebor_warriors). Default text MUST match the entries in taom_module_strings.xml
    // so the compiled fallback equals the loaded string.
    private static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>
        {
            ["dunland_raiders"] = "{=taom_hideout_desc_dunland}Among the wooded hills of Dunland you spy a rough camp of hide tents and a crude palisade, half-hidden in a clearing.",
            ["gundabad_raiders"] = "{=taom_hideout_desc_gundabad}Beneath the crags of the Misty Mountains you find a foul warren of Gundabad orcs burrowed into the bare rock.",
            ["harad_raiders"] = "{=taom_hideout_desc_harad}Out among the dunes of Harad you come upon a camouflaged Southron camp gathered about a hidden desert well.",
            ["rhun_raiders"] = "{=taom_hideout_desc_rhun}On the wide grasslands of Rhûn you make out the wagons and hide-tents of an Easterling raiding band camped in a gully.",
            ["umbar_corsairs"] = "{=taom_hideout_desc_umbar}Along the southern shore you find a sheltered cove where the black ships of the Corsairs of Umbar lie at anchor.",
            ["erebor_warriors"] = "{=taom_hideout_desc_erebor}Among the bleak hills of the east you come upon a dwarf-camp of the Blacklocks, a clan long ago fallen into shadow and ruin.",
        };

    public string? GetDescription(string cultureStringId)
    {
        if (string.IsNullOrEmpty(cultureStringId))
            return null;

        return Descriptions.TryGetValue(cultureStringId, out var text) ? text : null;
    }
}
