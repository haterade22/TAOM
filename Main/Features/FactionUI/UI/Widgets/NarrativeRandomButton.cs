using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>
/// The themed backstory screens' "Random" button (#704, Kysaro's): each click picks a random option on
/// the current backstory menu and presses Next. Kysaro's version also tried to keep going on the
/// following menus by itself, but every backstory menu is the same movie (the narrative view loads it
/// once, v1.5.3 CharacterCreationNarrativeStageView.cs:82), so the clicked button never fired again in
/// his module either; that dead cascade is not ported.
/// </summary>
public class NarrativeRandomButton : ButtonWidget
{
    private const string ItemListId = "ItemList";
    private const string NextButtonId = "NextButton";

    private static readonly MethodInfo? HandleClickMethod =
        typeof(ButtonWidget).GetMethod("HandleClick", BindingFlags.Instance | BindingFlags.NonPublic);

    public NarrativeRandomButton(UIContext context)
        : base(context)
    {
    }

    protected override void HandleClick()
    {
        base.HandleClick();
        PickAndAdvance();
    }

    private void PickAndAdvance()
    {
        try
        {
            Widget root = this;
            while (root.ParentWidget != null)
                root = root.ParentWidget;

            var options = root.FindChild(ItemListId, true)?.Children;
            if (options == null || options.Count == 0)
                return;

            if (options[MBRandom.RandomInt(options.Count)] is ButtonWidget option)
                HandleClickMethod?.Invoke(option, null);
            if (root.FindChild(NextButtonId, true) is ButtonWidget next)
                HandleClickMethod?.Invoke(next, null);
        }
        catch (Exception)
        {
            // A widget has no logger; a failed pick leaves the menu as the player left it.
        }
    }
}
