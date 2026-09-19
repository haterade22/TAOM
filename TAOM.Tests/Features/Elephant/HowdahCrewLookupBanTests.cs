using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The howdah crew must be looked up as a <see cref="TaleWorlds.Core.BasicCharacterObject"/>, never as the sealed
/// <see cref="CharacterObject"/> (#627, delta review P1). <c>MBObjectManager.GetObject&lt;T&gt;</c> takes an exact-type
/// path when T is sealed and matches only a record whose ObjectClass is that type; Custom Battle registers NPCCharacter
/// as BasicCharacterObject (CustomGame.OnRegisterTypes) while the campaign registers CharacterObject. Asking for the
/// sealed type therefore returns null in every Custom Battle, which is the mode the howdah smoke runs in: the crew
/// silently never spawn. The base type resolves in both modes.
/// </summary>
[TestClass]
public class HowdahCrewLookupBanTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void HowdahCrewSpawner_NeverResolvesTroopsAsTheSealedCharacterObject()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(typeof(CharacterObject).IsSealed,
            "CharacterObject is no longer sealed: GetObject<T> would take the assignable path and this ban can retire.");

        var violations = IlCallScanner.FindCallers(
            typeof(HowdahCrewSpawner).Assembly,
            IsSealedCharacterObjectLookup,
            out List<string> unreadable,
            out int scanned);

        Assert.AreEqual(0, unreadable.Count, "Unreadable method bodies, so the ban cannot vouch: " + string.Join("; ", unreadable));
        Assert.IsTrue(scanned > 0, "No method bodies scanned: the scan failed rather than passed.");
        var howdah = violations.Where(v => v.Contains("Howdah")).ToList();
        Assert.AreEqual(0, howdah.Count,
            "Resolve the crew with MBObjectManager.GetObject<BasicCharacterObject>: the sealed CharacterObject finds nothing in Custom Battle. " +
            string.Join("; ", howdah));
    }

    private static bool IsSealedCharacterObjectLookup(MethodBase called) =>
        called.Name == "GetObject"
        && called.IsGenericMethod
        && called.GetGenericArguments().FirstOrDefault() == typeof(CharacterObject);
}
