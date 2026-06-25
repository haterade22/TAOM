using System;
using System.Collections.Generic;

namespace TAOM.Features.NazgulFamily;

/// <summary>
/// The lore-fixed roster of the Nine Ringwraiths modelled in TAOM — the Witch-King of Angmar,
/// Khamûl the Easterling (second of the Nine), and the seven other Nazgûl. IDs verified against the
/// canonical Hero definitions in <c>characters/heroes.xslt</c> (the wraith family graph the feature
/// strips). Note Khamûl (<c>lord_1_48</c>) carries a different <c>skill_template</c> than the other
/// eight (he is statted as an orc-warrior in <c>taom_lord_skill_sets.xml</c>), so a skill_template-only
/// scope would miss him — he is a canonical Nazgûl regardless. This is a fixed lore set, not a tuning
/// knob, so it is a compiled constant rather than a config file (no behaviour to misconfigure).
/// </summary>
public sealed class NazgulRegistry : INazgulRegistry
{
    // OrdinalIgnoreCase to match the codebase convention (InitialChildGenerationService.IsExcluded);
    // Hero StringIds are lowercase, so this is a defensive superset, not a behaviour change.
    private static readonly HashSet<string> WraithIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "lord_1_15",   // Witch-King of Angmar
        "lord_1_155",  // Nazgûl, The Dark Marshall
        "lord_1_16",   // Nazgûl, The Knight of Umbar
        "lord_1_28",   // Nazgûl, The Betrayer
        "lord_1_38",   // Nazgûl, the Undying
        "lord_1_48",   // Khamûl the Easterling — second of the Nine (vanilla spouse of lord_1_48_1)
        "lord_1_48_1", // Nazgûl, the Tainted
        "lord_1_48_2", // Nazgûl, the Shadow of Northmen
        "lord_1_48_3", // Nazgûl, the Shadow of Umbar
    };

    public bool IsWraith(string heroStringId)
        => !string.IsNullOrEmpty(heroStringId) && WraithIds.Contains(heroStringId);
}
