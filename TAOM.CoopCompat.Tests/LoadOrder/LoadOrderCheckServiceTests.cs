using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.CoopCompat.LoadOrder;
using TAOM.Core.Logging;

namespace TAOM.CoopCompat.Tests.LoadOrder;

/// <summary>The host's load order check: the switch, a player that sent no order, a refused list and the compare.</summary>
[TestClass]
public class LoadOrderCheckServiceTests
{
    private sealed class FakeModules : ILoadOrderAdapter
    {
        public List<ModuleEntry> Modules { get; } = new()
        {
            new ModuleEntry("TAOM.Dependencies", "TAOM.Dependencies", false, false),
            new ModuleEntry("Native", "Native", true, true),
            new ModuleEntry("TAOM_Map", "TAOM Map", false, true),
            new ModuleEntry("LOTRLOME_Armory", "LOTR Armory", false, true),
            new ModuleEntry("TAOM", "TAOM", false, true),
            new ModuleEntry("RBM", "Realistic Battle", false, true),
        };

        public bool Throws { get; set; }

        public IReadOnlyList<ModuleEntry> ActiveModules() => Throws ? throw new InvalidOperationException("boom") : Modules;
    }

    private FakeModules _modules = null!;
    private IModLogger _logger = null!;
    private bool _enabled;
    private LoadOrderCheckService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _modules = new FakeModules();
        _logger = Substitute.For<IModLogger>();
        _enabled = true;
        _service = new LoadOrderCheckService(_modules, () => _enabled, _logger);
    }

    [TestMethod]
    public void LocalOrder_IsTheActiveModuleIdsInOrder()
    {
        CollectionAssert.AreEqual(new[] { "TAOM.Dependencies", "Native", "TAOM_Map", "LOTRLOME_Armory", "TAOM", "RBM" }, _service.LocalOrder());
    }

    [TestMethod]
    public void LocalOrder_TheModuleListThrows_SendsNoOrderAndLogs_ThenReadsAgain()
    {
        _modules.Throws = true;

        Assert.AreEqual(0, _service.LocalOrder().Length);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("load order: could not read this game's module list") && s.Contains("boom")));

        _modules.Throws = false;
        Assert.AreEqual(6, _service.LocalOrder().Length);
    }

    [TestMethod]
    public void Check_SameOrder_Matches()
    {
        var verdict = _service.Check("7", new[] { "Native", "TAOM_Map", "LOTRLOME_Armory", "TAOM", "RBM" });

        Assert.IsFalse(verdict.Differs);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("load order peer=7 match")));
    }

    [TestMethod]
    public void Check_SwappedPair_DiffersAndNamesBothModules_AndLogsBothOrders()
    {
        var verdict = _service.Check("7", new[] { "TAOM_Map", "RBM", "LOTRLOME_Armory", "TAOM" });

        Assert.IsTrue(verdict.Differs);
        Assert.AreEqual("LOTR Armory (LOTRLOME_Armory)", verdict.HostFirst);
        Assert.AreEqual("Realistic Battle (RBM)", verdict.HostSecond);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("load order peer=7 differ")
            && s.Contains("host order: TAOM.Dependencies, TAOM_Map, LOTRLOME_Armory, TAOM, RBM")
            && s.Contains("player order: TAOM_Map, RBM, LOTRLOME_Armory, TAOM")));
    }

    [TestMethod]
    public void Check_SwitchOff_DoesNotCompare()
    {
        _enabled = false;

        var verdict = _service.Check("7", new[] { "TAOM_Map", "RBM", "LOTRLOME_Armory", "TAOM" });

        Assert.IsFalse(verdict.Differs);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("not compared: the switch is off on this server")));
    }

    [TestMethod]
    public void Check_TheSwitchIsReadOnEveryCall()
    {
        var swapped = new[] { "TAOM_Map", "RBM", "LOTRLOME_Armory", "TAOM" };

        _enabled = false;
        Assert.IsFalse(_service.Check("7", swapped).Differs);
        _enabled = true;
        Assert.IsTrue(_service.Check("8", swapped).Differs);
    }

    [TestMethod]
    public void Check_NoOrderSent_IsNotReported()
    {
        Assert.IsFalse(_service.Check("7", Array.Empty<string>()).Differs);
        Assert.IsFalse(_service.Check("7", null!).Differs);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("not reported")));
    }

    [TestMethod]
    public void Check_RefusedList_IsIgnoredWithAWarning()
    {
        var verdict = _service.Check("7", new[] { "TAOM", "" });

        Assert.IsFalse(verdict.Differs);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("load order peer=7 ignored")));
    }

    [TestMethod]
    public void Check_TheComparedModules_AreLoggedOnce()
    {
        _service.Check("7", new[] { "TAOM" });
        _service.Check("8", new[] { "TAOM" });

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("load order compared modules (5): TAOM.Dependencies, TAOM_Map, LOTRLOME_Armory, TAOM, RBM")));
    }

    [TestMethod]
    public void Check_NoComparedModuleOnTheHost_StillLogsTheCount()
    {
        _modules.Modules.Clear();

        _service.Check("7", new[] { "TAOM" });

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("load order compared modules (0)")));
    }

    [TestMethod]
    public void Check_TheModuleListThrows_DoesNotThrow()
    {
        _modules.Throws = true;

        Assert.IsFalse(_service.Check("7", new[] { "TAOM" }).Differs);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("load order peer=7 not compared") && s.Contains("boom")));
    }
}
