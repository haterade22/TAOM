using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The clan page's roster panel: the native Members, Wars and Blood Feuds lists as paged galleries,
/// plus the clan's lore, one shown at a time behind a tab (Members by default). Each list keeps its
/// native order and returns to its first page on every refresh. The entries are borrowed from the
/// native page and never finalized here.
/// </summary>
internal sealed class ClanRosterSectionsVM<THero, TFaction> : ViewModel
    where THero : ViewModel where TFaction : ViewModel
{
    internal const int Members = 0, Wars = 1, BloodFeuds = 2, Info = 3;
    private readonly string[] _labels = { "", "", "", "" };
    private int _selected = Members;

    [DataSourceProperty] public RelationshipGalleryVM<THero> MembersGallery { get; }
    [DataSourceProperty] public RelationshipGalleryVM<TFaction> WarsGallery { get; }
    [DataSourceProperty] public RelationshipGalleryVM<TFaction> BloodFeudsGallery { get; }
    [DataSourceProperty] public string InformationText { get; private set; } = "";
    [DataSourceProperty] public string MembersLabel => _labels[Members];
    [DataSourceProperty] public string WarsLabel => _labels[Wars];
    [DataSourceProperty] public string BloodFeudsLabel => _labels[BloodFeuds];
    [DataSourceProperty] public string InfoLabel => _labels[Info];
    [DataSourceProperty] public bool IsMembersSelected => _selected == Members;
    [DataSourceProperty] public bool IsWarsSelected => _selected == Wars;
    [DataSourceProperty] public bool IsBloodFeudsSelected => _selected == BloodFeuds;
    [DataSourceProperty] public bool IsInfoSelected => _selected == Info;

    public ClanRosterSectionsVM(string emptyText, int pageSize)
    {
        MembersGallery = new RelationshipGalleryVM<THero>(emptyText, pageSize);
        WarsGallery = new RelationshipGalleryVM<TFaction>(emptyText, pageSize);
        BloodFeudsGallery = new RelationshipGalleryVM<TFaction>(emptyText, pageSize);
    }

    /// <summary>Takes a fresh snapshot of the native lists and lore; keeps the selected tab.</summary>
    public void SetSections(IEnumerable<THero> members, string membersLabel, IEnumerable<TFaction> wars, string warsLabel,
        IEnumerable<TFaction> bloodFeuds, string bloodFeudsLabel, string informationText, string infoLabel)
    {
        Fill(MembersGallery, Members, members, membersLabel);
        Fill(WarsGallery, Wars, wars, warsLabel);
        Fill(BloodFeudsGallery, BloodFeuds, bloodFeuds, bloodFeudsLabel);
        _labels[Info] = infoLabel ?? "";
        InformationText = informationText ?? "";
        OnPropertyChanged(nameof(InformationText));
        OnPropertyChanged(nameof(MembersLabel));
        OnPropertyChanged(nameof(WarsLabel));
        OnPropertyChanged(nameof(BloodFeudsLabel));
        OnPropertyChanged(nameof(InfoLabel));
        Show(_selected);
    }

    public void ExecuteShowMembers() => Show(Members);
    public void ExecuteShowWars() => Show(Wars);
    public void ExecuteShowBloodFeuds() => Show(BloodFeuds);
    public void ExecuteShowInfo() => Show(Info);

    private void Fill<T>(RelationshipGalleryVM<T> gallery, int section, IEnumerable<T>? items, string label) where T : ViewModel
    {
        _labels[section] = label ?? "";
        gallery.SetItems(items?.ToList() ?? new List<T>(), Enumerable.Empty<T>(), _labels[section]);
    }

    private void Show(int section)
    {
        _selected = section;
        OnPropertyChanged(nameof(IsMembersSelected));
        OnPropertyChanged(nameof(IsWarsSelected));
        OnPropertyChanged(nameof(IsBloodFeudsSelected));
        OnPropertyChanged(nameof(IsInfoSelected));
    }

    public override void OnFinalize()
    {
        // The encyclopedia page owns the native hero and faction VMs.
        MembersGallery.OnFinalize();
        WarsGallery.OnFinalize();
        BloodFeudsGallery.OnFinalize();
        base.OnFinalize();
    }
}
