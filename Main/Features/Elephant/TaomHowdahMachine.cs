using System;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant;

/// <summary>
/// Tracks the war-elephant every frame and repositions the howdah seat entity so seated archers
/// remain on the elephant's back. Clean-room port of ADOD_Beasts' ADODHowdah / ADODHowdahObject.
///
/// Instantiated at runtime by ElephantMissionBehavior.OnAgentBuild when the mahout's HorseHarness
/// item StringId matches ElephantConfig.HarnessStringId ("sk_elephant_armor_a"). Field refs are set
/// immediately after GameEntity.Instantiate returns — OnInit fires with nulls and OnTick propagates
/// them to child TaomHowdahStandingPoint instances once set.
///
/// Vanilla detachment assigns nearby troops automatically because GetDetachmentWeightAux returns 1.
/// </summary>
public class TaomHowdahMachine : UsableMachine
{
    public Agent elephantAgent;
    public Agent elephantRider;

    private bool _seatsInitialized;
    private bool _firstMachineTickLogged;
    private IModLogger _logger;

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
        _logger?.LogInfo($"[Howdah] Machine OnInit — entity={GameEntity.Name} standingPoints={StandingPoints.Count}");
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
                $"[Howdah] Machine OnTick FIRST FIRE — elephant={elephantAgent?.Name ?? "null"} " +
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
        RepositionToElephant();

        if (elephantRider?.MountAgent == null)
        {
            if (elephantRider != null)
                _logger?.LogInfo("[Howdah] Rider dismounted — clearing seat refs");
            elephantRider = null;
        }
    }

    private void ReleaseAllSeats()
    {
        foreach (StandingPoint sp in StandingPoints)
        {
            if (sp is TaomHowdahStandingPoint seat && seat.MovingAgent != null)
            {
                _logger?.LogInfo($"[Howdah] MissionEnded — force-releasing {seat.MovingAgent.Name} from seat (machine tick)");
                seat.ForceRelease();
            }
        }
    }

    public override TickRequirement GetTickRequirement()
        => TickRequirement.Tick | base.GetTickRequirement();

    public override void OnEndMission()
    {
        _logger?.LogInfo("[Howdah] Machine OnEndMission fired — releasing seats then clearing refs");
        // Release all seated agents first so TeleportToPosition lands them on the navmesh
        // before the end-battle sequencer tries to move them to exit positions.
        ReleaseAllSeats();
        elephantAgent = null;
        elephantRider = null;
        base.OnEndMission();
    }

    private void PropagateRefsToSeats()
    {
        bool wasInitialized = _seatsInitialized;
        int propagated = 0;
        foreach (StandingPoint sp in StandingPoints)
        {
            if (sp is TaomHowdahStandingPoint seat && seat.elephantAgent == null)
            {
                seat.elephantAgent = elephantAgent;
                seat.elephantRider = elephantRider;
                propagated++;
            }
        }
        _seatsInitialized = elephantAgent != null;
        if (_seatsInitialized && !wasInitialized)
            _logger?.LogInfo(
                $"[Howdah] Seat refs propagated — elephant={elephantAgent?.Name} " +
                $"totalSeats={StandingPoints.Count} propagated={propagated}");
    }

    // Anchor bone on the elephant's spine — the engine's own rider_sit_bone (lotr_monster_elephant.xml).
    // _liveTicking guards every skeleton access: it is false during the build-time call (OnAgentBuild),
    // when the elephant's skeleton is not yet built and native bone queries access-violate the load.
    private const string AnchorBoneName = "Spine1_05";
    private bool _liveTicking;
    private bool _boneResolved;
    private bool _loggedBoneError;
    private sbyte _anchorBoneIndex = -1;

    internal void RepositionToElephant()
    {
        // DEFERRED (2026-06-10): bone-tracking is a CONFIRMED slide source and is disabled for now.
        // It SetFrames the howdah's bo_ physics floor AT the spine bone — inside the elephant's collision capsule —
        // so the physics solver shoves the elephant ("slide"). Confirmed by the isolation ladder: the bone-test
        // build (bone off + crew off) did NOT slide; the control build (bone on + crew off) DID. Fixed-offset
        // (feet + 3.2 Z) keeps the floor above the capsule, so the empty howdah no longer perturbs the elephant.
        // Re-enable the bone branch ONLY together with the physics-contact fix (drop the floor's collision, or
        // raise the bone frame to clear the capsule). TryRepositionToBone/ResolveBoneIndex are retained for that
        // fix. See docs/features/elephant.md → "Slide root-cause isolation".
        // if (_liveTicking && TryRepositionToBone()) return;
        RepositionToFixedOffset();
    }

    // Mirrors the safe, index-based, bounds-checked, null-guarded bone idiom in
    // AdvancedCombat/BoneCheck.cs — never the by-name frame API, which faults on a missing name.
    private bool TryRepositionToBone()
    {
        try
        {
            var visuals = elephantAgent?.AgentVisuals;
            Skeleton skel = visuals?.GetSkeleton();
            if (skel == null) return false;

            int boneCount = skel.GetBoneCount();
            if (!_boneResolved)
            {
                _boneResolved = true;
                _anchorBoneIndex = ResolveBoneIndex(skel, AnchorBoneName, boneCount);
                _logger?.LogInfo(
                    $"[Howdah] Resolved bone '{AnchorBoneName}' -> index {_anchorBoneIndex} (boneCount={boneCount})");
            }
            if (_anchorBoneIndex < 0 || _anchorBoneIndex >= boneCount) return false;

            MatrixFrame boneLocal = skel.GetBoneEntitialFrameWithIndex(_anchorBoneIndex);
            MatrixFrame world = visuals.GetGlobalFrame().TransformToParent(in boneLocal);
            GameEntity.SetFrame(ref world);
            return true;
        }
        catch (Exception ex)
        {
            if (!_loggedBoneError)
            {
                _loggedBoneError = true;
                _logger?.LogError($"[Howdah] Bone reposition failed ({ex.GetType().Name}: {ex.Message}); using fixed offset");
            }
            return false;
        }
    }

    // Enumerate bones by index and match by name — validates existence without risking a native
    // fault on a bad name. Engine bone names carry a leading space + mixed case (e.g. " Spine1_05").
    private static sbyte ResolveBoneIndex(Skeleton skel, string name, int boneCount)
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
        MatrixFrame next = elephantAgent.Frame;
        next.origin = anchor;
        GameEntity.SetFrame(ref next);
    }
}
