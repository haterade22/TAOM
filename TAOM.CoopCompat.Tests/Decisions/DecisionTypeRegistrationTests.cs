using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Kingdoms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.CoopCompat.Decisions;
using TAOM.Features.FiefGranting;

namespace TAOM.CoopCompat.Tests.Decisions;

/// <summary>
/// #1346: TAOM's fief election reaches Coop's registry, so Coop sends it to clients and players vote
/// on it. Coop compares these names at join, so a type missing here refuses no one; it silently hands
/// every TAOM fief election to the host's AI.
/// </summary>
[TestClass]
public class DecisionTypeRegistrationTests
{
    [TestMethod]
    public void TryRegister_PutsTheFiefElectionInCoopsRegistry_AndLogsIt_OncePerProcess()
    {
        var lines = new List<string>();

        // The main menu and the first campaign start both call it; a dedicated server reaches only the second.
        DecisionTypeRegistration.TryRegister(lines.Add);
        DecisionTypeRegistration.TryRegister(lines.Add);

        CollectionAssert.Contains(KingdomDecisionTypeRegistry.GetTypeNames(), typeof(TaomSettlementClaimantDecision).FullName);
        Assert.IsTrue(lines.Single().Contains("fief elections sync"), lines.Single());
    }
}
