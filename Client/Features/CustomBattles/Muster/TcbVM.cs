using System;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Muster;
using TAOM.Features.EngineBugGuards.Domain;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Client.Features.CustomBattles.Muster
{
    /// <summary>
    /// The new Custom Battle screen (user's mockup 2026-11-07): two tabs, Armies and Battlefield. Two header cards (your
    /// army / enemy army: commander portrait, banner, faction, commander, men); the card clicked is the army being
    /// mustered below (available troops with search and type tabs, the mustered army with numbers and formation options =
    /// banner bearer / captain / horn blower taken from the number). Faction, commander (TAOM's famous names first) and
    /// banner (colours + emblem) are chosen in popups from the cards; heroes of the faction can join the army. Reads what
    /// the old screen reads, so TAOM's curation holds; Start hands the army to CustomBattleMuster.Pending.
    /// </summary>
    public class TcbVM : ViewModel
    {
        private string _status = "";
        private bool _fieldTab;
        private TcbSideVM _active = null!;   // set in the constructor through Activate

        /// <param name="kingdomBanners">Culture id to its kingdom's banner code (IBannerConfigProvider), each army's first banner.</param>
        public TcbVM(IReadOnlyDictionary<string, string>? kingdomBanners = null)
        {
            var factions = CustomBattleData.Factions.Where(f => f != null).ToList();
            Player = new TcbSideVM(new TextObject("{=taom_tcb_your_army}Your army").ToString(), true, factions, 0, OnChanged, Activate, kingdomBanners);
            Enemy = new TcbSideVM(new TextObject("{=taom_tcb_enemy_army}Enemy army").ToString(), false, factions, factions.Count > 1 ? 1 : 0, OnChanged, Activate, kingdomBanners);
            Field = new TcbFieldVM(OnChanged);
            Activate(Player);
            CustomBattleMuster.Log($"screen open: {factions.Count} factions");
            OnChanged();
        }

        [DataSourceProperty] public TcbSideVM Player { get; }
        [DataSourceProperty] public TcbSideVM Enemy { get; }
        [DataSourceProperty] public TcbFieldVM Field { get; }
        [DataSourceProperty] public TcbSideVM Active { get => _active; private set { _active = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string TitleText => new TextObject("{=taom_tcb_title}CUSTOM BATTLE").ToString();
        [DataSourceProperty] public string ArmiesText => new TextObject("{=taom_tcb_armies}ARMIES").ToString();
        [DataSourceProperty] public string FieldText => new TextObject("{=taom_tcb_battlefield}BATTLEFIELD").ToString();
        [DataSourceProperty] public string StartText => new TextObject("{=taom_tcb_start_battle}START BATTLE").ToString();
        [DataSourceProperty] public string BackText => new TextObject("{=taom_tcb_back}BACK").ToString();
        [DataSourceProperty] public string RandomText => new TextObject("{=taom_tcb_randomise}RANDOMISE").ToString();
        [DataSourceProperty] public bool IsArmiesTab => !_fieldTab;
        [DataSourceProperty] public bool IsFieldTab => _fieldTab;
        private int Sum => (Player?.Total ?? 0) + (Enemy?.Total ?? 0);
        [DataSourceProperty] public string TotalText => new TextObject("{=taom_tcb_total_troops}{SUM} / {MAX} TROOPS").SetTextVariable("SUM", Sum).SetTextVariable("MAX", ArmySpec.MaxTroops).ToString();
        [DataSourceProperty] public bool IsOverLimit => Sum > ArmySpec.MaxTroops;
        [DataSourceProperty]
        public string SummaryText => Player == null || Enemy == null || Field == null ? "" :
            new TextObject("{=taom_tcb_summary}{PLAYER} ({PLAYER_TOTAL})   vs   {ENEMY} ({ENEMY_TOTAL})      |      {FIELD}").SetTextVariable("PLAYER", Player.FactionName).SetTextVariable("PLAYER_TOTAL", Player.Total).SetTextVariable("ENEMY", Enemy.FactionName).SetTextVariable("ENEMY_TOTAL", Enemy.Total).SetTextVariable("FIELD", Field.Summary).ToString();

        [DataSourceProperty]
        public string Status
        {
            get => _status;
            set { if (value != _status) { _status = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasStatus)); } }
        }

        /// <summary>The footer shows the status line only when there is one; the count stays centred without it.</summary>
        [DataSourceProperty] public bool HasStatus => !string.IsNullOrEmpty(_status);

        private void Activate(TcbSideVM side)
        {
            if (Player != null) Player.IsActive = side == Player;
            if (Enemy != null) Enemy.IsActive = side == Enemy;
            Active = side;
            if (_fieldTab) ExecuteArmiesTab();
        }

        private void OnChanged()
        {
            OnPropertyChanged(nameof(TotalText));
            OnPropertyChanged(nameof(IsOverLimit));
            OnPropertyChanged(nameof(SummaryText));
            Status = IsOverLimit ? TooManyText : "";
        }

        // ArmySpec.Validate reports the same limit in English for spec files, so START checks it here first.
        private static string TooManyText =>
            new TextObject("{=taom_tcb_too_many}Too many men: the engine takes at most {MAX}").SetTextVariable("MAX", ArmySpec.MaxTroops).ToString();

        public void ExecuteArmiesTab() { _fieldTab = false; OnPropertyChanged(nameof(IsArmiesTab)); OnPropertyChanged(nameof(IsFieldTab)); }
        public void ExecuteFieldTab() { _fieldTab = true; OnPropertyChanged(nameof(IsArmiesTab)); OnPropertyChanged(nameof(IsFieldTab)); }
        public void ExecuteBack() => Game.Current?.GameStateManager?.PopState(0);
        public void ExecuteRandomise() => Active?.Randomise(ArmySpec.MaxTroops - (Sum - (Active?.Total ?? 0)));

        public void ExecuteStart()
        {
            try
            {
                if (Player.Culture == null || Enemy.Culture == null) { Status = new TextObject("{=taom_tcb_choose_both_factions}Choose both factions").ToString(); return; }
                if (Player.Units.Count == 0) { Status = new TextObject("{=taom_tcb_muster_player}Muster at least one unit for your army").ToString(); Activate(Player); return; }
                if (Enemy.Units.Count == 0) { Status = new TextObject("{=taom_tcb_muster_enemy}Muster at least one unit for the enemy").ToString(); Activate(Enemy); return; }
                if (Field.SceneId == null) { Status = new TextObject("{=taom_tcb_choose_battlefield}Choose a battlefield").ToString(); ExecuteFieldTab(); return; }
                if (IsOverLimit) { Status = TooManyText; return; }
                var spec = new ArmySpec { Player = Player.ToSpec(), Enemy = Enemy.ToSpec() };
                var problems = spec.Validate();
                if (problems.Count > 0) { Status = problems[0]; return; }
                var playerCmd = Player.Commander ?? CustomBattleMuster.CommanderOf(Player.Culture);
                var enemyCmd = Enemy.Commander ?? CustomBattleMuster.CommanderOf(Enemy.Culture);
                if (playerCmd == null || enemyCmd == null) { Status = new TextObject("{=taom_tcb_no_commander}No commander found for a faction").ToString(); return; }
                // A captain serves under another lord. The engine reads that lord's name for every field battle and
                // throws on a null one (#1184); a siege starts without him.
                var (generalId, refused) = MusterGeneralChoice.ForStart(Field.PlayerType == CustomBattlePlayerType.Sergeant, Field.IsSiege,
                    Player.LordIds, playerCmd.StringId, Player.HeroUnitIds.Concat(Enemy.HeroUnitIds).Append(enemyCmd.StringId));
                if (refused)
                {
                    Status = new TextObject("{=taom_tcb_captain_needs_lord}A captain needs another lord of the faction to lead the army").ToString();
                    Activate(Player);
                    return;
                }
                var general = generalId == null ? null : Player.Lord(generalId);
                CustomBattleMuster.Pending = spec;
                var counts = new[] { 1, 0, 0, 0 };   // replaced by the spec in ArmyPatch
                bool attacker = Field.Side == CustomBattlePlayerSide.Attacker;
                var parties = CustomBattleHelper.GetCustomBattleParties(playerCmd, general, enemyCmd, Player.Culture, counts, null,
                    Enemy.Culture, (int[])counts.Clone(), null, attacker);
                List<MissionSiegeWeapon>? attackerEngines = null, defenderEngines = null;
                float[]? walls = null;
                if (Field.IsSiege)
                {
                    Field.SiegeEngines(out attackerEngines, out defenderEngines);
                    walls = CustomBattleHelper.GetWallHitpointPercentages(Field.BreachedWalls);
                    CustomBattleMuster.Log($"KCB siege: attacker engines [{string.Join(", ", attackerEngines.Select(w => w.Type.StringId))}], defender [{string.Join(", ", defenderEngines.Select(w => w.Type.StringId))}]");
                }
                var data = CustomBattleHelper.PrepareBattleData(playerCmd, general, parties[0], parties[1], Field.Side,
                    Field.PlayerType, Field.GameType, Field.SceneId, Field.Season, Field.TimeOfDay,
                    attackerEngines, defenderEngines, walls, Field.SceneLevel, false, Field.ForcedSceneLevel);
                CustomBattleMuster.Log($"KCB start: {Player.Culture.StringId} ({playerCmd.StringId}) vs {Enemy.Culture.StringId} ({enemyCmd.StringId}); {Field.GameType} on {Field.SceneId}, {Field.Season}, {Field.TimeOfDay}h, player {Field.Side} as {Field.PlayerType}" + (general != null ? $" under {general.StringId}" : "") + (Field.IsSiege ? $", {Field.BreachedWalls} walls breached, level {Field.SceneLevel}" : ""));
                CustomBattleHelper.StartGame(data);
                if (Field.PlayerType == CustomBattlePlayerType.Sergeant)
                    Outcomes.Pass(MusterChecks.CaptainStartId, Field.GameType, MusterChecks.StartedDetail(Field.GameType, general?.StringId));
            }
            catch (Exception e)
            {
                Status = new TextObject("{=taom_tcb_start_failed}Could not start: {ERROR}").SetTextVariable("ERROR", e.Message).ToString();
                CustomBattleMuster.Log("KCB start failed: " + e);
                if (Field.PlayerType == CustomBattlePlayerType.Sergeant)
                    Outcomes.Fail(MusterChecks.CaptainStartId, Field.GameType, MusterChecks.FailedDetail(Field.GameType, e));
            }
        }

        internal static ImageIdentifierVM? Portrait(BasicCharacterObject? c)
        {
            try { return c == null ? null : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(c)); }
            catch (Exception e) { CustomBattleMuster.Log($"KCB portrait {c?.StringId}: {e.Message}"); return null; }
        }

        /// <summary>A troop's name without TAOM's "[Faction] " prefix: the army already says whose it is.</summary>
        internal static string Clean(BasicCharacterObject c)
        {
            var n = c.Name?.ToString() ?? c.StringId;
            int end = n.StartsWith("[") ? n.IndexOf(']') : -1;
            return end > 0 && end + 1 < n.Length ? n.Substring(end + 1).Trim() : n;
        }

        /// <summary>The tier as Roman numerals up to TAOM's X (#1446); heroes show their own text and never reach this.</summary>
        internal static string Tier(BasicCharacterObject c)
        {
            int t = EngineDisplayRules.TroopTier(c.Level, isHero: false);
            return new TextObject("{=taom_tcb_tier}Tier {TIER}").SetTextVariable("TIER", EngineDisplayRules.TierNumeral(t)).ToString();
        }

        /// <summary>Shift = 10, Ctrl = 100 per click.</summary>
        internal static int Step()
        {
            if (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl)) return 100;
            if (Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift)) return 10;
            return 1;
        }
    }

    /// <summary>One army: faction, commander, banner (colours + emblem), the faction's troops and heroes, the units.</summary>
    public class TcbSideVM : ViewModel
    {
        private static readonly (string name, FormationClass? cls, bool heroes)[] Kinds =
        {
            ("All", null, false), ("Infantry", FormationClass.Infantry, false), ("Archers", FormationClass.Ranged, false),
            ("Cavalry", FormationClass.Cavalry, false), ("Horse archers", FormationClass.HorseArcher, false), ("Heroes", null, true)
        };

        private readonly Action _changed;
        private readonly Action<TcbSideVM> _activate;
        private TcbFactionVM? _faction;
        private TcbCharacterVM? _commander;
        private readonly MusterBannerDesign _design = new MusterBannerDesign();
        private uint _color1, _color2;            // shown and marked in the palette: read from the banner as drawn
        private int _paletteFor;                 // 0 closed, 1 background, 2 emblem
        private bool _iconsOpen, _factionsOpen, _commandersOpen, _bannerOpen, _isActive;
        private string _kind = "All";
        private string _search = "";
        private BannerImageIdentifierVM? _banner, _bannerTall;
        private ImageIdentifierVM? _commanderImage;
        private List<BasicCharacterObject> _soldiers = new List<BasicCharacterObject>();
        private List<BasicCharacterObject> _heroes = new List<BasicCharacterObject>();
        private HashSet<string> _famous = new HashSet<string>();
        private readonly Dictionary<BasicCharacterObject, TcbTroopVM> _troopVMs = new Dictionary<BasicCharacterObject, TcbTroopVM>();

        public TcbSideVM(string label, bool isPlayer, List<BasicCultureObject> factions, int initial, Action changed, Action<TcbSideVM> activate,
            IReadOnlyDictionary<string, string>? kingdomBanners = null)
        {
            Label = label.ToUpperInvariant();
            IsPlayer = isPlayer;
            _changed = changed;
            _activate = activate;
            Factions = new MBBindingList<TcbFactionVM>();
            Commanders = new MBBindingList<TcbCharacterVM>();
            TroopKinds = new MBBindingList<TcbChoiceVM>();
            Troops = new MBBindingList<TcbTroopVM>();
            Units = new MBBindingList<TcbUnitVM>();
            Palette = new MBBindingList<TcbSwatchVM>();
            IconGroups = new MBBindingList<TcbChoiceVM>();
            Icons = new MBBindingList<TcbIconVM>();
            foreach (var c in BannerPalette.All()) Palette.Add(new TcbSwatchVM(c, PickColor));
            // tab widths fit the words and fill the 744 px row of the 792 px Available Troops column
            var widths = new[] { 70, 132, 124, 118, 190, 110 };
            for (int i = 0; i < Kinds.Length; i++)
            {
                var k = Kinds[i];
                TroopKinds.Add(new TcbChoiceVM(TcbTroopVM.ClassName(k.cls, k.heroes), k.name, c => { _kind = k.name; FillTroops(); }) { Owner = TroopKinds, Width = widths[i] });
            }
            foreach (var g in BannerManager.Instance?.BannerIconGroups?.Where(g => !g.IsPattern) ?? Enumerable.Empty<BannerIconGroup>())
                IconGroups.Add(new TcbChoiceVM(g.Name?.ToString() ?? new TextObject("{=taom_tcb_group}Group {ID}").SetTextVariable("ID", g.Id.ToString()).ToString(), g, c => FillIcons((BannerIconGroup)c.Value)) { Owner = IconGroups });
            foreach (var f in factions) Factions.Add(new TcbFactionVM(f, MusterBannerDesign.KingdomCode(kingdomBanners, f.StringId) ?? CultureBannerCode(f), SelectFaction));
            if (Factions.Count > initial) SelectFaction(Factions[initial]);
            if (TroopKinds.Count > 0) TroopKinds[0].ExecuteSelect();
        }

        public BasicCultureObject? Culture => _faction?.Culture;
        public BasicCharacterObject? Commander => _commander?.Character;
        public int Total => Units.Sum(u => u.Count);

        // What MusterGeneralChoice needs to pick a captain's general: the faction's lords in the screen's order, the
        // heroes already mustered as units, and a lord by id.
        public IReadOnlyList<string> LordIds => _heroes.Select(h => h.StringId).ToList();
        public IEnumerable<string> HeroUnitIds => Units.Where(u => u.Troop.IsHero).Select(u => u.Troop.StringId);
        public BasicCharacterObject Lord(string id) => _heroes.First(h => h.StringId == id);

        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public bool IsPlayer { get; }
        [DataSourceProperty] public bool IsEnemy => !IsPlayer;
        [DataSourceProperty] public MBBindingList<TcbFactionVM> Factions { get; }
        [DataSourceProperty] public MBBindingList<TcbCharacterVM> Commanders { get; }
        [DataSourceProperty] public MBBindingList<TcbChoiceVM> TroopKinds { get; }
        [DataSourceProperty] public MBBindingList<TcbTroopVM> Troops { get; }
        [DataSourceProperty] public MBBindingList<TcbUnitVM> Units { get; }
        [DataSourceProperty] public MBBindingList<TcbSwatchVM> Palette { get; }
        [DataSourceProperty] public MBBindingList<TcbChoiceVM> IconGroups { get; }
        [DataSourceProperty] public MBBindingList<TcbIconVM> Icons { get; }
        [DataSourceProperty] public BannerImageIdentifierVM? Banner { get => _banner; private set { _banner = value; OnPropertyChangedWithValue(value); } }
        /// <summary>The tall banner image (nine-grid) the pennants are cut from, as TAOM's encyclopedia pennants (the square one showed no emblem).</summary>
        [DataSourceProperty] public BannerImageIdentifierVM? BannerTall { get => _bannerTall; private set { _bannerTall = value; OnPropertyChangedWithValue(value); } }
        /// <summary>The commander's portrait beside his name on the army card (user 2026-11-11: the 3D box cropped him badly).</summary>
        [DataSourceProperty] public ImageIdentifierVM? CommanderImage { get => _commanderImage; private set { _commanderImage = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string FactionName => _faction?.Name ?? "-";
        [DataSourceProperty] public string CommanderName => _commander?.Name ?? "-";
        [DataSourceProperty] public string TotalNumber => Total.ToString();
        [DataSourceProperty] public string TroopsLine => new TextObject("{=taom_tcb_troops_line}{TOTAL} TROOPS").SetTextVariable("TOTAL", Total).ToString();
        [DataSourceProperty] public string TroopsWord => new TextObject("{=taom_tcb_troops}TROOPS").ToString();
        [DataSourceProperty] public string ChangeFactionText => new TextObject("{=taom_tcb_change_faction}CHANGE FACTION").ToString();
        [DataSourceProperty] public string BannerHint => new TextObject("{=taom_tcb_banner_hint}Click the pennant to change the banner").ToString();
        [DataSourceProperty] public string CommanderHeader => new TextObject("{=taom_tcb_commander_header}COMMANDER").ToString();
        [DataSourceProperty] public string BannerHeader => new TextObject("{=taom_tcb_banner_header}BANNER").ToString();
        [DataSourceProperty] public string FactionHeader => new TextObject("{=taom_tcb_choose_faction_header}CHOOSE FACTION").ToString();
        [DataSourceProperty] public string ChooseCommanderHeader => new TextObject("{=taom_tcb_choose_commander_header}CHOOSE COMMANDER").ToString();
        [DataSourceProperty] public string AvailableHeader => new TextObject("{=taom_tcb_available_header}AVAILABLE TROOPS").ToString();
        [DataSourceProperty] public string MusteredHeader => new TextObject("{=taom_tcb_mustered_header}MUSTERED ARMY").ToString();
        [DataSourceProperty] public string SearchPlaceholder => new TextObject("{=taom_tcb_search_placeholder}Search troops...").ToString();
        [DataSourceProperty] public string EmptyArmyText => Units.Count == 0 ? new TextObject("{=taom_tcb_empty_army}Add troops from the Available Troops list").ToString() : "";
        [DataSourceProperty] public bool IsArmyEmpty => Units.Count == 0;
        [DataSourceProperty] public string HintText => new TextObject("{=taom_tcb_hint_text}Shift + click: 10    Ctrl + click: 100").ToString();
        [DataSourceProperty] public string Color1 => TcbSwatchVM.Hex(_color1);
        [DataSourceProperty] public string Color2 => TcbSwatchVM.Hex(_color2);
        [DataSourceProperty] public string BannerColorText => new TextObject("{=taom_tcb_background}BACKGROUND").ToString();
        [DataSourceProperty] public string EmblemColorText => new TextObject("{=taom_tcb_emblem_colour}EMBLEM COLOUR").ToString();
        [DataSourceProperty] public string EmblemText => new TextObject("{=taom_tcb_emblem}EMBLEM").ToString();
        [DataSourceProperty] public string PaletteTitle => _paletteFor == 1 ? new TextObject("{=taom_tcb_background_colour}BACKGROUND COLOUR").ToString() : new TextObject("{=taom_tcb_emblem_colour}EMBLEM COLOUR").ToString();
        [DataSourceProperty] public string CloseText => new TextObject("{=taom_tcb_done}DONE").ToString();
        [DataSourceProperty] public bool IsPaletteOpen => _paletteFor != 0;
        [DataSourceProperty] public bool IsIconsOpen => _iconsOpen;
        [DataSourceProperty] public bool IsBannerHome => _bannerOpen && !_iconsOpen && _paletteFor == 0;
        [DataSourceProperty] public bool IsFactionsOpen => _factionsOpen;
        [DataSourceProperty] public bool IsCommandersOpen => _commandersOpen;
        [DataSourceProperty] public bool IsBannerOpen => _bannerOpen;
        [DataSourceProperty] public bool IsPopupOpen => _factionsOpen || _commandersOpen || _bannerOpen;

        [DataSourceProperty]
        public bool IsActive
        {
            get => _isActive;
            set { if (value != _isActive) { _isActive = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public string SearchText
        {
            get => _search;
            set { value = value ?? ""; if (value != _search) { _search = value; OnPropertyChangedWithValue(value); FillTroops(); } }
        }

        public void ExecuteActivate() => _activate?.Invoke(this);

        // ---------------------------------------------------------------- popups
        private void SetPopup(bool factions, bool commanders, bool banner)
        {
            _factionsOpen = factions; _commandersOpen = commanders; _bannerOpen = banner;
            if (!banner) { _iconsOpen = false; _paletteFor = 0; }
            if (factions || commanders || banner) _activate?.Invoke(this);
            NotifyPopups();
        }

        private void NotifyPopups()
        {
            OnPropertyChanged(nameof(IsFactionsOpen)); OnPropertyChanged(nameof(IsCommandersOpen)); OnPropertyChanged(nameof(IsBannerOpen));
            OnPropertyChanged(nameof(IsPopupOpen)); OnPropertyChanged(nameof(IsPaletteOpen)); OnPropertyChanged(nameof(IsIconsOpen));
            OnPropertyChanged(nameof(IsBannerHome)); OnPropertyChanged(nameof(PaletteTitle));
            OnPropertyChanged(nameof(Color1)); OnPropertyChanged(nameof(Color2));
        }

        public void ExecuteOpenFactions() => SetPopup(true, false, false);
        public void ExecuteOpenCommanders() => SetPopup(false, true, false);
        public void ExecuteOpenBanner() => SetPopup(false, false, true);
        public void ExecuteClosePopup() => SetPopup(false, false, false);

        // ---------------------------------------------------------------- faction
        private void SelectFaction(TcbFactionVM f)
        {
            if (f == _faction) { if (_factionsOpen) ExecuteClosePopup(); return; }
            foreach (var x in Factions) x.IsSelected = x == f;
            _faction = f;
            var all = Game.Current.ObjectManager.GetObjectTypeList<BasicCharacterObject>();
            _soldiers = all.Where(c => c.IsSoldier && !c.IsObsolete && c.Culture == f.Culture && CaptainResolver.IsOffered(c))
                           .OrderBy(c => c.Level).ThenBy(c => c.DefaultFormationClass).ToList();
            // heroes: TAOM's curated commanders of the faction first (its famous names), then its other lords
            var curated = Curated(f.Culture.StringId);
            _famous = new HashSet<string>(curated);
            _heroes = all.Where(c => c.IsHero && c.Culture == f.Culture && !c.IsObsolete && !c.StringId.StartsWith("spc_"))
                         .OrderBy(c => { var i = curated.IndexOf(c.StringId); return i < 0 ? 1000 : i; })
                         .ThenBy(c => c.Name?.ToString()).ToList();
            _troopVMs.Clear();
            Commanders.Clear();
            foreach (var h in _heroes) Commanders.Add(new TcbCharacterVM(h, _famous.Contains(h.StringId), SelectCommander));
            _commander = null;
            Units.Clear();
            if (Commanders.Count > 0) SelectCommander(Commanders[0]);
            // the kingdom's own banner, untouched until the player edits it
            _design.Reset(f.BannerCode);
            RebuildBanner();
            FillTroops();
            OnPropertyChanged(nameof(FactionName));
            if (_factionsOpen) ExecuteClosePopup();
            Refresh();
        }

        // TAOM's curated commanders (ICustomBattleCommandersProvider through ISideCommanderFilter), as the old screen shows them
        private static List<string> Curated(string culture) => CustomBattleMuster.CuratedCommanderIds(culture).ToList();

        private void SelectCommander(TcbCharacterVM c)
        {
            foreach (var x in Commanders) x.IsSelected = x == c;
            _commander = c;
            CommanderImage = TcbVM.Portrait(c?.Character);
            // the commander leads; he is not also one of the army's heroes
            var dup = Units.FirstOrDefault(u => u.Troop == c?.Character);
            if (dup != null) Units.Remove(dup);
            OnPropertyChanged(nameof(CommanderName));
            if (_commandersOpen) ExecuteClosePopup();
            FillTroops();
            Refresh();
        }

        // ---------------------------------------------------------------- troops and heroes
        private void FillTroops()
        {
            Troops.Clear();
            var kind = Kinds.First(k => k.name == _kind);
            IEnumerable<BasicCharacterObject> list = kind.heroes ? _heroes.Where(h => h != Commander)
                : _soldiers.Where(c => kind.cls == null || c.DefaultFormationClass == kind.cls);
            if (_kind == "All") list = list.Concat(_heroes.Where(h => h != Commander));
            if (!string.IsNullOrWhiteSpace(_search))
                list = list.Where(c => TcbVM.Clean(c).IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (var c in list)
            {
                if (!_troopVMs.TryGetValue(c, out var vm))
                    _troopVMs[c] = vm = new TcbTroopVM(c, AddUnit, c.IsHero ? (_famous.Contains(c.StringId) ? new TextObject("{=taom_tcb_famous_hero}Famous hero").ToString() : new TextObject("{=taom_tcb_lord}Lord").ToString()) : null);
                Troops.Add(vm);
            }
        }

        // a click adds 20 men (or 10 more to a unit already mustered); Shift / Ctrl: 10 / 100
        private void AddUnit(TcbTroopVM t)
        {
            int s = TcbVM.Step();
            AddUnit(t.Troop, t.Troop.IsHero ? 1 : s == 1 ? 20 : s);
        }

        private void AddUnit(BasicCharacterObject troop, int count)
        {
            var existing = Units.FirstOrDefault(u => u.Troop == troop);
            if (existing != null) { if (!troop.IsHero) existing.Add(TcbVM.Step() == 1 ? 10 : TcbVM.Step()); return; }
            Units.Add(new TcbUnitVM(troop, troop.IsHero ? 1 : count, Refresh, u => { Units.Remove(u); Refresh(); }));
            Refresh();
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(TotalNumber));
            OnPropertyChanged(nameof(TroopsLine));
            OnPropertyChanged(nameof(EmptyArmyText));
            OnPropertyChanged(nameof(IsArmyEmpty));
            _changed?.Invoke();
        }

        internal void Randomise(int room)
        {
            Units.Clear();
            var rnd = new Random();
            int budget = Math.Max(0, Math.Min(room, 200 + rnd.Next(0, 5) * 50));
            var pick = _soldiers.OrderBy(_ => rnd.Next()).Take(Math.Min(_soldiers.Count, 3 + rnd.Next(0, 3))).ToList();
            foreach (var t in pick)
            {
                int n = Math.Min(budget, (2 + rnd.Next(0, 7)) * 10);
                if (n <= 0) break;
                budget -= n;
                AddUnit(t, n);
                var u = Units.Last();
                if (rnd.Next(0, 3) == 0) u.ExecuteToggleBanner();
                if (rnd.Next(0, 3) == 0) u.ExecuteToggleCaptain();
            }
            var hero = _heroes.Where(h => h != Commander).OrderBy(_ => rnd.Next()).FirstOrDefault();
            if (hero != null && budget > 0 && rnd.Next(0, 2) == 0) AddUnit(hero, 1);
            Refresh();
        }

        // ---------------------------------------------------------------- banner
        /// <summary>A culture without a kingdom: its own banner in its colours, as the other TAOM battle launchers make it.</summary>
        internal static string CultureBannerCode(BasicCultureObject culture)
        {
            try
            {
                if (culture.Banner == null) return "";
                var b = new Banner(culture.Banner);
                if (b.GetBannerDataListCount() == 0) return "";
                b.ChangePrimaryColor(BannerPalette.Nearest(culture.Color) ?? culture.Color);
                b.ChangeIconColors(BannerPalette.Nearest(culture.Color2) ?? culture.Color2);
                return b.Serialize();
            }
            catch (Exception e) { CustomBattleMuster.Log($"KCB culture banner {culture.StringId}: {e.Message}"); return ""; }
        }

        /// <summary>A banner drawn from a code exactly (Deserialize keeps the code as the banner's own).</summary>
        internal static Banner FromCode(string code)
        {
            try { return MusterBannerDesign.ExactCode(code) != null ? new Banner(code) : new Banner(); }
            catch { return new Banner(); } // swallow-ok: an unreadable code draws an empty banner, the side stays usable
        }

        private void RebuildBanner()
        {
            _bannerCode = _design.Code;
            var b = FromCode(_bannerCode);
            Banner = new BannerImageIdentifierVM(b, false);
            BannerTall = new BannerImageIdentifierVM(b, true);
            if (b.GetBannerDataListCount() > 0)
            {
                _color1 = b.GetPrimaryColor();
                _color2 = b.GetBannerDataListCount() > 1 ? b.GetFirstIconColor() : b.GetSecondaryColor();
            }
            if (_iconsOpen) foreach (var i in Icons) i.Show(_design.WithEmblem(i.IconId));
        }

        private string _bannerCode = "";

        public void ExecuteEditColor1() { _paletteFor = 1; _iconsOpen = false; TcbSwatchVM.MarkCurrent(Palette, _color1); NotifyPopups(); }
        public void ExecuteEditColor2() { _paletteFor = 2; _iconsOpen = false; TcbSwatchVM.MarkCurrent(Palette, _color2); NotifyPopups(); }

        public void ExecuteOpenIcons()
        {
            _iconsOpen = true;
            _paletteFor = 0;
            if (Icons.Count == 0 && IconGroups.Count > 0) IconGroups[0].ExecuteSelect();
            NotifyPopups();
        }

        private void FillIcons(BannerIconGroup g)
        {
            Icons.Clear();
            foreach (var id in g.AvailableIcons.Keys.OrderBy(k => k))
                Icons.Add(new TcbIconVM(id, _design.WithEmblem(id), PickIcon));
        }

        private void PickIcon(TcbIconVM i)
        {
            _design.SetEmblem(i.IconId);
            _iconsOpen = false;
            RebuildBanner();
            NotifyPopups();
        }

        private void PickColor(TcbSwatchVM s)
        {
            int id = BannerManager.GetColorId(s.Argb);
            if (_paletteFor == 1) _design.SetBackgroundColor(id); else if (_paletteFor == 2) _design.SetEmblemColor(id);
            _paletteFor = 0;
            RebuildBanner();
            NotifyPopups();
        }

        // ---------------------------------------------------------------- out
        public ArmySpec.SideSpec ToSpec() => new ArmySpec.SideSpec
        {
            Faction = Culture?.StringId,
            Commander = Commander?.StringId,
            Color1 = Color1.Substring(0, 7),
            Color2 = Color2.Substring(0, 7),
            BannerIcon = _design.Emblem,
            BannerCode = _bannerCode,
            Units = Units.Select(u => new ArmySpec.UnitSpec { Troop = u.Troop.StringId, Count = u.Count, Banner = u.Banner, Captain = u.Captain, Horn = u.Horn }).ToList()
        };
    }

    /// <summary>A commander or hero to choose.</summary>
    public class TcbCharacterVM : ViewModel
    {
        private readonly Action<TcbCharacterVM> _select;
        private bool _isSelected;
        private ImageIdentifierVM? _image;

        public TcbCharacterVM(BasicCharacterObject c, bool famous, Action<TcbCharacterVM> select)
        {
            Character = c;
            _select = select;
            IsFamous = famous;
            Name = TcbVM.Clean(c);
            Info = famous ? new TextObject("{=taom_tcb_famous_commander}Famous commander").ToString() : new TextObject("{=taom_tcb_lord}Lord").ToString();
        }

        public BasicCharacterObject Character { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Info { get; }
        [DataSourceProperty] public bool IsFamous { get; }
        // made on first use: a portrait per lord of the faction only when the commander list is shown
        [DataSourceProperty] public ImageIdentifierVM? Image => _image ??(_image = TcbVM.Portrait(Character));

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value); } }
        }

        public void ExecuteSelect() => _select?.Invoke(this);
    }

    /// <summary>One emblem of the game's banner icons, shown on the side's own banner.</summary>
    public class TcbIconVM : ViewModel
    {
        private readonly Action<TcbIconVM> _pick;
        private BannerImageIdentifierVM? _image;

        public TcbIconVM(int id, string bannerCode, Action<TcbIconVM> pick)
        {
            IconId = id;
            _pick = pick;
            Show(bannerCode);
        }

        public int IconId { get; }
        [DataSourceProperty] public BannerImageIdentifierVM? Image { get => _image; private set { _image = value; OnPropertyChangedWithValue(value); } }

        /// <summary>The army's banner with this emblem (MusterBannerDesign.WithEmblem).</summary>
        internal void Show(string bannerCode) => Image = new BannerImageIdentifierVM(TcbSideVM.FromCode(bannerCode), false);

        public void ExecuteSelect() => _pick?.Invoke(this);
    }

    /// <summary>One colour of the game's banner palette.</summary>
    public class TcbSwatchVM : ViewModel
    {
        private readonly Action<TcbSwatchVM> _pick;

        public TcbSwatchVM(uint argb, Action<TcbSwatchVM> pick)
        {
            Argb = argb;
            _pick = pick;
            Color = Hex(argb);
        }

        private bool _isSelected;

        public uint Argb { get; }
        [DataSourceProperty] public string Color { get; }

        /// <summary>The colour the army uses now, framed in the open palette.</summary>
        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value); } }
        }

        public void ExecuteSelect() => _pick?.Invoke(this);

        internal static void MarkCurrent(IEnumerable<TcbSwatchVM> palette, uint current)
        {
            foreach (var s in palette) s.IsSelected = s.Argb == current;
        }

        internal static string Hex(uint argb) => "#" + (argb & 0xFFFFFF).ToString("X6") + "FF";
    }

    public class TcbFactionVM : ViewModel
    {
        private readonly Action<TcbFactionVM> _select;
        private bool _isSelected;
        private BannerImageIdentifierVM? _bannerTall;

        /// <param name="bannerCode">The kingdom's banner (or the culture's when it has no kingdom): the army's first banner.</param>
        public TcbFactionVM(BasicCultureObject culture, string bannerCode, Action<TcbFactionVM> select)
        {
            Culture = culture;
            BannerCode = bannerCode;
            _select = select;
            Name = culture.Name?.ToString() ?? culture.StringId;
        }

        public BasicCultureObject Culture { get; }
        public string BannerCode { get; }
        [DataSourceProperty] public string Name { get; }

        /// <summary>The tall banner the faction card's pennant is cut from (TcbHangingBannerSmall), made on first use.</summary>
        [DataSourceProperty]
        public BannerImageIdentifierVM? BannerTall => _bannerTall ??= new BannerImageIdentifierVM(TcbSideVM.FromCode(BannerCode), true);

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value); } }
        }

        public void ExecuteSelect() => _select?.Invoke(this);
    }

    /// <summary>A troop or hero of the faction that can be added to the army.</summary>
    public class TcbTroopVM : ViewModel
    {
        private readonly Action<TcbTroopVM> _add;
        private ImageIdentifierVM? _image;

        public TcbTroopVM(BasicCharacterObject troop, Action<TcbTroopVM> add, string? heroInfo)
        {
            Troop = troop;
            _add = add;
            Name = TcbVM.Clean(troop);
            Info = heroInfo ?? new TextObject("{=taom_tcb_tier_class}{TIER}  ·  {CLASS}").SetTextVariable("TIER", TcbVM.Tier(troop)).SetTextVariable("CLASS", TcbTroopVM.ClassName(troop.DefaultFormationClass)).ToString();
            IsHero = troop.IsHero;
        }

        public BasicCharacterObject Troop { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Info { get; }
        [DataSourceProperty] public bool IsHero { get; }
        [DataSourceProperty] public ImageIdentifierVM? Image => _image ??(_image = TcbVM.Portrait(Troop));

        public void ExecuteAdd() => _add?.Invoke(this);

        internal static string ClassName(FormationClass c)
        {
            switch (c)
            {
                case FormationClass.Infantry: return new TextObject("{=taom_tcb_kind_infantry}Infantry").ToString();
                case FormationClass.Ranged: return new TextObject("{=taom_tcb_kind_archers}Archers").ToString();
                case FormationClass.Cavalry: return new TextObject("{=taom_tcb_kind_cavalry}Cavalry").ToString();
                case FormationClass.HorseArcher: return new TextObject("{=taom_tcb_kind_horse_archers}Horse archers").ToString();
                default: return c.ToString();
            }
        }

        /// <summary>The label of a troop type tab: its class name, "All" or "Heroes". The tab's id stays the English word in Kinds.</summary>
        internal static string ClassName(FormationClass? c, bool heroes)
        {
            if (heroes) return new TextObject("{=taom_tcb_kind_heroes}Heroes").ToString();
            return c == null ? new TextObject("{=taom_tcb_kind_all}All").ToString() : ClassName(c.Value);
        }
    }

    /// <summary>A mustered unit: its number and its roles (each role is one man of the number). A hero is one man, no roles.</summary>
    public class TcbUnitVM : ViewModel
    {
        private readonly Action _changed;
        private readonly Action<TcbUnitVM> _remove;
        private int _count;
        private bool _banner, _captain, _horn, _optionsOpen;

        public TcbUnitVM(BasicCharacterObject troop, int count, Action changed, Action<TcbUnitVM> remove)
        {
            Troop = troop;
            _count = troop.IsHero ? 1 : count;
            _changed = changed;
            _remove = remove;
            Name = TcbVM.Clean(troop);
            Info = troop.IsHero ? new TextObject("{=taom_tcb_hero_one_man}Hero  ·  one man").ToString() : new TextObject("{=taom_tcb_tier_class}{TIER}  ·  {CLASS}").SetTextVariable("TIER", TcbVM.Tier(troop)).SetTextVariable("CLASS", TcbTroopVM.ClassName(troop.DefaultFormationClass)).ToString();
            Image = TcbVM.Portrait(troop);
        }

        public BasicCharacterObject Troop { get; }
        public int Count => _count;
        public bool Banner => _banner;
        public bool Captain => _captain;
        public bool Horn => _horn;
        private int Roles => (_banner ? 1 : 0) + (_captain ? 1 : 0) + (_horn ? 1 : 0);

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Info { get; }
        [DataSourceProperty] public ImageIdentifierVM? Image { get; }
        [DataSourceProperty] public bool IsHero => Troop.IsHero;
        [DataSourceProperty] public bool IsTroop => !Troop.IsHero;
        [DataSourceProperty] public string CountText => _count.ToString();
        [DataSourceProperty] public bool IsOptionsOpen => _optionsOpen && IsTroop;
        [DataSourceProperty] public string OptionsText => Roles == 0 ? new TextObject("{=taom_tcb_formation_options}FORMATION OPTIONS").ToString() : string.Join(" · ", new[] { _banner ? new TextObject("{=taom_tcb_banner_caps}BANNER").ToString() : null, _captain ? new TextObject("{=taom_tcb_captain_caps}CAPTAIN").ToString() : null, _horn ? new TextObject("{=taom_tcb_horn_caps}HORN").ToString() : null }.Where(s => s != null));
        [DataSourceProperty] public string Breakdown => Roles == 0 ? new TextObject("{=taom_tcb_soldiers}{COUNT} soldiers").SetTextVariable("COUNT", _count).ToString() : new TextObject("{=taom_tcb_soldiers_plus_roles}{COUNT} soldiers + {ROLES} from the unit's number").SetTextVariable("COUNT", _count - Roles).SetTextVariable("ROLES", Roles).ToString();
        [DataSourceProperty] public bool IsBanner => _banner;
        [DataSourceProperty] public bool IsCaptain => _captain;
        [DataSourceProperty] public bool IsHorn => _horn;
        [DataSourceProperty] public string BannerText => new TextObject("{=taom_tcb_banner_bearer}Banner Bearer").ToString();
        [DataSourceProperty] public string CaptainText => new TextObject("{=taom_tcb_captain}Captain").ToString();
        [DataSourceProperty] public string HornText => new TextObject("{=taom_tcb_horn_blower}Horn Blower").ToString();

        public void Add(int n)
        {
            if (IsHero) return;
            _count = Math.Max(ArmySpec.MinimumCount(_banner, _captain, _horn), Math.Min(ArmySpec.MaxTroops, _count + n));
            Notify();
        }

        public void ExecutePlus() => Add(TcbVM.Step());
        public void ExecuteMinus() => Add(-TcbVM.Step());
        public void ExecuteRemove() => _remove?.Invoke(this);
        public void ExecuteToggleOptions() { _optionsOpen = !_optionsOpen; OnPropertyChanged(nameof(IsOptionsOpen)); }

        public void ExecuteToggleBanner() { if (IsHero) return; _banner = !_banner; Fit(); }
        public void ExecuteToggleCaptain() { if (IsHero) return; _captain = !_captain; Fit(); }
        public void ExecuteToggleHorn() { if (IsHero) return; _horn = !_horn; Fit(); }

        private void Fit()
        {
            _count = Math.Max(_count, ArmySpec.MinimumCount(_banner, _captain, _horn));
            Notify();
        }

        private void Notify()
        {
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(Breakdown));
            OnPropertyChanged(nameof(OptionsText));
            OnPropertyChanged(nameof(IsBanner));
            OnPropertyChanged(nameof(IsCaptain));
            OnPropertyChanged(nameof(IsHorn));
            _changed?.Invoke();
        }
    }
}
