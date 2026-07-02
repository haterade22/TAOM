using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// Assembly-wide ban on calling <c>PartyBase.get_Owner</c>. The getter is a throwing computed
/// property: for a non-mobile (settlement) party it returns <c>Settlement.Owner</c>, which is
/// <c>OwnerClan.Leader</c> with no null guard — and <c>Settlement.OwnerClan</c> is null for any
/// settlement that is neither Village, Town, nor Hideout (TAOM_Map's <c>retirement_retreat</c>
/// CustomSettlementComponent settlement). A <c>?.</c> on the result guards nothing because the
/// throw happens inside the getter (.claude/rules/adapters.md, issue #281 family).
///
/// This shipped as a deterministic new-campaign CTD in v2.0.8.0 (crash 0b462fd8): the settlement
/// daily tick fed <c>settlement.Party</c> into <c>TaomPartyHealingModel.GetDailyHealingHpForHeroes</c>,
/// which resolved the career-passive hero via <c>party?.Owner</c>. Safe replacement:
/// <c>CareerPassiveHero.ResolveId</c> (<c>MobileParty?.Owner ?? LeaderHero</c>).
///
/// The scan walks raw IL bytes (not Harmony's <c>PatchProcessor.ReadMethodBody</c>, which throws
/// <c>NotSupportedException</c> on generic method definitions — 37 of them in TAOM.dll) so every
/// method body in the assembly is covered.
/// </summary>
[TestClass]
public class PartyOwnerGetterBanTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void TaomAssembly_AllMethodBodies_NeverCallPartyBaseOwnerGetter()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var bannedGetter = typeof(PartyBase).GetProperty(nameof(PartyBase.Owner))?.GetGetMethod();
        Assert.IsNotNull(bannedGetter, "PartyBase.Owner no longer exists on the installed engine — retire this ban test.");

        var violations = new List<string>();
        var unreadable = new List<string>();
        int scanned = 0;

        foreach (var method in EnumerateMethods(typeof(TAOM.IoC).Assembly))
        {
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception ex)
            {
                unreadable.Add($"{method.DeclaringType?.FullName}.{method.Name} — {ex.GetType().Name}: {ex.Message}");
                continue;
            }
            if (il == null)
                continue;

            scanned++;
            if (ExtractCalledMethods(method, il).Any(target => MethodsMatch(target, bannedGetter)))
                violations.Add($"{method.DeclaringType?.FullName}.{method.Name}");
        }

        if (scanned < 1000)
            Assert.Inconclusive($"Only {scanned} method bodies scanned (expected thousands) — assembly enumeration problem, not a genuine pass.");

        if (violations.Count > 0 || unreadable.Count > 0)
        {
            var sb = new StringBuilder();
            if (violations.Count > 0)
            {
                sb.AppendLine($"{violations.Count} method(s) call PartyBase.get_Owner — a throwing computed getter (NREs for settlements with null OwnerClan, crash 0b462fd8).");
                sb.AppendLine("Resolve the hero via CareerPassiveHero.ResolveId / party.MobileParty?.Owner instead:");
                foreach (var v in violations.Distinct()) sb.AppendLine("  " + v);
            }
            if (unreadable.Count > 0)
            {
                sb.AppendLine($"{unreadable.Count} method bodies could not be read — the ban cannot vouch for them:");
                foreach (var u in unreadable.Take(20)) sb.AppendLine("  " + u);
            }
            Assert.Fail(sb.ToString());
        }
    }

    private static bool MethodsMatch(MethodBase candidate, MethodInfo banned)
        => candidate.Name == banned.Name && candidate.DeclaringType == banned.DeclaringType;

    private static IEnumerable<MethodBase> EnumerateMethods(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
        }

        foreach (var type in types)
        {
            foreach (var m in type.GetMethods(all)) yield return m;
            foreach (var c in type.GetConstructors(all)) yield return c;
        }
    }

    // --- raw IL walking ---

    private static readonly Dictionary<short, OpCode> OpCodeMap = BuildOpCodeMap();

    private static Dictionary<short, OpCode> BuildOpCodeMap()
    {
        var map = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode op)
                map[op.Value] = op;
        }
        return map;
    }

    /// <summary>
    /// Yields every method referenced by a call-shaped instruction (InlineMethod operand:
    /// call / callvirt / newobj / ldftn / ldvirtftn / jmp) in the method's IL. Tokens are
    /// resolved with the declaring type's + method's own generic parameters as context so
    /// generic method definitions resolve too. Unresolvable tokens are skipped — the banned
    /// getter lives on a non-generic type and always resolves.
    /// </summary>
    private static IEnumerable<MethodBase> ExtractCalledMethods(MethodBase method, byte[] il)
    {
        var typeContext = method.DeclaringType != null && method.DeclaringType.IsGenericType
            ? method.DeclaringType.GetGenericArguments()
            : null;
        var methodContext = method.IsGenericMethod ? method.GetGenericArguments() : null;

        int pos = 0;
        while (pos < il.Length)
        {
            byte first = il[pos++];
            short key = first != 0xFE ? first : (short)(0xFE00 | il[pos++]);
            if (!OpCodeMap.TryGetValue(key, out var op))
                yield break; // unknown opcode — bail on this body rather than misparse the rest

            if (op.OperandType == OperandType.InlineMethod)
            {
                MethodBase resolved = null;
                try
                {
                    resolved = method.Module.ResolveMethod(BitConverter.ToInt32(il, pos), typeContext, methodContext);
                }
                catch
                {
                    // token needs a context this scan can't supply (never the case for
                    // PartyBase.get_Owner — non-generic memberRef); skip it
                }
                if (resolved != null)
                    yield return resolved;
            }

            pos += OperandSize(op.OperandType, il, pos);
        }
    }

    private static int OperandSize(OperandType operandType, byte[] il, int pos)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return 1;
            case OperandType.InlineVar:
                return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                return 8;
            case OperandType.InlineSwitch:
                return 4 + BitConverter.ToInt32(il, pos) * 4;
            default: // InlineBrTarget, InlineField, InlineI, InlineMethod, InlineSig, InlineString, InlineTok, InlineType, ShortInlineR
                return 4;
        }
    }
}
