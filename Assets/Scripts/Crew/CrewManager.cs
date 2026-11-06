using System;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Core.Interfaces;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Crew
{
    /// <summary>
    /// Phase 3 Crew Management & Officer Specialty Controller.
    /// Simulates Deckhand manpower capacity, casualty checks from incoming cannonball fire,
    /// and drives passive subsystem maintenance loops for the 5 officer specialties:
    /// - Carpenter: Consumes Timber to repair Hull and Rudder integrity over time.
    /// - Boatswain (Bosun): Actively pumps bilge water and mitigates crew casualties.
    /// - Master Gunner: Tightens cannon spread and speeds broadside reloads.
    /// - Sailmaster: Consumes Canvas to mend Sails and Rigging at sea.
    /// - Quartermaster: Multiplies gold and salvage cargo yields from flotsam crates.
    /// Implements ICrewEfficiencyProvider with zero heap allocations in Update.
    /// </summary>
    [RequireComponent(typeof(ShipSubsystemController))]
    [DefaultExecutionOrder(-45)]
    public class CrewManager : MonoBehaviour, ICrewEfficiencyProvider
    {
        [Header("Hull Data Profile")]
        [SerializeField] private ShipHullDataSO _hullData;

        [Header("Deckhand Manpower")]
        [SerializeField] private int _currentDeckhands = 2;
        [SerializeField] private int _minRequiredCrew = 1;
        [SerializeField] private int _maxCrewCapacity = 2;

        [Header("Assigned Officer Specialists")]
        [SerializeField] private CrewSpecialistSO _carpenter;
        [SerializeField] private CrewSpecialistSO _bosun;
        [SerializeField] private CrewSpecialistSO _masterGunner;
        [SerializeField] private CrewSpecialistSO _sailmaster;
        [SerializeField] private CrewSpecialistSO _quartermaster;

        // Events for UI, audio, and gameplay triggers
        public event Action<int, int> OnCrewCountChanged; // (current, max)
        public event Action<int> OnCrewCasualty;           // (remaining)
        public event Action<OfficerSpecialtyType, CrewSpecialistSO> OnOfficerChanged;
        public event Action<OfficerSpecialtyType, string> OnOfficerActionTick;

        private ShipSubsystemController _subsystems;
        private ShipCargoHold _cargoHold;
        private OfficerVisualFXController _visualFX;

        // Timers for passive resource-consumption loops
        private float _carpenterTimer = 0f;
        private float _sailmasterTimer = 0f;

        // ICrewEfficiencyProvider Implementation
        public float ManpowerRatio => Mathf.Clamp01((float)_currentDeckhands / Mathf.Max(1, _minRequiredCrew));
        public float BaseManpowerEfficiency => Mathf.Lerp(0.35f, 1.0f, ManpowerRatio);

        public float SailHandlingEfficiency => BaseManpowerEfficiency * (_sailmaster != null ? (1.0f + _sailmaster.SecondaryPassiveValue) : 1.0f);
        public float RudderHandlingEfficiency => BaseManpowerEfficiency;
        public float GunnerEfficiency => BaseManpowerEfficiency * (_masterGunner != null ? (1.0f + _masterGunner.PrimaryPassiveValue) : 1.0f);
        public float BilgeRepairEfficiency => BaseManpowerEfficiency * (_bosun != null ? 1.45f : 1.0f);

        public int CurrentDeckhands => _currentDeckhands;
        public int MinRequiredCrew => _minRequiredCrew;
        public int MaxCrewCapacity => _maxCrewCapacity;
        public bool IsUndercrewed => _currentDeckhands < _minRequiredCrew;

        public int MaxOfficerCapacity
        {
            get
            {
                if (_hullData != null)
                {
                    if (_hullData.MaxCrewCapacity <= 2) return 1;  // Starter rowboat: 1 officer berth
                    if (_hullData.MaxCrewCapacity <= 8) return 2;  // Cutter: 2 officer berths
                    if (_hullData.MaxCrewCapacity <= 16) return 4; // Brigantine: 4 officer berths
                }
                return 1;
            }
        }

        public int AssignedOfficerCount
        {
            get
            {
                int count = 0;
                if (_carpenter != null) count++;
                if (_bosun != null) count++;
                if (_masterGunner != null) count++;
                if (_sailmaster != null) count++;
                if (_quartermaster != null) count++;
                return count;
            }
        }

        public bool HasFreeOfficerSlot => AssignedOfficerCount < MaxOfficerCapacity;

        public System.Collections.Generic.List<CrewSpecialistSO> GetAssignedOfficers()
        {
            var list = new System.Collections.Generic.List<CrewSpecialistSO>();
            if (_carpenter != null) list.Add(_carpenter);
            if (_bosun != null) list.Add(_bosun);
            if (_masterGunner != null) list.Add(_masterGunner);
            if (_sailmaster != null) list.Add(_sailmaster);
            if (_quartermaster != null) list.Add(_quartermaster);
            return list;
        }

        public bool DismissDeckhand()
        {
            if (_currentDeckhands <= 0) return false;
            _currentDeckhands--;
            OnCrewCountChanged?.Invoke(_currentDeckhands, _maxCrewCapacity);
            return true;
        }

        public CrewSpecialistSO Carpenter => _carpenter;
        public CrewSpecialistSO Bosun => _bosun;
        public CrewSpecialistSO MasterGunner => _masterGunner;
        public CrewSpecialistSO Sailmaster => _sailmaster;
        public CrewSpecialistSO Quartermaster => _quartermaster;

        private void Awake()
        {
            _subsystems = GetComponent<ShipSubsystemController>();
            _cargoHold = GetComponent<ShipCargoHold>();
            _visualFX = GetComponent<OfficerVisualFXController>();

            ApplyHullData();

            if (_subsystems != null)
            {
                _subsystems.OnSubsystemDamaged += HandleSubsystemDamaged;
            }
        }

        private void OnDestroy()
        {
            if (_subsystems != null)
            {
                _subsystems.OnSubsystemDamaged -= HandleSubsystemDamaged;
            }
        }

        public void SetHullData(ShipHullDataSO hullData)
        {
            _hullData = hullData;
            ApplyHullData();
        }

        private void ApplyHullData()
        {
            if (_hullData == null) return;

            _minRequiredCrew = Mathf.Max(1, _hullData.MinRequiredCrew);
            _maxCrewCapacity = Mathf.Max(_minRequiredCrew, _hullData.MaxCrewCapacity);
            _currentDeckhands = Mathf.Clamp(_currentDeckhands, 1, _maxCrewCapacity);
        }

        private void Update()
        {
            if (_subsystems == null || _subsystems.IsDead || _subsystems.IsSinking) return;

            float dt = Time.deltaTime;

            // 1. Boatswain (Bosun) Continuous Bilge Pumping Loop
            if (_bosun != null && _subsystems.CurrentBilgeWater > 0f)
            {
                float pumpRate = _bosun.PrimaryPassiveValue * BilgeRepairEfficiency;
                _subsystems.PumpBilgeWater(pumpRate * dt);

                if (_visualFX != null)
                {
                    _visualFX.TriggerBosunPumpFX();
                }
            }

            // 2. Carpenter Maintenance Loops
            if (_carpenter != null)
            {
                // Continuous passive trickle hull repair (+1.5 HP/sec) without requiring timber
                if (_subsystems.HullIntegrityRatio < 1.0f)
                {
                    _subsystems.RepairSubsystem(ShipSubsystemType.Hull, 1.5f * dt);
                }

                _carpenterTimer += dt;
                if (_carpenterTimer >= 2.5f)
                {
                    _carpenterTimer = 0f;
                    ExecuteCarpenterMaintenance();
                }
            }

            // 3. Sailmaster Maintenance Loops
            if (_sailmaster != null)
            {
                // Continuous passive canvas patch (+1.5 HP/sec) without requiring canvas
                if (_subsystems.SailsEfficiency < 1.0f)
                {
                    _subsystems.RepairSubsystem(ShipSubsystemType.SailsRigging, 1.5f * dt);
                }

                _sailmasterTimer += dt;
                if (_sailmasterTimer >= 3.0f)
                {
                    _sailmasterTimer = 0f;
                    ExecuteSailmasterMaintenance();
                }
            }
        }

        private void ExecuteCarpenterMaintenance()
        {
            bool needsHullRepair = _subsystems.HullIntegrityRatio < 0.98f;
            bool needsRudderRepair = _subsystems.RudderEfficiency < 0.98f;

            if (!needsHullRepair && !needsRudderRepair) return;

            // Check if Timber is available in cargo hold
            if (_cargoHold != null && _cargoHold.HasCargo(CargoItemType.Timber, 1))
            {
                _cargoHold.ConsumeCargo(CargoItemType.Timber, 1);

                float repairAmount = _carpenter.PrimaryPassiveValue * 3.5f;
                if (needsHullRepair)
                {
                    _subsystems.RepairSubsystem(ShipSubsystemType.Hull, repairAmount);
                }
                if (needsRudderRepair)
                {
                    _subsystems.RepairSubsystem(ShipSubsystemType.RudderHelm, repairAmount * 0.75f);
                }

                if (_visualFX != null)
                {
                    _visualFX.TriggerCarpenterRepairFX(transform.position + Vector3.up * 0.5f);
                }

                OnOfficerActionTick?.Invoke(OfficerSpecialtyType.Carpenter, $"Carpenter used 1 Timber (+{repairAmount:F0} HP)");
                Debug.Log($"<color=lime>[Carpenter Maintenance]</color> Planking repaired! (+{repairAmount:F0} HP)");
            }
        }

        private void ExecuteSailmasterMaintenance()
        {
            if (_subsystems.SailsEfficiency >= 0.98f) return;

            // Check if Canvas is available in cargo hold
            if (_cargoHold != null && _cargoHold.HasCargo(CargoItemType.Canvas, 1))
            {
                _cargoHold.ConsumeCargo(CargoItemType.Canvas, 1);

                float repairAmount = _sailmaster.PrimaryPassiveValue * 4.0f;
                _subsystems.RepairSubsystem(ShipSubsystemType.SailsRigging, repairAmount);

                if (_visualFX != null)
                {
                    _visualFX.TriggerSailmasterMendFX(transform.position + Vector3.up * 2.5f);
                }

                OnOfficerActionTick?.Invoke(OfficerSpecialtyType.Sailmaster, $"Sailmaster used 1 Canvas (+{repairAmount:F0} HP)");
                Debug.Log($"<color=lime>[Sailmaster Maintenance]</color> Canvas mended! (+{repairAmount:F0} HP)");
            }
        }

        private void HandleSubsystemDamaged(ShipSubsystemType system, float currentHP, float maxHP)
        {
            // Broadside hits to Gun Deck or Hull have a calculated chance to cause crew casualties
            if (system == ShipSubsystemType.Hull || system == ShipSubsystemType.GunDeck)
            {
                float casualtyMitigation = _bosun != null ? _bosun.SecondaryPassiveValue : 0f;
                float casualtyRoll = UnityEngine.Random.value;

                // Base 25% casualty chance on penetrating hit, reduced up to 40% by Bosun
                if (casualtyRoll < (0.25f * (1.0f - casualtyMitigation)) && _currentDeckhands > 1)
                {
                    _currentDeckhands--;
                    OnCrewCasualty?.Invoke(_currentDeckhands);
                    OnCrewCountChanged?.Invoke(_currentDeckhands, _maxCrewCapacity);

                    Debug.Log($"<color=red><b>[CREW CASUALTY!]</b></color> A deckhand was lost in battle! Remaining: {_currentDeckhands}/{_maxCrewCapacity}");
                }
            }
        }

        public bool AssignOfficer(CrewSpecialistSO specialist)
        {
            if (specialist == null) return false;

            switch (specialist.Specialty)
            {
                case OfficerSpecialtyType.Carpenter:
                    _carpenter = specialist;
                    break;
                case OfficerSpecialtyType.Boatswain:
                    _bosun = specialist;
                    break;
                case OfficerSpecialtyType.MasterGunner:
                    _masterGunner = specialist;
                    break;
                case OfficerSpecialtyType.Sailmaster:
                    _sailmaster = specialist;
                    break;
                case OfficerSpecialtyType.Quartermaster:
                    _quartermaster = specialist;
                    break;
            }

            OnOfficerChanged?.Invoke(specialist.Specialty, specialist);
            Debug.Log($"<color=gold>[Officer Hired]</color> {specialist.OfficerName} assigned as {specialist.Specialty}!");
            return true;
        }

        public bool DismissOfficer(OfficerSpecialtyType specialty)
        {
            switch (specialty)
            {
                case OfficerSpecialtyType.Carpenter:
                    _carpenter = null;
                    break;
                case OfficerSpecialtyType.Boatswain:
                    _bosun = null;
                    break;
                case OfficerSpecialtyType.MasterGunner:
                    _masterGunner = null;
                    break;
                case OfficerSpecialtyType.Sailmaster:
                    _sailmaster = null;
                    break;
                case OfficerSpecialtyType.Quartermaster:
                    _quartermaster = null;
                    break;
            }

            OnOfficerChanged?.Invoke(specialty, null);
            return true;
        }

        public CrewSpecialistSO GetOfficer(OfficerSpecialtyType specialty)
        {
            switch (specialty)
            {
                case OfficerSpecialtyType.Carpenter: return _carpenter;
                case OfficerSpecialtyType.Boatswain: return _bosun;
                case OfficerSpecialtyType.MasterGunner: return _masterGunner;
                case OfficerSpecialtyType.Sailmaster: return _sailmaster;
                case OfficerSpecialtyType.Quartermaster: return _quartermaster;
                default: return null;
            }
        }

        public bool RecruitDeckhands(int count)
        {
            if (count <= 0) return false;
            int newTotal = Mathf.Min(_currentDeckhands + count, _maxCrewCapacity);
            if (newTotal == _currentDeckhands) return false;

            _currentDeckhands = newTotal;
            OnCrewCountChanged?.Invoke(_currentDeckhands, _maxCrewCapacity);
            return true;
        }
    }
}
