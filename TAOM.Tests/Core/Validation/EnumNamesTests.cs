using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Validation;
using TaleWorlds.Core;

namespace TAOM.Tests.Core.Validation;

/// <summary>
/// The name-only enum parser shared by SignatureStrikes and BannerBearers. Enum.TryParse accepts
/// numbers and comma lists; this accepts a declared member name (trimmed, any case) and nothing
/// else.
/// </summary>
[TestClass]
public class EnumNamesTests
{
    [DataTestMethod]
    [DataRow("Cavalry")]
    [DataRow("cavalry")]
    [DataRow(" Cavalry ")]
    public void TryParse_DeclaredName_AnyCaseOrPadding_Parses(string name)
    {
        Assert.IsTrue(EnumNames.TryParse(name, out FormationClass value));
        Assert.AreEqual(FormationClass.Cavalry, value);
    }

    [DataTestMethod]
    [DataRow("2")]
    [DataRow("-1")]
    [DataRow("Infantry, Ranged")]
    [DataRow("Cavlary")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void TryParse_AnythingButADeclaredName_Fails(string? name)
    {
        Assert.IsFalse(EnumNames.TryParse(name, out FormationClass value));
        Assert.AreEqual(default(FormationClass), value);
    }
}
