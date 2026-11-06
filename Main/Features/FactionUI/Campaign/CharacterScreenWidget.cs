using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    public class CharacterScreenWidget : Widget
    {
        public CharacterScreenVM Model { get; set; }
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
        public Widget FillWidget { get; set; }
        public Widget ContainerWidget { get; set; }
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
        public Widget IconWidget { get; set; }
        public CharacterPerkButtonWidget(UIContext context) : base(context) { }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            IsSelected = PerkState == 2;
            IconWidget.AlphaFactor = PerkState == 0 || PerkState == 3 || PerkState == 4 ? 0.4f : 1f;
        }
    }

    public class CharacterPerkScrollButtonWidget : ButtonWidget
    {
        public ScrollbarWidget Scrollbar { get; set; }
        public float Direction { get; set; }
        public CharacterPerkScrollButtonWidget(UIContext context) : base(context)
        {
            ClickEventHandlers.Add(widget => Scrollbar.ValueFloat = Math.Max(Scrollbar.MinValue, Math.Min(Scrollbar.MaxValue, Scrollbar.ValueFloat + Direction * 40f)));
        }
    }

    public class CharacterPerkScrollbarWidget : ScrollbarWidget
    {
        public CharacterPerkScrollbarWidget(UIContext context) : base(context) { }
        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            Handle.SuggestedWidth = 20f;
            Handle.SuggestedHeight = 18f;
        }
    }
}

