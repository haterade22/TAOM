namespace TAOM.Features.FactionUI.Resources;

/// <summary>How long a front-end image stays in memory (#704, Mike's load-on-demand decision).</summary>
public enum FrontEndSpriteKind
{
    /// <summary>The shared frame of the character-creation screens and the faction screen (<c>cc_*</c>
    /// and the faction screen's own <c>fs_*</c> pieces, which the later stages draw too).</summary>
    Chrome,

    /// <summary>The main menu's own images (<c>mm_*</c>): no other screen draws them.</summary>
    MenuChrome,

    /// <summary>Large faction art (portraits, reveals, emblems, territories, backgrounds): loaded one
    /// image at a time as the faction screen shows it.</summary>
    Art,

    /// <summary>The loading-screen frame: the loading window lives for the whole process.</summary>
    Resident,

    /// <summary>Kysaro's replacements for vanilla's skill icons, registered under the vanilla names for
    /// the session.</summary>
    SkillIcon,
}
