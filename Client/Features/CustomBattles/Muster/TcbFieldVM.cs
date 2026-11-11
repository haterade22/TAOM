using System;
using TAOM.Features.CustomBattles.Muster;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Client.Features.CustomBattles.Muster
{
    /// <summary>
    /// The Battlefield tab (user's mockup 2026-11-07): a large preview of the chosen map with a strip of neighbouring maps
    /// (arrows = previous / next), and the battle settings as dropdowns: battle type (field battle / village / siege, split
    /// as the old screen's MapSelectionGroupVM does), player type (commander / captain), side, map, season, time of day
    /// (CustomBattleTimeOfDay = the hour), and for a siege the breached walls and the town's level. Siege engines: ram +
    /// two towers, as the game's own CPU benchmark siege. Previews: GUI/CustomBattleMuster/Maps/&lt;scene id&gt;.png (TcbSprites.MapSprite).
    /// </summary>
    public class TcbFieldVM : ViewModel
    {
        private const int StripSize = 5;
        private readonly Action _changed;
        private readonly List<CustomBattleSceneData> _scenes;
        private readonly TcbDropdownVM _type, _player, _side, _map, _season, _time, _walls, _level, _list;
        private readonly TcbDropdownVM _atkEngines, _rngEngines, _defEngines;

        private static SiegeEngineType? Engine(string id)
        {
            switch (id.Trim())
            {
                case "ram": return DefaultSiegeEngineTypes.Ram;
                case "tower": return DefaultSiegeEngineTypes.SiegeTower;
                case "onager": return DefaultSiegeEngineTypes.Onager;
                case "trebuchet": return DefaultSiegeEngineTypes.Trebuchet;
                case "ballista": return DefaultSiegeEngineTypes.Ballista;
                case "fireballista": return DefaultSiegeEngineTypes.FireBallista;
                case "catapult": return DefaultSiegeEngineTypes.Catapult;
                case "firecatapult": return DefaultSiegeEngineTypes.FireCatapult;
                default: return null;
            }
        }

        private static IEnumerable<MissionSiegeWeapon> Engines(TcbDropdownVM d) =>
            ((d?.Selected?.Value as string) ?? "").Split(',').Select(Engine).Where(e => e != null).Select(e => MissionSiegeWeapon.CreateDefaultWeapon(e!));
        private bool _allMaps;

        public TcbFieldVM(Action changed)
        {
            _changed = changed;
            _scenes = CustomGame.Current?.CustomBattleScenes?.ToList() ?? new List<CustomBattleSceneData>();
            Settings = new MBBindingList<TcbDropdownVM>();
            Thumbs = new MBBindingList<TcbChoiceVM>();
            _type = Add("Game Type", CustomBattleData.GameTypes.Select(t => (t.Item1, (object)t.Item2)), c => FillMaps());
            _player = Add("Player Type", new[] { ("Commander", (object)CustomBattlePlayerType.Commander), ("Captain", (object)CustomBattlePlayerType.Sergeant) }, null);
            _side = Add("Player Side", new[] { ("Attacker", (object)CustomBattlePlayerSide.Attacker), ("Defender", (object)CustomBattlePlayerSide.Defender) }, null);
            _map = Add("Map", Enumerable.Empty<(string, object)>(), c => FillThumbs());
            _list = Add("Maps Shown", new[] { ("TAOM maps", (object)false), ("All maps", (object)true) }, c => { _allMaps = (bool)c.Value; FillMaps(); });
            _season = Add("Season", CustomBattleData.Seasons.Select(t => (t.Item1, (object)t.Item2)), null);
            _time = Add("Time of Day", CustomBattleData.TimesOfDay.Select(t => (t.Item1, (object)(int)t.Item2)), null);
            _walls = Add("Walls", CustomBattleData.WallHitpoints.Select(t => (t.Item1, (object)t.Item2)), null);
            _level = Add("Town Level", CustomBattleData.SceneLevels.Select(l => ($"Level {l}", (object)l)), null);
            // siege engines: the game's own Custom Battle machine slots
            _atkEngines = Add("Attacker Engines", new[]
            {
                ("Ram and two towers", (object)"ram,tower,tower"), ("Ram only", (object)"ram"), ("Two towers", (object)"tower,tower"),
                ("Ladders only", (object)""),
            }, null);
            _rngEngines = Add("Attacker Artillery", new[]
            {
                ("None", (object)""), ("Two catapults", (object)"onager,onager"), ("Trebuchet and catapults", (object)"trebuchet,onager,onager,fireballista"),
            }, null);
            _defEngines = Add("Defender Engines", new[]
            {
                ("None", (object)""), ("Ballistas", (object)"ballista,ballista"), ("Catapults and ballistas", (object)"firecatapult,catapult,ballista,fireballista"),
            }, null);
            _walls.IsSiegeOnly = _level.IsSiegeOnly = _atkEngines.IsSiegeOnly = _rngEngines.IsSiegeOnly = _defEngines.IsSiegeOnly = true;
            foreach (var d in Settings) d.Pick(0);
            _level.Pick(_level.Options.Count - 1);
            _type.Pick(0);
        }

        [DataSourceProperty] public MBBindingList<TcbDropdownVM> Settings { get; }
        [DataSourceProperty] public MBBindingList<TcbChoiceVM> Thumbs { get; }
        [DataSourceProperty] public string PreviewHeader => "BATTLEFIELD PREVIEW";
        [DataSourceProperty] public string SettingsHeader => "BATTLE SETTINGS";
        [DataSourceProperty] public string Tagline => "Choose the field upon which your armies shall meet.";
        [DataSourceProperty] public string MapName => _map.Selected?.Name ?? "No map for this battle type";
        [DataSourceProperty] public string PreviewSprite => TcbSprites.MapSprite(SceneId);
        [DataSourceProperty] public bool HasPreview => TcbSprites.HasMapPreview(SceneId);
        [DataSourceProperty] public bool NoPreview => !HasPreview;
        [DataSourceProperty] public string NoPreviewText => "No preview of this map yet";
        [DataSourceProperty] public bool IsSiege => GameType == "Siege";

        [DataSourceProperty]
        public string Summary => $"{_type.Selected?.Name}  ·  {_map.Selected?.Name ?? "no map"}  ·  {_time.Selected?.Name}, {_season.Selected?.Name}  ·  {_side.Selected?.Name}" +
                                 (IsSiege ? $", {_walls.Selected?.Name}" : "");

        public string GameType => _type.Selected?.Value as string ?? "Battle";
        public string? SceneId => (_map.Selected?.Value as CustomBattleSceneData?)?.SceneID;
        public string? ForcedSceneLevel => (_map.Selected?.Value as CustomBattleSceneData?)?.ForcedSceneLevel;
        public int TimeOfDay => _time.Selected?.Value is int h ? h : 12;
        public string Season => _season.Selected?.Value as string ?? "summer";
        public CustomBattlePlayerSide Side => _side.Selected?.Value is CustomBattlePlayerSide s ? s : CustomBattlePlayerSide.Attacker;
        public CustomBattlePlayerType PlayerType => _player.Selected?.Value is CustomBattlePlayerType p ? p : CustomBattlePlayerType.Commander;
        public int BreachedWalls => _walls.Selected?.Value is int w ? w : 0;
        public int SceneLevel => _level.Selected?.Value is int l ? l : 3;


        public void ExecutePreviousMap() => Step(-1);
        public void ExecuteNextMap() => Step(1);

        private void Step(int d)
        {
            var list = _map.Options;
            if (list.Count == 0) return;
            int i = _map.Selected == null ? 0 : Math.Max(0, list.IndexOf(_map.Selected));
            list[(i + d + list.Count) % list.Count].ExecuteSelect();
        }

        /// <summary>The siege engines as chosen (the game's own Custom Battle builds the same lists from its machine slots;
        /// an empty melee list = ladders only).</summary>
        public void SiegeEngines(out List<MissionSiegeWeapon> attacker, out List<MissionSiegeWeapon> defender)
        {
            attacker = Engines(_atkEngines).Concat(Engines(_rngEngines)).ToList();
            defender = Engines(_defEngines).ToList();
        }

        private static bool IsTaomMap(CustomBattleSceneData s) =>
            (s.SceneID ?? "").StartsWith("taom", StringComparison.OrdinalIgnoreCase) || (s.Name?.ToString() ?? "").StartsWith("[");

        private TcbDropdownVM Add(string label, IEnumerable<(string name, object value)> items, Action<TcbChoiceVM>? chosen)
        {
            var d = new TcbDropdownVM(label, Settings, c => { chosen?.Invoke(c); Notify(); });
            foreach (var (name, value) in items) d.AddOption(name, value);
            Settings.Add(d);
            return d;
        }

        private void FillMaps()
        {
            var type = GameType;
            // TAOM's own maps unless "All maps" (the old Calradia scenes are still registered: Ab Comer Castle, Akkalat...)
            var fit = _scenes.Where(s => type == "Village" ? s.IsVillageMap : type == "Siege" ? s.IsSiegeMap : !s.IsVillageMap && !s.IsSiegeMap && !s.IsLordsHallMap)
                             .Where(s => _allMaps || IsTaomMap(s))
                             .OrderBy(s => s.Name?.ToString() ?? s.SceneID);
            _map.Clear();
            foreach (var s in fit) _map.AddOption(s.Name?.ToString() ?? s.SceneID, s);
            _map.Pick(0);
            foreach (var d in Settings) d.Refresh(IsSiege);
            OnPropertyChanged(nameof(IsSiege));
            FillThumbs();
        }

        /// <summary>The strip: the chosen map with its neighbours in the list, the chosen one highlighted.</summary>
        private void FillThumbs()
        {
            Thumbs.Clear();
            var list = _map.Options;
            if (list.Count > 0)
            {
                int i = _map.Selected == null ? 0 : Math.Max(0, list.IndexOf(_map.Selected));
                int start = Math.Max(0, Math.Min(i - StripSize / 2, list.Count - StripSize));
                foreach (var o in list.Skip(start).Take(StripSize))
                {
                    var sceneId = (o.Value as CustomBattleSceneData?)?.SceneID;
                    Thumbs.Add(new TcbChoiceVM(o.Name, o, c => ((TcbChoiceVM)c.Value).ExecuteSelect())
                    { Sprite = TcbSprites.MapSprite(sceneId), IsSelected = o == _map.Selected });
                }
            }
            OnPropertyChanged(nameof(MapName));
            OnPropertyChanged(nameof(PreviewSprite));
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(NoPreview));
        }

        private void Notify()
        {
            OnPropertyChanged(nameof(Summary));
            _changed?.Invoke();
        }
    }

    /// <summary>A setting shown as a dropdown: its label, the chosen option, and the options while open (one open at a time).</summary>
    public class TcbDropdownVM : ViewModel
    {
        private readonly MBBindingList<TcbDropdownVM> _all;
        private readonly Action<TcbChoiceVM>? _chosen;
        private bool _isOpen, _isShown = true;

        public TcbDropdownVM(string label, MBBindingList<TcbDropdownVM> all, Action<TcbChoiceVM>? chosen)
        {
            Label = label;
            _all = all;
            _chosen = chosen;
            Options = new MBBindingList<TcbChoiceVM>();
        }

        public bool IsSiegeOnly;
        public TcbChoiceVM? Selected { get; private set; }
        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public MBBindingList<TcbChoiceVM> Options { get; }
        [DataSourceProperty] public string ValueText => Selected?.Name ?? "-";
        [DataSourceProperty] public bool IsOpen { get => _isOpen; set { if (value != _isOpen) { _isOpen = value; OnPropertyChangedWithValue(value); } } }
        [DataSourceProperty] public bool IsShown { get => _isShown; set { if (value != _isShown) { _isShown = value; OnPropertyChangedWithValue(value); } } }
        /// <summary>Height of the open list: up to 8 rows, then it scrolls.</summary>
        [DataSourceProperty] public int ListHeight => Math.Min(8, Math.Max(1, Options.Count)) * 40 + 8;

        internal void AddOption(string name, object value) =>
            Options.Add(new TcbChoiceVM(name, value, c =>
            {
                Selected = c;
                IsOpen = false;
                OnPropertyChanged(nameof(ValueText));
                _chosen?.Invoke(c);
            }) { Owner = Options });

        internal void Clear() { Options.Clear(); Selected = null; OnPropertyChanged(nameof(ValueText)); OnPropertyChanged(nameof(ListHeight)); }
        internal void Pick(int i) { if (Options.Count > i) Options[i].ExecuteSelect(); OnPropertyChanged(nameof(ListHeight)); }
        internal void Refresh(bool siege) { IsShown = !IsSiegeOnly || siege; if (!IsShown) IsOpen = false; }

        public void ExecuteToggle()
        {
            bool open = !IsOpen;
            foreach (var d in _all) d.IsOpen = false;
            IsOpen = open;
        }
    }

    /// <summary>One option of a choice list; choosing it deselects the others of its list.</summary>
    public class TcbChoiceVM : ViewModel
    {
        private readonly Action<TcbChoiceVM> _chosen;
        private bool _isSelected;

        public TcbChoiceVM(string name, object value, Action<TcbChoiceVM> chosen)
        {
            Name = name;
            Value = value;
            _chosen = chosen;
        }

        public object Value { get; }
        public MBBindingList<TcbChoiceVM>? Owner;
        [DataSourceProperty] public string Name { get; }
        /// <summary>Width of the option's button where words differ much in length (the troop type tabs).</summary>
        [DataSourceProperty] public int Width { get; set; } = 100;
        /// <summary>An image for the option (a map's preview in the strip).</summary>
        [DataSourceProperty] public string Sprite { get; set; } = "";

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value); } }
        }

        public void ExecuteSelect()
        {
            if (Owner != null) foreach (var o in Owner) o.IsSelected = o == this;
            else IsSelected = true;
            _chosen?.Invoke(this);
        }
    }
}
