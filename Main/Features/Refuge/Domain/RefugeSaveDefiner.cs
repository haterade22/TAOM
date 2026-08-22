using System.Collections.Generic;
using TAOM.Features.Refuge.Components;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.Refuge.Domain;

/// <summary>Base 726901201 continues TAOM's century-stepped block; localIds from 101. The source
/// module's 884412700 identity is not carried (fresh save surface, user decision 2026-08-22).</summary>
public class RefugeSaveDefiner : SaveableTypeDefiner
{
    public RefugeSaveDefiner()
        : base(726901201)
    {
    }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(RefugeData), 101);
        AddClassDefinition(typeof(RefugePartyComponent), 102);
    }

    protected override void DefineEnumTypes()
    {
        AddEnumDefinition(typeof(RefugeTier), 103);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<string, RefugeData>));
    }
}
