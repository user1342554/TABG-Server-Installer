using System;
using System.Collections.Generic;
using System.Globalization;
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
            Scavenging,
            Advancing,
            Fighting,
            Evading,
            Searching,
            Wandering,
            Unstuck,
            Dropping
        }

        private enum WeaponCombatClass
        {
            Unarmed,
            Shotgun,
            Smg,
            AssaultRifle,
            Lmg,
            Sniper,
            AutoSniper,
            Pistol,
            Launcher,
            Special
        }

        private enum FirePlan
        {
            Semi,
            Burst,
            FullAuto
        }

        private enum HitZone
        {
            Limb,
            Body,
            Head
        }

        private struct WeaponProfile
        {
            public readonly WeaponCombatClass CombatClass;
            public readonly FirePlan FirePlan;
            public readonly float MinRange;
            public readonly float PreferredRange;
            public readonly float MaxRange;
            public readonly float BaseDamage;
            public readonly float FireInterval;
            public readonly int BurstShots;
            public readonly int MagazineSize;
            public readonly float ReloadSeconds;
            public readonly float CloseHitChance;
            public readonly float FarHitChance;

            public WeaponProfile(
                WeaponCombatClass combatClass,
                FirePlan firePlan,
                float minRange,
                float preferredRange,
                float maxRange,
                float baseDamage,
                float fireInterval,
                int burstShots,
                int magazineSize,
                float reloadSeconds,
                float closeHitChance,
                float farHitChance)
            {
                CombatClass = combatClass;
                FirePlan = firePlan;
                MinRange = minRange;
                PreferredRange = preferredRange;
                MaxRange = maxRange;
                BaseDamage = baseDamage;
                FireInterval = fireInterval;
                BurstShots = burstShots;
                MagazineSize = magazineSize;
                ReloadSeconds = reloadSeconds;
                CloseHitChance = closeHitChance;
                FarHitChance = farHitChance;
            }
        }

        private struct GroundProbe
        {
            public bool Found;
            public bool BadTerrain;
            public float Y;
            public Vector3 Normal;
            public string SurfaceName;
        }

        private struct PendingShot
        {
            public TABGPlayerServer Target;
            public Vector3 AimPoint;
            public float Timer;

            public PendingShot(TABGPlayerServer target, Vector3 aimPoint, float timer)
            {
                Target = target;
                AimPoint = aimPoint;
                Timer = timer;
            }
        }

        private const float MoveSpeed = 2.2f;
        private const float CombatMoveSpeed = 2.55f;
        private const float ChaseRange = 180f;
        private const float LootSearchRange = 260f;
        private const float WeaponlessLootSearchRange = 700f;
        private const float PickupRange = 5.2f;
        private const float ShootRange = 30f;
        private const float PreferredFightRange = 16f;
        private const float MinFightRange = 6f;
        private const float UnarmedDangerRange = 42f;
        private const float UnarmedEvadeDistance = 34f;
        private const float DamagePerShot = 5.5f;
        private const float AutoDamagePerBullet = 2.4f;
        private const float AutoBulletInterval = 0.11f;
        private const float LowHealthRetreatThreshold = 34f;
        private const float CriticalHealthRetreatThreshold = 22f;
        private const float CoverRefreshInterval = 1.15f;
        private const float FireAnimationWindow = 0.2f;
        private const float ShotDamageDelay = 0.07f;
        private const float ShotTraceRadius = 0.08f;
        private const float TargetStickinessSeconds = 1.8f;
        private const float MuzzleForwardOffset = 0.45f;
        private const float PoiArriveDistance = 18f;
        private const float BadTerrainRepathPenalty = 65f;
        private static readonly bool EnableGunDamage = true;
        private static readonly bool EnableGrenadeThrows = true;
        private static readonly bool EnableGrenadeDamage = true;
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
        private const float ThreatMemorySeconds = 13f;
        private const float GunshotHearRange = 155f;
        private const float MovementHearRange = 46f;
        private const float MovementHearSpeed = 5.2f;
        private const float SoundThreatMemorySeconds = 8f;
        private const float LootThreatSuppressionSeconds = 4.2f;
        private const float UnarmedPanicSeconds = 2.6f;
        private const float SearchRepathInterval = 0.95f;
        private const float SearchSweepRadius = 19f;
        private const float PathRebuildInterval = 0.45f;
        private const float PathDestinationRebuildDistance = 7f;
        private const float PathCornerReachDistance = 2.6f;
        private const float NavMeshSampleDistance = 8f;
        private const float VehicleSearchRange = 90f;
        private const float VehicleEnterRange = 6.5f;
        private const float VehicleUseMinTargetDistance = 80f;
        private const float VehicleMoveSpeed = 7.5f;
        private const float GrenadeThrowRange = 28f;
        private const float GrenadeSplashRadius = 8f;
        private const float GrenadeFuseTime = 2.35f;
        private const float DropStartHeight = 215f;
        private const float DropHorizontalSpeed = 42f;
        private const float DropVerticalSpeed = 33f;
        private const float DropFinishDistance = 5f;
        private const float DropExitDistance = 230f;
        private const float DropLandingSafetyLift = 3.0f;
        private const float DropPositionLockSeconds = 3.0f;
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

        private static readonly HashSet<int> BurstWeaponIds = new HashSet<int>
        {
            155, 156, 157, 158, 160, 264, 315, 320
        };

        private static readonly Vector3[] PoiTargets =
        {
            new Vector3(-520f, 0f, -500f),
            new Vector3(-405f, 0f, 145f),
            new Vector3(-115f, 0f, 215f),
            new Vector3(100f, 0f, -100f),
            new Vector3(425f, 0f, -350f),
            new Vector3(610f, 0f, 520f),
            new Vector3(-720f, 0f, 80f),
            new Vector3(430f, 0f, 120f),
            new Vector3(-40f, 0f, -560f)
        };

        private ServerClient _server;
        private GameRoom _room;
        private TABGPlayerServer _player;
        private TABGPlayerServer _target;
        private TABGPlayerServer _threatTarget;
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
        private float _evadeTimer;
        private float _coverTimer;
        private float _reactionDelayTimer;
        private float _targetStickinessTimer;
        private float _reloadTimer;
        private float _fireAnimationTimer;
        private float _burstShotTimer;
        private float _poiTimer;
        private float _dropPositionLockTimer;
        private float _lastLootDistance = float.MaxValue;
        private float _bestPlaneDropDistance = float.MaxValue;
        private Vector3 _pendingGrenadePosition;
        private string _lastTargetName;
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
        private int _lastGunshotSequence;
        private int _skillLevel = 1;
        private int _pathCornerIndex = 1;
        private int _navFailureCount;
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
        private bool _hasPoiTarget;
        private bool _dropStarted;
        private bool _dropFinished;
        private NavMeshPath _navPath;
        private WeaponProfile _weaponProfile;
        private readonly List<PendingShot> _pendingShots = new List<PendingShot>();
        private readonly Dictionary<byte, Vector3> _lastSoundPositions = new Dictionary<byte, Vector3>();

        public void Init(ServerClient server, TABGPlayerServer player, int skillLevel = 1)
        {
            _server = server;
            _room = server.GameRoomReference;
            _player = player;
            _skillLevel = Mathf.Clamp(skillLevel, 1, 5);
            _warmupTimer = WarmupTime;
            _weaponProfile = GetWeaponProfile(-1, null);
            RemoveBuiltInBotController();
            InitPhysicalEnemyAiHooks();
            InitTerrainOffset();
            _navPath = new NavMeshPath();
            _dropTarget = PickDropTarget();
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
            _autoDamageTimer -= dt;
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
            _fireAnimationTimer -= dt;
            _burstShotTimer -= dt;
            _poiTimer -= dt;

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
            TickSoundAwareness(dt);
            TickLootChoice();
            TickVehicleChoice();
            TickHealing();
            TickReload();
            TickGrenade();
            TickPendingShots(dt);
            DecideState();

            Vector3 destination = ChooseDestination();
            _currentDestination = destination;
            TrackStuck(destination);
            MoveToward(destination, dt);

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
            _wantedLoot = null;
            _lootTimer = 0f;
            _lootProgressTimer = 0f;
            PickNewWanderTarget();
        }

        private void TickTargeting()
        {
            if (_threatMemoryTimer <= 0f && _lastSeenTimer <= 0f)
            {
                _hasThreatMemory = false;
                _threatTarget = null;
            }

            if (_retargetTimer <= 0f || _target == null || _target.IsDead || _target.IsDowned)
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
                if (sound.ShooterIndex == _player.PlayerIndex || FakePlayersPlugin.FakeIndices.Contains(sound.ShooterIndex))
                    continue;

                TABGPlayerServer shooter = _room.FindPlayer(sound.ShooterIndex);
                if (shooter == null || shooter.Bot || shooter.IsDead || shooter.IsDowned)
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
                if (candidate == null || candidate == _player || candidate.Bot || candidate.IsDead || candidate.IsDowned)
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
            if (target == null || target.IsDead || target.IsDowned)
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

            if (!suppressLoot)
                return;

            _lootThreatSuppressionTimer = Mathf.Max(_lootThreatSuppressionTimer, LootThreatSuppressionSeconds);
            if (!HasUsableWeapon())
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
                _wantedLoot = nextLoot;
                _lastLootDistance = float.MaxValue;
                _lootProgressTimer = 0f;
                _lastLootIndex = _wantedLoot != null ? _wantedLoot.Index : int.MinValue;
                if (_wantedLoot != null)
                {
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

        private void TickHealing()
        {
            if (_healingItemCount <= 0 || _healingItemId < 0 || _player.Health >= 62f)
                return;

            _healingItemCount--;
            _player.RemoveLoot(_healingItemId, 1);
            float newHealth = Mathf.Min(100f, _player.Health + Mathf.Lerp(18f, 32f, GetSkillT()));
            FakePlayersPlugin.ApplyHeal(_server, _player, newHealth);
            FakePlayersPlugin.Log($"AI dummy {_player.PlayerName} used healing item; health {newHealth:0}.");
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
                if (candidate == null || candidate == _player || candidate.Bot || candidate.IsDead || candidate.IsDowned)
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

            bool hasLoot = _wantedLoot != null && _room.Weapons.Contains(_wantedLoot);
            bool hasThreat = HasActiveThreatMemory();
            bool hasVisibleTarget = _target != null && _canSeeTarget;
            bool hasUsableWeapon = HasUsableWeapon();
            AiState next;
            if (ShouldRetreat())
            {
                next = AiState.Evading;
            }
            else if (!hasUsableWeapon && hasThreat && !CanRiskUnarmedLoot(hasLoot))
            {
                next = AiState.Evading;
            }
            else if (hasVisibleTarget && !hasUsableWeapon)
            {
                next = AiState.Evading;
            }
            else if (hasVisibleTarget && hasUsableWeapon && Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude <= GetDamageRange())
            {
                next = AiState.Fighting;
            }
            else if (hasVisibleTarget && hasUsableWeapon)
            {
                next = AiState.Advancing;
            }
            else if (hasThreat && hasUsableWeapon)
            {
                next = AiState.Searching;
            }
            else if (NeedsAmmo() && hasLoot)
            {
                next = AiState.Scavenging;
            }
            else if (!hasUsableWeapon && hasLoot)
            {
                next = AiState.Looting;
            }
            else if (hasLoot && (!hasThreat || _lootThreatSuppressionTimer <= 0f))
            {
                next = AiState.Scavenging;
            }
            else if (hasThreat || _hasLastSeenTarget)
            {
                next = hasUsableWeapon ? AiState.Searching : AiState.Evading;
            }
            else if (_hasPoiTarget)
            {
                next = AiState.Scavenging;
            }
            else
            {
                next = AiState.Wandering;
            }

            SetState(next, UnityEngine.Random.Range(0.75f, 1.2f));
            _decisionTimer = 0.25f;
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
            return HasUsableWeapon() && _magazineAmmo <= 0 && _reserveAmmo <= 0;
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
            if (threatDistance <= UnarmedDangerRange + 14f && lootDistance > 8f)
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
            if (_target != null && !_target.IsDead && !_target.IsDowned && _canSeeTarget)
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

            if (_threatTarget != null && !_threatTarget.IsDead && !_threatTarget.IsDowned)
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
            _combatStrafeDistance = UnityEngine.Random.Range(2.0f, 4.8f);
            _combatForwardBias = UnityEngine.Random.Range(0.25f, 1.6f);
            _combatStrafeTimer = UnityEngine.Random.Range(0.85f, 1.65f);
            if (_state == AiState.Fighting && UnityEngine.Random.value < 0.18f)
                QueueJump();
        }

        private TABGPlayerServer FindTarget()
        {
            TABGPlayerServer current = _target != null && !_target.IsDead && !_target.IsDowned ? _target : null;
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
                if (candidate == null || candidate == _player || candidate.Bot || candidate.IsDead || candidate.IsDowned)
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

        private Vector3 ChooseDestination()
        {
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

            Vector3 side = Vector3.Cross(Vector3.up, fromThreat);
            float[] forwardSteps = { 5f, 10f, 15f };
            float[] sideSteps = { -SearchSweepRadius, -SearchSweepRadius * 0.5f, SearchSweepRadius * 0.5f, SearchSweepRadius };
            Vector3 best = _lastSeenTargetPosition;
            float bestScore = float.MinValue;

            for (int f = 0; f < forwardSteps.Length; f++)
            {
                for (int s = 0; s < sideSteps.Length; s++)
                {
                    Vector3 candidate = _lastSeenTargetPosition + fromThreat * forwardSteps[f] + side * sideSteps[s];
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
            }

            _searchDestination = best;
            _searchRepathTimer = UnityEngine.Random.Range(SearchRepathInterval, SearchRepathInterval + 0.8f);
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
            float preferredRange = GetPreferredFightRange();
            float minRange = GetMinimumFightRange();
            float stableShotRange = Mathf.Max(minRange + 2f, Mathf.Min(GetDamageRange() - 1f, preferredRange + GetWeaponRangeTolerance()));

            if ((_player.Health <= LowHealthRetreatThreshold || !hasShot) && _coverTimer <= 0f && TryFindCoverDestination(out _coverTarget))
            {
                _coverTimer = CoverRefreshInterval;
                return _coverTarget;
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
                radius = Mathf.Clamp(ringRadius * 0.35f, 25f, 90f);
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

            if (IsBadTerrain(next))
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
            if (distance > damageRange || _reactionDelayTimer > 0f || _isReloading)
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

            FakePlayersPlugin.BroadcastFire(_server, _player, aimPoint);
            _fireAnimationTimer = FireAnimationWindow;
            QueueShotDamage(target, aimPoint);

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
                _autoDamageTimer = Mathf.Max(ShotDamageDelay, _weaponProfile.FireInterval);
                FakePlayersPlugin.BroadcastFullAutoStart(_server, _player, aimPoint);
                _fireAnimationTimer = Mathf.Max(_fireAnimationTimer, 0.35f);
            }

            if (_autoDamageTimer <= 0f)
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
                QueueShotDamage(target, aimPoint);
                _autoDamageTimer = Mathf.Max(0.045f, _weaponProfile.FireInterval);
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

            FakePlayersPlugin.BroadcastFullAutoStop(_server, _player, Mathf.Max(1, _autoBulletsFired));
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

            FakePlayersPlugin.BroadcastFire(_server, _player, aimPoint);
            _fireAnimationTimer = FireAnimationWindow;
            QueueShotDamage(target, aimPoint);

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

        private void QueueShotDamage(TABGPlayerServer target, Vector3 aimPoint)
        {
            if (!EnableGunDamage || target == null)
                return;

            _pendingShots.Add(new PendingShot(target, aimPoint, ShotDamageDelay));
        }

        private void TickPendingShots(float dt)
        {
            for (int i = _pendingShots.Count - 1; i >= 0; i--)
            {
                PendingShot shot = _pendingShots[i];
                shot.Timer -= dt;
                if (shot.Timer > 0f)
                {
                    _pendingShots[i] = shot;
                    continue;
                }

                if (shot.Target != null && !shot.Target.IsDead && !shot.Target.IsDowned)
                {
                    float damage;
                    if (TryResolveShotDamage(shot.Target, shot.AimPoint, out damage))
                        FakePlayersPlugin.ApplyDamage(_server, _player, shot.Target, damage);
                }

                _pendingShots.RemoveAt(i);
            }
        }

        private void TickReload()
        {
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

        private bool TryResolveShotDamage(TABGPlayerServer target, Vector3 aimPoint, out float damage)
        {
            damage = 0f;
            if ((_fireAnimationTimer <= 0f && !_isFullAutoFiring) || target == null)
                return false;

            float distance = Flat(target.PlayerPosition - _player.PlayerPosition).magnitude;
            float range = GetDamageRange();
            if (distance > range)
                return false;

            float hitChance = GetWeaponHitChance(distance, range);
            if (UnityEngine.Random.value > hitChance)
                return false;

            HitZone zone;
            Vector3 hitPoint;
            if (!TryRaycastShot(target, aimPoint, out zone, out hitPoint))
                return false;

            float falloff = GetDistanceFalloff(distance, range);
            float skillDamage = Mathf.Lerp(0.78f, 1.08f, GetSkillT());
            damage = Mathf.Max(0.5f, _weaponProfile.BaseDamage * falloff * skillDamage * GetHitZoneMultiplier(zone));
            return damage > 0.1f;
        }

        private bool TryRaycastShot(TABGPlayerServer target, Vector3 aimPoint, out HitZone zone, out Vector3 hitPoint)
        {
            zone = HitZone.Body;
            hitPoint = aimPoint;

            Vector3 start = GetMuzzlePosition();
            Vector3 delta = aimPoint - start;
            float range = Mathf.Max(1f, GetDamageRange());
            float rayDistance = Mathf.Min(delta.magnitude + 2f, range);
            if (rayDistance < 0.1f)
                return false;

            Vector3 direction = delta.normalized;
            RaycastHit[] hits = Physics.RaycastAll(
                start,
                direction,
                rayDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestBlock = float.MaxValue;
            float closestTarget = float.MaxValue;
            Vector3 targetHitPoint = aimPoint;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (IsOwnCollider(collider))
                    continue;

                if (IsTargetCollider(collider, target))
                {
                    if (hit.distance < closestTarget)
                    {
                        closestTarget = hit.distance;
                        targetHitPoint = hit.point;
                    }
                    continue;
                }

                if (collider != null && collider.GetComponentInParent<Player>() != null)
                {
                    closestBlock = Mathf.Min(closestBlock, hit.distance);
                    continue;
                }

                if (Vector3.Dot(hit.normal, Vector3.up) < 0.92f)
                    closestBlock = Mathf.Min(closestBlock, hit.distance);
            }

            if (closestTarget == float.MaxValue)
            {
                RaycastHit[] sphereHits = Physics.SphereCastAll(
                    start,
                    ShotTraceRadius,
                    direction,
                    rayDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < sphereHits.Length; i++)
                {
                    RaycastHit hit = sphereHits[i];
                    Collider collider = hit.collider;
                    if (IsOwnCollider(collider))
                        continue;

                    if (IsTargetCollider(collider, target))
                    {
                        if (hit.distance < closestTarget)
                        {
                            closestTarget = hit.distance;
                            targetHitPoint = hit.point;
                        }
                        continue;
                    }

                    if (collider != null && collider.GetComponentInParent<Player>() != null)
                    {
                        closestBlock = Mathf.Min(closestBlock, hit.distance);
                        continue;
                    }

                    if (Vector3.Dot(hit.normal, Vector3.up) < 0.92f)
                        closestBlock = Mathf.Min(closestBlock, hit.distance);
                }
            }

            if (closestTarget < float.MaxValue && closestTarget < closestBlock)
            {
                hitPoint = targetHitPoint;
                zone = ClassifyHitZone(target, targetHitPoint);
                return true;
            }

            return false;
        }

        private Vector3 GetMuzzlePosition()
        {
            Vector3 forward = Quaternion.Euler(new Vector3(_player.PlayerRotation.x, _player.PlayerRotation.y, 0f)) * Vector3.forward;
            return _player.PlayerPosition + Vector3.up * 1.32f + Flat(forward).normalized * MuzzleForwardOffset;
        }

        private static Vector3 ClosestPointOnLine(Vector3 start, Vector3 direction, Vector3 point)
        {
            float t = Mathf.Max(0f, Vector3.Dot(point - start, direction));
            return start + direction * t;
        }

        private static HitZone ClassifyHitZone(TABGPlayerServer target, Vector3 hitPoint)
        {
            float height = hitPoint.y - target.PlayerPosition.y;
            if (height >= 1.48f)
                return HitZone.Head;
            if (height <= 0.62f)
                return HitZone.Limb;
            return HitZone.Body;
        }

        private float GetHitZoneMultiplier(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head:
                    return _weaponProfile.CombatClass == WeaponCombatClass.Shotgun ? 1.35f : 1.85f;
                case HitZone.Limb:
                    return 0.62f;
                default:
                    return 1f;
            }
        }

        private NetworkGun FindLoot()
        {
            NetworkGun best = null;
            float bestScore = float.MaxValue;
            float maxDistance = _hasWeapon ? LootSearchRange : WeaponlessLootSearchRange;
            bool threatActive = HasActiveThreatMemory();
            Vector3 threatPosition = Vector3.zero;
            bool hasThreatPosition = threatActive && TryGetThreatPosition(out threatPosition);

            for (int i = 0; i < _room.Weapons.Count; i++)
            {
                NetworkGun loot = _room.Weapons[i];
                if (loot == null)
                    continue;

                float distance = Flat(loot.Position - _player.PlayerPosition).magnitude;
                if (distance > maxDistance)
                    continue;

                Pickup pickup = GetPickup(loot);
                float value = ScoreLootValue(loot, pickup);
                if (value <= 0f)
                    continue;

                bool visible = HasLineToPoint(loot.Position + Vector3.up * 0.4f, allowGround: true);
                float score = distance - value + (visible ? 0f : 55f);
                if (_target != null && _canSeeTarget)
                    score += Mathf.Clamp(Flat(loot.Position - _target.PlayerPosition).magnitude * 0.12f, 0f, 20f);
                if (!_hasWeapon && hasThreatPosition)
                {
                    Vector3 toLoot = Flat(loot.Position - _player.PlayerPosition);
                    Vector3 toThreat = Flat(threatPosition - _player.PlayerPosition);
                    if (toLoot.sqrMagnitude > 1f && toThreat.sqrMagnitude > 1f)
                    {
                        float dot = Vector3.Dot(toLoot.normalized, toThreat.normalized);
                        if (dot > 0f)
                            score += dot * 95f;
                    }

                    float threatDistance = Flat(threatPosition - _player.PlayerPosition).magnitude;
                    if (threatDistance < UnarmedDangerRange + 10f && distance > 12f)
                        score += 120f;
                    if (visible)
                        score -= 8f;
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
                    return _grenadeCount < 2 ? 58f : 0f;

                case Pickup.WeaponType.Health:
                    if (!_hasWeapon && _player.Health >= 45f)
                        return 0f;
                    if (_player.Health < 75f)
                        return 68f;
                    return _healingItemCount < 2 ? 32f : 0f;

                case Pickup.WeaponType.Ammo:
                    if (!_hasWeapon)
                        return 0f;
                    if (NeedsAmmo())
                        return 95f;
                    return _reserveAmmo < _weaponProfile.MagazineSize * 2 ? 46f : 18f;

                case Pickup.WeaponType.Armor:
                case Pickup.WeaponType.Blessing:
                case Pickup.WeaponType.WeaponAttatchment:
                    if (!_hasWeapon)
                        return 0f;
                    return _target == null || !_canSeeTarget ? 24f : 0f;

                case Pickup.WeaponType.OtherConsumable:
                    if (!_hasWeapon)
                        return 0f;
                    return _target == null ? 14f : 0f;
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

        private static int GetWeaponScore(int itemId, string name)
        {
            WeaponProfile profile = GetWeaponProfile(itemId, name);
            int score = profile.FirePlan == FirePlan.FullAuto ? 70 : 52;
            string lower = (name ?? string.Empty).ToLowerInvariant();
            switch (profile.CombatClass)
            {
                case WeaponCombatClass.Sniper:
                case WeaponCombatClass.AutoSniper:
                    score += 26;
                    break;
                case WeaponCombatClass.AssaultRifle:
                case WeaponCombatClass.Lmg:
                    score += 20;
                    break;
                case WeaponCombatClass.Smg:
                    score += 13;
                    break;
                case WeaponCombatClass.Shotgun:
                    score += 6;
                    break;
                case WeaponCombatClass.Pistol:
                    score -= 8;
                    break;
                case WeaponCombatClass.Launcher:
                    score += 10;
                    break;
            }

            if (lower.Contains("barrett") || lower.Contains("awm") || lower.Contains("sniper"))
                score += 24;
            if (lower.Contains("scar") || lower.Contains("ak") || lower.Contains("m4") || lower.Contains("aug"))
                score += 18;
            if (lower.Contains("mp") || lower.Contains("vector") || lower.Contains("uzi"))
                score += 10;
            if (lower.Contains("shotgun") || lower.Contains("crossbow") || lower.Contains("flintlock"))
                score -= 18;
            if (lower.Contains("pistol") || lower.Contains("revolver"))
                score -= 12;
            return score;
        }

        private static WeaponProfile GetWeaponProfile(int itemId, string name)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();

            if (itemId < 0)
                return new WeaponProfile(WeaponCombatClass.Unarmed, FirePlan.Semi, 2f, 4f, 5f, 1f, 0.8f, 1, 1, 1f, 0f, 0f);

            if ((BurstWeaponIds.Contains(itemId) || lower.Contains("burst") || lower.Contains("famas") || lower.Contains("beam")) && !lower.Contains("sniper"))
                return new WeaponProfile(WeaponCombatClass.AssaultRifle, FirePlan.Burst, 10f, 25f, 58f, 5.4f, 0.095f, 3, 30, 2.25f, 0.58f, 0.27f);

            if ((itemId >= 292 && itemId <= 301) || itemId == 326 || lower.Contains("shotgun") || lower.Contains("mossberg") || lower.Contains("blunderbuss") || lower.Contains("aa-12") || lower.Contains("rainmaker") || lower.Contains("arnold"))
            {
                FirePlan plan = itemId == 292 || itemId == 296 ? FirePlan.FullAuto : FirePlan.Semi;
                return new WeaponProfile(WeaponCombatClass.Shotgun, plan, 3.5f, 8.5f, 26f, 15.5f, plan == FirePlan.FullAuto ? 0.18f : 0.92f, 1, plan == FirePlan.FullAuto ? 12 : 5, 2.6f, 0.72f, 0.16f);
            }

            if ((itemId >= 317 && itemId <= 328) || lower.Contains("awp") || lower.Contains("barrett") || lower.Contains("kar98") || lower.Contains("vss") || lower.Contains("sniper"))
            {
                FirePlan plan = itemId == 303 || itemId == 328 || lower.Contains("vss") ? FirePlan.FullAuto : FirePlan.Semi;
                WeaponCombatClass combatClass = plan == FirePlan.FullAuto ? WeaponCombatClass.AutoSniper : WeaponCombatClass.Sniper;
                return new WeaponProfile(combatClass, plan, plan == FirePlan.FullAuto ? 16f : 24f, plan == FirePlan.FullAuto ? 36f : 52f, plan == FirePlan.FullAuto ? 72f : 96f, plan == FirePlan.FullAuto ? 7.2f : 26f, plan == FirePlan.FullAuto ? 0.14f : 1.35f, 1, plan == FirePlan.FullAuto ? 20 : 8, plan == FirePlan.FullAuto ? 2.7f : 3.25f, 0.62f, 0.38f);
            }

            if ((itemId >= 302 && itemId <= 316) || lower.Contains("smg") || lower.Contains("mp5") || lower.Contains("mp-") || lower.Contains("vector") || lower.Contains("uzi") || lower.Contains("ump"))
                return new WeaponProfile(WeaponCombatClass.Smg, FirePlan.FullAuto, 5.5f, 16f, 42f, 4.7f, 0.085f, 1, 28, 1.85f, 0.64f, 0.2f);

            if ((itemId >= 151 && itemId <= 165) || lower.Contains("ak") || lower.Contains("aug") || lower.Contains("scar") || lower.Contains("m16") || lower.Contains("ar"))
                return new WeaponProfile(WeaponCombatClass.AssaultRifle, AutomaticWeaponIds.Contains(itemId) ? FirePlan.FullAuto : FirePlan.Semi, 9f, 27f, 64f, 7.2f, 0.12f, 1, 30, 2.15f, 0.62f, 0.28f);

            if ((itemId >= 217 && itemId <= 220) || itemId == 176 || itemId == 177 || itemId == 178 || lower.Contains("minigun") || lower.Contains("mg") || lower.Contains("bar"))
                return new WeaponProfile(WeaponCombatClass.Lmg, FirePlan.FullAuto, 11f, 32f, 70f, 6.4f, 0.075f, 1, 80, 3.15f, 0.56f, 0.22f);

            if (itemId == 179 || itemId == 181 || itemId == 182 || lower.Contains("launcher") || lower.Contains("rocket") || lower.Contains("missile"))
                return new WeaponProfile(WeaponCombatClass.Launcher, FirePlan.Semi, 12f, 30f, 68f, 18f, 1.45f, 1, 6, 3.4f, 0.45f, 0.18f);

            if ((itemId >= 264 && itemId <= 284) || lower.Contains("pistol") || lower.Contains("revolver") || lower.Contains("deagle") || lower.Contains("glock") || lower.Contains("m1911"))
            {
                FirePlan plan = AutomaticWeaponIds.Contains(itemId) ? FirePlan.FullAuto : FirePlan.Semi;
                return new WeaponProfile(WeaponCombatClass.Pistol, plan, 4.5f, 15f, 34f, plan == FirePlan.FullAuto ? 4.2f : 8.5f, plan == FirePlan.FullAuto ? 0.1f : 0.55f, 1, plan == FirePlan.FullAuto ? 20 : 8, 1.7f, 0.58f, 0.18f);
            }

            FirePlan fallbackPlan = AutomaticWeaponIds.Contains(itemId) ? FirePlan.FullAuto : FirePlan.Semi;
            return new WeaponProfile(WeaponCombatClass.Special, fallbackPlan, 7f, 20f, 44f, fallbackPlan == FirePlan.FullAuto ? 4.5f : 7f, fallbackPlan == FirePlan.FullAuto ? 0.12f : 0.75f, 1, fallbackPlan == FirePlan.FullAuto ? 25 : 8, 2.2f, 0.5f, 0.2f);
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
                return VehicleMoveSpeed;

            float skillBonus = Mathf.Lerp(-0.25f, 0.2f, GetSkillT());
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
            return _hasWeapon && _equippedWeaponId >= 0 && _weaponProfile.CombatClass != WeaponCombatClass.Unarmed;
        }

        private float GetSkillT()
        {
            return Mathf.Clamp01((_skillLevel - 1) / 4f);
        }

        private float GetDamageRange()
        {
            return Mathf.Lerp(_weaponProfile.MaxRange * 0.72f, _weaponProfile.MaxRange, GetSkillT());
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

        private float GetWeaponHitChance(float distance, float range)
        {
            float t = Mathf.Clamp01(distance / Mathf.Max(1f, range));
            float baseChance = Mathf.Lerp(_weaponProfile.CloseHitChance, _weaponProfile.FarHitChance, t);
            return Mathf.Clamp01(baseChance + Mathf.Lerp(-0.12f, 0.1f, GetSkillT()));
        }

        private float GetDistanceFalloff(float distance, float range)
        {
            float preferred = Mathf.Max(1f, _weaponProfile.PreferredRange);
            float t = Mathf.Clamp01((distance - preferred) / Mathf.Max(1f, range - preferred));
            if (_weaponProfile.CombatClass == WeaponCombatClass.Shotgun)
                return Mathf.Lerp(1.12f, 0.28f, t);
            if (_weaponProfile.CombatClass == WeaponCombatClass.Sniper || _weaponProfile.CombatClass == WeaponCombatClass.AutoSniper)
                return Mathf.Lerp(0.88f, 1.0f, Mathf.Clamp01(distance / preferred)) * Mathf.Lerp(1f, 0.76f, t);
            return Mathf.Lerp(1f, 0.58f, t);
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

        public string GetDebugSummary()
        {
            string targetName = _target != null ? _target.PlayerName : (HasActiveThreatMemory() ? (_soundMemoryTimer > 0f ? "last-heard" : "last-seen") : "none");
            string lootName = _wantedLoot != null ? _wantedLoot.WeaponName : "none";
            float targetDistance = _target != null ? Flat(_target.PlayerPosition - _player.PlayerPosition).magnitude : -1f;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} idx={1} state={2} hp={3:0} weapon={4} class={5} ammo={6}/{7} reserve={8} target={9} dist={10:0} los={11} threat={12:0.0}s sound={13:0.0}s loot={14} reload={15:0.0}s goal=({16:0},{17:0},{18:0})",
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
                _currentDestination.z);
        }

        public string DebugState
        {
            get { return _state.ToString(); }
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

        private void OnDestroy()
        {
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
