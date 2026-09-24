using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MonsterSize;

namespace TAOM.Tests.Features.MonsterSize;

/// <summary>
/// Source pins for the Monster size pass's two wiring lines (#646). Without the registration the SubModule's
/// IoC.Resolve throws out of OnGameInitializationFinished before every late patch category; without the call every
/// sized mount silently builds at its item's placeholder, 1.0x; and a call placed after the once-per-process guard
/// would size only the first game of a session.
/// </summary>
[TestClass]
public class MonsterSizeWiringTests
{
    [TestMethod]
    public void MainIoC_RegistersTheMonsterSizeFeature()
    {
        string? ioc = ReadProjectSource("Main", "IoC.cs");
        if (ioc == null)
            Assert.Inconclusive("Main/IoC.cs not found: run from the repo root");
        StringAssert.Contains(ioc, "Features.MonsterSize.MonsterSizeIoC.RegisterMonsterSizeFeature(container);");
    }

    [TestMethod]
    public void MonsterSizeIoC_Registration_ResolvesTheService()
    {
        // The SubModule resolves the service unguarded at every game init, so the graph must close (the
        // CampsContainerWiringTests shape: Validate walks it without constructing anything).
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        MonsterSizeIoC.RegisterMonsterSizeFeature(container);

        var errors = container.Validate(typeof(IMonsterSizeService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void OnGameInitializationFinished_AppliesMonsterSizes_BeforeTheOncePerProcessGuard()
    {
        string? sub = ReadProjectSource("Main", "SubModule.cs");
        if (sub == null)
            Assert.Inconclusive("Main/SubModule.cs not found: run from the repo root");
        int method = sub!.IndexOf("public override void OnGameInitializationFinished(Game game)", System.StringComparison.Ordinal);
        Assert.IsTrue(method >= 0, "OnGameInitializationFinished is gone from SubModule.cs");
        int call = sub.IndexOf("IoC.Resolve<Features.MonsterSize.IMonsterSizeService>().ApplyMonsterSizes();", method, System.StringComparison.Ordinal);
        int guard = sub.IndexOf("if (_gameInitPatchesApplied) return;", method, System.StringComparison.Ordinal);
        Assert.IsTrue(call > method, "OnGameInitializationFinished no longer applies the Monster sizes");
        Assert.IsTrue(guard > call, "the Monster size call must run before the once-per-process guard: each game reloads its items");
    }

    private static string? ReadProjectSource(params string[] relativeParts)
    {
        string? dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
