using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Integration guard against the class of bug behind issues #249/#250: config the content
/// uses but the parser/consumer doesn't support, invisible to synthetic-XML unit tests.
/// Loads the REAL shipped taom_career_choices.xml — the only way to catch a schema drift like
/// the 310 dead <PassiveEffects>-wrapped choices. See
/// docs/reviews/rca-career-partysize-2026-05-29.md + memory feedback_parse_real_config_in_tests.
/// </summary>
[TestClass]
public class CareerChoicesIntegrationTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private CareerConfigProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _provider = new CareerConfigProvider(pathService, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void RealChoicesXml_Loads_NonEmpty()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml")),
            $"real taom_career_choices.xml not found under {ModuleDataPath}");
        Assert.IsTrue(_provider.LoadChoices().Count > 100,
            "real taom_career_choices.xml should load the full career choice set");
    }

    [TestMethod]
    public void RealChoicesXml_EveryPassiveChoice_ParsesNonNullPassive()
    {
        // The wrapped-schema bug (#250): 310 <PassiveEffects> choices parsed to a null Passive
        // because ParseChoice only read a direct <PassiveEffect> child + magnitude=. Every
        // type="Passive" choice MUST yield a parsed PassiveEffect — this asserts it against the
        // real file so any future schema drift fails CI immediately.
        var dead = _provider.LoadChoices()
            .Where(c => c.Type == ChoiceType.Passive && c.Passive == null)
            .Select(c => c.Id)
            .ToList();

        Assert.AreEqual(0, dead.Count,
            "Every type=\"Passive\" choice must parse to a non-null PassiveEffect. Dead choices: "
            + string.Join(", ", dead));
    }

    [TestMethod]
    public void RealChoicesXml_NoPassiveEffect_DefaultsToSpecialType()
    {
        // Guard against an unrecognized/typo'd type= silently defaulting to PassiveEffectType.Special
        // (the ParseEnum fallback) — a parsed-but-inert passive.
        var special = _provider.LoadChoices()
            .Where(c => c.Passive != null && c.Passive.EffectType == PassiveEffectType.Special)
            .Select(c => c.Id)
            .ToList();

        Assert.AreEqual(0, special.Count,
            "No PassiveEffect should fall back to type=Special (unrecognized type=). Offenders: "
            + string.Join(", ", special));
    }
}
