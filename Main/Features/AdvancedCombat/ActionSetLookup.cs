using System;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Drift-guard test for an <c>MBActionSet.GetActionSet(id)</c> lookup (#1065). On v1.5.6 a missing id logs
/// "Action set ... could not be found, using default action set!" and returns set 0 (as_human_warrior), which
/// passes <c>IsValid</c>, so <c>IsValid</c> alone never reports a miss. The set is the one asked for only when it
/// is valid and carries the requested name. Ordinal, because the engine matches ids with a strcmp-like search
/// (docs/reference/bannerlord-animation-system-map.md section 7). Pure so it needs no engine type; call sites
/// pass <c>set.IsValid</c> and <c>set.GetName()</c>, which returns "Invalid" for an invalid set without a native
/// call (v1.5.6 <c>MBActionSet.cs:36-43</c>).
/// </summary>
public static class ActionSetLookup
{
    public static bool Found(string requestedId, bool isValid, string lookedUpName)
        => isValid && requestedId != null && string.Equals(requestedId, lookedUpName, StringComparison.Ordinal);
}
