using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Core.Validation;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the engine binding contract for EVERY TAOM console command, wherever it lives.
///
/// This is a whole-assembly invariant, which is why it sits under Features/DevConsole rather than
/// beside any one command's feature (it was originally filed under SpecialResources, the first
/// command to ship). Adding a command anywhere in Main/ is automatically covered — there is no
/// per-command wiring to remember.
///
/// Why the contract is load-bearing, read from the v1.4.7 dump
/// (Core/TaleWorlds.Library/TaleWorlds.Library/CommandLineFunctionality.cs):
///
/// * `CollectCommandLineFunctions` walks every loaded assembly that references TaleWorlds.Library,
///   pre-filters on `methodInfo.ReturnType != typeof(string)`, then calls
///   `Delegate.CreateDelegate(typeof(Func&lt;List&lt;string&gt;, string&gt;), methodInfo)` with NO try/catch.
///   A `string`-returning static whose PARAMETER list is anything but `(List&lt;string&gt;)` throws there.
/// * That throw escapes into `ManagedExtensions.CollectCommandLineFunctions`, an [EngineCallback]
///   invoked from TaleWorlds.Native.dll through a bare [MonoPInvokeCallback]. There is no managed
///   backstop anywhere in the decompiled tree, and `Utilities.AddCommandLineFunction` only runs
///   AFTER the collect returns — so a single malformed TAOM method takes out the whole console at
///   minimum, and plausibly game startup. We could not determine the native behavior, so these
///   tests are deliberately conservative.
/// * Duplicate `group.name` keys do NOT throw — `if (!AllFunctions.ContainsKey(text))` silently
///   keeps the first one reached, in unspecified assembly/type/method order. Silent and
///   non-deterministic is worse to debug than loud, hence <see cref="ConsoleCommands_AllAttributedMethods_HaveUniqueQualifiedNames"/>.
/// </summary>
[TestClass]
public class ConsoleCommandBindingTests
{
    // Read-only output uses `print_` and nothing else. Enumerated across all 130 commands in the
    // v1.4.7 dump: `print_` appears 9 times in the campaign group, `dump_` zero times anywhere.
    // Admitting synonyms is how the convention drifts, so they fail the build instead of review.
    private static readonly string[] BannedNamePrefixes = { "dump_", "list_", "get_" };

    private static readonly Regex NameShape = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    private static IEnumerable<MethodInfo> GetAttributedCommands()
    {
        // Mirrors the engine's own `GetTypesSafe()` — some TAOM types (UI mixins, widgets) fail to
        // load outside the game host, and a hard GetTypes() would throw before reaching our commands.
        // FiniteFloatValidator is a deliberately neutral anchor into the Main assembly: it belongs to
        // no feature, so this test does not have to move when commands are added or retired.
        Type[] types;
        try
        {
            types = typeof(FiniteFloatValidator).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
        }

        return types
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false).Length > 0);
    }

    private static CommandLineFunctionality.CommandLineArgumentFunction GetAttribute(MethodInfo method) =>
        (CommandLineFunctionality.CommandLineArgumentFunction)method
            .GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false)[0];

    private static string Describe(MethodInfo method) => $"{method.DeclaringType?.Name}.{method.Name}";

    [TestMethod]
    public void ConsoleCommands_AllAttributedMethods_MatchEngineDelegateShape()
    {
        // Arrange
        var commands = GetAttributedCommands().ToList();

        // Act / Assert
        Assert.AreNotEqual(0, commands.Count, "Expected at least one attributed console command in the TAOM assembly.");
        foreach (var method in commands)
        {
            var name = Describe(method);
            Assert.IsTrue(method.IsStatic, $"{name} must be static.");
            Assert.AreEqual(typeof(string), method.ReturnType, $"{name} must return string.");

            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length, $"{name} must take exactly one parameter.");
            Assert.AreEqual(typeof(List<string>), parameters[0].ParameterType, $"{name} must take List<string>.");

            // The exact call the engine makes; throws ArgumentException on any shape mismatch.
            Delegate.CreateDelegate(typeof(Func<List<string>, string>), method);
        }
    }

    [TestMethod]
    public void ConsoleCommands_AllAttributedMethods_UseTheTaomGroup()
    {
        foreach (var method in GetAttributedCommands())
        {
            var attribute = GetAttribute(method);

            Assert.AreEqual("taom", attribute.GroupName,
                $"{Describe(method)} must register under the 'taom' console group.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.Name),
                $"{Describe(method)} must declare a command name.");
        }
    }

    /// <summary>
    /// A within-group name collision is silently resolved by the engine in favour of whichever method
    /// the assembly/type/method walk reaches first (`CommandLineFunctionality.cs`,
    /// `if (!AllFunctions.ContainsKey(text))`). None of `AppDomain.GetAssemblies()`,
    /// `GetTypesSafe()`, or `Type.GetMethods()` specifies an order, so the loser is not merely
    /// dropped — WHICH one loses can change between runs. Nothing is logged either way.
    /// </summary>
    [TestMethod]
    public void ConsoleCommands_AllAttributedMethods_HaveUniqueQualifiedNames()
    {
        // Arrange
        var byQualifiedName = GetAttributedCommands()
            .Select(m => new { Method = m, Qualified = $"{GetAttribute(m).GroupName}.{GetAttribute(m).Name}" })
            .GroupBy(x => x.Qualified)
            .Where(g => g.Count() > 1)
            .ToList();

        // Assert
        Assert.AreEqual(0, byQualifiedName.Count,
            "Duplicate console command names (the engine silently drops all but one, in unspecified order): "
            + string.Join("; ", byQualifiedName.Select(g => $"{g.Key} declared by {string.Join(" and ", g.Select(x => Describe(x.Method)))}")));
    }

    /// <summary>
    /// Settles the naming convention at build time instead of at review time, across however many
    /// commands the suite grows to. See <see cref="BannedNamePrefixes"/> for the evidence.
    /// </summary>
    [TestMethod]
    public void ConsoleCommands_AllAttributedMethods_UseLowerSnakeCaseNames()
    {
        foreach (var method in GetAttributedCommands())
        {
            var commandName = GetAttribute(method).Name;

            Assert.IsTrue(NameShape.IsMatch(commandName),
                $"{Describe(method)} declares '{commandName}'; console names must match ^[a-z][a-z0-9_]*$.");

            foreach (var banned in BannedNamePrefixes)
            {
                Assert.IsFalse(commandName.StartsWith(banned, StringComparison.Ordinal),
                    $"{Describe(method)} declares '{commandName}'. Read-only commands use the 'print_' prefix "
                    + $"(vanilla uses it 9 times and '{banned}' zero times). See docs/features/dev-console.md.");
            }

            Assert.IsFalse(commandName.StartsWith("taom", StringComparison.Ordinal),
                $"{Describe(method)} declares '{commandName}'; the 'taom' group already carries the prefix.");
        }
    }

    /// <summary>
    /// Invokes every command exactly as the engine would, with no campaign loaded, and asserts each
    /// returns <see cref="CampaignCheats.CampaignNotStarted"/>. Four things fall out of one assertion:
    /// the cheat gate exists, it runs BEFORE any campaign-state access, the command does not throw on
    /// the most common real misuse, and the declaring type's static initializer does not touch engine
    /// state (TOR_Core's command class opens with a static field initialized from its ability factory —
    /// that pattern fails here, which is exactly why we do not copy it).
    ///
    /// Deterministic in the test host: `Campaign.Current` is a plain auto-property, so it is null with
    /// no game, and `CampaignCheats.CheckCheatUsage` tests it first and returns without ever reading
    /// `Game.Current`. An empty argument list is required — `CheckHelp` returns false for it, so
    /// gate-then-help and help-then-gate orderings both land on the same string.
    ///
    /// Limit, stated rather than oversold: this proves the `Campaign.Current == null` branch only. The
    /// `Game.Current.CheatMode` half needs a live Game and is covered structurally by routing every
    /// command through the shared guard, not by this test.
    /// </summary>
    [TestMethod]
    public void ConsoleCommands_AllAttributedMethods_WithoutCampaign_ReturnCampaignNotStartedAndDoNotThrow()
    {
        foreach (var method in GetAttributedCommands())
        {
            var command = (Func<List<string>, string>)Delegate.CreateDelegate(typeof(Func<List<string>, string>), method);

            string result;
            try
            {
                result = command(new List<string>());
            }
            catch (Exception ex)
            {
                Assert.Fail($"{Describe(method)} threw {ex.GetType().Name} with no campaign loaded. "
                    + "The engine dispatches console commands across a native reverse-P/Invoke with no "
                    + $"managed backstop, so this escapes into TaleWorlds.Native.dll. Detail: {ex.Message}");
                return;
            }

            Assert.AreEqual(CampaignCheats.CampaignNotStarted, result,
                $"{Describe(method)} must gate on CampaignCheats.CheckCheatUsage (or the shared "
                + "DevConsole guard) before touching any campaign state.");
        }
    }
}
