using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.Localization;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Adapters;

/// <summary>
/// One vanilla <see cref="Incident"/> as the service sees it. A text is written in place into the incident's own
/// <see cref="TextObject"/> through its public <c>Value</c> field, followed by <c>CacheTokens()</c> (the token cache is keyed by
/// language only, so without it the old words stay on screen); the variables vanilla's conditions set live in the object's
/// attributes and survive. The first write to a slot remembers vanilla's own value, in a process-wide table keyed by incident id
/// (vanilla's values do not change between campaigns), so a later call that has no TAOM text for the slot puts vanilla's back.
/// Built per call by the patch; it keeps nothing but the incident it was built for.
/// </summary>
public sealed class IncidentTextHandle : IIncidentTexts
{
    private static readonly Dictionary<string, Dictionary<string, string?>> Originals = new();

    private readonly Incident _incident;

    public IncidentTextHandle(Incident incident) => _incident = incident;

    public string Id => _incident.StringId ?? string.Empty;

    public int OptionCount => _incident.NumOfOptions;

    public void Set(string slot, string? rawText)
    {
        var target = Slot(slot);
        if (target == null)
            return;

        if (!Originals.TryGetValue(Id, out var saved))
            Originals[Id] = saved = new Dictionary<string, string?>();
        if (!saved.ContainsKey(slot))
            saved[slot] = target.Value;

        var value = rawText ?? saved[slot];
        if (value == null || value == target.Value)
            return;
        target.Value = value;
        target.CacheTokens();
    }

    public void SetVariable(string name, string rawText)
    {
        foreach (var slot in IncidentTextKeys.Slots(_incident.NumOfOptions))
            Slot(slot)?.SetTextVariable(name, new TextObject(rawText));
    }

    public void SetPlace(string name, string value) => _incident.Description?.SetTextVariable(name, value);

    private TextObject? Slot(string slot)
    {
        if (slot == IncidentTextKeys.Title)
            return _incident.Title;
        if (slot == IncidentTextKeys.Description)
            return _incident.Description;
        var n = IncidentTextKeys.OptionNumber(slot);
        return n >= 1 && n <= _incident.NumOfOptions ? _incident.GetOptionText(n - 1) : null;
    }
}
