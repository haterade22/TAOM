using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterCreation.Hooks;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class GetRaceNamesHookTests
{
    [TestMethod]
    public void GetRaceNamesHook_ImplementsInterface()
    {
        var hook = new GetRaceNamesHook();

        Assert.IsInstanceOfType(hook, typeof(IOnGetRaceNames));
    }
}
