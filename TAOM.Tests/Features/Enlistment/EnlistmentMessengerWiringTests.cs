using System.Text.RegularExpressions;
using TAOM.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>Source pin for #898: the enlist oath reads the open-conversation flag when it decides whether it can be chosen.</summary>
[TestClass]
public class EnlistmentMessengerWiringTests
{
    [TestMethod]
    public void EnlistmentDialog_PassesTheMessengerFlagToTheTakeableCheck()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(@"throughMessenger\s*=\s*_messenger\.IsOpen"));
        StringAssert.Matches(src, new Regex(@"OfferIsTakeable\(\s*verdict\s*,\s*throughMessenger\s*\)"));
    }

    [TestMethod]
    public void EnlistmentDialog_PassesTheMessengerFlagToTheReason()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(@"EnlistUnavailableReason\(\s*verdict\s*,\s*throughMessenger\s*\)"));
    }
}
