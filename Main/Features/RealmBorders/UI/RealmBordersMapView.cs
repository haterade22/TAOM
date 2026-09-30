using System;
using System.Collections.Generic;
using SandBox.View.Map;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Features.RealmBorders.UI;

/// <summary>
/// The realm borders' map-screen entry point, attached by <c>RealmBordersCampaignBehavior</c> (the
/// parameterless ctor is AddMapView's new() constraint, so services come from IoC, the sanctioned
/// engine-instantiated boundary). Every map frame: the two keys, the border service with the real
/// camera distance, and the realm names projected onto the screen. The names layer is display-only
/// (every widget ignores events, no input restrictions), so it never takes a click from the map.
/// </summary>
public class RealmBordersMapView : MapView
{
    private const int NamesLayerOrder = 40;

    private readonly Dictionary<string, float> _labelHeights = new Dictionary<string, float>();
    private RealmBorderService? _borders;
    private IRealmMapAdapter? _map;
    private IMapTerrainAdapter? _terrain;
    private RealmBordersKeys? _keys;
    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private RealmNamesVM? _names;
    private IReadOnlyList<RealmLabel>? _heightsFor;
    private Func<string, string>? _nameOf;
    private Func<RealmLabel, (bool Visible, float X, float Y)>? _project;
    private Camera? _camera;
    private float _width, _height;

    protected override void CreateLayout()
    {
        base.CreateLayout();
        _borders = IoC.Resolve<RealmBorderService>();
        _map = IoC.Resolve<IRealmMapAdapter>();
        _terrain = IoC.Resolve<IMapTerrainAdapter>();
        _keys = new RealmBordersKeys();
        _nameOf = _map.RealmName;
        _project = label => Project(_camera!, label, _width, _height);
        _names = new RealmNamesVM();
        _layer = new GauntletLayer("TaomRealmNames", NamesLayerOrder);
        _movie = _layer.LoadMovie("TaomRealmNames", _names);
        Layer = _layer;
        MapScreen.AddLayer(_layer);
    }

    protected override void OnMapScreenUpdate(float dt)
    {
        base.OnMapScreenUpdate(dt);
        if (_borders == null || _keys == null)
            return;

        if (_keys.TogglePressed)
            _borders.ToggleVisible();
        if (_keys.CycleModePressed)
            _borders.CycleMode();

        var cameraView = MapScreen?.MapCameraView;
        _borders.OnMapFrame(cameraView?.CameraDistance ?? float.NaN);
        PlaceNames(cameraView?.Camera);
    }

    protected override void OnFinalize()
    {
        if (_layer != null)
        {
            if (_movie != null)
            {
                _layer.ReleaseMovie(_movie);
                _movie = null;
            }
            MapScreen.Instance?.RemoveLayer(_layer);
            _layer = null;
        }
        _names?.OnFinalize();
        _names = null;
        _borders?.OnMapScreenClosed();
        base.OnFinalize();
    }

    private void PlaceNames(Camera? camera)
    {
        if (_names == null || _borders == null || _nameOf == null || _project == null)
            return;
        var labels = _borders.Labels;
        _names.SetLabels(labels, _nameOf);
        if (camera == null)
        {
            _names.Place(0f, 1f, 1f, _project);
            return;
        }
        RefreshHeights(labels);
        // WorldToScreenInsideUsableArea answers inside the usable area, and the Gauntlet root spans it,
        // so the labels are centred on the usable area rather than the whole screen.
        _camera = camera;
        _width = Screen.RealScreenResolutionWidth * ScreenManager.UsableArea.x;
        _height = Screen.RealScreenResolutionHeight * ScreenManager.UsableArea.y;
        _names.Place(_borders.Alpha, _width, _height, _project);
    }

    private (bool Visible, float X, float Y) Project(Camera camera, RealmLabel label, float width, float height)
    {
        _labelHeights.TryGetValue(label.Realm, out float z);
        float x = 0f, y = 0f, w = 0f;
        MBWindowManager.WorldToScreenInsideUsableArea(camera, new Vec3(label.Position.X, label.Position.Y, z + 2f), ref x, ref y, ref w);
        // w < 0 is behind the camera (the order-of-battle markers' test); keep a margin off-screen.
        bool visible = w > 0f && x > -0.2f * width && x < 1.2f * width && y > -0.2f * height && y < 1.2f * height;
        return (visible, x, y);
    }

    private void RefreshHeights(IReadOnlyList<RealmLabel> labels)
    {
        if (ReferenceEquals(labels, _heightsFor) || _terrain == null)
            return;
        _heightsFor = labels;
        _labelHeights.Clear();
        foreach (var label in labels)
            _labelHeights[label.Realm] = _terrain.HeightAt(label.Position.X, label.Position.Y, 0f);
    }
}
