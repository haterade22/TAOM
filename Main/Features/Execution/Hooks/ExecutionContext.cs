using System.Threading;

namespace TAOM.Features.Execution.Hooks;

public static class ExecutionContext
{
    private static readonly ThreadLocal<string> VictimKingdomId = new ThreadLocal<string>();
    private static readonly ThreadLocal<string> ExecutorKingdomId = new ThreadLocal<string>();

    public static void Set(string victimKingdomId, string executorKingdomId)
    {
        VictimKingdomId.Value = victimKingdomId;
        ExecutorKingdomId.Value = executorKingdomId;
    }

    public static void Clear()
    {
        VictimKingdomId.Value = null;
        ExecutorKingdomId.Value = null;
    }

    public static string GetVictimKingdomId() => VictimKingdomId.Value;
    public static string GetExecutorKingdomId() => ExecutorKingdomId.Value;
    public static bool HasContext => VictimKingdomId.Value != null;
}
