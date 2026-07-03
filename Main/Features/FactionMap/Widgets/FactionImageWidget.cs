using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

using EngineTexture = TaleWorlds.Engine.Texture;
using TwoDimTexture = TaleWorlds.TwoDimension.Texture;

namespace TAOM.Features.FactionMap.Widgets;

public class FactionImageWidget : ImageWidget
{
    private string _imageId = "";
    private string _loadedImageId = "";
    private bool _textureLoaded;
    private bool _loadFailed;

    public FactionImageWidget(UIContext context) : base(context)
    {
        OverrideDefaultStateSwitchingEnabled = true;
    }

    [Editor(false)]
    public string ImageId
    {
        get => _imageId;
        set
        {
            if (_imageId != value)
            {
                _imageId = value;
                if (_loadedImageId != value)
                {
                    _textureLoaded = false;
                    _loadFailed = false;
                    _loadedImageId = "";
                }
                OnPropertyChanged(value, nameof(ImageId));
            }
        }
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);
        TryLoadTexture();
    }

    private void TryLoadTexture()
    {
        if (_textureLoaded || _loadFailed || string.IsNullOrEmpty(_imageId))
            return;

        try
        {
            string modPath = FactionMapPaths.ModulePath;
            if (string.IsNullOrEmpty(modPath)) { _loadFailed = true; return; }

            string file = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap", $"{_imageId}.png");
            if (!System.IO.File.Exists(file))
            {
                _loadFailed = true;
                FactionMapPaths.LogError($"[FactionImage] File not found: {file}");
                return;
            }

            string fileName = System.IO.Path.GetFileName(file);
            string folder = System.IO.Path.GetDirectoryName(file);

            EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
            if (engineTex == null)
            {
                _loadFailed = true;
                FactionMapPaths.LogError($"[FactionImage] LoadTextureFromPath returned null for {_imageId}");
                return;
            }

            Sprite = RuntimeSpriteFactory.FromEngineTexture(engineTex);
            _textureLoaded = true;
            _loadedImageId = _imageId;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            FactionMapPaths.LogError($"[FactionImage] Error: {ex.Message}");
        }
    }
}
