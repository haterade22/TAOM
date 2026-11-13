using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Client;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Adapters;

/// <summary>
/// No TAOM code reads <c>BasicCharacterObject.Culture</c> where it means the culture a character fights for (#1157).
/// The base getter returns the raw field, which for the player's hero holds <c>main_hero</c>'s ModuleData culture;
/// <c>CharacterObject.Culture</c> is a <c>new</c> property with its own getter that returns <c>Hero.Culture</c>. The IL
/// scan finds every call that binds to the base getter, whatever the source spells: an agent's <c>Character</c>, a
/// <c>BasicCharacterObject</c> local or parameter, <c>Agent.Origin.Troop</c>. Such code goes through
/// <see cref="TAOM.Adapters.CharacterCulture"/>; the types in <see cref="RawReaders"/> read the raw field on purpose.
/// </summary>
[TestClass]
public class CharacterCultureGateTests
{
    private const string BaseCharacter = "TaleWorlds.Core.BasicCharacterObject";

    // Keyed by type or namespace; the reason each one wants the raw field. Custom Battle registers NPCCharacter
    // as a BasicCharacterObject (CustomGame.OnRegisterTypes), so there the raw field is the only culture.
    private static readonly Dictionary<string, string> RawReaders = new()
    {
        ["TAOM.Adapters.CharacterCulture"] = "the helper's own fallback, for a troop or a hero without a culture",
        ["TAOM.Adapters.HeroCharacterStateAdapter"] = "it inspects and repairs the raw field itself",
        ["TAOM.Adapters.ObjectManagerAdapter"] = "the BasicCharacterObject catalog that Custom Battle and shader precompilation read",
        ["TAOM.Features.CustomBattles.Muster"] = "the Custom Battle muster, BasicCharacterObjects only",
        ["TAOM.Client.Features.CustomBattles.Muster"] = "the Custom Battle muster screen, BasicCharacterObjects only",
    };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void NoTaomAssembly_ReadsTheBaseCultureOutsideTheRawReaders()
    {
        RequireGameAssemblies();

        foreach (var assembly in new[] { typeof(TAOM.IoC).Assembly, typeof(ClientSubModule).Assembly })
        {
            var callers = IlCallScanner.FindCallers(assembly, IsBaseCulture, out var unreadable, out var scanned);

            Assert.IsTrue(scanned > 0, $"No method bodies scanned in {assembly.GetName().Name}: the scan failed rather than passed.");
            Assert.AreEqual(0, unreadable.Count, $"Unreadable bodies in {assembly.GetName().Name}, so the gate cannot vouch: " + string.Join("; ", unreadable.Take(20)));

            var offenders = callers.Where(c => !RawReaders.Keys.Any(t => c.StartsWith(t + ".") || c.StartsWith(t + "+"))).ToList();
            Assert.AreEqual(0, offenders.Count,
                "these methods read BasicCharacterObject.Culture, the raw field, not a hero's culture; use CharacterCulture.Of or "
                + "IdOf, or add the type to RawReaders with the reason it wants the raw field: " + string.Join("; ", offenders));
        }
    }

    [TestMethod]
    public void TheScan_FindsABaseCultureRead_ThroughAnAgentAndThroughALocal()
    {
        RequireGameAssemblies();

        foreach (var name in new[] { nameof(ReadsBaseCulture.ThroughAgent), nameof(ReadsBaseCulture.ThroughLocal) })
        {
            var method = typeof(ReadsBaseCulture).GetMethod(name);
            Assert.IsNotNull(method, name + " is gone.");

            var calls = IlCallScanner.ExtractCalledMethods(method!, method!.GetMethodBody()!.GetILAsByteArray());

            Assert.IsTrue(calls.Any(IsBaseCulture), $"The scanner did not see the base getter in {name}, so the assembly gate would pass vacuously.");
        }
    }

    private static void RequireGameAssemblies()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static bool IsBaseCulture(MethodBase method) =>
        method.DeclaringType?.FullName == BaseCharacter && method.Name == "get_Culture";

    // Never run: the canary that proves the scanner sees both routes. The second is the one a source regex missed.
    private sealed class ReadsBaseCulture
    {
        public string? ThroughAgent(Agent agent) => agent.Character?.Culture?.StringId;

        public string? ThroughLocal(BasicCharacterObject character) => character.Culture?.StringId;
    }
}
