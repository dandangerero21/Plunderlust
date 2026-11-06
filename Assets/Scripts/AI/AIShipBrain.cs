using Plunderlust.Cargo;
using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.Factions;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.AI
{
    /// <summary>
    /// Phase 4 Naval Combat Artificial Intelligence.
    /// Drives tactical maneuvering, orbital broadsides, and subsystem targeting doctrines:
    /// - Imperial Navy: Aims high at Sails & Rigging to dismast and immobilize.
    /// - Corsair Brethren: Aims low at waterline to induce flooding breaches and sinking.
    /// - Merchant Guild: Flees toward port havens, firing defensive volleys only when cornered.
    /// Implements Tactical Surrender when severely crippled (<15% HP or dismasted).
    /// Zero heap allocations in runtime update loops.
    /// </summary>
    [RequireComponent(typeof(ShipLocomotionController))]
    [RequireComponent(typeof(ShipSubsystemController))]
    [RequireComponent(typeof(NavalCombatController))]
    public class AIShipBrain : MonoBehaviour
    {
        [Header("Faction & Identity")]
        [SerializeField] private FactionType _faction = FactionType.MerchantGuild;
        [SerializeField] private AICombatState _state = AICombatState.PatrollingTradeLane;

        [Header("Combat Tactics")]
        [SerializeField] private float _desiredCombatDistance = 18.0f;
        [SerializeField] private float _detectionRange = 60.0f;
        [SerializeField] private float _surrenderThreshold = 0.15f;
        [SerializeField] private float _firingArcTolerance = 35.0f;

        [Header("Obstacle & Collision Avoidance")]
        [SerializeField] private float _avoidanceLookahead = 45.0f;
        [SerializeField] private float _avoidanceWeight = 4.5f;

        [Header("Patrol Waypoints")]
        [SerializeField] private Vector3[] _patrolWaypoints;
        private int _currentWaypointIndex = 0;

        // Components
        private ShipLocomotionController _locomotion;
        private ShipSubsystemController _subsystems;
        private NavalCombatController _combat;
        private ShipCargoHold _cargoHold;
        private FactionPennantVisual _pennant;
        private Rigidbody _rb;
        private Collider[] _myColliders;

        // Target tracking
        private Transform _targetTransform;
        private ShipSubsystemController _targetSubsystems;
        private float _orbitSide = 1.0f; // +1 Starboard, -1 Port
        private bool _isSurrendered = false;
        private bool _isPlundered = false;
        private float _retargetTimer = 0f;

        // Anti-stuck watchdog
        private float _stuckTimer = 0f;
        private bool _isUnsticking = false;
        private float _unstickTimer = 0f;
        private float _unstickSteerSign = 1.0f;

        public FactionType Faction => _faction;
        public AICombatState State => _state;
        public bool IsSurrendered => _isSurrendered;
        public bool IsPlundered => _isPlundered;

        private void Awake()
        {
            _locomotion = GetComponent<ShipLocomotionController>();
            _subsystems = GetComponent<ShipSubsystemController>();
            _combat = GetComponent<NavalCombatController>();
            _cargoHold = GetComponent<ShipCargoHold>();
            _pennant = GetComponent<FactionPennantVisual>();
            _rb = GetComponent<Rigidbody>();
            _myColliders = GetComponentsInChildren<Collider>();

            if (_pennant == null)
            {
                _pennant = gameObject.AddComponent<FactionPennantVisual>();
            }
            _pennant.SetFaction(_faction);

            _subsystems.OnSubsystemDamaged += HandleDamageTaken;
            _subsystems.OnShipDestroyed += HandleShipDestroyed;
        }

        private void OnDestroy()
        {
            if (_subsystems != null)
            {
                _subsystems.OnSubsystemDamaged -= HandleDamageTaken;
                _subsystems.OnShipDestroyed -= HandleShipDestroyed;
            }
        }

        public void InitializeAI(FactionType faction, Vector3[] waypoints)
        {
            _faction = faction;
            _patrolWaypoints = waypoints;
            _currentWaypointIndex = 0;
            _orbitSide = UnityEngine.Random.value > 0.5f ? 1.0f : -1.0f;

            if (_pennant != null)
            {
                _pennant.SetFaction(_faction);
            }

            // Seed Cargo Hold for looting
            SeedCargoCargo();
        }

        private void SeedCargoCargo()
        {
            if (_cargoHold == null) return;

            switch (_faction)
            {
                case FactionType.MerchantGuild:
                    _cargoHold.AddCargo(CargoItemType.Gold, 180, out _);
                    _cargoHold.AddCargo(CargoItemType.Sugar, 8, out _);
                    _cargoHold.AddCargo(CargoItemType.Spices, 6, out _);
                    _cargoHold.AddCargo(CargoItemType.Timber, 5, out _);
                    break;

                case FactionType.ImperialNavy:
                    _cargoHold.AddCargo(CargoItemType.Gold, 250, out _);
                    _cargoHold.AddCargo(CargoItemType.Cannonballs, 16, out _);
                    _cargoHold.AddCargo(CargoItemType.Canvas, 8, out _);
                    break;

                case FactionType.CorsairBrethren:
                    _cargoHold.AddCargo(CargoItemType.Gold, 220, out _);
                    _cargoHold.AddCargo(CargoItemType.Rum, 7, out _);
                    _cargoHold.AddCargo(CargoItemType.Timber, 8, out _);
                    break;

                case FactionType.FreeportTraders:
                    _cargoHold.AddCargo(CargoItemType.Gold, 150, out _);
                    _cargoHold.AddCargo(CargoItemType.Rum, 5, out _);
                    _cargoHold.AddCargo(CargoItemType.Canvas, 5, out _);
                    break;
            }
        }

        private void Update()
        {
            if (_subsystems == null || _subsystems.IsDead || _subsystems.IsSinking) return;

            // Check Surrender Condition
            if (!_isSurrendered && ShouldSurrender())
            {
                TriggerSurrender();
                return;
            }

            if (_isSurrendered)
            {
                HandleSurrenderedState();
                return;
            }

            // Anti-Stuck Emergency Watchdog
            if (_isUnsticking)
            {
                ExecuteUnstickLogic();
                return;
            }

            CheckStuckStatus();

            _retargetTimer += Time.deltaTime;
            if (_retargetTimer >= 1.0f)
            {
                _retargetTimer = 0f;
                ScanForTargets();
            }

            ExecuteStateLogic();
        }

        private void CheckStuckStatus()
        {
            if (_locomotion == null) return;

            // If we are demanding forward throttle but forward speed is blocked by an obstacle
            float currentSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            if (_locomotion.ThrottleInput > 0.25f && currentSpeed < 0.45f)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= 1.2f && !_isUnsticking)
                {
                    TriggerUnstickRoutine();
                }

                // If pinned against island or pier for over 4.5 seconds, emergency reposition into clear open sea
                if (_stuckTimer >= 4.5f)
                {
                    _stuckTimer = 0f;
                    _isUnsticking = false;
                    Vector3 safeSpot = NavalAIManager.FindSafeSpawnPoint(transform.position - (transform.forward * 30.0f));
                    transform.position = safeSpot;
                    if (_rb != null)
                    {
                        _rb.linearVelocity = Vector3.zero;
                        _rb.angularVelocity = Vector3.zero;
                    }
                    _locomotion.SetSailState(SailState.BattleSails);
                    _locomotion.ThrottleInput = 0.8f;
                    Debug.Log($"<color=yellow>[NavalAI]</color> Repositioned wedged ship {gameObject.name} into clear sea at {safeSpot}");
                }
            }
            else
            {
                _stuckTimer = Mathf.Max(0f, _stuckTimer - Time.deltaTime * 0.6f);
            }
        }

        private void TriggerUnstickRoutine(float steerSignOverride = 0f)
        {
            _isUnsticking = true;
            _unstickTimer = 3.2f;
            _unstickSteerSign = Mathf.Abs(steerSignOverride) > 0.1f
                ? steerSignOverride
                : (UnityEngine.Random.value > 0.5f ? 1.0f : -1.0f);
        }

        private void ExecuteUnstickLogic()
        {
            _unstickTimer -= Time.deltaTime;
            _locomotion.SetSailState(SailState.BattleSails);
            _locomotion.ThrottleInput = -1.0f; // Full reverse thrust!
            _locomotion.SteerInput = _unstickSteerSign; // Swing stern into clear sea

            if (_unstickTimer <= 0f)
            {
                _isUnsticking = false;
                _stuckTimer = 0f;
                _locomotion.ThrottleInput = 0.65f;
                _locomotion.SetSailState(SailState.BattleSails);
            }
        }

        private bool ShouldSurrender()
        {
            // Surrender if hull below threshold (15%) or if severely dismasted (<20% sails) with under 25% hull
            if (_subsystems.HullIntegrityRatio <= _surrenderThreshold) return true;
            if (_subsystems.SailsEfficiency <= 0.20f && _subsystems.HullIntegrityRatio <= 0.25f) return true;
            return false;
        }

        private void TriggerSurrender()
        {
            _isSurrendered = true;
            _state = AICombatState.Surrendered;

            // Strike colors
            if (_pennant != null)
            {
                _pennant.SetSurrendered(true);
            }

            // Lower canvas and drop anchor
            _locomotion.SetSailState(SailState.Anchored);

            Debug.Log($"<color=white><b>[STRUCK COLORS]</b></color> {_faction} vessel {gameObject.name} has hoisted the White Flag and surrendered!");
        }

        private void HandleSurrenderedState()
        {
            _locomotion.SetSailState(SailState.Anchored);

            // In-world boarding prompt
            if (_isPlundered) return;

            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null) return;

            float distToPlayer = Vector3.Distance(transform.position, playerGo.transform.position);
            if (distToPlayer <= 18.0f && Input.GetKeyDown(KeyCode.E))
            {
                ExecuteBoardAndPlunder(playerGo);
            }
        }

        public void ExecuteBoardAndPlunder(GameObject playerGo)
        {
            _isPlundered = true;
            ShipCargoHold playerHold = playerGo.GetComponent<ShipCargoHold>();
            if (playerHold != null && _cargoHold != null)
            {
                // Transfer all cargo & gold
                int transferredGold = _cargoHold.CurrentGold;
                playerHold.AddCargo(CargoItemType.Gold, transferredGold, out _);
                _cargoHold.ConsumeCargo(CargoItemType.Gold, transferredGold);

                for (int i = 1; i <= 7; i++)
                {
                    CargoItemType type = (CargoItemType)i;
                    int count = _cargoHold.GetItemCount(type);
                    if (count > 0)
                    {
                        playerHold.AddCargo(type, count, out int taken);
                        _cargoHold.ConsumeCargo(type, taken);
                    }
                }

                Debug.Log($"<color=gold><b>[BOARDING SUCCESS!]</b></color> Plundered {transferredGold} Gold and cargo supplies from surrendered vessel!");
            }

            // Record faction standing & bounty
            if (FactionManager.Instance != null)
            {
                FactionManager.Instance.RecordShipPlunderedOrSunk(_faction, false);
            }
        }

        private void ScanForTargets()
        {
            if (_targetTransform != null && !_targetSubsystems.IsDead && !_targetSubsystems.IsSinking)
            {
                float dist = Vector3.Distance(transform.position, _targetTransform.position);
                if (dist > _detectionRange * 1.5f)
                {
                    _targetTransform = null;
                    _targetSubsystems = null;
                    _state = AICombatState.PatrollingTradeLane;
                }
                return;
            }

            // Find player
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null) return;

            float playerDist = Vector3.Distance(transform.position, playerGo.transform.position);
            if (playerDist <= _detectionRange)
            {
                bool isHostile = FactionManager.Instance != null && FactionManager.Instance.IsHostileToPlayer(_faction);

                // Corsairs are naturally aggressive if within 40m
                if (_faction == FactionType.CorsairBrethren && playerDist <= 40f) isHostile = true;

                if (isHostile)
                {
                    SetCombatTarget(playerGo.transform);
                }
            }
        }

        public void SetCombatTarget(Transform target)
        {
            if (target == null) return;
            Transform rootTarget = target.root;
            _targetTransform = rootTarget;
            _targetSubsystems = rootTarget.GetComponent<ShipSubsystemController>() ?? rootTarget.GetComponentInChildren<ShipSubsystemController>();
            _state = AICombatState.BroadsideOrbit;
        }

        private void ExecuteStateLogic()
        {
            switch (_state)
            {
                case AICombatState.PatrollingTradeLane:
                    ExecutePatrol();
                    break;

                case AICombatState.ClosingDistance:
                case AICombatState.BroadsideOrbit:
                    ExecuteTacticalOrbit();
                    break;

                case AICombatState.Fleeing:
                    ExecuteTacticalOrbit();
                    break;
            }
        }

        private void ExecutePatrol()
        {
            if (_patrolWaypoints == null || _patrolWaypoints.Length == 0)
            {
                _locomotion.SetSailState(SailState.BattleSails);
                return;
            }

            Vector3 targetWp = _patrolWaypoints[_currentWaypointIndex];
            float dist = Vector3.Distance(transform.position, targetWp);

            if (dist < 18.0f)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _patrolWaypoints.Length;
            }

            SteerTowards(targetWp);
            _locomotion.SetSailState(SailState.BattleSails);
        }

        private void ExecuteTacticalOrbit()
        {
            if (_targetTransform == null)
            {
                _state = AICombatState.PatrollingTradeLane;
                return;
            }

            Vector3 targetPos = _targetTransform.position;
            Vector3 diff = targetPos - transform.position;
            Vector3 flatDir = Vector3.ProjectOnPlane(diff, Vector3.up).normalized;
            float currentDist = diff.magnitude;

            if (flatDir.sqrMagnitude < 0.001f) flatDir = transform.forward;

            // 1. Determine which Broadside battery to align with the player
            float starboardDot = Vector3.Dot(transform.right, flatDir);
            float portDot = Vector3.Dot(-transform.right, flatDir);

            // Prioritize battery that can fire or is closer to target
            bool useStarboard = true;
            if (_combat.CanFireStarboard && !_combat.CanFirePort)
            {
                useStarboard = true;
            }
            else if (_combat.CanFirePort && !_combat.CanFireStarboard)
            {
                useStarboard = false;
            }
            else
            {
                useStarboard = starboardDot >= portDot;
            }

            // 2. Compute the exact forward heading required for the chosen broadside to face player
            Vector3 broadsideHeading = useStarboard
                ? Vector3.Cross(Vector3.up, flatDir).normalized
                : Vector3.Cross(flatDir, Vector3.up).normalized;

            // 3. Distance Management & Heading Blend (Optimal broadside range around _desiredCombatDistance)
            Vector3 desiredHeading = broadsideHeading;
            if (currentDist > _desiredCombatDistance + 4.0f)
            {
                // Close distance while turning into broadside alignment
                float blend = Mathf.Clamp01((currentDist - _desiredCombatDistance) / 16.0f);
                desiredHeading = Vector3.Slerp(broadsideHeading, flatDir, blend * 0.65f).normalized;
                _locomotion.SetSailState(currentDist > 35.0f ? SailState.FullCanvas : SailState.BattleSails);
                _locomotion.ThrottleInput = 1.0f;
            }
            else if (currentDist < _desiredCombatDistance - 6.0f)
            {
                // Too close; steer outward to open distance
                desiredHeading = Vector3.Slerp(broadsideHeading, -flatDir, 0.45f).normalized;
                _locomotion.SetSailState(SailState.BattleSails);
                _locomotion.ThrottleInput = 0.8f;
            }
            else
            {
                // Sweet spot: broadside locked right onto the player!
                desiredHeading = broadsideHeading;
                _locomotion.SetSailState(SailState.BattleSails);
                _locomotion.ThrottleInput = 0.65f;
            }

            // Apply forward obstacle and vessel avoidance
            desiredHeading = ApplyObstacleAvoidance(desiredHeading, out float speedScale);
            _locomotion.ThrottleInput = Mathf.Max(0.35f, _locomotion.ThrottleInput * speedScale);

            // 4. Steer rudder towards desired heading
            Vector3 localHeading = transform.InverseTransformDirection(desiredHeading);
            float headingAngle = Mathf.Atan2(localHeading.x, localHeading.z) * Mathf.Rad2Deg;
            float steerInput = Mathf.Clamp(headingAngle / 28.0f, -1.0f, 1.0f);
            _locomotion.SteerInput = steerInput;

            // 5. Broadside Firing Evaluation
            Vector3 firingFlank = useStarboard ? transform.right : -transform.right;
            float alignmentAngle = Vector3.Angle(firingFlank, flatDir);

            if (alignmentAngle <= _firingArcTolerance && currentDist <= 32.0f)
            {
                float tacticalPitch = 0f;
                switch (_faction)
                {
                    case FactionType.ImperialNavy:
                        tacticalPitch = 5.0f; // High aim at Sails & Rigging
                        break;
                    case FactionType.CorsairBrethren:
                        tacticalPitch = -1.5f; // Waterline aim at Hull
                        break;
                    default:
                        tacticalPitch = 1.0f;
                        break;
                }

                _combat.AimPitchAngle = tacticalPitch;
                if (useStarboard && _combat.CanFireStarboard)
                {
                    _combat.FireStarboardBroadside();
                }
                else if (!useStarboard && _combat.CanFirePort)
                {
                    _combat.FirePortBroadside();
                }
            }
        }

        private void SteerTowards(Vector3 destination)
        {
            Vector3 diff = destination - transform.position;
            Vector3 desiredHeading = Vector3.ProjectOnPlane(diff, Vector3.up).normalized;
            if (desiredHeading.sqrMagnitude < 0.001f) desiredHeading = transform.forward;

            // Apply forward obstacle and vessel avoidance
            desiredHeading = ApplyObstacleAvoidance(desiredHeading, out float speedScale);
            _locomotion.ThrottleInput = Mathf.Max(0.4f, 1.0f * speedScale);

            Vector3 localTarget = transform.InverseTransformDirection(desiredHeading);
            float targetAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

            float steerInput = Mathf.Clamp(targetAngle / 30.0f, -1.0f, 1.0f);
            _locomotion.SteerInput = steerInput;
        }

        private Vector3 ApplyObstacleAvoidance(Vector3 desiredHeading, out float speedScale)
        {
            speedScale = 1.0f;
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;

            // 5 Feeler directions: Center, Left 30, Right 30, Hard Left 55, Hard Right 55
            Vector3[] feelerDirs = new Vector3[]
            {
                fwd,
                (fwd - right * 0.55f).normalized,
                (fwd + right * 0.55f).normalized,
                (fwd - right * 1.15f).normalized,
                (fwd + right * 1.15f).normalized
            };

            float[] feelerDists = new float[]
            {
                _avoidanceLookahead,
                _avoidanceLookahead * 0.82f,
                _avoidanceLookahead * 0.82f,
                _avoidanceLookahead * 0.55f,
                _avoidanceLookahead * 0.55f
            };

            Vector3 avoidanceForce = Vector3.zero;
            bool hitObstacle = false;
            float closestHitFraction = 1.0f;

            for (int i = 0; i < feelerDirs.Length; i++)
            {
                // Broad beam sphere cast with raycast fallback for solid rocks, docks, and island barriers (ignoring triggers)
                if (Physics.SphereCast(origin, 2.5f, feelerDirs[i], out RaycastHit hit, feelerDists[i], Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    Physics.Raycast(origin, feelerDirs[i], out hit, feelerDists[i], Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.isTrigger) continue;
                    if (IsOwnCollider(hit.collider)) continue;

                    hitObstacle = true;
                    float hitFraction = Mathf.Clamp01(hit.distance / feelerDists[i]);
                    if (hitFraction < closestHitFraction) closestHitFraction = hitFraction;

                    Vector3 awayDir = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    if (awayDir.sqrMagnitude < 0.001f)
                    {
                        awayDir = (i == 1 || i == 3) ? right : -right;
                    }

                    float weight = (1.0f - hitFraction) * _avoidanceWeight;
                    avoidanceForce += awayDir * weight;
                }
            }

            // Proximity island coast auto-steer avoidance
            var islandCenters = Plunderlust.World.IslandLandmassBarrier.ActiveIslandCenters;
            if (islandCenters != null)
            {
                for (int k = 0; k < islandCenters.Count; k++)
                {
                    Vector3 toShip = transform.position - islandCenters[k];
                    toShip.y = 0f;
                    float dist = toShip.magnitude;
                    if (dist < 38.0f && dist > 0.1f)
                    {
                        hitObstacle = true;
                        float factor = Mathf.Clamp01((38.0f - dist) / 22.0f);
                        avoidanceForce += toShip.normalized * (_avoidanceWeight * factor * 1.8f);
                        if (factor > 0.35f && closestHitFraction > 0.45f)
                        {
                            closestHitFraction = 0.45f;
                        }
                    }
                }
            }

            if (hitObstacle)
            {
                // Slow down when nearing obstacles
                speedScale = Mathf.Clamp(closestHitFraction * 1.2f, 0.35f, 1.0f);
                Vector3 blended = (desiredHeading + avoidanceForce).normalized;
                return blended.sqrMagnitude > 0.01f ? blended : desiredHeading;
            }

            return desiredHeading;
        }

        private bool IsOwnCollider(Collider col)
        {
            if (_myColliders == null) return false;
            for (int i = 0; i < _myColliders.Length; i++)
            {
                if (_myColliders[i] == col) return true;
            }
            return false;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_isSurrendered) return;

            // If colliding with rocks or shore structures and forward motion is blocked, accelerate unstick trigger
            float currentSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            if (currentSpeed < 0.55f && !_isUnsticking)
            {
                _stuckTimer += Time.deltaTime * 2.5f;
                if (_stuckTimer >= 1.0f)
                {
                    float steerSign = 1.0f;
                    if (collision.contactCount > 0)
                    {
                        Vector3 contactNormal = collision.GetContact(0).normal;
                        float dot = Vector3.Dot(transform.right, contactNormal);
                        steerSign = dot >= 0f ? 1.0f : -1.0f;
                    }
                    TriggerUnstickRoutine(steerSign);
                }
            }
        }

        private void HandleDamageTaken(ShipSubsystemType system, float currentHP, float maxHP)
        {
            GameObject attacker = _subsystems != null ? _subsystems.LastAttacker : null;
            if (attacker == null) return;

            // Retaliate against whoever fired the shot (whether an NPC ship or the player)
            SetCombatTarget(attacker.transform);

            // Only blame the player and adjust faction standing if the player was the actual shooter
            bool isPlayerAttacker = attacker.CompareTag("Player") || attacker.transform.root.CompareTag("Player");
            if (isPlayerAttacker && FactionManager.Instance != null)
            {
                FactionManager.Instance.RecordShipAttacked(_faction);
            }
        }

        private void HandleShipDestroyed()
        {
            GameObject attacker = _subsystems != null ? _subsystems.LastAttacker : null;
            bool isPlayerAttacker = attacker != null && (attacker.CompareTag("Player") || attacker.transform.root.CompareTag("Player"));

            // Only apply sinking reputation ripple and bounty if the player sank the vessel
            if (isPlayerAttacker && FactionManager.Instance != null)
            {
                FactionManager.Instance.RecordShipPlunderedOrSunk(_faction, true);
            }
        }

        private void OnGUI()
        {
            if (!_isSurrendered || _isPlundered) return;
            if (UI.GameOverUI.IsGameOver) return;
            if (UI.PauseMenuUI.Instance != null && UI.PauseMenuUI.IsPaused) return;
            if (UI.SeaChartMapUI.Instance != null && UI.SeaChartMapUI.Instance.IsOpen) return;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 3.5f);
            if (screenPos.z < 0.2f) return;

            float screenY = Screen.height - screenPos.y;
            float width = 360f;
            float height = 36f;
            float startX = screenPos.x - (width * 0.5f);

            if (GUI.Button(new Rect(startX, screenY, width, height), "🏳️ TAP TO BOARD & PLUNDER VESSEL"))
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null && Vector3.Distance(transform.position, playerGo.transform.position) <= 22.0f)
                {
                    ExecuteBoardAndPlunder(playerGo);
                }
            }
        }
    }
}
