using System;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Features.Elephant;

/// <summary>
/// Tracks the war-elephant every frame and repositions the howdah seat entity so seated archers
/// remain on the elephant's back. Behavioural port of ADOD_Beasts's howdah machine / howdah-object
/// types (docs/reference/provenance-register.md). Not clean-room: the source was read while writing this.
///
/// Instantiated at runtime by ElephantMissionBehavior.OnAgentBuild when the mahout's HorseHarness is one
/// HowdahHarness.GetsPlatform accepts (the howdah harness, which also gets a crew, or the plain armour). Field refs are set
/// immediately after GameEntity.Instantiate returns — OnInit fires with nulls and OnTick propagates
/// them to child TaomHowdahStandingPoint instances once set.
///
/// Vanilla detachment never assigns anyone here: GetDetachmentWeightAux returns 0. The seats are filled by
/// HowdahCrewSpawner, which spawns the crew straight onto them.
///
/// Diagnostics (#627): <see cref="LogTag"/> ("[Howdah#n]", set at bind) prefixes every line, and a
/// <see cref="HowdahDiagnosticsReporter"/> writes the layout, status and summary lines behind the MCM toggle.
/// </summary>
public class TaomHowdahMachine : UsableMachine
{
    public Agent elephantAgent;
    public Agent elephantRider;

    /// <summary>Per-mission howdah serial for the log, e.g. "[Howdah#2]". Agent indices recycle, so not an index.</summary>
    public string LogTag = "[Howdah]";

    private bool _seatsInitialized;
    private bool _firstMachineTickLogged;
    private IModLogger _logger;
    private HowdahDiagnosticsReporter _reporter;

    public override TextObject GetActionTextForStandingPoint(UsableMissionObject usableGameObject)
        => new TextObject("Enter");

    public override TextObject GetDescriptionText(WeakGameEntity gameEntity)
        => new TextObject("Howdah Seat");

    // 0 = no detachment; seats are pre-filled at spawn, dead crew stay empty for the battle.
    protected override float GetDetachmentWeightAux(BattleSideEnum side) => 0f;

    protected override void OnInit()
    {
        base.OnInit();
        _logger = IoC.Resolve<IModLogger>();
        _reporter = new HowdahDiagnosticsReporter(_logger, IoC.Resolve<IHowdahDiagnosticsSettingsProvider>());
        _logger?.LogInfo($"{LogTag} Machine OnInit — entity={GameEntity.Name} standingPoints={StandingPoints.Count}");
        PropagateRefsToSeats();
    }

    protected override void OnTick(float dt)
    {
        // Mission is live here — the elephant's skeleton/visuals are built and safe to query.
        // The build-time RepositionToElephant call (from ElephantMissionBehavior.TryInstantiateHowdah,
        // during OnAgentBuild) runs with this false, so it never touches the skeleton (native AV on load).
        _liveTicking = true;

        if (!_seatsInitialized) PropagateRefsToSeats();

        if (!_firstMachineTickLogged)
        {
            _firstMachineTickLogged = true;
            int seated = 0;
            foreach (StandingPoint sp in StandingPoints)
                if (sp is TaomHowdahStandingPoint s && s.MovingAgent != null) seated++;
            _logger?.LogInfo(
                $"{LogTag} Machine OnTick FIRST FIRE — elephant={elephantAgent?.Name ?? "null"} " +
                $"seatsInitialized={_seatsInitialized} seatedAgents={seated}/{StandingPoints.Count}");
        }

        // Belt-and-suspenders: if mission ended, release all seats from the machine side
        // so the end-battle sequence isn't blocked by seated agents. This fires even if a
        // TaomHowdahStandingPoint.OnTick hasn't run yet for that seat.
        if (Mission.Current != null && Mission.Current.MissionEnded)
        {
            ReleaseAllSeats();
            return;
        }

        if (elephantAgent == null) return;
        // A dead elephant's handle keeps reading its recycled engine slot, so the platform would
        // follow whatever agent inherits the index (#592, #595). Drop everything the moment the
        // elephant is no longer alive and the occupant of its own slot.
        if (!elephantAgent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(elephantAgent))
        {
            _logger?.LogInfo($"{LogTag} Elephant '{elephantAgent.Name}' gone: releasing seats and clearing refs");
            // Release first: a dead elephant that still owns its slot hands the seats the corpse
            // position to drop to, and ReleaseAgent itself rejects a recycled handle.
            ReleaseAllSeats();
            ClearSeatRefs();
            elephantAgent = null;
            elephantRider = null;
            return;
        }
        _reporter?.BeforeReposition(dt, GameEntity, _lastAnchor, _hasAnchor);
        RepositionToElephant();
        _reporter?.AfterReposition(LogTag, GameEntity, elephantAgent, StandingPoints, _placement, DeckNavMeshName, DeckNavMeshIdStart);

        if (elephantRider?.MountAgent == null)
        {
            if (elephantRider != null)
                _logger?.LogInfo($"{LogTag} Rider dismounted — clearing seat refs");
            elephantRider = null;
        }
    }

    private void ClearSeatRefs()
    {
        foreach (StandingPoint sp in StandingPoints)
        {
            if (sp is TaomHowdahStandingPoint seat)
            {
                seat.elephantAgent = null;
                seat.elephantRider = null;
            }
        }
        _seatsInitialized = false;
    }

    private void ReleaseAllSeats()
    {
        foreach (StandingPoint sp in StandingPoints)
        {
            if (sp is TaomHowdahStandingPoint seat && seat.MovingAgent != null)
            {
                _logger?.LogInfo($"{LogTag} MissionEnded — force-releasing {seat.MovingAgent.Name} from seat (machine tick)");
                seat.ForceRelease();
            }
        }
    }

    /// <summary>
    /// What the engine made of this platform's NavMeshPrefabName (#627). MissionObject.OnInit calls
    /// AttachDynamicNavmeshToEntity, which imports the named prefab from any module's NavMeshPrefabs folder and
    /// attaches its faces to this entity, and nothing re-attaches them as the entity moves: the faces follow it.
    /// Empty name means no deck navmesh, which is what the crew's nav=0 reads. The pair is logged once per howdah.
    /// </summary>
    internal string DeckNavMeshName => NavMeshPrefabName ?? "";
    internal int DeckNavMeshIdStart => DynamicNavmeshIdStart;

    public override TickRequirement GetTickRequirement()
        => TickRequirement.Tick | base.GetTickRequirement();

    /// <summary>
    /// Empties every seat BEFORE vanilla deactivates it, or the game hangs (#627, 2026-09-19).
    /// <c>UsableMachine.OnMissionEnded</c> sets <c>IsDeactivated = true</c> on each standing point, and that setter
    /// spins <c>while (HasAIMovingTo) MovingAgent.StopUsingGameObject();</c> (UsableMissionObject.cs:129-133). Our
    /// seats register their archer with <c>AddMovingAgent</c> alone and never call <c>AIMoveToGameObjectEnable</c>,
    /// the native path that would send agents climbing after a seat the navmesh cannot reach, so
    /// <c>StopUsingGameObject</c> has nothing to unwind and <c>MovingAgent</c> never clears: the loop never exits.
    /// It hung twice on 2026-09-19, both times as a battle ended with archers still aboard, mission tick dead at
    /// over 250 fps while the memory sampler kept writing. The hang dump named this exact stack. Releasing first
    /// leaves <c>MovingAgent</c> null, so vanilla's loop has nothing to spin on.
    /// </summary>
    public override void OnMissionEnded()
    {
        ReleaseAllSeats();
        base.OnMissionEnded();
    }

    /// <summary>
    /// The second copy of the same hang, closed before anyone can reach it (#627, engine review E3).
    /// <c>UsableMachine.Disable</c> also ends by setting <c>IsDeactivated</c> on every standing point, and its one
    /// <c>StopUsingGameObject</c> per seat cannot clear a <c>MovingAgent</c> registered the way this seat registers
    /// one, so it would spin exactly as mission end did. Nothing reaches it today (it needs a
    /// <c>DestructableComponent</c> on the howdah entity, which the prefab has none of), which is precisely why it is
    /// worth three lines now rather than another hang dump later. <c>UsableMachine.Deactivate</c> has the same shape
    /// and is NOT virtual: never route a howdah through a SiegeWeapon-style controller without re-reading this.
    /// </summary>
    public override void Disable()
    {
        ReleaseAllSeats();
        base.Disable();
    }

    public override void OnEndMission()
    {
        _logger?.LogInfo($"{LogTag} Machine OnEndMission fired — releasing seats then clearing refs");
        _reporter?.LogSummary(LogTag, StandingPoints.Count);
        // Release all seated agents first so TeleportToPosition lands them on the navmesh
        // before the end-battle sequencer tries to move them to exit positions.
        ReleaseAllSeats();
        elephantAgent = null;
        elephantRider = null;
        base.OnEndMission();
    }

    private void PropagateRefsToSeats()
    {
        // Nothing to hand the seats before the bind or after the elephant is gone (ClearSeatRefs has nulled them);
        // without this, the per-seat tag string below would allocate every frame for the rest of the mission.
        if (elephantAgent == null) return;
        bool wasInitialized = _seatsInitialized;
        int propagated = 0;
        for (int i = 0; i < StandingPoints.Count; i++)
        {
            if (StandingPoints[i] is TaomHowdahStandingPoint seat && seat.elephantAgent == null)
            {
                seat.elephantAgent = elephantAgent;
                seat.elephantRider = elephantRider;
                seat.LogTag = $"{LogTag} seat{i}";
                propagated++;
            }
        }
        _seatsInitialized = elephantAgent != null;
        if (_seatsInitialized && !wasInitialized)
            _logger?.LogInfo(
                $"{LogTag} Seat refs propagated — elephant={elephantAgent?.Name} " +
                $"totalSeats={StandingPoints.Count} propagated={propagated}");
    }

    // Anchor bone on the elephant's spine — the engine's own rider_sit_bone (lotr_monster_elephant.xml).
    // _liveTicking guards every skeleton access: it is false during the build-time call (OnAgentBuild),
    // when the elephant's skeleton is not yet built and native bone queries access-violate the load.
    private const string AnchorBoneName = "Spine1_05";
    private bool _liveTicking;
    private bool _boneResolved;
    private bool _loggedBoneError;
    private bool _loggedNoSkeleton;
    private bool _loggedBoneOutOfRange;
    private sbyte _anchorBoneIndex = -1;

    // Where the last RepositionToElephant put the root, and by which path: the diagnostics compare the next frame's
    // position against it, and the status line names the path.
    private Vec3 _lastAnchor;
    private bool _hasAnchor;
    private string _placement = "none";

    // DEFERRED (2026-06-10): spine bone-tracking is a CONFIRMED slide source — the howdah's bo_ floor tracked to the
    // spine bone sits inside the elephant's collision capsule and the physics solver shoves the elephant. Disabled
    // until the floor-collision fix; flip true (with that fix) to re-enable. static readonly (NOT const) so the
    // _liveTicking read in the RepositionToElephant gate is never constant-folded away. Internal so the diagnostics
    // config banner can report it.
    internal static readonly bool BoneTrackingEnabled = false;

    internal void RepositionToElephant()
    {
        // DEFERRED (2026-06-10): bone-tracking is a CONFIRMED slide source and is disabled for now.
        // It SetFrames the howdah's bo_ physics floor AT the spine bone — inside the elephant's collision capsule —
        // so the physics solver shoves the elephant ("slide"). Confirmed by the isolation ladder: the bone-test
        // build (bone off + crew off) did NOT slide; the control build (bone on + crew off) DID. Fixed-offset
        // (feet + 3.2 Z) keeps the floor above the capsule, so the empty howdah no longer perturbs the elephant.
        // Re-enable ONLY together with the physics-contact fix (drop the floor's collision, or raise the bone frame
        // to clear the capsule): flip BoneTrackingEnabled true. The gate stays wired (and BoneTrackingEnabled is
        // false) so the _liveTicking load-safety — bone APIs only once the mission is live; the OnAgentBuild-time
        // call has it false and takes the fixed-offset path — and the bone path are both preserved.
        // See docs/features/elephant.md → "Slide root-cause isolation".
        if (_liveTicking && BoneTrackingEnabled && TryRepositionToBone())
        {
            _placement = "bone";
            return;
        }
        RepositionToFixedOffset();
        _placement = "fixed-offset";
    }

    // Mirrors the safe, index-based, bounds-checked, null-guarded bone idiom in
    // AdvancedCombat/BoneCheck.cs — never the by-name frame API, which faults on a missing name.
    private bool TryRepositionToBone()
    {
        try
        {
            var visuals = elephantAgent?.AgentVisuals;
            Skeleton skel = visuals?.GetSkeleton();
            if (skel == null)
            {
                if (!_loggedNoSkeleton)
                {
                    _loggedNoSkeleton = true;
                    _logger?.LogInfo($"{LogTag} Bone tracking: elephant has no skeleton yet; using the fixed offset until it does");
                }
                return false;
            }

            int boneCount = skel.GetBoneCount();
            if (!_boneResolved)
            {
                _boneResolved = true;
                _anchorBoneIndex = ResolveBoneIndex(skel, AnchorBoneName, boneCount);
                _logger?.LogInfo(
                    $"{LogTag} Resolved bone '{AnchorBoneName}' -> index {_anchorBoneIndex} (boneCount={boneCount})");
            }
            if (_anchorBoneIndex < 0 || _anchorBoneIndex >= boneCount)
            {
                if (!_loggedBoneOutOfRange)
                {
                    _loggedBoneOutOfRange = true;
                    _logger?.LogWarning(
                        $"{LogTag} Bone tracking: '{AnchorBoneName}' index {_anchorBoneIndex} is not in 0..{boneCount - 1}; using the fixed offset");
                }
                return false;
            }

            MatrixFrame boneLocal = skel.GetBoneEntitialFrameWithIndex(_anchorBoneIndex);
            MatrixFrame world = visuals.GetGlobalFrame().TransformToParent(in boneLocal);
            GameEntity.SetFrame(ref world);
            _lastAnchor = world.origin;
            _hasAnchor = true;
            return true;
        }
        catch (Exception ex)
        {
            if (!_loggedBoneError)
            {
                _loggedBoneError = true;
                _logger?.LogError($"{LogTag} Bone reposition failed ({ex.GetType().Name}: {ex.Message}); using fixed offset");
            }
            return false;
        }
    }

    // Enumerate bones by index and match by name — validates existence without risking a native
    // fault on a bad name. Engine bone names carry a leading space + mixed case (e.g. " Spine1_05").
    internal static sbyte ResolveBoneIndex(Skeleton skel, string name, int boneCount)
    {
        for (sbyte i = 0; i < boneCount; i++)
        {
            string boneName = skel.GetBoneName(i);
            if (boneName != null && boneName.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    // Fixed height above the elephant's navmesh feet — safe at build time and the fallback whenever
    // the bone path is unavailable (skeleton not ready, bone missing, or a native query failed).
    private void RepositionToFixedOffset()
    {
        Vec3 anchor = elephantAgent.Position + new Vec3(0f, 0f, ElephantConfig.HowdahHeightAboveGround);
        // A native position that came back NaN would go straight into SetFrame and take the whole platform with it.
        if (!HowdahSeatMotion.IsPlaceable(anchor.x, anchor.y, anchor.z)) return;
        MatrixFrame next = elephantAgent.Frame;
        next.origin = anchor;
        GameEntity.SetFrame(ref next);
        _lastAnchor = anchor;
        _hasAnchor = true;
    }
}
