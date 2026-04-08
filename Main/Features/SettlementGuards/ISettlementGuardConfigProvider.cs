using System.Collections.Generic;
using TAOM.Features.SettlementGuards.Domain;

namespace TAOM.Features.SettlementGuards;

public interface ISettlementGuardConfigProvider
{
    GuardPool GetBySettlementId(string settlementId);
    GuardPool GetByClanId(string clanId);
    GuardPool GetByCultureId(string cultureId);
    string GetSpearItemId(string cultureId);
    IReadOnlyDictionary<string, string> GetSpearMappings();
}
