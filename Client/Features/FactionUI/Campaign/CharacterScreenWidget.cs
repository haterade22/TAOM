using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.Campaign
{
    public class CharacterScreenWidget : Widget
    {
        // Set by CampaignPresentation.OnLoaded after the movie is built; null until then.
        public CharacterScreenVM? Model { get; set; }
        public CharacterScreenWidget(UIContext context) : base(context) { }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            Model?.Synchronize();
        }
    }

    public class CharacterFocusWidget : Widget
    {
        public int Focus { get; set; }
        public bool UseSlotSprites { get; set; }
        public CharacterFocusWidget(UIContext context) : base(context) { }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            int index = 0;
            foreach (Widget child in Children)
            {
                bool filled = index++ < Focus;
                if (UseSlotSprites)
                {
                    child.Sprite = Context.SpriteData.GetSprite(filled ? "cs_focus_full" : "cs_focus_empty");
                    child.AlphaFactor = 1f;
                }
                else child.AlphaFactor = filled ? 1f : 0.2f;
            }
        }
    }

    public class CharacterProgressWidget : Widget
    {
        public double InitialAmount { get; set; }
        public double MaxAmount { get; set; }
        // Set by Gauntlet from the prefab's widget references, before the first update.
        public Widget FillWidget { get; set; } = null!;
        public Widget? ContainerWidget { get; set; }
        public float FillInset { get; set; }
        public CharacterProgressWidget(UIContext context) : base(context) { }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            FillWidget.SuggestedWidth = (SuggestedWidth - FillInset * 2f) * (float)Math.Max(0.0, Math.Min(1.0, MaxAmount > 0.0 ? InitialAmount / MaxAmount : 0.0));
        }
    }

    public class CharacterPerkButtonWidget : ButtonWidget
    {
        public int PerkState { get; set; }
        // Set by Gauntlet from the prefab's widget references (the medallion rim, the native perk icon, the
        // name, the chosen tick and the available diamond), before the first update.
        public Widget? IconWidget { get; set; }
        public Widget? RimWidget { get; set; }
        public Widget? NameWidget { get; set; }
        public Widget? SelectedWidget { get; set; }
        public Widget? AvailableWidget { get; set; }
        public CharacterPerkButtonWidget(UIContext context) : base(context) { }

        internal static readonly Color Gold = Color.ConvertStringToColor("#CFAE6BFF");
        internal static readonly Color Ivory = Color.ConvertStringToColor("#E8DCC0FF");
        internal static readonly Color Dim = Color.ConvertStringToColor("#A39882FF");
        internal static readonly Color Muted = Color.ConvertStringToColor("#7F766AFF");

        internal readonly struct PerkLook
        {
            public PerkLook(bool chosen, bool available, float rimAlpha, Color iconColor, float iconAlpha, float nameAlpha, float rimBrightness = 1f)
            {
                Chosen = chosen;
                Available = available;
                RimAlpha = rimAlpha;
                IconColor = iconColor;
                IconAlpha = iconAlpha;
                NameAlpha = nameAlpha;
                RimBrightness = rimBrightness;
            }

            public bool Chosen { get; }
            public bool Available { get; }
            public float RimAlpha { get; }
            // The rim's ColorFactor before hover: an open tier's rims read bright ivory.
            public float RimBrightness { get; }
            public Color IconColor { get; }
            public float IconAlpha { get; }
            public float NameAlpha { get; }
        }

        // PerkVM.PerkStates in the installed 1.5.7 DLL: 0 NotEarned, 1 EarnedButNotSelected,
        // 2 EarnedAndActive, 3 EarnedAndNotActive, 4 EarnedPreviousPerkNotSelected. Only the native state
        // picks the look. Chosen (2): gold rim and icon and the tick. Available (1): an ivory rim and the
        // small diamond. The pair's rejected perk (3) and a perk behind an unchosen tier (4) are dimmed; not
        // earned (0) is the faintest. Both perks of an open tier (1) get a brighter rim, and no tick until one is
        // chosen. Hover lifts only the rim's brightness.
        internal const float OpenRimBrightness = 1.35f;
        internal static PerkLook Look(int state)
        {
            switch (state)
            {
                case 2: return new PerkLook(true, false, 1f, Gold, 1f, 1f);
                case 1: return new PerkLook(false, true, 1f, Ivory, 1f, 1f, OpenRimBrightness);
                case 3:
                case 4: return new PerkLook(false, false, 0.55f, Dim, 0.8f, 0.67f);
                default: return new PerkLook(false, false, 0.45f, Muted, 0.6f, 0.47f);
            }
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            PerkLook look = Look(PerkState);
            IsSelected = look.Chosen;
            if (RimWidget != null)
            {
                RimWidget.AlphaFactor = look.RimAlpha;
                // The gold rim sprite reads ivory when its saturation drops and its value lifts.
                RimWidget.SaturationFactor = look.Available ? -90f : 0f;
                RimWidget.ValueFactor = look.Available ? 40f : 0f;
                RimWidget.ColorFactor = look.RimBrightness * (IsHovered ? 1.25f : 1f);
            }
            if (IconWidget != null)
            {
                IconWidget.Color = look.IconColor;
                IconWidget.AlphaFactor = look.IconAlpha;
            }
            if (NameWidget != null) NameWidget.AlphaFactor = look.NameAlpha;
            if (SelectedWidget != null) SelectedWidget.IsVisible = look.Chosen;
            if (AvailableWidget != null) AvailableWidget.IsVisible = look.Available;
        }
    }

    public class CharacterPerkScrollButtonWidget : ButtonWidget
    {
        // Set by Gauntlet from the prefab's widget reference, before the first click.
        public ScrollbarWidget Scrollbar { get; set; } = null!;
        public float Direction { get; set; }
        public CharacterPerkScrollButtonWidget(UIContext context) : base(context)
        {
            ClickEventHandlers.Add(widget => Scrollbar.ValueFloat = Math.Max(Scrollbar.MinValue, Math.Min(Scrollbar.MaxValue, Scrollbar.ValueFloat + Direction * 40f)));
        }
    }

    public class CharacterPerkScrollbarWidget : ScrollbarWidget
    {
        // Bound from the VM: a new request (a skill or member switch) scrolls once to the fraction of the
        // bar's own range; every other frame leaves the value alone, so a manual scroll is kept.
        public float ScrollFraction { get; set; }
        public int ScrollRequest { get; set; }
        private int _appliedRequest;
        public CharacterPerkScrollbarWidget(UIContext context) : base(context) { }

        // Null when the request was applied already, or while the panel cannot scroll yet (the request waits).
        internal static float? RequestedValue(int request, int applied, float min, float max, float fraction)
        {
            if (request == applied || !(max > min)) return null;
            float f = float.IsNaN(fraction) ? 0f : Math.Max(0f, Math.Min(1f, fraction));
            return min + f * (max - min);
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            Handle.SuggestedWidth = 20f;
            Handle.SuggestedHeight = 18f;
            float? value = RequestedValue(ScrollRequest, _appliedRequest, MinValue, MaxValue, ScrollFraction);
            if (value.HasValue)
            {
                ValueFloat = value.Value;
                _appliedRequest = ScrollRequest;
            }
        }
    }
}

