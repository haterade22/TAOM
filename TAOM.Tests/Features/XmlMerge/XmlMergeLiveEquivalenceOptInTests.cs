using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Keeps the live harness inside /verify-bindings (the maintainer's review follow-up of 2026-10-03). A default run
/// skips the harness unless TAOM_RUN_BENCHMARKS=1, and the binding gate reaches it by category: lose
/// <c>BindingVerification</c> and /verify-bindings stops running it without one red test. The harness needs the game,
/// so this pin sits in a class that does not and runs in the default suite and on hosted CI. The gate's other half,
/// the variable in binding-gate.runsettings, is pinned in BindingGateRunSettingsTests.
/// </summary>
[TestClass]
public class XmlMergeLiveEquivalenceOptInTests
{
    [TestMethod]
    public void LiveEquivalenceTests_Categories_IncludeBindingVerification()
    {
        // Arrange
        var type = typeof(XmlMergeLiveEquivalenceTests);

        // Act
        var categories = type.GetCustomAttributes(typeof(TestCategoryAttribute), inherit: false)
            .Cast<TestCategoryAttribute>()
            .SelectMany(a => a.TestCategories)
            .ToList();

        // Assert
        CollectionAssert.Contains(categories, "BindingVerification",
            "XmlMergeLiveEquivalenceTests lost BindingVerification: /verify-bindings would no longer run the live merge harness.");
    }
}
