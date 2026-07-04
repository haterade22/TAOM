using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// One faction banner button in the popup's Leaders/Allies rows (Relationship.xml).
/// LOTRAOM parity + two fixes: BannerImageIdentifierVM (ImageIdentifierVM is abstract
/// on 1.4.6) and the Hint property the prefab binds but the source VM never had.
/// </summary>
public sealed class FactionRelationshipVM : ViewModel
{
    private readonly IFaction _faction;
    private ImageIdentifierVM _imageIdentifier;
    private string _nameText;
    private HintViewModel _hint;

    public FactionRelationshipVM(IFaction faction)
    {
        _faction = faction;
        _imageIdentifier = new BannerImageIdentifierVM(faction?.Banner, nineGrid: true);
        _nameText = faction?.Name?.ToString() ?? "";
        _hint = new HintViewModel(faction?.Name);
    }

    public void ExecuteLink()
    {
        if (_faction?.EncyclopediaLink != null)
            Campaign.Current?.EncyclopediaManager?.GoToLink(_faction.EncyclopediaLink);
    }

    [DataSourceProperty]
    public string NameText
    {
        get => _nameText;
        set
        {
            if (value != _nameText)
            {
                _nameText = value;
                OnPropertyChangedWithValue(value, nameof(NameText));
            }
        }
    }

    [DataSourceProperty]
    public ImageIdentifierVM ImageIdentifier
    {
        get => _imageIdentifier;
        set
        {
            if (value != _imageIdentifier)
            {
                _imageIdentifier = value;
                OnPropertyChangedWithValue(value, nameof(ImageIdentifier));
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel Hint
    {
        get => _hint;
        set
        {
            if (value != _hint)
            {
                _hint = value;
                OnPropertyChangedWithValue(value, nameof(Hint));
            }
        }
    }
}
