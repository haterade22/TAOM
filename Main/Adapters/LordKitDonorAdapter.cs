using System;
using System.Collections.Generic;
using TAOM.Features.GeneratedLordKits;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public class LordKitDonorAdapter : ILordKitDonorAdapter
{
    public Equipment PickKit(Hero hero, Equipment.EquipmentType equipmentType)
    {
        var request = Describe(hero, equipmentType);
        if (!LordKitSelector.Wants(request))
            return null;

        var pick = LordKitSelector.Pick(request, Candidates(request), MBRandom.RandomInt);
        // The token is an Equipment every lord defined with that roster shares: hand out a copy.
        return (pick?.Token as Equipment)?.Clone();
    }

    private static LordKitRequest Describe(Hero hero, Equipment.EquipmentType equipmentType)
    {
        var cultureId = hero?.Culture?.StringId;
        var character = hero?.CharacterObject;
        if (string.IsNullOrEmpty(cultureId) || character == null || equipmentType == Equipment.EquipmentType.Stealth)
            return null;
        // IsChild is Age < HeroComesOfAge, the comparison vanilla's own aging uses; a NaN age reads as adult there too.
        return new LordKitRequest(cultureId, character.Race, hero.IsFemale,
            equipmentType == Equipment.EquipmentType.Civilian,
            hero.IsLord, !hero.IsChild, hero.Clan?.IsMinorFaction == true);
    }

    // Rebuilt per call: characters belong to the running campaign, and a pick happens only when a lord is
    // equipped by the engine (created, of age, made a lord, stepping down), so there is no cache to go stale
    // between campaigns or as lords die.
    private static List<LordKitCandidate> Candidates(LordKitRequest request)
    {
        var candidates = new List<LordKitCandidate>();
        var objects = MBObjectManager.Instance;
        if (Campaign.Current == null || objects == null)
            return candidates;

        foreach (var character in CharacterObject.All)
        {
            // XML lords of the requested culture, race and sex. HeroObject excludes the faction-picker copies,
            // IsOriginalCharacter excludes generated heroes (their character is a copy of a template).
            var donor = character?.HeroObject;
            if (donor == null || !donor.IsLord || donor.IsHumanPlayerCharacter || !character.IsOriginalCharacter
                || character.IsTemplate || character.Race != request.Race || donor.IsFemale != request.IsFemale
                || !string.Equals(donor.Culture?.StringId, request.CultureId, StringComparison.Ordinal))
                continue;

            // The lord's kit as authored, alive or dead: BasicCharacterObject.Deserialize registers the
            // character's own roster under its id. Live gear would read the engine's shared kit for the dead.
            var roster = objects.GetObject<MBEquipmentRoster>(character.StringId);
            if (roster == null)
                continue;
            foreach (var kit in roster.AllEquipments)
            {
                if (kit == null || kit.IsStealth || kit.IsCivilian != request.IsCivilian || kit[EquipmentIndex.Body].IsEmpty)
                    continue;
                candidates.Add(new LordKitCandidate(character.StringId, request.CultureId, character.Race, donor.IsFemale,
                    kit.IsCivilian, donor.IsAlive, Signature(kit), kit));
            }
        }
        return candidates;
    }

    private static string Signature(Equipment kit)
    {
        var ids = new string[(int)EquipmentIndex.NumEquipmentSetSlots];
        for (var i = 0; i < ids.Length; i++)
            ids[i] = kit[i].Item?.StringId ?? string.Empty;
        return string.Join("|", ids);
    }
}
