using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins values that other mods read from TAOM by name or by number (#1186). A change to one of
/// them compiles and can pass the other tests, and it changes what those mods do. The readers
/// are listed in docs/reference/external-mod-couplings.md.
/// </summary>
[TestClass]
public class ExternalModContractTests
{
    /// <summary>
    /// Seven standalone mods of the original developer switch themselves off when a module with
    /// the id "TAOM" is loaded, because TAOM ships a port of each. With another id both copies
    /// can activate, and two of those mods then register the same save ids as TAOM.
    /// </summary>
    [TestMethod]
    public void SubModuleXml_DeclaresTheModuleIdTaom()
    {
        var manifest = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));

        var id = manifest.Root?.Element("Id")?.Attribute("value")?.Value;

        Assert.AreEqual("TAOM", id,
            "Other mods find TAOM by this module id and switch off their own copy of a ported feature. A rename lets "
            + "both copies activate, with duplicate save ids (docs/reference/external-mod-couplings.md).");
    }

    /// <summary>New members go at the end. A reorder or an insert changes the numbers.</summary>
    [TestMethod]
    public void FactionSide_KeepsTheValuesOtherModsRead()
    {
        const string why = "TAOM_UI and RingSystem read IAlignmentService.GetKingdomSide as an integer. "
            + "Append new members; never reorder (docs/reference/external-mod-couplings.md).";

        Assert.AreEqual(0, (int)FactionSide.Free, why);
        Assert.AreEqual(1, (int)FactionSide.Evil, why);
        Assert.AreEqual(2, (int)FactionSide.Neutral, why);
    }

    /// <summary>New members go at the end. A reorder or an insert changes the numbers.</summary>
    [TestMethod]
    public void WarPhase_KeepsTheValuesOtherModsAndSavesRead()
    {
        const string why = "RingSystem reads IWarOfTheRingService.CurrentPhase as an integer, saves hold the integer, "
            + "and TAOM compares phases by number. Append new members; never reorder "
            + "(docs/reference/external-mod-couplings.md).";

        Assert.AreEqual(0, (int)WarPhase.Peace, why);
        Assert.AreEqual(1, (int)WarPhase.IsengardWar, why);
        Assert.AreEqual(2, (int)WarPhase.FullWar, why);
        Assert.AreEqual(3, (int)WarPhase.WarEnded, why);
    }
}
