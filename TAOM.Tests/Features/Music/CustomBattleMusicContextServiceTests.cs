using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class CustomBattleMusicContextServiceTests
{
    [TestMethod]
    public void SelectPlayerCulture_StoresTrimmedCultureId()
    {
        var service = new CustomBattleMusicContextService();

        service.SelectPlayerCulture("  gondor  ");

        Assert.AreEqual("gondor", service.SelectedPlayerCultureId);
    }

    [TestMethod]
    public void SelectPlayerCulture_BlankClearsStoredCulture()
    {
        var service = new CustomBattleMusicContextService();
        service.SelectPlayerCulture("gondor");

        service.SelectPlayerCulture(" ");

        Assert.IsNull(service.SelectedPlayerCultureId);
    }

    [TestMethod]
    public void ClearSelectedCulture_RemovesStoredCulture()
    {
        var service = new CustomBattleMusicContextService();
        service.SelectPlayerCulture("mordor");

        service.ClearSelectedCulture();

        Assert.IsNull(service.SelectedPlayerCultureId);
    }
}
