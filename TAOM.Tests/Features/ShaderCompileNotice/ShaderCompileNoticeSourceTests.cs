using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ShaderCompileNotice;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ShaderCompileNotice;

/// <summary>
/// ADR-013, B4c. The notice's window is WinForms, so it lives in TAOM.Client behind a window host. The static
/// notice class keeps its name, thread and flags in TAOM.dll (docs/features/shader-compile-notice.md, the
/// VanillaTuning lookup by full type name).
/// </summary>
[TestClass]
public class ShaderCompileNoticeSourceTests
{
    [TestMethod]
    public void NullShaderCompileNoticeWindowHost_Run_ReturnsAtOnce()
    {
        var texts = new ShaderCompileNoticeTexts("a", "b", "c");
        var host = new NullShaderCompileNoticeWindowHost();
        var done = new ManualResetEventSlim();

        var thread = new Thread(() => { host.Run(texts, ShaderCompileNoticeMode.Startup, Substitute.For<IModLogger>(), () => false); done.Set(); });
        thread.Start();

        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(5)), "the null host must not wait for a stop request");
    }

    [TestMethod]
    public void CoreNotice_KeepsTheThreadAndFlags_AndNamesNoWinFormsType()
    {
        var src = RepoPaths.ReadSource("Main/Features/ShaderCompileNotice/ShaderCompileNotice.cs", stripComments: true);

        Assert.IsFalse(src.Contains("System.Windows.Forms"));
        Assert.IsFalse(src.Contains("Application."));
        Assert.IsFalse(src.Contains("ApplicationContext"));
        StringAssert.Contains(src, "ApartmentState.STA");
        StringAssert.Contains(src, "Interlocked.Exchange(ref _started, 1)");
        StringAssert.Contains(src, "host.Run(texts, ShaderCompileNoticeMode.Startup, logger, () => _stopRequested)");
    }

    [TestMethod]
    public void TheNoticeClass_StaysInTaomDll() =>
        Assert.IsNotNull(typeof(SubModule).Assembly.GetType("TAOM.Features.ShaderCompileNotice.ShaderCompileNotice"),
            "VanillaTuning may look the class up by full name in the assembly named TAOM");

    [TestMethod]
    public void SubModule_StartsTheNoticeWithTheResolvedWindowHost()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int bridge = src.IndexOf("EarlyLogBridge.Connect", StringComparison.Ordinal);
        int start = src.IndexOf("ShaderCompileNoticeEntry.Start(", StringComparison.Ordinal);
        int stamp = src.IndexOf("BuildStampReport.BuildReport", StringComparison.Ordinal);
        Assert.IsTrue(bridge >= 0 && start > bridge && stamp > start, "an anchor moved");

        var call = src.Substring(start, stamp - start);
        StringAssert.Contains(call, "IoC.Resolve<Features.ShaderCompileNotice.IShaderCompileNoticeWindowHost>()");
    }
}
