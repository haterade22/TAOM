using System;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TAOM.Features.LinesOfDescent;

namespace TAOM.Features.FactionUI.Campaign
{
    [ViewModelMixin("Refresh")]
    internal sealed class EncyclopediaFamilyTreeMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {
        private InlineFamilyTreeVM _tree;
        private bool _requested;

        public EncyclopediaFamilyTreeMixin(EncyclopediaHeroPageVM page) : base(page) { }

        [DataSourceProperty]
        public InlineFamilyTreeVM InlineFamilyTree
        {
            get
            {
                if (!_requested)
                {
                    _requested = true;
                    _tree = BuildTree();
                }
                return _tree;
            }
        }

        [DataSourceProperty]
        public bool HasInlineFamilyTree => InlineFamilyTree != null;

        [DataSourceProperty]
        public string HeroLevelText => ViewModel?.Obj is Hero hero
            ? new TaleWorlds.Localization.TextObject("{=taom_hero_level}Level {LEVEL}").SetTextVariable("LEVEL", hero.Level).ToString()
            : string.Empty;

        public override void OnRefresh()
        {
            if (!_requested) return;

            var previous = _tree;
            _tree = BuildTree();
            OnPropertyChangedWithValue(_tree, nameof(InlineFamilyTree));
            OnPropertyChangedWithValue(_tree != null, nameof(HasInlineFamilyTree));
            previous?.OnFinalize();
        }

        public override void OnFinalize() => _tree?.OnFinalize();

        private InlineFamilyTreeVM BuildTree()
        {
            if (!(ViewModel?.Obj is Hero hero) || ViewModel.Family.Count == 0)
                return null;

            try
            {
                var client = AppDomain.CurrentDomain.GetAssemblies()
                    .First(assembly => assembly.GetName().Name == "TAOM.Client");
                var nodeType = client.GetType("TAOM.Features.LinesOfDescent.UI.FamilyTreeNodeVM", throwOnError: true);
                var result = TAOM.IoC.Resolve<IFamilyTreeService>().Build(hero.StringId);
                // TAOM owns the recursive node VM and its localized relationship labels.
                if (result == null) return null;

                var nativeTree = (ViewModel)Activator.CreateInstance(nodeType, result.Root);
                try
                {
                    return new InlineFamilyTreeVM(nativeTree);
                }
                catch
                {
                    nativeTree.OnFinalize();
                    throw;
                }
            }
            catch (Exception error)
            {
                CampaignPresentation.LogError("Inline encyclopedia family tree could not be built.", error);
                return null;
            }
        }
    }
}
