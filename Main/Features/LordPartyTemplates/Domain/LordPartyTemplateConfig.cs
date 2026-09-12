using System;
using System.Collections.Generic;

namespace TAOM.Features.LordPartyTemplates.Domain;

/// <summary>
/// Shape of <c>ModuleData/lord_party_templates/lord_party_templates.json</c>: hero StringId to
/// party template StringId, both bare (no <c>Hero.</c> / <c>PartyTemplate.</c> prefix). A hero
/// listed here fields the named template instead of the clan binding vanilla would give him.
/// </summary>
public class LordPartyTemplateConfig
{
    public Dictionary<string, string> Overrides { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
