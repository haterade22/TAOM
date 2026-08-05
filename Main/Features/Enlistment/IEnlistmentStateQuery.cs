using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Cross-feature read facade over the enlistment state. Consumers: FieldCommission
/// (suppress promotion offers while enlisted), FiefManagement's F6 gate, and the
/// leader-keyed attribution audits. Registered in root IoC; a null-object implementation
/// keeps consumers working if the feature is disabled.
/// </summary>
public interface IEnlistmentStateQuery
{
    bool IsEnlisted { get; }

    EnlistmentState State { get; }

    string CommanderHeroId { get; }

    string EnlistedHeroId { get; }

    double? ContractEndDay { get; }

    /// <summary>True when the given party is the enlisted commander's CURRENT party (live lookup, never cached).</summary>
    bool IsCommanderParty(string partyId);
}
