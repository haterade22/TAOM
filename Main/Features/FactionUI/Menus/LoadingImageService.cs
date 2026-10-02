using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Features.FactionUI.Menus;

/// <summary>
/// Kysaro's loading screens in place of the image vanilla picks for each loading screen (#704), never
/// the same one twice in a row. Ported from his <c>LoadingScreen_ImageName_Patch</c>; unlike his, only
/// the picture last shown stays loaded (<see cref="FrontEndSpriteService.ShowLoadingImage"/>), and none
/// once the option is off.
/// </summary>
public sealed class LoadingImageService
{
    private readonly FrontEndSpriteService _sprites;
    private readonly IFrontEndResourceAdapter _adapter;
    private readonly FactionUIPaths _paths;
    private readonly FactionUISettingsProvider _settings;
    private readonly Random _random;

    private IReadOnlyList<string>? _files;
    private int _lastIndex = -1;

    public LoadingImageService(
        FrontEndSpriteService sprites,
        IFrontEndResourceAdapter adapter,
        FactionUIPaths paths,
        FactionUISettingsProvider settings,
        Random random)
    {
        _sprites = sprites;
        _adapter = adapter;
        _paths = paths;
        _settings = settings;
        _random = random;
    }

    /// <summary>The sprite name to show instead of <paramref name="requested"/>, or null to keep it.</summary>
    public string? ReplaceImageName(string? requested)
    {
        if (string.IsNullOrEmpty(requested)
            || requested!.StartsWith(FrontEndSpriteService.LoadingSpritePrefix, StringComparison.Ordinal))
        {
            return null;
        }
        if (!_settings.Current.CustomLoadingImages)
        {
            _sprites.ReleaseLoadingImage();
            return null;
        }

        var files = _files ??= _adapter.ListFiles(_paths.LoadingScreens, ".png");
        if (files.Count == 0)
            return null;

        var index = files.Count == 1 ? 0 : _random.Next(files.Count);
        if (files.Count > 1 && index == _lastIndex)
            index = (index + 1) % files.Count;

        var spriteName = _sprites.ShowLoadingImage(files[index]);
        if (spriteName != null)
            _lastIndex = index;
        return spriteName;
    }
}
