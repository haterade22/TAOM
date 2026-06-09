using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Linq;
using Assimp;
using Microsoft.Win32;

namespace BannerlordCraftingTool;

public partial class MainWindow : Window
{
    // ── Data ─────────────────────────────────────────────────────────────────

    private List<CraftingPieceData> _allPieces = new();
    private Dictionary<string, HashSet<string>> _templatePieceIds = new();

    private CraftingPieceData? _blade, _guard, _handle, _pommel;
    private CraftingPieceData? _active;

    private readonly Dictionary<string, string> _searchText = new()
    {
        ["Blade"] = "", ["Guard"] = "", ["Handle"] = "", ["Pommel"] = ""
    };

    private bool _suppressUpdate;
    private float _stepSize = 1f;
    private float _pixelsPerUnit = 3f;

    private string _fbxFolder = "";
    private readonly Dictionary<string, MeshGeometry3D?> _meshCache = new();
    private readonly Dictionary<string, string> _meshIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GeometryModel3D, CraftingPieceData> _model3DMap = new();
    private System.Windows.Point _mouseDownPos;

    // piece type → (WPF color, 2D rect width)
    private static readonly Dictionary<string, (System.Windows.Media.Color col, double w)> PieceStyle = new()
    {
        ["Blade"]  = (System.Windows.Media.Color.FromRgb(0x3A, 0x7A, 0xB8), 36),
        ["Guard"]  = (System.Windows.Media.Color.FromRgb(0xB8, 0x40, 0x40), 110),
        ["Handle"] = (System.Windows.Media.Color.FromRgb(0x38, 0xA8, 0x50), 26),
        ["Pommel"] = (System.Windows.Media.Color.FromRgb(0xB8, 0x88, 0x30), 50),
    };

    public MainWindow() => InitializeComponent();

    // ── File loading ──────────────────────────────────────────────────────────

    private void BrowsePieces_Click(object sender, RoutedEventArgs e)
    {
        if (PickXml() is string p) PiecesPathBox.Text = p;
    }

    private void BrowseTemplates_Click(object sender, RoutedEventArgs e)
    {
        if (PickXml("XSLT/XML|*.xslt;*.xml|All|*.*") is string p) TemplatesPathBox.Text = p;
    }

    private void LoadPieces_Click(object sender, RoutedEventArgs e)
    {
        _allPieces.Clear();
        _meshCache.Clear();
        LoadPiecesFromFile(PiecesPathBox.Text.Trim(), replace: false);
    }

    private void AddPieces_Click(object sender, RoutedEventArgs e)
    {
        if (PickXml() is string p)
        {
            PiecesPathBox.Text = p;
            LoadPiecesFromFile(p, replace: false);
        }
    }

    private void LoadPiecesFromFile(string path, bool replace)
    {
        if (!File.Exists(path)) { Status("File not found: " + path); return; }
        try
        {
            var doc = XDocument.Load(path);
            var loaded = doc.Descendants("CraftingPiece")
                .Select(ParsePiece).OfType<CraftingPieceData>().ToList();

            if (replace)
            {
                // Replace pieces with same ID, add new ones
                var byId = _allPieces.ToDictionary(p => p.Id);
                foreach (var p in loaded) byId[p.Id] = p;
                _allPieces = byId.Values.ToList();
            }
            else
            {
                // Merge: existing IDs win
                var existingIds = _allPieces.Select(p => p.Id).ToHashSet();
                _allPieces.AddRange(loaded.Where(p => !existingIds.Contains(p.Id)));
            }

            Status($"{_allPieces.Count} pieces loaded total.");
            RefreshDropdowns();
        }
        catch (Exception ex) { Status($"Error: {ex.Message}"); }
    }

    private void LoadTemplates_Click(object sender, RoutedEventArgs e)
    {
        var path = TemplatesPathBox.Text.Trim();
        if (!File.Exists(path)) { Status("File not found."); return; }
        try
        {
            var doc = XDocument.Load(path);
            XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
            _templatePieceIds.Clear();

            foreach (var tmpl in doc.Descendants(xsl + "template"))
            {
                var match = tmpl.Attribute("match")?.Value ?? "";
                var m = Regex.Match(match, @"CraftingTemplate\[@id='(\w+)'\]");
                if (!m.Success) continue;
                var id = m.Groups[1].Value;

                var ids = tmpl.Descendants()
                    .Where(el => el.Name.LocalName is "UsablePiece" or "AvailablePiece")
                    .Select(u => u.Attribute("piece_id")?.Value ?? u.Attribute("id")?.Value)
                    .OfType<string>().ToHashSet();

                if (!_templatePieceIds.TryGetValue(id, out var existing))
                    _templatePieceIds[id] = ids;
                else
                    foreach (var pid in ids) existing.Add(pid);
            }

            // If this file is a base crafting_templates.xml (not the xslt), also pull real
            // build orders from <CraftingTemplate><PieceDatas> so assembly matches that template
            // exactly. (The xslt has no <CraftingTemplate> elements, so this is a no-op for it —
            // build orders then come from the bundled vanilla table.)
            foreach (var ct in doc.Descendants("CraftingTemplate"))
            {
                var tid = ct.Attribute("id")?.Value;
                var pds = ct.Element("PieceDatas");
                if (tid == null || pds == null) continue;
                var order = pds.Elements("PieceData")
                    .Select(pd => (type: pd.Attribute("piece_type")?.Value ?? "",
                                   ord:  int.TryParse(pd.Attribute("build_order")?.Value, out var o) ? o : 0))
                    .Where(x => x.type.Length > 0).ToArray();
                if (order.Length > 0) _loadedBuildOrders[tid] = order;
            }

            var boNote = _loadedBuildOrders.Count > 0 ? $" (+{_loadedBuildOrders.Count} build orders)" : "";
            Status($"{_templatePieceIds.Count} templates loaded{boNote}.");
            RefreshTemplateCombo();
        }
        catch (Exception ex) { Status($"Error: {ex.Message}"); }
    }

    // ── XML parsing ───────────────────────────────────────────────────────────

    private static CraftingPieceData? ParsePiece(XElement el)
    {
        var id      = el.Attribute("id")?.Value;
        var typeStr = el.Attribute("piece_type")?.Value;
        if (id == null || typeStr == null) return null;

        // Engine rule (CraftingPiece.Deserialize): 'length' OR the two 'distance_to_*' halves —
        // mutually exclusive. 'length' splits 50/50; otherwise Length = next + prev (asymmetric ok).
        float len, dn, dp;
        if (TryFloat(el.Attribute("length")?.Value, out len))
        {
            dn = dp = len / 2f;
        }
        else
        {
            TryFloat(el.Attribute("distance_to_next_piece")?.Value, out dn);
            TryFloat(el.Attribute("distance_to_previous_piece")?.Value, out dp);
            len = dn + dp;   // 0 if neither present — piece still loads (no extent), unlike before.
        }

        var bd = el.Element("BuildData");

        float po   = ParseBd(bd, "piece_offset");
        float prev = ParseBd(bd, "previous_piece_offset");
        float next = ParseBd(bd, "next_piece_offset");

        return new CraftingPieceData
        {
            Id                      = id,
            PieceType               = typeStr,
            Length                  = len,
            DistanceToNextPiece     = dn,
            DistanceToPreviousPiece = dp,
            MeshName                = el.Attribute("mesh")?.Value ?? id,
            PieceOffset             = po,
            PreviousPieceOffset     = prev,
            NextPieceOffset         = next,
            OrigPieceOffset         = po,
            OrigPreviousPieceOffset = prev,
            OrigNextPieceOffset     = next,
        };
    }

    private static float ParseBd(XElement? bd, string attr)
    {
        TryFloat(bd?.Attribute(attr)?.Value, out float v); return v;
    }

    private static bool TryFloat(string? s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    // ── Dropdowns ─────────────────────────────────────────────────────────────

    private void RefreshTemplateCombo()
    {
        TemplateCombo.Items.Clear();
        TemplateCombo.Items.Add("(All pieces)");
        foreach (var id in _templatePieceIds.Keys.OrderBy(k => k))
            TemplateCombo.Items.Add(id);
        if (TemplateCombo.SelectedIndex < 0) TemplateCombo.SelectedIndex = 0;
    }

    private void RefreshDropdowns()
    {
        HashSet<string>? allowed = null;
        if (TemplateCombo.SelectedItem is string t && t != "(All pieces)"
            && _templatePieceIds.TryGetValue(t, out var ids))
            allowed = ids;

        IEnumerable<CraftingPieceData> Filter(string type) =>
            _allPieces
                .Where(p => p.PieceType == type && (allowed == null || allowed.Contains(p.Id)))
                .Where(p => string.IsNullOrEmpty(_searchText[type]) ||
                            p.Id.Contains(_searchText[type], StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Id);

        // Remember current selections so they survive the repopulate
        var prevBlade  = _blade?.Id;
        var prevGuard  = _guard?.Id;
        var prevHandle = _handle?.Id;
        var prevPommel = _pommel?.Id;
        var prevActive = _active?.Id;

        _suppressUpdate = true;
        PopulateCombo(BladeCombo,  Filter("Blade"));
        PopulateCombo(GuardCombo,  Filter("Guard"));
        PopulateCombo(HandleCombo, Filter("Handle"));
        PopulateCombo(PommelCombo, Filter("Pommel"));

        // Restore selections — if the piece is still in the filtered list, re-select it
        _blade  = RestoreComboSelection(BladeCombo,  prevBlade);
        _guard  = RestoreComboSelection(GuardCombo,  prevGuard);
        _handle = RestoreComboSelection(HandleCombo, prevHandle);
        _pommel = RestoreComboSelection(PommelCombo, prevPommel);
        _suppressUpdate = false;

        // Re-point _active at whichever slot it was in (may become null if filtered out)
        var newActive = (_blade?.Id  == prevActive ? _blade  : null)
                     ?? (_guard?.Id  == prevActive ? _guard  : null)
                     ?? (_handle?.Id == prevActive ? _handle : null)
                     ?? (_pommel?.Id == prevActive ? _pommel : null);
        SetActivePiece(newActive);

        RedrawCanvas();
        Refresh3D();
    }

    private static CraftingPieceData? RestoreComboSelection(ComboBox cb, string? prevId)
    {
        if (prevId == null) return null;
        foreach (var item in cb.Items.OfType<CraftingPieceData>())
        {
            if (item.Id == prevId)
            {
                cb.SelectedItem = item;
                return item;
            }
        }
        return null; // filtered out — combo stays on (None)
    }

    private static void PopulateCombo(ComboBox cb, IEnumerable<CraftingPieceData> pieces)
    {
        cb.Items.Clear();
        cb.Items.Add(new CraftingPieceData { Id = "(None)", PieceType = "" });
        foreach (var p in pieces) cb.Items.Add(p);
        cb.SelectedIndex = 0;
    }

    private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshDropdowns();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is string slot)
            _searchText[slot] = tb.Text;
        RefreshDropdowns();
    }

    private void PieceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUpdate) return;
        var cb   = (ComboBox)sender;
        var slot = cb.Tag as string ?? "";
        var piece = cb.SelectedItem as CraftingPieceData;
        var sel   = piece?.Id == "(None)" ? null : piece;

        switch (slot)
        {
            case "Blade":  _blade  = sel; break;
            case "Guard":  _guard  = sel; break;
            case "Handle": _handle = sel; break;
            case "Pommel": _pommel = sel; break;
        }

        // Bug fix: selecting a piece in a combo always makes it active
        if (sel != null)
            SetActivePiece(sel);
        else if (_active?.PieceType == slot)
            SetActivePiece(_guard ?? _blade ?? _handle ?? _pommel);

        RedrawCanvas();
        Refresh3D();
        UpdateXmlOutput();
    }

    // ── Active piece editor ───────────────────────────────────────────────────

    private void SetActivePiece(CraftingPieceData? piece)
    {
        _active = piece;
        _suppressUpdate = true;

        SelectedPieceLabel.Text = piece?.Id ?? "(click a piece to edit)";
        SelectedPieceLabel.Foreground = piece != null
            ? Brushes.White
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));

        PieceOffsetBox.Text = piece != null ? Fmt(piece.PieceOffset) : "";
        PrevOffsetBox.Text  = piece != null ? Fmt(piece.PreviousPieceOffset) : "";
        NextOffsetBox.Text  = piece != null ? Fmt(piece.NextPieceOffset) : "";
        ScaleBox.Text       = piece != null ? piece.Scale.ToString(CultureInfo.InvariantCulture) : "";
        OffsetEditor.IsEnabled = piece != null;

        _suppressUpdate = false;
    }

    private void OffsetBox_LostFocus(object sender, RoutedEventArgs e) => CommitBoxes();

    private void OffsetBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitBoxes();
    }

    private void CommitBoxes()
    {
        if (_suppressUpdate || _active == null) return;
        if (TryFloat(PieceOffsetBox.Text, out float po))   _active.PieceOffset = po;
        if (TryFloat(PrevOffsetBox.Text,  out float prev)) _active.PreviousPieceOffset = prev;
        if (TryFloat(NextOffsetBox.Text,  out float next)) _active.NextPieceOffset = next;
        if (int.TryParse(ScaleBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sc) && sc > 0)
            _active.Scale = sc;
        // Re-sync the boxes to the committed model values so rejected input (e.g. "50.5", "abc",
        // a non-positive scale) snaps back instead of lingering as stale text.
        SetActivePiece(_active);
        RedrawCanvas();
        Refresh3D();
        UpdateXmlOutput();
    }

    private void OffsetStep_Click(object sender, RoutedEventArgs e)
    {
        if (_active == null || sender is not Button btn) return;
        var parts = (btn.Tag as string ?? "").Split(',');
        if (parts.Length != 2 || !TryFloat(parts[1], out float dir)) return;
        float delta = dir * _stepSize;

        switch (parts[0])
        {
            case "piece_offset":          _active.PieceOffset += delta; break;
            case "previous_piece_offset": _active.PreviousPieceOffset += delta; break;
            case "next_piece_offset":     _active.NextPieceOffset += delta; break;
        }
        SetActivePiece(_active);
        RedrawCanvas();
        Refresh3D();
        UpdateXmlOutput();
    }

    private void StepRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && TryFloat(rb.Tag as string, out float s))
            _stepSize = s;
    }

    // ── Assembly algorithm ────────────────────────────────────────────────────
    // Faithful port of TaleWorlds.Core.WeaponDesign.CalculatePivotDistances() +
    // CalculateWeaponLength() (v1.4.5, verbatim). Pieces are placed in the template's
    // build order, branching on Sign(build_order): Handle(0) is the ANCHOR at the hand
    // grip (Z=0); top pieces (Guard/Blade) stack toward +Z (tip); bottom (Pommel) toward
    // -Z (butt). Spacing is dominated by the half-lengths (DistanceTo*Piece = Length/2);
    // the three offsets are SUBTRACTED nudges. Handle.piece_offset is a GLOBAL shift of
    // the whole weapon relative to the grip — it does not move the handle vs the blade.
    // Values are raw XML units (= cm); the engine works in metres (x0.01) but the geometry
    // is identical up to that constant, so the tool stays in XML units to match what you
    // type. Scaled* applies <Piece scale_factor> (ScaleFactor = scale% * 0.01).

    // Standard vanilla build orders (Native/ModuleData/crafting_templates.xml <PieceDatas>),
    // bundled so the tool works without that file. A loaded base crafting_templates.xml overrides.
    private static readonly (string type, int order)[] DefaultBuildOrder =
        { ("Handle", 0), ("Guard", 1), ("Blade", 2), ("Pommel", -1) };
    private static readonly (string type, int order)[] AxeMaceBuildOrder =
        { ("Handle", 0), ("Blade", 1), ("Pommel", -1) };

    private static readonly Dictionary<string, (string type, int order)[]> BundledBuildOrders =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["OneHandedSword"]   = DefaultBuildOrder,
        ["TwoHandedSword"]   = DefaultBuildOrder,
        ["Dagger"]           = DefaultBuildOrder,
        ["ThrowingKnife"]    = DefaultBuildOrder,
        ["TwoHandedPolearm"] = DefaultBuildOrder,
        ["Pike"]             = DefaultBuildOrder,
        ["Javelin"]          = DefaultBuildOrder,
        ["OneHandedAxe"]     = AxeMaceBuildOrder,
        ["TwoHandedAxe"]     = AxeMaceBuildOrder,
        ["Mace"]             = AxeMaceBuildOrder,
        ["TwoHandedMace"]    = AxeMaceBuildOrder,
        ["ThrowingAxe"]      = new[] { ("Handle", 0), ("Blade", 1) },
    };

    // Build orders loaded from a base crafting_templates.xml (override bundled), if provided.
    private readonly Dictionary<string, (string type, int order)[]> _loadedBuildOrders =
        new(StringComparer.OrdinalIgnoreCase);

    private string CurrentTemplateId() =>
        TemplateCombo.SelectedItem is string t && t != "(All pieces)" ? t : "";

    private (string type, int order)[] BuildOrderFor(string templateId)
    {
        if (!string.IsNullOrEmpty(templateId))
        {
            if (_loadedBuildOrders.TryGetValue(templateId, out var loaded)) return loaded;
            if (BundledBuildOrders.TryGetValue(templateId, out var bundled)) return bundled;
        }
        return DefaultBuildOrder;
    }

    // Returns signed pivot Z (raw units) per piece type, for the CURRENT template's build
    // order. weaponLength = crafted reach (raw units = cm; NaN if no blade); handToBottom =
    // how far the butt sits below the grip. Pieces not selected → NaN pivot (skipped by render).
    private Dictionary<string, float> CalculatePivotDistances(
        CraftingPieceData? blade, CraftingPieceData? guard,
        CraftingPieceData? handle, CraftingPieceData? pommel,
        out float weaponLength, out float handToBottom)
    {
        var byType = new Dictionary<string, CraftingPieceData?>
        {
            ["Blade"] = blade, ["Guard"] = guard, ["Handle"] = handle, ["Pommel"] = pommel
        };
        var pivots = new Dictionary<string, float>();
        float num = 0f, num2 = 0f;   // num = bottom pointer (toward pommel), num2 = top (toward blade)

        foreach (var (type, order) in BuildOrderFor(CurrentTemplateId()))
        {
            var el = byType.TryGetValue(type, out var sel) ? sel : null;
            if (el == null) { pivots[type] = float.NaN; continue; }

            int s = Math.Sign(order);
            if (s == 0)
            {
                num2 += el.ScaledPieceOffset;
                num  -= el.ScaledPieceOffset;
            }
            else if (s < 0)
            {
                num += el.ScaledDistanceToNextPiece;
                num += el.ScaledPieceOffset;
                num -= el.ScaledNextPieceOffset;
            }
            else // s > 0
            {
                num2 += el.ScaledDistanceToPreviousPiece;
                num2 += el.ScaledPieceOffset;
                num2 -= el.ScaledPreviousPieceOffset;
            }

            pivots[type] = (float)s * ((s < 0) ? num : num2) + ((s == 0) ? el.ScaledPieceOffset : 0f);

            if (s == 0)
            {
                num  += el.ScaledDistanceToPreviousPiece - el.ScaledPreviousPieceOffset;
                num2 += el.ScaledDistanceToNextPiece     - el.ScaledNextPieceOffset;
            }
            else if (s < 0)
            {
                num  += el.ScaledDistanceToPreviousPiece - el.ScaledPreviousPieceOffset;
            }
            else
            {
                num2 += el.ScaledDistanceToNextPiece     - el.ScaledNextPieceOffset;
            }
        }
        handToBottom = num;

        // CalculateWeaponLength(): max( bladePivot + blade.ScaledDistanceToNextPiece,
        //   max over valid pieces of (ScaledDistanceToNextPiece + ScaledPieceOffset) ).
        weaponLength = float.NaN;
        if (blade != null && pivots.TryGetValue("Blade", out float bp) && !float.IsNaN(bp))
        {
            float a = bp + blade.ScaledDistanceToNextPiece;
            float m = 0f;
            foreach (var el in new[] { blade, guard, handle, pommel })
                if (el != null && el.ScaledDistanceToNextPiece > m)
                    m = el.ScaledDistanceToNextPiece + el.ScaledPieceOffset;
            weaponLength = MathF.Max(a, m);
        }
        return pivots;
    }

    // ── 2D Canvas ─────────────────────────────────────────────────────────────

    private void RedrawCanvas()
    {
        if (WeaponCanvas == null) return;
        WeaponCanvas.Children.Clear();

        var pivots = CalculatePivotDistances(_blade, _guard, _handle, _pommel,
                                             out float weaponLength, out _);
        if (pivots.Count == 0) return;

        var slots = new[] {
            ("Blade", _blade), ("Guard", _guard), ("Handle", _handle), ("Pommel", _pommel)
        };

        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var (key, data) in slots)
        {
            if (data == null || !pivots.TryGetValue(key, out float piv) || float.IsNaN(piv)) continue;
            minZ = MathF.Min(minZ, piv - data.ScaledDistanceToPreviousPiece);
            maxZ = MathF.Max(maxZ, piv + data.ScaledDistanceToNextPiece);
        }
        if (minZ == float.MaxValue) return;
        // Always keep the grip (Z=0) in frame so the global shift from Handle.piece_offset is visible.
        minZ = MathF.Min(minZ, 0f);
        maxZ = MathF.Max(maxZ, 0f);

        const float cw = 220f, pad = 16f;
        WeaponCanvas.Height = (maxZ - minZ) * _pixelsPerUnit + pad * 2;
        float WorldToY(float z) => pad + (maxZ - z) * _pixelsPerUnit;

        // Z=0 line
        float zy = WorldToY(0f);
        WeaponCanvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 0, X2 = cw, Y1 = zy, Y2 = zy,
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x3A)),
            StrokeDashArray = new DoubleCollection { 5, 4 }, StrokeThickness = 1,
        });
        var zLbl = Label("grip (Z=0)", 3, zy - 13, "#3A3A3A");
        WeaponCanvas.Children.Add(zLbl);

        foreach (var (key, data) in slots)
        {
            if (data == null || !pivots.TryGetValue(key, out float piv) || float.IsNaN(piv)) continue;
            if (!PieceStyle.TryGetValue(key, out var s)) continue;

            // Faithful extent: piece spans [pivot - ScaledDistToPrev, pivot + ScaledDistToNext].
            float topZ  = piv + data.ScaledDistanceToNextPiece;
            float botZ  = piv - data.ScaledDistanceToPreviousPiece;
            float rectH = MathF.Max((topZ - botZ) * _pixelsPerUnit, 3f);
            float rectW = (float)s.w;
            float rectX = (cw - rectW) / 2f;
            float rectY = WorldToY(topZ);
            bool  active = data == _active;

            var fill = new SolidColorBrush(s.col) { Opacity = active ? 1.0 : 0.72 };
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = rectW, Height = rectH, Fill = fill, RadiusX = 2, RadiusY = 2,
                Stroke = active ? Brushes.White
                                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
                StrokeThickness = active ? 2 : 0.5,
                Cursor = Cursors.Hand, Tag = data,
                ToolTip = $"{key}\n{data.Id}\nlength={Fmt(data.Length)}\npiece_offset={Fmt(data.PieceOffset)}\nprev={Fmt(data.PreviousPieceOffset)}  next={Fmt(data.NextPieceOffset)}",
            };
            rect.MouseLeftButtonDown += PieceRect_Click;
            Canvas.SetLeft(rect, rectX); Canvas.SetTop(rect, rectY);
            WeaponCanvas.Children.Add(rect);

            // pivot tick
            float pivY = WorldToY(piv);
            WeaponCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = rectX, X2 = rectX + rectW, Y1 = pivY, Y2 = pivY,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 1, IsHitTestVisible = false,
            });

            if (rectH > 14)
            {
                var lbl = Label(key, rectX + 3, rectY + 2, "#FFFFFF");
                lbl.Width = rectW - 4;
                WeaponCanvas.Children.Add(lbl);
            }
        }

        string wl = float.IsNaN(weaponLength) ? "—"
            : $"{Fmt(weaponLength)} (reach {Fmt(MathF.Round(weaponLength))})";
        if (WeaponLengthLabel != null) WeaponLengthLabel.Text = $"Weapon length: {wl}";
        Status($"Weapon length: {wl} cm  |  Blade {Piv(pivots,"Blade")}  Guard {Piv(pivots,"Guard")}  Handle {Piv(pivots,"Handle")}  Pommel {Piv(pivots,"Pommel")}");
    }

    private void PieceRect_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Shapes.Rectangle { Tag: CraftingPieceData d })
        {
            SetActivePiece(d);
            RedrawCanvas();
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _pixelsPerUnit = (float)e.NewValue;
        RedrawCanvas();
    }

    private void CenterTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl) Refresh3D();
    }

    // 3D viewport click — select the piece under the cursor (ignore drags)
    private void Viewport3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(Viewport3D);
    }

    private void Viewport3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var up = e.GetPosition(Viewport3D);
        var dx = up.X - _mouseDownPos.X;
        var dy = up.Y - _mouseDownPos.Y;
        if (dx * dx + dy * dy > 25) return; // moved >5 px — was a drag, not a click

        CraftingPieceData? hit = null;
        VisualTreeHelper.HitTest(
            Viewport3D,
            null,
            r =>
            {
                if (r is RayMeshGeometry3DHitTestResult ray &&
                    ray.ModelHit is GeometryModel3D gm &&
                    _model3DMap.TryGetValue(gm, out var piece))
                {
                    hit = piece;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(_mouseDownPos));

        if (hit == null) return;
        SetActivePiece(hit);
        RedrawCanvas();
        Refresh3D();
        UpdateXmlOutput();
    }

    private static TextBlock Label(string text, float x, float y, string hex)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 9, IsHitTestVisible = false,
            Foreground = new SolidColorBrush((System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(hex)),
        };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
        return tb;
    }

    // ── 3D View ───────────────────────────────────────────────────────────────

    private void BrowseFbxFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select folder containing FBX files" };
        if (dlg.ShowDialog() == true)
        {
            FbxFolderBox.Text = dlg.FolderName;
            _fbxFolder = dlg.FolderName;
            IndexFbxFolder();
            Refresh3D();
        }
    }

    private void ReloadFbx_Click(object sender, RoutedEventArgs e)
    {
        _fbxFolder = FbxFolderBox.Text.Trim();
        IndexFbxFolder();
        Refresh3D();
    }

    // Drag one or more .fbx (or a folder) onto the viewport to index without browsing.
    private void Viewport3D_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Viewport3D_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        var fbx = new List<string>();
        bool folderDropped = false;
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                fbx.AddRange(Directory.EnumerateFiles(p, "*.fbx", SearchOption.AllDirectories));
                folderDropped = true;
            }
            else if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                fbx.Add(p);
            }
        }
        if (fbx.Count == 0) { Status("No FBX files in drop."); return; }
        // A folder drop replaces the index (full-folder behaviour); loose files merge into it.
        IndexFbxFiles(fbx, merge: !folderDropped);
        Refresh3D();
    }

    // Index a specific set of FBX files. merge=false clears first (full reindex); merge=true appends.
    private void IndexFbxFiles(List<string> files, bool merge)
    {
        if (!merge) { _meshIndex.Clear(); _meshCache.Clear(); }
        FbxStatusLabel.Text = "Indexing…";
        using var ctx = new AssimpContext();
        foreach (var path in files)
        {
            try
            {
                var scene = ctx.ImportFile(path, PostProcessSteps.None);
                if (scene?.RootNode == null) continue;
                IndexNode(scene.RootNode, scene, path);
            }
            catch { /* skip unreadable files */ }
        }
        FbxStatusLabel.Text = _meshIndex.Count > 0
            ? $"Index: {_meshIndex.Count} mesh names in {files.Count} FBX — select pieces to preview"
            : "No named mesh nodes found in dropped FBX";
    }

    // Phase 1: scan every FBX in the folder tree with no geometry processing (fast).
    // Builds _meshIndex: name → fbx-file-path indexed by both node names and mesh names.
    private void IndexFbxFolder()
    {
        _meshIndex.Clear();
        _meshCache.Clear();
        FbxStatusLabel.Text = "Indexing…";

        if (string.IsNullOrEmpty(_fbxFolder) || !Directory.Exists(_fbxFolder))
        {
            FbxStatusLabel.Text = "Folder not found";
            return;
        }

        var files = Directory.EnumerateFiles(_fbxFolder, "*.fbx", SearchOption.AllDirectories).ToList();
        int nodeCount = 0;

        using var ctx = new AssimpContext();
        foreach (var path in files)
        {
            try
            {
                var scene = ctx.ImportFile(path, PostProcessSteps.None);
                if (scene?.RootNode == null) continue;
                nodeCount += IndexNode(scene.RootNode, scene, path);
            }
            catch { /* skip unreadable files */ }
        }

        FbxStatusLabel.Text = nodeCount > 0
            ? $"Index: {nodeCount} meshes in {files.Count} FBX — select pieces to preview"
            : files.Count > 0
                ? $"Found {files.Count} FBX file(s) but no named mesh nodes"
                : "No FBX files found in folder";
    }

    // Index by node name AND by Assimp mesh name — Bannerlord FBX may store the
    // XML mesh-id in either place depending on how the FBX was exported.
    private int IndexNode(Node node, Scene scene, string fbxPath)
    {
        int count = 0;

        // Index node name
        var nodeName = node.Name;
        if (!string.IsNullOrEmpty(nodeName) &&
            !nodeName.Equals("RootNode", StringComparison.OrdinalIgnoreCase) &&
            !IsFilteredNode(nodeName))
        {
            if (_meshIndex.TryAdd(nodeName, fbxPath)) count++;
            // Also index without .lod0 suffix so XML "wm_blade" matches node "wm_blade.lod0"
            if (nodeName.EndsWith(".lod0", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = nodeName[..^5];
                if (!string.IsNullOrEmpty(baseName)) { _meshIndex.TryAdd(baseName, fbxPath); count++; }
            }
        }

        // Index names of meshes directly referenced by this node
        foreach (int mi in node.MeshIndices)
        {
            if (mi < 0 || mi >= scene.MeshCount) continue;
            var meshName = scene.Meshes[mi].Name;
            if (!string.IsNullOrEmpty(meshName) && !IsFilteredNode(meshName))
            {
                if (_meshIndex.TryAdd(meshName, fbxPath)) count++;
                if (meshName.EndsWith(".lod0", StringComparison.OrdinalIgnoreCase))
                {
                    var baseName = meshName[..^5];
                    if (!string.IsNullOrEmpty(baseName)) _meshIndex.TryAdd(baseName, fbxPath);
                }
            }
        }

        foreach (var child in node.Children)
            count += IndexNode(child, scene, fbxPath);
        return count;
    }

    private void Refresh3D()
    {
        if (Viewport3D == null) return;

        _fbxFolder = FbxFolderBox?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(_fbxFolder) || !Directory.Exists(_fbxFolder))
        {
            SceneRoot.Content = null;
            return;
        }

        var pivots = CalculatePivotDistances(_blade, _guard, _handle, _pommel, out _, out _);
        var group  = new Model3DGroup();
        _model3DMap.Clear();

        var slots = new[] {
            ("Blade", _blade), ("Guard", _guard), ("Handle", _handle), ("Pommel", _pommel)
        };

        int loaded = 0, missing = 0;
        bool anyLoaded = SceneRoot.Content != null; // don't zoom if scene already visible
        foreach (var (key, data) in slots)
        {
            if (data == null || !pivots.TryGetValue(key, out float piv) || float.IsNaN(piv)) continue;
            var geo = GetOrLoadMesh(data.MeshName);
            if (geo == null) { missing++; continue; }

            var col      = PieceStyle[key].col;
            bool isActive = data == _active;
            // Dim non-active pieces like the 2D canvas does
            var displayCol = isActive ? col
                : System.Windows.Media.Color.FromRgb(
                    (byte)(col.R * 0.72f), (byte)(col.G * 0.72f), (byte)(col.B * 0.72f));
            var mat   = new DiffuseMaterial(new SolidColorBrush(displayCol));
            // scale_factor scales the mesh geometry about its origin, then translate to the pivot.
            Transform3D xform;
            if (data.Scale != 100)
            {
                var g = new Transform3DGroup();
                g.Children.Add(new ScaleTransform3D(data.ScaleFactor, data.ScaleFactor, data.ScaleFactor));
                g.Children.Add(new TranslateTransform3D(0, piv, 0));
                xform = g;
            }
            else xform = new TranslateTransform3D(0, piv, 0);
            var model = new GeometryModel3D(geo, mat)
            {
                BackMaterial = mat,
                Transform    = xform,
            };
            _model3DMap[model] = data;
            group.Children.Add(model);
            loaded++;
        }

        SceneRoot.Content = group;
        if (loaded > 0 && !anyLoaded) Viewport3D.ZoomExtents(0);

        // Only overwrite the index-count label when we actually attempted mesh loads
        if (loaded > 0 || missing > 0)
            FbxStatusLabel.Text = missing > 0
                ? $"{loaded} loaded, {missing} not in index"
                : $"{loaded} meshes loaded";
        // else: keep the "Index: N meshes" text from IndexFbxFolder
    }

    // Phase 2: look up the mesh by node name in the pre-built index, then load only
    // that node's geometry from the FBX that contains it.
    private MeshGeometry3D? GetOrLoadMesh(string meshName)
    {
        if (_meshCache.TryGetValue(meshName, out var cached)) return cached;

        if (!_meshIndex.TryGetValue(meshName, out var fbxPath))
        {
            _meshCache[meshName] = null;
            return null;
        }

        try
        {
            var geo = LoadMeshByNodeName(fbxPath, meshName);
            _meshCache[meshName] = geo;
            return geo;
        }
        catch (Exception ex)
        {
            Status($"FBX error ({meshName}): {ex.Message}");
            _meshCache[meshName] = null;
            return null;
        }
    }

    private static MeshGeometry3D? LoadMeshByNodeName(string fbxPath, string targetName)
    {
        using var ctx = new AssimpContext();
        var scene = ctx.ImportFile(fbxPath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.GenerateNormals |
            PostProcessSteps.JoinIdenticalVertices);

        if (scene == null || !scene.HasMeshes) return null;

        var positions = new Point3DCollection();
        var normals   = new Vector3DCollection();
        var indices   = new Int32Collection();

        // Start with identity so the root node's own transform is the first applied
        var identity = new Assimp.Matrix4x4();
        identity.A1 = identity.B2 = identity.C3 = identity.D4 = 1f;
        FindAndCollectNode(scene.RootNode, scene, targetName, positions, normals, indices, identity);
        if (positions.Count == 0) return null;

        // Do NOT center — the Blender export origin is the correct attachment pivot.
        // The node world transform already places vertices at (0,0,0)-relative positions.
        // Refresh3D applies TranslateTransform3D(0, piv, 0) to position each piece.
        return new MeshGeometry3D { Positions = positions, Normals = normals, TriangleIndices = indices };
    }

    // Walks the node tree accumulating the world transform at each level.
    // Matches by node name OR by any mesh name the node references (handles both indexing cases).
    // Applies world transform to vertices so bounding-box centering and pivot offset work correctly.
    private static bool FindAndCollectNode(Node node, Scene scene, string targetName,
        Point3DCollection pos, Vector3DCollection nrm, Int32Collection idx,
        Assimp.Matrix4x4 parentTransform)
    {
        // world = parent_world * node_local  (column-vector convention, Assimp standard)
        var world = parentTransform * node.Transform;

        // Accept exact match OR name with .lod0 suffix (friend's naming convention)
        static bool NameMatches(string name, string target) =>
            string.Equals(name, target, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, target + ".lod0", StringComparison.OrdinalIgnoreCase);

        bool byNodeName = NameMatches(node.Name, targetName);
        bool byMeshName = !byNodeName && node.MeshIndices.Any(mi =>
            mi >= 0 && mi < scene.MeshCount &&
            NameMatches(scene.Meshes[mi].Name, targetName));

        if (byNodeName || byMeshName)
        {
            foreach (int mi in node.MeshIndices)
            {
                // When matching by mesh name, skip sibling meshes on the same node
                if (byMeshName && !NameMatches(scene.Meshes[mi].Name, targetName))
                    continue;

                var mesh    = scene.Meshes[mi];
                int baseIdx = pos.Count;

                foreach (var v in mesh.Vertices)
                    pos.Add(new Point3D(
                        world.A1 * v.X + world.A2 * v.Y + world.A3 * v.Z + world.A4,
                        world.B1 * v.X + world.B2 * v.Y + world.B3 * v.Z + world.B4,
                        world.C1 * v.X + world.C2 * v.Y + world.C3 * v.Z + world.C4));

                if (mesh.HasNormals)
                    foreach (var n in mesh.Normals)
                        // Normals use only the rotational part (no translation, no W term)
                        nrm.Add(new System.Windows.Media.Media3D.Vector3D(
                            world.A1 * n.X + world.A2 * n.Y + world.A3 * n.Z,
                            world.B1 * n.X + world.B2 * n.Y + world.B3 * n.Z,
                            world.C1 * n.X + world.C2 * n.Y + world.C3 * n.Z));
                else
                    for (int i = 0; i < mesh.VertexCount; i++)
                        nrm.Add(new System.Windows.Media.Media3D.Vector3D(0, 1, 0));

                foreach (var face in mesh.Faces)
                    if (face.IndexCount == 3)
                    {
                        idx.Add(baseIdx + face.Indices[0]);
                        idx.Add(baseIdx + face.Indices[1]);
                        idx.Add(baseIdx + face.Indices[2]);
                    }
            }
            return true;
        }

        foreach (var child in node.Children)
            if (FindAndCollectNode(child, scene, targetName, pos, nrm, idx, world))
                return true;

        return false;
    }

    // Skip LOD1+ meshes, bone helpers, collision meshes — keep LOD0 / main mesh
    private static bool IsFilteredNode(string name)
    {
        var n = name.ToLowerInvariant();
        return Regex.IsMatch(n, @"_lod[1-9]") ||   // _lod1, _lod2 … but not _lod0
               Regex.IsMatch(n, @"\blod[1-9]\b") || // standalone lod1, lod2 … but not lod0
               n.StartsWith("bo_") ||
               n.StartsWith("col_") ||
               n.StartsWith("ub_") ||
               n == "armature";
    }

    // ── XML output ────────────────────────────────────────────────────────────

    private void UpdateXmlOutput()
    {
        if (_active == null) { XmlOutputBox.Text = ""; return; }
        var p = _active;
        XmlOutputBox.Text =
            $"<!-- {p.Id} -->\n" +
            $"<BuildData\n" +
            $"    piece_offset=\"{Fmt(p.PieceOffset)}\"\n" +
            $"    previous_piece_offset=\"{Fmt(p.PreviousPieceOffset)}\"\n" +
            $"    next_piece_offset=\"{Fmt(p.NextPieceOffset)}\" />";
    }

    private void CopyXml_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(XmlOutputBox.Text))
            Clipboard.SetText(XmlOutputBox.Text);
    }

    // Export only the pieces whose offsets changed since load (dirty), with original + tuned
    // values, so an LLM can patch just those <BuildData> attributes back into the source XML.
    private void ExportOffsets_Click(object sender, RoutedEventArgs e)
    {
        var dirty = _allPieces.Where(p => p.IsDirty).ToList();
        if (dirty.Count == 0) { Status("No tuned offsets to export — nothing changed since load."); return; }

        var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "offset_tuning.json" };
        if (dlg.ShowDialog() != true) return;

        var payload = dirty.Select(p => new
        {
            id         = p.Id,
            piece_type = p.PieceType,
            length     = p.Length,
            original   = new { piece_offset = p.OrigPieceOffset, previous_piece_offset = p.OrigPreviousPieceOffset, next_piece_offset = p.OrigNextPieceOffset },
            tuned      = new { piece_offset = p.PieceOffset,     previous_piece_offset = p.PreviousPieceOffset,     next_piece_offset = p.NextPieceOffset },
        }).ToList();

        try
        {
            File.WriteAllText(dlg.FileName,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            Status($"Exported {dirty.Count} tuned piece(s) → {dlg.FileName}");
        }
        catch (Exception ex) { Status($"Export error: {ex.Message}"); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Status(string msg) => StatusLabel.Text = msg;

    private static string Fmt(float v) =>
        v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Piv(Dictionary<string, float> d, string k) =>
        d.TryGetValue(k, out float v) ? Fmt(v) : "—";

    private static string? PickXml(string filter = "XML files|*.xml|All files|*.*")
    {
        var dlg = new OpenFileDialog { Filter = filter };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}

public class CraftingPieceData
{
    public string Id                       { get; set; } = "";
    public string PieceType                { get; set; } = "";
    public string MeshName                 { get; set; } = "";
    public float  Length                   { get; set; }   // raw XML units (cm). NOT *0.01 — tool stays in XML units.
    public float  DistanceToNextPiece      { get; set; }   // = Length/2 if 'length' given, else from distance_to_next_piece
    public float  DistanceToPreviousPiece  { get; set; }
    public float  PieceOffset              { get; set; }
    public float  PreviousPieceOffset      { get; set; }
    public float  NextPieceOffset          { get; set; }
    public int    Scale                    { get; set; } = 100;  // <Piece scale_factor> %; default 100

    // As-loaded offsets, for dirty tracking + the export-for-AI file.
    public float  OrigPieceOffset          { get; set; }
    public float  OrigPreviousPieceOffset  { get; set; }
    public float  OrigNextPieceOffset      { get; set; }
    public bool   IsDirty =>
        PieceOffset         != OrigPieceOffset ||
        PreviousPieceOffset != OrigPreviousPieceOffset ||
        NextPieceOffset     != OrigNextPieceOffset;

    // Engine-faithful scaled accessors — mirror TaleWorlds.Core.WeaponDesignElement.Scaled*
    // (ScaleFactor = scalePercentage * 0.01; at 100% every Scaled* == its raw value).
    public float ScaleFactor                  => Scale * 0.01f;
    public float ScaledLength                  => Length                  * ScaleFactor;
    public float ScaledDistanceToNextPiece     => DistanceToNextPiece     * ScaleFactor;
    public float ScaledDistanceToPreviousPiece => DistanceToPreviousPiece * ScaleFactor;
    public float ScaledPieceOffset             => PieceOffset             * ScaleFactor;
    public float ScaledPreviousPieceOffset     => PreviousPieceOffset     * ScaleFactor;
    public float ScaledNextPieceOffset         => NextPieceOffset         * ScaleFactor;

    public override string ToString() => Id;
}
