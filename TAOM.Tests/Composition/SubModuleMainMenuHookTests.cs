using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// The engine calls OnBeforeInitialModuleScreenSetAsRoot with no try and again on every tick while no
/// state is active, so the menu customization, the one TAOM call that can throw, must stay inside
/// MainMenuStep.Run. ShaderCompileNoticeEntry.Stop, where present, only sets a volatile flag and is called directly.
/// </summary>
[TestClass]
public sealed class SubModuleMainMenuHookTests
{
    [TestMethod]
    public void OnBeforeInitialModuleScreenSetAsRoot_MenuCustomization_RunsInsideAMainMenuStep()
    {
        var body = MethodBody(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true));

        Assert.AreEqual(1, Regex.Matches(body, @"\.CustomizeMenu\(\)").Count,
            "CustomizeMenu is called exactly once in the hook.");
        Assert.IsTrue(
            Regex.IsMatch(body, @"MainMenuStep\.Run\(\s*""[^""]+""\s*,[^;]*CustomizeMenu\(\)"),
            "CustomizeMenu must run through MainMenuStep.Run.");
    }

    private static string MethodBody(string source)
    {
        var start = source.IndexOf("void OnBeforeInitialModuleScreenSetAsRoot()", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "OnBeforeInitialModuleScreenSetAsRoot not found.");
        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
        }
        Assert.Fail("Unbalanced braces.");
        return "";
    }
}
