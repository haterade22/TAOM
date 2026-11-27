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
    /// Counts the selectable companies of the player's side with a soldier whose projected chest is inside the rectangle, and keeps them for
    /// <see cref="SelectBox"/>. The ground never hides a soldier here, as in any box select.
    /// </summary>
    int PickBox(SeatRect box);

    /// <summary>Writes the selection through the order controller, on the companies <see cref="PickBox"/> kept.</summary>
    void SelectBox(SeatSelectAction action);

    /// <summary>Shows the selection rectangle on the Seat's cursor layer, in real pixels.</summary>
    void BoxShow(SeatRect box);

    /// <summary>Hides the selection rectangle; a no-op when it is not shown.</summary>
    void BoxHide();

    /// <summary>What a transfer into the company at a number key's index reads: the target, and the selection with its soldier counts.</summary>
    SeatTransferFacts ReadTransfer(int formationIndex);

    /// <summary>Takes the target out of the selection if it is in it, and moves <paramref name="number"/> soldiers of the selected companies into it.</summary>
    void Transfer(int formationIndex, int number);

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

    /// <summary>
    /// The teardown: forgets the flags and the focus subscription with no native call, because the scene may be gone (as Marshal's
    /// <c>DropFlags</c> does). Safe to call twice.
    /// </summary>
    void Drop();
}
