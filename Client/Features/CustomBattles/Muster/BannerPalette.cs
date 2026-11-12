using System;
using System.Linq;
using TaleWorlds.Core;

namespace TAOM.Client.Features.CustomBattles.Muster
{
    /// <summary>
    /// The game's banner colour palette (BannerManager): the only colours a banner takes. The colour chooser offers these;
    /// any other colour is snapped to the nearest (squared RGB distance).
    /// </summary>
    internal static class BannerPalette
    {
        public static uint? Nearest(uint? argb)
        {
            if (argb == null || BannerManager.Instance?.ReadOnlyColorPalette == null) return argb;
            if (BannerManager.GetColorId(argb.Value) >= 0) return argb;
            int r = (int)(argb.Value >> 16 & 0xFF), g = (int)(argb.Value >> 8 & 0xFF), b = (int)(argb.Value & 0xFF);
            return BannerManager.Instance.ReadOnlyColorPalette.Values.Select(c => c.Color)
                .OrderBy(c => Sq((int)(c >> 16 & 0xFF) - r) + Sq((int)(c >> 8 & 0xFF) - g) + Sq((int)(c & 0xFF) - b))
                .Cast<uint?>().FirstOrDefault() ?? argb;
        }

        /// <summary>Every palette colour, ordered by hue (greys first, by brightness) for the colour chooser.</summary>
        public static System.Collections.Generic.List<uint> All()
        {
            var pal = BannerManager.Instance?.ReadOnlyColorPalette;
            if (pal == null) return new System.Collections.Generic.List<uint>();
            return pal.Values.Select(c => c.Color).Distinct().OrderBy(c => Key(c)).ToList();
        }

        private static double Key(uint c)
        {
            double r = (c >> 16 & 0xFF) / 255.0, g = (c >> 8 & 0xFF) / 255.0, b = (c & 0xFF) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            double light = (max + min) / 2;
            if (d < 0.12) return light;                                 // greys first, dark to light (0..1)
            double h = max == r ? ((g - b) / d + 6) % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            int band = (int)Math.Floor(h * 2);                          // 12 colour families round the wheel
            return 2 + band + light * 0.99;                             // each family dark to light
        }

        private static int Sq(int v) => v * v;
    }
}
