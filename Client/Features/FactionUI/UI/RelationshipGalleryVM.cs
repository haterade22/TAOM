using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI;

/// <summary>A fixed-size card page (six by default) over borrowed native view models.</summary>
internal sealed class RelationshipGalleryVM<T> : ViewModel where T : ViewModel
{
    public const int DefaultPageSize = 6;
    public int PageSize { get; }
    private List<T> _members = new();
    private string _title = string.Empty;
    private int _page;

    [DataSourceProperty] public MBBindingList<T> Items { get; } = new();
    [DataSourceProperty] public string EmptyText { get; }
    [DataSourceProperty] public string Title => $"{_title} ({_members.Count})";
    [DataSourceProperty] public string PageLabel => $"{(HasItems ? _page + 1 : 0)} / {PageCount}";
    [DataSourceProperty] public bool HasItems => _members.Count != 0;
    [DataSourceProperty] public bool IsEmpty => !HasItems;
    [DataSourceProperty] public bool HasPrevious => _page > 0;
    [DataSourceProperty] public bool HasNext => _page + 1 < PageCount;
    private int PageCount => (_members.Count + PageSize - 1) / PageSize;

    public RelationshipGalleryVM(string emptyText, int pageSize = DefaultPageSize)
    {
        EmptyText = emptyText;
        PageSize = pageSize > 0 ? pageSize : DefaultPageSize;
    }

    public void SetItems(IEnumerable<T> primary, IEnumerable<T> overflow, string title)
    {
        _members = primary.Concat(overflow).ToList();
        _title = title;
        _page = 0;
        RefreshPage();
    }

    public void ExecutePrevious()
    {
        if (!HasPrevious) return;
        _page--;
        RefreshPage();
    }

    public void ExecuteNext()
    {
        if (!HasNext) return;
        _page++;
        RefreshPage();
    }

    private void RefreshPage()
    {
        Items.Clear();
        foreach (var member in _members.Skip(_page * PageSize).Take(PageSize))
            Items.Add(member);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PageLabel));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasPrevious));
        OnPropertyChanged(nameof(HasNext));
    }

    public override void OnFinalize()
    {
        // The encyclopedia owns these HeroVMs, including links and image providers.
        Items.Clear();
        _members.Clear();
        base.OnFinalize();
    }
}
