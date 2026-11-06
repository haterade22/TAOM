using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every path in module_sounds.xml (module_sound and variation elements) must name a file under ModuleSounds.
/// The elf horn registered elf_horn.wav while the file shipped as horn_elf.wav, and nothing reported it.
/// </summary>
[TestClass]
public class ModuleSoundPathTests
{
    [TestMethod]
    public void EveryModuleSoundPath_NamesAShippedFile()
    {
        var moduleData = CultureDataFixture.ModuleDataPath();
        var sounds = Path.Combine(moduleData, "..", "ModuleSounds");
        var paths = XDocument.Load(Path.Combine(moduleData, "module_sounds.xml")).Descendants()
            .Select(e => (string?)e.Attribute("path"))
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        Assert.IsTrue(paths.Count > 0, "module_sounds.xml yielded no path attributes; the file shape changed");

        var missing = paths.Where(p => !File.Exists(Path.Combine(sounds, p!))).ToList();

        Assert.AreEqual(0, missing.Count, "module_sounds.xml paths with no file:\n  " + string.Join("\n  ", missing));
    }
}
