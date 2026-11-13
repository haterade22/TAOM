using System;
using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The siege engines' half of the field fake (#1160): a restore answers what a test set once and then nothing, a read answers the engines a test set, and a break counts.</summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>What the next restore answers; it then answers <see cref="LdEngineRestore.None"/> until a test sets it again, as a field with nothing destroyed would.</summary>
    public LdEngineRestore RestoreAnswer { get; set; } = LdEngineRestore.None;

    /// <summary>The engines a read reports.</summary>
    public IReadOnlyList<LdEngineState> EngineStates { get; set; } = Array.Empty<LdEngineState>();

    /// <summary>How many engines a break reports destroyed.</summary>
    public int BreakAnswer { get; set; } = 3;

    public LdEngineRestore RestoreEngines()
    {
        Calls.Add("RestoreEngines");
        var answer = RestoreAnswer;
        RestoreAnswer = LdEngineRestore.None;
        return answer;
    }

    public IReadOnlyList<LdEngineState> ReadEngines()
    {
        Calls.Add("ReadEngines");
        return EngineStates;
    }

    public int BreakEngines()
    {
        Calls.Add("BreakEngines");
        return BreakAnswer;
    }
}
