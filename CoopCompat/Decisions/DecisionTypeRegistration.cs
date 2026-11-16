using System;
using System.Runtime.CompilerServices;
using TAOM.Core.Logging;
using TAOM.Features.FiefGranting;

namespace TAOM.CoopCompat.Decisions;

/// <summary>
/// Registers TAOM's kingdom decision types with Coop (#1346), so Coop sends a TAOM fief election to
/// every client and players vote on it. Coop compares the registered types when a player joins, so
/// this runs before any host or join: from the main menu on a client or a listen host, and at the
/// first campaign start on the dedicated server, where the main-menu hook does not fire under the
/// Coop host. Once per process.
/// </summary>
internal static class DecisionTypeRegistration
{
    public const string RegisteredCheck = "taom.coopcompat.fief-election.registered";

    private static bool _tried;

    public static void TryRegister(Action<string> log)
    {
        if (_tried) return;
        _tried = true;
        try
        {
            Register();
            log("[CoopCompat] fief elections sync to players");
            Outcomes.Pass(RegisteredCheck, "", "type=" + nameof(TaomSettlementClaimantDecision));
        }
        catch (Exception ex)
        {
            // A Coop without the registry: the host's AI decides TAOM fief elections, as before.
            log("[CoopCompat] fief elections not synced: " + ex.GetType().Name + ": " + ex.Message);
            Outcomes.Fail(RegisteredCheck, "", "reason=" + ex.GetType().Name);
        }
    }

    // The only method here that names a Coop-backed type; NoInlining keeps the JIT from resolving it in TryRegister.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register() =>
        CoopDecisionTypes.Register<TaomSettlementClaimantDecision>(FiefClaimantDecisionCodec.Write, FiefClaimantDecisionCodec.Read);
}
