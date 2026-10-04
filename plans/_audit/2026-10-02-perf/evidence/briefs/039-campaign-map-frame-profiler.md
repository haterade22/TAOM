Plan 039: attribute campaign-map frame time and allocation to TAOM's per-frame map code, the way plan 028
does for missions, so the slow fast-forward (7 to 8 fps on the desktop with Campaign.RealTick at 20 ms,
docs/migration/v1.5.2-impact.md:146-147) can be split into TAOM's share and the engine's.

DEPENDS ON: plan 028's profiler core (the per-type accumulator, the timing and allocation counters, the
line format conventions and the MCM toggle). Build on the tip of plan 028's branch.

WHAT TAOM RUNS PER MAP FRAME (audit 2026-10-02 at dffdf879, E3; re-read each):
- CampaignEvents.TickEvent listeners (8): FieldCommission (a 7-read MCM snapshot per frame,
  FieldCommissionBehavior.cs:51, :105-117), FieldCamp (GetMapView list scan, FieldCampCampaignBehavior.cs:53),
  Refuge, RealmBorders EnsureMapView (RealmBordersCampaignBehavior.cs:85-91), SupplyLines, Enlistment Pump,
  InventorySearch, Messenger cleanup. Vanilla dispatches TickEvent from Campaign.Tick (find the dispatch
  loop in the v1.5.3 decompile: an MbEvent<float> Invoke over its listeners).
- MapView overrides (RealmBordersMapView.OnMapScreenUpdate, others: grep `: MapView`), dispatched by
  MapScreen per frame.
- SubModule.OnApplicationTick (SubModule.cs:2177-2196): TimeAcceleration input checks and the FactionUI
  ticker every frame.
- Harmony patches on per-frame map methods (Campaign.RealTick prefix and postfix of Patch89, MapScreen
  OnFrameTick postfixes of Patch36 and Patch89, MapState.OnTick of Patch43, SceneView ready checks).
DESIGN: time each TickEvent listener (by its declaring type), each MapView's per-frame override, and
OnApplicationTick's TAOM work, plus Campaign.RealTick total and MapScreen.OnFrameTick total, every 5 s of
wall clock while the map is the top screen: `[MapProfile] t=+<s>s frames=<n> wallMs=<x> realTickMs=<x>
mapScreenMs=<x> otherMs=<x> allocKB=<x|na> speed=<Stop|Play|FF|FF2|FF3> parties=<n> top=<Type>:<ms>/<calls>/<maxMs>/<KB>,...`.
How to time TickEvent listeners: wrap the MbEvent invocation (decompile how CampaignEvents invokes
listeners; a transpiler on that dispatch loop like plan 028's, or a prefix/postfix pair on each TAOM
listener method, whichever is less fragile; the plan argues the choice). Same default-off toggle family
as plan 028; patches applied only when the toggle is on at game start.
TESTS: the accumulator reuse from 028, the line format (literal pin; plan 029's parser gains the new tag
in a follow-up or here), the speed classification, the dispatch rewrite on synthetic IL if a transpiler is
chosen, a binding test against the installed engine.
OUT OF SCOPE: any fix; the engine's own map rendering.
STOP conditions to include: the TickEvent dispatch cannot be wrapped without changing invocation order or
exception behaviour.
