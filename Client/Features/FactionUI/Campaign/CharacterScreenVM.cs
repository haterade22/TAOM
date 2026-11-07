using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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
        private string _detailTitle = "", _detailText = "";
        private string _nameText = "", _levelText = "";
        private int _nameFontSize;
        private bool _showTraits;
        private int _perkCount;
        private float _perkWidth;

        [DataSourceProperty] public ViewModel? CurrentCharacter { get => _character; private set { _character = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public ViewModel? CurrentSkill { get => _skill; private set { _skill = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public CharacterAttributeTabVM? SelectedAttribute { get => _selectedAttribute; private set { _selectedAttribute = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string DetailTitle { get => _detailTitle; private set { _detailTitle = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string DetailText { get => _detailText; private set { _detailText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowTraits { get => _showTraits; private set { _showTraits = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float PerkWidth { get => _perkWidth; private set { _perkWidth = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string NameText { get => _nameText; private set { _nameText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string LevelText { get => _levelText; private set { _levelText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public int NameFontSize { get => _nameFontSize; private set { _nameFontSize = value; OnPropertyChangedWithValue(value); } }

        public CharacterScreenVM(ViewModel native)
        {
            Native = native;
            Synchronize();
        }

        internal static T Read<T>(ViewModel source, string property) => (T)source.GetPropertyValue(property);
        internal static IEnumerable<ViewModel> Items(ViewModel source, string property) => ((IEnumerable)source.GetPropertyValue(property)).Cast<ViewModel>();

        public void Synchronize()
        {
            var character = Read<ViewModel>(Native, "CurrentCharacter");
            if (!ReferenceEquals(character, CurrentCharacter))
            {
                CurrentCharacter = character;
                NameText = Read<string>(character, "HeroNameText");
                NameFontSize = Math.Max(17, Math.Min(30, 460 / Math.Max(1, NameText.Length)));
                LevelText = new TextObject("{=taom_hero_level}Level {LEVEL}")
                    .SetTextVariable("LEVEL", Read<string>(character, "CurrentCharacterLevelLbl")).ToString();
                Tabs.Clear();
                foreach (ViewModel attribute in Items(character, "Attributes")) Tabs.Add(new CharacterAttributeTabVM(this, attribute));
                var inspected = Read<ViewModel>(character, "CurrentSkill");
                string skillId = Read<string>(inspected, "SkillId");
                SelectAttribute(Tabs.First(t => t.SkillIds.Contains(skillId)), false);
                ShowTraits = false;
            }
            var skill = Read<ViewModel>(character, "CurrentSkill");
            bool changed = !ReferenceEquals(skill, CurrentSkill);
            if (changed)
            {
                CurrentSkill = skill;
                var tab = Tabs.First(t => t.SkillIds.Contains(Read<string>(skill, "SkillId")));
                if (tab != SelectedAttribute) SelectAttribute(tab, false);
                DetailTitle = Read<string>(skill, "NameText");
                DetailText = Read<string>(skill, "DescriptionText");
            }
            var perks = Items(skill, "Perks").ToList();
            if (changed || perks.Count != _perkCount || !ReferenceEquals(perks.FirstOrDefault(), _firstPerk))
            {
                Perks.Clear();
                var levels = perks.Select(p => Read<int>(p, "Level")).Distinct().OrderBy(l => l).ToList();
                foreach (ViewModel perk in perks) Perks.Add(new CharacterPerkItemVM(this, perk, levels.IndexOf(Read<int>(perk, "Level"))));
                PerkWidth = Math.Max(984f, levels.Count * 192f + 24f);
                _firstPerk = perks.FirstOrDefault();
                _perkCount = perks.Count;
            }
            foreach (var item in Skills) item.RefreshSelection();
            foreach (var item in Perks) item.RefreshState();
        }

        internal void SelectAttribute(CharacterAttributeTabVM tab, bool selectSkill)
        {
            foreach (var entry in Tabs) entry.IsSelected = entry == tab;
            SelectedAttribute = tab;
            Skills.Clear();
            var character = CurrentCharacter;
            if (character is null) return;
            foreach (ViewModel skill in Items(character, "Skills").Where(s => tab.SkillIds.Contains(Read<string>(s, "SkillId"))))
                Skills.Add(new CharacterSkillItemVM(this, skill));
            if (selectSkill && Skills.Count > 0) InspectSkill(Skills[0].Native);
        }

        internal void InspectSkill(ViewModel skill)
        {
            skill.ExecuteCommand("ExecuteInspect", Array.Empty<object>());
            Synchronize();
        }

        internal void InspectPerk(ViewModel perk)
        {
            var nativePerk = ((PerkVM)perk).Perk;
            DetailTitle = nativePerk.Name.ToString();
            DetailText = nativePerk.Description.ToString();
        }

        private void ExecutePreviousMember() => SwitchMember("ExecuteSelectPreviousItem", -1);
        private void ExecuteNextMember() => SwitchMember("ExecuteSelectNextItem", 1);
        private void SwitchMember(string command, int direction)
        {
            Read<ViewModel>(Native, "CharacterList").ExecuteCommand(command, Array.Empty<object>());
            Synchronize();
        }
        private void ExecuteToggleTraits() { ShowTraits = !ShowTraits; }
        private void ExecuteReset() { Native.ExecuteCommand("ExecuteReset", Array.Empty<object>()); Synchronize(); }
    }

    public class CharacterAttributeTabVM : ViewModel
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

    public class CharacterSkillItemVM : ViewModel
    {
        private readonly CharacterScreenVM _screen;
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public string IconSprite { get; private set; }
        private bool _isSelected;
        [DataSourceProperty] public bool IsSelected { get => _isSelected; private set { _isSelected = value; OnPropertyChangedWithValue(value); } }
        internal void RefreshSelection() { bool value = CharacterScreenVM.Read<bool>(Native, "IsInspected"); if (value != IsSelected) IsSelected = value; }
        internal CharacterSkillItemVM(CharacterScreenVM screen, ViewModel native)
        {
            _screen = screen;
            Native = native;
            string id = CharacterScreenVM.Read<string>(native, "SkillId").ToLowerInvariant();
            IconSprite = "gui_skills_icon_" + (id == "smithing" ? "crafting" : id) + "_small";
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
        private int _state = -1;
        [DataSourceProperty] public int State { get => _state; private set { _state = value; OnPropertyChangedWithValue(value); } }
        internal void RefreshState() { int value = CharacterScreenVM.Read<int>(Native, "PerkState"); if (value != State) State = value; }
        internal CharacterPerkItemVM(CharacterScreenVM screen, ViewModel native, int index)
        {
            _screen = screen;
            Native = native;
            X = 24f + index * 192f;
            Y = CharacterScreenVM.Read<int>(native, "AlternativeType") == 2 ? 104f : 32f;
            ShowLevel = CharacterScreenVM.Read<int>(native, "AlternativeType") != 2;
        }
        private void ExecuteInspect()
        {
            _screen.InspectPerk(Native);
            ((PerkVM)Native).Hint.ExecuteBeginHint();
        }
        private void ExecuteEndInspect()
        {
            ((PerkVM)Native).Hint.ExecuteEndHint();
        }
        private void ExecuteSelect()
        {
            _screen.InspectPerk(Native);
            Native.ExecuteCommand("ExecuteStartSelection", Array.Empty<object>());
            _screen.Synchronize();
        }
    }
}
