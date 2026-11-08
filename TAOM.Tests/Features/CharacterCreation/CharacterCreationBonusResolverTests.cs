using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CharacterCreationBonusResolverTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    [TestMethod]
    public void ResolveSkills_NullOrEmpty_ReturnsEmptyWithoutFactoryCalls()
    {
        var calls = 0;
        var map = Map(("Bow", () => { calls++; return SkillToken(); }));

        var nullResult = CharacterCreationBonusResolver.ResolveSkills(null, _logger, false, map);
        var emptyResult = CharacterCreationBonusResolver.ResolveSkills(Array.Empty<string>(), _logger, false, map);

        Assert.AreEqual(0, nullResult.Length);
        Assert.AreEqual(0, emptyResult.Length);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void ResolveSkills_MixedCaseOrderAndDuplicates_InvokesFactoriesPerOccurrence()
    {
        var one = SkillToken();
        var two = SkillToken();
        var calls = new List<string>();
        var map = Map(
            ("One", () => { calls.Add("One"); return one; }),
            ("Two", () => { calls.Add("Two"); return two; }));

        var result = CharacterCreationBonusResolver.ResolveSkills(
            new[] { "two", "ONE", "Two" }, _logger, false, map);
        var secondResult = CharacterCreationBonusResolver.ResolveSkills(new[] { "one" }, _logger, false, map);

        Assert.AreEqual(3, result.Length);
        Assert.AreSame(two, result[0]);
        Assert.AreSame(one, result[1]);
        Assert.AreSame(two, result[2]);
        Assert.AreEqual(1, secondResult.Length);
        Assert.AreSame(one, secondResult[0]);
        CollectionAssert.AreEqual(new[] { "Two", "One", "Two", "One" }, calls);
    }

    [TestMethod]
    public void ResolveSkills_UnknownEmptyAndWhitespaceNames_LogsAndSkips()
    {
        var calls = 0;
        var map = Map(("Bow", () => { calls++; return SkillToken(); }));

        var result = CharacterCreationBonusResolver.ResolveSkills(
            new[] { "Unknown", "", " Bow " }, _logger, false, map);

        Assert.AreEqual(0, result.Length);
        Assert.AreEqual(0, calls);
        _logger.Received(1).LogWarning("Unknown skill name: 'Unknown'");
        _logger.Received(1).LogWarning("Unknown skill name: ''");
        _logger.Received(1).LogWarning("Unknown skill name: ' Bow '");
    }

    [TestMethod]
    public void ResolveSkills_NullFactoryResult_FiltersAndHonorsLoggingPolicy()
    {
        var map = Map(("Null", () => (SkillObject)null!));

        var silent = CharacterCreationBonusResolver.ResolveSkills(new[] { "Null" }, _logger, false, map);
        var logged = CharacterCreationBonusResolver.ResolveSkills(new[] { "Null" }, _logger, true, map);

        Assert.AreEqual(0, silent.Length);
        Assert.AreEqual(0, logged.Length);
        _logger.Received(1).LogWarning("Skill 'Null' resolved to null — game may not be initialized");
    }

    [TestMethod]
    public void ResolveSkills_ThrowingFactory_PropagatesAndStops()
    {
        var expected = new InvalidOperationException("factory failed");
        var laterCalls = 0;
        var map = Map(
            ("Bad", () => throw expected),
            ("Later", () => { laterCalls++; return SkillToken(); }));

        var actual = Assert.ThrowsException<InvalidOperationException>(() =>
            CharacterCreationBonusResolver.ResolveSkills(new[] { "Bad", "Later" }, _logger, false, map));

        Assert.AreSame(expected, actual);
        Assert.AreEqual(0, laterCalls);
    }

    [TestMethod]
    public void ResolveSkills_NullDelegate_ThrowsNullReferenceAndStops()
    {
        var laterCalls = 0;
        var map = Map(
            ("Null", (Func<SkillObject>)null!),
            ("Later", () => { laterCalls++; return SkillToken(); }));

        Assert.ThrowsException<NullReferenceException>(() =>
            CharacterCreationBonusResolver.ResolveSkills(new[] { "Null", "Later" }, _logger, false, map));

        Assert.AreEqual(0, laterCalls);
    }

    [TestMethod]
    public void ResolveAttribute_NullOrEmpty_ReturnsNullSilently()
    {
        var calls = 0;
        var map = Map(("Vigor", () => { calls++; return AttributeToken(); }));

        var nullResult = CharacterCreationBonusResolver.ResolveAttribute(null, _logger, false, map);
        var emptyResult = CharacterCreationBonusResolver.ResolveAttribute("", _logger, false, map);

        Assert.IsNull(nullResult);
        Assert.IsNull(emptyResult);
        Assert.AreEqual(0, calls);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ResolveAttribute_MixedCaseAndRepeatedCalls_InvokesFactoryEachTime()
    {
        var token = AttributeToken();
        var calls = 0;
        var map = Map(("Vigor", () => { calls++; return token; }));

        var first = CharacterCreationBonusResolver.ResolveAttribute("VIGOR", _logger, false, map);
        var second = CharacterCreationBonusResolver.ResolveAttribute("vigor", _logger, false, map);

        Assert.AreSame(token, first);
        Assert.AreSame(token, second);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void ResolveAttribute_UnknownAndWhitespace_LogsAndReturnsNull()
    {
        var calls = 0;
        var map = Map(("Vigor", () => { calls++; return AttributeToken(); }));

        var unknown = CharacterCreationBonusResolver.ResolveAttribute("Unknown", _logger, false, map);
        var whitespace = CharacterCreationBonusResolver.ResolveAttribute(" Vigor ", _logger, false, map);

        Assert.IsNull(unknown);
        Assert.IsNull(whitespace);
        Assert.AreEqual(0, calls);
        _logger.Received(1).LogWarning("Unknown attribute name: 'Unknown'");
        _logger.Received(1).LogWarning("Unknown attribute name: ' Vigor '");
    }

    [TestMethod]
    public void ResolveAttribute_NullFactoryResult_HonorsLoggingPolicy()
    {
        var map = Map(("Null", () => (CharacterAttribute)null!));

        var silent = CharacterCreationBonusResolver.ResolveAttribute("Null", _logger, false, map);
        var logged = CharacterCreationBonusResolver.ResolveAttribute("Null", _logger, true, map);

        Assert.IsNull(silent);
        Assert.IsNull(logged);
        _logger.Received(1).LogWarning("Attribute 'Null' resolved to null — game may not be initialized");
    }

    [TestMethod]
    public void ResolveAttribute_ThrowingFactory_Propagates()
    {
        var expected = new InvalidOperationException("factory failed");
        var map = Map<CharacterAttribute>(("Bad", () => throw expected));

        var actual = Assert.ThrowsException<InvalidOperationException>(() =>
            CharacterCreationBonusResolver.ResolveAttribute("Bad", _logger, false, map));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void ResolveAttribute_NullDelegate_ThrowsNullReference()
    {
        var map = Map(("Null", (Func<CharacterAttribute>)null!));

        Assert.ThrowsException<NullReferenceException>(() =>
            CharacterCreationBonusResolver.ResolveAttribute("Null", _logger, false, map));
    }

    private static Dictionary<string, Func<T>> Map<T>(params (string Name, Func<T> Factory)[] entries)
    {
        var map = new Dictionary<string, Func<T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, factory) in entries)
            map.Add(name, factory);
        return map;
    }

    private static SkillObject SkillToken() =>
        (SkillObject)FormatterServices.GetUninitializedObject(typeof(SkillObject));

    private static CharacterAttribute AttributeToken() =>
        (CharacterAttribute)FormatterServices.GetUninitializedObject(typeof(CharacterAttribute));
}
