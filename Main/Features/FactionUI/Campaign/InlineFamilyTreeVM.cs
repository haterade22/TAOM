using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.Campaign
{
    internal sealed class InlineFamilyTreeVM : ViewModel
    {
        private const int CardWidth = 148;
        private const int CardHeight = 146;
        private const int RowPitch = 168;
        private const int BranchGap = 76;
        private const int Padding = 12;
        private const int PortraitCenter = 46;
        private const int PortraitInset = 32;

        private readonly ViewModel _nativeTree;

        [DataSourceProperty] public MBBindingList<FamilyTreeCardPlacementVM> Cards { get; } = new MBBindingList<FamilyTreeCardPlacementVM>();
        [DataSourceProperty] public MBBindingList<FamilyTreeLineVM> Lines { get; } = new MBBindingList<FamilyTreeLineVM>();
        [DataSourceProperty] public int TreeWidth { get; private set; }
        [DataSourceProperty] public int TreeHeight { get; private set; }

        public InlineFamilyTreeVM(ViewModel nativeTree)
        {
            _nativeTree = nativeTree;
            var root = new Branch(nativeTree);
            Measure(root);
            TreeWidth = 2 * Padding + CardWidth + (root.Depth - 1) * (CardWidth + BranchGap);
            TreeHeight = root.Rows * RowPitch + 2 * Padding;
            Arrange(root, Padding, Padding);
        }

        public override void OnFinalize() => _nativeTree.OnFinalize();

        private static void Measure(Branch branch)
        {
            foreach (var child in branch.Children)
                Measure(child);

            branch.Depth = 1 + branch.Children.Select(child => child.Depth).DefaultIfEmpty().Max();
            branch.ChildRows = branch.Children.Sum(child => child.Rows);
            branch.Rows = Math.Max(branch.MemberRows, branch.ChildRows);
        }

        private int Arrange(Branch branch, int x, int top)
        {
            int y = top + (branch.Rows - branch.MemberRows) * RowPitch / 2;
            if (branch.Members.Length == 0)
                Cards.Add(new FamilyTreeCardPlacementVM(x, y, null, branch.MoreText));

            int spouseX = x + CardWidth / 2;
            for (int index = 0; index < branch.Members.Length; index++)
            {
                int cardY = y + index * RowPitch;
                Cards.Add(new FamilyTreeCardPlacementVM(x, cardY, branch.Members[index], ""));
                if (index > 0)
                {
                    // Keep the marriage connector in the gap below the relationship label.
                    Lines.Add(new FamilyTreeLineVM(spouseX, cardY - RowPitch + CardHeight, 1,
                        RowPitch - CardHeight + 4));
                }
            }

            int incomingY = y + PortraitCenter;
            if (branch.Children.Length == 0)
                return incomingY;

            int nextX = x + CardWidth + BranchGap;
            int joint = nextX - BranchGap / 2;
            int outgoingX = x + CardWidth - PortraitInset;
            int outgoingY = incomingY;
            if (branch.Members.Length > 1)
            {
                outgoingX = spouseX;
                outgoingY = y + CardHeight + (RowPitch - CardHeight) / 2;
            }

            int firstCenter = outgoingY;
            int lastCenter = outgoingY;
            int childTop = top + (branch.Rows - branch.ChildRows) * RowPitch / 2;
            foreach (var child in branch.Children)
            {
                int childCenter = Arrange(child, nextX, childTop);
                firstCenter = Math.Min(firstCenter, childCenter);
                lastCenter = Math.Max(lastCenter, childCenter);
                Lines.Add(new FamilyTreeLineVM(joint, childCenter, nextX + PortraitInset - joint, 1));
                childTop += child.Rows * RowPitch;
            }

            Lines.Add(new FamilyTreeLineVM(outgoingX, outgoingY, joint - outgoingX + 1, 1));
            Lines.Add(new FamilyTreeLineVM(joint, firstCenter, 1, lastCenter - firstCenter + 1));
            return incomingY;
        }

        private sealed class Branch
        {
            public ViewModel[] Members { get; }
            public Branch[] Children { get; }
            public string MoreText { get; }
            public int Rows { get; set; }
            public int ChildRows { get; set; }
            public int Depth { get; set; }
            public int MemberRows => Math.Max(1, Members.Length);

            public Branch(ViewModel node)
            {
                Type type = node.GetType();
                Members = ReadList(type.GetProperty("Members"), node).ToArray();
                Children = ReadList(type.GetProperty("Branch"), node).Select(child => new Branch(child)).ToArray();
                MoreText = (string)type.GetProperty("MoreText").GetValue(node);
            }

            private static IEnumerable<ViewModel> ReadList(PropertyInfo property, ViewModel node)
                => ((IEnumerable)property.GetValue(node)).Cast<ViewModel>();
        }
    }

    internal sealed class FamilyTreeCardPlacementVM : ViewModel
    {
        [DataSourceProperty] public int X { get; }
        [DataSourceProperty] public int Y { get; }
        [DataSourceProperty] public ViewModel Member { get; }
        [DataSourceProperty] public string MoreText { get; }
        [DataSourceProperty] public bool HasMember => Member != null;

        public FamilyTreeCardPlacementVM(int x, int y, ViewModel member, string moreText)
        {
            X = x;
            Y = y;
            Member = member;
            MoreText = moreText;
        }
    }

    internal sealed class FamilyTreeLineVM : ViewModel
    {
        [DataSourceProperty] public int X { get; }
        [DataSourceProperty] public int Y { get; }
        [DataSourceProperty] public int Width { get; }
        [DataSourceProperty] public int Height { get; }

        public FamilyTreeLineVM(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
