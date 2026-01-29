using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using System.IO;
using System.Linq;

namespace TAOM.Tests.Core.Logging;

[TestClass]
public class FileLoggerTests
{
    private string _testDir;
    private string _originalDir;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TAOM_LogTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [TestMethod]
    public void LogInfo_WritesInfoMessageToLogFile()
    {
        var logger = new FileLogger();

        logger.LogInfo("Test message");
        logger.Dispose();

        var logDir = Path.Combine(_testDir, "Logs");
        var logFile = Directory.GetFiles(logDir).First();
        var content = File.ReadAllText(logFile);

        Assert.IsTrue(content.Contains("[INFO]"));
        Assert.IsTrue(content.Contains("Test message"));
    }

    [TestMethod]
    public void LogError_WritesErrorPrefixToLogFile()
    {
        var logger = new FileLogger();

        logger.LogError("Something failed");
        logger.Dispose();

        var logDir = Path.Combine(_testDir, "Logs");
        var logFile = Directory.GetFiles(logDir).First();
        var content = File.ReadAllText(logFile);

        Assert.IsTrue(content.Contains("[ERROR]"));
        Assert.IsTrue(content.Contains("Something failed"));
    }

    [TestMethod]
    public void Dispose_SubsequentWritesDoNotThrow()
    {
        var logger = new FileLogger();
        logger.Dispose();

        logger.LogInfo("After dispose");
        logger.LogError("After dispose error");
    }

    [TestMethod]
    public void LogMultipleMessages_AllWrittenToFile()
    {
        var logger = new FileLogger();

        logger.LogInfo("First");
        logger.LogWarning("Second");
        logger.LogDebug("Third");
        logger.LogError("Fourth");
        logger.Dispose();

        var logDir = Path.Combine(_testDir, "Logs");
        var logFile = Directory.GetFiles(logDir).First();
        var content = File.ReadAllText(logFile);

        Assert.IsTrue(content.Contains("First"));
        Assert.IsTrue(content.Contains("Second"));
        Assert.IsTrue(content.Contains("Third"));
        Assert.IsTrue(content.Contains("Fourth"));
    }
}
