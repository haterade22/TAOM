namespace TAOM.Features.EncounterLines;

/// <summary>The validated <c>encounter_lines/encounter_groups.json</c>. Loaded once per process (<c>Reuse.Singleton</c>): a JSON edit needs a full game restart.</summary>
internal interface IEncounterGroupsConfigProvider
{
    EncounterGroupMap GetMap();
}
