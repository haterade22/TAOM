using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CrashReport;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The main-thread id the hook records in Subscribe() (called from SubModule.OnSubModuleLoad) is the
// reference Native2ManagedBridge compares against (maintainer decision 2026-09-24, #650).
[TestClass]
public class AppDomainExceptionHookTests
{
    [TestMethod]
    public void Subscribe_RecordsTheSubscribingThreadAsTheMainThread()
    {
        var hook = new AppDomainExceptionHook(Substitute.For<ICrashReportService>(), Substitute.For<IModLogger>());
        int subscriber = 0;
        var boot = new Thread(() =>
        {
            subscriber = Thread.CurrentThread.ManagedThreadId;
            hook.Subscribe();
        });

        try
        {
            boot.Start();
            boot.Join();

            Assert.AreEqual(subscriber, AppDomainExceptionHook.MainThreadId);
        }
        finally
        {
            hook.Unsubscribe();
        }
    }

    [TestMethod]
    public void IsOffMainThread_IsFalseOnlyOnTheRecordedThread()
    {
        int current = Thread.CurrentThread.ManagedThreadId;

        Assert.IsFalse(AppDomainExceptionHook.IsOffMainThread(current));
        Assert.IsTrue(AppDomainExceptionHook.IsOffMainThread(current + 1));
        Assert.IsTrue(AppDomainExceptionHook.IsOffMainThread(0), "an unset id counts as off-main, the safe direction");
    }

    [TestMethod]
    public void OnUnhandled_TellsTheServiceWhetherItRanOnTheSubscribingThread()
    {
        // HandleException's offMainThread defaults to false, so a call that dropped the verdict would
        // still compile and send every worker-thread capture down the full path.
        var service = new RecordingCrashService();
        var hook = new AppDomainExceptionHook(service, Substitute.For<IModLogger>());
        try
        {
            hook.Subscribe();

            hook.OnUnhandled(this, new UnhandledExceptionEventArgs(new InvalidOperationException("taom-006 main"), false));
            var worker = new Thread(() =>
                hook.OnUnhandled(this, new UnhandledExceptionEventArgs(new InvalidOperationException("taom-006 worker"), false)));
            worker.Start();
            worker.Join();

            Assert.AreEqual(2, service.Calls.Count);
            Assert.AreEqual("AppDomain.UnhandledException", service.Calls[0].Origin);
            Assert.IsFalse(service.Calls[0].OffMainThread, "the subscribing thread is the main thread");
            Assert.IsTrue(service.Calls[1].OffMainThread, "a worker-thread capture must take the reduced path");
        }
        finally
        {
            hook.Unsubscribe();
        }
    }
}
