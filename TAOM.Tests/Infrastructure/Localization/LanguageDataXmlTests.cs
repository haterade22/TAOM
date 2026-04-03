using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace TAOM.Tests.Infrastructure.Localization;

[TestClass]
public class LanguageDataXmlTests
{
    private static string LanguagesPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData\Languages"));

    private static readonly string[] SupportedLanguageDirs =
        new[] { "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    [TestMethod]
    public void LanguagesDirExists()
    {
        Assert.IsTrue(Directory.Exists(LanguagesPath),
            $"Languages directory not found: {LanguagesPath}");
    }

    [TestMethod]
    public void RootLanguageDataFile_Exists()
    {
        var path = Path.Combine(LanguagesPath, "language_data.xml");
        Assert.IsTrue(File.Exists(path), $"Root language_data.xml not found: {path}");
    }

    [TestMethod]
    public void RootLanguageDataFile_IsWellFormedXml()
    {
        var path = Path.Combine(LanguagesPath, "language_data.xml");
        var doc = XDocument.Load(path);
        Assert.IsNotNull(doc.Root);
    }

    [TestMethod]
    public void RootLanguageDataFile_HasEnglishId()
    {
        var path = Path.Combine(LanguagesPath, "language_data.xml");
        var doc = XDocument.Load(path);
        Assert.AreEqual("English", (string?)doc.Root?.Attribute("id"),
            "Root language_data.xml must declare id=\"English\"");
    }

    [TestMethod]
    public void AllSupportedLanguageDirs_Exist()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var dir = Path.Combine(LanguagesPath, lang);
            Assert.IsTrue(Directory.Exists(dir),
                $"Missing language directory: Languages/{lang}/");
        }
    }

    [TestMethod]
    public void AllLanguageSubdirs_HaveLanguageDataXml()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var path = Path.Combine(LanguagesPath, lang, "language_data.xml");
            Assert.IsTrue(File.Exists(path),
                $"Missing language_data.xml in Languages/{lang}/");
        }
    }

    [TestMethod]
    public void AllLanguageDataXml_AreWellFormedXml()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var path = Path.Combine(LanguagesPath, lang, "language_data.xml");
            var doc = XDocument.Load(path);
            Assert.IsNotNull(doc.Root, $"Languages/{lang}/language_data.xml has no root element");
        }
    }

    [TestMethod]
    public void AllLanguageDataXml_HaveLanguageDataRootElement()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var path = Path.Combine(LanguagesPath, lang, "language_data.xml");
            var doc = XDocument.Load(path);
            Assert.AreEqual("LanguageData", doc.Root?.Name.LocalName,
                $"Languages/{lang}/language_data.xml root must be <LanguageData>");
        }
    }

    [TestMethod]
    public void AllLanguageDataXml_HaveNonEmptyId()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var path = Path.Combine(LanguagesPath, lang, "language_data.xml");
            var doc = XDocument.Load(path);
            var id = (string?)doc.Root?.Attribute("id");
            Assert.IsFalse(string.IsNullOrWhiteSpace(id),
                $"Languages/{lang}/language_data.xml must have a non-empty id attribute");
        }
    }

    [TestMethod]
    public void AllLanguageFilePaths_ReferenceExistingFiles()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var langDataPath = Path.Combine(LanguagesPath, lang, "language_data.xml");
            var doc = XDocument.Load(langDataPath);
            foreach (var fileEl in doc.Descendants("LanguageFile"))
            {
                var xmlPath = (string?)fileEl.Attribute("xml_path");
                Assert.IsNotNull(xmlPath,
                    $"Languages/{lang}/language_data.xml has a <LanguageFile> with no xml_path");
                var fullPath = Path.Combine(LanguagesPath, xmlPath);
                Assert.IsTrue(File.Exists(fullPath),
                    $"Languages/{lang}/language_data.xml references missing file: {xmlPath}");
            }
        }
    }

    [TestMethod]
    public void AllTranslationFiles_AreWellFormedXml()
    {
        foreach (var (file, lang) in GetAllTranslationFiles())
        {
            var doc = XDocument.Load(file);
            Assert.IsNotNull(doc.Root, $"Translation file has no root element: Languages/{lang}/...");
        }
    }

    [TestMethod]
    public void AllTranslationFiles_HaveBaseRootWithTypeString()
    {
        foreach (var (file, lang) in GetAllTranslationFiles())
        {
            var doc = XDocument.Load(file);
            Assert.AreEqual("base", doc.Root?.Name.LocalName,
                $"Translation file root must be <base> in Languages/{lang}/...");
            Assert.AreEqual("string", (string?)doc.Root?.Attribute("type"),
                $"Translation file <base> must have type=\"string\" in Languages/{lang}/...");
        }
    }

    [TestMethod]
    public void AllTranslationFiles_HaveTagsElement()
    {
        foreach (var (file, lang) in GetAllTranslationFiles())
        {
            var doc = XDocument.Load(file);
            var tags = doc.Root?.Element("tags");
            Assert.IsNotNull(tags,
                $"Translation file missing <tags> element in Languages/{lang}/...");
            Assert.IsNotNull(tags.Element("tag"),
                $"Translation file <tags> missing <tag> in Languages/{lang}/...");
        }
    }

    [TestMethod]
    public void AllTranslationFiles_HaveStringsElement()
    {
        foreach (var (file, lang) in GetAllTranslationFiles())
        {
            var doc = XDocument.Load(file);
            Assert.IsNotNull(doc.Root?.Element("strings"),
                $"Translation file missing <strings> element in Languages/{lang}/...");
        }
    }

    [TestMethod]
    public void AllTranslationFiles_StringEntries_HaveIdAndTextAttributes()
    {
        foreach (var (file, lang) in GetAllTranslationFiles())
        {
            var doc = XDocument.Load(file);
            var strings = doc.Root?.Element("strings");
            if (strings is null) continue;
            foreach (var str in strings.Elements("string"))
            {
                var id = (string?)str.Attribute("id");
                var text = (string?)str.Attribute("text");
                Assert.IsNotNull(id,
                    $"<string> missing 'id' attribute in Languages/{lang}/...");
                Assert.IsNotNull(text,
                    $"<string id=\"{id}\"> missing 'text' attribute in Languages/{lang}/...");
            }
        }
    }

    private static IEnumerable<(string FilePath, string Lang)> GetAllTranslationFiles()
    {
        foreach (var lang in SupportedLanguageDirs)
        {
            var langDataPath = Path.Combine(LanguagesPath, lang, "language_data.xml");
            if (!File.Exists(langDataPath)) continue;
            var doc = XDocument.Load(langDataPath);
            foreach (var fileEl in doc.Descendants("LanguageFile"))
            {
                var xmlPath = (string?)fileEl.Attribute("xml_path");
                if (xmlPath is null) continue;
                var fullPath = Path.Combine(LanguagesPath, xmlPath);
                if (File.Exists(fullPath))
                    yield return (fullPath, lang);
            }
        }
    }
}
