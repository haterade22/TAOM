using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.StaleCharacterRepair.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IStaleCharacterAdapter"/>.
///
/// <para><b>What is null, and why.</b> A save-restored object is built by
/// <c>FormatterServices.GetUninitializedObject</c>, so no constructor and no field initializer
/// runs, and it self-registers through <c>MBObjectBase</c>'s <c>[LoadInitializationCallback]</c>.
/// ModuleData XML then upgrades those registered instances in place. A character whose id has been
/// removed from ModuleData is never reached by that pass, so it keeps every
/// <c>BasicCharacterObject</c>-level field at default. <c>CharacterObject.Init()</c> restores only
/// occupation, traits, level and restriction flags.</para>
///
/// <para><b>The four fields repaired here are the ones with an unguarded dereference downstream:</b></para>
/// <list type="bullet">
/// <item><c>DefaultCharacterSkills</c> — <c>BasicCharacterObject.GetSkillValue:292</c> is
/// <c>DefaultCharacterSkills.Skills.GetPropertyValue(skill)</c>. This is the one that produced
/// crash bundle 065939b6.</item>
/// <item><c>UpgradeTargets</c> — declared <c>= new CharacterObject[0]</c> as an auto-property
/// INITIALIZER (<c>CharacterObject.cs:314</c>), which runs in the constructor and is therefore
/// skipped. <c>PartyCharacterVM.cs:1113</c> reads <c>.Length</c> with no guard, so a stale troop in
/// any roster crashes the party screen.</item>
/// <item><c>BodyPropertyRange</c> — <c>BasicCharacterObject.cs:221</c> reads <c>.HairTags</c> on
/// agent spawn. Repaired exactly the way vanilla repairs it at <c>BasicCharacterObject.cs:472-474</c>.</item>
/// <item><c>_basicName</c> — <c>Name</c> returns it directly and <c>ToString()</c> calls
/// <c>Name.ToString()</c>.</item>
/// </list>
///
/// <para><c>_culture</c> is deliberately NOT invented: a wrong culture is a silent gameplay lie,
/// where a null one surfaces through TAOM's own adapter chokepoints. See the feature doc.</para>
/// </summary>
public class StaleCharacterAdapter : IStaleCharacterAdapter
{
    // Resolved once. A per-character AccessTools lookup across thousands of characters would turn
    // a millisecond sweep into a measurable load-time cost.
    private static readonly FieldInfo DefaultSkillsField =
        AccessTools.Field(typeof(BasicCharacterObject), "DefaultCharacterSkills");
    private static readonly FieldInfo BasicNameField =
        AccessTools.Field(typeof(BasicCharacterObject), "_basicName");
    private static readonly MethodInfo BodyPropertyRangeSetter =
        AccessTools.PropertySetter(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BodyPropertyRange));
    private static readonly MethodInfo UpgradeTargetsSetter =
        AccessTools.PropertySetter(typeof(CharacterObject), nameof(CharacterObject.UpgradeTargets));

    // Owner handle for the deferred notice listener, and the count it will report. Static because
    // the listener is attached ONCE per process: re-attaching on a second save load would double
    // the message, and CampaignEvents' removal API is instance-scoped on a Campaign that is still
    // being built when the repair runs. A later load simply overwrites the pending count.
    private static readonly object NoticeOwner = new object();
    private static bool _noticeListenerAttached;
    private static int _pendingNoticeCount;

    private readonly IModLogger _logger;
    private readonly IInquiryAdapter _inquiry;

    // The objects the CURRENT sweep found, so the repair writes to the object the scan actually
    // saw rather than round-tripping through a StringId. GetObject<T> resolves the FIRST assignable
    // type record's match, which is not guaranteed to be the same instance the scan collected.
    // Cleared at the start of every scan, so it never outlives one sweep.
    private readonly Dictionary<string, BasicCharacterObject> _found =
        new Dictionary<string, BasicCharacterObject>(StringComparer.Ordinal);

    public StaleCharacterAdapter(IModLogger logger, IInquiryAdapter inquiry)
    {
        _logger = logger;
        _inquiry = inquiry;
    }

    public void ShowNoticeOnSessionStart(int repairedCount)
    {
        if (repairedCount <= 0) return;
        try
        {
            _pendingNoticeCount = repairedCount;
            if (_noticeListenerAttached) return;
            _noticeListenerAttached = true;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(NoticeOwner, _ =>
            {
                var count = _pendingNoticeCount;
                _pendingNoticeCount = 0;
                if (count <= 0) return;
                try
                {
                    _inquiry?.ShowMessage(
                        "taom_stale_character_repair_notice",
                        "TAOM repaired {COUNT} troop(s) your save references but this version no " +
                        "longer defines. The save loads, but that data is still broken: see " +
                        "Logs/taom_debug for the ids, and keep a backup before overwriting old saves.",
                        "COUNT", count.ToString());
                }
                catch { /* a notice must never break the session start */ }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"StaleCharacterAdapter: could not queue the player notice: {ex.Message}");
        }
    }

    // True when any repairable field is missing. Checked per field rather than inferring the whole
    // stub from one of them, so a partially-repaired object (another mod, an interrupted sweep) is
    // still finished off.
    private static bool IsStale(BasicCharacterObject character) =>
        character.GetDefaultCharacterSkills() == null
        || character.BodyPropertyRange == null
        || character.Name == null
        || (character is CharacterObject c && c.UpgradeTargets == null);

    public IReadOnlyList<string> FindStaleCharacters()
    {
        var found = new List<string>();
        _found.Clear();
        try
        {
            var characters = MBObjectManager.Instance?.GetObjectTypeList<BasicCharacterObject>();
            if (characters == null) return found;

            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null || !IsStale(character)) continue;

                var id = character.StringId ?? string.Empty;
                found.Add(id);
                // A blank or duplicate id would otherwise collide in the map and silently drop a
                // real object; keep the first and let the repair report the rest as unresolved.
                if (!string.IsNullOrEmpty(id) && !_found.ContainsKey(id)) _found[id] = character;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"StaleCharacterAdapter: failed to scan characters: {ex.Message}");
        }
        return found;
    }

    public StubRepairOutcome TryMakeInert(string characterId)
    {
        // One cause, reported once, by the service — not blamed on the player's save data.
        if (DefaultSkillsField == null || BasicNameField == null
            || BodyPropertyRangeSetter == null || UpgradeTargetsSetter == null)
            return StubRepairOutcome.BindingUnavailable;

        if (string.IsNullOrEmpty(characterId)) return StubRepairOutcome.NotResolved;

        try
        {
            if (!_found.TryGetValue(characterId, out var character) || character == null)
                character = MBObjectManager.Instance?.GetObject<BasicCharacterObject>(characterId);
            if (character == null) return StubRepairOutcome.NotResolved;

            // Re-check rather than trusting the scan's list: another mod's load hook may have
            // filled these between the scan and here, and overwriting a real skill set with an
            // empty one would silently zero a troop's stats.
            if (!IsStale(character)) return StubRepairOutcome.AlreadyHealthy;

            if (character.GetDefaultCharacterSkills() == null)
                DefaultSkillsField.SetValue(character, new MBCharacterSkills());

            if (character.BodyPropertyRange == null)
            {
                // Vanilla's own fallback, verbatim (BasicCharacterObject.cs:472-474). Registered,
                // unlike the skill set, because that is what vanilla does here and MBBodyProperty
                // is resolved by id elsewhere. Note the consequence: a default BodyProperties is
                // age 0, so this troop renders as a child if it ever spawns. That is the shipped
                // vanilla behaviour for a face-less character and is preferable to a hard crash.
                var range = MBObjectManager.Instance?.RegisterPresumedObject(
                    new MBBodyProperty(character.StringId));
                if (range != null)
                {
                    range.Init(default(BodyProperties), default(BodyProperties));
                    BodyPropertyRangeSetter.Invoke(character, new object[] { range });
                }
            }

            if (character.Name == null)
                BasicNameField.SetValue(character, new TextObject(character.StringId));

            if (character is CharacterObject troop && troop.UpgradeTargets == null)
                UpgradeTargetsSetter.Invoke(troop, new object[] { new CharacterObject[0] });

            return StubRepairOutcome.Repaired;
        }
        catch (Exception ex)
        {
            _logger.LogError($"StaleCharacterAdapter: failed to repair '{characterId}': {ex.Message}");
            return StubRepairOutcome.Failed;
        }
    }
}
