namespace TAOM.Features.MainMenuCustomizer;

public interface IModuleMenuAdapter
{
    /// <summary>Hides the option. Returns false when no option with that id is registered.</summary>
    /// <remarks>
    /// Reports the miss rather than logging it: this runs once per screen-root set, which a headless
    /// dedicated server does thousands of times per boot, so whether a miss is worth a log line is a
    /// decision only the caller can dedupe (field report 2026-08-03 §9.8).
    /// </remarks>
    bool HideOption(string id);

    /// <summary>Renames the option. Returns false when no option with that id is registered.</summary>
    bool RenameOption(string id, string newName);
}
