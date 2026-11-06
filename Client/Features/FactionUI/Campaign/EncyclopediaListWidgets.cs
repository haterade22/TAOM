extern alias SNV;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;
using Vector2 = SNV::System.Numerics.Vector2;

namespace TAOM.Features.FactionUI.Campaign
{
    public class EncyclopediaListScreenWidget : Widget
    {
        // Model is set by CampaignPresentation.OnLoaded; the two panels by Gauntlet from the prefab's widget references.
        public EncyclopediaListScreenVM? Model { get; set; }
        public ListPanel? EntryList { get; set; }
        public ScrollablePanel? EntryScroll { get; set; }
        private int _selectionVersion = -1;

        public EncyclopediaListScreenWidget(UIContext context) : base(context) { }
        protected override void OnUpdate(float dt) { base.OnUpdate(dt); Model?.Synchronize(); }
        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            if (Model == null || EntryList == null || EntryScroll == null || EntryScroll.Size.Y == 0 || _selectionVersion == Model.SelectionVersion) return;
            var selected = EntryList.Children.OfType<EncyclopediaListEntryButtonWidget>().FirstOrDefault(x => x.EntryId == Model.SelectedId && x.IsVisible);
            if (selected == null) return;
            _selectionVersion = Model.SelectionVersion;
            EntryScroll.ScrollToChild(selected);
        }
        protected override void OnDisconnectedFromRoot() { Model?.DisposePresentation(); base.OnDisconnectedFromRoot(); }
    }

    public class EncyclopediaListEntryButtonWidget : ButtonWidget
    {
        public string? EntryId { get; set; }
        public EncyclopediaListEntryButtonWidget(UIContext context) : base(context) { }
    }

    public class EncyclopediaCollapseIndicatorWidget : ImageWidget
    {
        public bool IsExpanded { get; set; }
        private bool? _last;
        public EncyclopediaCollapseIndicatorWidget(UIContext context) : base(context) { }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (_last == IsExpanded) return;
            _last = IsExpanded;
            Sprite = Context.SpriteData.GetSprite(IsExpanded ? "ec_list_collapse" : "ec_list_expand");
        }
    }

    public class EncyclopediaPictureWidget : ImageWidget
    {
        public string? PictureFile { get; set; }
        public string? PictureSprite { get; set; }
        /// <summary>Masks the picture, file or fallback sprite, with the hero portrait outline (list preview only).</summary>
        public bool PortraitFrame { get; set; }
        /// <summary>Per-sprite crops for fallback art with a different baked border, "prefix:zoom:focus;...".</summary>
        public string? SpriteFrameCrops { get; set; }
        /// <summary>Uniform enlargement past cover that pushes the picture's baked border outside the mask.</summary>
        public float FrameZoom { get; set; } = 1f;
        /// <summary>Share of the vertical excess cropped from the top: 0 keeps the top, 1 the bottom.</summary>
        public float FrameFocusY { get; set; } = 0.5f;
        /// <summary>Any sprite used as the picture's mask instead of the portrait outline (a kingdom fief arch).</summary>
        public string? FrameMask { get; set; }
        /// <summary>The rim drawn over a <see cref="FrameMask"/> picture; none when empty.</summary>
        public string? FrameRim { get; set; }
        private string? _file, _sprite;
        private readonly Brush _frameMaterial = new Brush();
        private readonly BrushRenderer _frameRenderer = new BrushRenderer();
        private ulong _frameRenderFrame;
        private readonly Brush _rimMaterial = new Brush();
        private readonly BrushRenderer _rimRenderer = new BrushRenderer();

        public EncyclopediaPictureWidget(UIContext context) : base(context)
        {
            _rimRenderer.Brush = _rimMaterial;
            _rimRenderer.CurrentState = "Default";
            _frameRenderer.Brush = _frameMaterial;
            _frameRenderer.CurrentState = "Default";
            _frameMaterial.DefaultLayer.OverlayMethod = BrushOverlayMethod.CoverWithTexture;
            _frameMaterial.DefaultLayer.UseOverlayAlphaAsMask = true;
        }

        internal static (float Width, float Height, float OffsetX, float OffsetY) FramePicture(
            float sourceWidth, float sourceHeight, float width, float height, float zoom, float focusY)
        {
            float scale = Math.Max(width / sourceWidth, height / sourceHeight) * (zoom > 1f ? zoom : 1f);
            float focus = focusY >= 0f && focusY <= 1f ? focusY : 0.5f;
            float imageWidth = sourceWidth * scale, imageHeight = sourceHeight * scale;
            return (imageWidth, imageHeight, (imageWidth - width) * 0.5f, (imageHeight - height) * focus);
        }

        /// <summary>
        /// Picks the crop for a fallback sprite: the first "prefix:zoom:focus" rule whose prefix starts
        /// the sprite name, else the default crop used for settlement picture files.
        /// </summary>
        internal static (float Zoom, float FocusY) ResolveFrameCrop(string? spriteName, string? crops,
            float zoom, float focusY)
        {
            if (spriteName is null || spriteName.Length == 0 || crops is null || crops.Length == 0)
                return (zoom, focusY);
            foreach (string rule in crops.Split(';'))
            {
                string[] parts = rule.Split(':');
                if (parts.Length == 3 && parts[0].Length > 0
                    && spriteName.StartsWith(parts[0], StringComparison.Ordinal)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ruleZoom)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ruleFocus))
                    return (ruleZoom, ruleFocus);
            }
            return (zoom, focusY);
        }

        /// <summary>
        /// The mask and rim sprite names: a FrameMask with its own FrameRim wins, else PortraitFrame
        /// keeps the portrait outline and gold panel rim, else the picture draws unmasked.
        /// </summary>
        internal static (string? Mask, string? Rim) ResolveFrameSprites(bool portraitFrame, string? mask, string? rim)
        {
            if (!string.IsNullOrEmpty(mask)) return (mask, string.IsNullOrEmpty(rim) ? null : rim);
            return portraitFrame ? ("ec_portrait_mask", "ec_portrait_panel") : (null, null);
        }

        protected override void OnRender(TwoDimensionContext context, TwoDimensionDrawContext drawContext)
        {
            var sprites = ResolveFrameSprites(PortraitFrame, FrameMask, FrameRim);
            var picture = Sprite;
            var mask = sprites.Mask != null && picture != null ? Context.SpriteData.GetSprite(sprites.Mask) : null;
            if (mask == null || picture == null || Size.X <= 0 || Size.Y <= 0)
            {
                base.OnRender(context, drawContext);
                return;
            }

            float width = Size.X * _inverseScaleToUse;
            float height = Size.Y * _inverseScaleToUse;
            var crop = ResolveFrameCrop(string.IsNullOrEmpty(_file) ? _sprite : null, SpriteFrameCrops,
                FrameZoom, FrameFocusY);
            var frame = FramePicture(picture.Width, picture.Height, width, height, crop.Zoom, crop.FocusY);
            _frameMaterial.DefaultLayer.Sprite = mask;
            _frameMaterial.DefaultLayer.OverlaySprite = picture;
            _frameRenderer.Update(++_frameRenderFrame, 0f, 0f);
            _frameRenderer.Render(drawContext, in AreaRect, _scaleToUse, Context.ContextAlpha,
                new Vector2(frame.OffsetX, frame.OffsetY), new Vector2(frame.Width, frame.Height));

            // The frame's rim (for a portrait, the hero portrait panel's gold rim) over the masked picture.
            _rimMaterial.DefaultLayer.Sprite = sprites.Rim == null ? null : Context.SpriteData.GetSprite(sprites.Rim);
            if (_rimMaterial.DefaultLayer.Sprite == null)
                return;
            _rimRenderer.Update(_frameRenderFrame, 0f, 0f);
            _rimRenderer.Render(drawContext, in AreaRect, _scaleToUse, Context.ContextAlpha,
                Vector2.Zero, Vector2.Zero);
        }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (_file == PictureFile && _sprite == PictureSprite) return;
            ClearPicture();
            _file = PictureFile;
            _sprite = PictureSprite;
            var file = PictureFile;
            if (file != null && file.Length > 0)
            {
                try { Sprite = EncyclopediaPictureCache.Acquire(file); }
                catch (Exception e) { CampaignPresentation.LogError("Could not load encyclopedia picture " + file, e); }
            }
            else Sprite = string.IsNullOrEmpty(PictureSprite) ? null : Context.SpriteData.GetSprite(PictureSprite);
        }
        private void ClearPicture()
        {
            Sprite = null;
            if (_file != null && _file.Length > 0) EncyclopediaPictureCache.Release(_file);
            _file = null;
            _sprite = null;
        }
        protected override void OnDisconnectedFromRoot() { ClearPicture(); base.OnDisconnectedFromRoot(); }
    }
}
