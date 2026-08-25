using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace TAOM.Tests.Migration;

/// <summary>
/// Raw-IL call scanner shared by the assembly-wide ban tests.
///
/// Walks method bodies byte by byte rather than using Harmony's
/// <c>PatchProcessor.ReadMethodBody</c>, which throws <c>NotSupportedException</c> on generic
/// method definitions (37 of them in TAOM.dll) and would silently leave those bodies unscanned.
/// Extracted from <c>PartyOwnerGetterBanTests</c> when the second ban landed; the behaviour is
/// unchanged, so both bans cover the same surface.
/// </summary>
public static class IlCallScanner
{
    /// <summary>
    /// Names of every method in <paramref name="assembly"/> whose IL contains a call-shaped
    /// instruction targeting a method <paramref name="isBanned"/> accepts.
    /// </summary>
    /// <param name="unreadable">Bodies that threw while being read. A non-empty list means the
    /// ban cannot vouch for those methods and the caller must fail rather than pass.</param>
    /// <param name="scanned">How many bodies were actually walked, so a caller can reject an
    /// enumeration failure that would otherwise look like a clean pass.</param>
    public static List<string> FindCallers(
        Assembly assembly,
        Func<MethodBase, bool> isBanned,
        out List<string> unreadable,
        out int scanned)
    {
        var violations = new List<string>();
        unreadable = new List<string>();
        scanned = 0;

        foreach (var method in EnumerateMethods(assembly))
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
            if (ExtractCalledMethods(method, il).Any(isBanned))
                violations.Add($"{method.DeclaringType?.FullName}.{method.Name}");
        }

        return violations;
    }

    /// <summary>Same declaring type and name. Deliberately loose enough to catch every overload.</summary>
    public static bool SameMethod(MethodBase candidate, MethodBase banned)
        => candidate != null && banned != null
           && candidate.Name == banned.Name
           && candidate.DeclaringType == banned.DeclaringType;

    public static IEnumerable<MethodBase> EnumerateMethods(Assembly assembly)
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
    /// generic method definitions resolve too. Unresolvable tokens are skipped — a banned
    /// member on a non-generic type always resolves.
    /// </summary>
    public static IEnumerable<MethodBase> ExtractCalledMethods(MethodBase method, byte[] il)
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
                    // token needs a context this scan cannot supply; skip it
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
