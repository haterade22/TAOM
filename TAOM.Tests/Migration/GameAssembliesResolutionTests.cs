using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// How the binding gate finds the game. The two environment variables win, in the order
/// Directory.Build.props uses; when neither names a usable folder in the test process (the
/// override needs Bannerlord.exe, the game dir only needs to exist), the gate falls back to
/// the GameFolder this test assembly was compiled against, so a test DLL built against the game
/// never skips the binding suite just because its runner lacks the variables.
/// </summary>
[TestClass]
public class GameAssembliesResolutionTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "TAOM_GameDirTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, true); } catch { }
        }
    }

    private string MakeDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [TestMethod]
    public void ResolveGameDir_NoEnvironmentVariables_FallsBackToTheBuildFolder()
    {
        // Arrange
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, null, built);

        // Assert
        Assert.AreEqual(built, result);
    }

    [TestMethod]
    public void ResolveGameDir_GameDirSet_WinsOverTheBuildFolder()
    {
        // Arrange
        var game = MakeDir("game");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, game, built);

        // Assert
        Assert.AreEqual(game, result);
    }

    [TestMethod]
    public void ResolveGameDir_OverrideHoldingBannerlordExe_WinsOverBoth()
    {
        // Arrange
        var over = MakeDir("over");
        var overBin = Path.Combine(over, "bin", "Win64_Shipping_Client");
        Directory.CreateDirectory(overBin);
        File.WriteAllText(Path.Combine(overBin, "Bannerlord.exe"), "");
        var game = MakeDir("game");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(over, game, built);

        // Assert
        Assert.AreEqual(over, result);
    }

    [TestMethod]
    public void ResolveGameDir_OverrideWithoutBannerlordExe_FallsThroughToTheGameDir()
    {
        // Arrange
        var over = MakeDir("over");
        var game = MakeDir("game");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(over, game, built);

        // Assert
        Assert.AreEqual(game, result);
    }

    [TestMethod]
    public void ResolveGameDir_GameDirNotOnDisk_FallsBackToTheBuildFolder()
    {
        // Arrange
        var gone = Path.Combine(_root, "gone");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, gone, built);

        // Assert
        Assert.AreEqual(built, result);
    }

    [TestMethod]
    public void ResolveGameDir_BuildFolderNotOnDisk_ReturnsNull()
    {
        // Arrange
        var missing = Path.Combine(_root, "missing");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, null, missing);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ResolveGameDir_BuildFolderEmpty_ReturnsNull()
    {
        // Arrange (none)

        // Act
        var result = GameAssemblies.ResolveGameDir(null, null, "");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuiltGameFolder_TestAssemblyAsCompiled_CarriesTheTaomGameFolderMetadata()
    {
        // Arrange (none: the attribute comes from TAOM.Tests.csproj at build time)

        // Act
        var folder = GameAssemblies.BuiltGameFolder;

        // Assert
        Assert.IsNotNull(folder, "TAOM.Tests.csproj must emit [assembly: AssemblyMetadata(\"TaomGameFolder\", \"$(GameFolder)\")] so the binding gate can find the game the build used.");
    }
}
