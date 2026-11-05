using System;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>One card of the open boon pick: its name, its category, what it gives and, in its own line and colour, what it costs. A click takes it by id.</summary>
public sealed class LongDefeatBoonVM : LongDefeatViewModel
{
    private readonly Action<string> _onPick;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _categoryText = string.Empty;
    private string _text = string.Empty;
    private string _drawbackText = string.Empty;
    private bool _hasDrawback;

    internal LongDefeatBoonVM(Action<string> onPick) => _onPick = onPick;

    [DataSourceProperty]
    public string Name { get => _name; private set => Set(ref _name, value, nameof(Name)); }

    [DataSourceProperty]
    public string CategoryText { get => _categoryText; private set => Set(ref _categoryText, value, nameof(CategoryText)); }

    [DataSourceProperty]
    public string Text { get => _text; private set => Set(ref _text, value, nameof(Text)); }

    [DataSourceProperty]
    public string DrawbackText { get => _drawbackText; private set => Set(ref _drawbackText, value, nameof(DrawbackText)); }

    [DataSourceProperty]
    public bool HasDrawback { get => _hasDrawback; private set => Set(ref _hasDrawback, value, nameof(HasDrawback)); }

    public void ExecutePick() => _onPick(_id);

    internal void Update(BoonCard card)
    {
        if (_id == card.Id)
            return;

        _id = card.Id;
        Name = LongDefeatHudText.OfferName(card.Name);
        CategoryText = LongDefeatHudText.Category(card.Category);
        Text = LongDefeatHudText.OfferName(card.Text);
        HasDrawback = card.HasDrawback;
        DrawbackText = card.HasDrawback ? LongDefeatHudText.OfferName(card.DrawbackText) : string.Empty;
    }
}
