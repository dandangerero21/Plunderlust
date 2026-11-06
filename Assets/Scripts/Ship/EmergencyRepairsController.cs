using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Subsystems;
using Plunderlust.UI;
using UnityEngine;

namespace Plunderlust.Ship
{
    /// <summary>
    /// Phase 6 Resource-Based Emergency At-Sea Repair System.
    /// Allows the player to order the shipwrights and crew to patch hull breaches
    /// using Timber and mend shredded canvas rigging using Canvas at sea.
    /// Enhanced by Carpenter and Sailmaster specialists with real-time cooldown timers.
    /// </summary>
    public class EmergencyRepairsController : MonoBehaviour
    {
        public static EmergencyRepairsController Instance { get; private set; }

        [Header("Resource Costs")]
        [SerializeField] private int _timberPerHullPatch = 1;
        [SerializeField] private int _canvasPerSailMend = 1;

        [Header("Repair Values")]
        [SerializeField] private float _baseHullRestoreAmount = 100f;
        [SerializeField] private float _baseSailsRestoreAmount = 100f;
        [SerializeField] private float _bilgePumpAmount = 45f;
        [SerializeField] private float _repairDuration = 8.0f;
        [SerializeField] private float _repairCooldown = 15.0f;

        private ShipCargoHold _cargo;
        private ShipSubsystemController _subsystems;
        private CrewManager _crew;

        private float _hullRepairTimer = 0f;
        private float _hullRepairDurationTimer = 0f;
        private float _sailsRepairTimer = 0f;
        private float _sailsRepairDurationTimer = 0f;

        private bool _isRepairingHull = false;
        private bool _isRepairingSails = false;

        public bool IsRepairingHull => _isRepairingHull;
        public bool IsRepairingSails => _isRepairingSails;

        public float HullCooldownNormalized => Mathf.Clamp01(_hullRepairTimer / _repairCooldown);
        public float SailsCooldownNormalized => Mathf.Clamp01(_sailsRepairTimer / _repairCooldown);

        private void Awake()
        {
            if (CompareTag("Player") || Instance == null)
            {
                Instance = this;
            }

            _cargo = GetComponent<ShipCargoHold>();
            _subsystems = GetComponent<ShipSubsystemController>();
            _crew = GetComponent<CrewManager>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Handle Cooldowns
            if (_hullRepairTimer > 0f) _hullRepairTimer = Mathf.Max(0f, _hullRepairTimer - dt);
            if (_sailsRepairTimer > 0f) _sailsRepairTimer = Mathf.Max(0f, _sailsRepairTimer - dt);

            // Progressive Active Hull Repair Over Time
            if (_isRepairingHull)
            {
                _hullRepairDurationTimer -= dt;
                float repairRate = (_baseHullRestoreAmount / _repairDuration) * dt;

                // Carpenter perk: +50% faster repair speed
                if (_crew != null && _crew.Carpenter != null)
                {
                    repairRate *= 1.5f;
                }

                if (_subsystems != null)
                {
                    _subsystems.RepairSubsystem(ShipSubsystemType.Hull, repairRate);
                    _subsystems.RepairSubsystem(ShipSubsystemType.RudderHelm, repairRate * 0.4f);
                }

                if (_hullRepairDurationTimer <= 0f)
                {
                    _isRepairingHull = false;
                    if (PirateCombatHUD.Instance != null)
                    {
                        PirateCombatHUD.Instance.ShowBanner("HULL PATCH COMPLETE: Breach sealed!", Color.green, 2.5f);
                    }
                }
            }

            // Progressive Active Sails Repair Over Time
            if (_isRepairingSails)
            {
                _sailsRepairDurationTimer -= dt;
                float repairRate = (_baseSailsRestoreAmount / _repairDuration) * dt;

                // Sailmaster perk: +50% faster canvas mending
                if (_crew != null && _crew.Sailmaster != null)
                {
                    repairRate *= 1.5f;
                }

                if (_subsystems != null)
                {
                    _subsystems.RepairSubsystem(ShipSubsystemType.SailsRigging, repairRate);
                }

                if (_sailsRepairDurationTimer <= 0f)
                {
                    _isRepairingSails = false;
                    if (PirateCombatHUD.Instance != null)
                    {
                        PirateCombatHUD.Instance.ShowBanner("RIGGING REPAIRED: Canvas mended!", Color.cyan, 2.5f);
                    }
                }
            }

            // Keyboard Shortcuts: H for Hull Patch, J for Rigging Mend
            if (Input.GetKeyDown(KeyCode.H))
            {
                ExecuteHullRepair();
            }
            if (Input.GetKeyDown(KeyCode.J))
            {
                ExecuteSailsRepair();
            }
        }

        public bool CanRepairHull(out string reason)
        {
            if (_subsystems == null)
            {
                reason = "No subsystems found";
                return false;
            }

            if (_hullRepairTimer > 0f || _isRepairingHull)
            {
                reason = $"Carpenter busy ({Mathf.CeilToInt(_hullRepairTimer > 0f ? _hullRepairTimer : _hullRepairDurationTimer)}s)";
                return false;
            }

            if (_subsystems.HullHealth >= _subsystems.MaxHullHealth && _subsystems.FloodingRatio <= 0.01f)
            {
                reason = "Hull at full integrity";
                return false;
            }

            if (_cargo == null || _cargo.GetItemCount(CargoItemType.Timber) < _timberPerHullPatch)
            {
                reason = $"Need {_timberPerHullPatch}x Timber";
                return false;
            }

            reason = "Ready";
            return true;
        }

        public bool CanRepairSails(out string reason)
        {
            if (_subsystems == null)
            {
                reason = "No subsystems found";
                return false;
            }

            if (_sailsRepairTimer > 0f || _isRepairingSails)
            {
                reason = $"Sailmaster busy ({Mathf.CeilToInt(_sailsRepairTimer > 0f ? _sailsRepairTimer : _sailsRepairDurationTimer)}s)";
                return false;
            }

            if (_subsystems.SailsHealth >= _subsystems.MaxSailsHealth)
            {
                reason = "Rigging at 100% canvas";
                return false;
            }

            if (_cargo == null || _cargo.GetItemCount(CargoItemType.Canvas) < _canvasPerSailMend)
            {
                reason = $"Need {_canvasPerSailMend}x Canvas";
                return false;
            }

            reason = "Ready";
            return true;
        }

        public bool ExecuteHullRepair()
        {
            if (!CanRepairHull(out string reason))
            {
                if (PirateCombatHUD.Instance != null)
                {
                    PirateCombatHUD.Instance.ShowBanner($"[REPAIR FAILED]: {reason}", new Color(1f, 0.4f, 0.3f), 2.5f);
                }
                return false;
            }

            _cargo.ConsumeCargo(CargoItemType.Timber, _timberPerHullPatch);
            _isRepairingHull = true;
            _hullRepairDurationTimer = _repairDuration;
            _hullRepairTimer = _repairCooldown;

            // Immediately pump out a burst of bilge water to alleviate sinking
            if (_subsystems != null)
            {
                _subsystems.PumpBilgeWater(_bilgePumpAmount);
            }

            string carpName = (_crew != null && _crew.Carpenter != null) ? _crew.Carpenter.OfficerName : "Carpenter";
            if (PirateCombatHUD.Instance != null)
            {
                PirateCombatHUD.Instance.ShowBanner($"🪵 {carpName} patching hull breach (-1 Timber, +100 HP)!", Color.green, 3.0f);
            }

            return true;
        }

        public bool ExecuteSailsRepair()
        {
            if (!CanRepairSails(out string reason))
            {
                if (PirateCombatHUD.Instance != null)
                {
                    PirateCombatHUD.Instance.ShowBanner($"[REPAIR FAILED]: {reason}", new Color(1f, 0.4f, 0.3f), 2.5f);
                }
                return false;
            }

            _cargo.ConsumeCargo(CargoItemType.Canvas, _canvasPerSailMend);
            _isRepairingSails = true;
            _sailsRepairDurationTimer = _repairDuration;
            _sailsRepairTimer = _repairCooldown;

            string sailName = (_crew != null && _crew.Sailmaster != null) ? _crew.Sailmaster.OfficerName : "Sailmaster";
            if (PirateCombatHUD.Instance != null)
            {
                PirateCombatHUD.Instance.ShowBanner($"⛵ {sailName} mending canvas rigging (-1 Canvas, +100 HP)!", Color.cyan, 3.0f);
            }

            return true;
        }

        public bool TriggerEmergencyRepairs()
        {
            bool anySuccess = false;
            if (CanRepairHull(out _))
            {
                if (ExecuteHullRepair()) anySuccess = true;
            }
            if (CanRepairSails(out _))
            {
                if (ExecuteSailsRepair()) anySuccess = true;
            }
            return anySuccess;
        }
    }
}
