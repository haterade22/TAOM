using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Adapters;

/// <summary>A left click's pick: whether a company of the player's side was named, and whether it is already selected.</summary>
internal readonly record struct SeatPicked(bool Found, bool Selected);

/// <summary>What a right click's ray and pick found: an enemy company, an object the side can order, and whether the ground there takes a company.</summary>
internal readonly record struct SeatRightProbe(bool Enemy, bool Orderable, bool ValidGround);

/// <summary>
/// Every engine read and write of the Seat's command mouse (Seat build-spec 6.4), so the control runs in a test. It reads the player's own
/// companies and the enemy's, projects and casts rays, writes the order controller's selection and gives orders. A client type with no caller in
/// core code; every member runs on the main thread, from <c>OnMissionTick</c>. The probe methods keep what they found for the one action that
/// follows in the same tick.
/// </summary>
internal interface ISeatCommandAdapter
{
    /// <summary>The panel's own gate, read for <see cref="SeatCommandRule"/>.</summary>
    SeatCommandFacts ReadFacts();

    /// <summary>How many companies the player's order controller has selected.</summary>
    int SelectedCount { get; }

    /// <summary>The company of the player's soldier nearest the pixel, if the pointer is within reach and the ground does not hide him; keeps it for <see cref="Select"/>.</summary>
    SeatPicked PickOwn(float pixelX, float pixelY);

    /// <summary>Writes the selection through the order controller, on the company <see cref="PickOwn"/> kept.</summary>
    void Select(SeatSelectAction action);

    /// <summary>
    /// What a right click at a pixel names: the enemy soldier nearest the pointer (else the company the focus handler names), the object the
    /// pointer's ray hits that the side can order, and the ground it hits. Keeps all three for the order that follows.
    /// </summary>
    SeatRightProbe ProbeRight(float pixelX, float pixelY, float rangedX, float rangedY);

    /// <summary>Sends the selected companies to charge the enemy company the last probe found.</summary>
    void Charge();

    /// <summary>Gives the selected companies the order of the object the last probe found.</summary>
    void UseOrderable();

    /// <summary>Moves the selected companies to the ground the last probe found, as a line of no length.</summary>
    void MoveToProbed(bool horizontal);

    /// <summary>Starts a dragged line at the ground under the pointer; false when that ground takes no company.</summary>
    bool LineBegin(float rangedX, float rangedY);

    /// <summary>Previews the line from its start to the ground under the pointer, with the order flags the placer would draw.</summary>
    void LinePreview(float rangedX, float rangedY, bool horizontal);

    /// <summary>Gives the line's order, from its start to the ground under the pointer, and hides the preview.</summary>
    void LineCommit(float rangedX, float rangedY, bool horizontal);

    /// <summary>Hides the preview and forgets the line.</summary>
    void LineCancel();

    /// <summary>A line is being previewed.</summary>
    bool LineShown { get; }
}
