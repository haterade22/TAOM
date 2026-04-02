using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.MainMenuCustomizer;

public class ModuleMenuAdapter : IModuleMenuAdapter
{
    private readonly IModLogger _logger;

    public ModuleMenuAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public void HideOption(string id)
    {
        var existing = Module.CurrentModule.GetInitialStateOptionWithId(id);
        if (existing == null)
        {
            _logger.LogWarning($"MainMenuCustomizer: option '{id}' not found, skipping hide");
            return;
        }

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
    }

    public void RenameOption(string id, string newName)
    {
        var existing = Module.CurrentModule.GetInitialStateOptionWithId(id);
        if (existing == null)
        {
            _logger.LogWarning($"MainMenuCustomizer: option '{id}' not found, skipping rename");
            return;
        }

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
    }
}
