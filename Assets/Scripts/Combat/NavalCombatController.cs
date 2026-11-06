using System.Collections;
using Plunderlust.Core.Data;
using Plunderlust.Core.Interfaces;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Combat
{
    /// <summary>
    /// Naval broadside combat controller managing Port and Starboard batteries,
    /// dynamic disabled barrel counts from Gun Deck damage, ballistic pitch elevation,
    /// staggered ripple fire volleys, and transverse recoil physics.
    /// Operates with zero heap allocations in Update and FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NavalCombatController : MonoBehaviour
    {
        [Header("Hull Data Profile")]
        [SerializeField] private ShipHullDataSO _hullData;

        [Header("Battery Cannon Counts")]
        [SerializeField] private int _portCannonsCount = 2;
        [SerializeField] private int _starboardCannonsCount = 2;
        [SerializeField] private bool _hasForwardSwivelGun = true;

        public int PortCannonsCount => _portCannonsCount;
        public int StarboardCannonsCount => _starboardCannonsCount;
        public bool HasForwardSwivelGun => _hasForwardSwivelGun;

        [Header("Ballistics & Ordnance")]
        [SerializeField] private float _cannonballMuzzleSpeed = 38f;
        [SerializeField] private float _cannonDamage = 75f;
        [SerializeField] private float _cannonballSpreadAngle = 3.5f;
        [Tooltip("Delay in seconds between sequential cannon discharges in a broadside ripple.")]
        [SerializeField] private float _rippleFireDelay = 0.08f;

        [Header("Aiming Elevation (Pitch Degrees)")]
        [Tooltip("Low pitch: Waterline hull shots. Mid: Gun deck. High: Rigging demasting.")]
        [Range(-5f, 25f)]
        [SerializeField] private float _aimPitchAngle = 6.0f;

        [Header("Recoil Physics")]
        [Tooltip("Transverse roll impulse applied to ship upon broadside blast.")]
        [SerializeField] private float _broadsideRecoilImpulse = 450f;

        [Header("Reload Cooldowns")]
        [SerializeField] private float _baseReloadTime = 5.0f;
        [SerializeField] private float _portReloadTimer = 0f;
        [SerializeField] private float _starboardReloadTimer = 0f;
        [SerializeField] private float _forwardReloadTimer = 0f;

        [Header("Hardpoint Offsets (Auto-Calculated if null)")]
        [SerializeField] private Transform[] _portHardpoints;
        [SerializeField] private Transform[] _starboardHardpoints;
        [SerializeField] private Transform _forwardHardpoint;

        private Rigidbody _rb;
        private ShipSubsystemController _subsystems;
        private ICrewEfficiencyProvider _crewProvider;

        private ICrewEfficiencyProvider CrewEfficiencyProvider
        {
            get
            {
                if (_crewProvider == null)
                {
                    _crewProvider = GetComponent<ICrewEfficiencyProvider>() ?? GetComponentInParent<ICrewEfficiencyProvider>();
                }
                return _crewProvider;
            }
        }

        // Pre-allocated aiming trajectory buffer
        private readonly Vector3[] _aimTrajectoryBuffer = new Vector3[24];

        public event System.Action OnWeaponFired;

        public float PortReloadNormalized => Mathf.Clamp01(_portReloadTimer / Mathf.Max(0.1f, GetEffectiveReloadTime()));
        public float StarboardReloadNormalized => Mathf.Clamp01(_starboardReloadTimer / Mathf.Max(0.1f, GetEffectiveReloadTime()));
        public float ForwardReloadNormalized => Mathf.Clamp01(_forwardReloadTimer / Mathf.Max(0.1f, GetEffectiveReloadTime() * 0.7f));

        public bool CanFirePort => _portReloadTimer <= 0f && (_subsystems == null || !_subsystems.IsSinking) && GetActivePortCannons() > 0;
        public bool CanFireStarboard => _starboardReloadTimer <= 0f && (_subsystems == null || !_subsystems.IsSinking) && GetActiveStarboardCannons() > 0;
        public bool CanFireForward => _hasForwardSwivelGun && _forwardReloadTimer <= 0f && (_subsystems == null || !_subsystems.IsSinking);

        public float AimPitchAngle
        {
            get => _aimPitchAngle;
            set => _aimPitchAngle = Mathf.Clamp(value, -5f, 25f);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _subsystems = GetComponent<ShipSubsystemController>() ?? GetComponentInParent<ShipSubsystemController>();
            _crewProvider = GetComponent<ICrewEfficiencyProvider>() ?? GetComponentInParent<ICrewEfficiencyProvider>();

            ApplyHullData();
            InitializeHardpoints();
        }

        public void SetHullData(ShipHullDataSO hullData)
        {
            _hullData = hullData;
            ApplyHullData();
            RebuildHardpoints();
        }

        private void ApplyHullData()
        {
            if (_hullData == null) return;

            _portCannonsCount = _hullData.PortCannonsCount;
            _starboardCannonsCount = _hullData.StarboardCannonsCount;
            _baseReloadTime = _hullData.CannonReloadBaseTime;
            // Small rowboat starts with a forward swivel gun
            _hasForwardSwivelGun = _portCannonsCount == 0 && _starboardCannonsCount == 0;
        }

        public void RebuildHardpoints()
        {
            // Destroy existing procedural containers if present
            Transform oldPort = transform.Find("Port_Hardpoints");
            if (oldPort != null) Destroy(oldPort.gameObject);

            Transform oldStbd = transform.Find("Starboard_Hardpoints");
            if (oldStbd != null) Destroy(oldStbd.gameObject);

            _portHardpoints = GenerateProceduralHardpoints(true, Mathf.Max(1, _portCannonsCount));
            _starboardHardpoints = GenerateProceduralHardpoints(false, Mathf.Max(1, _starboardCannonsCount));
        }

        private void InitializeHardpoints()
        {
            if (_portHardpoints == null || _portHardpoints.Length == 0 || _portHardpoints[0] == null)
            {
                _portHardpoints = GenerateProceduralHardpoints(true, Mathf.Max(1, _portCannonsCount));
            }

            if (_starboardHardpoints == null || _starboardHardpoints.Length == 0 || _starboardHardpoints[0] == null)
            {
                _starboardHardpoints = GenerateProceduralHardpoints(false, Mathf.Max(1, _starboardCannonsCount));
            }
        }

        private Transform[] GenerateProceduralHardpoints(bool isPort, int count)
        {
            Transform container = new GameObject(isPort ? "Port_Hardpoints" : "Starboard_Hardpoints").transform;
            container.SetParent(transform, false);

            BoxCollider col = GetComponent<BoxCollider>();
            float halfWidth = col != null ? (col.size.x * 0.5f + 0.65f) : 1.6f;
            float lengthSpan = col != null ? (col.size.z * 0.55f) : 2.5f;

            Transform[] points = new Transform[count];
            float sideX = isPort ? -halfWidth : halfWidth;
            float spacingZ = count > 1 ? lengthSpan / (count - 1) : 0f;
            float startZ = (count - 1) * spacingZ * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject hp = new GameObject($"Hardpoint_{i}");
                hp.transform.SetParent(container, false);
                float z = count > 1 ? startZ - (i * spacingZ) : 0f;
                hp.transform.localPosition = new Vector3(sideX, 0.75f, z);
                hp.transform.localRotation = Quaternion.Euler(0f, isPort ? -90f : 90f, 0f);
                points[i] = hp.transform;
            }

            return points;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_portReloadTimer > 0f) _portReloadTimer = Mathf.Max(0f, _portReloadTimer - dt);
            if (_starboardReloadTimer > 0f) _starboardReloadTimer = Mathf.Max(0f, _starboardReloadTimer - dt);
            if (_forwardReloadTimer > 0f) _forwardReloadTimer = Mathf.Max(0f, _forwardReloadTimer - dt);
        }

        public float GetEffectiveReloadTime()
        {
            float gunDeckEfficiency = _subsystems != null ? _subsystems.GunDeckEfficiency : 1.0f;
            float crewEfficiency = CrewEfficiencyProvider != null ? CrewEfficiencyProvider.GunnerEfficiency : 1.0f;

            // Damaged gun deck slows reloads up to +80%; skilled crew speeds it up
            float subsystemPenalty = 1.0f + ((1.0f - gunDeckEfficiency) * 0.8f);
            return (_baseReloadTime * subsystemPenalty) / Mathf.Max(0.35f, crewEfficiency);
        }

        public int GetActivePortCannons()
        {
            if (_portCannonsCount <= 0) return 0;
            float efficiency = _subsystems != null ? _subsystems.GunDeckEfficiency : 1.0f;
            return Mathf.Max(1, Mathf.CeilToInt(_portCannonsCount * efficiency));
        }

        public int GetActiveStarboardCannons()
        {
            if (_starboardCannonsCount <= 0) return 0;
            float efficiency = _subsystems != null ? _subsystems.GunDeckEfficiency : 1.0f;
            return Mathf.Max(1, Mathf.CeilToInt(_starboardCannonsCount * efficiency));
        }

        /// <summary>
        /// Fires Port broadside volley with sequential ripple fire (or aims swivel gun to port on small craft).
        /// </summary>
        public bool FirePortBroadside()
        {
            if (_portCannonsCount <= 0 && _hasForwardSwivelGun)
            {
                return FireSwivelDirection(-90f);
            }

            if (!CanFirePort) return false;

            int activeCount = GetActivePortCannons();
            StartCoroutine(ExecuteRippleVolleyRoutine(true, activeCount));
            _portReloadTimer = GetEffectiveReloadTime();

            // Apply recoil roll impulse (blasts push ship to starboard)
            ApplyBroadsideRecoil(true, activeCount);

            if (CompareTag("Player"))
            {
                if (Plunderlust.CameraControl.NavalCameraController.Instance != null)
                {
                    Plunderlust.CameraControl.NavalCameraController.Instance.AddTrauma(0.28f);
                }
                Plunderlust.UI.TutorialManagerUI.Instance?.CompleteStep3();
            }
            OnWeaponFired?.Invoke();
            return true;
        }

        /// <summary>
        /// Fires Starboard broadside volley with sequential ripple fire (or aims swivel gun to starboard on small craft).
        /// </summary>
        public bool FireStarboardBroadside()
        {
            if (_starboardCannonsCount <= 0 && _hasForwardSwivelGun)
            {
                return FireSwivelDirection(90f);
            }

            if (!CanFireStarboard) return false;

            int activeCount = GetActiveStarboardCannons();
            StartCoroutine(ExecuteRippleVolleyRoutine(false, activeCount));
            _starboardReloadTimer = GetEffectiveReloadTime();

            // Apply recoil roll impulse (blasts push ship to port)
            ApplyBroadsideRecoil(false, activeCount);

            if (CompareTag("Player"))
            {
                if (Plunderlust.CameraControl.NavalCameraController.Instance != null)
                {
                    Plunderlust.CameraControl.NavalCameraController.Instance.AddTrauma(0.28f);
                }
                Plunderlust.UI.TutorialManagerUI.Instance?.CompleteStep3();
            }
            OnWeaponFired?.Invoke();
            return true;
        }

        /// <summary>
        /// Fires bow swivel gun forward.
        /// </summary>
        public bool FireForwardSwivel()
        {
            return FireSwivelDirection(0f);
        }

        private bool FireSwivelDirection(float angleOffset)
        {
            if (!CanFireForward) return false;

            BoxCollider col = GetComponent<BoxCollider>();
            float forwardOffset = col != null ? (col.size.z * 0.5f + 1.2f) : 2.5f;
            Vector3 spawnPos = _forwardHardpoint != null ? _forwardHardpoint.position : transform.position + (transform.forward * forwardOffset) + (Vector3.up * 0.75f);

            float heading = transform.eulerAngles.y + angleOffset;
            Vector3 aimDir = Quaternion.Euler(-_aimPitchAngle, heading, 0f) * Vector3.forward;
            Vector3 shipVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector3 velocity = (aimDir * _cannonballMuzzleSpeed) + shipVelocity;

            if (ProjectilePool.Instance != null)
            {
                Plunderlust.Crew.CrewManager crew = CrewEfficiencyProvider as Plunderlust.Crew.CrewManager ?? GetComponent<Plunderlust.Crew.CrewManager>();
                float gunnerDmgMult = (crew != null && crew.MasterGunner != null) ? 1.25f : 1.0f;
                ProjectilePool.Instance.SpawnCannonball(spawnPos, velocity, _cannonDamage * gunnerDmgMult, gameObject);
            }
            Plunderlust.Audio.NauticalAudioManager.Instance?.PlaySFX(Plunderlust.Audio.SFXType.CannonFire, 0.10f);

            _forwardReloadTimer = GetEffectiveReloadTime() * 0.6f;
            if (CompareTag("Player"))
            {
                if (Plunderlust.CameraControl.NavalCameraController.Instance != null)
                {
                    Plunderlust.CameraControl.NavalCameraController.Instance.AddTrauma(0.18f);
                }
                Plunderlust.UI.TutorialManagerUI.Instance?.CompleteStep3();
            }
            OnWeaponFired?.Invoke();
            return true;
        }

        private IEnumerator ExecuteRippleVolleyRoutine(bool isPort, int activeCannons)
        {
            Transform[] hardpoints = isPort ? _portHardpoints : _starboardHardpoints;
            if (hardpoints == null || hardpoints.Length == 0) yield break;

            int count = Mathf.Min(activeCannons, hardpoints.Length);
            float baseHeading = transform.eulerAngles.y + (isPort ? -90f : 90f);

            Plunderlust.Crew.CrewManager crew = CrewEfficiencyProvider as Plunderlust.Crew.CrewManager ?? GetComponent<Plunderlust.Crew.CrewManager>();
            float spreadMult = (crew != null && crew.MasterGunner != null) ? 0.6f : 1.0f;
            float gunnerDmgMult = (crew != null && crew.MasterGunner != null) ? 1.25f : 1.0f;

            for (int i = 0; i < count; i++)
            {
                Transform hp = hardpoints[i];
                Vector3 spawnPos = hp != null ? hp.position : transform.position;

                // Add slight spread per cannon barrel (tightened by Master Gunner)
                float spreadY = Random.Range(-_cannonballSpreadAngle * spreadMult, _cannonballSpreadAngle * spreadMult);
                float spreadPitch = Random.Range(-1.0f * spreadMult, 1.0f * spreadMult);

                Quaternion fireRot = Quaternion.Euler(-(_aimPitchAngle + spreadPitch), baseHeading + spreadY, 0f);
                Vector3 shipVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
                Vector3 muzzleVelocity = ((fireRot * Vector3.forward) * _cannonballMuzzleSpeed) + shipVelocity;

                if (ProjectilePool.Instance != null)
                {
                    ProjectilePool.Instance.SpawnCannonball(spawnPos, muzzleVelocity, _cannonDamage * gunnerDmgMult, gameObject);
                }
                Plunderlust.Audio.NauticalAudioManager.Instance?.PlaySFX(Plunderlust.Audio.SFXType.CannonFire, 0.12f);

                if (i < count - 1 && _rippleFireDelay > 0.001f)
                {
                    yield return new WaitForSeconds(_rippleFireDelay);
                }
            }
        }

        private void ApplyBroadsideRecoil(bool isPort, int activeCannons)
        {
            if (_rb == null) return;

            // Port broadside pushes ship to starboard (gentle realistic roll torque: 35 N*s per gun)
            float rollDirection = isPort ? 1.0f : -1.0f;
            float totalRecoil = Mathf.Clamp(_broadsideRecoilImpulse * 0.08f * activeCannons, 20f, 120f);

            _rb.AddTorque(transform.forward * (rollDirection * totalRecoil), ForceMode.Impulse);
        }

        /// <summary>
        /// Evaluates aiming trajectory arc points for visual broadside line rendering.
        /// Zero allocations.
        /// </summary>
        public int ComputeAimTrajectory(bool isPort, Vector3[] outPoints)
        {
            if (outPoints == null || outPoints.Length == 0) return 0;

            float baseHeading = transform.eulerAngles.y + (isPort ? -90f : 90f);
            Quaternion fireRot = Quaternion.Euler(-_aimPitchAngle, baseHeading, 0f);
            Vector3 vel = (fireRot * Vector3.forward) * _cannonballMuzzleSpeed;

            Vector3 currentPos = transform.position + (transform.up * 0.5f) + ((isPort ? -transform.right : transform.right) * 1.2f);
            float timeStep = 0.12f;
            int pointsWritten = 0;

            for (int i = 0; i < outPoints.Length; i++)
            {
                outPoints[i] = currentPos;
                pointsWritten++;

                vel.y += -9.81f * timeStep;
                currentPos += vel * timeStep;

                if (currentPos.y < -1.0f) break;
            }

            return pointsWritten;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            int count = ComputeAimTrajectory(false, _aimTrajectoryBuffer);
            for (int i = 0; i < count - 1; i++)
            {
                Gizmos.DrawLine(_aimTrajectoryBuffer[i], _aimTrajectoryBuffer[i + 1]);
            }
        }
#endif
    }
}
