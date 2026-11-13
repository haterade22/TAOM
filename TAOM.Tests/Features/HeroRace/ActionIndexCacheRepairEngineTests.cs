using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Diffs the installed engine's <c>ActionIndexCache</c> static constructor against
/// <see cref="ActionIndexCacheRepair.ResolveActionName"/>.
///
/// The repair looks an action up by field name. That is right unless the engine's cctor builds a
/// field from a different literal, and <c>KnownNameOverrides</c> then has to say so. Two such
/// fields (<c>act_wreckage_death_01</c> and <c>_02</c>) sat unmapped from v1.5.3 on and showed up
/// only as "2 unknown to this engine build" in the log. The round-trip guard cannot catch a wrong
/// or missing row: it compares the engine's answer with the map's own output.
///
/// The test reads the cctor's IL, which runs no initializer. Every field must come from one
/// <c>ldstr literal; call Create; stsfld field</c> triple, except <c>act_none</c>. An opcode or
/// shape the walker does not know fails the test instead of passing on an empty result.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ActionIndexCacheRepairEngineTests
{
    private const byte LdcI4M1 = 0x15;
    private const byte Ret = 0x2A;
    private const byte Call = 0x28;
    private const byte Ldstr = 0x72;
    private const byte Newobj = 0x73;
    private const byte Stsfld = 0x80;

    [TestMethod]
    public void ResolveActionName_EveryFieldOfTheInstalledEngineCctor_MapsToItsCreateLiteral()
    {
        System.Reflection.Module module = typeof(ActionIndexCache).Module;
        ConstructorInfo? cctor = typeof(ActionIndexCache).TypeInitializer;
        Assert.IsNotNull(cctor, "ActionIndexCache has no static constructor in the installed engine: the repair's premise changed.");
        byte[] il = cctor!.GetMethodBody()!.GetILAsByteArray()!;

        var pairs = new List<(string Field, string Literal)>();
        string? pendingLiteral = null;
        int constructed = 0;
        int i = 0;
        while (i < il.Length)
        {
            switch (il[i])
            {
                case LdcI4M1:
                case Ret:
                    i += 1;
                    break;
                case Ldstr:
                    pendingLiteral = module.ResolveString(BitConverter.ToInt32(il, i + 1));
                    i += 5;
                    break;
                case Call:
                {
                    MethodBase callee = module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                    Assert.AreEqual(typeof(ActionIndexCache), callee.DeclaringType, "cctor calls a method other than ActionIndexCache.Create");
                    Assert.AreEqual("Create", callee.Name, "cctor calls a method other than ActionIndexCache.Create");
                    i += 5;
                    break;
                }
                case Newobj:
                    constructed++;
                    i += 5;
                    break;
                case Stsfld:
                {
                    FieldInfo field = module.ResolveField(BitConverter.ToInt32(il, i + 1));
                    if (pendingLiteral != null)
                        pairs.Add((field.Name, pendingLiteral));
                    pendingLiteral = null;
                    i += 5;
                    break;
                }
                default:
                    Assert.Fail($"ActionIndexCache's cctor holds opcode 0x{il[i]:X2} at IL offset {i}, which this walker does not read. " +
                                "Extend the walker, then re-diff the field names against the literals.");
                    break;
            }
        }

        int fieldCount = typeof(ActionIndexCache)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Count(f => f.FieldType == typeof(ActionIndexCache));
        Assert.AreEqual(fieldCount, pairs.Count + constructed,
            "Every ActionIndexCache field must be set by one Create(literal) or by the act_none constructor call. " +
            "A new form of initialisation means the walker, and the repair, need a second look.");
        Assert.AreEqual(pairs.Count, pairs.Select(p => p.Field).Distinct().Count(), "A field is assigned twice in the cctor.");

        List<string> wrong = pairs
            .Where(p => ActionIndexCacheRepair.ResolveActionName(p.Field) != p.Literal)
            .Select(p => $"{p.Field}: the engine uses '{p.Literal}', the map gives '{ActionIndexCacheRepair.ResolveActionName(p.Field)}'")
            .ToList();
        Assert.AreEqual(0, wrong.Count,
            "KnownNameOverrides disagrees with the engine's static constructor. Add, change or remove the row:\n" +
            string.Join("\n", wrong));
    }
}
