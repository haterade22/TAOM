using System.Collections.Generic;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace;

/// <summary>Which of the two independently-tuned framing surfaces a lookup refers to.</summary>
public enum RacePositionSurface
{
    /// <summary>The 3D character tableau: inventory, party, encyclopedia. CharacterAvatarPatch.json.</summary>
    Avatar,

    /// <summary>The 2D portrait thumbnails. CharacterImagePatch.json.</summary>
    Image,
}

/// <summary>
/// Single owner of the two race-framing configs.
///
/// <para>The surfaces are deliberately NOT merged. They are tuned independently against different
/// cameras and different scenes, so a race can legitimately have a row in one file and not the
/// other, and borrowing across files frames the 2D portrait with 3D numbers.</para>
///
/// <para>Rows are handed out as live references so the in-game tuner can mutate them and have the
/// next tableau refresh pick the change up without a reload.</para>
/// </summary>
public interface IRacePositionStore
{
    /// <summary>3D tableau offsets for a race, or null when the race is unconfigured (vanilla framing).</summary>
    RacePositionConfigItem ResolveAvatar(string raceName);

    /// <summary>3D tableau offsets for the <c>mount_&lt;race&gt;</c> row, or null.</summary>
    RacePositionConfigItem ResolveAvatarMount(string raceName);

    /// <summary>2D portrait offsets for a race, or null when the race is unconfigured.</summary>
    RacePositionConfigItem ResolveImage(string raceName);

    /// <summary>
    /// Returns the live row for a race on a surface, creating a zeroed one if absent. A zeroed row
    /// is a no-op offset, so adding one never changes what the player sees until it is edited.
    /// </summary>
    RacePositionConfigItem GetOrAdd(RacePositionSurface surface, string raceName);

    /// <summary>Every configured row on a surface, in file order.</summary>
    IReadOnlyList<RacePositionConfigItem> List(RacePositionSurface surface);

    /// <summary>Re-reads both files from disk, discarding unsaved edits.</summary>
    void Reload();

    /// <summary>Writes both files back to <c>ModuleData/configs</c>.</summary>
    void Save();
}
