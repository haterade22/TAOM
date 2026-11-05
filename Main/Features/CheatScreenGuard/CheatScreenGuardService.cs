using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>
/// Decides whether a cheat list opens (docs/features/cheat-screen-guard.md). A setting that cannot be read counts as off,
/// the shipped default. The first hidden list per launch writes one log line and one chat line. Main thread.
/// </summary>
public sealed class CheatScreenGuardService : ICheatScreenGuardService
{
    internal const string NoticeKey = "taom_cheat_screen_guard_notice";
    internal const string NoticeFallback = "TAOM: cheat mode is on. Its every-item inventory and every-troop party lists take gigabytes of memory, so TAOM opened the normal screens. To get the cheat lists, turn on Cheat Mode Lists All Items And Troops in MCM, TAOM Battle Load Diagnostics, Map Performance.";

    private readonly ICheatScreenGuardSettingsProvider _settings;
    private readonly IInquiryAdapter _notice;
    private readonly IModLogger _logger;
    private bool _noticed;

    public CheatScreenGuardService(ICheatScreenGuardSettingsProvider settings, IInquiryAdapter notice, IModLogger logger)
    {
        _settings = settings;
        _notice = notice;
        _logger = logger;
    }

    public bool ShowCheatList(CheatScreen screen)
    {
        if (ListsEnabled()) return true;
        if (_noticed) return false;
        _noticed = true;

        try { _logger.LogInfo(CheatScreenGuardLines.BuildHiddenLine(screen)); }
        catch { }
        try { _notice.ShowMessage(NoticeKey, NoticeFallback, null, null); }
        catch { }
        return false;
    }

    public void LogInstall(int inventorySwapped, int partySwapped)
    {
        try
        {
            if (inventorySwapped == 1 && partySwapped == 1)
                _logger.LogInfo(CheatScreenGuardLines.BuildInstallLine());
            else
                _logger.LogWarning(CheatScreenGuardLines.BuildPartialInstallLine(inventorySwapped, partySwapped));
        }
        catch { }
    }

    private bool ListsEnabled()
    {
        try { return _settings.CheatListsEnabled; }
        catch { return false; }
    }
}
