using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>One claimant: the native hero card (portrait, name, link, hint) and the kind of claim under it.</summary>
internal sealed class ClaimantVM : ViewModel
{
    public ClaimantVM(Hero hero, ClaimKind kind)
    {
        Hero = new HeroVM(hero, true);
        KindText = new TextObject(kind switch
        {
            ClaimKind.Conquest => "{=realms_claim_conquest}Conquest claim",
            ClaimKind.Fabricated => "{=realms_claim_fabricated}Fabricated claim",
            _ => "{=realms_claim_ancestral}Ancestral claim",
        }).ToString();
    }

    [DataSourceProperty] public HeroVM Hero { get; }
    [DataSourceProperty] public string KindText { get; }

    public override void OnFinalize()
    {
        base.OnFinalize();
        Hero.OnFinalize();
    }
}

/// <summary>The data source of RealmsClaimantsPopup.xml: "Claims on" the title, the claimants and the close command.</summary>
internal sealed class ClaimantsPopupVM : ViewModel
{
    private readonly Action _close;

    public ClaimantsPopupVM(RealmsCampaign b, TitleDefinition title, Action close)
    {
        _close = close;
        var text = new TextObject("{=realms_claims_on}Claims on {TITLE}");
        text.SetTextVariable("TITLE", TitleNames.Of(title, b));
        Title = text.ToString();
        Claimants = new MBBindingList<ClaimantVM>();
        foreach (var claim in b.Service.Ledger.ClaimsOn(title.Id).OrderBy(c => c.CampaignDay))
        {
            var hero = TaleWorlds.CampaignSystem.Hero.Find(claim.HeroId);
            if (hero != null)
                Claimants.Add(new ClaimantVM(hero, claim.Kind));
        }
    }

    [DataSourceProperty] public string Title { get; }
    [DataSourceProperty] public MBBindingList<ClaimantVM> Claimants { get; }

    public void ExecuteClose() => _close();

    public override void OnFinalize()
    {
        base.OnFinalize();
        foreach (var claimant in new List<ClaimantVM>(Claimants))
            claimant.OnFinalize();
    }
}
