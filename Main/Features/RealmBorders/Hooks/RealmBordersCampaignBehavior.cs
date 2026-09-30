using System;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ModuleManager;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.RealmBorders.UI;

namespace TAOM.Features.RealmBorders.Hooks;

/// <summary>
/// The realm borders' campaign entry point, events only. Any change of who holds what, who fights or
/// allies with whom, or which kingdom a clan serves marks the borders for a look on the next map frame
/// (the service skips the repaint when nothing on screen would change); the hour drives the crossing
/// notice; the tick attaches the map view that draws, since MapScreen.Instance does not exist at
/// session launch and the screen is rebuilt on every load (the FieldCamp overlay's pattern), whose
/// teardown also clears the borders. Nothing is saved.
/// Registers nothing on a dedicated server (no map screen, and the map-view code is client-only) or
/// when the Kingdom Borders mod is loaded, so the map never carries two sets of lines.
/// </summary>
public sealed class RealmBordersCampaignBehavior : CampaignBehaviorBase
{
    /// <summary>The Kingdom Borders mod's module id, from its SubModule.xml (provenance register row).</summary>
    public const string KingdomBordersModuleId = "KingdomBorders";

    private readonly RealmBorderService _borders;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public RealmBordersCampaignBehavior(RealmBorderService borders, IDedicatedServerProvider server, IModLogger logger)
    {
        _borders = borders ?? throw new ArgumentNullException(nameof(borders));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void RegisterEvents()
    {
        if (!ShouldDraw())
            return;
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _borders.OnSessionStart());
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, (_, _, _, _, _, _) => _borders.MarkDirty());
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, (_, _, _, _, _) => _borders.MarkDirty());
        CampaignEvents.KingdomCreatedEvent.AddNonSerializedListener(this, _ => _borders.MarkDirty());
        CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, _ => _borders.MarkDirty());
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, (_, _, _) => _borders.MarkDirty());
        CampaignEvents.MakePeace.AddNonSerializedListener(this, (_, _, _) => _borders.MarkDirty());
        CampaignEvents.OnAllianceStartedEvent.AddNonSerializedListener(this, (_, _) => _borders.MarkDirty());
        CampaignEvents.OnAllianceEndedEvent.AddNonSerializedListener(this, (_, _) => _borders.MarkDirty());
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, _borders.OnHourlyTick);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, _ => EnsureMapView());
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private bool ShouldDraw()
    {
        if (_server.IsDedicatedServer)
            return false;
        if (!KingdomBordersActive())
            return true;
        _logger.LogInfo("[RealmBorders] the Kingdom Borders mod is loaded; TAOM's realm borders stand aside so the map carries one set of lines");
        return false;
    }

    private bool KingdomBordersActive()
    {
        try
        {
            return ModuleHelper.GetActiveModules().Any(m => string.Equals(m.Id, KingdomBordersModuleId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            // An unreadable module list must not take the borders away, but say so: with the Kingdom
            // Borders mod loaded, the player would see two sets of lines.
            _logger.LogWarning($"[RealmBorders] could not read the module list ({ex.GetType().Name}: {ex.Message}); "
                + "drawing TAOM's borders without checking for the Kingdom Borders mod");
            return false;
        }
    }

    private static void EnsureMapView()
    {
        var mapScreen = MapScreen.Instance;
        if (mapScreen == null || mapScreen.GetMapView<RealmBordersMapView>() != null)
            return;
        mapScreen.AddMapView<RealmBordersMapView>();
    }
}
