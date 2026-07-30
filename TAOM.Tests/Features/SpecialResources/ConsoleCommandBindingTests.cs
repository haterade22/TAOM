using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.SpecialResources.Cheats;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Pins the engine binding contract for every TAOM console command.
///
/// Why this matters beyond our own command: `CommandLineFunctionality.CollectCommandLineFunctions`
/// builds each command with an UNGUARDED `Delegate.CreateDelegate(typeof(Func&lt;List&lt;string&gt;, string&gt;), m)`.
/// A TAOM method carrying the attribute with the wrong shape throws inside that loop and aborts
/// discovery for every OTHER command in the same pass — including vanilla's `campaign.*` cheats.
/// A refactor that changes a parameter type must fail here, not silently break the game's console.
/// </summary>
[TestClass]
public class ConsoleCommandBindingTests
{
    private static IEnumerable<MethodInfo> GetAttributedCommands()
    {
        // Mirrors the engine's own `GetTypesSafe()` — some TAOM types (UI mixins, widgets) fail to
        // load outside the game host, and a hard GetTypes() would throw before reaching our command.
        Type[] types;
        try
        {
            types = typeof(SpecialResourceCheats).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
        }

        return types
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false).Length > 0);
    }

    [TestMethod]
    public void ConsoleCommands_AllAttributedMethods_MatchEngineDelegateShape()
    {
        // Arrange
        var commands = GetAttributedCommands().ToList();

        // Act / Assert
        Assert.AreNotEqual(0, commands.Count, "Expected at least one attributed console command in the TAOM assembly.");
        foreach (var method in commands)
        {
            var name = $"{method.DeclaringType?.Name}.{method.Name}";
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
            var attribute = (CommandLineFunctionality.CommandLineArgumentFunction)method
                .GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false)[0];

            Assert.AreEqual("taom", attribute.GroupName,
                $"{method.DeclaringType?.Name}.{method.Name} must register under the 'taom' console group.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.Name),
                $"{method.DeclaringType?.Name}.{method.Name} must declare a command name.");
        }
    }
}
