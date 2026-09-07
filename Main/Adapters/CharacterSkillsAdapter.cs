using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ICharacterSkillsAdapter"/>.
///
/// Reading needs no reflection — <c>BasicCharacterObject.GetDefaultCharacterSkills()</c> is public
/// (v1.4.8 <c>:287-290</c>). Writing does: the backing <c>DefaultCharacterSkills</c> is a
/// <c>protected</c> field with no setter, and the only vanilla paths that assign it are
/// <c>Deserialize</c>, <c>FillFrom</c> and <c>InitializeHeroBasicCharacterOnAfterLoad</c>. A
/// character restored from a save whose XML definition no longer exists reaches none of them:
/// <c>CharacterObject</c>'s <c>[LoadInitializationCallback]</c> runs <c>Init()</c>
/// (<c>CharacterObject.cs:402-414</c>), which sets occupation, traits, level and restriction flags
/// and nothing else.
///
/// Enumeration mirrors <see cref="ObjectManagerAdapter"/>: <c>BasicCharacterObject</c> is not
/// sealed, so <c>GetObjectTypeList</c> walks every type record and collects what is assignable —
/// which is what we want, since the null field lives on the base type.
/// </summary>
public class CharacterSkillsAdapter : ICharacterSkillsAdapter
{
    // Resolved once. A per-character AccessTools lookup across a few thousand characters would
    // turn a millisecond sweep into a measurable load-time cost.
    private static readonly FieldInfo DefaultSkillsField =
        AccessTools.Field(typeof(BasicCharacterObject), "DefaultCharacterSkills");

    private readonly IModLogger _logger;

    public CharacterSkillsAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> FindCharactersWithNoSkillSet()
    {
        var found = new List<string>();
        try
        {
            var characters = MBObjectManager.Instance?.GetObjectTypeList<BasicCharacterObject>();
            if (characters == null) return found;

            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null) continue;
                // GetDefaultCharacterSkills is a plain field read, so it cannot throw the way a
                // computed TaleWorlds property can — but the enumeration around it runs during
                // load, so the whole loop stays inside the catch below regardless.
                if (character.GetDefaultCharacterSkills() == null)
                    found.Add(character.StringId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"CharacterSkillsAdapter: failed to scan characters: {ex.Message}");
        }
        return found;
    }

    public bool TryGiveEmptySkillSet(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || DefaultSkillsField == null) return false;

        try
        {
            var character = MBObjectManager.Instance?.GetObject<BasicCharacterObject>(characterId);
            if (character == null) return false;
            // Re-check rather than trusting the caller's list: another mod's load hook may have
            // repaired the same character between the scan and here, and overwriting a real skill
            // set with an empty one would silently zero a troop's stats.
            if (character.GetDefaultCharacterSkills() != null) return false;

            // A fresh MBCharacterSkills, not a shared one: its ctor builds the Skills
            // PropertyOwner, and per-character instances cost nothing at these counts while
            // removing any chance of one character's later write aliasing onto another.
            DefaultSkillsField.SetValue(character, new MBCharacterSkills());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"CharacterSkillsAdapter: failed to repair '{characterId}': {ex.Message}");
            return false;
        }
    }
}
