using System;
using Plunderlust.Core.Data;
using Plunderlust.Core.Interfaces;
using Plunderlust.Ocean;
using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.Subsystems
{
    /// <summary>
    /// Localized ship subsystem damage and active bilge water simulation.
    /// Manages Hull & Planking, Sails & Rigging, Rudder & Helm, and Gun Deck integrity.
    /// Evaluates projectile hit heights and coordinates to deliver tactical localized damage.
    /// Implements IShipSubsystemProvider to drive propulsion, turning, and buoyancy physics.
    /// </summary>
    [RequireComponent(typeof(BuoyancyBody))]
    [DefaultExecutionOrder(-50)]
    public class ShipSubsystemController : MonoBehaviour, IShipSubsystemProvider
    {
        [Header("Hull Profile Reference")]
        [SerializeField] private ShipHullDataSO _hullData;

        [Header("Subsystem Current Health")]
        [SerializeField] private float _hullHealth = 500f;
        [SerializeField] private float _sailsHealth = 300f;
        [SerializeField] private float _rudderHealth = 250f;
        [SerializeField] private float _gunDeckHealth = 400f;

        [Header("Subsystem Maximum Health")]
        [SerializeField] private float _maxHullHealth = 500f;
        [SerializeField] private float _maxSailsHealth = 300f;
        [SerializeField] private float _maxRudderHealth = 250f;
        [SerializeField] private float _maxGunDeckHealth = 400f;

        [Header("Bilge Water & Flooding Mechanics")]
        [Tooltip("Current volume of water taken on in the bilge in liters/units.")]
        [SerializeField] private float _currentBilgeWater = 0f;
        [Tooltip("Max bilge capacity before ship founders and sinks.")]
        [SerializeField] private float _maxBilgeCapacity = 1000f;
        [Tooltip("Base leak rate accumulated per second when hull is critically breached.")]
        [SerializeField] private float _baseLeakRate = 25f;

        [Header("Visual & Spatial Geometry Offsets")]
        [Tooltip("Height threshold above deck dividing Gun Deck from Sails/Rigging.")]
        [SerializeField] private float _sailsHitHeightThreshold = 2.0f;
        [Tooltip("Height threshold below which hits breach the Hull planking/waterline.")]
        [SerializeField] private float _hullHitHeightThreshold = 1.1f;
        [Tooltip("Normalized longitudinal fraction from stern (< 0.28 = rudder hit).")]
        [SerializeField] private float _sternRudderHitRatio = 0.28f;
        [Tooltip("Total physical length of the hull for longitudinal hit zoning.")]
        [SerializeField] private float _hullLength = 8.0f;

        [Header("State Telemetry")]
        [SerializeField] private bool _isSinking = false;
        [SerializeField] private bool _isDead = false;

        // Events for UI, audio, and visual FX
        public event Action<ShipSubsystemType, float, float> OnSubsystemDamaged; // Type, currentHP, maxHP
        public event Action<ShipSubsystemType, float, float> OnSubsystemRepaired; // Type, currentHP, maxHP
        public event Action<ShipSubsystemType, float, float, Vector3> OnSubsystemHitDetailed; // Type, damage, currentHP, worldHitPos
        public event Action OnShipStartedSinking;
        public event Action OnShipDestroyed;

        private BuoyancyBody _buoyancyBody;
        private Rigidbody _rb;

        // IShipSubsystemProvider Implementation
        public float SailsEfficiency => Mathf.Clamp(_sailsHealth / Mathf.Max(1f, _maxSailsHealth), 0.15f, 1.0f);
        public float RudderEfficiency => Mathf.Clamp(_rudderHealth / Mathf.Max(1f, _maxRudderHealth), 0.10f, 1.0f);
        public float GunDeckEfficiency => Mathf.Clamp(_gunDeckHealth / Mathf.Max(1f, _maxGunDeckHealth), 0.20f, 1.0f);
        public float HullIntegrityRatio => Mathf.Clamp01(_hullHealth / Mathf.Max(1f, _maxHullHealth));
        public float FloodingRatio => Mathf.Clamp01(_currentBilgeWater / Mathf.Max(1f, _maxBilgeCapacity));
        public bool IsRudderCritical => (_rudderHealth / Mathf.Max(1f, _maxRudderHealth)) <= 0.25f;
        public bool IsSailsCrippled => (_sailsHealth / Mathf.Max(1f, _maxSailsHealth)) <= 0.20f;
        public bool IsSinking => _isSinking;
        public bool IsDead => _isDead;

        public float HullHealth => _hullHealth;
        public float SailsHealth => _sailsHealth;
        public float RudderHealth => _rudderHealth;
        public float GunDeckHealth => _gunDeckHealth;
        public GameObject LastAttacker { get; private set; }
        public float MaxHullHealth => _maxHullHealth;
        public float MaxSailsHealth => _maxSailsHealth;
        public float MaxRudderHealth => _maxRudderHealth;
        public float MaxGunDeckHealth => _maxGunDeckHealth;
        public ShipHullDataSO HullData => _hullData;
        public float CurrentBilgeWater => _currentBilgeWater;
        public float MaxBilgeCapacity => _maxBilgeCapacity;

        private void Awake()
        {
            _buoyancyBody = GetComponent<BuoyancyBody>();
            _rb = GetComponent<Rigidbody>();

            if (GetComponent<Plunderlust.Combat.ShipWorldHealthBar>() == null)
            {
                gameObject.AddComponent<Plunderlust.Combat.ShipWorldHealthBar>();
            }

            ApplyHullData();
        }

        public void SetHullData(ShipHullDataSO hullData)
        {
            _hullData = hullData;
            ApplyHullData();
        }

        private void ApplyHullData()
        {
            if (_hullData == null) return;

            _maxHullHealth = _hullData.MaxHullHealth;
            _maxSailsHealth = _hullData.MaxSailsHealth;
            _maxRudderHealth = _hullData.MaxRudderHealth;
            _maxGunDeckHealth = _hullData.MaxGunDeckHealth;
            _maxBilgeCapacity = _hullData.MaxBilgeWaterCapacity;
            _baseLeakRate = _hullData.BaseLeakRatePerSec;

            _hullHealth = _maxHullHealth;
            _sailsHealth = _maxSailsHealth;
            _rudderHealth = _maxRudderHealth;
            _gunDeckHealth = _maxGunDeckHealth;
            _currentBilgeWater = 0f;

            // Approximate hull length from collider if present
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                _hullLength = col.bounds.size.z;
            }
        }

        private void Update()
        {
            if (_isDead) return;

            float dt = Time.deltaTime;

            // Active Hull Leaking & Bilge Water Accumulation
            // When Hull integrity drops below 50%, water begins leaking into the bilge
            float hullRatio = HullIntegrityRatio;
            if (hullRatio < 0.50f && !_isSinking)
            {
                // Breach severity scales from 0 (at 50% HP) to 1 (at 0% HP)
                float breachSeverity = (0.50f - hullRatio) / 0.50f;
                float leakRate = _baseLeakRate * breachSeverity * (1.0f + (breachSeverity * 0.5f));
                _currentBilgeWater = Mathf.Min(_currentBilgeWater + (leakRate * dt), _maxBilgeCapacity);
            }

            // Push current flooding ratio into BuoyancyBody for physical mass scaling
            if (_buoyancyBody != null)
            {
                _buoyancyBody.FloodingRatio = FloodingRatio;
            }

            // Check Sinking Condition
            if ((_hullHealth <= 0f || _currentBilgeWater >= _maxBilgeCapacity) && !_isSinking)
            {
                TriggerSinkingState();
            }
        }

        /// <summary>
        /// Evaluates a world space projectile hit, converts to local ship coordinates,
        /// and applies localized damage to the appropriate subsystem:
        /// - Stern rear 28%: Rudder & Helm
        /// - High elevation: Sails & Rigging
        /// - Waterline low elevation: Hull & Planking
        /// - Mid deck: Gun Deck
        /// </summary>
        public ShipSubsystemType ApplyLocalizedDamage(Vector3 hitWorldPoint, float rawDamage, Vector3 shotVelocity, GameObject attacker = null)
        {
            if (_isDead) return ShipSubsystemType.Hull;

            if (attacker != null)
            {
                LastAttacker = attacker;
            }

            // Apply Boatswain (Bosun) damage mitigation perk if present on crew
            float effectiveDamage = rawDamage;
            Plunderlust.Crew.CrewManager crew = GetComponent<Plunderlust.Crew.CrewManager>() ?? GetComponentInParent<Plunderlust.Crew.CrewManager>();
            if (crew != null && crew.Bosun != null)
            {
                effectiveDamage *= (1.0f - Mathf.Clamp01(crew.Bosun.SecondaryPassiveValue));
            }

            // Convert hit position to local ship coordinates
            Vector3 localHit = transform.InverseTransformPoint(hitWorldPoint);
            ShipSubsystemType hitSystem;

            // 1. Stern Raking Check: Hits in the rear longitudinal fraction damage the Rudder & Helm
            float halfLength = Mathf.Max(1.0f, _hullLength * 0.5f);
            float sternThresholdZ = -halfLength + (_hullLength * _sternRudderHitRatio);

            if (localHit.z <= sternThresholdZ && Mathf.Abs(localHit.x) < 1.4f)
            {
                hitSystem = ShipSubsystemType.RudderHelm;
                _rudderHealth = Mathf.Max(0f, _rudderHealth - effectiveDamage);
                _hullHealth = Mathf.Max(0f, _hullHealth - (effectiveDamage * 0.70f)); // Significant structural damage
                OnSubsystemDamaged?.Invoke(hitSystem, _rudderHealth, _maxRudderHealth);
                OnSubsystemHitDetailed?.Invoke(hitSystem, effectiveDamage, _rudderHealth, hitWorldPoint);
            }
            // 2. High Elevation Check: Hits high above the deck strike Sails & Rigging
            else if (localHit.y >= _sailsHitHeightThreshold)
            {
                hitSystem = ShipSubsystemType.SailsRigging;
                _sailsHealth = Mathf.Max(0f, _sailsHealth - effectiveDamage);
                _hullHealth = Mathf.Max(0f, _hullHealth - (effectiveDamage * 0.55f));
                OnSubsystemDamaged?.Invoke(hitSystem, _sailsHealth, _maxSailsHealth);
                OnSubsystemHitDetailed?.Invoke(hitSystem, effectiveDamage, _sailsHealth, hitWorldPoint);
            }
            // 3. Low Elevation Check: Hits near the waterline pierce Hull & Planking
            else if (localHit.y <= _hullHitHeightThreshold)
            {
                hitSystem = ShipSubsystemType.Hull;
                _hullHealth = Mathf.Max(0f, _hullHealth - effectiveDamage);
                // Waterline strikes immediately take on a burst of bilge water
                _currentBilgeWater = Mathf.Min(_currentBilgeWater + (effectiveDamage * 0.65f), _maxBilgeCapacity);
                OnSubsystemDamaged?.Invoke(hitSystem, _hullHealth, _maxHullHealth);
                OnSubsystemHitDetailed?.Invoke(hitSystem, effectiveDamage, _hullHealth, hitWorldPoint);
            }
            // 4. Mid Deck Elevation Check: Hits strike the Gun Deck batteries
            else
            {
                hitSystem = ShipSubsystemType.GunDeck;
                _gunDeckHealth = Mathf.Max(0f, _gunDeckHealth - effectiveDamage);
                _hullHealth = Mathf.Max(0f, _hullHealth - (effectiveDamage * 0.80f));
                OnSubsystemDamaged?.Invoke(hitSystem, _gunDeckHealth, _maxGunDeckHealth);
                OnSubsystemHitDetailed?.Invoke(hitSystem, effectiveDamage, _gunDeckHealth, hitWorldPoint);
            }

            // Spawn lightweight visual hit splinter effect
            SpawnVisualHitImpact(hitWorldPoint);
            Plunderlust.Audio.NauticalAudioManager.Instance?.PlaySFX(Plunderlust.Audio.SFXType.ShipHit, 0.10f);
            if (_hullHealth < _maxHullHealth * 0.6f || UnityEngine.Random.value < 0.35f)
            {
                Plunderlust.Audio.NauticalAudioManager.Instance?.PlaySFX(Plunderlust.Audio.SFXType.WoodCreak, 0.15f);
            }

            // Apply physical kinetic impact impulse to the struck ship
            if (_rb != null)
            {
                Vector3 impulseDir = shotVelocity.sqrMagnitude > 0.01f ? shotVelocity.normalized : transform.forward;
                _rb.AddForceAtPosition(impulseDir * (rawDamage * 55f), hitWorldPoint, ForceMode.Impulse);
            }

            // Cinematic screen shake if player was struck
            if (CompareTag("Player") && Plunderlust.CameraControl.NavalCameraController.Instance != null)
            {
                Plunderlust.CameraControl.NavalCameraController.Instance.AddTrauma(Mathf.Clamp01(effectiveDamage / 90f));
            }

            Debug.Log($"<color=red><b>[{gameObject.name} STRUCK!]</b></color> Hit {hitSystem} for {rawDamage} DMG! Hull HP: {_hullHealth:F0}/{_maxHullHealth:F0}, Bilge Flooding: {FloodingRatio * 100:F0}%");

            return hitSystem;
        }

        private void SpawnVisualHitImpact(Vector3 pos)
        {
            // Lightweight procedural splinter sparks without external prefab dependencies
            GameObject sparkGo = new GameObject("HitSplinterFX");
            sparkGo.transform.position = pos;

            for (int i = 0; i < 4; i++)
            {
                GameObject splinter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                splinter.name = "Splinter";
                splinter.transform.position = pos + (UnityEngine.Random.insideUnitSphere * 0.2f);
                splinter.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.08f, 0.16f);

                Renderer r = splinter.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = new Color(0.6f, 0.4f, 0.15f); // Oak wood tone
                }

                Collider col = splinter.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Rigidbody rb = splinter.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                Vector3 blastDir = (UnityEngine.Random.onUnitSphere + Vector3.up * 1.5f).normalized;
                rb.AddForce(blastDir * UnityEngine.Random.Range(3.5f, 6.5f), ForceMode.Impulse);

                Destroy(splinter, UnityEngine.Random.Range(0.8f, 1.4f));
            }

            Destroy(sparkGo, 1.5f);
        }

        /// <summary>
        /// Manually repairs a specified subsystem by an amount (e.g. from Carpenter or Canvas at sea).
        /// </summary>
        public void RepairSubsystem(ShipSubsystemType system, float repairAmount)
        {
            switch (system)
            {
                case ShipSubsystemType.Hull:
                    _hullHealth = Mathf.Min(_hullHealth + repairAmount, _maxHullHealth);
                    OnSubsystemRepaired?.Invoke(system, _hullHealth, _maxHullHealth);
                    break;
                case ShipSubsystemType.SailsRigging:
                    _sailsHealth = Mathf.Min(_sailsHealth + repairAmount, _maxSailsHealth);
                    OnSubsystemRepaired?.Invoke(system, _sailsHealth, _maxSailsHealth);
                    break;
                case ShipSubsystemType.RudderHelm:
                    _rudderHealth = Mathf.Min(_rudderHealth + repairAmount, _maxRudderHealth);
                    OnSubsystemRepaired?.Invoke(system, _rudderHealth, _maxRudderHealth);
                    break;
                case ShipSubsystemType.GunDeck:
                    _gunDeckHealth = Mathf.Min(_gunDeckHealth + repairAmount, _maxGunDeckHealth);
                    OnSubsystemRepaired?.Invoke(system, _gunDeckHealth, _maxGunDeckHealth);
                    break;
            }
        }

        /// <summary>
        /// Pumping bilge water reduces flooding ratio (operated by Bosun / bilge pumps).
        /// </summary>
        public void PumpBilgeWater(float drainAmount)
        {
            _currentBilgeWater = Mathf.Max(0f, _currentBilgeWater - drainAmount);
        }

        private void TriggerSinkingState()
        {
            _isSinking = true;
            _currentBilgeWater = _maxBilgeCapacity; // Force 100% flooding

            OnShipStartedSinking?.Invoke();

            // Disable locomotion thrust
            ShipLocomotionController locomotion = GetComponent<ShipLocomotionController>();
            if (locomotion != null)
            {
                locomotion.SetSailState(SailState.Anchored);
                locomotion.enabled = false;
            }

            // Apply listing roll torque so the foundering ship rolls over into the waves
            if (_rb != null)
            {
                _rb.linearDamping = 1.4f;
                _rb.angularDamping = 2.5f;
                _rb.AddTorque(transform.forward * (_rb.mass * 8f) + transform.right * (_rb.mass * 4f), ForceMode.Impulse);
            }

            // Begin final sinking decay (12 seconds allows ship to submerge 15m deep under the waves)
            Invoke(nameof(FinalizeShipDeath), 12.0f);
        }

        private void FinalizeShipDeath()
        {
            _isDead = true;
            OnShipDestroyed?.Invoke();
            Destroy(gameObject, 0.5f);
        }
    }
}
