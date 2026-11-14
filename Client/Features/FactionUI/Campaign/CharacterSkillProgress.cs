using System;
using System.Collections.Generic;

namespace TAOM.Features.FactionUI.Campaign
{
    // The perk timeline's progress, from the native SkillVM.Level and the sorted, distinct PerkVM.Level tiers
    // only: no XP and no cap. Tier i is drawn in perk column i.
    internal static class CharacterSkillProgress
    {
        internal static float TierCentre(int tier) => CharacterPerkItemVM.ColumnX(tier) + CharacterPerkItemVM.ColumnWidth / 2f;

        // The rail runs from RailInset to the content width less RailInset; the gold fill starts at the rail's
        // left end and stops at the exact marker.
        internal const float RailInset = 24f;

        // The one displayed endpoint: the logical marker held on the rail. The fill ends here and the drawn
        // diamond is centred here, so the two coincide at level 0 and at the cap. Content narrower than the
        // two insets has no rail: the endpoint is its middle. Empty or NaN content, or a NaN marker, gives a
        // finite start.
        internal static float RailEnd(float marker, float content)
        {
            if (!(content > 0f)) return 0f;
            float start = Math.Min(RailInset, content / 2f);
            if (float.IsNaN(marker)) return start;
            return Math.Max(start, Math.Min(content - start, marker));
        }

        internal static float FillWidth(float marker, float content) => Math.Max(0f, RailEnd(marker, content) - RailInset);

        // On a tier the marker sits on that column's centre; between two tiers it moves in proportion to the
        // levels between them; below the first tier it runs from 0; past the last it continues at the last
        // step and stops at the content end.
        internal static float MarkerX(IReadOnlyList<int> tiers, int level)
        {
            int count = tiers.Count;
            if (count == 0 || level <= 0) return 0f;
            if (level < tiers[0]) return TierCentre(0) * level / tiers[0];
            for (int i = 0; i + 1 < count; i++)
            {
                if (level < tiers[i + 1])
                    return TierCentre(i) + (float)(level - tiers[i]) / (tiers[i + 1] - tiers[i]) * CharacterPerkItemVM.ColumnWidth;
            }
            int step = count > 1 ? tiers[count - 1] - tiers[count - 2] : tiers[0];
            float past = TierCentre(count - 1) + (float)(level - tiers[count - 1]) / Math.Max(1, step) * CharacterPerkItemVM.ColumnWidth;
            return Math.Min(past, CharacterScreenVM.PerkContentWidth(count));
        }

        internal const float MarkerDiamondSize = 12f;
        internal const float MarkerLabelGap = 4f;
        internal const float MarkerLabelWidth = 40f;

        internal readonly struct MarkerVisual
        {
            internal MarkerVisual(float diamondLeft, float labelLeft, bool labelFlipped)
            {
                DiamondLeft = diamondLeft;
                LabelLeft = labelLeft;
                LabelFlipped = labelFlipped;
            }

            internal float DiamondLeft { get; }
            internal float LabelLeft { get; }
            internal bool LabelFlipped { get; }
        }

        // Where the marker's diamond and number are drawn. The diamond centre is the fill's end (RailEnd), so
        // the drawn marker never leaves the rail; the number moves to the diamond's left when the right has
        // no room inside the content, which the view clips.
        internal static MarkerVisual Visual(float marker, float content)
        {
            float half = MarkerDiamondSize / 2f;
            float centre = RailEnd(marker, content);
            float right = centre + half + MarkerLabelGap;
            bool flipped = right + MarkerLabelWidth > content;
            float label = flipped ? centre - half - MarkerLabelGap - MarkerLabelWidth : right;
            return new MarkerVisual(centre - half, label, flipped);
        }

        internal static bool IsReached(int tier, int level) => level >= tier;

        // The scroll position, as a fraction of the scrollable range, that centres the marker in the view.
        internal static float ScrollFraction(float marker, float content, float view)
        {
            float range = content - view;
            if (!(range > 0f) || float.IsNaN(marker)) return 0f;
            float offset = Math.Max(0f, Math.Min(range, marker - view / 2f));
            return offset / range;
        }
    }
}
