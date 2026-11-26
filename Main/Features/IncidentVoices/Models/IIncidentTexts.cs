namespace TAOM.Features.IncidentVoices.Models;

/// <summary>
/// One vanilla incident as the service sees it: its id and the text slots it shows. The patch builds one from the engine's
/// <c>Incident</c> and no engine type crosses into the service (ADR-007).
/// </summary>
public interface IIncidentTexts
{
    string Id { get; }

    /// <summary>The number of options vanilla registered.</summary>
    int OptionCount { get; }

    /// <summary>
    /// Writes <paramref name="rawText"/> (<c>{=key}English</c>) into <paramref name="slot"/> (<c>title</c>, <c>desc</c>, <c>optN</c>); null puts
    /// vanilla's own text back. A slot the incident does not have is ignored.
    /// </summary>
    void Set(string slot, string? rawText);

    /// <summary>Sets <c>{name}</c> on every slot to the text <paramref name="rawText"/>.</summary>
    void SetVariable(string name, string rawText);

    /// <summary>Sets <c>{name}</c> on the description to a plain value (a place's name), over what vanilla's condition set.</summary>
    void SetPlace(string name, string value);
}
