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
}
