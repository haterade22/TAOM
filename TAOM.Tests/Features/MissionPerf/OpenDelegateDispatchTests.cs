using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The mechanism behind <c>MissionAttributionHooks.TimedScriptTick</c>, proven with no engine type: an open
/// delegate bound to a <c>protected internal virtual</c> method performs a virtual call, so the override
/// runs (the engine's <c>ScriptComponentBehavior.OnTick</c> has this shape), and an override's exception
/// reaches the caller unchanged.
/// </summary>
[TestClass]
public class OpenDelegateDispatchTests
{
    public class DispatchBase
    {
        public string Calls = "";

        protected internal virtual void Tick(float dt) { Calls = "base"; }
    }

    public sealed class DispatchDerived : DispatchBase
    {
        protected internal override void Tick(float dt) { Calls = "derived"; }
    }

    public sealed class ThrowingDerived : DispatchBase
    {
        protected internal override void Tick(float dt) => throw new InvalidOperationException("from the override");
    }

    private static MethodInfo BaseTick() =>
        typeof(DispatchBase).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [TestMethod]
    public void OpenDelegate_ProtectedInternalVirtual_CallsTheOverride()
    {
        var call = ProbeDelegates.BindOpenInstance<DispatchBase>(BaseTick());
        var target = new DispatchDerived();

        Assert.IsNotNull(call);
        call(target, 0.016f);

        Assert.AreEqual("derived", target.Calls);
    }

    [TestMethod]
    public void OpenDelegate_ThrowingOverride_PropagatesTheException()
    {
        var call = ProbeDelegates.BindOpenInstance<DispatchBase>(BaseTick())!;

        var ex = Assert.ThrowsException<InvalidOperationException>(() => call(new ThrowingDerived(), 0.016f));

        Assert.AreEqual("from the override", ex.Message);
        Assert.IsNull(ProbeDelegates.BindOpenInstance<DispatchBase>(null), "A missing method binds to null.");
    }
}
