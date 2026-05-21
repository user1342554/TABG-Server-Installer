using System;
using System.Collections.Generic;
using System.Reflection;
using Landfall.Network;
using UnityEngine;
using UnityEngine.AI;

namespace TabgInstaller.FakePlayers
{
    internal class AiDummyController : MonoBehaviour
    {
        private enum AiState
        {
            Warmup,
            Looting,
            Advancing,
            Fighting,
            Searching,
            Wandering,
            Unstuck,
            Dropping
        }

        private const float MoveSpeed = 2.2f;
        private const float CombatMoveSpeed = 2.55f;
        private const float ChaseRange = 10000f;
        private const float LootSearchRange = 180f;
        private const float PickupRange = 5.2f;
        private const float ShootRange = 30f;
        private const float PreferredFightRange = 16f;
        private const float MinFightRange = 6f;
        private const float HardPushRange = 32f;
        private const float DamagePerShot = 5.5f;
        private const float AutoDamagePerBullet = 2.4f;
        private const float AutoBulletInterval = 0.11f;
        private const float WarmupTime = 4f;
        private const float WallProbeDistance = 3.0f;
        private const float WallProbeRadius = 0.55f;
        private const float NetworkSendInterval = 0.08f;
        private const float TerrainProbeUp = 8f;
        private const float TerrainProbeDown = 320f;
        private const float MaxVerticalSpeed = 7.5f;
        private const float StuckCheckInterval = 0.6f;
        private const float StuckSeconds = 2.2f;
        private const float LastSeenMemory = 8f;
        private const float PathRebuildInterval = 0.45f;
        private const float PathDestinationRebuildDistance = 7f;
        private const float PathCornerReachDistance = 2.6f;
        private const float NavMeshSampleDistance = 8f;
        private const float VehicleSearchRange = 0f;
        private const float VehicleEnterRange = 0f;
        private const float VehicleUseMinTargetDistance = 10000f;
        private const float GrenadeThrowRange = 0f;
        private const float GrenadeSplashRadius = 8f;
        private const float GrenadeFuseTime = 2.35f;
        private const float DropStartHeight = 215f;
        private const float DropHorizontalSpeed = 42f;
        private const float DropVerticalSpeed = 33f;
        private const float DropFinishDistance = 5f;
        private const float DropExitDistance = 230f;
        private const float DropLandingSafetyLift = 3.0f;
        private const float DropPositionLockSeconds = 3.0f;
        private const float MaxForcedPositionDrift = 22f;
        private const int AiGrenadeItemId = 208;

        private static readonly Vector3[] DropTargets =
        {
            new Vector3(-313f, 170f, -530f),
            new Vector3(-465f, 175f, -481f),
            new Vector3(-169f, 125f, 159f),
            new Vector3(-573f, 130f, 301f),
            new Vector3(-422f, 130f, 82f),
            new Vector3(141f, 140f, -375f),
            new Vector3(-13f, 140f, -523f),
            new Vector3(723f, 125f, -689f),
            new Vector3(439f, 130f, -274f),
            new Vector3(478f, 125f, -446f),
            new Vector3(631f, 126f, 487f),
            new Vector3(454f, 140f, 313f),
            new Vector3(-492f, 120f, 333f),
            new Vector3(-787f, 125f, 69f),
            new Vector3(-23f, 140f, 338f),
            new Vector3(-162f, 140f, -214f),
            new Vector3(-534f, 170f, -311f),
            new Vector3(-674f, 125f, -8f),
            new Vector3(-392f, 130f, 400f),
            new Vector3(385f, 135f, 468f),
            new Vector3(659f, 125f, 600f),
            new Vector3(-411f, 120f, 517f),
            new Vector3(685f, 140f, 8f),
            new Vector3(-254f, 140f, -128f),
            new Vector3(-689f, 145f, 510f),
            new Vector3(366f, 130f, -646f),
            new Vector3(-74f, 125f, 611f),
            new Vector3(98f, 125f, -95f),
            new Vector3(433f, 140f, 73f),
            new Vector3(-385f, 175f, -771f),
            new Vector3(636f, 125f, 240f),
            new Vector3(-32f, 129f, -645f),
            new Vector3(-70f, 133f, -635f),
            new Vector3(-71f, 141f, -594f),
            new Vector3(-91f, 137f, -558f),
            new Vector3(-63f, 131f, -513f),
            new Vector3(-24f, 132f, -494f),
            new Vector3(16f, 137f, -515f),
            new Vector3(28f, 132f, -557f),
            new Vector3(10f, 124f, -600f)
        };

        private static readonly HashSet<int> ShootableWeaponIds = new HashSet<int>
        {
            151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162,
            163, 164, 169, 171, 172, 174, 184,
            176, 177, 178, 179, 180, 181, 182, 185, 203, 217, 218, 219, 220,
            264, 265, 266, 267, 269, 270, 271, 272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 283, 284,
            285, 286, 287, 288, 289, 290, 291,
            292, 293, 294, 297, 298, 300, 301,
            302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316,
            317, 319, 320, 321, 322, 323, 325, 326, 327, 328
        };

        private static readonly HashSet<int> AutomaticWeaponIds = new HashSet<int>
        {
            151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162,
            176, 178, 217, 218, 220,
            264, 269, 271,
            302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316
        };

        private ServerClient _server;
        private GameRoom _room;
        private TABGPlayerServer _player;
        private TABGPlayerServer _target;
        private NetworkGun _wantedLoot;
        private TABGCarServer _wantedCar;
        private TABGCarServer _activeCar;
        private InputHandler _physicalInput;
        private Transform _physicalHip;
        private Transform _physicalRotationTarget;
        private AiState _state = AiState.Warmup;
        private AiState _lastLoggedState = AiState.Warmup;
        private Vector3 _wanderTarget;
        private Vector3 _movementNoise;
        private Vector3 _smoothedDirection;
        private Vector3 _lastSeenTargetPosition;
        private Vector3 _lastTargetPosition;
        private Vector3 _targetVelocity;
        private Vector3 _lastAimDirection;
        private Vector3 _unstuckTarget;
        private Vector3 _lastProgressPosition;
        private Vector3 _lastPathDestination;
        private Vector3 _localAvoidWaypoint;
        private Vector3 _dropTarget;
        private Vector3 _lockedDropLanding;
        private float _terrainHeightOffset = 1.15f;
        private float _retargetTimer;
        private float _wanderTimer;
        private float _movementNoiseTimer;
        private float _decisionTimer;
        private float _stateTimer;
        private float _lastSeenTimer;
        private float _stuckCheckTimer;
        private float _stuckTimer;
        private float _combatStrafeTimer;
        private float _combatStrafeSign = 1f;
        private float _combatStrafeDistance = 4f;
        private float _combatForwardBias = 1.2f;
        private float _jumpCooldown;
        private float _jumpFlagTimer;
        private float _jumpVisualTimer;
        private float _lootTimer;
        private float _networkTimer;
        private float _shootTimer;
        private float _autoBurstTimer;
        private float _autoDamageTimer;
        private float _aimSettleTimer;
        private float _warmupTimer;
        private float _lootProgressTimer;
        private float _pathRebuildTimer;
        private float _localAvoidTimer;
        private float _lootDiagnosticsTimer;
        private float _vehicleDecisionTimer;
        private float _grenadeCooldown;
        private float _grenadeFuseTimer;
        private float _dropPositionLockTimer;
        private float _lastLootDistance = float.MaxValue;
        private float _bestPlaneDropDistance = float.MaxValue;
        private Vector3 _pendingGrenadePosition;
        private string _lastTargetName;
        private int _lastLootIndex = int.MinValue;
        private int _equippedWeaponId = -1;
        private int _autoBulletsFired;
        private int _skillLevel = 1;
        private int _pathCornerIndex = 1;
        private int _navFailureCount;
        private bool _canSeeTarget;
        private bool _hasLastSeenTarget;
        private bool _hasTargetPosition;
        private bool _hasAimDirection;
        private bool _hasWeapon;
        private bool _hasNavPath;
        private bool _navMeshDisabled;
        private bool _isFullAutoFiring;
        private bool _dropStarted;
        private bool _dropFinished;
        private NavMeshPath _navPath;

        public void Init(ServerClient server, TABGPlayerServer player, int skillLevel = 1)
        {
            _server = server;
            _room = server.GameRoomReference;
            _player = player;
            _skillLevel = Mathf.Clamp(skillLevel, 1, 5);
            _warmupTimer = WarmupTime;
            RemoveBuiltInBotController();
            InitPhysicalEnemyAiHooks();
            InitTerrainOffset();
            _navPath = new NavMeshPath();
            _dropTarget = PickDropTarget();
            PickNewWanderTarget();
            PickMovementNoise();
            _lastProgressPosition = _player.PlayerPosition;
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} skill level {_skillLevel}: range {GetDamageRange():0}m, drop target {_dropTarget}.");
            FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, _player.PlayerPosition);
        }

        private void Update()
        {
            if (_server == null || _room == null || _player == null || _player.IsDead)
            {
                StopFullAuto();
                Destroy(this);
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _retargetTimer -= dt;
            _wanderTimer -= dt;
            _movementNoiseTimer -= dt;
            _decisionTimer -= dt;
            _stateTimer -= dt;
            _lastSeenTimer -= dt;
            _stuckCheckTimer -= dt;
            _combatStrafeTimer -= dt;
            _jumpCooldown -= dt;
            _jumpFlagTimer -= dt;
            _jumpVisualTimer -= dt;
            _lootTimer -= dt;
            _networkTimer -= dt;
            _shootTimer -= dt;
            _autoBurstTimer -= dt;
            _autoDamageTimer -= dt;
            _aimSettleTimer -= dt;
            _warmupTimer -= dt;
            _pathRebuildTimer -= dt;
            _localAvoidTimer -= dt;
            _lootDiagnosticsTimer -= dt;
            _vehicleDecisionTimer -= dt;
            _grenadeCooldown -= dt;
            _grenadeFuseTimer -= dt;

            if (!_dropFinished && ShouldHandleRoundDrop())
            {
                TickRoundDrop(dt);
                return;
            }

            if (_dropPositionLockTimer > 0f)
            {
                _dropPositionLockTimer -= dt;
                ForceServerPosition(_lockedDropLanding);
                StopFullAuto();
                ClearPhysicalInput();
                _player.UpdateMovementDirection(Vector3.zero);
                _player.UpdateMovementType(0);
                if (_networkTimer <= 0f)
                {
                    FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, _lockedDropLanding);
                    _networkTimer = NetworkSendInterval;
                }
                return;
            }

            if (_warmupTimer > 0f)
            {
                _player.UpdateMovementDirection(Vector3.zero);
                _player.UpdateMovementType(0);
                ClearPhysicalInput();
                if (_networkTimer <= 0f)
                {
                    FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, _player.PlayerPosition);
                    _networkTimer = NetworkSendInterval;
                }
                return;
            }

            if (_movementNoiseTimer <= 0f)
                PickMovementNoise();

            TickTargeting();
            TickLootChoice();
            TickVehicleChoice();
            TickGrenade();
            DecideState();

            Vector3 destination = ChooseDestination();
            TrackStuck(destination);
            MoveToward(destination, dt);

            if (!_hasWeapon)
                TryPickupLoot();

            if (_state == AiState.Fighting && _hasWeapon && _target != null)
                TryShoot(_target);
            else
                StopFullAuto();

            if (_networkTimer <= 0f)
            {
                FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, _player.PlayerPosition);
                _networkTimer = NetworkSendInterval;
            }
        }

        private bool ShouldHandleRoundDrop()
        {
            switch (_room.CurrentGameState)
            {
                case GameState.WaitingForPlayers:
                case GameState.CountDown:
                case GameState.Started:
                    return true;
                default:
                    return false;
            }
        }

        private void TickRoundDrop(float dt)
        {
            if (_room.CurrentGameState == GameState.WaitingForPlayers || _room.CurrentGameState == GameState.CountDown)
            {
                WaitForDropWindow();
                return;
            }

            if (!_dropStarted)
            {
                StartDrop();
                return;
            }

            ContinueDrop(dt);
        }

        private void WaitForDropWindow()
        {
            StopFullAuto();
            ClearPhysicalInput();
            _player.UpdateMovementDirection(Vector3.zero);
            _player.UpdateMovementType(0);
        }

        private bool ShouldJumpFromPlane()
        {
            if (_room.CurrentGameState == GameState.Started)
                return true;

            Vector3 planePosition;
            if (!TryGetPlaneDropPosition(out planePosition))
                return true;

            float distance = Flat(planePosition - _dropTarget).magnitude;
            if (distance < _bestPlaneDropDistance)
                _bestPlaneDropDistance = distance;

            if (distance <= DropExitDistance)
                return true;

            return _bestPlaneDropDistance < float.MaxValue && distance > _bestPlaneDropDistance + 25f;
        }

        private void StartDrop()
        {
            Vector3 landing = ResolveDropLandingPoint();

            _dropStarted = false;
            _dropFinished = true;
            _lockedDropLanding = landing;
            _dropPositionLockTimer = DropPositionLockSeconds;
            _warmupTimer = Mathf.Min(_warmupTimer, 1.0f);
            _lastProgressPosition = landing;

            if (!_player.HasDropped)
                _player.Dropped();
            FakePlayersPlugin.RespawnWithVanillaPacket(_server, _player, landing);
            _player.Land();
            ForceServerPosition(landing);
            _player.UpdateMovementDirection(Vector3.zero);
            _player.UpdateMovementType(0);
            FacePoint(landing + Vector3.up * 1.5f);
            _server.ForceChunkEntry(_player);
            PickNewWanderTarget();
            FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, landing);
            SetState(AiState.Wandering, 0.5f);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} respawn-teleported to drop target {landing} during {_room.CurrentGameState}.");
        }

        private void ContinueDrop(float dt)
        {
            Vector3 current = _player.PlayerPosition;
            Vector3 landing = ResolveDropLandingPoint();
            Vector3 flatDelta = Flat(landing - current);
            float flatDistance = flatDelta.magnitude;
            Vector3 direction = flatDistance > 0.01f ? flatDelta / flatDistance : Vector3.zero;
            float horizontalStep = DropHorizontalSpeed * Mathf.Max(dt, 0.016f);
            Vector3 next = current;

            if (flatDistance > horizontalStep)
                next += direction * horizontalStep;
            else
            {
                next.x = landing.x;
                next.z = landing.z;
            }

            next.y = Mathf.MoveTowards(current.y, landing.y, DropVerticalSpeed * Mathf.Max(dt, 0.016f));
            _player.UpdatePosition(next);
            _player.UpdateMovementDirection(direction);

            byte movement = 0;
            if (direction != Vector3.zero)
            {
                movement = movement.SetBit(0);
                movement = movement.SetBit(3);
            }
            _player.UpdateMovementType(movement);
            if (direction != Vector3.zero)
                FacePoint(next + direction * 8f + Vector3.up * 0.4f);

            if (_networkTimer <= 0f)
            {
                FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, next);
                _networkTimer = NetworkSendInterval;
            }

            if (Flat(landing - next).magnitude > DropFinishDistance || Mathf.Abs(next.y - landing.y) > 2f)
                return;

            _dropFinished = true;
            _dropStarted = false;
            _lockedDropLanding = landing;
            _dropPositionLockTimer = DropPositionLockSeconds;
            _warmupTimer = Mathf.Min(_warmupTimer, 1.0f);
            _lastProgressPosition = landing;
            ForceServerPosition(landing);
            _player.UpdateMovementDirection(Vector3.zero);
            _player.UpdateMovementType(0);
            _server.ForceChunkEntry(_player);
            PickNewWanderTarget();
            FakePlayersPlugin.BroadcastRespawn(_server, _player, landing);
            FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, landing);
            SetState(AiState.Wandering, 0.5f);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} landed at {landing}.");
        }

        private Vector3 PickDropTarget()
        {
            Vector3 target = DropTargets[UnityEngine.Random.Range(0, DropTargets.Length)];
            Vector2 offset = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(4f, 18f);
            target.x += offset.x;
            target.z += offset.y;

            float groundY;
            if (TryFindGroundY(target, out groundY))
                target.y = groundY + _terrainHeightOffset;
            return target;
        }

        private Vector3 BuildDropStartNearLanding(Vector3 landing)
        {
            Vector2 lateral = UnityEngine.Random.insideUnitCircle.normalized;
            if (lateral.sqrMagnitude < 0.01f)
                lateral = new Vector2(1f, 0f);

            float distance = UnityEngine.Random.Range(95f, 155f);
            return landing + new Vector3(lateral.x * distance, DropStartHeight, lateral.y * distance);
        }

        private Vector3 ResolveDropLandingPoint()
        {
            Vector3 landing = _dropTarget;
            float groundY;
            if (TryFindGroundY(landing, out groundY))
                landing.y = groundY + _terrainHeightOffset + DropLandingSafetyLift;
            else
                landing.y += DropLandingSafetyLift;
            return landing;
        }

        private bool TryGetPlaneDropPosition(out Vector3 position)
        {
            position = _player.PlayerPosition;
            if (_server == null)
                return false;

            try
            {
                Dropper plane = _server.GetSpawnedPlane();
                if (plane == null)
                    return false;

                position = plane.GetDropPosition();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TryPickupLoot()
        {
            if (_wantedLoot == null || !_room.Weapons.Contains(_wantedLoot))
                return;

            float distance = Flat(_wantedLoot.Position - _player.PlayerPosition).magnitude;
            if (distance > PickupRange)
            {
                TrackLootProgress(distance);
                return;
            }

            _room.RemoveWeapon(_wantedLoot);
            _player.AddLoot(_wantedLoot);
            _player.UpdateEquipment((byte)Pickup.EquipSlots.WeaponSlot01, (short)_wantedLoot.UniqueIdentifier, -1, -1, -1, -1, Array.Empty<short>());
            _player.ChangeWeaponType(_wantedLoot.UniqueIdentifier);
            _player.ChangeAimDownSightState(false);
            FakePlayersPlugin.BroadcastWeaponChanged(_server, _player);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} picked up {_wantedLoot.WeaponName}.");
            _equippedWeaponId = _wantedLoot.UniqueIdentifier;
            _wantedLoot = null;
            _hasWeapon = true;
        }

        private void TrackLootProgress(float distance)
        {
            if (distance < _lastLootDistance - 0.8f)
            {
                _lastLootDistance = distance;
                _lootProgressTimer = 0f;
                return;
            }

            _lootProgressTimer += Time.unscaledDeltaTime;
            if (_lootProgressTimer < 5f)
                return;

            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} gave up on blocked loot {_wantedLoot.WeaponName}.");
            _wantedLoot = null;
            _lootTimer = 0f;
            _lootProgressTimer = 0f;
            PickNewWanderTarget();
        }

        private void TickTargeting()
        {
            if (_retargetTimer <= 0f || _target == null || _target.IsDead || _target.IsDowned)
            {
                _target = FindTarget();
                string targetName = _target != null ? _target.PlayerName : "";
                if (_lastTargetName != targetName)
                {
                    FakePlayersPlugin.Log(string.IsNullOrEmpty(targetName)
                        ? $"AI dummy {_player.PlayerName} has no target."
                        : $"AI dummy {_player.PlayerName} targeting {targetName}.");
                    _lastTargetName = targetName;
                }
                _retargetTimer = 0.5f;
            }

            TrackTargetVelocity(Time.unscaledDeltaTime);

            _canSeeTarget = _target != null && HasLineOfSight(_target);
            if (_target != null)
            {
                _lastSeenTargetPosition = _target.PlayerPosition;
                _lastSeenTimer = LastSeenMemory;
                _hasLastSeenTarget = true;
            }
            else if (_lastSeenTimer <= 0f)
            {
                _hasLastSeenTarget = false;
            }
        }

        private void TrackTargetVelocity(float dt)
        {
            if (_target == null)
            {
                _hasTargetPosition = false;
                _targetVelocity = Vector3.zero;
                return;
            }

            Vector3 current = _target.PlayerPosition;
            if (_hasTargetPosition && dt > 0.001f)
            {
                Vector3 measured = (current - _lastTargetPosition) / dt;
                measured.y = 0f;
                measured = Vector3.ClampMagnitude(measured, 13f);
                _targetVelocity = Vector3.Lerp(_targetVelocity, measured, 0.35f);
            }
            else
            {
                _targetVelocity = Vector3.zero;
                _hasTargetPosition = true;
            }

            _lastTargetPosition = current;
        }

        private void TickLootChoice()
        {
            if (_hasWeapon || _lootTimer > 0f)
                return;

            NetworkGun nextLoot = FindLoot();
            if (nextLoot == null && _lootDiagnosticsTimer <= 0f)
            {
                int totalWeapons = _room != null && _room.Weapons != null ? _room.Weapons.Count : -1;
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} found no loot. Server weapon count: {totalWeapons}.");
                _lootDiagnosticsTimer = 6f;
            }

            if (nextLoot != _wantedLoot)
            {
                _wantedLoot = nextLoot;
                _lastLootDistance = float.MaxValue;
                _lootProgressTimer = 0f;
                _lastLootIndex = _wantedLoot != null ? _wantedLoot.Index : int.MinValue;
                if (_wantedLoot != null)
                    FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} moving to {_wantedLoot.WeaponName} ({_wantedLoot.UniqueIdentifier}).");
            }

            _lootTimer = 1.2f;
        }

        private void TickVehicleChoice()
        {
            if (_vehicleDecisionTimer > 0f)
                return;

            _vehicleDecisionTimer = 1.0f;
            if (_target == null || _room.Cars == null)
            {
                _wantedCar = null;
                _activeCar = null;
                return;
            }

            float targetDistance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
            if (_activeCar != null && targetDistance < 28f)
            {
                _activeCar = null;
                _wantedCar = null;
                return;
            }

            if (_activeCar != null)
                return;

            if (targetDistance < VehicleUseMinTargetDistance)
            {
                _wantedCar = null;
                return;
            }

            _wantedCar = FindNearestCar(VehicleSearchRange);
        }

        private TABGCarServer FindNearestCar(float maxDistance)
        {
            TABGCarServer best = null;
            float bestScore = maxDistance * maxDistance;
            for (int i = 0; i < _room.Cars.Count; i++)
            {
                TABGCarServer car = _room.Cars[i];
                if (car == null)
                    continue;

                float distance = Flat(car.CarPosition - _player.PlayerPosition).sqrMagnitude;
                if (distance >= bestScore)
                    continue;

                best = car;
                bestScore = distance;
            }

            return best;
        }

        private void TickGrenade()
        {
            if (_grenadeFuseTimer <= 0f && _pendingGrenadePosition != Vector3.zero)
            {
                DetonatePendingGrenade();
                _pendingGrenadePosition = Vector3.zero;
            }

            if (_grenadeCooldown > 0f || _target == null || !HasUsableWeapon())
                return;

            float distance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
            if (distance < 10f || distance > GrenadeThrowRange || !HasLineToPoint(_target.PlayerPosition + Vector3.up * 1.0f, allowGround: true, _target))
                return;

            if (UnityEngine.Random.value > Mathf.Lerp(0.16f, 0.34f, GetSkillT()))
                return;

            ThrowGrenadeAt(_target);
        }

        private void ThrowGrenadeAt(TABGPlayerServer target)
        {
            Vector3 predicted = PredictTargetPosition(target, Mathf.Lerp(0.35f, 0.65f, GetSkillT()));
            Vector3 throwOrigin = _player.PlayerPosition + Vector3.up * 1.35f;
            Vector3 direction = predicted + Vector3.up * 1.0f - throwOrigin;
            if (direction.sqrMagnitude < 0.1f)
                direction = Quaternion.Euler(0f, _player.PlayerRotation.y, 0f) * Vector3.forward;
            direction.Normalize();

            FakePlayersPlugin.BroadcastGrenadeThrow(_server, _player, AiGrenadeItemId, 1, throwOrigin, direction, sync: true);
            _pendingGrenadePosition = predicted;
            _grenadeFuseTimer = GrenadeFuseTime;
            _grenadeCooldown = UnityEngine.Random.Range(7f, 12f);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} threw grenade toward {target.PlayerName}.");
        }

        private void DetonatePendingGrenade()
        {
            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (candidate == null || candidate == _player || candidate.Bot || candidate.IsDead || candidate.IsDowned)
                    continue;

                float distance = Flat(candidate.PlayerPosition - _pendingGrenadePosition).magnitude;
                if (distance > GrenadeSplashRadius)
                    continue;

                float damage = Mathf.Lerp(9f, 23f, GetSkillT()) * (1f - Mathf.Clamp01(distance / GrenadeSplashRadius) * 0.55f);
                FakePlayersPlugin.ApplyDirectDamage(_server, _player, candidate, damage);
            }
        }

        private void DecideState()
        {
            if (_decisionTimer > 0f)
                return;

            AiState next;
            if (!_hasWeapon && _wantedLoot != null && _room.Weapons.Contains(_wantedLoot))
            {
                next = AiState.Looting;
            }
            else if (_target != null && HasUsableWeapon() && Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude <= ShootRange)
            {
                next = AiState.Fighting;
            }
            else if (_target != null)
            {
                next = AiState.Advancing;
            }
            else if (_hasLastSeenTarget)
            {
                next = AiState.Searching;
            }
            else
            {
                next = AiState.Wandering;
            }

            SetState(next, UnityEngine.Random.Range(0.75f, 1.2f));
            _decisionTimer = 0.25f;
        }

        private void SetState(AiState next, float minTime)
        {
            if (_state == next && _stateTimer > 0f)
                return;

            _state = next;
            _stateTimer = minTime;
            if (_lastLoggedState != next)
            {
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} state: {next}.");
                _lastLoggedState = next;
            }

            if (next == AiState.Fighting)
                PickCombatStrafe();
        }

        private void TrackStuck(Vector3 destination)
        {
            if (_state == AiState.Unstuck)
                return;

            if (_stuckCheckTimer > 0f)
                return;

            float moved = Flat(_player.PlayerPosition - _lastProgressPosition).magnitude;
            float distanceToDestination = Flat(destination - _player.PlayerPosition).magnitude;
            if (distanceToDestination > 7f && moved < 0.45f)
            {
                _stuckTimer += StuckCheckInterval;
            }
            else
            {
                _stuckTimer = 0f;
            }

            _lastProgressPosition = _player.PlayerPosition;
            _stuckCheckTimer = StuckCheckInterval;

            if (_stuckTimer < StuckSeconds)
                return;

            PickUnstuckTarget(destination);
            _stuckTimer = 0f;
                SetState(AiState.Unstuck, UnityEngine.Random.Range(0.8f, 1.2f));
        }

        private void PickUnstuckTarget(Vector3 blockedDestination)
        {
            Vector3 away = Flat(_player.PlayerPosition - blockedDestination);
            if (away.sqrMagnitude < 0.1f)
                away = UnityEngine.Random.insideUnitSphere;
            away = Flat(away).normalized;

            Vector3 side = Vector3.Cross(Vector3.up, away) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            Vector3 direction = (away * 0.65f + side * 0.75f).normalized;
            _unstuckTarget = _player.PlayerPosition + direction * UnityEngine.Random.Range(10f, 18f);
            _unstuckTarget.y = _player.PlayerPosition.y;
            PickMovementNoise();
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} unstuck target {_unstuckTarget}.");
        }

        private void PickCombatStrafe()
        {
            _combatStrafeSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _combatStrafeDistance = UnityEngine.Random.Range(3.2f, 6.8f);
            _combatForwardBias = UnityEngine.Random.Range(0.8f, 2.6f);
            _combatStrafeTimer = UnityEngine.Random.Range(0.35f, 0.8f);
            if (_state == AiState.Fighting && UnityEngine.Random.value < 0.55f)
                QueueJump();
        }

        private TABGPlayerServer FindTarget()
        {
            TABGPlayerServer best = null;
            float bestDistance = ChaseRange * ChaseRange;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (candidate == null || candidate == _player || candidate.Bot || candidate.IsDead || candidate.IsDowned)
                    continue;

                float distance = (candidate.PlayerPosition - _player.PlayerPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private Vector3 ChooseDestination()
        {
            if (_state == AiState.Looting && _wantedLoot != null && _room.Weapons.Contains(_wantedLoot))
                return _wantedLoot.Position;

            if (_activeCar == null && _wantedCar != null)
                return _wantedCar.CarPosition;

            Vector3 ringCenter;
            float ringRadius;
            if (TryGetRing(out ringCenter, out ringRadius))
            {
                Vector3 flat = Flat(_player.PlayerPosition - ringCenter);
                float safeRadius = ringRadius * 0.48f;
                if (flat.magnitude > safeRadius)
                    return ringCenter + flat.normalized * Mathf.Max(10f, safeRadius * 0.6f);
            }

            switch (_state)
            {
                case AiState.Fighting:
                    return ChooseCombatDestination();

                case AiState.Advancing:
                    if (_target != null)
                        return PredictTargetPosition(_target, 0.65f);
                    break;

                case AiState.Searching:
                    if (_hasLastSeenTarget)
                        return _lastSeenTargetPosition;
                    break;

                case AiState.Unstuck:
                    return _unstuckTarget;
            }

            if (_wanderTimer <= 0f || Flat(_wanderTarget - _player.PlayerPosition).magnitude < 4f)
                PickNewWanderTarget();

            return _wanderTarget;
        }

        private Vector3 ChooseCombatDestination()
        {
            if (_target == null)
                return _player.PlayerPosition;

            if (_combatStrafeTimer <= 0f)
                PickCombatStrafe();

            Vector3 toTarget = Flat(_target.PlayerPosition - _player.PlayerPosition);
            float distance = toTarget.magnitude;
            if (distance < 0.1f)
                return _player.PlayerPosition;

            Vector3 toward = toTarget.normalized;
            Vector3 strafe = Vector3.Cross(Vector3.up, toward) * _combatStrafeSign;
            Vector3 destination;
            Vector3 targetMove = Flat(_targetVelocity);
            bool targetIsRunningAway = targetMove.sqrMagnitude > 6f && Vector3.Dot(targetMove.normalized, toward) > 0.25f;
            bool hasShot = _canSeeTarget && HasShotLine(_target);
            float attackRange = Mathf.Lerp(5.5f, 7.5f, GetSkillT());

            if (!hasShot)
            {
                float step = Mathf.Clamp(distance - attackRange, 4f, 15f);
                destination = _player.PlayerPosition + toward * step + strafe * Mathf.Min(_combatStrafeDistance, 1.2f);
            }
            else if (distance < MinFightRange)
            {
                destination = _player.PlayerPosition + toward * 0.45f + strafe * (_combatStrafeDistance * 1.1f);
            }
            else if (distance > attackRange + 1.5f || targetIsRunningAway)
            {
                float step = targetIsRunningAway
                    ? Mathf.Clamp(distance - MinFightRange, 1.4f, 5.5f)
                    : Mathf.Clamp(distance - attackRange, 1.2f, 6.5f);
                destination = _player.PlayerPosition + toward * step + strafe * _combatStrafeDistance;
            }
            else
            {
                destination = _player.PlayerPosition + toward * _combatForwardBias + strafe * (_combatStrafeDistance * 1.15f);
            }

            if (UnityEngine.Random.value < 0.2f + GetSkillT() * 0.08f)
                QueueJump();

            destination.y = _player.PlayerPosition.y;
            return destination;
        }

        private void PickNewWanderTarget()
        {
            Vector3 center = _player != null ? _player.PlayerPosition : Vector3.zero;
            float radius = 45f;

            Vector3 ringCenter;
            float ringRadius;
            if (TryGetRing(out ringCenter, out ringRadius))
            {
                Vector3 currentToCenter = Flat(_player.PlayerPosition - ringCenter);
                if (currentToCenter.magnitude > ringRadius * 0.5f)
                    center = ringCenter;
                radius = Mathf.Clamp(ringRadius * 0.35f, 25f, 90f);
            }

            Vector2 random = UnityEngine.Random.insideUnitCircle * radius;
            _wanderTarget = center + new Vector3(random.x, 0f, random.y);
            if (_player != null)
            _wanderTarget.y = _player.PlayerPosition.y;
            _wanderTimer = UnityEngine.Random.Range(3f, 8f);
        }

        private bool TryFindCoverDestination(out Vector3 cover)
        {
            cover = _player.PlayerPosition;
            if (_target == null)
                return false;

            Vector3 current = _player.PlayerPosition;
            Vector3 toTarget = Flat(_target.PlayerPosition - current);
            if (toTarget.sqrMagnitude < 9f)
                return false;

            Vector3 toward = toTarget.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            float[] distances = { 7f, 11f, 15f };
            float[] sideSigns = { _combatStrafeSign, -_combatStrafeSign };

            for (int d = 0; d < distances.Length; d++)
            {
                for (int s = 0; s < sideSigns.Length; s++)
                {
                    Vector3 candidate = current + side * sideSigns[s] * distances[d] + toward * UnityEngine.Random.Range(-2f, 3f);
                    float groundY;
                    if (!TryFindGroundY(candidate, out groundY))
                        continue;
                    candidate.y = groundY + _terrainHeightOffset;

                    Vector3 moveDir = Flat(candidate - current);
                    if (moveDir.sqrMagnitude < 4f || IsBlocked(current, moveDir.normalized))
                        continue;

                    if (HasCoverFromTarget(candidate))
                    {
                        cover = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private void MoveToward(Vector3 destination, float dt)
        {
            Vector3 current = _player.PlayerPosition;
            if (_dropFinished && _lockedDropLanding != Vector3.zero && Flat(current - _lockedDropLanding).magnitude > MaxForcedPositionDrift && Time.unscaledTime - _stateTimer < 60f)
            {
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} snapped back from forced drift {current} to {_lockedDropLanding}.");
                ForceServerPosition(_lockedDropLanding);
                current = _lockedDropLanding;
            }

            TryEnterVehicle(current);

            if (_state != AiState.Fighting || !_canSeeTarget)
                destination = ResolvePathWaypoint(current, destination);
            Vector3 desired = BuildEnemyAiStyleDirection(current, destination);
            Vector3 direction = desired.sqrMagnitude > 0.1f ? desired.normalized : Vector3.zero;

            byte movement = 0;
            bool blocked = direction != Vector3.zero && IsBlocked(current, direction);
            if (blocked)
            {
                QueueJump();
                direction = FindClearDirection(current, direction);
            }

            if (direction == Vector3.zero && Flat(destination - current).magnitude > 2f)
            {
                if (_state == AiState.Fighting || _state == AiState.Advancing)
                    direction = FindClearDirection(current, Flat(destination - current).normalized, ignoreBack: true);
                if (direction == Vector3.zero)
                    direction = Flat(destination - current).normalized;
            }

            if (_state == AiState.Fighting && _target != null && direction != Vector3.zero)
                direction = KeepCombatMovementAggressive(current, direction);

            if (direction != Vector3.zero)
            {
                if (_smoothedDirection == Vector3.zero)
                    _smoothedDirection = direction;
                else
                _smoothedDirection = Vector3.Slerp(_smoothedDirection, direction, Mathf.Clamp01(dt * (_state == AiState.Fighting ? 18f : 4f))).normalized;
            }
            else
            {
                _smoothedDirection = Vector3.zero;
            }

            bool physicalMoved = TryDrivePhysicalEnemyAi(destination);

            Vector3 next = current;
            if (physicalMoved)
            {
                next = _physicalHip.position;
                next.y = ResolveTerrainY(current, next, dt);
            }
            else if (_smoothedDirection != Vector3.zero)
            {
                next += _smoothedDirection * GetCurrentMoveSpeed() * dt;
                next.y = ResolveTerrainY(current, next, dt);
            }
            else
            {
                next.y = ResolveTerrainY(current, next, dt);
            }

            if (blocked && direction == Vector3.zero)
            {
                PickNewWanderTarget();
            }

            UpdateActiveVehicle(next, dt);
            ForceServerPosition(next);
            _player.UpdateMovementDirection(_smoothedDirection);
            movement = BuildMovementFlags(_smoothedDirection, _player.PlayerRotation.y);
            if (_jumpFlagTimer > 0f)
                movement = movement.SetBit(7);
            _player.UpdateMovementType(movement);
            FaceBestInterest(_smoothedDirection);
        }

        private void ForceServerPosition(Vector3 position)
        {
            if (_player == null)
                return;

            _player.UpdatePosition(position);

            GameObject playerObject = _player.PlayerObject;
            if (playerObject == null)
                return;

            playerObject.transform.position = position;

            Hip hip = playerObject.GetComponentInChildren<Hip>();
            if (hip != null)
                hip.transform.position = position;

            Rigidbody[] bodies = playerObject.GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null)
                    continue;

                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
            }
        }

        private Vector3 KeepCombatMovementAggressive(Vector3 current, Vector3 direction)
        {
            Vector3 toTarget = Flat(_target.PlayerPosition - current);
            if (toTarget.sqrMagnitude < 0.1f)
                return direction;

            Vector3 toward = toTarget.normalized;
            float forwardDot = Vector3.Dot(direction, toward);
            if (forwardDot >= 0.12f)
                return direction;

            Vector3 side = direction - toward * forwardDot;
            if (side.sqrMagnitude < 0.04f)
                side = Vector3.Cross(Vector3.up, toward) * _combatStrafeSign;

            return (side.normalized * 1.15f + toward * 0.35f).normalized;
        }

        private void TryEnterVehicle(Vector3 current)
        {
            if (_activeCar != null || _wantedCar == null)
                return;

            if (Flat(_wantedCar.CarPosition - current).magnitude > VehicleEnterRange)
                return;

            _activeCar = _wantedCar;
            _wantedCar = null;
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} using vehicle {_activeCar.CarIndex}.");
        }

        private void UpdateActiveVehicle(Vector3 playerPosition, float dt)
        {
            if (_activeCar == null)
                return;

            Vector3 carPosition = playerPosition;
            carPosition.y -= 0.25f;
            _activeCar.UpdatePosition(carPosition);
        }

        private void TryShoot(TABGPlayerServer target)
        {
            if (!HasUsableWeapon())
            {
                StopFullAuto();
                return;
            }

            float distance = Flat(target.PlayerPosition - _player.PlayerPosition).magnitude;
            float damageRange = GetDamageRange();
            if (distance > damageRange)
            {
                StopFullAuto();
                return;
            }
            bool hasLineOfSight = HasShotLine(target);

            _player.ChangeAimDownSightState(distance > 20f);
            Vector3 exactAimPoint = GetCombatAimPoint(target, distance, addMiss: false);
            TrackAimSettle(exactAimPoint);
            FacePoint(exactAimPoint);
            FakePlayersPlugin.BroadcastPlayerUpdate(_server, _player, _player.PlayerPosition);

            if (!hasLineOfSight || _aimSettleTimer > 0f || !IsAimingAt(target, GetAimCone()))
            {
                StopFullAuto();
                return;
            }

            Vector3 aimPoint = GetCombatAimPoint(target, distance, addMiss: true);

            if (IsAutomaticWeapon(_equippedWeaponId))
            {
                if (!_isFullAutoFiring && _shootTimer > 0f)
                    return;

                TickFullAuto(target, aimPoint, hasLineOfSight);
                return;
            }

            StopFullAuto();
            if (_shootTimer > 0f)
                return;

            FakePlayersPlugin.BroadcastFire(_server, _player, aimPoint);

            float hitChance = Mathf.Lerp(GetSemiNearHitChance(), GetSemiFarHitChance(), Mathf.Clamp01(distance / damageRange));
            if (UnityEngine.Random.value < hitChance)
                FakePlayersPlugin.ApplyDirectDamage(_server, _player, target, GetSemiDamage());

            _shootTimer = UnityEngine.Random.Range(0.75f, 1.35f);
        }

        private void TickFullAuto(TABGPlayerServer target, Vector3 aimPoint, bool hasLineOfSight)
        {
            if (!HasUsableWeapon())
            {
                StopFullAuto();
                return;
            }

            if (!_isFullAutoFiring)
            {
                _isFullAutoFiring = true;
                _autoBulletsFired = 0;
                _autoBurstTimer = UnityEngine.Random.Range(0.8f, 1.6f);
                _autoDamageTimer = 0f;
                FakePlayersPlugin.BroadcastFullAutoStart(_server, _player, aimPoint);
            }

            if (_autoDamageTimer <= 0f)
            {
                float distance = Flat(target.PlayerPosition - _player.PlayerPosition).magnitude;
                float damageRange = GetDamageRange();
                if (distance > damageRange || !HasShotLine(target) || !IsAimingAt(target, GetAimCone()))
                {
                    StopFullAuto();
                    return;
                }

                _autoBulletsFired++;
                float hitChance = Mathf.Lerp(GetAutoNearHitChance(), GetAutoFarHitChance(), Mathf.Clamp01(distance / damageRange));
                if (UnityEngine.Random.value < hitChance)
                    FakePlayersPlugin.ApplyDirectDamage(_server, _player, target, GetAutoDamage());
                _autoDamageTimer = AutoBulletInterval;
            }

            if (_autoBurstTimer <= 0f)
            {
                StopFullAuto();
                _shootTimer = UnityEngine.Random.Range(0.18f, 0.45f);
            }
        }

        private void StopFullAuto()
        {
            if (!_isFullAutoFiring)
                return;

            FakePlayersPlugin.BroadcastFullAutoStop(_server, _player, Mathf.Max(1, _autoBulletsFired));
            _isFullAutoFiring = false;
            _autoBulletsFired = 0;
        }

        private NetworkGun FindLoot()
        {
            NetworkGun best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _room.Weapons.Count; i++)
            {
                NetworkGun loot = _room.Weapons[i];
                if (loot == null || !IsShootableWeapon(loot))
                    continue;

                float distance = Flat(loot.Position - _player.PlayerPosition).magnitude;
                if (distance > LootSearchRange)
                    continue;

                bool visible = HasLineToPoint(loot.Position + Vector3.up * 0.4f, allowGround: true);
                float score = distance + (visible ? 0f : 45f);
                if (_target != null)
                    score += Mathf.Clamp(Flat(loot.Position - _target.PlayerPosition).magnitude * 0.12f, 0f, 20f);
                if (_wantedLoot != null && loot.Index == _lastLootIndex)
                    score -= 8f;

                if (score < bestScore)
                {
                    best = loot;
                    bestScore = score;
                }
            }

            return best;
        }

        private void FaceBestInterest(Vector3 movementDirection)
        {
            Vector3 lookTarget;
            if (HasUsableWeapon() && _target != null && Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude <= ShootRange)
            {
                float distance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
                lookTarget = GetCombatAimPoint(_target, distance, addMiss: false);
            }
            else if (movementDirection != Vector3.zero)
            {
                lookTarget = _player.PlayerPosition + movementDirection;
            }
            else if (_wantedLoot != null && _room.Weapons.Contains(_wantedLoot))
            {
                lookTarget = _wantedLoot.Position;
            }
            else
            {
                return;
            }

            FacePoint(lookTarget);
        }

        private void FacePoint(Vector3 lookTarget)
        {
            Vector3 delta = lookTarget - (_player.PlayerPosition + Vector3.up * 1.2f);
            Vector3 flat = Flat(delta);
            if (flat.sqrMagnitude < 0.01f)
                return;

            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(delta.y, flat.magnitude) * Mathf.Rad2Deg;
            if (pitch < 0f)
                pitch += 360f;

            _player.UpdateRotation(new Vector2(pitch, yaw));
        }

        private Vector3 GetCombatAimPoint(TABGPlayerServer target, float distance, bool addMiss)
        {
            Vector3 aimPoint = PredictTargetPosition(target, Mathf.Clamp(distance / 80f, 0.08f, 0.45f)) + Vector3.up * 1.08f;
            if (!addMiss)
                return aimPoint;

            float t = Mathf.Clamp01(distance / ShootRange);
            float skillT = GetSkillT();
            float horizontalMiss = Mathf.Lerp(0.85f, 0.18f, skillT) * Mathf.Lerp(0.35f, 1.1f, t);
            float verticalMiss = Mathf.Lerp(0.18f, 0.04f, skillT) * Mathf.Lerp(0.45f, 1.0f, t);
            aimPoint += new Vector3(
                UnityEngine.Random.Range(-horizontalMiss, horizontalMiss),
                UnityEngine.Random.Range(-verticalMiss, verticalMiss),
                UnityEngine.Random.Range(-horizontalMiss, horizontalMiss));
            return aimPoint;
        }

        private Vector3 PredictTargetPosition(TABGPlayerServer target, float leadSeconds)
        {
            if (target == null)
                return _player.PlayerPosition;

            Vector3 velocity = Flat(_targetVelocity);
            return target.PlayerPosition + velocity * leadSeconds;
        }

        private Vector3 BuildEnemyAiStyleDirection(Vector3 current, Vector3 destination)
        {
            Vector3 targetPoint = destination;
            float distance = Flat(destination - current).magnitude;
            if (distance > 2f && _state != AiState.Fighting && _state != AiState.Advancing)
                targetPoint += _movementNoise * Mathf.Clamp(distance * 0.18f, 0f, 7f);

            Vector3 desired = Flat(targetPoint - current);
            if (desired.sqrMagnitude <= 0.1f)
                return Vector3.zero;
            return desired.normalized;
        }

        private Vector3 ResolvePathWaypoint(Vector3 current, Vector3 destination)
        {
            if (Flat(destination - current).sqrMagnitude <= 16f)
                return destination;

            Vector3 waypoint;
            if (!_navMeshDisabled && TryResolveNavMeshWaypoint(current, destination, out waypoint))
                return waypoint;

            return ResolveLocalAvoidWaypoint(current, destination);
        }

        private bool TryResolveNavMeshWaypoint(Vector3 current, Vector3 destination, out Vector3 waypoint)
        {
            waypoint = destination;

            try
            {
                if (_navPath == null)
                    _navPath = new NavMeshPath();

                bool rebuild = !_hasNavPath ||
                    _pathRebuildTimer <= 0f ||
                    Flat(destination - _lastPathDestination).sqrMagnitude > PathDestinationRebuildDistance * PathDestinationRebuildDistance;

                if (rebuild && !TryBuildNavPath(current, destination))
                    return false;

                Vector3[] corners = _navPath.corners;
                if (corners == null || corners.Length <= 1)
                    return false;

                _pathCornerIndex = Mathf.Clamp(_pathCornerIndex, 1, corners.Length - 1);
                while (_pathCornerIndex < corners.Length - 1 &&
                    Flat(corners[_pathCornerIndex] - current).sqrMagnitude <= PathCornerReachDistance * PathCornerReachDistance)
                {
                    _pathCornerIndex++;
                }

                waypoint = corners[_pathCornerIndex];
                waypoint.y = current.y;
                return true;
            }
            catch (Exception ex)
            {
                NoteNavFailure($"exception: {ex.GetType().Name}");
                return false;
            }
        }

        private bool TryBuildNavPath(Vector3 current, Vector3 destination)
        {
            NavMeshHit source;
            NavMeshHit target;
            if (!TrySampleNavMesh(current, out source) || !TrySampleNavMesh(destination, out target))
            {
                NoteNavFailure("sample failed");
                return false;
            }

            if (!NavMesh.CalculatePath(source.position, target.position, NavMesh.AllAreas, _navPath) ||
                _navPath.status == NavMeshPathStatus.PathInvalid ||
                _navPath.corners == null ||
                _navPath.corners.Length <= 1)
            {
                NoteNavFailure($"path {_navPath.status}");
                return false;
            }

            _hasNavPath = true;
            _pathCornerIndex = 1;
            _pathRebuildTimer = PathRebuildInterval;
            _lastPathDestination = destination;
            _navFailureCount = 0;
            return true;
        }

        private static bool TrySampleNavMesh(Vector3 position, out NavMeshHit hit)
        {
            return NavMesh.SamplePosition(position, out hit, NavMeshSampleDistance, NavMesh.AllAreas);
        }

        private void NoteNavFailure(string reason)
        {
            _hasNavPath = false;
            _pathRebuildTimer = Mathf.Max(_pathRebuildTimer, 1.0f);
            _navFailureCount++;
            if (_navFailureCount < 8)
                return;

            _navMeshDisabled = true;
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} disabling NavMesh pathing ({reason}); using local steering fallback.");
        }

        private Vector3 ResolveLocalAvoidWaypoint(Vector3 current, Vector3 destination)
        {
            if (_localAvoidTimer > 0f && Flat(_localAvoidWaypoint - current).sqrMagnitude > 6.25f)
                return _localAvoidWaypoint;

            Vector3 desired = Flat(destination - current);
            if (desired.sqrMagnitude <= 0.1f)
                return destination;

            desired.Normalize();
            if (!IsBlocked(current, desired))
                return destination;

            float currentDistance = Flat(destination - current).magnitude;
            float[] distances = { 9f, 15f, 23f };
            float[] angles = { 25f, -25f, 45f, -45f, 70f, -70f, 105f, -105f, 145f, -145f };
            Vector3 best = destination;
            float bestScore = float.MaxValue;

            for (int d = 0; d < distances.Length; d++)
            {
                for (int a = 0; a < angles.Length; a++)
                {
                    Vector3 direction = Quaternion.Euler(0f, angles[a], 0f) * desired;
                    if (IsBlocked(current, direction))
                        continue;

                    Vector3 candidate = current + direction.normalized * distances[d];
                    float groundY;
                    if (!TryFindGroundY(candidate, out groundY))
                        continue;

                    candidate.y = groundY + _terrainHeightOffset;
                    Vector3 candidateDirection = Flat(candidate - current);
                    if (candidateDirection.sqrMagnitude < 4f || IsBlocked(current, candidateDirection.normalized))
                        continue;

                    float destinationDistance = Flat(destination - candidate).magnitude;
                    if (destinationDistance > currentDistance + 10f && Mathf.Abs(angles[a]) < 110f)
                        continue;

                    float score = destinationDistance + Mathf.Abs(angles[a]) * 0.08f + distances[d] * 0.04f;
                    if (score >= bestScore)
                        continue;

                    best = candidate;
                    bestScore = score;
                }
            }

            if (bestScore < float.MaxValue)
            {
                _localAvoidWaypoint = best;
                _localAvoidTimer = UnityEngine.Random.Range(0.9f, 1.4f);
                QueueJump();
                return _localAvoidWaypoint;
            }

            return destination;
        }

        private void PickMovementNoise()
        {
            Vector2 random = UnityEngine.Random.insideUnitCircle;
            _movementNoise = new Vector3(random.x, 0f, random.y);
            _movementNoiseTimer = UnityEngine.Random.Range(1.0f, 3.0f);
        }

        private byte BuildMovementFlags(Vector3 direction, float yaw)
        {
            if (direction == Vector3.zero)
                return 0;

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float forwardDot = Vector3.Dot(Flat(forward).normalized, direction.normalized);
            float rightDot = Vector3.Dot(Flat(right).normalized, direction.normalized);
            byte flags = 0;
            if (forwardDot > 0.35f)
                flags = flags.SetBit(0);
            else if (forwardDot < -0.55f)
                flags = flags.SetBit(1);

            if (Mathf.Abs(rightDot) > 0.35f)
                flags = flags.SetBit(2);

            return flags;
        }

        private float GetCurrentMoveSpeed()
        {
            if (_activeCar != null)
                return MoveSpeed;

            float skillBonus = Mathf.Lerp(-0.25f, 0.2f, GetSkillT());
            if (_state == AiState.Fighting)
            {
                if (_target != null && _canSeeTarget)
                {
                    float distance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
                    if (distance <= PreferredFightRange)
                        return Mathf.Lerp(2.35f, 3.1f, GetSkillT());
                }

                return Mathf.Lerp(2.8f, 3.35f, GetSkillT());
            }
            if (_state == AiState.Fighting || _state == AiState.Advancing || _state == AiState.Searching)
                return CombatMoveSpeed + skillBonus;
            return MoveSpeed + skillBonus;
        }

        private bool HasUsableWeapon()
        {
            return _hasWeapon && IsShootableWeapon(_equippedWeaponId);
        }

        private float GetSkillT()
        {
            return Mathf.Clamp01((_skillLevel - 1) / 4f);
        }

        private float GetDamageRange()
        {
            return Mathf.Lerp(10f, 24f, GetSkillT());
        }

        private float GetAimCone()
        {
            return Mathf.Lerp(28f, 42f, GetSkillT());
        }

        private float GetSemiDamage()
        {
            return Mathf.Lerp(3.2f, DamagePerShot, GetSkillT());
        }

        private float GetAutoDamage()
        {
            return Mathf.Lerp(1.3f, AutoDamagePerBullet, GetSkillT());
        }

        private float GetSemiNearHitChance()
        {
            return Mathf.Lerp(0.48f, 0.78f, GetSkillT());
        }

        private float GetSemiFarHitChance()
        {
            return Mathf.Lerp(0.18f, 0.38f, GetSkillT());
        }

        private float GetAutoNearHitChance()
        {
            return Mathf.Lerp(0.28f, 0.58f, GetSkillT());
        }

        private float GetAutoFarHitChance()
        {
            return Mathf.Lerp(0.1f, 0.28f, GetSkillT());
        }

        private void RemoveBuiltInBotController()
        {
            if (_player == null || _player.PlayerObject == null)
                return;

            ServerNetworkBot builtInBot = _player.PlayerObject.GetComponent<ServerNetworkBot>();
            if (builtInBot != null)
                Destroy(builtInBot);
        }

        private void InitPhysicalEnemyAiHooks()
        {
            if (_player == null || _player.PlayerObject == null)
                return;

            _physicalInput = _player.PlayerObject.GetComponent<InputHandler>();
            Hip hip = _player.PlayerObject.GetComponentInChildren<Hip>();
            RotationTarget rotationTarget = _player.PlayerObject.GetComponentInChildren<RotationTarget>();
            _physicalHip = hip != null ? hip.transform : null;
            _physicalRotationTarget = rotationTarget != null ? rotationTarget.transform : null;

            FakePlayersPlugin.Log(
                $"AI dummy {_player.PlayerName} physical hooks: input={_physicalInput != null}, hip={_physicalHip != null}, rotationTarget={_physicalRotationTarget != null}.");
        }

        private void InitTerrainOffset()
        {
            float groundY;
            if (!TryFindGroundY(_player.PlayerPosition, out groundY))
                return;

            _terrainHeightOffset = Mathf.Clamp(_player.PlayerPosition.y - groundY, 0.7f, 2.2f);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} terrain height offset: {_terrainHeightOffset:0.00}.");
        }

        private bool TryDrivePhysicalEnemyAi(Vector3 destination)
        {
            ClearPhysicalInput();
            return false;

#pragma warning disable CS0162
            if (_physicalInput == null || _physicalHip == null || _physicalRotationTarget == null)
                return false;

            if (_state == AiState.Fighting)
            {
                ClearPhysicalInput();
                return false;
            }

            Vector3 targetPoint = destination;
            float distance = Vector3.Distance(targetPoint, _physicalHip.position);
            if (distance > 2f)
                targetPoint += _movementNoise * Mathf.Clamp(distance * 0.22f, 0f, 10f);

            Vector3 direction = targetPoint - _physicalHip.position;
            if (direction.sqrMagnitude <= 0.1f)
            {
                ClearPhysicalInput();
                return true;
            }

            direction.Normalize();
            _physicalInput.inputMovementDirection = direction;
            _physicalInput.isWalkingForward = true;
            _physicalInput.isWalkingBackward = false;
            _physicalInput.isStrafing = false;
            _physicalInput.isSpringting = true;
            if (Flat(direction).sqrMagnitude > 0.01f)
                _physicalRotationTarget.rotation = Quaternion.LookRotation(Flat(direction).normalized);

            return Vector3.Distance(_physicalHip.position, _player.PlayerPosition) > 0.04f;
#pragma warning restore CS0162
        }

        private void ClearPhysicalInput()
        {
            if (_physicalInput == null)
                return;

            _physicalInput.inputMovementDirection = Vector3.zero;
            _physicalInput.isWalkingForward = false;
            _physicalInput.isWalkingBackward = false;
            _physicalInput.isStrafing = false;
            _physicalInput.isSpringting = false;
        }

        private void QueueJump()
        {
            if (_jumpCooldown > 0f)
                return;

            _jumpCooldown = UnityEngine.Random.Range(1.4f, 2.6f);
            _jumpFlagTimer = 0f;
            _jumpVisualTimer = 0f;
        }

        private float ResolveTerrainY(Vector3 current, Vector3 next, float dt)
        {
            float groundY;
            if (!TryFindGroundY(next, out groundY))
                return current.y;

            float targetY = groundY + _terrainHeightOffset;
            if (current.y > targetY + 1.5f || next.y > targetY + 1.5f)
                return targetY;

            if (_jumpVisualTimer > 0f)
            {
                float t = Mathf.Clamp01(_jumpVisualTimer / 0.42f);
                targetY += Mathf.Sin(t * Mathf.PI) * 0.85f;
            }
            float maxDelta = MaxVerticalSpeed * Mathf.Max(dt, 0.016f);
            return Mathf.MoveTowards(current.y, targetY, maxDelta);
        }

        private bool IsBlocked(Vector3 position, Vector3 direction)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                position + Vector3.up * 1.1f,
                WallProbeRadius,
                direction,
                WallProbeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                if (IsOwnCollider(hits[i].collider))
                    continue;
                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.65f)
                    return true;
            }

            return false;
        }

        private Vector3 FindClearDirection(Vector3 position, Vector3 desiredDirection)
        {
            return FindClearDirection(position, desiredDirection, ignoreBack: false);
        }

        private Vector3 FindClearDirection(Vector3 position, Vector3 desiredDirection, bool ignoreBack)
        {
            float[] angles = { -35f, 35f, -70f, 70f, -110f, 110f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, angles[i], 0f) * desiredDirection;
                if (!IsBlocked(position, candidate))
                    return candidate.normalized;
            }

            if (ignoreBack)
                return Vector3.zero;

            Vector3 back = -desiredDirection;
            return IsBlocked(position, back) ? Vector3.zero : back.normalized;
        }

        private bool HasLineOfSight(TABGPlayerServer target)
        {
            if (target == null)
                return false;

            return HasLineToPoint(target.PlayerPosition + Vector3.up * 1.15f, allowGround: false, target);
        }

        private bool HasShotLine(TABGPlayerServer target)
        {
            if (target == null)
                return false;

            if (Flat(target.PlayerPosition - _player.PlayerPosition).magnitude < 6f)
                return true;

            return HasLineToPoint(target.PlayerPosition + Vector3.up * 1.05f, allowGround: true, target);
        }

        private bool HasCoverFromTarget(Vector3 candidate)
        {
            if (_target == null)
                return false;

            Vector3 start = _target.PlayerPosition + Vector3.up * 1.2f;
            Vector3 end = candidate + Vector3.up * 1.1f;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < 0.1f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                start,
                delta / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (IsOwnCollider(collider) || IsTargetCollider(collider, _target))
                    continue;
                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.7f)
                    return true;
            }

            return false;
        }

        private void TrackAimSettle(Vector3 aimPoint)
        {
            Vector3 direction = Flat(aimPoint - (_player.PlayerPosition + Vector3.up * 1.2f));
            if (direction.sqrMagnitude < 0.01f)
                return;

            direction.Normalize();
            if (!_hasAimDirection || Vector3.Angle(_lastAimDirection, direction) > Mathf.Lerp(10f, 22f, GetSkillT()))
                _aimSettleTimer = Mathf.Lerp(0.32f, 0.1f, GetSkillT());

            _lastAimDirection = direction;
            _hasAimDirection = true;
        }

        private bool IsAimingAt(TABGPlayerServer target, float maxAngle)
        {
            if (target == null)
                return false;

            Vector3 toTarget = Flat((target.PlayerPosition + Vector3.up * 1.05f) - (_player.PlayerPosition + Vector3.up * 1.2f));
            if (toTarget.sqrMagnitude < 0.01f)
                return true;

            Vector3 forward = Quaternion.Euler(new Vector3(_player.PlayerRotation.x, _player.PlayerRotation.y, 0f)) * Vector3.forward;
            return Vector3.Angle(Flat(forward), toTarget) <= maxAngle;
        }

        private bool HasLineToPoint(Vector3 point, bool allowGround, TABGPlayerServer target = null)
        {
            Vector3 start = _player.PlayerPosition + Vector3.up * 1.35f;
            Vector3 end = point;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < 0.1f)
                return true;

            RaycastHit[] hits = Physics.RaycastAll(
                start,
                delta / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (IsOwnCollider(collider) || IsTargetCollider(collider, target))
                    continue;
                float upDot = Vector3.Dot(hits[i].normal, Vector3.up);
                if (allowGround && upDot >= 0.55f)
                    continue;
                if (upDot < 0.75f)
                    return false;
            }

            return true;
        }

        private bool IsOwnCollider(Collider collider)
        {
            return collider != null && _player != null && _player.PlayerObject != null && collider.transform.IsChildOf(_player.PlayerObject.transform);
        }

        private static bool IsTargetCollider(Collider collider, TABGPlayerServer target)
        {
            return collider != null && target != null && target.PlayerObject != null && collider.transform.IsChildOf(target.PlayerObject.transform);
        }

        private bool TryFindGroundY(Vector3 nearPosition, out float y)
        {
            y = nearPosition.y;
            Vector3 origin = nearPosition + Vector3.up * TerrainProbeUp;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                TerrainProbeUp + TerrainProbeDown,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsOwnCollider(hit.collider) || Vector3.Dot(hit.normal, Vector3.up) < 0.25f)
                    continue;

                float distance = Mathf.Abs((hit.point.y + _terrainHeightOffset) - nearPosition.y);
                if (distance < bestDistance)
                {
                    y = hit.point.y;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsShootableWeapon(NetworkGun loot)
        {
            return loot != null && IsShootableWeapon(loot.UniqueIdentifier);
        }

        private static bool IsShootableWeapon(int weaponId)
        {
            return ShootableWeaponIds.Contains(weaponId);
        }

        private static bool IsAutomaticWeapon(int weaponId)
        {
            return AutomaticWeaponIds.Contains(weaponId);
        }

        private void OnDestroy()
        {
            StopFullAuto();
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static bool TryGetRing(out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0f;

            TheRing ring = TheRing.Instance;
            if (ring == null)
                return false;

            center = ring.GetCurrentRingPosition();
            radius = ring.GetCurrentRingSize() * 0.5f;
            return radius > 1f;
        }
    }
}
