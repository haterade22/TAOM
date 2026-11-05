namespace TAOM.Features.MarshalsWord;

/// <summary>The MCM values behind Marshal's Word (build-spec section 4).</summary>
internal interface IMarshalSettingsProvider
{
    /// <summary>Marshal's Word acts in a battle. Read when the session decides whether to go live.</summary>
    bool Enabled { get; }

    /// <summary>A click or drag on the ground moves the selected companies as one body (stage M2).</summary>
    bool KeepArray { get; }

    /// <summary>Companies ordered together walk at paces that bring them in at the same time (stage M2).</summary>
    bool ArriveTogether { get; }

    /// <summary>The selected companies and the companies they mark wear an outline while the order panel is open (stage M4).</summary>
    bool Outlines { get; }

    /// <summary>A Square order on a player company builds the plain engine square, which keeps a chosen side (stage M5).</summary>
    bool HollowSquare { get; }

    /// <summary>A company with one rider among its foot soldiers keeps the foot soldiers' spacing limits. Read at patch time, so a change needs a restart (stage M5).</summary>
    bool SpacingRepair { get; }
}
