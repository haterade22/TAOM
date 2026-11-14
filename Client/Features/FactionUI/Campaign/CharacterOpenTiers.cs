using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FactionUI.Campaign
{
    // The open perk tiers of the Character screen's current hero: per skill, the tiers the player can choose now.
    // Only PerkVM.PerkStates 1 (EarnedButNotSelected) counts; 4 (EarnedPreviousPerkNotSelected) waits on an
    // earlier tier, 2 and 3 are chosen, 0 is not earned. A pair's two perks share their Level, so a tier counts once.
    // The native SkillVM.NumOfUnopenedPerks counts states 1 and 4 together, so it is only the change signal: the
    // native updates it in the same RefreshLists and ResetChanges calls that move a PerkVM's state.
    internal sealed class CharacterOpenTiers
    {
        internal const int SelectableState = 1;
        private object? _hero;
        private int[] _signal = Array.Empty<int>();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static int OpenTiers(IEnumerable<(int Level, int State)> perks) =>
            perks.Where(p => p.State == SelectableState).Select(p => p.Level).Distinct().Count();

        internal int ForSkill(string skillId) => _counts.TryGetValue(skillId, out int count) ? count : 0;
        internal int ForAttribute(IEnumerable<string> skillIds) => skillIds.Sum(ForSkill);

        // Replaces every count, so nothing from an earlier hero survives.
        internal void Store(IEnumerable<KeyValuePair<string, int>> counts)
        {
            _counts.Clear();
            foreach (var entry in counts) _counts[entry.Key] = entry.Value;
        }

        // True on a new hero, a forced recount (the screen's own Reset or perk choice) or a changed native signal.
        // The signal is copied, so the caller may refill one buffer every frame.
        internal bool NeedsRecount(object hero, IReadOnlyList<int> signal, bool force)
        {
            bool changed = force || !ReferenceEquals(hero, _hero) || !SameSignal(signal);
            if (!changed) return false;
            _hero = hero;
            if (_signal.Length != signal.Count) _signal = new int[signal.Count];
            for (int i = 0; i < signal.Count; i++) _signal[i] = signal[i];
            return true;
        }

        private bool SameSignal(IReadOnlyList<int> signal)
        {
            if (signal.Count != _signal.Length) return false;
            for (int i = 0; i < _signal.Length; i++)
                if (_signal[i] != signal[i]) return false;
            return true;
        }
    }
}
