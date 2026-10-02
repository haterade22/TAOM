using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: the face generator is being built (#704). Inside character creation this applies
/// a hero picked on Kysaro's faction screen and dresses the model in its gear (his
/// <c>FaceGenEquipmentPatch</c>), whether or not the face generator is themed; and it tells the camera
/// service whether the themed face generator owns the camera, before the constructor sets the camera up
/// (BodyGeneratorView.cs:156, before its movie loads at :185). Target: the <c>BodyGeneratorView</c>
/// constructor, bound by being the only one, as Player Switcher's Patch77 binds it (a hand-written type
/// array broke when 1.4.8 added a thirteenth parameter); the prefix takes <c>dressedEquipment</c> by
/// name, which <c>FactionUIBindingTests</c> pins. Patch77 postfixes the same constructor and attaches
/// nothing while the faction screen hides Player Switcher's panel. A hero taken over on the faction
/// screen skips this generator, unless the stage list was not vanilla's.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class BodyGeneratorViewConstructorPatch
{
    private static bool _reported;

    static bool Prepare() => typeof(BodyGeneratorView).GetConstructors().Length == 1;

    static MethodBase TargetMethod() => typeof(BodyGeneratorView).GetConstructors()[0];

    static void Prefix(ref Equipment dressedEquipment)
    {
        try
        {
            FactionUIPatchContext.Camera?.OnFaceGeneratorOpening(
                FactionUIPatchContext.Movies?.WillThemeFaceGenerator() == true);
            if (FactionUIPatchContext.State?.IsInCharacterCreation() != true)
                return;
            if (FactionUIPatchContext.Presets?.OnFaceGeneratorOpening() is Equipment presetGear)
                dressedEquipment = presetGear;
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(BodyGeneratorViewConstructorPatch), ex);
        }
    }
}
