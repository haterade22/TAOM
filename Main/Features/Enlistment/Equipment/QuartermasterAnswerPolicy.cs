namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Maps an issuance outcome to the quartermaster's reply. <see cref="QuartermasterAnswer.HostOnly"/>
/// is not produced here: the behavior's authority branch sets it without calling the issuer. The
/// default arm is <see cref="QuartermasterAnswer.CannotLoad"/>, so a result added later still gets
/// a truthful "not just now" reply rather than a wrong one.
/// </summary>
public static class QuartermasterAnswerPolicy
{
    public static QuartermasterAnswer For(EquipmentIssueResult result) => result switch
    {
        EquipmentIssueResult.Issued               => QuartermasterAnswer.Issued,
        EquipmentIssueResult.AlreadyIssuedForRank => QuartermasterAnswer.AlreadyDrawn,
        EquipmentIssueResult.NoRosterFound        => QuartermasterAnswer.EmptyStores,
        EquipmentIssueResult.NoValidItems         => QuartermasterAnswer.EmptyStores,
        EquipmentIssueResult.PartyUnavailable     => QuartermasterAnswer.CannotLoad,
        _                                         => QuartermasterAnswer.CannotLoad,
    };
}
