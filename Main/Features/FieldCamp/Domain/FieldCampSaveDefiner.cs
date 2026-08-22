using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.FieldCamp.Domain;

/// <summary>Base 726901101 continues TAOM's century-stepped block; localIds from 101 (the
/// CareerQuest Module.Initialize crash rule). The source module's 841200601 identity is not
/// carried (fresh save surface, user decision 2026-08-22).</summary>
public class FieldCampSaveDefiner : SaveableTypeDefiner
{
    public FieldCampSaveDefiner()
        : base(726901101)
    {
    }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(CampState), 101);
    }

    protected override void DefineEnumTypes()
    {
        AddEnumDefinition(typeof(CampType), 102);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<string, CampState>));
    }
}
