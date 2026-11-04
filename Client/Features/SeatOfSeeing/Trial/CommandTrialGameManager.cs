using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>Loads the field trial (build-spec section 12): a campaign-free <see cref="CustomGame"/> from the main menu, then the battle, in the shape of <c>LongDefeatGameManager</c>.</summary>
public sealed class CommandTrialGameManager : CustomGameManager
{
    private readonly CommandTrialOptions _options;
    private readonly IModLogger _logger;

    internal CommandTrialGameManager(CommandTrialOptions options, IModLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public override void OnLoadFinished()
    {
        // The base pushes the custom battle state; the launcher then opens the mission on top.
        base.OnLoadFinished();
        var refusal = new CommandTrialLauncher(_options, _logger).Launch();
        if (refusal == null)
            return;

        _logger.LogError($"[SeatTrial] the launcher opened no mission ({refusal}); the run ends at the main menu");
        CommandTrialRun.Finish(refusal);
    }
}
