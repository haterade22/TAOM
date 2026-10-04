using TAOM.Dependencies;

namespace TAOM.Core.Logging;

/// <summary>
/// Hands TAOM.Dependencies' startup log lines to Main's logger (#725).
///
/// TAOM.Dependencies loads before Main, so what it logs while loading goes into EarlyLog's buffer:
/// the Harmony fork version, the AssemblyResolve redirects, the UnpatchAll guard, and the error
/// for a second 0Harmony.dll in the process. EarlyLog.DrainTo flushes that buffer into a logger and
/// sends later lines straight to it, but nothing ever called it, so every one of those lines was
/// lost. SubModule.OnSubModuleLoad calls Connect right after IoC.Configure().
/// </summary>
public static class EarlyLogBridge
{
    public static void Connect(IModLogger logger)
    {
        EarlyLog.DrainTo((level, message) => Write(logger, level, message));
    }

    // Never throws. After Connect, EarlyLog runs this from the AssemblyResolve handler, the UnpatchAll(null)
    // guard prefix and the catch blocks in Dependencies' OnGameInitializationFinished. There, an exception
    // would fail an assembly load, abort another mod's UnpatchAll call or escape into game init. Inside the
    // flush it would also leave every line queued behind the failing one unlogged.
    private static void Write(IModLogger logger, string level, string message)
    {
        try
        {
            switch (level)
            {
                case "INFO":
                    logger.LogInfo(message);
                    break;
                case "WARNING":
                    logger.LogWarning(message);
                    break;
                case "ERROR":
                    logger.LogError(message);
                    break;
                default: // a level EarlyLog gains later keeps its name, so it cannot pass for a routine INFO line
                    logger.LogInfo($"[{level}] {message}");
                    break;
            }
        }
        catch
        {
            // A logger that throws costs this one line and nothing else.
        }
    }
}
