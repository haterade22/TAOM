using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(FaceGenVM), "UpdateRaceAndGenderBasedResources")]
[HarmonyPatchCategory("Patch6_FaceGenIcons")]
public class FaceGenVM_UpdateRaceAndGenderBasedResources_Patch
{
    private static IOnFaceGenUpdateRaceResources _hook;

    public static void Initialize(IOnFaceGenUpdateRaceResources hook)
    {
        _hook = hook;
    }

    [HarmonyPostfix]
    public static void Postfix(FaceGenVM __instance)
    {
        if (_hook == null)
            return;

        var race = __instance.RaceSelector.SelectedIndex;
        var gender = __instance.SelectedGender;

        var beardItems = __instance.BeardTypes
            .Select(item => (IFaceGenItem)new FacegenListItemAdapter(item))
            .ToList();

        var hairItems = __instance.HairTypes
            .Select(item => (IFaceGenItem)new FacegenListItemAdapter(item))
            .ToList();

        _hook.OnUpdateRaceAndGenderBasedResources(race, gender, beardItems, hairItems);
    }
}
