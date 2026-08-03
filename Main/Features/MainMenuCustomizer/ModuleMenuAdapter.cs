using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MainMenuCustomizer;

/// <summary>
/// Thin wrapper over <see cref="Module"/>'s initial-state options. Deliberately holds no logger:
/// a miss here is reported to the caller as <c>false</c> so the service can dedupe it, because this
/// runs once per screen-root set and a headless server does that thousands of times per boot.
/// </summary>
public class ModuleMenuAdapter : IModuleMenuAdapter
{
    public bool HideOption(string id)
    {
        var existing = Module.CurrentModule.GetInitialStateOptionWithId(id);
        if (existing == null)
            return false;

        // Note: InitialStateOption constructor calls IsDisabledAndReason() immediately as a validation
        // side-effect. Re-using existing.IsDisabledAndReason is safe here because the original
        // option was registered and fully initialized before OnBeforeInitialModuleScreenSetAsRoot fires.
        Module.CurrentModule.OverrideInitialStateOption(id, new InitialStateOption(
            existing.Id,
            existing.Name,
            existing.OrderIndex,
            existing.DoAction,
            existing.IsDisabledAndReason,
            existing.EnabledHint,
            () => true));
        return true;
    }

    public bool RenameOption(string id, string newName)
    {
        var existing = Module.CurrentModule.GetInitialStateOptionWithId(id);
        if (existing == null)
            return false;

        // Note: InitialStateOption constructor calls IsDisabledAndReason() immediately as a validation
        // side-effect. Re-using existing.IsDisabledAndReason is safe here — see HideOption comment.
        Module.CurrentModule.OverrideInitialStateOption(id, new InitialStateOption(
            existing.Id,
            new TextObject(newName),
            existing.OrderIndex,
            existing.DoAction,
            existing.IsDisabledAndReason,
            existing.EnabledHint,
            existing.IsHidden));
        return true;
    }
}
