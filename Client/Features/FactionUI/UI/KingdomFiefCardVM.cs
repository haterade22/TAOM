using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Library;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// A kingdom-page fief card over the native settlement entry: the native name, link and tooltip,
/// plus the settlement's approved encyclopedia picture. The native entry is borrowed and never
/// finalized here; the picture file is only resolved when a card is first bound.
/// </summary>
internal sealed class KingdomFiefCardVM : ViewModel
{
    private readonly EncyclopediaSettlementVM _native;
    private string? _pictureFile;

    public KingdomFiefCardVM(EncyclopediaSettlementVM native) => _native = native;

    [DataSourceProperty] public string NameText => _native.NameText;
    [DataSourceProperty] public string PictureFile => _pictureFile ??= KingdomFiefPicture.Resolve(_native);

    public void ExecuteLink() => _native.ExecuteLink();
    public void ExecuteBeginHint() => _native.ExecuteBeginHint();
    public void ExecuteEndHint() => _native.ExecuteEndHint();
}

/// <summary>
/// Reads the settlement behind a native <see cref="EncyclopediaSettlementVM"/>. v1.5.6 keeps it in
/// <c>private Settlement _settlement</c> (null for a hideout) with no public accessor, so the
/// encyclopedia_pictures.json lookup goes by the settlement's own id, never by its name.
/// </summary>
internal static class KingdomFiefPicture
{
    private static readonly FieldInfo? SettlementField =
        typeof(EncyclopediaSettlementVM).GetField("_settlement", BindingFlags.Instance | BindingFlags.NonPublic);
    private static bool _reportedMissingField;

    internal static string Resolve(EncyclopediaSettlementVM card)
    {
        try
        {
            if (SettlementField?.GetValue(card) is Settlement settlement)
                return EncyclopediaPictureCache.ResolveSettlement(settlement.StringId,
                    settlement.Culture?.StringId ?? "", settlement.IsCastle ? "castle" : "town");
            if (SettlementField == null && !_reportedMissingField)
            {
                _reportedMissingField = true;
                CampaignPresentation.LogInfo("EncyclopediaSettlementVM._settlement not found; kingdom fief cards use the default picture.");
            }
            // The map's generic town picture, never a guess from the display name.
            return EncyclopediaPictureCache.ResolveSettlement("", "", "town");
        }
        catch (Exception e)
        {
            CampaignPresentation.LogError("Could not resolve a kingdom fief picture", e);
            return "";
        }
    }
}
