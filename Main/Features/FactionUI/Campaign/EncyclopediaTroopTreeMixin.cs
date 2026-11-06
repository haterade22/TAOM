using System;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.Campaign
{
    [ViewModelMixin(nameof(EncyclopediaUnitPageVM.RefreshValues))]
    internal sealed class EncyclopediaTroopTreeMixin : BaseViewModelMixin<EncyclopediaUnitPageVM>
    {
        private TroopTreeLayoutVM _layout;

        public EncyclopediaTroopTreeMixin(EncyclopediaUnitPageVM page) : base(page) { }

        [DataSourceProperty]
        public TroopTreeLayoutVM TroopTreeLayout => ViewModel.Tree == null
            ? null
            : _layout ?? (_layout = new TroopTreeLayoutVM(ViewModel.Tree));

        public override void OnRefresh()
        {
            if (_layout == null) return;
            _layout = null;
            OnPropertyChangedWithValue(TroopTreeLayout, nameof(TroopTreeLayout));
        }
    }

    internal sealed class TroopTreeLayoutVM : ViewModel
    {
        // EncyclopediaUnitVM exposes its tier icon but keeps the underlying troop private.
        private static readonly AccessTools.FieldRef<EncyclopediaUnitVM, CharacterObject> UnitCharacter =
            AccessTools.FieldRefAccess<EncyclopediaUnitVM, CharacterObject>("_character");
        private static readonly string[] RomanTiers = { "0", "I", "II", "III", "IV", "V", "VI", "VII" };

        private const int ViewportWidth = 1288;
        private const int ViewportHeight = 644;
        private const int CardWidth = 174;
        private const int PortraitCenterY = 55;
        private const int HeaderHeight = 64;
        private const int RowPitch = 174;
        private const int LineWeight = 3;

        private readonly int _columnPitch;
        private readonly bool _useTiers;
        private readonly int _firstTier;

        [DataSourceProperty] public int TreeWidth { get; }
        [DataSourceProperty] public int TreeHeight { get; }
        [DataSourceProperty] public MBBindingList<TroopTreeColumnVM> Columns { get; } = new MBBindingList<TroopTreeColumnVM>();
        [DataSourceProperty] public MBBindingList<TroopTreeCardVM> Cards { get; } = new MBBindingList<TroopTreeCardVM>();
        [DataSourceProperty] public MBBindingList<TroopTreeLineVM> Lines { get; } = new MBBindingList<TroopTreeLineVM>();

        public TroopTreeLayoutVM(EncyclopediaTroopTreeNodeVM tree)
        {
            var root = new Branch(tree);
            _useTiers = root.HasIncreasingTiers;
            _firstTier = root.Tier;
            // Some special upgrades stay at the same tier. Give those trees progression columns.
            int columnCount = _useTiers ? root.LastTier - _firstTier + 1 : root.Depth;
            _columnPitch = Math.Max(210, Math.Min(270, ViewportWidth / columnCount));
            TreeWidth = columnCount * _columnPitch;
            TreeHeight = Math.Max(ViewportHeight, HeaderHeight + root.Rows * RowPitch);

            for (int i = 0; i < columnCount; i++)
            {
                int tier = _firstTier + i;
                string number = tier >= 0 && tier < RomanTiers.Length ? RomanTiers[tier] : tier.ToString();
                string label = _useTiers
                    ? new TextObject("{=AkUpgiBl}Tier {TIER_LEVEL}").SetTextVariable("TIER_LEVEL", number).ToString()
                    : new TextObject("{=taom_encyclopedia_upgrade_stage}Stage {STAGE}").SetTextVariable("STAGE", i + 1).ToString();
                Columns.Add(new TroopTreeColumnVM(i * _columnPitch, _columnPitch, label));
            }

            int top = HeaderHeight + (TreeHeight - HeaderHeight - root.Rows * RowPitch) / 2;
            Arrange(root, 0, top);
        }

        private int CardX(Branch branch, int depth) =>
            (_useTiers ? branch.Tier - _firstTier : depth) * _columnPitch + (_columnPitch - CardWidth) / 2;

        private int Arrange(Branch branch, int depth, int top)
        {
            int x = CardX(branch, depth);
            int y = top + (branch.Rows - 1) * RowPitch / 2;
            Cards.Add(new TroopTreeCardVM(x, y, branch.Node));

            int parentCenter = y + PortraitCenterY;
            int parentEdge = x + CardWidth;
            int joint = parentEdge + (_columnPitch - CardWidth) / 2;
            foreach (var child in branch.Children)
            {
                int childCenter = Arrange(child, depth + 1, top);
                int childX = CardX(child, depth + 1);
                Lines.Add(new TroopTreeLineVM(parentEdge, parentCenter, joint - parentEdge + LineWeight, LineWeight, child.OnSelectedRoute));
                Lines.Add(new TroopTreeLineVM(joint, Math.Min(parentCenter, childCenter), LineWeight, Math.Abs(childCenter - parentCenter) + LineWeight, child.OnSelectedRoute));
                Lines.Add(new TroopTreeLineVM(joint, childCenter, childX - joint, LineWeight, child.OnSelectedRoute));
                top += child.Rows * RowPitch;
            }
            return parentCenter;
        }

        private sealed class Branch
        {
            public EncyclopediaTroopTreeNodeVM Node { get; }
            public Branch[] Children { get; }
            public int Rows { get; }
            public int Tier { get; }
            public int LastTier { get; }
            public int Depth { get; }
            public bool OnSelectedRoute { get; }
            public bool HasIncreasingTiers { get; }

            public Branch(EncyclopediaTroopTreeNodeVM node)
            {
                Node = node;
                Tier = UnitCharacter(node.Unit).Tier;
                Children = node.Branch.Select(child => new Branch(child)).ToArray();
                Rows = Math.Max(1, Children.Sum(child => child.Rows));
                LastTier = Children.Aggregate(Tier, (last, child) => Math.Max(last, child.LastTier));
                Depth = Children.Aggregate(1, (depth, child) => Math.Max(depth, child.Depth + 1));
                OnSelectedRoute = node.IsActiveUnit || Children.Any(child => child.OnSelectedRoute);
                HasIncreasingTiers = Children.All(child => child.Tier > Tier && child.HasIncreasingTiers);
            }
        }
    }

    internal sealed class TroopTreeColumnVM : ViewModel
    {
        [DataSourceProperty] public int X { get; }
        [DataSourceProperty] public int Width { get; }
        [DataSourceProperty] public string Label { get; }

        public TroopTreeColumnVM(int x, int width, string label)
        {
            X = x;
            Width = width;
            Label = label;
        }
    }

    internal sealed class TroopTreeCardVM : ViewModel
    {
        [DataSourceProperty] public int X { get; }
        [DataSourceProperty] public int Y { get; }
        [DataSourceProperty] public EncyclopediaTroopTreeNodeVM Node { get; }
        [DataSourceProperty] public string CardName { get; }

        public TroopTreeCardVM(int x, int y, EncyclopediaTroopTreeNodeVM node)
        {
            X = x;
            Y = y;
            Node = node;
            CardName = node.Unit.NameText;
        }
    }

    internal sealed class TroopTreeLineVM : ViewModel
    {
        [DataSourceProperty] public int X { get; }
        [DataSourceProperty] public int Y { get; }
        [DataSourceProperty] public int Width { get; }
        [DataSourceProperty] public int Height { get; }
        [DataSourceProperty] public bool IsHighlighted { get; }

        public TroopTreeLineVM(int x, int y, int width, int height, bool isHighlighted)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IsHighlighted = isHighlighted;
        }
    }
}
