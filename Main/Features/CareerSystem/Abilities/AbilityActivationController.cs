namespace TAOM.Features.CareerSystem.Abilities;

// Per-frame V-key + cooldown state machine extracted from CareerPerkMissionBehavior.OnMissionTick
// (Issue #102). Pure logic + injected adapters -- no TaleWorlds statics, fully unit-testable.
//
// Result fields per tick (flags-style — multiple can be true simultaneously):
//   no career               -> all false
//   cooldown finished       -> JustBecameReady = true (one-shot until next Activated)
//   V pressed, ready        -> Activated = true (re-arms the JustBecameReady flag); JustBecameReady
//                              is also true on the same frame if the cooldown completed THIS frame
//   V pressed, on cooldown  -> Charging = true (throttled to once per 2s)
public class AbilityActivationController : IAbilityActivationController
{
    private const float ChargingMessageThrottleSeconds = 2f;

    private readonly ICareerAbilityService _abilityService;
    private readonly IAbilityInputAdapter _input;
    private readonly IMissionTimeProvider _time;

    private bool _abilityReadyNotified;
    private float _lastChargingMessageTime = -ChargingMessageThrottleSeconds;

    public AbilityActivationController(
        ICareerAbilityService abilityService,
        IAbilityInputAdapter input,
        IMissionTimeProvider time)
    {
        _abilityService = abilityService;
        _input = input;
        _time = time;
    }

    public AbilityActivationResult Tick(float dt, string heroStringId, bool hasCareer, bool isControllingCareerHero)
    {
        var result = default(AbilityActivationResult);
        if (!hasCareer) return result;

        // dt is the mission-simulation delta from MissionBehavior.OnMissionTick -- NOT wall-clock.
        // v1.4.5 Mission.OnTick (verified via Codex #102) scales dt by Scene.TimeSpeed and
        // splits fast-forward into multiple 0.1s sub-ticks before the final tick. The cooldown
        // domain wants mission-time (30s of fighting, not 30s of real-world wall-clock), so this
        // is the correct semantic. Tick(dt) is invoked per-frame and not batched (Codex Review
        // #31 caught a single-bucket accumulator dropping elapsed time on long frames).
        _abilityService.Tick(heroStringId, dt);

        // Issue #377 — controlled-agent gate. The cooldown above keeps draining in mission
        // time, but while the player controls a different agent no prompt fires (deferring
        // _abilityReadyNotified makes the ready toast fire when control returns) and V-presses
        // fall through (pre-fix, a V-press here consumed the cooldown while ApplyAoeBuff
        // early-returned on the missing agent — a silently wasted activation).
        if (!isControllingCareerHero) return result;

        // Deep-review #102 LOW — hoist a single isReady local. Two IsAbilityReady calls per
        // activation frame is sub-microsecond waste but trivially deduplicated.
        // Codex review 2026-08-05 P2 — a live active window blocks re-activation: duration
        // mutations can push the window past the cooldown floor (olog_hai reaches a 16s
        // window vs a 15s cooldown), and a recast inside the window would double-stack
        // buff contributions for the overlap. "Ready" therefore also means "window closed".
        var isReady = _abilityService.IsAbilityReady(heroStringId)
            && !_abilityService.IsAbilityActive(heroStringId);

        if (isReady && !_abilityReadyNotified)
        {
            _abilityReadyNotified = true;
            result.JustBecameReady = true;
        }

        if (_input.IsActivationKeyPressed())
        {
            if (isReady)
            {
                _abilityService.ActivateAbility(heroStringId);
                _abilityReadyNotified = false; // re-arm for the NEXT cooldown completion
                result.Activated = true;
                return result;
            }

            var now = _time.CurrentTime;
            if (now - _lastChargingMessageTime >= ChargingMessageThrottleSeconds)
            {
                _lastChargingMessageTime = now;
                result.Charging = true;
            }
        }

        return result;
    }

    public void Reset()
    {
        _abilityReadyNotified = false;
        _lastChargingMessageTime = -ChargingMessageThrottleSeconds;
    }
}
