using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles;

public class TaomFactionSelectionVM : CustomBattleFactionSelectionVM
{
    public TaomFactionSelectionVM(Action<BasicCultureObject> onSelectionChanged)
        : base(onSelectionChanged)
    {
    }

    public void ExecuteSelectNextFaction()
    {
        if (Factions.Count == 0) return;
        var nextIndex = (FindCurrentIndex() + 1) % Factions.Count;
        SelectFaction(nextIndex);
    }

    public void ExecuteSelectPreviousFaction()
    {
        if (Factions.Count == 0) return;
        var prevIndex = (FindCurrentIndex() - 1 + Factions.Count) % Factions.Count;
        SelectFaction(prevIndex);
    }

    private int FindCurrentIndex()
    {
        for (int i = 0; i < Factions.Count; i++)
        {
            if (Factions[i].IsSelected)
                return i;
        }
        return 0;
    }
}
