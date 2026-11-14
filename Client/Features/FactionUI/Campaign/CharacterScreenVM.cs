using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;

namespace TAOM.Features.FactionUI.Campaign
{
    // Presentation only. Every investment, perk choice and close action goes to the native VM.
    public class CharacterScreenVM : ViewModel
    {
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public bool CanSwitchMember => !Read<bool>(Read<ViewModel>(Native, "CharacterList"), "HasSingleItem");
        [DataSourceProperty] public MBBindingList<CharacterAttributeTabVM> Tabs { get; private set; } = new MBBindingList<CharacterAttributeTabVM>();
        [DataSourceProperty] public MBBindingList<CharacterSkillItemVM> Skills { get; private set; } = new MBBindingList<CharacterSkillItemVM>();
        [DataSourceProperty] public MBBindingList<CharacterPerkItemVM> Perks { get; private set; } = new MBBindingList<CharacterPerkItemVM>();
        // Null until the constructor's first Synchronize finds a character and a skill.
        private ViewModel? _character, _skill, _firstPerk;
        private CharacterAttributeTabVM? _selectedAttribute;
        private string _nameText = "", _levelText = "";
        private int _nameFontSize;
        private bool _hasNoTraits, _hasPartyRole;
        private int _perkCount;
        private float _perkWidth;
        // The skill's perk tiers (sorted, distinct PerkVM.Level) and the SkillVM.Level they were last drawn for.
        private List<int> _tiers = new List<int>();
        private int _skillLevel = -1;
        private float _progressWidth, _markerDiamondLeft, _markerLabelLeft, _scrollFraction;
        // The exact marker position in the perk content; the fill, the drawn marker and the scroll derive from it.
        private float _markerX;
        private bool _markerLabelFlipped, _canScrollPerks;
        private int _scrollRequest;
        private string _skillLevelText = "";
        // The current hero's native SkillVMs, the per-skill NumOfUnopenedPerks buffer refilled each frame, and the
        // open tier counts the badges show.
        private readonly CharacterOpenTiers _openTiers = new CharacterOpenTiers();
        private List<SkillVM> _nativeSkills = new List<SkillVM>();
        private int[] _openTierSignal = Array.Empty<int>();
        private bool _hasAttributePoints, _hasFocusPoints;
        // True while the hero has unspent points: the pool value turns bright gold.
        [DataSourceProperty] public bool HasAttributePoints { get => _hasAttributePoints; private set { _hasAttributePoints = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool HasFocusPoints { get => _hasFocusPoints; private set { _hasFocusPoints = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float ProgressWidth { get => _progressWidth; private set { _progressWidth = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float MarkerDiamondLeft { get => _markerDiamondLeft; private set { _markerDiamondLeft = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float MarkerLabelLeft { get => _markerLabelLeft; private set { _markerLabelLeft = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsMarkerLabelFlipped { get => _markerLabelFlipped; private set { _markerLabelFlipped = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string SkillLevelText { get => _skillLevelText; private set { _skillLevelText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float PerkScrollFraction { get => _scrollFraction; private set { _scrollFraction = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public int PerkScrollRequest { get => _scrollRequest; private set { _scrollRequest = value; OnPropertyChangedWithValue(value); } }

        [DataSourceProperty] public ViewModel? CurrentCharacter { get => _character; private set { _character = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public ViewModel? CurrentSkill { get => _skill; private set { _skill = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public CharacterAttributeTabVM? SelectedAttribute { get => _selectedAttribute; private set { _selectedAttribute = value; OnPropertyChangedWithValue(value); } }
        // The current member's native trait items (nonzero personality traits) with their native level names.
        [DataSourceProperty] public MBBindingList<CharacterTraitItemVM> Traits { get; private set; } = new MBBindingList<CharacterTraitItemVM>();
        [DataSourceProperty] public bool HasNoTraits { get => _hasNoTraits; private set { _hasNoTraits = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool HasPartyRole { get => _hasPartyRole; private set { _hasPartyRole = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float PerkWidth { get => _perkWidth; private set { _perkWidth = value; OnPropertyChangedWithValue(value); } }
        // True only when the skill has more tiers than the view shows: the scroll arrows show only then.
        [DataSourceProperty] public bool CanScrollPerks { get => _canScrollPerks; private set { _canScrollPerks = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string NameText { get => _nameText; private set { _nameText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string LevelText { get => _levelText; private set { _levelText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public int NameFontSize { get => _nameFontSize; private set { _nameFontSize = value; OnPropertyChangedWithValue(value); } }

        public CharacterScreenVM(ViewModel native)
        {
            Native = native;
            Synchronize();
        }

        // The perk view is 1260 wide: five 252 tier columns fit, more scroll so every tier stays reachable.
        internal const float PerkViewWidth = 1260f;
        internal static float PerkContentWidth(int tiers) => Math.Max(PerkViewWidth, tiers * CharacterPerkItemVM.ColumnWidth);

        internal static T Read<T>(ViewModel source, string property) => (T)source.GetPropertyValue(property);
        internal static IEnumerable<ViewModel> Items(ViewModel source, string property) => ((IEnumerable)source.GetPropertyValue(property)).Cast<ViewModel>();

        public void Synchronize() => Synchronize(false);

        // recountOpenTiers: the screen's own Reset or perk choice recounts at once instead of waiting on the signal.
        internal void Synchronize(bool recountOpenTiers)
        {
            var character = Read<ViewModel>(Native, "CurrentCharacter");
            if (!ReferenceEquals(character, CurrentCharacter))
            {
                CurrentCharacter = character;
                NameText = Read<string>(character, "HeroNameText");
                NameFontSize = Math.Max(15, Math.Min(28, 300 / Math.Max(1, NameText.Length)));
                LevelText = new TextObject("{=taom_hero_level}Level {LEVEL}")
                    .SetTextVariable("LEVEL", Read<string>(character, "CurrentCharacterLevelLbl")).ToString();
                Tabs.Clear();
                foreach (ViewModel attribute in Items(character, "Attributes")) Tabs.Add(new CharacterAttributeTabVM(this, attribute));
                // Counted for this hero before the skill tabs are built, so no tab shows another hero's badge.
                _nativeSkills = Items(character, "Skills").Cast<SkillVM>().ToList();
                RefreshOpenTiers(character, true);
                var inspected = Read<ViewModel>(character, "CurrentSkill");
                string skillId = Read<string>(inspected, "SkillId");
                SelectAttribute(Tabs.First(t => t.SkillIds.Contains(skillId)), false);
                var hero = ((CharacterDeveloperHeroItemVM)character).Hero;
                RebuildTraits(Traits, Items(character, "Traits"), trait => TraitLevelName(hero, Read<string>(trait, "TraitId")));
                HasNoTraits = Traits.Count == 0;
                HasPartyRole = Items(character, "CharacterStats").Any();
            }
            var skill = Read<ViewModel>(character, "CurrentSkill");
            bool changed = !ReferenceEquals(skill, CurrentSkill);
            if (changed)
            {
                CurrentSkill = skill;
                var tab = Tabs.First(t => t.SkillIds.Contains(Read<string>(skill, "SkillId")));
                if (tab != SelectedAttribute) SelectAttribute(tab, false);
            }
            var perks = Items(skill, "Perks").ToList();
            bool rebuilt = changed || perks.Count != _perkCount || !ReferenceEquals(perks.FirstOrDefault(), _firstPerk);
            if (rebuilt)
            {
                Perks.Clear();
                _tiers = perks.Select(p => Read<int>(p, "Level")).Distinct().OrderBy(l => l).ToList();
                foreach (ViewModel perk in perks) Perks.Add(new CharacterPerkItemVM(this, perk, _tiers.IndexOf(Read<int>(perk, "Level"))));
                PerkWidth = PerkContentWidth(_tiers.Count);
                CanScrollPerks = PerkWidth > PerkViewWidth;
                _firstPerk = perks.FirstOrDefault();
                _perkCount = perks.Count;
            }
            int level = Read<int>(skill, "Level");
            if (rebuilt || level != _skillLevel) RefreshProgress(level);
            // Only a new skill (a skill or member switch) scrolls to the level; a reset or a perk choice
            // keeps the player's own scroll.
            if (changed)
            {
                PerkScrollFraction = CharacterSkillProgress.ScrollFraction(_markerX, PerkWidth, PerkViewWidth);
                PerkScrollRequest++;
            }
            foreach (var item in Skills) item.RefreshSelection();
            foreach (var item in Perks) item.RefreshState();
            RefreshOpenTiers(character, recountOpenTiers);
            var pools = (CharacterDeveloperHeroItemVM)character;
            bool attributePoints = pools.UnspentAttributePoints > 0, focusPoints = pools.UnspentCharacterPoints > 0;
            if (attributePoints != HasAttributePoints) HasAttributePoints = attributePoints;
            if (focusPoints != HasFocusPoints) HasFocusPoints = focusPoints;
        }

        // Per frame this reads one int per skill; the perks are walked only when the native signal moved, the hero
        // changed or the screen's own command asked.
        private void RefreshOpenTiers(ViewModel character, bool force)
        {
            if (_openTierSignal.Length != _nativeSkills.Count) _openTierSignal = new int[_nativeSkills.Count];
            for (int i = 0; i < _nativeSkills.Count; i++) _openTierSignal[i] = _nativeSkills[i].NumOfUnopenedPerks;
            if (!_openTiers.NeedsRecount(character, _openTierSignal, force)) return;
            _openTiers.Store(_nativeSkills.Select(skill => new KeyValuePair<string, int>(skill.SkillId,
                CharacterOpenTiers.OpenTiers(skill.Perks.Select(perk => (perk.Level, perk.PerkState))))));
            foreach (var tab in Tabs) tab.SetOpenTiers(_openTiers.ForAttribute(tab.SkillIds));
            foreach (var item in Skills) item.SetOpenTiers(_openTiers.ForSkill(item.SkillId));
        }

        internal static void RebuildTraits(MBBindingList<CharacterTraitItemVM> list, IEnumerable<ViewModel> natives, Func<ViewModel, string> name)
        {
            list.Clear();
            foreach (var native in natives) list.Add(new CharacterTraitItemVM(native, name(native)));
        }

        // The native EncyclopediaTraitItemVM keeps its TraitObject private, so the trait comes from the same
        // CampaignUIHelper.GetHeroTraits list the native hero item built it from. GetTraitNameText reads
        // GameTexts "str_trait_name_<id>" at variation level + |MinValue|: the localized positive or negative
        // level name (Honest, Deceitful), not the base trait name.
        private static string TraitLevelName(Hero hero, string traitId)
        {
            var trait = CampaignUIHelper.GetHeroTraits().FirstOrDefault(t => t.StringId == traitId);
            return trait == null ? "" : CampaignUIHelper.GetTraitNameText(trait, hero);
        }

        private void RefreshProgress(int level)
        {
            _skillLevel = level;
            float marker = CharacterSkillProgress.MarkerX(_tiers, level);
            float content = PerkContentWidth(_tiers.Count);
            _markerX = marker;
            ProgressWidth = CharacterSkillProgress.FillWidth(marker, content);
            var visual = CharacterSkillProgress.Visual(marker, content);
            MarkerDiamondLeft = visual.DiamondLeft;
            MarkerLabelLeft = visual.LabelLeft;
            IsMarkerLabelFlipped = visual.LabelFlipped;
            SkillLevelText = level.ToString();
            foreach (var item in Perks) item.RefreshTier(level);
        }

        internal void SelectAttribute(CharacterAttributeTabVM tab, bool selectSkill)
        {
            foreach (var entry in Tabs) entry.IsSelected = entry == tab;
            SelectedAttribute = tab;
            Skills.Clear();
            var character = CurrentCharacter;
            if (character is null) return;
            foreach (ViewModel skill in Items(character, "Skills").Where(s => tab.SkillIds.Contains(Read<string>(s, "SkillId"))))
            {
                var item = new CharacterSkillItemVM(this, skill);
                item.SetOpenTiers(_openTiers.ForSkill(item.SkillId));
                Skills.Add(item);
            }
            if (selectSkill && Skills.Count > 0) InspectSkill(Skills[0].Native);
        }

        internal void InspectSkill(ViewModel skill)
        {
            skill.ExecuteCommand("ExecuteInspect", Array.Empty<object>());
            Synchronize();
        }

        private void ExecutePreviousMember() => SwitchMember("ExecuteSelectPreviousItem", -1);
        private void ExecuteNextMember() => SwitchMember("ExecuteSelectNextItem", 1);
        private void SwitchMember(string command, int direction)
        {
            Read<ViewModel>(Native, "CharacterList").ExecuteCommand(command, Array.Empty<object>());
            Synchronize();
        }
        private void ExecuteReset() { Native.ExecuteCommand("ExecuteReset", Array.Empty<object>()); Synchronize(true); }
    }

    // The count badge's two bindings, shared by the attribute tab and the skill card.
    public abstract class CharacterOpenTierBadgeVM : ViewModel
    {
        private int _openTierCount;
        [DataSourceProperty] public int OpenTierCount { get => _openTierCount; private set { _openTierCount = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool HasOpenTiers => _openTierCount > 0;
        internal void SetOpenTiers(int count)
        {
            if (count == _openTierCount) return;
            bool wasVisible = HasOpenTiers;
            OpenTierCount = count;
            if (HasOpenTiers != wasVisible) OnPropertyChangedWithValue(HasOpenTiers, nameof(HasOpenTiers));
        }
    }

    public class CharacterAttributeTabVM : CharacterOpenTierBadgeVM
    {
        private readonly CharacterScreenVM _screen;
        private bool _isSelected;
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public string NameText { get; private set; }
        [DataSourceProperty] public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChangedWithValue(value); } }
        internal HashSet<string> SkillIds { get; private set; }
        internal CharacterAttributeTabVM(CharacterScreenVM screen, ViewModel native)
        {
            _screen = screen;
            Native = native;
            NameText = ((CharacterAttributeItemVM)native).AttributeType.Name.ToString();
            SkillIds = new HashSet<string>(CharacterScreenVM.Items(native, "BoundSkills").Select(s => CharacterScreenVM.Read<string>(s, "SkillId")));
        }
        private void ExecuteSelect() => _screen.SelectAttribute(this, true);
    }

    // One inline trait: the native item (icon id, value, effect hint) plus its native level name and the
    // signed value. A negative trait has its own value colour.
    public class CharacterTraitItemVM : ViewModel
    {
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public string NameText { get; private set; }
        [DataSourceProperty] public string ValueText { get; private set; }
        [DataSourceProperty] public bool IsNegative { get; private set; }
        internal static string SignedValue(int value) => value > 0 ? "+" + value : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        internal CharacterTraitItemVM(ViewModel native, string name)
        {
            Native = native;
            NameText = name;
            int value = CharacterScreenVM.Read<int>(native, "Value");
            ValueText = SignedValue(value);
            IsNegative = value < 0;
        }
    }

    public class CharacterSkillItemVM : CharacterOpenTierBadgeVM
    {
        private readonly CharacterScreenVM _screen;
        [DataSourceProperty] public ViewModel Native { get; private set; }
        internal string SkillId { get; }
        private bool _isSelected;
        [DataSourceProperty] public bool IsSelected { get => _isSelected; private set { _isSelected = value; OnPropertyChangedWithValue(value); } }
        internal void RefreshSelection() { bool value = CharacterScreenVM.Read<bool>(Native, "IsInspected"); if (value != IsSelected) IsSelected = value; }
        internal CharacterSkillItemVM(CharacterScreenVM screen, ViewModel native)
        {
            _screen = screen;
            Native = native;
            SkillId = CharacterScreenVM.Read<string>(native, "SkillId");
        }
        private void ExecuteSelect() => _screen.InspectSkill(Native);
    }

    public class CharacterPerkItemVM : ViewModel
    {
        private readonly CharacterScreenVM _screen;
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public float X { get; private set; }
        [DataSourceProperty] public float Y { get; private set; }
        [DataSourceProperty] public bool ShowLevel { get; private set; }
        // True on the first perk of a pair (native AlternativeType 1): it draws the connector and the "or"
        // down to its alternative. A single perk (0) draws none.
        [DataSourceProperty] public bool HasAlternativeBelow { get; private set; }
        [DataSourceProperty] public string NameText { get; private set; }
        // One 252 column per tier, five across the 1260 view (layout D); a pair's second medallion sits 182
        // below the first, with the connector and the "or" between them.
        internal const float ColumnWidth = 252f;
        internal static float ColumnX(int tier) => tier * ColumnWidth;
        internal static float RowY(int alternativeType) => alternativeType == 2 ? 182f : 0f;
        internal static bool HasConnectorBelow(int alternativeType) => alternativeType == 1;
        internal static float ConnectorAlphaFor(bool tierReached) => tierReached ? 1f : 0.6f;
        private float _connectorAlpha = 1f;
        // The connector is quieter in a locked tier.
        [DataSourceProperty] public float ConnectorAlpha { get => _connectorAlpha; private set { _connectorAlpha = value; OnPropertyChangedWithValue(value); } }
        private int _state = -1;
        [DataSourceProperty] public int State { get => _state; private set { _state = value; OnPropertyChangedWithValue(value); } }
        internal void RefreshState()
        {
            int value = CharacterScreenVM.Read<int>(Native, "PerkState");
            if (value != State) State = value;
            bool open = TierOpen(ShowLevel, value);
            if (open != IsTierOpen) IsTierOpen = open;
        }
        // An open tier (native state 1) shows a small diamond beside its label; the label is on the first perk only.
        internal static bool TierOpen(bool showLevel, int state) => showLevel && state == CharacterOpenTiers.SelectableState;
        private bool _isTierOpen;
        [DataSourceProperty] public bool IsTierOpen { get => _isTierOpen; private set { _isTierOpen = value; OnPropertyChangedWithValue(value); } }
        // The native PerkVM.Level this medallion's column stands for; reached or locked shows on the tier label
        // (gold, or the word Locked, not colour alone), so only the first perk of a column carries it.
        [DataSourceProperty] public int Tier { get; private set; }
        [DataSourceProperty] public string LockedText { get; private set; }
        private bool _isTierReached, _isTierLocked;
        [DataSourceProperty] public bool IsTierReached { get => _isTierReached; private set { _isTierReached = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsTierLocked { get => _isTierLocked; private set { _isTierLocked = value; OnPropertyChangedWithValue(value); } }
        internal void RefreshTier(int level)
        {
            bool reached = ShowLevel && CharacterSkillProgress.IsReached(Tier, level);
            bool locked = ShowLevel && !reached;
            if (reached != IsTierReached) IsTierReached = reached;
            if (locked != IsTierLocked) IsTierLocked = locked;
            float alpha = ConnectorAlphaFor(CharacterSkillProgress.IsReached(Tier, level));
            if (alpha != ConnectorAlpha) ConnectorAlpha = alpha;
        }
        internal CharacterPerkItemVM(CharacterScreenVM screen, ViewModel native, int index)
        {
            _screen = screen;
            Native = native;
            int alternative = CharacterScreenVM.Read<int>(native, "AlternativeType");
            X = ColumnX(index);
            Y = RowY(alternative);
            ShowLevel = alternative != 2;
            HasAlternativeBelow = HasConnectorBelow(alternative);
            NameText = ((PerkVM)native).Perk.Name.ToString();
            Tier = CharacterScreenVM.Read<int>(native, "Level");
            LockedText = new TextObject("{=taom_char_perk_tier_locked}Locked").ToString();
        }
        // The native PerkVM.Hint is a BasicTooltipViewModel whose tooltip text is
        // CampaignUIHelper.GetPerkEffectText: the perk's effects, shown on hover instead of a detail strip.
        private void ExecuteBeginHint() => ((PerkVM)Native).Hint.ExecuteBeginHint();
        private void ExecuteEndHint() => ((PerkVM)Native).Hint.ExecuteEndHint();
        private void ExecuteSelect()
        {
            Native.ExecuteCommand("ExecuteStartSelection", Array.Empty<object>());
            _screen.Synchronize(true);
        }
    }
}
