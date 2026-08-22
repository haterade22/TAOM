using System.Collections.Generic;
using TAOM.Features.SupplyLines.Components;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.SupplyLines.Domain;

/// <summary>
/// Save registration for SupplyLines.
///
/// <para>Base id continues TAOM's block (…501/601/701/801/901 are taken; this takes 726901001).
/// Local ids start at 101, NOT 1: the engine's global id is base + localId and TAOM bases step by
/// 100, so ids 1..100 would land inside the previous definer's century (the exact
/// Module.Initialize crash the CareerQuest definer shipped; RCA 2026-06-01).</para>
///
/// <para>The yotthani module used base 841200501 with different type names. Deliberately NOT
/// preserved: no players run the standalone module (user decision 2026-08-22), and TAOM never
/// carries another module's save identity into its own surface.</para>
/// </summary>
public class SupplyLinesSaveDefiner : SaveableTypeDefiner
{
    public SupplyLinesSaveDefiner()
        : base(726901001)
    {
    }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(SupplyOrder), 101);
        AddClassDefinition(typeof(SupplyCaravanComponent), 102);
    }

    protected override void DefineEnumTypes()
    {
        AddEnumDefinition(typeof(SupplyOrderStatus), 103);
        AddEnumDefinition(typeof(SupplyEscortOption), 104);
    }

    protected override void DefineContainerDefinitions()
    {
        // Dictionary<string,int> is a basic-typed container already covered by the engine's
        // SaveableBasicTypeDefiner; re-declaring it asserts. Only OUR class needs a container row.
        ConstructContainerDefinition(typeof(Dictionary<string, SupplyOrder>));
    }
}
