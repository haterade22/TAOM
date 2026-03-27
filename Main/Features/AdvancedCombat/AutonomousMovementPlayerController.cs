using System;
using NetworkMessages.FromClient;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace TAOM.Features.AdvancedCombat;

[DefaultView]
public class AutonomousMovementPlayerController : MissionView
{
    public event OnLockedAgentChangedDelegate OnLockedAgentChanged;
    public event OnPotentialLockedAgentChangedDelegate OnPotentialLockedAgentChanged;
    public bool IsDisabled { get; set; }
    public Agent TargetAgent { get; set; }

    bool wentLeftLastTick = false;
    private Vec2 GoToTarget()
    {
        if (TargetAgent == null) return new Vec2();
        Vec2 fromMovement = new(0, 0);
        if ((TargetAgent.Position.AsVec2 - Agent.Main.Position.AsVec2).Length > 4) fromMovement = TargetAgent.GetMovementDirection() * TargetAgent.MovementVelocity.Y;
        Vec3 estimatedTargetDestination = TargetAgent.Position + fromMovement.ToVec3();
        Vec3 directionToEstimatedTarget = (estimatedTargetDestination - Agent.Main.MountAgent.Position).NormalizedCopy();
        Vec2 goTo = new();
        Vec2 agentVec = Agent.Main.MountAgent.Frame.rotation.f.AsVec2;
        Vec2 worldVec = directionToEstimatedTarget.AsVec2;

        Vec2 temp = estimatedTargetDestination.AsVec2 - Agent.Main.Position.AsVec2;
        float distance = (float)MathF.Sqrt(MathF.Pow(temp.X, 2) + MathF.Pow(temp.Y, 2));
        Vec2 v1 = agentVec;
        Vec2 v2 = worldVec;
        v1 /= v1.Length;
        v2 /= v2.Length;

        float dot = Vec2.DotProduct(v1, v2);
        float det = v1.X * v2.Y - v1.Y * v2.X;
        float angleRad = MathF.Atan2(det, dot);
        float angleDeg = angleRad * (180f / MathF.PI);

        if (angleDeg < 0) angleDeg += 360;
        if (MathF.Abs(angleDeg) < 10) goTo.x = 0;
        else if (angleDeg > 330 || angleDeg < 30)
        {
            if (wentLeftLastTick) goTo.x = -1;
            else goTo.x = 1;
        }
        else if (angleDeg >= 180)
        {
            goTo.x = 1;
            wentLeftLastTick = false;
        }
        else
        {
            goTo.x = -1;
            wentLeftLastTick = true;
        }
        switch (distance)
        {
            case < 1:
                if (angleDeg > 135 && angleDeg < 225)
                    goTo.y = 1;
                goTo.y = -1;
                break;
            case < 2.5f:
                goTo.y = 0;
                if (Agent.Main.Velocity.Y != 0)
                    goTo.y = -1;
                break;
            case < 10:
                if (angleDeg < 15 || angleDeg > 345)
                    goTo.y = 1;
                else goTo.y = -1;
                break;
            default:
                if (angleDeg < 90 || angleDeg > 270) goTo.y = 1;
                else goTo.y = -1;
                break;
        };
        return goTo;
    }

    public Vec3 CustomLookDir { get; set; }

    public bool IsPlayerAiming
    {
        get
        {
            if (_isPlayerAiming) return true;
            if (Mission.MainAgent == null) return false;
            bool flag = false;
            bool flag2 = false;
            bool flag3 = false;
            if (Input != null) flag2 = Input.IsGameKeyDown(9);
            if (Mission.MainAgent != null)
            {
                if (Mission.MainAgent.WieldedWeapon.CurrentUsageItem != null)
                    flag = Mission.MainAgent.WieldedWeapon.CurrentUsageItem.IsRangedWeapon || Mission.MainAgent.WieldedWeapon.CurrentUsageItem.IsAmmo;
                flag3 = Mission.MainAgent.MovementFlags.HasAnyFlag(Agent.MovementControlFlag.AttackMask);
            }
            return flag && flag2 && flag3;
        }
    }

    public Agent LockedAgent
    {
        get => _lockedAgent;
        private set
        {
            if (_lockedAgent != value)
            {
                _lockedAgent = value;
                OnLockedAgentChanged?.Invoke(value);
            }
        }
    }

    public Agent PotentialLockTargetAgent
    {
        get => _potentialLockTargetAgent;
        private set
        {
            if (_potentialLockTargetAgent != value)
            {
                _potentialLockTargetAgent = value;
                OnPotentialLockedAgentChanged?.Invoke(value);
            }
        }
    }

    public AutonomousMovementPlayerController()
    {
        CustomLookDir = Vec3.Zero;
        IsChatOpen = false;
    }

    public override void EarlyStart()
    {
        base.EarlyStart();
        Game.Current.EventManager.RegisterEvent(new Action<MissionPlayerToggledOrderViewEvent>(OnPlayerToggleOrder));
        Mission.OnMainAgentChanged += Mission_OnMainAgentChanged;
        MissionMultiplayerGameModeBaseClient missionBehavior = Mission.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();
        if ((missionBehavior != null ? missionBehavior.RoundComponent : null) != null)
        {
            missionBehavior.RoundComponent.OnRoundStarted += Disable;
            missionBehavior.RoundComponent.OnPreparationEnded += Enable;
        }
        ManagedOptions.OnManagedOptionChanged = (ManagedOptions.OnManagedOptionChangedDelegate)Delegate.Combine(ManagedOptions.OnManagedOptionChanged, new ManagedOptions.OnManagedOptionChangedDelegate(OnManagedOptionChanged));
        UpdateLockTargetOption();
    }

    public override void OnMissionScreenFinalize()
    {
        base.OnMissionScreenFinalize();
        Mission.OnMainAgentChanged -= Mission_OnMainAgentChanged;
        Game.Current.EventManager.UnregisterEvent(new Action<MissionPlayerToggledOrderViewEvent>(OnPlayerToggleOrder));
        MissionMultiplayerGameModeBaseClient missionBehavior = Mission.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();
        if ((missionBehavior != null ? missionBehavior.RoundComponent : null) != null)
        {
            missionBehavior.RoundComponent.OnRoundStarted -= Disable;
            missionBehavior.RoundComponent.OnPreparationEnded -= Enable;
        }
        ManagedOptions.OnManagedOptionChanged = (ManagedOptions.OnManagedOptionChangedDelegate)Delegate.Remove(ManagedOptions.OnManagedOptionChanged, new ManagedOptions.OnManagedOptionChangedDelegate(OnManagedOptionChanged));
    }

    public override bool IsReady()
    {
        bool result = true;
        if (Mission.MainAgent != null && Mission.MainAgent.IsActive() && !Mission.MainAgent.IsFadingOut())
            result = Mission.MainAgent.AgentVisuals.CheckResources(true);
        return result;
    }

    private void Mission_OnMainAgentChanged(Agent oldAgent)
    {
        if (Mission.MainAgent != null)
        {
            _isPlayerAgentAdded = true;
            _strafeModeActive = false;
            _autoDismountModeActive = false;
        }
    }

    public override void OnPreMissionTick(float dt)
    {
        Agent mainAgent = Mission.MainAgent;
        if (mainAgent == null || mainAgent.MountAgent == null) return;
        base.OnPreMissionTick(dt);
        if (MissionScreen == null) return;
        if (Mission.MainAgent == null && GameNetwork.MyPeer != null)
        {
            MissionPeer component = GameNetwork.MyPeer.GetComponent<MissionPeer>();
            if (component != null)
            {
                if (component.HasSpawnedAgentVisuals)
                    AgentVisualsMovementCheck();
                else if (component.FollowedAgent != null)
                    RequestToSpawnAsBotCheck();
            }
        }
        if (mainAgent != null && mainAgent.State == AgentState.Active && !MissionScreen.IsCheatGhostMode && !Mission.MainAgent.IsAIControlled && !IsDisabled && _activated)
        {
            ControlTick();
            LookTick(dt);
            return;
        }
        LockedAgent = null;
    }

    private void LookTick(float dt)
    {
        if (IsDisabled) return;
        Agent mainAgent = Mission.MainAgent;
        if (mainAgent == null) return;

        if (_isPlayerAgentAdded)
        {
            _isPlayerAgentAdded = false;
            mainAgent.LookDirectionAsAngle = mainAgent.MovementDirectionAsAngle;
        }
        if (Mission.ClearSceneTimerElapsedTime >= 0f)
        {
            Vec3 lookDirection;
            if (LockedAgent != null)
            {
                float num = 0f;
                float agentScale = LockedAgent.AgentScale;
                float agentScale2 = mainAgent.AgentScale;
                if (!LockedAgent.GetAgentFlags().HasAnyFlag(AgentFlag.IsHumanoid))
                    num += LockedAgent.Monster.BodyCapsulePoint1.z * agentScale;
                else if (LockedAgent.HasMount)
                    num += (LockedAgent.MountAgent.Monster.RiderCameraHeightAdder + LockedAgent.MountAgent.Monster.BodyCapsulePoint1.z + LockedAgent.MountAgent.Monster.BodyCapsuleRadius) * LockedAgent.MountAgent.AgentScale + LockedAgent.Monster.CrouchEyeHeight * agentScale;
                else if (LockedAgent.CrouchMode || LockedAgent.IsSitting())
                    num += (LockedAgent.Monster.CrouchEyeHeight + 0.2f) * agentScale;
                else
                    num += (LockedAgent.Monster.StandingEyeHeight + 0.2f) * agentScale;

                if (!mainAgent.GetAgentFlags().HasAnyFlag(AgentFlag.IsHumanoid))
                    num -= LockedAgent.Monster.BodyCapsulePoint1.z * agentScale2;
                else if (mainAgent.HasMount)
                    num -= (mainAgent.MountAgent.Monster.RiderCameraHeightAdder + mainAgent.MountAgent.Monster.BodyCapsulePoint1.z + mainAgent.MountAgent.Monster.BodyCapsuleRadius) * mainAgent.MountAgent.AgentScale + mainAgent.Monster.CrouchEyeHeight * agentScale2;
                else if (mainAgent.CrouchMode || mainAgent.IsSitting())
                    num -= (mainAgent.Monster.CrouchEyeHeight + 0.2f) * agentScale2;
                else
                    num -= (mainAgent.Monster.StandingEyeHeight + 0.2f) * agentScale2;

                if (LockedAgent.GetAgentFlags().HasAnyFlag(AgentFlag.IsHumanoid))
                    num -= 0.3f * agentScale;

                num = MBMath.Lerp(_lastLockedAgentHeightDifference, num, MathF.Min(8f * dt, 1f), 1E-05f);
                _lastLockedAgentHeightDifference = num;
                lookDirection = (LockedAgent.VisualPosition + (LockedAgent.MountAgent != null ? LockedAgent.MountAgent.GetMovementDirection().ToVec3(0f) * LockedAgent.MountAgent.Monster.RiderBodyCapsuleForwardAdder : Vec3.Zero) + new Vec3(0f, 0f, num, -1f) - (mainAgent.VisualPosition + (mainAgent.MountAgent != null ? mainAgent.MountAgent.GetMovementDirection().ToVec3(0f) * mainAgent.MountAgent.Monster.RiderBodyCapsuleForwardAdder : Vec3.Zero))).NormalizedCopy();
            }
            else if (CustomLookDir.IsNonZero)
            {
                lookDirection = CustomLookDir;
            }
            else
            {
                Mat3 identity = Mat3.Identity;
                identity.RotateAboutUp(MissionScreen.CameraBearing);
                identity.RotateAboutSide(MissionScreen.CameraElevation);
                lookDirection = identity.f;
            }
            if (!MissionScreen.IsViewingCharacter() && !mainAgent.IsLookDirectionLocked && mainAgent.MovementLockedState != AgentMovementLockedState.FrameLocked)
                mainAgent.LookDirection = lookDirection;
            mainAgent.HeadCameraMode = Mission.CameraIsFirstPerson;
        }
    }

    private void AgentVisualsMovementCheck()
    {
        if (Input.IsGameKeyReleased(13))
            BreakAgentVisualsInvulnerability();
    }

    public void BreakAgentVisualsInvulnerability()
    {
        if (GameNetwork.IsClient)
        {
            GameNetwork.BeginModuleEventAsClient();
            GameNetwork.WriteMessage(new AgentVisualsBreakInvulnerability());
            GameNetwork.EndModuleEventAsClient();
            return;
        }
        Mission.Current.GetMissionBehavior<SpawnComponent>().SetEarlyAgentVisualsDespawning(GameNetwork.MyPeer.GetComponent<MissionPeer>(), true);
    }

    private void RequestToSpawnAsBotCheck()
    {
        if (Input.IsGameKeyPressed(13))
        {
            if (GameNetwork.IsClient)
            {
                GameNetwork.BeginModuleEventAsClient();
                GameNetwork.WriteMessage(new RequestToSpawnAsBot());
                GameNetwork.EndModuleEventAsClient();
                return;
            }
            if (GameNetwork.MyPeer.GetComponent<MissionPeer>().HasSpawnTimerExpired)
                GameNetwork.MyPeer.GetComponent<MissionPeer>().WantsToSpawnAsBot = true;
        }
    }

    private Agent FindTargetedLockableAgent(Agent player)
    {
        Vec3 direction = MissionScreen.CombatCamera.Direction;
        Vec3 vec = direction;
        Vec3 position = MissionScreen.CombatCamera.Position;
        Vec3 visualPosition = player.VisualPosition;
        float num = new Vec3(position.x, position.y, 0f, -1f).Distance(new Vec3(visualPosition.x, visualPosition.y, 0f, -1f));
        Vec3 v = position * (1f - num) + (position + direction) * num;
        float num2 = 0f;
        Agent agent = null;
        foreach (Agent agent2 in Mission.Agents)
        {
            if (agent2.IsMount && agent2.RiderAgent != null && agent2.RiderAgent.IsEnemyOf(player) || !agent2.IsMount && agent2.IsEnemyOf(player))
            {
                Vec3 vec2 = agent2.GetChestGlobalPosition() - v;
                float num3 = vec2.Normalize();
                if (num3 < 20f)
                {
                    float num4 = Vec2.DotProduct(vec.AsVec2.Normalized(), vec2.AsVec2.Normalized());
                    float num5 = Vec2.DotProduct(new Vec2(vec.AsVec2.Length, vec.z), new Vec2(vec2.AsVec2.Length, vec2.z));
                    if (num4 > 0.95f && num5 > 0.95f)
                    {
                        float num6 = num4 * num4 * num4 / MathF.Pow(num3, 0.15f);
                        if (num6 > num2)
                        {
                            num2 = num6;
                            agent = agent2;
                        }
                    }
                }
            }
        }
        if (agent != null && agent.IsMount && agent.RiderAgent != null)
            return agent.RiderAgent;
        return agent;
    }

    private void ControlTick()
    {
        if (ShouldSkipControlTick()) return;
        Agent mainAgent = Mission.MainAgent;
        bool lockedAgentWasCleared = ValidateAndClearInvalidLockedAgent(mainAgent);
        if (IsInConversationMode(mainAgent)) return;

        if (Mission.ClearSceneTimerElapsedTime >= 0f && mainAgent.State == AgentState.Active)
        {
            ProcessMovementInput(mainAgent);
            ProcessTargetLocking(mainAgent, lockedAgentWasCleared);
            ProcessCombatControls(mainAgent);
            ProcessActionControls(mainAgent);
        }
    }

    private bool ShouldSkipControlTick() => (MissionScreen != null && MissionScreen.IsPhotoModeEnabled) || IsChatOpen;

    private bool ValidateAndClearInvalidLockedAgent(Agent mainAgent)
    {
        bool wasCleared = false;
        if (LockedAgent != null && ShouldClearLockedAgent(mainAgent))
        {
            LockedAgent = null;
            wasCleared = true;
        }
        return wasCleared;
    }

    private bool ShouldClearLockedAgent(Agent mainAgent)
    {
        return !Mission.Agents.ContainsQ(LockedAgent) ||
               !LockedAgent.IsActive() ||
               LockedAgent.Position.DistanceSquared(mainAgent.Position) > 625f ||
               Input.IsGameKeyReleased(26) ||
               Input.IsGameKeyDown(25) ||
               (Mission.Mode != MissionMode.Battle && Mission.Mode != MissionMode.Stealth) ||
               (!mainAgent.WieldedWeapon.IsEmpty && mainAgent.WieldedWeapon.CurrentUsageItem.IsRangedWeapon) ||
               MissionScreen == null ||
               MissionScreen.GetSpectatingData(MissionScreen.CombatCamera.Frame.origin).CameraType != SpectatorCameraTypes.LockToMainPlayer;
    }

    private bool IsInConversationMode(Agent mainAgent)
    {
        if (Mission.Mode == MissionMode.Conversation)
        {
            mainAgent.MovementFlags = 0U;
            mainAgent.MovementInputVector = Vec2.Zero;
            return true;
        }
        return false;
    }

    private void ProcessMovementInput(Agent mainAgent)
    {
        Vec2 movementVector = GoToTarget();
        ApplyAutoDismountMovement(mainAgent, ref movementVector);
        NormalizeMovementDeadzone(ref movementVector);
        CalculateMovementDirections(movementVector, out bool moveLeft, out bool moveRight);

        mainAgent.EventControlFlags = 0U;
        mainAgent.MovementFlags = 0U;
        mainAgent.MovementInputVector = Vec2.Zero;
        ApplyMountTurning(mainAgent, movementVector.x, moveLeft, moveRight);
        mainAgent.MovementInputVector = movementVector;
    }

    private void ApplyAutoDismountMovement(Agent mainAgent, ref Vec2 movementVector)
    {
        if (_autoDismountModeActive)
        {
            if (!Input.IsGameKeyDown(0) && mainAgent.MountAgent != null)
            {
                if (mainAgent.GetCurrentVelocity().y > 0f)
                    movementVector.y = -1f;
            }
            else
                _autoDismountModeActive = false;
        }
    }

    private void NormalizeMovementDeadzone(ref Vec2 movementVector)
    {
        if (MathF.Abs(movementVector.x) < 0.2f) movementVector.x = 0f;
        if (MathF.Abs(movementVector.y) < 0.2f) movementVector.y = 0f;
    }

    private void CalculateMovementDirections(Vec2 movementVector, out bool moveLeft, out bool moveRight)
    {
        moveLeft = false;
        moveRight = false;
        if (movementVector.IsNonZero())
        {
            float rotationInRadians = movementVector.RotationInRadians;
            if (rotationInRadians < -2.3561945f || rotationInRadians > 2.3561945f) { }
            else if (rotationInRadians < 0f) moveRight = true;
            else if (rotationInRadians > 0.7853982f) moveLeft = true;
        }
    }

    private void ApplyMountTurning(Agent mainAgent, float horizontalInput, bool moveLeft, bool moveRight)
    {
        if (mainAgent.MountAgent != null && !_strafeModeActive)
        {
            if (moveRight || horizontalInput > 0f)
                mainAgent.MovementFlags |= Agent.MovementControlFlag.TurnRight;
            else if (moveLeft || horizontalInput < 0f)
                mainAgent.MovementFlags |= Agent.MovementControlFlag.TurnLeft;
        }
    }

    private void ProcessTargetLocking(Agent mainAgent, bool lockedAgentWasCleared)
    {
        if (!_isTargetLockEnabled) return;
        if (ShouldShowPotentialLockTarget(mainAgent))
        {
            float applicationTime = Time.ApplicationTime;
            if (_lastLockKeyPressTime <= 0f) _lastLockKeyPressTime = applicationTime;
            if (applicationTime > _lastLockKeyPressTime + 0.3f)
                PotentialLockTargetAgent = FindTargetedLockableAgent(mainAgent);
        }
        else
            PotentialLockTargetAgent = null;

        if (ShouldLockTarget(mainAgent, lockedAgentWasCleared))
        {
            _lastLockKeyPressTime = 0f;
            LockedAgent = FindTargetedLockableAgent(mainAgent);
        }
    }

    private bool ShouldShowPotentialLockTarget(Agent mainAgent)
    {
        return Input.IsGameKeyDown(26) && LockedAgent == null && !Input.IsGameKeyDown(25) &&
               (Mission.Mode == MissionMode.Battle || Mission.Mode == MissionMode.Stealth) &&
               (mainAgent.WieldedWeapon.IsEmpty || !mainAgent.WieldedWeapon.CurrentUsageItem.IsRangedWeapon) &&
               !GameNetwork.IsMultiplayer;
    }

    private bool ShouldLockTarget(Agent mainAgent, bool lockedAgentWasCleared)
    {
        return LockedAgent == null && !lockedAgentWasCleared && Input.IsGameKeyReleased(26) && !GameNetwork.IsMultiplayer &&
               !Input.IsGameKeyDown(25) && (Mission.Mode == MissionMode.Battle || Mission.Mode == MissionMode.Stealth) &&
               (mainAgent.WieldedWeapon.IsEmpty || !mainAgent.WieldedWeapon.CurrentUsageItem.IsRangedWeapon) &&
               MissionScreen != null && MissionScreen.GetSpectatingData(MissionScreen.CombatCamera.Frame.origin).CameraType == SpectatorCameraTypes.LockToMainPlayer;
    }

    private void ProcessCombatControls(Agent mainAgent)
    {
        if (!CanProcessCombatControls(mainAgent)) return;
        WeaponComponentData currentUsageItem = mainAgent.WieldedWeapon.CurrentUsageItem;
        bool isStringHeldWeapon = currentUsageItem != null && currentUsageItem.WeaponFlags.HasAllFlags(WeaponFlags.StringHeldByHand);
        bool isNonConsumableRangedWeapon = currentUsageItem != null && currentUsageItem.IsRangedWeapon && !currentUsageItem.IsConsumable && !currentUsageItem.WeaponFlags.HasAllFlags(WeaponFlags.StringHeldByHand);
        bool useAlternativeAiming = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.EnableAlternateAiming) != 0f && (isStringHeldWeapon || isNonConsumableRangedWeapon);

        if (useAlternativeAiming)
            HandleRangedWeaponAttackAlternativeAiming(mainAgent);
        else if (Input.IsGameKeyDown(9))
            mainAgent.MovementFlags |= mainAgent.AttackDirectionToMovementFlag(mainAgent.GetAttackDirection());

        if (!useAlternativeAiming && Input.IsGameKeyDown(10))
            ProcessDefendInput(mainAgent);
    }

    private bool CanProcessCombatControls(Agent mainAgent)
    {
        return !MissionScreen.MouseVisible && !MissionScreen.IsRadialMenuActive && !_isPlayerOrderOpen && mainAgent.CombatActionsEnabled;
    }

    private void ProcessDefendInput(Agent mainAgent)
    {
        if (ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ControlBlockDirection) == 2f && MissionGameModels.Current.AutoBlockModel != null)
        {
            Agent.UsageDirection blockDirection = MissionGameModels.Current.AutoBlockModel.GetBlockDirection(Mission);
            if (blockDirection == Agent.UsageDirection.AttackLeft) mainAgent.MovementFlags |= Agent.MovementControlFlag.DefendRight;
            else if (blockDirection == Agent.UsageDirection.AttackRight) mainAgent.MovementFlags |= Agent.MovementControlFlag.DefendLeft;
            else if (blockDirection == Agent.UsageDirection.AttackUp) mainAgent.MovementFlags |= Agent.MovementControlFlag.DefendUp;
            else if (blockDirection == Agent.UsageDirection.AttackDown) mainAgent.MovementFlags |= Agent.MovementControlFlag.DefendDown;
        }
        else
            mainAgent.MovementFlags |= mainAgent.GetDefendMovementFlag();
    }

    private void ProcessActionControls(Agent mainAgent)
    {
        if (!CanProcessActionControls()) return;
        ProcessKickInput(mainAgent);
        ProcessWeaponSelectionInput(mainAgent);
        ProcessWeaponToggleInput(mainAgent);
        ProcessWalkRunToggle(mainAgent);
        ProcessMountControls(mainAgent);
    }

    private bool CanProcessActionControls() => !MissionScreen.IsRadialMenuActive && !Mission.IsOrderMenuOpen;

    private void ProcessKickInput(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(16) && (mainAgent.KickClear() || mainAgent.MountAgent != null))
            mainAgent.EventControlFlags |= Agent.EventControlFlag.Kick;
    }

    private void ProcessWeaponSelectionInput(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(18)) mainAgent.TryToWieldWeaponInSlot(EquipmentIndex.WeaponItemBeginSlot, Agent.WeaponWieldActionType.WithAnimation, false);
        else if (Input.IsGameKeyPressed(19)) mainAgent.TryToWieldWeaponInSlot(EquipmentIndex.Weapon1, Agent.WeaponWieldActionType.WithAnimation, false);
        else if (Input.IsGameKeyPressed(20)) mainAgent.TryToWieldWeaponInSlot(EquipmentIndex.Weapon2, Agent.WeaponWieldActionType.WithAnimation, false);
        else if (Input.IsGameKeyPressed(21)) mainAgent.TryToWieldWeaponInSlot(EquipmentIndex.Weapon3, Agent.WeaponWieldActionType.WithAnimation, false);
        else if (Input.IsGameKeyPressed(11) && _lastWieldNextPrimaryWeaponTriggerTime + 0.2f < Time.ApplicationTime)
        {
            _lastWieldNextPrimaryWeaponTriggerTime = Time.ApplicationTime;
            mainAgent.WieldNextWeapon(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.WithAnimation);
        }
        else if (Input.IsGameKeyPressed(12) && _lastWieldNextOffhandWeaponTriggerTime + 0.2f < Time.ApplicationTime)
        {
            _lastWieldNextOffhandWeaponTriggerTime = Time.ApplicationTime;
            mainAgent.WieldNextWeapon(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.WithAnimation);
        }
        else if (Input.IsGameKeyPressed(23))
            mainAgent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.WithAnimation);
    }

    private void ProcessWeaponToggleInput(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(17) || _weaponUsageToggleRequested)
        {
            mainAgent.EventControlFlags |= Agent.EventControlFlag.ToggleAlternativeWeapon;
            _weaponUsageToggleRequested = false;
        }
    }

    private void ProcessWalkRunToggle(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(30))
            mainAgent.EventControlFlags |= mainAgent.WalkMode ? Agent.EventControlFlag.Run : Agent.EventControlFlag.Walk;
    }

    private void ProcessMountControls(Agent mainAgent)
    {
        if (mainAgent.MountAgent != null) ProcessDismountInput(mainAgent);
        else ProcessCrouchInput(mainAgent);
    }

    private void ProcessDismountInput(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(15) || _autoDismountModeActive)
        {
            if (mainAgent.GetCurrentVelocity().y < 0.5f && mainAgent.MountAgent.GetCurrentActionType(0) != Agent.ActionCodeType.Rear)
            {
                mainAgent.EventControlFlags |= Agent.EventControlFlag.Dismount;
                return;
            }
            if (Input.IsGameKeyPressed(15))
            {
                _autoDismountModeActive = true;
                mainAgent.EventControlFlags &= ~(Agent.EventControlFlag.DoubleTapToDirectionUp | Agent.EventControlFlag.DoubleTapToDirectionDown | Agent.EventControlFlag.DoubleTapToDirectionRight);
                mainAgent.EventControlFlags |= Agent.EventControlFlag.DoubleTapToDirectionDown;
            }
        }
    }

    private void ProcessCrouchInput(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(15))
            mainAgent.EventControlFlags |= mainAgent.CrouchMode ? Agent.EventControlFlag.Stand : Agent.EventControlFlag.Crouch;
    }

    private void HandleRangedWeaponAttackAlternativeAiming(Agent player)
    {
        if (Input.GetKeyState(InputKey.ControllerLTrigger).x > 0.2f)
        {
            if (Input.GetKeyState(InputKey.ControllerRTrigger).x < 0.6f)
                player.MovementFlags |= player.AttackDirectionToMovementFlag(player.GetAttackDirection());
            _isPlayerAiming = true;
            return;
        }
        if (_isPlayerAiming)
        {
            player.MovementFlags |= Agent.MovementControlFlag.DefendUp;
            _isPlayerAiming = false;
        }
    }

    public override bool IsThereAgentAction(Agent userAgent, Agent otherAgent)
    {
        return otherAgent.IsMount && otherAgent.IsActive();
    }

    public void Disable() { _activated = false; }
    public void Enable() { _activated = true; }

    private void OnPlayerToggleOrder(MissionPlayerToggledOrderViewEvent obj)
    {
        _isPlayerOrderOpen = obj.IsOrderEnabled;
    }

    public void OnWeaponUsageToggleRequested() { _weaponUsageToggleRequested = true; }

    private void OnManagedOptionChanged(ManagedOptions.ManagedOptionsType optionType)
    {
        if (optionType == ManagedOptions.ManagedOptionsType.LockTarget)
            UpdateLockTargetOption();
    }

    private void UpdateLockTargetOption()
    {
        _isTargetLockEnabled = ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.LockTarget) == 1f;
        LockedAgent = null;
        PotentialLockTargetAgent = null;
        _lastLockKeyPressTime = 0f;
        _lastLockedAgentHeightDifference = 0f;
    }

    private float _lastWieldNextPrimaryWeaponTriggerTime;
    private float _lastWieldNextOffhandWeaponTriggerTime;
    private bool _activated = true;
    private bool _strafeModeActive;
    private bool _autoDismountModeActive;
    private bool _isPlayerAgentAdded;
    private bool _isPlayerAiming;
    private bool _isPlayerOrderOpen;
    private bool _isTargetLockEnabled;
    private Agent _lockedAgent;
    private Agent _potentialLockTargetAgent;
    private float _lastLockKeyPressTime;
    private float _lastLockedAgentHeightDifference;
    public bool IsChatOpen;
    private bool _weaponUsageToggleRequested;

    public delegate void OnLockedAgentChangedDelegate(Agent newAgent);
    public delegate void OnPotentialLockedAgentChangedDelegate(Agent newPotentialAgent);
}
