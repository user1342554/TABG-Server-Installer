using System;
using System.Collections.Generic;
using System.Globalization;
using Landfall.Network;
using UnityEngine;
using UnityEngine.AI;
using static TabgInstaller.FakePlayers.AiDummyCatalog;

namespace TabgInstaller.FakePlayers
{
    internal class AiDummyController : MonoBehaviour
    {
        private enum AiState
        {
            Warmup,
            Looting,
            Scavenging,
            Advancing,
            Fighting,
            Evading,
            Searching,
            FollowingTeam,
            FollowingOrder,
            Reviving,
            Wandering,
            Unstuck,
            Dropping
        }

        private enum AiAction
        {
            None,
            Fight,
            Reload,
            TakeCover,
            Push,
            RunToRing,
            LootWeapon,
            LootAmmo,
            Heal,
            FollowTeam,
            FollowOrder,
            ReviveTeamMate,
            SearchLastSeen,
            Flee
        }

        private struct UtilityOption
        {
            public AiAction Action;
            public AiState State;
            public float Score;
            public string Reason;

            public UtilityOption(AiAction action, AiState state, float score, string reason)
            {
                Action = action;
                State = state;
                Score = score;
                Reason = reason;
            }
        }

        private struct RingContext
        {
            public bool HasRing;
            public Vector3 Center;
            public float Radius;
            public float Distance;
            public float Fraction;
            public float Danger;
            public bool IsMoving;
            public bool IsClosing;
            public bool IsLateGame;
            public bool ShouldRotate;
        }

        private const float PlayableMinX = -900f;
        private const float PlayableMaxX = 850f;
        private const float PlayableMinZ = -850f;
        private const float PlayableMaxZ = 750f;
        private const float MaxFairGunDamageRange = 58f;
        private const float RingUnsafeRadiusFraction = 1.03f;
        private const float RingDestinationRadiusFraction = 0.82f;
        private const float RingEarlyRotateFraction = 0.68f;
        private const float RingHardRotateFraction = 0.86f;
        private const float LateGameRingRadius = 95f;
        private const float BotRingDamagePerSecond = 8f;
        private const float BotRingDamageTickSeconds = 1f;
        private const float TeamContextRefreshSeconds = 0.2f;
        private const float TeamFollowComfortDistance = 9f;
        private const float TeamFollowUrgentDistance = 30f;
        private const float TeamOrderArrivalDistance = 5f;
        private const float TeamCombatAssistRange = 110f;
        private const float TeamThreatMemorySeconds = 8f;
        private const float TeamReviveApproachDistance = 1.75f;
        private const float TeamReviveBreakDistance = 3f;
        private const float VanillaReviveHealthPerSecond = 10f;
        private const float EnemyPingLostGraceSeconds = 1.25f;
        private const float EnemyPingMinUpdateSeconds = 1.75f;
        private const float EnemyPingMovementThreshold = 8f;
        private const float UtilitySwitchMargin = 9f;
        private const float UtilityCurrentActionBonus = 5f;
        private const float SearchGiveUpSeconds = 7.5f;
        private const float HealHealthThreshold = 64f;
        private const float HealCriticalThreshold = 42f;
        private const float RepositionAfterShotsDistance = 4.5f;
        private const float BlockedLootCooldownSeconds = 24f;
        private const float EmergencyLootSearchRange = 2600f;
        private const int MaxBlockedLootEntries = 40;
        private static readonly int[] AmmoLootItemIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

        private struct GroundProbe
        {
            public bool Found;
            public bool BadTerrain;
            public float Y;
            public Vector3 Normal;
            public string SurfaceName;
        }

        private struct ServerProjectile
        {
            public Vector3 Position;
            public readonly Vector3 Direction;
            public readonly float MaxRange;
            public readonly float Speed;
            public readonly float Damage;
            public float RemainingRange;
            public float DistanceTravelled;

            public ServerProjectile(Vector3 position, Vector3 direction, float range, float speed, float damage)
            {
                Position = position;
                Direction = direction;
                MaxRange = range;
                Speed = speed;
                Damage = damage;
                RemainingRange = range;
                DistanceTravelled = 0f;
            }
        }

        private ServerClient _server;
        private GameRoom _room;
        private TABGPlayerServer _player;
        private TABGPlayerServer _target;
        private TABGPlayerServer _threatTarget;
        private TABGPlayerServer _teamLeader;
        private TABGPlayerServer _reviveTarget;
        private TABGPlayerServer _activeReviveTarget;
        private TABGPlayerServer _enemyPingTarget;
        private NetworkGun _wantedLoot;
        private TABGCarServer _wantedCar;
        private TABGCarServer _activeCar;
        private TABGCarServerSeat _activeSeat;
        private InputHandler _physicalInput;
        private Transform _physicalHip;
        private Transform _physicalRotationTarget;
        private AiState _state = AiState.Warmup;
        private AiState _lastLoggedState = AiState.Warmup;
        private Vector3 _wanderTarget;
        private Vector3 _movementNoise;
        private Vector3 _smoothedDirection;
        private Vector3 _lastSeenTargetPosition;
        private Vector3 _lastKnownThreatPosition;
        private Vector3 _searchDestination;
        private Vector3 _lastTargetPosition;
        private Vector3 _targetVelocity;
        private Vector3 _lastAimDirection;
        private Vector3 _unstuckTarget;
        private Vector3 _lastProgressPosition;
        private Vector3 _lastPathDestination;
        private Vector3 _localAvoidWaypoint;
        private Vector3 _dropTarget;
        private Vector3 _lockedDropLanding;
        private Vector3 _evadeTarget;
        private Vector3 _coverTarget;
        private Vector3 _currentDestination;
        private Vector3 _currentPoiTarget;
        private Vector3 _teamOrderPosition;
        private Vector3 _enemyPingPosition;
        private float _terrainHeightOffset = 1.15f;
        private float _retargetTimer;
        private float _wanderTimer;
        private float _movementNoiseTimer;
        private float _decisionTimer;
        private float _stateTimer;
        private float _lastSeenTimer;
        private float _threatMemoryTimer;
        private float _soundMemoryTimer;
        private float _lootThreatSuppressionTimer;
        private float _unarmedPanicTimer;
        private float _searchRepathTimer;
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
        private float _autoShotTimer;
        private float _aimSettleTimer;
        private float _warmupTimer;
        private float _lootProgressTimer;
        private float _pathRebuildTimer;
        private float _localAvoidTimer;
        private float _lootDiagnosticsTimer;
        private float _vehicleDecisionTimer;
        private float _grenadeCooldown;
        private float _grenadeFuseTimer;
        private float _evadeTimer;
        private float _coverTimer;
        private float _reactionDelayTimer;
        private float _targetStickinessTimer;
        private float _reloadTimer;
        private float _healTimer;
        private float _peekDelayTimer;
        private float _postShotRepositionTimer;
        private float _searchGiveUpTimer;
        private float _fireAnimationTimer;
        private float _burstShotTimer;
        private float _poiTimer;
        private float _dropPositionLockTimer;
        private float _physicalHookRetryTimer;
        private float _lastLootDistance = float.MaxValue;
        private float _bestPlaneDropDistance = float.MaxValue;
        private float _lastRingDanger;
        private float _ringDamageTimer;
        private float _teamContextTimer;
        private float _reviveTimer;
        private float _enemyPingLostTimer;
        private float _enemyPingUpdateCooldown;
        private float _currentUtilityScore;
        private Vector3 _pendingGrenadePosition;
        private Vector3 _lastFirePosition;
        private string _lastTargetName;
        private string _lastPhysicalHookLog;
        private string _stateReason = "warmup";
        private string _topUtilityScores = "none";
        private string _wantedLootKind = "none";
        private int _lastLootIndex = int.MinValue;
        private int _equippedWeaponId = -1;
        private int _equippedWeaponScore;
        private int _magazineAmmo;
        private int _reserveAmmo;
        private int _burstShotsRemaining;
        private int _grenadeItemId = AiGrenadeItemId;
        private int _grenadeCount;
        private int _healingItemId = -1;
        private int _healingItemCount;
        private int _autoBulletsFired;
        private int _sameFireSpotShots;
        private int _searchSweepIndex;
        private int _lastGunshotSequence;
        private int _skillLevel = 1;
        private int _pathCornerIndex = 1;
        private int _navFailureCount;
        private int _teamOrderSequence;
        private int _completedTeamOrderSequence;
        private bool _canSeeTarget;
        private bool _hasLastSeenTarget;
        private bool _hasThreatMemory;
        private bool _hasTargetPosition;
        private bool _hasAimDirection;
        private bool _hasWeapon;
        private bool _hasNavPath;
        private bool _navMeshDisabled;
        private bool _isFullAutoFiring;
        private bool _isReloading;
        private bool _isHealing;
        private bool _hasPoiTarget;
        private bool _dropStarted;
        private bool _dropFinished;
        private bool _hasTeamOrder;
        private bool _teamOrderIsPing;
        private bool _reviveStarted;
        private bool _enemyPingActive;
        private AiAction _currentAction = AiAction.None;
        private AiAction _lastLoggedAction = AiAction.None;
        private NavMeshPath _navPath;
        private WeaponProfile _weaponProfile;
        private static readonly Dictionary<int, byte> LootClaims = new Dictionary<int, byte>();
        private readonly List<ServerProjectile> _serverProjectiles = new List<ServerProjectile>();
        private readonly Dictionary<byte, Vector3> _lastSoundPositions = new Dictionary<byte, Vector3>();
        private readonly Dictionary<int, float> _blockedLootUntil = new Dictionary<int, float>();
        private readonly List<int> _blockedLootScratch = new List<int>();

        public void Init(ServerClient server, TABGPlayerServer player, int skillLevel = 1)
        {
            _server = server;
            _room = server.GameRoomReference;
            _player = player;
            _skillLevel = Mathf.Clamp(skillLevel, 1, 5);
            _warmupTimer = WarmupTime;
            _weaponProfile = GetWeaponProfile(-1, null);

            // Fake players cannot play the client-side Gulag. Mark their one-time
            // boss attempt as consumed so any server-authoritative death remains final.
            if (!_player.HasDoneBossFight)
            {
                _player.EnterBoss();
                _player.ExitBoss();
            }

            RemoveBuiltInBotController();
            InitPhysicalEnemyAiHooks();
            InitTerrainOffset();
            _navPath = new NavMeshPath();
            _dropTarget = PickDropTarget();
            if (!ShouldHandleRoundDrop())
                StartDrop();
            _lastGunshotSequence = FakePlayersPlugin.GunshotSoundSequence;
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
                CancelTeamRevive();
                ClearEnemyPing();
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
            _threatMemoryTimer -= dt;
            _soundMemoryTimer -= dt;
            _lootThreatSuppressionTimer -= dt;
            _unarmedPanicTimer -= dt;
            _searchRepathTimer -= dt;
            _stuckCheckTimer -= dt;
            _combatStrafeTimer -= dt;
            _jumpCooldown -= dt;
            _jumpFlagTimer -= dt;
            _jumpVisualTimer -= dt;
            _lootTimer -= dt;
            _networkTimer -= dt;
            _shootTimer -= dt;
            _autoBurstTimer -= dt;
            _autoShotTimer -= dt;
            _aimSettleTimer -= dt;
            _warmupTimer -= dt;
            _pathRebuildTimer -= dt;
            _localAvoidTimer -= dt;
            _lootDiagnosticsTimer -= dt;
            _vehicleDecisionTimer -= dt;
            _grenadeCooldown -= dt;
            _grenadeFuseTimer -= dt;
            _evadeTimer -= dt;
            _coverTimer -= dt;
            _reactionDelayTimer -= dt;
            _targetStickinessTimer -= dt;
            _reloadTimer -= dt;
            _healTimer -= dt;
            _peekDelayTimer -= dt;
            _postShotRepositionTimer -= dt;
            _searchGiveUpTimer -= dt;
            _fireAnimationTimer -= dt;
            _burstShotTimer -= dt;
            _poiTimer -= dt;
            _physicalHookRetryTimer -= dt;
            _teamContextTimer -= dt;
            _enemyPingUpdateCooldown -= dt;

            TickServerProjectiles(dt);

            if ((_physicalInput == null || _physicalHip == null || _physicalRotationTarget == null) && _physicalHookRetryTimer <= 0f)
            {
                InitPhysicalEnemyAiHooks();
                _physicalHookRetryTimer = 1.0f;
            }

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

            if (_player.IsDowned)
            {
                CancelTeamRevive();
                ClearEnemyPing();
                StopFullAuto();
                ClearPhysicalInput();
                _player.UpdateMovementDirection(Vector3.zero);
                _player.UpdateMovementType(0);
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

            TickBotRingDamage(dt);
            if (_player.IsDead)
                return;

            TickTeamContext();

            if (_movementNoiseTimer <= 0f)
                PickMovementNoise();

            TickTargeting();
            TickEnemyPing(dt);
            TickSoundAwareness(dt);
            TickLootChoice();
            TickVehicleChoice();
            DecideState();
            TickReload();
            TickHealing(dt);
            TickGrenade();

            Vector3 destination = ChooseDestination();
            _currentDestination = destination;
            TrackStuck(destination);
            MoveToward(destination, dt);
            TickTeamRevive(dt);

            if (_state != AiState.Reviving)
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
            for (int i = 0; i < 40; i++)
            {
                Vector3 target = DropTargets[UnityEngine.Random.Range(0, DropTargets.Length)];
                Vector2 offset = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(4f, 22f);
                target.x += offset.x;
                target.z += offset.y;

                if (TryResolveSafeGround(target, out target))
                    return target;
            }

            for (int i = 0; i < PoiTargets.Length; i++)
            {
                Vector3 target = PoiTargets[UnityEngine.Random.Range(0, PoiTargets.Length)];
                Vector2 offset = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(8f, 30f);
                target.x += offset.x;
                target.z += offset.y;

                if (TryResolveSafeGround(target, out target))
                    return target;
            }

            Vector3 fallback = _player != null ? _player.PlayerPosition : Vector3.zero;
            if (TryResolveSafeGround(fallback, out fallback))
                return fallback;

            return _player != null ? _player.PlayerPosition : Vector3.zero;
        }

        private bool TryResolveSafeGround(Vector3 target, out Vector3 grounded)
        {
            grounded = target;

            GroundProbe probe;
            if (!TryFindGroundInfo(target, out probe) || probe.BadTerrain)
                return false;

            grounded.y = probe.Y + _terrainHeightOffset;
            return true;
        }

        private static Vector3 BuildDropStartNearLanding(Vector3 landing)
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

            Pickup pickup = GetPickup(_wantedLoot);
            Pickup.WeaponType pickupType = pickup != null ? pickup.weaponType : Pickup.WeaponType.Weapon;
            _room.RemoveWeapon(_wantedLoot);
            _player.AddLoot(_wantedLoot);

            try
            {
                _room.RemoveProjectileSyncIndex(_wantedLoot.Index);
                _room.CurrentGameMode?.HandlePlayerPickup(_player, _wantedLoot);
            }
            catch { }

            byte pickupSlot = GetPickupSlot(pickupType);
            FakePlayersPlugin.BroadcastPickupAccepted(_server, _player, _wantedLoot, pickupSlot);

            if (pickupType == Pickup.WeaponType.Weapon)
                EquipWeapon(_wantedLoot);
            else if (pickupType == Pickup.WeaponType.Grenade)
            {
                _grenadeItemId = _wantedLoot.UniqueIdentifier;
                _grenadeCount = Mathf.Min(_grenadeCount + Mathf.Max(1, _wantedLoot.Quantity), 3);
            }
            else if (pickupType == Pickup.WeaponType.Health)
            {
                _healingItemId = _wantedLoot.UniqueIdentifier;
                _healingItemCount = Mathf.Min(_healingItemCount + Mathf.Max(1, _wantedLoot.Quantity), 4);
            }
            else if (pickupType == Pickup.WeaponType.Ammo)
            {
                _reserveAmmo = Mathf.Min(_reserveAmmo + Mathf.Max(12, _wantedLoot.Quantity * 18), _weaponProfile.MagazineSize * 8);
            }

            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} picked up {_wantedLoot.WeaponName} ({pickupType}).");
            ReleaseLootClaim();
            _wantedLoot = null;
            _lootTimer = 0.35f;
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
            MarkLootTemporarilyBlocked(_wantedLoot);
            ReleaseLootClaim();
            _wantedLoot = null;
            _lootTimer = 0.35f;
            _lootProgressTimer = 0f;
            PickNewWanderTarget();
        }

        private void TickTeamContext()
        {
            if (_teamContextTimer > 0f || _room == null || _room.Players == null)
                return;

            _teamContextTimer = TeamContextRefreshSeconds;
            _teamLeader = FindTeamLeader();
            _reviveTarget = FindTeamMateToRevive();

            FakePlayersPlugin.TeamMoveOrder order;
            if (FakePlayersPlugin.TryGetTeamMoveOrder(_room, _player.GroupIndex, out order) &&
                order.Sequence != _completedTeamOrderSequence)
            {
                _teamOrderSequence = order.Sequence;
                _teamOrderPosition = ResolveTeamDestination(order.Position);
                _teamOrderIsPing = order.IsPing;
                _hasTeamOrder = true;

                if (Flat(_teamOrderPosition - _player.PlayerPosition).magnitude <= TeamOrderArrivalDistance)
                {
                    _completedTeamOrderSequence = order.Sequence;
                    _hasTeamOrder = false;
                    FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} reached teammate {(order.IsPing ? "ping" : "map marker")}.");
                }
            }
            else
            {
                _hasTeamOrder = false;
            }

            TABGPlayerServer teamThreat = FindRecentTeamThreat();
            if (teamThreat != null)
                RememberThreat(teamThreat, teamThreat.PlayerPosition, TeamThreatMemorySeconds, suppressLoot: true);
        }

        private TABGPlayerServer FindTeamLeader()
        {
            TABGPlayerServer orderSender = null;
            FakePlayersPlugin.TeamMoveOrder order;
            if (FakePlayersPlugin.TryGetTeamMoveOrder(_room, _player.GroupIndex, out order))
                orderSender = _room.FindPlayer(order.SenderIndex);

            if (IsActiveHumanTeamMate(orderSender))
                return orderSender;

            TABGPlayerServer closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (!IsActiveHumanTeamMate(candidate))
                    continue;

                float distance = Flat(candidate.PlayerPosition - _player.PlayerPosition).sqrMagnitude;
                if (distance >= closestDistance)
                    continue;

                closest = candidate;
                closestDistance = distance;
            }

            return closest;
        }

        private TABGPlayerServer FindTeamMateToRevive()
        {
            TABGPlayerServer best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (candidate == null || candidate == _player || candidate.GroupIndex != _player.GroupIndex ||
                    candidate.IsDead || !candidate.IsDowned || !candidate.HasDropped)
                    continue;
                if (candidate.IsBeingRevived && candidate.Reviver != _player)
                    continue;

                float score = Flat(candidate.PlayerPosition - _player.PlayerPosition).magnitude;
                if (candidate.Bot)
                    score += 200f;
                if (candidate == _activeReviveTarget || candidate == _reviveTarget)
                    score -= 6f;

                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private TABGPlayerServer FindRecentTeamThreat()
        {
            TABGPlayerServer best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer teamMate = _room.Players[i];
                if (teamMate == null || teamMate == _player || teamMate.GroupIndex != _player.GroupIndex || teamMate.IsDead)
                    continue;
                if (teamMate.LastAttacker == byte.MaxValue || Time.time - teamMate.LastAttackTime > TeamThreatMemorySeconds)
                    continue;

                TABGPlayerServer attacker = _room.FindPlayer(teamMate.LastAttacker);
                if (!IsValidEnemyTarget(attacker))
                    continue;

                float botDistance = Flat(attacker.PlayerPosition - _player.PlayerPosition).magnitude;
                float teamDistance = Flat(attacker.PlayerPosition - teamMate.PlayerPosition).magnitude;
                if (botDistance > TeamCombatAssistRange && teamDistance > TeamCombatAssistRange)
                    continue;

                float score = Mathf.Min(botDistance, teamDistance) + Mathf.Max(0f, Time.time - teamMate.LastAttackTime) * 3f;
                if (score < bestScore)
                {
                    best = attacker;
                    bestScore = score;
                }
            }

            return best;
        }

        private bool IsActiveHumanTeamMate(TABGPlayerServer candidate)
        {
            return candidate != null &&
                candidate != _player &&
                !candidate.Bot &&
                candidate.GroupIndex == _player.GroupIndex &&
                !candidate.IsDead &&
                !candidate.IsDowned &&
                candidate.HasDropped;
        }

        private Vector3 ResolveTeamDestination(Vector3 destination)
        {
            if (IsOutsidePlayableBounds(destination))
                return _player.PlayerPosition;

            Vector3 ringCenter;
            float ringRadius;
            if (_room.CurrentGameState == GameState.Started && TryGetRing(out ringCenter, out ringRadius))
            {
                Vector3 fromCenter = Flat(destination - ringCenter);
                float safeRadius = Mathf.Max(8f, ringRadius * 0.9f);
                if (fromCenter.magnitude > safeRadius)
                {
                    if (fromCenter.sqrMagnitude < 0.01f)
                        fromCenter = Vector3.forward;
                    destination = ringCenter + fromCenter.normalized * safeRadius;
                }
            }

            float groundY;
            if (TryFindGroundY(destination, out groundY))
                destination.y = groundY + _terrainHeightOffset;
            else
                destination.y = _player.PlayerPosition.y;
            return destination;
        }

        private void TickTargeting()
        {
            if (_searchGiveUpTimer <= 0f && _state == AiState.Searching && !_canSeeTarget)
            {
                _hasThreatMemory = false;
                _hasLastSeenTarget = false;
                _threatTarget = null;
                _target = null;
                _targetStickinessTimer = 0f;
            }

            if (_threatMemoryTimer <= 0f && _lastSeenTimer <= 0f)
            {
                _hasThreatMemory = false;
                _threatTarget = null;
            }

            if (_retargetTimer <= 0f || !IsValidEnemyTarget(_target))
            {
                TABGPlayerServer previous = _target;
                TABGPlayerServer next = FindTarget();
                if (previous != next)
                {
                    _reactionDelayTimer = UnityEngine.Random.Range(
                        Mathf.Lerp(0.55f, 0.16f, GetSkillT()),
                        Mathf.Lerp(0.95f, 0.32f, GetSkillT()));
                    _targetStickinessTimer = TargetStickinessSeconds;
                    StopFullAuto();
                }

                _target = next;
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

            _canSeeTarget = _target != null && HasLineOfSight(_target);
            if (_target != null && _canSeeTarget)
            {
                TrackTargetVelocity(Time.unscaledDeltaTime);
                Vector3 predicted = ProjectThreatPosition(_target.PlayerPosition + Flat(_targetVelocity) * Mathf.Lerp(0.2f, 0.55f, GetSkillT()));
                RememberThreat(_target, predicted, ThreatMemorySeconds, !HasUsableWeapon() || _player.Health <= LowHealthRetreatThreshold);
            }
            else if (_target != null && _hasThreatMemory && _threatMemoryTimer > 0f)
            {
                Vector3 drift = Flat(_targetVelocity) * Mathf.Min(Time.unscaledDeltaTime, 0.12f);
                if (drift.sqrMagnitude > 0.001f)
                    _lastKnownThreatPosition = ProjectThreatPosition(_lastKnownThreatPosition + drift);

                _lastSeenTargetPosition = _lastKnownThreatPosition;
                _hasLastSeenTarget = true;
                _targetVelocity = Vector3.Lerp(_targetVelocity, Vector3.zero, Mathf.Clamp01(Time.unscaledDeltaTime * 0.5f));
                _hasTargetPosition = false;
            }
            else if (_lastSeenTimer <= 0f && _threatMemoryTimer <= 0f)
            {
                _hasLastSeenTarget = false;
                _hasThreatMemory = false;
                _threatTarget = null;
                if (_target != null && !_canSeeTarget)
                    _target = null;
            }
            else
            {
                _hasTargetPosition = false;
                _targetVelocity = Vector3.zero;
            }
        }

        private void TickEnemyPing(float dt)
        {
            TABGPlayerServer visibleEnemy = _target != null && _canSeeTarget && IsValidEnemyTarget(_target)
                ? _target
                : null;

            if (visibleEnemy != null && HasHumanTeamMate() && CanPublishEnemyPing(visibleEnemy))
            {
                _enemyPingLostTimer = 0f;
                Vector3 position = visibleEnemy.PlayerPosition + Vector3.up * 0.9f;
                bool targetChanged = _enemyPingTarget != visibleEnemy;
                bool movedEnough = Flat(position - _enemyPingPosition).magnitude >= EnemyPingMovementThreshold;
                if (!_enemyPingActive || targetChanged || (movedEnough && _enemyPingUpdateCooldown <= 0f))
                {
                    ServerMessages.SendEnemyPing(_server, _player, position);
                    if (!_enemyPingActive || targetChanged)
                        FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} pinged enemy {visibleEnemy.PlayerName} for group {_player.GroupIndex}.");

                    _enemyPingActive = true;
                    _enemyPingTarget = visibleEnemy;
                    _enemyPingPosition = position;
                    _enemyPingUpdateCooldown = EnemyPingMinUpdateSeconds;
                }

                return;
            }

            if (!_enemyPingActive)
                return;

            bool invalidTarget = !IsValidEnemyTarget(_enemyPingTarget);
            bool yieldedToTeamMate = visibleEnemy != null && !CanPublishEnemyPing(visibleEnemy);
            _enemyPingLostTimer += Mathf.Max(0f, dt);
            if (invalidTarget || yieldedToTeamMate || _enemyPingLostTimer >= EnemyPingLostGraceSeconds)
                ClearEnemyPing();
        }

        private bool HasHumanTeamMate()
        {
            if (_room == null || _room.Players == null)
                return false;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (candidate != null && !candidate.Bot && candidate.GroupIndex == _player.GroupIndex && !candidate.IsDead)
                    return true;
            }

            return false;
        }

        private bool CanPublishEnemyPing(TABGPlayerServer enemy)
        {
            if (_room == null || _room.Players == null || enemy == null)
                return false;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (candidate == null || candidate == _player || candidate.GroupIndex != _player.GroupIndex ||
                    candidate.PlayerIndex >= _player.PlayerIndex || candidate.PlayerObject == null)
                    continue;

                AiDummyController controller = candidate.PlayerObject.GetComponent<AiDummyController>();
                if (controller != null && controller._canSeeTarget && controller._target == enemy)
                    return false;
            }

            return true;
        }

        private void ClearEnemyPing()
        {
            if (_enemyPingActive && _server != null && _player != null)
                ServerMessages.RemoveEnemyPing(_server, _player);

            _enemyPingActive = false;
            _enemyPingTarget = null;
            _enemyPingPosition = Vector3.zero;
            _enemyPingLostTimer = 0f;
            _enemyPingUpdateCooldown = 0f;
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

        private void TickSoundAwareness(float dt)
        {
            if (_room == null || _room.Players == null)
                return;

            ConsumeGunshotSounds();
            TrackMovementSounds(dt);
        }

        private void ConsumeGunshotSounds()
        {
            List<FakePlayersPlugin.GunshotSoundEvent> sounds = FakePlayersPlugin.GunshotSounds;
            if (sounds == null || sounds.Count == 0)
            {
                _lastGunshotSequence = FakePlayersPlugin.GunshotSoundSequence;
                return;
            }

            int newestSequence = _lastGunshotSequence;
            for (int i = 0; i < sounds.Count; i++)
            {
                FakePlayersPlugin.GunshotSoundEvent sound = sounds[i];
                if (sound.Sequence <= _lastGunshotSequence)
                    continue;

                newestSequence = Mathf.Max(newestSequence, sound.Sequence);
                if (sound.ShooterIndex == _player.PlayerIndex)
                    continue;

                TABGPlayerServer shooter = _room.FindPlayer(sound.ShooterIndex);
                if (!IsValidEnemyTarget(shooter))
                    continue;

                float distance = Flat(sound.Position - _player.PlayerPosition).magnitude;
                if (distance > GunshotHearRange)
                    continue;

                bool seesShooter = HasLineOfSight(shooter);
                float uncertainty = seesShooter ? 0f : Mathf.Lerp(13f, 4f, GetSkillT()) * Mathf.Clamp01(distance / GunshotHearRange);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * uncertainty;
                Vector3 heardPosition = ProjectThreatPosition((seesShooter ? shooter.PlayerPosition : sound.Position) + new Vector3(offset.x, 0f, offset.y));
                RememberThreat(shooter, heardPosition, SoundThreatMemorySeconds, suppressLoot: true);
                _soundMemoryTimer = SoundThreatMemorySeconds;
                float soundReaction = UnityEngine.Random.Range(Mathf.Lerp(0.65f, 0.18f, GetSkillT()), Mathf.Lerp(1.05f, 0.38f, GetSkillT()));
                _reactionDelayTimer = _reactionDelayTimer > 0f ? Mathf.Min(_reactionDelayTimer, soundReaction) : soundReaction;
            }

            _lastGunshotSequence = newestSequence;
        }

        private void TrackMovementSounds(float dt)
        {
            if (dt <= 0.001f)
                return;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (!IsValidEnemyTarget(candidate))
                    continue;

                Vector3 previous;
                bool hadPrevious = _lastSoundPositions.TryGetValue(candidate.PlayerIndex, out previous);
                _lastSoundPositions[candidate.PlayerIndex] = candidate.PlayerPosition;
                if (!hadPrevious)
                    continue;

                float distance = Flat(candidate.PlayerPosition - _player.PlayerPosition).magnitude;
                if (distance > MovementHearRange || (_target == candidate && _canSeeTarget))
                    continue;

                float speed = Flat(candidate.PlayerPosition - previous).magnitude / dt;
                if (speed < MovementHearSpeed || speed > 28f)
                    continue;

                if (UnityEngine.Random.value > Mathf.Lerp(0.16f, 0.42f, GetSkillT()))
                    continue;

                float uncertainty = Mathf.Lerp(8f, 2.5f, GetSkillT()) * Mathf.Clamp01(distance / MovementHearRange);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * uncertainty;
                Vector3 heardPosition = ProjectThreatPosition(candidate.PlayerPosition + new Vector3(offset.x, 0f, offset.y));
                RememberThreat(candidate, heardPosition, Mathf.Lerp(3.2f, 5.5f, GetSkillT()), suppressLoot: true);
                _soundMemoryTimer = Mathf.Max(_soundMemoryTimer, 2.2f);
            }
        }

        private void RememberThreat(TABGPlayerServer target, Vector3 position, float seconds, bool suppressLoot)
        {
            if (!IsValidEnemyTarget(target))
                return;

            _target = target;
            _threatTarget = target;
            _lastKnownThreatPosition = ProjectThreatPosition(position);
            _lastSeenTargetPosition = _lastKnownThreatPosition;
            _hasLastSeenTarget = true;
            _hasThreatMemory = true;
            _lastSeenTimer = Mathf.Max(_lastSeenTimer, Mathf.Min(seconds, ThreatMemorySeconds));
            _threatMemoryTimer = Mathf.Max(_threatMemoryTimer, seconds);
            _targetStickinessTimer = Mathf.Max(_targetStickinessTimer, TargetStickinessSeconds);
            _hasPoiTarget = false;
            _searchRepathTimer = 0f;
            _searchGiveUpTimer = Mathf.Max(_searchGiveUpTimer, SearchGiveUpSeconds);
            _searchSweepIndex = 0;

            float threatDistance = Flat(_lastKnownThreatPosition - _player.PlayerPosition).magnitude;
            bool unarmed = !HasUsableWeapon();
            if (!suppressLoot)
                return;
            if (unarmed && threatDistance > UnarmedDangerRange)
                return;

            _lootThreatSuppressionTimer = Mathf.Max(_lootThreatSuppressionTimer, LootThreatSuppressionSeconds);
            if (unarmed)
                _unarmedPanicTimer = Mathf.Max(_unarmedPanicTimer, UnarmedPanicSeconds);
        }

        private Vector3 ProjectThreatPosition(Vector3 position)
        {
            float groundY;
            if (TryFindGroundY(position, out groundY))
                position.y = groundY + _terrainHeightOffset;
            else if (_player != null)
                position.y = _player.PlayerPosition.y;

            return position;
        }

        private void TickLootChoice()
        {
            if (_lootTimer > 0f)
                return;

            if (_state == AiState.Fighting && _target != null && _canSeeTarget && !NeedsAmmo())
                return;

            if (HasActiveThreatMemory() && !HasUsableWeapon() && _lootThreatSuppressionTimer > 0f)
            {
                _lootTimer = 0.35f;
                return;
            }

            if (HasActiveThreatMemory() && HasUsableWeapon() && !NeedsAmmo() && (_state == AiState.Searching || _state == AiState.Evading))
            {
                _lootTimer = 0.65f;
                return;
            }

            NetworkGun nextLoot = FindLoot();
            if (nextLoot == null && _lootDiagnosticsTimer <= 0f)
            {
                int totalWeapons = _room != null && _room.Weapons != null ? _room.Weapons.Count : -1;
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} found no loot. Server weapon count: {totalWeapons}.");
                _lootDiagnosticsTimer = 6f;
            }

            if (nextLoot == null && !HasActiveThreatMemory() && (_target == null || !_canSeeTarget))
                PickUsefulPoiTarget();

            if (nextLoot != _wantedLoot)
            {
                ReleaseLootClaim();
                _wantedLoot = nextLoot;
                _lastLootDistance = float.MaxValue;
                _lootProgressTimer = 0f;
                _lastLootIndex = _wantedLoot != null ? _wantedLoot.Index : int.MinValue;
                if (_wantedLoot != null)
                {
                    ClaimLoot(_wantedLoot);
                    _hasPoiTarget = false;
                    FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} moving to {_wantedLoot.WeaponName} ({_wantedLoot.UniqueIdentifier}).");
                }
            }

            _lootTimer = 1.2f;
        }

        private void TickVehicleChoice()
        {
            if (_vehicleDecisionTimer > 0f)
                return;

            _vehicleDecisionTimer = 1.0f;
            if (_target == null || !_canSeeTarget || _room.Cars == null)
            {
                _wantedCar = null;
                LeaveVehicle();
                return;
            }

            float targetDistance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
            if (_activeCar != null && targetDistance < 28f)
            {
                _wantedCar = null;
                LeaveVehicle();
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
                if (FindUsableSeat(car) == null)
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
            if (!EnableGrenadeThrows)
                return;

            if (_grenadeFuseTimer <= 0f && _pendingGrenadePosition != Vector3.zero)
            {
                DetonatePendingGrenade();
                _pendingGrenadePosition = Vector3.zero;
            }

            if (_grenadeCooldown > 0f || _grenadeCount <= 0 || _target == null || !_canSeeTarget || !HasUsableWeapon())
                return;

            float distance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
            if (distance < 10f || distance > GrenadeThrowRange || !HasLineToPoint(_target.PlayerPosition + Vector3.up * 1.0f, allowGround: true, _target))
                return;

            if (UnityEngine.Random.value > Mathf.Lerp(0.16f, 0.34f, GetSkillT()))
                return;

            ThrowGrenadeAt(_target);
        }

        private void TickHealing(float dt)
        {
            if (_isHealing)
            {
                StopFullAuto();
                if (_target != null && _canSeeTarget && !HasCoverFromTarget(_player.PlayerPosition))
                {
                    _isHealing = false;
                    _healTimer = 0f;
                    return;
                }

                if (_healTimer > 0f)
                    return;

                _isHealing = false;
                if (_healingItemCount <= 0 || _healingItemId < 0 || _player.Health >= HealHealthThreshold)
                    return;

                _healingItemCount--;
                _player.RemoveLoot(_healingItemId, 1);
                float healed = Mathf.Lerp(18f, 32f, GetSkillT());
                if (_player.Health <= HealCriticalThreshold)
                    healed += 8f;
                float newHealth = Mathf.Min(100f, _player.Health + healed);
                FakePlayersPlugin.ApplyHeal(_server, _player, newHealth);
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} used healing item; health {newHealth:0}.");
                return;
            }

            if (_currentAction != AiAction.Heal || _healingItemCount <= 0 || _healingItemId < 0 || _player.Health >= HealHealthThreshold)
                return;

            if (_target != null && _canSeeTarget && !HasCoverFromTarget(_player.PlayerPosition))
                return;

            StopFullAuto();
            _isHealing = true;
            _healTimer = Mathf.Lerp(1.55f, 0.85f, GetSkillT());
        }

        private void ThrowGrenadeAt(TABGPlayerServer target)
        {
            Vector3 predicted = PredictTargetPosition(target, Mathf.Lerp(0.35f, 0.65f, GetSkillT()));
            Vector3 throwOrigin = _player.PlayerPosition + Vector3.up * 1.35f;
            Vector3 direction = predicted + Vector3.up * 1.0f - throwOrigin;
            if (direction.sqrMagnitude < 0.1f)
                direction = Quaternion.Euler(0f, _player.PlayerRotation.y, 0f) * Vector3.forward;
            direction.Normalize();

            _grenadeCount--;
            _player.RemoveLoot(_grenadeItemId, 1);
            FakePlayersPlugin.BroadcastGrenadeThrow(_server, _player, _grenadeItemId, 1, throwOrigin, direction, sync: true);
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
                if (!IsValidEnemyTarget(candidate))
                    continue;

                float distance = Flat(candidate.PlayerPosition - _pendingGrenadePosition).magnitude;
                if (distance > GrenadeSplashRadius)
                    continue;
                if (!HasExplosionLine(candidate))
                    continue;

                if (EnableGrenadeDamage)
                {
                    float damage = Mathf.Lerp(9f, 23f, GetSkillT()) * (1f - Mathf.Clamp01(distance / GrenadeSplashRadius) * 0.55f);
                    FakePlayersPlugin.ApplyDamage(_server, _player, candidate, damage);
                }
            }
        }

        private void DecideState()
        {
            if (_decisionTimer > 0f)
                return;

            UtilityOption next = SelectUtilityAction();
            _currentAction = next.Action;
            _currentUtilityScore = next.Score;
            _stateReason = next.Reason;
            SetState(next.State, GetActionMinimumTime(next.Action));
            if (_lastLoggedAction != next.Action)
            {
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} action: {next.Action} ({next.Reason}); top={_topUtilityScores}.");
                _lastLoggedAction = next.Action;
            }

            _decisionTimer = 0.25f;
        }

        private UtilityOption SelectUtilityAction()
        {
            List<UtilityOption> options = BuildUtilityOptions();
            if (options.Count == 0)
                return new UtilityOption(AiAction.SearchLastSeen, AiState.Wandering, 0f, "no-options");

            options.Sort((a, b) => b.Score.CompareTo(a.Score));
            _topUtilityScores = FormatTopUtilityScores(options);

            UtilityOption best = options[0];
            UtilityOption current;
            if (_currentAction != AiAction.None && TryFindUtilityOption(options, _currentAction, out current))
            {
                float currentScore = current.Score + UtilityCurrentActionBonus;
                bool currentStillRelevant = current.Score > 4f && _stateTimer > 0f;
                if (currentStillRelevant && best.Action != _currentAction && best.Score < currentScore + UtilitySwitchMargin)
                {
                    current.Score = currentScore;
                    current.Reason += ", hysteresis";
                    return current;
                }
            }

            return best;
        }

        private List<UtilityOption> BuildUtilityOptions()
        {
            var options = new List<UtilityOption>(13);
            bool hasLoot = _wantedLoot != null && _room.Weapons.Contains(_wantedLoot);
            bool hasThreat = HasActiveThreatMemory();
            bool hasVisibleTarget = _target != null && _canSeeTarget;
            bool hasUsableWeapon = HasUsableWeapon();
            bool hasCombatWeapon = HasCombatWeapon();
            bool needsReload = hasCombatWeapon && _magazineAmmo <= 0 && _reserveAmmo > 0;
            bool needsAmmo = NeedsAmmo();
            bool canRiskLoot = CanRiskUnarmedLoot(hasLoot);
            Vector3 threatPosition;
            bool hasThreatPosition = TryGetThreatPosition(out threatPosition);
            float threatDistance = hasThreatPosition ? Flat(threatPosition - _player.PlayerPosition).magnitude : float.MaxValue;
            float targetDistance = _target != null ? Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude : threatDistance;
            bool hasShot = hasVisibleTarget && _target != null && HasShotLine(_target);
            RingContext ring = GetRingContext();
            _lastRingDanger = ring.Danger;

            Pickup lootPickup = hasLoot ? GetPickup(_wantedLoot) : null;
            Pickup.WeaponType lootType = lootPickup != null ? lootPickup.weaponType : Pickup.WeaponType.OtherConsumable;
            _wantedLootKind = hasLoot ? GetLootDebugKind(_wantedLoot, lootPickup) : "none";

            float reviveScore = ScoreReviveTeamMate(hasVisibleTarget, targetDistance, ring);
            options.Add(new UtilityOption(
                AiAction.ReviveTeamMate,
                AiState.Reviving,
                reviveScore,
                _reviveTarget != null ? $"revive {_reviveTarget.PlayerName}" : "no downed teammate"));

            float orderScore = ScoreFollowOrder(hasThreat, hasVisibleTarget, ring);
            options.Add(new UtilityOption(
                AiAction.FollowOrder,
                AiState.FollowingOrder,
                orderScore,
                _hasTeamOrder ? $"{(_teamOrderIsPing ? "ping" : "marker")} order {_teamOrderSequence}" : "no teammate order"));

            float followScore = ScoreFollowTeam(hasThreat, hasVisibleTarget, ring);
            options.Add(new UtilityOption(
                AiAction.FollowTeam,
                AiState.FollowingTeam,
                followScore,
                _teamLeader != null ? $"stay near {_teamLeader.PlayerName}" : "no human teammate"));

            float fightScore = ScoreFight(hasVisibleTarget, hasUsableWeapon, needsReload, targetDistance, hasShot, ring, threatPosition);
            options.Add(new UtilityOption(AiAction.Fight, AiState.Fighting, fightScore, GetFightReason(targetDistance, hasShot)));

            float reloadScore = ScoreReload(needsReload, hasThreat, hasVisibleTarget, ring);
            options.Add(new UtilityOption(AiAction.Reload, AiState.Evading, reloadScore, needsReload ? "empty mag, reserve ammo" : "no reload needed"));

            float coverScore = ScoreTakeCover(hasThreat, hasVisibleTarget, hasShot, threatDistance, needsReload, ring);
            options.Add(new UtilityOption(AiAction.TakeCover, AiState.Evading, coverScore, GetCoverReason(hasVisibleTarget, hasShot, needsReload)));

            float pushScore = ScorePush(hasVisibleTarget, hasThreatPosition, hasUsableWeapon, targetDistance, ring, threatPosition);
            options.Add(new UtilityOption(AiAction.Push, targetDistance <= GetDamageRange() ? AiState.Fighting : AiState.Advancing, pushScore, GetPushReason(targetDistance)));

            float ringScore = ScoreRunToRing(ring, hasVisibleTarget, threatPosition);
            options.Add(new UtilityOption(AiAction.RunToRing, AiState.Scavenging, ringScore, GetRingReason(ring)));

            float lootWeaponScore = ScoreLootAction(hasLoot, lootType, Pickup.WeaponType.Weapon, hasThreat, hasVisibleTarget, needsAmmo, ring, canRiskLoot);
            options.Add(new UtilityOption(AiAction.LootWeapon, hasUsableWeapon ? AiState.Scavenging : AiState.Looting, lootWeaponScore, GetLootReason(lootType, Pickup.WeaponType.Weapon)));

            float lootAmmoScore = ScoreLootAction(hasLoot, lootType, Pickup.WeaponType.Ammo, hasThreat, hasVisibleTarget, needsAmmo, ring, canRiskLoot);
            options.Add(new UtilityOption(AiAction.LootAmmo, AiState.Scavenging, lootAmmoScore, GetLootReason(lootType, Pickup.WeaponType.Ammo)));

            bool wantedHealthLoot = hasLoot && lootType == Pickup.WeaponType.Health && ScoreLootValue(_wantedLoot, lootPickup) > 0f;
            float healScore = ScoreHeal(hasThreat, hasVisibleTarget, ring, wantedHealthLoot);
            options.Add(new UtilityOption(AiAction.Heal, wantedHealthLoot ? AiState.Scavenging : (hasThreat ? AiState.Evading : AiState.Wandering), healScore, GetHealReason(hasVisibleTarget, wantedHealthLoot)));

            float searchScore = ScoreSearchLastSeen(hasThreat, hasVisibleTarget, hasUsableWeapon, ring);
            options.Add(new UtilityOption(AiAction.SearchLastSeen, hasThreat ? AiState.Searching : (_hasPoiTarget ? AiState.Scavenging : AiState.Wandering), searchScore, GetSearchReason(hasThreat)));

            float fleeScore = ScoreFlee(hasThreat, hasThreatPosition, hasUsableWeapon, threatDistance, ring, hasLoot, canRiskLoot);
            options.Add(new UtilityOption(AiAction.Flee, AiState.Evading, fleeScore, GetFleeReason(threatDistance)));

            return options;
        }

        private float ScoreReviveTeamMate(bool hasVisibleTarget, float enemyDistance, RingContext ring)
        {
            TABGPlayerServer reviveCandidate = _reviveStarted ? _activeReviveTarget : _reviveTarget;
            if (!IsValidReviveTarget(reviveCandidate))
                return 0f;

            float distance = Flat(reviveCandidate.PlayerPosition - _player.PlayerPosition).magnitude;
            float score = 230f - Mathf.Clamp(distance * 0.5f, 0f, 80f);
            if (hasVisibleTarget && enemyDistance < 12f)
                score -= 85f;
            if (ring.Danger > 0.8f)
                score -= 75f;
            if (_reviveStarted)
                score += 30f;
            return Mathf.Max(0f, score);
        }

        private float ScoreFollowOrder(bool hasThreat, bool hasVisibleTarget, RingContext ring)
        {
            if (!_hasTeamOrder)
                return 0f;

            float distance = Flat(_teamOrderPosition - _player.PlayerPosition).magnitude;
            if (distance <= TeamOrderArrivalDistance)
                return 0f;

            float score = 170f + Mathf.Clamp(distance * 0.35f, 0f, 30f);
            if (hasVisibleTarget)
                score -= 110f;
            else if (hasThreat)
                score -= 60f;
            if (ring.Danger > 0.7f)
                score -= 35f;
            return Mathf.Max(0f, score);
        }

        private float ScoreFollowTeam(bool hasThreat, bool hasVisibleTarget, RingContext ring)
        {
            if (_teamLeader == null || _hasTeamOrder)
                return 0f;

            float distance = Flat(_teamLeader.PlayerPosition - _player.PlayerPosition).magnitude;
            if (distance <= TeamFollowComfortDistance)
                return 4f;

            float score = distance >= TeamFollowUrgentDistance
                ? 112f + Mathf.Clamp((distance - TeamFollowUrgentDistance) * 0.3f, 0f, 24f)
                : 58f + (distance - TeamFollowComfortDistance) * 1.6f;
            if (hasVisibleTarget)
                score -= 58f;
            else if (hasThreat)
                score -= 25f;
            if (ring.Danger > 0.75f)
                score -= 28f;
            return Mathf.Max(0f, score);
        }

        private float ScoreFight(bool hasVisibleTarget, bool hasUsableWeapon, bool needsReload, float targetDistance, bool hasShot, RingContext ring, Vector3 threatPosition)
        {
            if (!hasVisibleTarget || !hasUsableWeapon || needsReload || _isReloading || _isHealing)
                return 0f;

            float damageRange = GetDamageRange();
            if (targetDistance > damageRange + GetWeaponRangeTolerance())
                return _weaponProfile.CombatClass == WeaponCombatClass.Shotgun || _weaponProfile.CombatClass == WeaponCombatClass.Pistol ? 4f : 16f;

            float score = 38f + GetWeaponRangeFit(targetDistance) * 34f;
            if (hasShot)
                score += 15f;
            else
                score -= 18f;

            if (_player.Health <= LowHealthRetreatThreshold)
                score -= Mathf.Lerp(24f, 12f, GetSkillT());
            if (_peekDelayTimer > 0f)
                score -= 18f;
            if (_postShotRepositionTimer > 0f)
                score -= 20f;
            if (ring.Danger > 0.55f && IsThreatWorseRingSide(ring, threatPosition))
                score -= 28f;

            switch (_weaponProfile.CombatClass)
            {
                case WeaponCombatClass.Shotgun:
                    if (targetDistance > _weaponProfile.MaxRange)
                        score -= 35f;
                    else if (targetDistance <= _weaponProfile.PreferredRange + 4f)
                        score += 16f;
                    break;
                case WeaponCombatClass.Sniper:
                    if (targetDistance < GetMinimumFightRange() + 4f)
                        score -= 45f;
                    else if (hasShot && _aimSettleTimer <= 0f)
                        score += 14f;
                    break;
                case WeaponCombatClass.Smg:
                case WeaponCombatClass.AssaultRifle:
                case WeaponCombatClass.Lmg:
                    if (targetDistance >= GetMinimumFightRange() && targetDistance <= GetPreferredFightRange() + 12f)
                        score += 10f;
                    break;
                case WeaponCombatClass.Pistol:
                    if (targetDistance > _weaponProfile.PreferredRange + 8f)
                        score -= 30f;
                    break;
            }

            return Mathf.Max(0f, score);
        }

        private float ScoreReload(bool needsReload, bool hasThreat, bool hasVisibleTarget, RingContext ring)
        {
            if (!needsReload && !_isReloading)
                return 0f;

            float score = _isReloading ? 83f : 72f;
            if (hasThreat)
                score += 10f;
            if (hasVisibleTarget && !HasCoverFromTarget(_player.PlayerPosition))
                score -= 10f;
            if (ring.Danger > 0.7f)
                score += 8f;
            return score;
        }

        private float ScoreTakeCover(bool hasThreat, bool hasVisibleTarget, bool hasShot, float threatDistance, bool needsReload, RingContext ring)
        {
            if (!hasThreat && !hasVisibleTarget && !_isReloading && !_isHealing)
                return ring.IsLateGame ? 12f : 0f;

            float score = 22f;
            if (hasVisibleTarget)
                score += 20f;
            if (!hasShot && hasVisibleTarget)
                score += 20f;
            if (_player.Health <= LowHealthRetreatThreshold)
                score += 26f;
            if (needsReload || _isReloading || _isHealing)
                score += 34f;
            if (_weaponProfile.CombatClass == WeaponCombatClass.Sniper && threatDistance < GetMinimumFightRange() + 5f)
                score += 22f;
            if (ring.IsLateGame)
                score += 10f;
            return Mathf.Max(0f, score - ring.Danger * 12f);
        }

        private float ScorePush(bool hasVisibleTarget, bool hasThreatPosition, bool hasUsableWeapon, float targetDistance, RingContext ring, Vector3 threatPosition)
        {
            if ((!hasVisibleTarget && !hasThreatPosition) || _isReloading || _isHealing)
                return 0f;

            if (ring.Danger > 0.5f && IsThreatWorseRingSide(ring, threatPosition))
                return 0f;

            if (!hasUsableWeapon)
                return 0f;

            float score = 0f;
            switch (_weaponProfile.CombatClass)
            {
                case WeaponCombatClass.Shotgun:
                    if (targetDistance > _weaponProfile.MaxRange + 16f)
                        score = 8f;
                    else
                        score = 48f + Mathf.Clamp(targetDistance - _weaponProfile.PreferredRange, 0f, 22f);
                    break;
                case WeaponCombatClass.Smg:
                    if (targetDistance > GetPreferredFightRange() + 8f && targetDistance <= GetDamageRange())
                        score = 36f;
                    break;
                case WeaponCombatClass.AssaultRifle:
                case WeaponCombatClass.Lmg:
                    if (targetDistance > GetPreferredFightRange() + 15f && targetDistance <= GetDamageRange())
                        score = 24f;
                    break;
            }

            if (_player.Health <= LowHealthRetreatThreshold)
                score -= 22f;
            return Mathf.Max(0f, score);
        }

        private float ScoreRunToRing(RingContext ring, bool hasVisibleTarget, Vector3 threatPosition)
        {
            if (!ring.HasRing)
                return 0f;
            if (!ring.ShouldRotate)
                return 0f;

            float score = ring.Danger * 72f;
            if (ring.ShouldRotate)
                score += 24f;
            if (ring.IsClosing)
                score += 14f;
            if (hasVisibleTarget && IsThreatWorseRingSide(ring, threatPosition))
                score += 22f;
            if (ring.IsLateGame && ring.Fraction > 0.56f)
                score += 10f;
            return Mathf.Max(0f, score);
        }

        private float ScoreLootAction(
            bool hasLoot,
            Pickup.WeaponType lootType,
            Pickup.WeaponType wantedType,
            bool hasThreat,
            bool hasVisibleTarget,
            bool needsAmmo,
            RingContext ring,
            bool canRiskLoot)
        {
            if (!hasLoot || lootType != wantedType || _isReloading || _isHealing)
                return 0f;

            float distance = Flat(_wantedLoot.Position - _player.PlayerPosition).magnitude;
            float lootValue = ScoreLootValue(_wantedLoot, GetPickup(_wantedLoot));
            if (lootValue <= 0f)
                return 0f;

            float score = Mathf.Clamp(lootValue, 0f, 150f) - Mathf.Clamp(distance * 0.18f, 0f, 32f);
            if (wantedType == Pickup.WeaponType.Weapon && !_hasWeapon)
                score += 36f;
            if (wantedType == Pickup.WeaponType.Ammo && needsAmmo)
                score += 46f;
            if (hasVisibleTarget)
                score -= wantedType == Pickup.WeaponType.Ammo && needsAmmo ? 18f : 38f;
            if (hasThreat && !canRiskLoot)
                score -= 46f;
            if (ring.Danger > 0.45f)
                score -= ring.Danger * 46f;
            if (IsLootClaimedByOther(_wantedLoot.Index))
                score -= 55f;
            return Mathf.Max(0f, score);
        }

        private float ScoreHeal(bool hasThreat, bool hasVisibleTarget, RingContext ring, bool wantedHealthLoot)
        {
            if ((_healingItemCount <= 0 || _healingItemId < 0) && !wantedHealthLoot)
                return _isHealing ? 80f : 0f;
            if (_player.Health >= HealHealthThreshold)
                return _isHealing ? 80f : 0f;

            bool watched = hasVisibleTarget && !HasCoverFromTarget(_player.PlayerPosition);
            if (wantedHealthLoot && watched && _player.Health > HealCriticalThreshold)
                return 0f;

            float missing = 100f - _player.Health;
            float score = 24f + Mathf.Clamp(missing * 0.82f, 0f, 58f);
            if (_player.Health <= HealCriticalThreshold)
                score += 22f;
            if (wantedHealthLoot)
                score += _healingItemCount <= 0 ? 34f : 8f;
            if (_isHealing)
                score += 30f;
            if (hasThreat)
                score += 8f;
            if (watched)
                score -= 26f;
            if (ring.Danger > 0.7f)
                score -= 10f;
            return Mathf.Max(0f, score);
        }

        private float ScoreSearchLastSeen(bool hasThreat, bool hasVisibleTarget, bool hasUsableWeapon, RingContext ring)
        {
            if (hasVisibleTarget)
                return 0f;

            if (!hasThreat && !_hasLastSeenTarget)
                return _hasPoiTarget ? 22f : 10f;

            if (_searchGiveUpTimer <= 0f)
                return _hasPoiTarget ? 16f : 8f;

            float score = hasUsableWeapon ? 48f : 18f;
            score += Mathf.Clamp(_searchGiveUpTimer * 2.2f, 0f, 15f);
            if (ring.Danger > 0.5f)
                score -= ring.Danger * 35f;
            return Mathf.Max(0f, score);
        }

        private float ScoreFlee(bool hasThreat, bool hasThreatPosition, bool hasUsableWeapon, float threatDistance, RingContext ring, bool hasLoot, bool canRiskLoot)
        {
            if (!hasThreat && !hasThreatPosition)
                return 0f;

            float score = 0f;
            if (ShouldRetreat())
                score += 74f;
            if (!hasUsableWeapon)
                score += threatDistance <= UnarmedDangerRange ? 64f : 24f;
            if (_player.Health <= CriticalHealthRetreatThreshold)
                score += 42f;
            else if (_player.Health <= LowHealthRetreatThreshold)
                score += 18f;
            if (hasLoot && canRiskLoot)
                score -= 18f;
            if (ring.Danger > 0.65f)
                score -= 14f;
            return Mathf.Max(0f, score);
        }

        private static bool TryFindUtilityOption(List<UtilityOption> options, AiAction action, out UtilityOption option)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Action != action)
                    continue;

                option = options[i];
                return true;
            }

            option = default(UtilityOption);
            return false;
        }

        private static string FormatTopUtilityScores(List<UtilityOption> options)
        {
            int count = Mathf.Min(3, options.Count);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = string.Format(CultureInfo.InvariantCulture, "{0}:{1:0}", options[i].Action, options[i].Score);
            return string.Join(", ", parts);
        }

        private static float GetActionMinimumTime(AiAction action)
        {
            switch (action)
            {
                case AiAction.Fight:
                    return UnityEngine.Random.Range(0.45f, 0.85f);
                case AiAction.Reload:
                case AiAction.Heal:
                case AiAction.TakeCover:
                    return UnityEngine.Random.Range(0.75f, 1.25f);
                case AiAction.RunToRing:
                    return UnityEngine.Random.Range(0.95f, 1.55f);
                case AiAction.ReviveTeamMate:
                    return 0.25f;
                case AiAction.FollowOrder:
                case AiAction.FollowTeam:
                    return UnityEngine.Random.Range(0.45f, 0.8f);
                case AiAction.SearchLastSeen:
                    return UnityEngine.Random.Range(0.85f, 1.35f);
                default:
                    return UnityEngine.Random.Range(0.65f, 1.15f);
            }
        }

        private string GetFightReason(float targetDistance, bool hasShot)
        {
            if (_target == null || !_canSeeTarget)
                return "no visible target";
            if (!HasUsableWeapon())
                return "no usable weapon";
            if (_magazineAmmo <= 0)
                return "empty magazine";
            return string.Format(CultureInfo.InvariantCulture, "{0} at {1:0}m, shot={2}", _weaponProfile.CombatClass, targetDistance, hasShot);
        }

        private string GetCoverReason(bool hasVisibleTarget, bool hasShot, bool needsReload)
        {
            if (_isHealing)
                return "healing behind cover";
            if (_isReloading || needsReload)
                return "reload needs line-of-sight break";
            if (_player.Health <= LowHealthRetreatThreshold)
                return "low health";
            if (hasVisibleTarget && !hasShot)
                return "target sees bot but shot is blocked";
            return hasVisibleTarget ? "under watch" : "hold safe angle";
        }

        private string GetPushReason(float targetDistance)
        {
            if (_weaponProfile.CombatClass == WeaponCombatClass.Shotgun)
                return string.Format(CultureInfo.InvariantCulture, "shotgun closes to {0:0}m", GetPreferredFightRange());
            if (!HasUsableWeapon())
                return "unarmed push blocked";
            return string.Format(CultureInfo.InvariantCulture, "{0} pressure at {1:0}m", _weaponProfile.CombatClass, targetDistance);
        }

        private static string GetRingReason(RingContext ring)
        {
            if (!ring.HasRing)
                return "no ring";
            return string.Format(
                CultureInfo.InvariantCulture,
                "ring danger {0:0.00}, fraction {1:0.00}, moving={2}, closing={3}",
                ring.Danger,
                ring.Fraction,
                ring.IsMoving,
                ring.IsClosing);
        }

        private string GetLootReason(Pickup.WeaponType lootType, Pickup.WeaponType wantedType)
        {
            if (_wantedLoot == null)
                return "no wanted loot";
            if (lootType != wantedType)
                return string.Format(CultureInfo.InvariantCulture, "wanted loot is {0}", lootType);
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", wantedType, _wantedLoot.WeaponName);
        }

        private string GetHealReason(bool hasVisibleTarget, bool wantedHealthLoot)
        {
            if (wantedHealthLoot && _wantedLoot != null)
                return "get health item " + _wantedLoot.WeaponName;
            if (_healingItemCount <= 0 || _healingItemId < 0)
                return "no heal item";
            if (_player.Health >= HealHealthThreshold)
                return "health ok";
            return hasVisibleTarget ? "heal after cover" : "safe heal";
        }

        private string GetSearchReason(bool hasThreat)
        {
            if (!hasThreat && !_hasLastSeenTarget)
                return _hasPoiTarget ? "rotate to poi" : "wander";
            return string.Format(CultureInfo.InvariantCulture, "last known contact, giveup={0:0.0}s", Mathf.Max(0f, _searchGiveUpTimer));
        }

        private string GetFleeReason(float threatDistance)
        {
            if (!HasUsableWeapon())
                return string.Format(CultureInfo.InvariantCulture, "unarmed danger at {0:0}m", threatDistance);
            if (_player.Health <= CriticalHealthRetreatThreshold)
                return "critical health";
            return string.Format(CultureInfo.InvariantCulture, "retreat check at {0:0}m", threatDistance);
        }

        private float GetWeaponRangeFit(float distance)
        {
            float min = GetMinimumFightRange();
            float preferred = Mathf.Max(min + 0.1f, GetPreferredFightRange());
            float max = Mathf.Max(preferred + 0.1f, GetDamageRange());
            if (distance < min)
                return Mathf.Clamp01(distance / min) * 0.55f;
            if (distance <= preferred)
                return Mathf.Lerp(0.76f, 1f, Mathf.InverseLerp(min, preferred, distance));
            return Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(preferred, max, distance));
        }

        private RingContext GetRingContext()
        {
            RingContext context = new RingContext();
            Vector3 center;
            float radius;
            if (!TryGetRing(out center, out radius))
                return context;

            context.HasRing = true;
            context.Center = center;
            context.Radius = Mathf.Max(1f, radius);
            context.Distance = Flat(_player.PlayerPosition - center).magnitude;
            context.Fraction = context.Distance / context.Radius;
            context.IsLateGame = context.Radius <= LateGameRingRadius;

            TheRing ring = TheRing.Instance;
            if (ring != null)
            {
                context.IsMoving = ring.isMoving;
                context.IsClosing = ring.isClosing;
            }

            float danger = 0f;
            if (context.Fraction > 1f)
                danger = 1f + Mathf.Clamp01((context.Fraction - 1f) * 1.5f) * 0.35f;
            else if (context.Fraction > RingHardRotateFraction)
                danger = Mathf.Lerp(0.62f, 0.95f, Mathf.InverseLerp(RingHardRotateFraction, 1f, context.Fraction));
            else if (context.Fraction > RingEarlyRotateFraction)
                danger = Mathf.Lerp(0.24f, 0.58f, Mathf.InverseLerp(RingEarlyRotateFraction, RingHardRotateFraction, context.Fraction));
            else if (context.IsLateGame && context.Fraction > 0.52f)
                danger = 0.18f;

            if (context.IsMoving)
                danger += 0.16f;
            if (context.IsClosing)
                danger += 0.2f;

            context.Danger = Mathf.Clamp(danger, 0f, 1.35f);
            context.ShouldRotate = context.Danger >= 0.34f || context.Fraction >= RingHardRotateFraction || context.IsClosing;
            return context;
        }

        private void TickBotRingDamage(float dt)
        {
            if (_room.CurrentGameState != GameState.Started || !_player.HasDropped)
            {
                _ringDamageTimer = 0f;
                return;
            }

            Vector3 center;
            float radius;
            if (!TryGetRing(out center, out radius) || Flat(_player.PlayerPosition - center).magnitude <= radius)
            {
                _ringDamageTimer = 0f;
                return;
            }

            _ringDamageTimer += Mathf.Max(0f, dt);
            if (_ringDamageTimer < BotRingDamageTickSeconds)
                return;

            int ticks = Mathf.Max(1, Mathf.FloorToInt(_ringDamageTimer / BotRingDamageTickSeconds));
            _ringDamageTimer -= ticks * BotRingDamageTickSeconds;
            FakePlayersPlugin.ApplyEnvironmentDamage(
                _server,
                _player,
                ticks * BotRingDamagePerSecond * BotRingDamageTickSeconds,
                "outside the ring");
        }

        private bool IsThreatWorseRingSide(RingContext ring, Vector3 threatPosition)
        {
            if (!ring.HasRing || threatPosition == Vector3.zero)
                return false;

            float ownFraction = Flat(_player.PlayerPosition - ring.Center).magnitude / Mathf.Max(1f, ring.Radius);
            float threatFraction = Flat(threatPosition - ring.Center).magnitude / Mathf.Max(1f, ring.Radius);
            return threatFraction > ownFraction + 0.08f || threatFraction > RingHardRotateFraction;
        }

        private string GetLootDebugKind(NetworkGun loot, Pickup pickup)
        {
            if (loot == null)
                return "none";

            Pickup.WeaponType type = pickup != null ? pickup.weaponType : Pickup.WeaponType.Weapon;
            if (type != Pickup.WeaponType.Weapon)
                return type.ToString();

            WeaponProfile profile = GetWeaponProfile(loot.UniqueIdentifier, loot.WeaponName);
            if (!_hasWeapon)
                return "weapon:any";
            if (GetWeaponScore(loot.UniqueIdentifier, loot.WeaponName) > _equippedWeaponScore + 8)
                return "weapon:upgrade:" + profile.CombatClass;
            return "weapon:" + profile.CombatClass;
        }

        private bool ShouldRetreat()
        {
            Vector3 threatPosition;
            bool hasThreatPosition = TryGetThreatPosition(out threatPosition);
            if (!hasThreatPosition)
                return false;

            float distance = Flat(threatPosition - _player.PlayerPosition).magnitude;
            if (!HasUsableWeapon() && (_unarmedPanicTimer > 0f || distance <= UnarmedDangerRange))
                return true;

            if (_player.Health <= CriticalHealthRetreatThreshold)
                return true;

            if (_player.Health <= LowHealthRetreatThreshold && _healingItemCount <= 0)
                return distance < GetPreferredFightRange() + 10f;

            if (HasUsableWeapon() && distance < GetMinimumFightRange() * 0.75f && _weaponProfile.CombatClass == WeaponCombatClass.Sniper)
                return true;

            return false;
        }

        private bool NeedsAmmo()
        {
            return HasCombatWeapon() && _magazineAmmo <= 0 && _reserveAmmo <= 0;
        }

        private bool CanRiskUnarmedLoot(bool hasLoot)
        {
            if (!hasLoot || _wantedLoot == null)
                return false;

            if (!HasActiveThreatMemory())
                return true;

            if (_unarmedPanicTimer > 0f || _lootThreatSuppressionTimer > 0f)
                return false;

            Vector3 threatPosition;
            if (!TryGetThreatPosition(out threatPosition))
                return true;

            Vector3 current = _player.PlayerPosition;
            float threatDistance = Flat(threatPosition - current).magnitude;
            float lootDistance = Flat(_wantedLoot.Position - current).magnitude;
            if (threatDistance <= Mathf.Max(3f, UnarmedDangerRange + 1.5f) && lootDistance > 8f)
                return false;

            Vector3 toLoot = Flat(_wantedLoot.Position - current);
            Vector3 toThreat = Flat(threatPosition - current);
            if (toLoot.sqrMagnitude < 1f)
                return true;
            if (toThreat.sqrMagnitude < 1f)
                return false;

            float dot = Vector3.Dot(toLoot.normalized, toThreat.normalized);
            return dot < 0.2f || lootDistance <= 14f;
        }

        private bool HasActiveThreatMemory()
        {
            return (_hasThreatMemory && _threatMemoryTimer > 0f) || (_hasLastSeenTarget && _lastSeenTimer > 0f);
        }

        private bool TryGetThreatPosition(out Vector3 threatPosition)
        {
            if (IsValidEnemyTarget(_target) && _canSeeTarget)
            {
                threatPosition = _target.PlayerPosition;
                return true;
            }

            if (HasActiveThreatMemory())
            {
                threatPosition = _lastKnownThreatPosition;
                if (threatPosition == Vector3.zero)
                    threatPosition = _lastSeenTargetPosition;
                return true;
            }

            if (IsValidEnemyTarget(_threatTarget))
            {
                threatPosition = _lastKnownThreatPosition;
                return true;
            }

            threatPosition = Vector3.zero;
            return false;
        }

        private bool PickUsefulPoiTarget()
        {
            if (_poiTimer > 0f && _hasPoiTarget)
                return true;

            Vector3 current = _player != null ? _player.PlayerPosition : Vector3.zero;
            Vector3 best = Vector3.zero;
            float bestScore = float.MaxValue;

            for (int i = 0; i < PoiTargets.Length; i++)
            {
                Vector3 candidate = PoiTargets[i];
                float groundY;
                if (TryFindGroundY(candidate, out groundY))
                    candidate.y = groundY + _terrainHeightOffset;
                else
                    candidate.y = current.y;

                if (IsBadTerrain(candidate))
                    continue;

                float distance = Flat(candidate - current).magnitude;
                if (distance < PoiArriveDistance)
                    continue;

                float score = distance + UnityEngine.Random.Range(0f, 30f);
                Vector3 ringCenter;
                float ringRadius;
                if (TryGetRing(out ringCenter, out ringRadius))
                {
                    float ringDistance = Flat(candidate - ringCenter).magnitude;
                    if (ringDistance > ringRadius * 0.5f)
                        score += 220f;
                }

                if (score >= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            if (bestScore == float.MaxValue)
            {
                _hasPoiTarget = false;
                return false;
            }

            _currentPoiTarget = best;
            _hasPoiTarget = true;
            _poiTimer = UnityEngine.Random.Range(8f, 15f);
            return true;
        }

        private void SetState(AiState next, float minTime)
        {
            if (_state == next && _stateTimer > 0f)
                return;

            AiState previous = _state;
            _state = next;
            _stateTimer = minTime;
            if (previous == AiState.Reviving && next != AiState.Reviving)
                CancelTeamRevive();
            if (_lastLoggedState != next)
            {
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} state: {next}.");
                _lastLoggedState = next;
            }

            if (next == AiState.Fighting)
            {
                if (previous == AiState.Evading || previous == AiState.Searching)
                    _peekDelayTimer = UnityEngine.Random.Range(0.28f, Mathf.Lerp(0.85f, 0.45f, GetSkillT()));
                PickCombatStrafe();
            }
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
            if (!TryResolveSafeGround(_unstuckTarget, out _unstuckTarget))
            {
                _unstuckTarget = _player.PlayerPosition + direction * 6f;
                _unstuckTarget.y = _player.PlayerPosition.y;
            }
            PickMovementNoise();
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} unstuck target {_unstuckTarget}.");
        }

        private void PickCombatStrafe()
        {
            _combatStrafeSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            switch (_weaponProfile.CombatClass)
            {
                case WeaponCombatClass.Shotgun:
                    _combatStrafeDistance = UnityEngine.Random.Range(4.2f, 7.6f);
                    _combatForwardBias = UnityEngine.Random.Range(1.6f, 3.8f);
                    break;
                case WeaponCombatClass.Sniper:
                case WeaponCombatClass.AutoSniper:
                    _combatStrafeDistance = UnityEngine.Random.Range(5.0f, 9.0f);
                    _combatForwardBias = UnityEngine.Random.Range(-2.8f, 0.4f);
                    break;
                case WeaponCombatClass.Smg:
                case WeaponCombatClass.AssaultRifle:
                case WeaponCombatClass.Lmg:
                    _combatStrafeDistance = UnityEngine.Random.Range(3.2f, 6.2f);
                    _combatForwardBias = UnityEngine.Random.Range(-0.4f, 1.1f);
                    break;
                case WeaponCombatClass.Pistol:
                    _combatStrafeDistance = UnityEngine.Random.Range(2.4f, 4.6f);
                    _combatForwardBias = UnityEngine.Random.Range(-0.8f, 0.7f);
                    break;
                default:
                    _combatStrafeDistance = UnityEngine.Random.Range(2.0f, 4.8f);
                    _combatForwardBias = UnityEngine.Random.Range(0.25f, 1.6f);
                    break;
            }

            _combatStrafeTimer = UnityEngine.Random.Range(0.85f, 1.65f);
            if (_state == AiState.Fighting && UnityEngine.Random.value < 0.18f)
                QueueJump();
        }

        private TABGPlayerServer FindTarget()
        {
            TABGPlayerServer teamThreat = FindRecentTeamThreat();
            if (teamThreat != null && Flat(teamThreat.PlayerPosition - _player.PlayerPosition).magnitude <= TeamCombatAssistRange)
                return teamThreat;

            TABGPlayerServer current = IsValidEnemyTarget(_target) ? _target : null;
            float currentDistance = current != null ? Flat(current.PlayerPosition - _player.PlayerPosition).magnitude : float.MaxValue;
            bool keepCurrent = current != null &&
                (_canSeeTarget || _hasLastSeenTarget) &&
                _targetStickinessTimer > 0f &&
                currentDistance <= ChaseRange;

            TABGPlayerServer best = null;
            float bestDistance = ChaseRange * ChaseRange;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (!IsValidEnemyTarget(candidate))
                    continue;

                float distance = (candidate.PlayerPosition - _player.PlayerPosition).sqrMagnitude;
                if (distance > bestDistance)
                    continue;
                if (distance > UnarmedDangerRange * UnarmedDangerRange && !HasLineOfSight(candidate))
                    continue;

                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            if (keepCurrent && best == null)
                return current;

            if (keepCurrent && best != null)
            {
                float bestFlatDistance = Mathf.Sqrt(bestDistance);
                if (best != current && bestFlatDistance > currentDistance - Mathf.Lerp(16f, 7f, GetSkillT()))
                    return current;
            }

            return best;
        }

        private bool IsValidEnemyTarget(TABGPlayerServer candidate)
        {
            if (candidate == null || candidate == _player || !FakePlayersPlugin.IsCombatTargetAlive(candidate))
                return false;
            if (candidate.GroupIndex == _player.GroupIndex)
                return false;

            return !candidate.Bot || FakePlayersPlugin.IsTrackedAiPlayer(candidate);
        }

        private bool IsValidReviveTarget(TABGPlayerServer candidate)
        {
            return candidate != null &&
                candidate != _player &&
                candidate.GroupIndex == _player.GroupIndex &&
                candidate.HasDropped &&
                candidate.IsDowned &&
                !candidate.IsDead &&
                (!candidate.IsBeingRevived || candidate.Reviver == _player);
        }

        private void TickTeamRevive(float dt)
        {
            TABGPlayerServer target = _reviveStarted ? _activeReviveTarget : _reviveTarget;
            if (_state != AiState.Reviving || _currentAction != AiAction.ReviveTeamMate || !IsValidReviveTarget(target))
            {
                CancelTeamRevive();
                return;
            }

            float distance = Flat(target.PlayerPosition - _player.PlayerPosition).magnitude;
            FacePoint(target.PlayerPosition + Vector3.up * 0.9f);
            if (distance > TeamReviveBreakDistance)
            {
                CancelTeamRevive();
                return;
            }

            if (distance > TeamReviveApproachDistance)
                return;

            StopFullAuto();
            _player.UpdateMovementDirection(Vector3.zero);
            _player.UpdateMovementType(0);

            if (!_reviveStarted)
            {
                if (!ServerMessages.TryRunReviveState(_server, _player, target, ReviveState.Start))
                    return;

                _reviveStarted = true;
                _activeReviveTarget = target;
                _isHealing = false;
                _healTimer = 0f;
                _reviveTimer = Mathf.Max(0.25f, (100f - Mathf.Clamp(target.Health, 0f, 100f)) / VanillaReviveHealthPerSecond);
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} started reviving {target.PlayerName} ({_reviveTimer:0.0}s).");
                return;
            }

            _reviveTimer -= Mathf.Max(0f, dt);
            if (_reviveTimer > 0f)
                return;

            TABGPlayerServer revived = _activeReviveTarget;
            if (ServerMessages.TryRunReviveState(_server, _player, revived, ReviveState.Finished))
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} revived {revived.PlayerName}.");

            _reviveStarted = false;
            _reviveTimer = 0f;
            _activeReviveTarget = null;
            _reviveTarget = null;
            _decisionTimer = 0f;
        }

        private void CancelTeamRevive()
        {
            if (_reviveStarted && _server != null && _player != null && _activeReviveTarget != null &&
                _activeReviveTarget.IsBeingRevived && _activeReviveTarget.Reviver == _player)
            {
                ServerMessages.TryRunReviveState(_server, _player, _activeReviveTarget, ReviveState.Stop);
            }

            _reviveStarted = false;
            _reviveTimer = 0f;
            _activeReviveTarget = null;
        }

        private Vector3 ChooseDestination()
        {
            Vector3 ringDestination;
            if (_currentAction == AiAction.RunToRing && TryGetRingRotationDestination(out ringDestination))
            {
                ReleaseLootClaim();
                _wantedLoot = null;
                _wantedCar = null;
                _hasPoiTarget = false;
                return ringDestination;
            }

            if (TryGetRingEscapeDestination(out ringDestination))
            {
                ReleaseLootClaim();
                _wantedLoot = null;
                _wantedCar = null;
                _hasPoiTarget = false;
                return ringDestination;
            }

            TABGPlayerServer reviveDestination = _reviveStarted ? _activeReviveTarget : _reviveTarget;
            if (_currentAction == AiAction.ReviveTeamMate && IsValidReviveTarget(reviveDestination))
            {
                ReleaseLootClaim();
                _wantedLoot = null;
                _wantedCar = null;
                _hasPoiTarget = false;
                float distance = Flat(reviveDestination.PlayerPosition - _player.PlayerPosition).magnitude;
                return distance <= TeamReviveApproachDistance
                    ? _player.PlayerPosition
                    : ResolveTeamDestination(reviveDestination.PlayerPosition);
            }

            if (_currentAction == AiAction.FollowOrder && _hasTeamOrder)
            {
                ReleaseLootClaim();
                _wantedLoot = null;
                _wantedCar = null;
                _hasPoiTarget = false;
                return GetTeamOrderDestination();
            }

            if (_currentAction == AiAction.FollowTeam && _teamLeader != null)
            {
                ReleaseLootClaim();
                _wantedLoot = null;
                _wantedCar = null;
                _hasPoiTarget = false;
                return GetTeamFollowDestination();
            }

            if ((_currentAction == AiAction.Heal && _isHealing && !HasActiveThreatMemory()) ||
                (_currentAction == AiAction.Reload && _isReloading && !HasActiveThreatMemory()))
            {
                return _player.PlayerPosition;
            }

            if ((_state == AiState.Looting || _state == AiState.Scavenging) && _wantedLoot != null && _room.Weapons.Contains(_wantedLoot))
                return _wantedLoot.Position;

            if (_activeCar == null && _wantedCar != null)
                return _wantedCar.CarPosition;

            if (_state == AiState.Scavenging && _hasPoiTarget)
            {
                if (Flat(_currentPoiTarget - _player.PlayerPosition).magnitude < PoiArriveDistance)
                {
                    _hasPoiTarget = false;
                    PickNewWanderTarget();
                }
                else
                {
                    return _currentPoiTarget;
                }
            }

            switch (_state)
            {
                case AiState.Fighting:
                    return ChooseCombatDestination();

                case AiState.Advancing:
                    if (_target != null && _canSeeTarget)
                        return PredictTargetPosition(_target, 0.65f);
                    break;

                case AiState.Evading:
                    return ChooseEvadeDestination();

                case AiState.Searching:
                    if (HasActiveThreatMemory())
                        return ChooseSearchDestination();
                    break;

                case AiState.Unstuck:
                    return _unstuckTarget;
            }

            if (_wanderTimer <= 0f || Flat(_wanderTarget - _player.PlayerPosition).magnitude < 4f)
                PickNewWanderTarget();

            return _wanderTarget;
        }

        private Vector3 GetTeamOrderDestination()
        {
            float angle = (_player.PlayerIndex * 137.5f) % 360f;
            float radius = 1.8f + (_player.PlayerIndex % 3) * 0.9f;
            Vector3 spread = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            return ResolveTeamDestination(_teamOrderPosition + spread);
        }

        private Vector3 GetTeamFollowDestination()
        {
            if (_teamLeader == null)
                return _player.PlayerPosition;

            Vector3 leaderDirection = Flat(_teamLeader.MovementDirection);
            if (leaderDirection.sqrMagnitude < 0.04f)
                leaderDirection = Quaternion.Euler(0f, _teamLeader.PlayerRotation.y, 0f) * Vector3.forward;
            leaderDirection = Flat(leaderDirection).normalized;

            float sideSlot = (_player.PlayerIndex % 5) - 2f;
            Vector3 side = Vector3.Cross(Vector3.up, leaderDirection);
            Vector3 destination = _teamLeader.PlayerPosition - leaderDirection * (5.5f + Mathf.Abs(sideSlot)) + side * sideSlot * 2.4f;
            return ResolveTeamDestination(destination);
        }

        private bool TryGetRingRotationDestination(out Vector3 destination)
        {
            destination = Vector3.zero;
            RingContext ring = GetRingContext();
            if (!ring.HasRing || !ring.ShouldRotate)
                return false;

            Vector3 fromCenter = Flat(_player.PlayerPosition - ring.Center);
            if (fromCenter.sqrMagnitude < 0.1f)
            {
                Vector3 threatPosition;
                if (TryGetThreatPosition(out threatPosition))
                    fromCenter = Flat(_player.PlayerPosition - threatPosition);
            }

            if (fromCenter.sqrMagnitude < 0.1f)
                fromCenter = UnityEngine.Random.insideUnitSphere;
            fromCenter = Flat(fromCenter).normalized;

            float targetFraction = ring.IsLateGame ? 0.58f : RingDestinationRadiusFraction;
            float targetRadius = Mathf.Clamp(ring.Radius * targetFraction, 10f, Mathf.Max(10f, ring.Radius - 8f));
            float[] angles = { 0f, 18f, -18f, 36f, -36f, 62f, -62f, 100f, -100f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, angles[i], 0f) * fromCenter;
                Vector3 candidate = ring.Center + direction * targetRadius;
                if (TryResolveSafeGround(candidate, out destination))
                    return true;
            }

            Vector3 towardCenter = Flat(ring.Center - _player.PlayerPosition);
            if (towardCenter.sqrMagnitude > 0.1f)
            {
                Vector3 candidate = _player.PlayerPosition + towardCenter.normalized * Mathf.Min(40f, Mathf.Max(8f, ring.Distance - targetRadius));
                if (TryResolveSafeGround(candidate, out destination))
                    return true;
            }

            destination = ring.Center;
            destination.y = _player.PlayerPosition.y;
            return true;
        }

        private bool TryGetRingEscapeDestination(out Vector3 destination)
        {
            destination = Vector3.zero;
            if (_player == null)
                return false;

            Vector3 ringCenter;
            float ringRadius;
            if (!TryGetRing(out ringCenter, out ringRadius))
                return false;

            Vector3 fromCenter = Flat(_player.PlayerPosition - ringCenter);
            float distance = fromCenter.magnitude;
            float unsafeRadius = Mathf.Max(18f, ringRadius * RingUnsafeRadiusFraction);
            if (distance <= unsafeRadius)
                return false;

            Vector3 direction = distance > 0.1f ? fromCenter / distance : Vector3.forward;
            float targetRadius = Mathf.Clamp(
                ringRadius * RingDestinationRadiusFraction,
                8f,
                Mathf.Max(8f, unsafeRadius - 10f));

            float[] angles = { 0f, 18f, -18f, 38f, -38f, 70f, -70f, 115f, -115f, 180f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 rotated = Quaternion.Euler(0f, angles[i], 0f) * direction;
                Vector3 candidate = ringCenter + rotated * targetRadius;
                if (TryResolveSafeGround(candidate, out destination))
                    return true;
            }

            Vector3 towardCenter = Flat(ringCenter - _player.PlayerPosition);
            if (towardCenter.sqrMagnitude > 0.1f)
            {
                Vector3 candidate = _player.PlayerPosition + towardCenter.normalized * Mathf.Min(35f, distance - unsafeRadius + 8f);
                if (TryResolveSafeGround(candidate, out destination))
                    return true;
            }

            destination = ringCenter;
            destination.y = _player.PlayerPosition.y;
            return true;
        }

        private Vector3 ChooseEvadeDestination()
        {
            Vector3 threatPosition;
            if (!TryGetThreatPosition(out threatPosition))
                return _player.PlayerPosition;

            if (HasUsableWeapon() && _coverTimer <= 0f && TryFindCoverDestination(out _coverTarget))
            {
                _coverTimer = CoverRefreshInterval;
                _evadeTarget = _coverTarget;
                _evadeTimer = UnityEngine.Random.Range(1.1f, 1.9f);
                return _evadeTarget;
            }

            if (_evadeTimer > 0f && Flat(_evadeTarget - _player.PlayerPosition).sqrMagnitude > 16f)
                return _evadeTarget;

            Vector3 away = Flat(_player.PlayerPosition - threatPosition);
            if (away.sqrMagnitude < 0.1f)
                away = UnityEngine.Random.insideUnitSphere;
            away = Flat(away).normalized;

            float[] angles = { 0f, 32f, -32f, 65f, -65f, 105f, -105f };
            Vector3 best = _player.PlayerPosition + away * UnarmedEvadeDistance;
            float bestScore = float.MinValue;

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, angles[i], 0f) * away;
                if (IsBlocked(_player.PlayerPosition, direction))
                    continue;

                Vector3 candidate = _player.PlayerPosition + direction.normalized * UnarmedEvadeDistance;
                float groundY;
                if (TryFindGroundY(candidate, out groundY))
                    candidate.y = groundY + _terrainHeightOffset;
                else
                    candidate.y = _player.PlayerPosition.y;
                if (IsBadTerrain(candidate))
                    continue;

                float score = Flat(candidate - threatPosition).magnitude - Mathf.Abs(angles[i]) * 0.08f;
                if (!HasUsableWeapon() && HasCoverFromTarget(candidate))
                    score += 18f;
                Vector3 ringCenter;
                float ringRadius;
                if (TryGetRing(out ringCenter, out ringRadius))
                {
                    float ringDistance = Flat(candidate - ringCenter).magnitude;
                    if (ringDistance > ringRadius * 0.48f)
                        score -= 45f;
                }

                if (score <= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            _evadeTarget = best;
            _evadeTimer = UnityEngine.Random.Range(1.0f, 1.7f);
            if (!HasUsableWeapon() && UnityEngine.Random.value < 0.35f)
                QueueJump();
            return _evadeTarget;
        }

        private Vector3 ChooseSearchDestination()
        {
            Vector3 current = _player.PlayerPosition;
            if (Flat(_lastSeenTargetPosition - current).magnitude > 7f)
                return _lastSeenTargetPosition;

            if (_searchRepathTimer > 0f && Flat(_searchDestination - current).magnitude > 4f)
                return _searchDestination;

            PickSearchSweepDestination();
            return _searchDestination;
        }

        private void PickSearchSweepDestination()
        {
            Vector3 current = _player.PlayerPosition;
            Vector3 fromThreat = Flat(current - _lastSeenTargetPosition);
            if (fromThreat.sqrMagnitude < 1f)
                fromThreat = UnityEngine.Random.insideUnitSphere;
            fromThreat = Flat(fromThreat).normalized;

            Vector3 side = Vector3.Cross(Vector3.up, fromThreat).normalized;
            Vector3 best = _lastSeenTargetPosition;
            float bestScore = float.MinValue;

            for (int i = 0; i < 12; i++)
            {
                int step = _searchSweepIndex + i;
                float radius = Mathf.Min(SearchSweepRadius * 2.2f, 6f + step * 2.8f);
                float angle = step * 78f;
                float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
                float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
                Vector3 candidate = _lastSeenTargetPosition + fromThreat * (cos * radius) + side * (sin * radius);

                float groundY;
                if (!TryFindGroundY(candidate, out groundY))
                    continue;
                candidate.y = groundY + _terrainHeightOffset;
                if (IsBadTerrain(candidate))
                    continue;

                Vector3 moveDir = Flat(candidate - current);
                if (moveDir.sqrMagnitude < 4f || IsBlocked(current, moveDir.normalized))
                    continue;

                float score = UnityEngine.Random.Range(0f, 8f) - Flat(candidate - current).magnitude * 0.04f;
                if (HasLineToPoint(candidate + Vector3.up * 1.0f, allowGround: true))
                    score += 5f;
                if (score <= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            _searchDestination = best;
            _searchSweepIndex += 3;
            _searchRepathTimer = UnityEngine.Random.Range(SearchRepathInterval, SearchRepathInterval + 0.8f);
        }

        private Vector3 ChooseCombatDestination()
        {
            if (_target == null)
                return _player.PlayerPosition;

            if ((_isReloading || _isHealing) && _coverTimer <= 0f && TryFindCoverDestination(out _coverTarget))
            {
                _coverTimer = CoverRefreshInterval;
                return _coverTarget;
            }

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
            float preferredRange = GetPreferredFightRange();
            float minRange = GetMinimumFightRange();
            float stableShotRange = Mathf.Max(minRange + 2f, Mathf.Min(GetDamageRange() - 1f, preferredRange + GetWeaponRangeTolerance()));

            if (_postShotRepositionTimer > 0f)
                return ChoosePostShotRepositionDestination(toward, strafe);

            if ((_player.Health <= LowHealthRetreatThreshold || !hasShot) && _coverTimer <= 0f && TryFindCoverDestination(out _coverTarget))
            {
                _coverTimer = CoverRefreshInterval;
                return _coverTarget;
            }

            if (_weaponProfile.CombatClass == WeaponCombatClass.Shotgun && distance > GetDamageRange() + 8f)
            {
                if (_coverTimer <= 0f && TryFindCoverDestination(out _coverTarget))
                {
                    _coverTimer = CoverRefreshInterval;
                    return _coverTarget;
                }

                destination = _player.PlayerPosition - toward * 5f + strafe * _combatStrafeDistance;
                destination.y = _player.PlayerPosition.y;
                return destination;
            }

            if (hasShot && distance >= minRange + 1f && distance <= stableShotRange)
            {
                if (_combatStrafeTimer > 0.18f)
                    return _player.PlayerPosition;

                destination = _player.PlayerPosition + strafe * Mathf.Min(1.8f, _combatStrafeDistance * 0.35f);
                destination.y = _player.PlayerPosition.y;
                return destination;
            }

            if (!hasShot)
            {
                float step = Mathf.Clamp(distance - preferredRange, 4f, 15f);
                destination = _player.PlayerPosition + toward * step + strafe * Mathf.Min(_combatStrafeDistance, 1.2f);
            }
            else if (distance < minRange)
            {
                Vector3 away = -toward;
                destination = _player.PlayerPosition + away * Mathf.Clamp(minRange - distance + 3f, 3f, 10f) + strafe * (_combatStrafeDistance * 0.7f);
            }
            else if (distance > preferredRange + 1.5f || targetIsRunningAway || _weaponProfile.CombatClass == WeaponCombatClass.Shotgun)
            {
                float step = targetIsRunningAway
                    ? Mathf.Clamp(distance - minRange, 1.4f, 5.5f)
                    : Mathf.Clamp(distance - preferredRange, 1.2f, _weaponProfile.CombatClass == WeaponCombatClass.Shotgun ? 9.5f : 6.5f);
                destination = _player.PlayerPosition + toward * step + strafe * _combatStrafeDistance;
            }
            else
            {
                destination = _player.PlayerPosition + toward * _combatForwardBias + strafe * (_combatStrafeDistance * 1.15f);
            }

            if (UnityEngine.Random.value < 0.08f + GetSkillT() * 0.05f)
                QueueJump();

            destination.y = _player.PlayerPosition.y;
            return destination;
        }

        private Vector3 ChoosePostShotRepositionDestination(Vector3 toward, Vector3 strafe)
        {
            Vector3 away = -toward;
            Vector3 destination;
            if (_weaponProfile.CombatClass == WeaponCombatClass.Sniper || _weaponProfile.CombatClass == WeaponCombatClass.AutoSniper)
                destination = _player.PlayerPosition + strafe * Mathf.Max(7f, _combatStrafeDistance) + away * 4f;
            else
                destination = _player.PlayerPosition + strafe * Mathf.Max(4f, _combatStrafeDistance) + away * 2f;

            float groundY;
            if (TryFindGroundY(destination, out groundY))
                destination.y = groundY + _terrainHeightOffset;
            else
                destination.y = _player.PlayerPosition.y;

            if (!IsBadTerrain(destination) && !IsBlocked(_player.PlayerPosition, Flat(destination - _player.PlayerPosition).normalized))
                return destination;

            return _player.PlayerPosition + strafe * 4f;
        }

        private void PickNewWanderTarget()
        {
            if (_wantedLoot == null && !HasActiveThreatMemory() && (_target == null || !_canSeeTarget) && PickUsefulPoiTarget())
            {
                _wanderTarget = _currentPoiTarget;
                _wanderTimer = UnityEngine.Random.Range(5f, 10f);
                return;
            }

            Vector3 center = _player != null ? _player.PlayerPosition : Vector3.zero;
            float radius = 45f;

            Vector3 ringCenter;
            float ringRadius;
            if (TryGetRing(out ringCenter, out ringRadius))
            {
                Vector3 currentToCenter = Flat(_player.PlayerPosition - ringCenter);
                if (currentToCenter.magnitude > ringRadius * 0.5f)
                    center = ringCenter;
                radius = ringRadius <= LateGameRingRadius
                    ? Mathf.Clamp(ringRadius * 0.18f, 12f, 28f)
                    : Mathf.Clamp(ringRadius * 0.35f, 25f, 90f);
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(random.x, 0f, random.y);
                float groundY;
                if (TryFindGroundY(candidate, out groundY))
                    candidate.y = groundY + _terrainHeightOffset;
                else if (_player != null)
                    candidate.y = _player.PlayerPosition.y;

                if (!IsBadTerrain(candidate))
                {
                    _wanderTarget = candidate;
                    _wanderTimer = UnityEngine.Random.Range(3f, 8f);
                    return;
                }
            }

            _wanderTarget = center;
            if (_player != null)
                _wanderTarget.y = _player.PlayerPosition.y;
            _wanderTimer = UnityEngine.Random.Range(3f, 8f);
        }

        private bool TryFindCoverDestination(out Vector3 cover)
        {
            cover = _player.PlayerPosition;
            Vector3 threatPosition;
            if (!TryGetThreatPosition(out threatPosition))
                return false;

            Vector3 current = _player.PlayerPosition;
            Vector3 toTarget = Flat(threatPosition - current);
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
                    if (IsBadTerrain(candidate))
                        continue;

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

            if (IsOutsidePlayableBounds(next))
            {
                next = PickDropTarget();
                _lastProgressPosition = next;
                _smoothedDirection = Vector3.zero;
                _hasNavPath = false;
                _localAvoidTimer = 0f;
                PickNewWanderTarget();
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} corrected off-map movement to {next}.");
            }
            else if (IsBadTerrain(next))
            {
                Vector3 avoidDirection = _smoothedDirection != Vector3.zero ? -_smoothedDirection : Flat(current - destination);
                if (avoidDirection.sqrMagnitude < 0.1f)
                    avoidDirection = UnityEngine.Random.insideUnitSphere;
                avoidDirection = Flat(avoidDirection).normalized;
                Vector3 fallbackDirection = FindClearDirection(current, avoidDirection, ignoreBack: false);
                if (fallbackDirection != Vector3.zero)
                {
                    next = current + fallbackDirection * GetCurrentMoveSpeed() * dt;
                    next.y = ResolveTerrainY(current, next, dt);
                    _smoothedDirection = fallbackDirection;
                    _hasNavPath = false;
                    _localAvoidTimer = 0f;
                }
                else
                {
                    next = current;
                    _smoothedDirection = Vector3.zero;
                    PickNewWanderTarget();
                }
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
            if (_weaponProfile.CombatClass != WeaponCombatClass.Shotgun && _weaponProfile.CombatClass != WeaponCombatClass.Unarmed)
                return direction;

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
            _activeSeat = FindUsableSeat(_activeCar);
            if (_activeSeat == null)
            {
                _activeCar = null;
                return;
            }

            _activeCar.RemoveTemporaryOwner();
            _activeSeat.SetOccupant(_player);
            _player.UpdateSeat(_activeSeat, _activeCar);
            FakePlayersPlugin.BroadcastSeatAccepted(_server, _player, _activeCar, _activeSeat, getIn: true);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} using vehicle {_activeCar.CarIndex}.");
        }

        private void UpdateActiveVehicle(Vector3 playerPosition, float dt)
        {
            if (_activeCar == null)
                return;

            Vector3 carPosition = playerPosition;
            carPosition.y -= 0.25f;
            _activeCar.UpdatePosition(carPosition);
            if (_smoothedDirection != Vector3.zero)
            {
                Vector3 flatDirection = Flat(_smoothedDirection).normalized;
                _activeCar.UpdateRotation(Quaternion.LookRotation(flatDirection));
                _activeCar.UpdateInput(flatDirection);
            }
            _activeCar.UpdateDrivingState(CarDrivingState.None);
        }

        private void LeaveVehicle()
        {
            if (_activeCar == null)
                return;
            if (_player == null)
            {
                _activeSeat = null;
                _activeCar = null;
                return;
            }

            TABGCarServer car = _activeCar;
            TABGCarServerSeat seat = _activeSeat;
            if (seat != null)
                seat.EjectOccupant();
            _player.UpdateSeat(null, null);
            car.GiveTemporaryOwner(_player.PlayerIndex, 5f);
            FakePlayersPlugin.BroadcastSeatAccepted(_server, _player, car, seat, getIn: false);
            _activeSeat = null;
            _activeCar = null;
        }

        private static TABGCarServerSeat FindUsableSeat(TABGCarServer car)
        {
            if (car == null)
                return null;

            for (int i = 0; i < car.NumberOfSeats; i++)
            {
                TABGCarServerSeat seat = car.GetSeat(i);
                if (seat == null || seat.Occupant != null)
                    continue;

                if (seat.DriverSeat)
                    return seat;
            }

            return null;
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
            if (distance > damageRange || _reactionDelayTimer > 0f || _peekDelayTimer > 0f || _postShotRepositionTimer > 0f || _isReloading || _isHealing)
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

            if (_magazineAmmo <= 0)
            {
                StartReload();
                StopFullAuto();
                return;
            }

            Vector3 aimPoint = GetCombatAimPoint(target, distance, addMiss: true);

            if (_weaponProfile.FirePlan == FirePlan.FullAuto)
            {
                if (!_isFullAutoFiring && _shootTimer > 0f)
                    return;

                TickFullAuto(target, aimPoint);
                return;
            }

            StopFullAuto();
            if (_weaponProfile.FirePlan == FirePlan.Burst)
            {
                TickBurstFire(target, aimPoint);
                return;
            }

            if (_shootTimer > 0f || !ConsumeRound())
                return;

            FireProjectile(aimPoint);
            _fireAnimationTimer = FireAnimationWindow;
            NoteShotFired();

            _shootTimer = GetSemiFireInterval();
        }

        private void TickFullAuto(TABGPlayerServer target, Vector3 aimPoint)
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
                _autoShotTimer = 0f;
                _fireAnimationTimer = Mathf.Max(_fireAnimationTimer, 0.35f);
            }

            if (_autoShotTimer <= 0f)
            {
                float distance = Flat(target.PlayerPosition - _player.PlayerPosition).magnitude;
                float damageRange = GetDamageRange();
                if (distance > damageRange || !HasShotLine(target) || !IsAimingAt(target, GetAimCone()) || !ConsumeRound())
                {
                    StopFullAuto();
                    return;
                }

                _autoBulletsFired++;
                _fireAnimationTimer = Mathf.Max(_fireAnimationTimer, FireAnimationWindow);
                FireProjectile(aimPoint);
                if (_autoBulletsFired == 1 || _autoBulletsFired % 6 == 0)
                    NoteShotFired();
                _autoShotTimer = Mathf.Max(0.045f, _weaponProfile.FireInterval);
            }

            if (_autoBurstTimer <= 0f || _magazineAmmo <= 0)
            {
                StopFullAuto();
                if (_magazineAmmo <= 0)
                    StartReload();
                _shootTimer = UnityEngine.Random.Range(0.22f, 0.55f);
            }
        }

        private void StopFullAuto()
        {
            if (!_isFullAutoFiring)
                return;

            _isFullAutoFiring = false;
            _autoBulletsFired = 0;
        }

        private void TickBurstFire(TABGPlayerServer target, Vector3 aimPoint)
        {
            if (_shootTimer > 0f && _burstShotsRemaining <= 0)
                return;

            if (_burstShotsRemaining <= 0)
            {
                _burstShotsRemaining = Mathf.Max(2, _weaponProfile.BurstShots);
                _burstShotTimer = 0f;
            }

            if (_burstShotTimer > 0f)
                return;

            if (!ConsumeRound())
            {
                _burstShotsRemaining = 0;
                StartReload();
                return;
            }

            FireProjectile(aimPoint);
            _fireAnimationTimer = FireAnimationWindow;
            NoteShotFired();

            _burstShotsRemaining--;
            if (_burstShotsRemaining > 0 && _magazineAmmo > 0)
            {
                _burstShotTimer = Mathf.Max(0.065f, _weaponProfile.FireInterval);
                return;
            }

            _burstShotsRemaining = 0;
            _shootTimer = UnityEngine.Random.Range(0.34f, 0.72f);
            if (_magazineAmmo <= 0)
                StartReload();
        }

        private void FireProjectile(Vector3 aimPoint)
        {
            Vector3 muzzle = GetMuzzlePosition();
            Vector3 direction = aimPoint - muzzle;
            if (direction.sqrMagnitude < 0.01f)
                direction = Quaternion.Euler(new Vector3(_player.PlayerRotation.x, _player.PlayerRotation.y, 0f)) * Vector3.forward;
            direction.Normalize();

            // Use the game's normal server command so vanilla clients create the same
            // remote projectile, muzzle flash, sound, and tracer as for a real player.
            FakePlayersPlugin.BroadcastFire(_server, _player, aimPoint);

            float range = Mathf.Min(GetDamageRange(), MaxFairGunDamageRange);
            _serverProjectiles.Add(new ServerProjectile(
                muzzle,
                direction,
                range,
                GetProjectileSpeed(),
                _weaponProfile.BaseDamage));
        }

        private void TickServerProjectiles(float dt)
        {
            if (_serverProjectiles.Count == 0 || _room == null)
                return;

            float stepTime = Mathf.Clamp(dt, 0f, 0.25f);
            for (int i = _serverProjectiles.Count - 1; i >= 0; i--)
            {
                ServerProjectile projectile = _serverProjectiles[i];
                float stepDistance = Mathf.Min(projectile.RemainingRange, projectile.Speed * stepTime);
                if (stepDistance <= 0.001f)
                {
                    _serverProjectiles.RemoveAt(i);
                    continue;
                }

                float worldHitDistance = FindWorldHitDistance(projectile.Position, projectile.Direction, stepDistance);
                TABGPlayerServer hitPlayer;
                float playerHitDistance;
                float hitMultiplier;
                bool hit = TryFindPlayerHit(
                    projectile.Position,
                    projectile.Direction,
                    Mathf.Min(stepDistance, worldHitDistance),
                    out hitPlayer,
                    out playerHitDistance,
                    out hitMultiplier);

                if (hit)
                {
                    float travelled = projectile.DistanceTravelled + playerHitDistance;
                    float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(travelled / Mathf.Max(1f, projectile.MaxRange)));
                    float damage = Mathf.Max(0.5f, projectile.Damage * falloff * hitMultiplier);
                    FakePlayersPlugin.ApplyDamage(_server, _player, hitPlayer, damage);
                    _serverProjectiles.RemoveAt(i);
                    continue;
                }

                if (worldHitDistance <= stepDistance)
                {
                    _serverProjectiles.RemoveAt(i);
                    continue;
                }

                projectile.Position += projectile.Direction * stepDistance;
                projectile.DistanceTravelled += stepDistance;
                projectile.RemainingRange -= stepDistance;
                if (projectile.RemainingRange <= 0.001f)
                    _serverProjectiles.RemoveAt(i);
                else
                    _serverProjectiles[i] = projectile;
            }
        }

        private float FindWorldHitDistance(Vector3 origin, Vector3 direction, float distance)
        {
            float closest = float.MaxValue;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || IsAnyPlayerCollider(collider))
                    continue;

                closest = Mathf.Min(closest, hits[i].distance);
            }

            return closest;
        }

        private bool TryFindPlayerHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out TABGPlayerServer hitPlayer,
            out float hitDistance,
            out float damageMultiplier)
        {
            hitPlayer = null;
            hitDistance = float.MaxValue;
            damageMultiplier = 1f;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer candidate = _room.Players[i];
                if (!IsValidEnemyTarget(candidate))
                    continue;

                float candidateDistance;
                float candidateMultiplier;
                if (!TryIntersectPlayer(origin, direction, distance, candidate, out candidateDistance, out candidateMultiplier))
                    continue;
                if (candidateDistance >= hitDistance)
                    continue;

                hitPlayer = candidate;
                hitDistance = candidateDistance;
                damageMultiplier = candidateMultiplier;
            }

            return hitPlayer != null;
        }

        private static bool TryIntersectPlayer(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            TABGPlayerServer player,
            out float hitDistance,
            out float damageMultiplier)
        {
            hitDistance = float.MaxValue;
            damageMultiplier = 1f;

            float distance;
            if (TryIntersectSphere(origin, direction, maxDistance, player.PlayerPosition + Vector3.up * 0.45f, 0.46f, out distance) && distance < hitDistance)
            {
                hitDistance = distance;
                damageMultiplier = 0.78f;
            }
            if (TryIntersectSphere(origin, direction, maxDistance, player.PlayerPosition + Vector3.up * 1.02f, 0.48f, out distance) && distance < hitDistance)
            {
                hitDistance = distance;
                damageMultiplier = 1f;
            }
            if (TryIntersectSphere(origin, direction, maxDistance, player.PlayerPosition + Vector3.up * 1.62f, 0.31f, out distance) && distance < hitDistance)
            {
                hitDistance = distance;
                damageMultiplier = 1.55f;
            }

            return hitDistance < float.MaxValue;
        }

        private static bool TryIntersectSphere(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            Vector3 center,
            float radius,
            out float distance)
        {
            Vector3 toCenter = center - origin;
            float projected = Vector3.Dot(toCenter, direction);
            float centerDistanceSquared = toCenter.sqrMagnitude - projected * projected;
            float radiusSquared = radius * radius;
            if (centerDistanceSquared > radiusSquared)
            {
                distance = 0f;
                return false;
            }

            float offset = Mathf.Sqrt(Mathf.Max(0f, radiusSquared - centerDistanceSquared));
            distance = projected - offset;
            if (distance < 0f)
                distance = projected + offset;
            return distance >= 0f && distance <= maxDistance;
        }

        private bool IsAnyPlayerCollider(Collider collider)
        {
            if (collider == null || _room == null)
                return false;

            for (int i = 0; i < _room.Players.Count; i++)
            {
                TABGPlayerServer player = _room.Players[i];
                if (player != null && player.PlayerObject != null && collider.transform.IsChildOf(player.PlayerObject.transform))
                    return true;
            }

            return collider.GetComponentInParent<Player>() != null;
        }

        private float GetProjectileSpeed()
        {
            switch (_weaponProfile.CombatClass)
            {
                case WeaponCombatClass.Launcher:
                    return 35f;
                case WeaponCombatClass.Shotgun:
                    return 82f;
                case WeaponCombatClass.Pistol:
                case WeaponCombatClass.Smg:
                    return 95f;
                case WeaponCombatClass.Sniper:
                case WeaponCombatClass.AutoSniper:
                    return 165f;
                default:
                    return 120f;
            }
        }

        private void NoteShotFired()
        {
            Vector3 current = _player.PlayerPosition;
            if (_lastFirePosition == Vector3.zero || Flat(current - _lastFirePosition).magnitude > RepositionAfterShotsDistance)
            {
                _lastFirePosition = current;
                _sameFireSpotShots = 1;
            }
            else
            {
                _sameFireSpotShots++;
            }

            bool sniper = _weaponProfile.CombatClass == WeaponCombatClass.Sniper || _weaponProfile.CombatClass == WeaponCombatClass.AutoSniper;
            int shotLimit = sniper ? 1 : (_weaponProfile.FirePlan == FirePlan.FullAuto ? 8 : 5);
            if (_sameFireSpotShots < shotLimit)
                return;

            _postShotRepositionTimer = sniper ? UnityEngine.Random.Range(1.2f, 2.0f) : UnityEngine.Random.Range(0.8f, 1.35f);
            _sameFireSpotShots = 0;
            PickCombatStrafe();
        }

        private void TickReload()
        {
            if (!_isReloading && _currentAction == AiAction.Reload && HasCombatWeapon() && _magazineAmmo <= 0 && _reserveAmmo > 0)
                StartReload();

            if (!_isReloading)
                return;

            if (_reloadTimer > 0f)
                return;

            _isReloading = false;
            int rounds = Mathf.Min(Mathf.Max(1, _weaponProfile.MagazineSize), Mathf.Max(0, _reserveAmmo));
            _magazineAmmo = rounds;
            _reserveAmmo -= rounds;
            _shootTimer = UnityEngine.Random.Range(0.12f, 0.28f);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} reloaded {_player.WeaponName}; reserve {_reserveAmmo}.");
        }

        private bool ConsumeRound()
        {
            if (_isReloading)
                return false;

            if (_magazineAmmo <= 0)
            {
                StartReload();
                return false;
            }

            _magazineAmmo--;
            if (_magazineAmmo <= 0 && _weaponProfile.FirePlan != FirePlan.FullAuto)
                StartReload();
            return true;
        }

        private void StartReload()
        {
            if (_isReloading)
                return;

            if (_reserveAmmo <= 0)
                return;

            StopFullAuto();
            _burstShotsRemaining = 0;
            _isReloading = true;
            _reloadTimer = Mathf.Lerp(_weaponProfile.ReloadSeconds * 1.2f, _weaponProfile.ReloadSeconds * 0.8f, GetSkillT());
            _shootTimer = _reloadTimer;
        }

        private NetworkGun FindLoot()
        {
            float maxDistance = _hasWeapon ? LootSearchRange : WeaponlessLootSearchRange;
            bool threatActive = HasActiveThreatMemory();
            Vector3 threatPosition = Vector3.zero;
            bool hasThreatPosition = threatActive && TryGetThreatPosition(out threatPosition);
            PruneBlockedLoot();

            NetworkGun best = FindBestLootInRange(maxDistance, hasThreatPosition, threatPosition, requireVisibleFallback: false);
            if (best != null || !NeedsEmergencyLoot())
                return best;

            return FindBestLootInRange(EmergencyLootSearchRange, hasThreatPosition, threatPosition, requireVisibleFallback: true);
        }

        private NetworkGun FindBestLootInRange(float maxDistance, bool hasThreatPosition, Vector3 threatPosition, bool requireVisibleFallback)
        {
            NetworkGun best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _room.Weapons.Count; i++)
            {
                NetworkGun loot = _room.Weapons[i];
                if (loot == null)
                    continue;
                if (IsLootTemporarilyBlocked(loot.Index))
                    continue;
                if (_teamLeader != null && !_hasTeamOrder &&
                    Flat(loot.Position - _teamLeader.PlayerPosition).magnitude > TeamFollowUrgentDistance)
                    continue;

                float distance = Flat(loot.Position - _player.PlayerPosition).magnitude;
                if (distance > maxDistance)
                    continue;

                Pickup pickup = GetPickup(loot);
                float value = ScoreLootValue(loot, pickup);
                if (value <= 0f)
                    continue;

                bool visible = HasLineToPoint(loot.Position + Vector3.up * 0.4f, allowGround: true);
                if (requireVisibleFallback && !visible && !_navMeshDisabled)
                    continue;

                float score = distance - value + (visible ? 0f : 55f) + GetLootJitter(loot.Index);
                if (IsLootClaimedByOther(loot.Index))
                    score += 180f;
                if (_target != null && _canSeeTarget)
                    score += Mathf.Clamp(Flat(loot.Position - _target.PlayerPosition).magnitude * 0.12f, 0f, 20f);
                if (hasThreatPosition)
                {
                    Vector3 toLoot = Flat(loot.Position - _player.PlayerPosition);
                    Vector3 toThreat = Flat(threatPosition - _player.PlayerPosition);
                    if (toLoot.sqrMagnitude > 1f && toThreat.sqrMagnitude > 1f)
                    {
                        float dot = Vector3.Dot(toLoot.normalized, toThreat.normalized);
                        if (dot > 0f)
                            score += dot * (_hasWeapon ? 42f : 95f);
                    }

                    float threatDistance = Flat(threatPosition - _player.PlayerPosition).magnitude;
                    if (!_hasWeapon && threatDistance < Mathf.Max(3f, UnarmedDangerRange + 1.5f) && distance > 12f)
                        score += 120f;
                    if (!_hasWeapon && visible)
                        score -= 8f;
                    if (_hasWeapon && _canSeeTarget && distance > 8f)
                        score += 45f;
                }
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

        private bool NeedsEmergencyLoot()
        {
            if (!_hasWeapon || !HasCombatWeapon())
                return true;
            if (NeedsAmmo())
                return true;
            return _player != null && _player.Health < 55f && _healingItemCount <= 0;
        }

        private void MarkLootTemporarilyBlocked(NetworkGun loot)
        {
            if (loot == null)
                return;

            _blockedLootUntil[loot.Index] = Time.unscaledTime + BlockedLootCooldownSeconds;
            if (_blockedLootUntil.Count > MaxBlockedLootEntries)
                PruneBlockedLoot(clearIfStillTooLarge: true);
        }

        private bool IsLootTemporarilyBlocked(int lootIndex)
        {
            float blockedUntil;
            if (!_blockedLootUntil.TryGetValue(lootIndex, out blockedUntil))
                return false;

            if (blockedUntil > Time.unscaledTime)
                return true;

            _blockedLootUntil.Remove(lootIndex);
            return false;
        }

        private void PruneBlockedLoot(bool clearIfStillTooLarge = false)
        {
            if (_blockedLootUntil.Count == 0)
                return;

            float now = Time.unscaledTime;
            _blockedLootScratch.Clear();
            foreach (KeyValuePair<int, float> entry in _blockedLootUntil)
            {
                if (entry.Value <= now)
                    _blockedLootScratch.Add(entry.Key);
            }

            for (int i = 0; i < _blockedLootScratch.Count; i++)
                _blockedLootUntil.Remove(_blockedLootScratch[i]);

            if (clearIfStillTooLarge && _blockedLootUntil.Count > MaxBlockedLootEntries)
                _blockedLootUntil.Clear();
        }

        private void ClaimLoot(NetworkGun loot)
        {
            if (loot == null || _player == null)
                return;

            LootClaims[loot.Index] = _player.PlayerIndex;
        }

        private void ReleaseLootClaim()
        {
            if (_wantedLoot == null || _player == null)
                return;

            byte owner;
            if (LootClaims.TryGetValue(_wantedLoot.Index, out owner) && owner == _player.PlayerIndex)
                LootClaims.Remove(_wantedLoot.Index);
        }

        private bool IsLootClaimedByOther(int lootIndex)
        {
            byte owner;
            if (!LootClaims.TryGetValue(lootIndex, out owner))
                return false;

            if (_player != null && owner == _player.PlayerIndex)
                return false;

            if (_room == null || _room.FindPlayer(owner) == null)
            {
                LootClaims.Remove(lootIndex);
                return false;
            }

            return true;
        }

        private float GetLootJitter(int lootIndex)
        {
            int playerIndex = _player != null ? _player.PlayerIndex : 0;
            unchecked
            {
                int hash = (lootIndex * 73856093) ^ (playerIndex * 19349663);
                return Mathf.Abs(hash % 37);
            }
        }

        private float ScoreLootValue(NetworkGun loot, Pickup pickup)
        {
            if (loot == null)
                return 0f;

            Pickup.WeaponType type = pickup != null ? pickup.weaponType : Pickup.WeaponType.Weapon;
            switch (type)
            {
                case Pickup.WeaponType.Weapon:
                    int weaponScore = GetWeaponScore(loot.UniqueIdentifier, loot.WeaponName);
                    if (!_hasWeapon)
                        return 130f + weaponScore;
                    if (_target != null && _canSeeTarget)
                        return 0f;
                    return weaponScore > _equippedWeaponScore + 8 ? 70f + (weaponScore - _equippedWeaponScore) : 0f;

                case Pickup.WeaponType.Grenade:
                    if (!_hasWeapon)
                        return 0f;
                    return EnableGrenadeThrows && _grenadeCount < 1 && (_target == null || !_canSeeTarget) ? 28f : 0f;

                case Pickup.WeaponType.Health:
                    bool watched = _target != null && _canSeeTarget && !HasCoverFromTarget(_player.PlayerPosition);
                    if (watched && _player.Health > HealCriticalThreshold)
                        return 0f;
                    if (!_hasWeapon && _player.Health >= 45f)
                        return 0f;
                    if (_player.Health < 55f)
                        return watched ? 38f : 68f;
                    if (_player.Health < 75f && _healingItemCount <= 0)
                        return 36f;
                    return 0f;

                case Pickup.WeaponType.Ammo:
                    if (!_hasWeapon)
                        return 0f;
                    if (NeedsAmmo())
                        return 95f;
                    return _reserveAmmo < _weaponProfile.MagazineSize ? 34f : 0f;

                case Pickup.WeaponType.Armor:
                case Pickup.WeaponType.Blessing:
                case Pickup.WeaponType.WeaponAttatchment:
                    return 0f;

                case Pickup.WeaponType.OtherConsumable:
                    return 0f;
            }

            return 0f;
        }

        private Pickup GetPickup(NetworkGun loot)
        {
            try
            {
                return loot != null && _room != null ? _room.GetItem(loot.UniqueIdentifier) : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte GetPickupSlot(Pickup.WeaponType type)
        {
            switch (type)
            {
                case Pickup.WeaponType.Weapon:
                    return (byte)Pickup.EquipSlots.WeaponSlot01;
                case Pickup.WeaponType.Grenade:
                    return (byte)Pickup.EquipSlots.ThrowableSlot;
                default:
                    return (byte)Pickup.EquipSlots.None;
            }
        }

        private void EquipWeapon(NetworkGun loot)
        {
            _player.UpdateEquipment((byte)Pickup.EquipSlots.WeaponSlot01, (short)loot.UniqueIdentifier, -1, -1, -1, -1, Array.Empty<short>());
            _player.ChangeWeaponType(loot.UniqueIdentifier);
            _player.ChangeAimDownSightState(false);
            FakePlayersPlugin.BroadcastWeaponChanged(_server, _player);
            _equippedWeaponId = loot.UniqueIdentifier;
            _equippedWeaponScore = GetWeaponScore(loot.UniqueIdentifier, loot.WeaponName);
            _weaponProfile = GetWeaponProfile(loot.UniqueIdentifier, loot.WeaponName);
            _magazineAmmo = Mathf.Max(1, _weaponProfile.MagazineSize);
            _reserveAmmo = Mathf.Max(_reserveAmmo, _weaponProfile.MagazineSize * 3);
            _burstShotsRemaining = 0;
            _reloadTimer = 0f;
            _isReloading = false;
            _hasWeapon = true;
        }

        internal void SyncDeathLoot()
        {
            if (_player == null || _room == null)
                return;

            if (HasCombatWeapon() && _equippedWeaponId >= 0 && _player.HasLoot(_equippedWeaponId) <= 0)
                AddSyntheticLoot(_equippedWeaponId, 1);

            for (int i = 0; i < AmmoLootItemIds.Length; i++)
                RemoveLootIfPresent(AmmoLootItemIds[i]);

            if (HasCombatWeapon())
            {
                int looseRounds = Mathf.Max(0, _magazineAmmo + _reserveAmmo);
                if (looseRounds > 0)
                {
                    int ammoStacks = Mathf.Clamp(Mathf.CeilToInt(looseRounds / 18f), 1, 8);
                    AddSyntheticLoot(GetAmmoItemIdForWeapon(), ammoStacks);
                }
            }

            if (_healingItemId >= 0)
            {
                RemoveLootIfPresent(_healingItemId);
                if (_healingItemCount > 0)
                    AddSyntheticLoot(_healingItemId, Mathf.Clamp(_healingItemCount, 1, 4));
            }

            if (_grenadeItemId >= 0)
            {
                RemoveLootIfPresent(_grenadeItemId);
                if (_grenadeCount > 0)
                    AddSyntheticLoot(_grenadeItemId, Mathf.Clamp(_grenadeCount, 1, 3));
            }
        }

        private int GetAmmoItemIdForWeapon()
        {
            string name = (_player?.WeaponName ?? string.Empty).ToLowerInvariant();
            switch (_weaponProfile.CombatClass)
            {
                case WeaponCombatClass.Shotgun:
                    return 8; // Shotgun ammo
                case WeaponCombatClass.Smg:
                case WeaponCombatClass.Pistol:
                    return 9; // Small ammo
                case WeaponCombatClass.Launcher:
                    return 7; // Rocket ammo
                case WeaponCombatClass.Sniper:
                case WeaponCombatClass.AutoSniper:
                    if (name.Contains("crossbow"))
                        return 2;
                    if (name.Contains("musket"))
                        return 4;
                    return 6; // Normal ammo
                default:
                    return 6; // Normal ammo
            }
        }

        private void RemoveLootIfPresent(int itemId)
        {
            int count = _player.HasLoot(itemId);
            if (count > 0)
                _player.RemoveLoot(itemId, count);
        }

        private void AddSyntheticLoot(int itemId, int amount)
        {
            if (itemId < 0 || amount <= 0)
                return;

            int index = _room.GetNewWeaponIndex();
            _player.AddLoot(new NetworkGun("AI carried loot", amount, index, itemId, null));
        }

        private void FaceBestInterest(Vector3 movementDirection)
        {
            Vector3 lookTarget;
            if (HasUsableWeapon() && _target != null && _canSeeTarget && Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude <= GetDamageRange())
            {
                float distance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
                lookTarget = GetCombatAimPoint(_target, distance, addMiss: false);
            }
            else if ((_state == AiState.Searching || _state == AiState.Evading || HasActiveThreatMemory()) && TryGetThreatPosition(out lookTarget))
            {
                lookTarget += Vector3.up * 1.1f;
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
            float aimHeight = _weaponProfile.CombatClass == WeaponCombatClass.Sniper && GetSkillT() > 0.55f ? 1.42f : 1.08f;
            Vector3 aimPoint = PredictTargetPosition(target, Mathf.Clamp(distance / 80f, 0.08f, 0.45f)) + Vector3.up * aimHeight;
            if (!addMiss)
                return aimPoint;

            float t = Mathf.Clamp01(distance / Mathf.Max(1f, GetDamageRange()));
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
            bool canWanderOffLine = _state == AiState.Wandering || _state == AiState.Searching;
            if (distance > 2f && canWanderOffLine)
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
            if (IsBadTerrain(destination))
            {
                NoteNavFailure("bad terrain target");
                return false;
            }

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
                    if (IsBadTerrain(candidate))
                        continue;

                    Vector3 candidateDirection = Flat(candidate - current);
                    if (candidateDirection.sqrMagnitude < 4f || IsBlocked(current, candidateDirection.normalized))
                        continue;

                    float destinationDistance = Flat(destination - candidate).magnitude;
                    if (destinationDistance > currentDistance + 10f && Mathf.Abs(angles[a]) < 110f)
                        continue;

                    float score = destinationDistance + Mathf.Abs(angles[a]) * 0.08f + distances[d] * 0.04f;
                    if (IsBadTerrain(destination))
                        score += BadTerrainRepathPenalty;
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

        private static byte BuildMovementFlags(Vector3 direction, float yaw)
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
                return VehicleMoveSpeed;

            float skillBonus = Mathf.Lerp(-0.25f, 0.2f, GetSkillT());
            if (_currentAction == AiAction.RunToRing)
                return CombatMoveSpeed + skillBonus + 0.25f;
            if (_currentAction == AiAction.FollowTeam || _currentAction == AiAction.FollowOrder || _currentAction == AiAction.ReviveTeamMate)
                return CombatMoveSpeed + skillBonus;

            if (_state == AiState.Fighting)
            {
                if (_target != null && _canSeeTarget)
                {
                    float distance = Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude;
                    if (distance <= GetPreferredFightRange())
                        return Mathf.Lerp(2.35f, 3.1f, GetSkillT());
                }

                return Mathf.Lerp(2.8f, 3.35f, GetSkillT());
            }
            if (_state == AiState.Fighting || _state == AiState.Advancing || _state == AiState.Evading || _state == AiState.Searching)
                return CombatMoveSpeed + skillBonus;
            return MoveSpeed + skillBonus;
        }

        private bool HasUsableWeapon()
        {
            return HasCombatWeapon() && (_magazineAmmo > 0 || _reserveAmmo > 0 || _isReloading);
        }

        private bool HasCombatWeapon()
        {
            return _hasWeapon && _equippedWeaponId >= 0 && _weaponProfile.CombatClass != WeaponCombatClass.Unarmed;
        }

        private float GetSkillT()
        {
            return Mathf.Clamp01((_skillLevel - 1) / 4f);
        }

        private float GetDamageRange()
        {
            float weaponRange = Mathf.Lerp(_weaponProfile.MaxRange * 0.72f, _weaponProfile.MaxRange, GetSkillT());
            return Mathf.Min(weaponRange, MaxFairGunDamageRange);
        }

        private float GetPreferredFightRange()
        {
            return Mathf.Lerp(_weaponProfile.PreferredRange * 0.86f, _weaponProfile.PreferredRange, GetSkillT());
        }

        private float GetMinimumFightRange()
        {
            return Mathf.Max(2.2f, _weaponProfile.MinRange);
        }

        private float GetWeaponRangeTolerance()
        {
            switch (_weaponProfile.CombatClass)
            {
                case WeaponCombatClass.Sniper:
                case WeaponCombatClass.AutoSniper:
                    return 16f;
                case WeaponCombatClass.Shotgun:
                    return 4f;
                case WeaponCombatClass.Smg:
                    return 8f;
                default:
                    return 11f;
            }
        }

        private float GetSemiFireInterval()
        {
            return UnityEngine.Random.Range(
                _weaponProfile.FireInterval * Mathf.Lerp(1.25f, 0.86f, GetSkillT()),
                _weaponProfile.FireInterval * Mathf.Lerp(1.65f, 1.08f, GetSkillT()));
        }

        private float GetAimCone()
        {
            return Mathf.Lerp(34f, 16f, GetSkillT());
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

            _physicalInput =
                _player.PlayerObject.GetComponent<InputHandler>() ??
                _player.PlayerObject.GetComponentInChildren<InputHandler>() ??
                _player.PlayerObject.GetComponentInParent<InputHandler>();
            Hip hip =
                _player.PlayerObject.GetComponent<Hip>() ??
                _player.PlayerObject.GetComponentInChildren<Hip>() ??
                _player.PlayerObject.GetComponentInParent<Hip>();
            RotationTarget rotationTarget =
                _player.PlayerObject.GetComponent<RotationTarget>() ??
                _player.PlayerObject.GetComponentInChildren<RotationTarget>() ??
                _player.PlayerObject.GetComponentInParent<RotationTarget>();
            _physicalHip = hip != null ? hip.transform : null;
            _physicalRotationTarget = rotationTarget != null ? rotationTarget.transform : null;

            string hookState = $"input={_physicalInput != null}, hip={_physicalHip != null}, rotationTarget={_physicalRotationTarget != null}";
            if (!string.Equals(_lastPhysicalHookLog, hookState, StringComparison.Ordinal))
            {
                _lastPhysicalHookLog = hookState;
                FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} physical hooks: {hookState}.");
            }
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
            _jumpFlagTimer = 0.18f;
            _jumpVisualTimer = 0.42f;
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

            return HasLineToPoint(target.PlayerPosition + Vector3.up * 1.55f, allowGround: true, target) ||
                HasLineToPoint(target.PlayerPosition + Vector3.up * 0.95f, allowGround: true, target);
        }

        private bool HasShotLine(TABGPlayerServer target)
        {
            if (target == null)
                return false;

            return HasLineFromMuzzle(target.PlayerPosition + Vector3.up * 1.35f, target) ||
                HasLineFromMuzzle(target.PlayerPosition + Vector3.up * 0.9f, target);
        }

        private bool HasCoverFromTarget(Vector3 candidate)
        {
            Vector3 threatPosition;
            if (!TryGetThreatPosition(out threatPosition))
                return false;

            Vector3 start = threatPosition + Vector3.up * 1.2f;
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
                if (IsOwnCollider(collider) || IsThreatCollider(collider))
                    continue;
                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.7f)
                    return true;
            }

            return false;
        }

        private bool IsThreatCollider(Collider collider)
        {
            return IsTargetCollider(collider, _target) || IsTargetCollider(collider, _threatTarget);
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
            if (!allowGround && target != null)
                return HasClearMapLine(point) ||
                    HasClearMapLine(target.PlayerPosition + Vector3.up * 0.75f);

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

        private bool HasLineFromMuzzle(Vector3 point, TABGPlayerServer target)
        {
            Vector3 start = GetMuzzlePosition();
            Vector3 delta = point - start;
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
                if (collider != null && collider.GetComponentInParent<Player>() != null)
                    return false;
                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.9f)
                    return false;
            }

            return true;
        }

        private Vector3 GetMuzzlePosition()
        {
            return _player.PlayerPosition + Vector3.up * 1.3f;
        }

        private bool HasClearMapLine(Vector3 point)
        {
            Vector3 start = _player.PlayerPosition + Vector3.up * 1.35f;
            Vector3 delta = point - start;
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
                if (IsOwnCollider(collider))
                    continue;
                if (collider != null && collider.GetComponentInParent<Player>() != null)
                    continue;
                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.92f)
                    return false;
            }

            return true;
        }

        private bool HasExplosionLine(TABGPlayerServer target)
        {
            if (target == null)
                return false;

            Vector3 start = _pendingGrenadePosition + Vector3.up * 0.6f;
            Vector3 end = target.PlayerPosition + Vector3.up * 1.0f;
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
                if (collider != null && collider.GetComponentInParent<Player>() != null)
                    continue;
                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.75f)
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

        private bool IsBadTerrain(Vector3 nearPosition)
        {
            if (IsOutsidePlayableBounds(nearPosition))
                return true;

            GroundProbe probe;
            if (!TryFindGroundInfo(nearPosition, out probe))
                return true;

            return probe.BadTerrain;
        }

        private bool TryFindGroundY(Vector3 nearPosition, out float y)
        {
            GroundProbe probe;
            if (TryFindGroundInfo(nearPosition, out probe))
            {
                y = probe.Y;
                return true;
            }

            y = nearPosition.y;
            return false;
        }

        private bool TryFindGroundInfo(Vector3 nearPosition, out GroundProbe probe)
        {
            probe = new GroundProbe
            {
                Found = false,
                BadTerrain = true,
                Y = nearPosition.y,
                Normal = Vector3.up,
                SurfaceName = string.Empty
            };

            if (IsOutsidePlayableBounds(nearPosition))
                return false;

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
                    probe.Y = hit.point.y;
                    probe.Normal = hit.normal;
                    probe.SurfaceName = hit.collider != null ? hit.collider.name : string.Empty;
                    bestDistance = distance;
                    found = true;
                }
            }

            probe.Found = found;
            if (!found)
                return false;

            probe.BadTerrain = IsBadGround(probe);
            return true;
        }

        private static bool IsBadGround(GroundProbe probe)
        {
            if (!probe.Found)
                return true;

            if (Vector3.Dot(probe.Normal, Vector3.up) < 0.42f)
                return true;

            string surface = probe.SurfaceName ?? string.Empty;
            surface = surface.ToLowerInvariant();
            return surface.Contains("water") ||
                surface.Contains("ocean") ||
                surface.Contains("sea") ||
                surface.Contains("lake") ||
                surface.Contains("river");
        }

        private static bool IsOutsidePlayableBounds(Vector3 position)
        {
            return position.x < PlayableMinX ||
                position.x > PlayableMaxX ||
                position.z < PlayableMinZ ||
                position.z > PlayableMaxZ;
        }

        private static bool IsShootableWeapon(NetworkGun loot)
        {
            return loot != null && AiDummyCatalog.IsShootableWeapon(loot.UniqueIdentifier);
        }

        public string GetDebugSummary()
        {
            string targetName = _target != null ? _target.PlayerName : (HasActiveThreatMemory() ? (_soundMemoryTimer > 0f ? "last-heard" : "last-seen") : "none");
            string lootName = _wantedLoot != null ? _wantedLoot.WeaponName : "none";
            float targetDistance = _target != null ? Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude : -1f;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} idx={1} state={2} hp={3:0} weapon={4} class={5} ammo={6}/{7} reserve={8} target={9} dist={10:0} los={11} threat={12:0.0}s sound={13:0.0}s loot={14} reload={15:0.0}s goal=({16:0},{17:0},{18:0}) action={19} score={20:0} reason=\"{21}\" top3=[{22}] ring={23:0.00} wanted={24} leader={25} order={26} revive={27}",
                _player.PlayerName,
                _player.PlayerIndex,
                _state,
                _player.Health,
                _player.WeaponName,
                _weaponProfile.CombatClass,
                _magazineAmmo,
                _weaponProfile.MagazineSize,
                _reserveAmmo,
                targetName,
                targetDistance,
                _canSeeTarget,
                Mathf.Max(0f, _threatMemoryTimer),
                Mathf.Max(0f, _soundMemoryTimer),
                lootName,
                Mathf.Max(0f, _reloadTimer),
                _currentDestination.x,
                _currentDestination.y,
                _currentDestination.z,
                _currentAction,
                _currentUtilityScore,
                _stateReason,
                _topUtilityScores,
                _lastRingDanger,
                _wantedLootKind,
                _teamLeader != null ? _teamLeader.PlayerName : "none",
                _hasTeamOrder ? (_teamOrderIsPing ? "ping" : "marker") + "#" + _teamOrderSequence : "none",
                _reviveTarget != null ? _reviveTarget.PlayerName : "none");
        }

        public string DebugState
        {
            get { return _state.ToString(); }
        }

        public string DebugAction
        {
            get { return _currentAction.ToString(); }
        }

        public string DebugStateReason
        {
            get { return _stateReason; }
        }

        public float DebugUtilityScore
        {
            get { return _currentUtilityScore; }
        }

        public string DebugTopUtilityScores
        {
            get { return _topUtilityScores; }
        }

        public float DebugRingDanger
        {
            get { return _lastRingDanger; }
        }

        public string DebugTargetName
        {
            get { return _target != null ? _target.PlayerName : (HasActiveThreatMemory() ? (_soundMemoryTimer > 0f ? "last-heard" : "last-seen") : string.Empty); }
        }

        public string DebugWeaponName
        {
            get { return _player != null ? _player.WeaponName : string.Empty; }
        }

        public bool DebugHasLineOfSight
        {
            get { return _canSeeTarget; }
        }

        public bool DebugIsFiring
        {
            get { return _isFullAutoFiring || _fireAnimationTimer > 0f; }
        }

        public bool DebugHasMoveGoal
        {
            get { return _currentDestination != Vector3.zero; }
        }

        public Vector3 DebugMoveGoal
        {
            get { return _currentDestination; }
        }

        public bool DebugHasLootGoal
        {
            get { return _wantedLoot != null; }
        }

        public Vector3 DebugLootGoal
        {
            get { return _wantedLoot != null ? _wantedLoot.Position : Vector3.zero; }
        }

        public string DebugLootName
        {
            get { return _wantedLoot != null ? _wantedLoot.WeaponName : string.Empty; }
        }

        public string DebugWantedLootKind
        {
            get { return _wantedLootKind; }
        }

        private void OnDestroy()
        {
            ClearEnemyPing();
            CancelTeamRevive();
            ReleaseLootClaim();
            StopFullAuto();
            LeaveVehicle();
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
