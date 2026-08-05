using System;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// The single exit pipeline — the ONLY code allowed to end a service term. Fixed order,
/// unconditional: restore party presence FIRST, then clear the record, then notify
/// consequence subscribers. Every <see cref="DischargeReason"/> runs the same pipeline;
/// the donor mod's commission-exit softlock (record cleared, presence never restored) is
/// unrepresentable here.
/// </summary>
public interface IDischargeService
{
    /// <summary>Run the discharge pipeline. False (nothing changed) when not enlisted.</summary>
    bool Execute(DischargeReason reason);

    /// <summary>Raised once per completed discharge, AFTER the record is cleared. Consequence layers subscribe.</summary>
    event Action<DischargeReason> EnlistmentEnded;
}
