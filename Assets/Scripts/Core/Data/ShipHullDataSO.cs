using UnityEngine;

namespace Plunderlust.Core.Data
{
    /// <summary>
    /// ScriptableObject defining immutable base specifications for a ship hull tier.
    /// Matches Kenney Pirate Kit models and drives physics, subsystems, manpower, and armaments.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipHullData", menuName = "Plunderlust/Ship Hull Data", order = 100)]
    public class ShipHullDataSO : ScriptableObject
    {
        [Header("Classification")]
        [SerializeField] private ShipTier _tier = ShipTier.Tier1_LeakyRowboat;
        [SerializeField] private string _shipClassName = "Coastal Cutter";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "Fast, agile vessel suited for coastal skirmishing.";
        [SerializeField] private GameObject _modelPrefab;

        [Header("Physics & Mass Architecture")]
        [Tooltip("Dry mass of the vessel in kilograms.")]
        [SerializeField] private float _baseMass = 2400f;
        [Tooltip("Additional mass accumulated when bilge water is at 100% capacity.")]
        [SerializeField] private float _maxFloodingMass = 3600f;
        [Tooltip("Linear water resistance applied to the Rigidbody.")]
        [SerializeField] private float _linearDrag = 0.85f;
        [Tooltip("Rotational water resistance applied to the Rigidbody.")]
        [SerializeField] private float _angularDrag = 2.0f;
        [Tooltip("Lower center of mass produces stable self-righting naval physics.")]
        [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0f, -0.6f, 0f);

        [Header("Buoyancy Probes Configuration")]
        [Tooltip("Nominal depth below water at which a probe generates 100% Archimedes buoyant force.")]
        [SerializeField] private float _probeSubmersionDepth = 1.2f;
        [Tooltip("Base upward buoyant force per probe when fully submerged.")]
        [SerializeField] private float _buoyantForcePerProbe = 8500f;
        [Tooltip("Multiplier applied to buoyancy when ship is 100% flooded before sink state.")]
        [Range(0f, 1f)]
        [SerializeField] private float _sinkingBuoyancyRetention = 0.25f;
        [Tooltip("Waterline trim offset in meters. Positive values lift the hull higher out of the water.")]
        [SerializeField] private float _waterlineOffset = 0.0f;
        [Tooltip("Multiplier for emergency reserve buoyancy when wave crests rise above submersion depth.")]
        [SerializeField] private float _reserveBuoyancyScale = 2.5f;
        [Tooltip("Torque aligning ship up-vector with local wave normal. High for small boats (cork effect), low for large ships.")]
        [SerializeField] private float _surfaceAlignmentTorque = 6.0f;

        [Header("Locomotion & Propulsion")]
        [SerializeField] private bool _supportsRowing = false;
        [SerializeField] private bool _supportsSails = true;
        [SerializeField] private float _fullCanvasMaxSpeed = 13.5f;
        [SerializeField] private float _battleSailsMaxSpeed = 7.0f;
        [SerializeField] private float _rowingSpeed = 4.0f;
        [SerializeField] private float _forwardAcceleration = 4.5f;
        [SerializeField] private float _reverseSpeed = 2.0f;
        [SerializeField] private float _rudderTorque = 8.0f;
        [Tooltip("Rudder torque multiplier when in Battle Sails state.")]
        [SerializeField] private float _rudderResponsivenessBattleSails = 1.4f;
        [Tooltip("Roll torque applied into turns to simulate ship heeling.")]
        [SerializeField] private float _turnBankingFactor = 0.35f;

        [Header("Subsystem Integrity & Bilge")]
        [SerializeField] private float _maxHullHealth = 600f;
        [SerializeField] private float _maxSailsHealth = 350f;
        [SerializeField] private float _maxRudderHealth = 300f;
        [SerializeField] private float _maxGunDeckHealth = 450f;
        [SerializeField] private float _maxBilgeWaterCapacity = 1200f;
        [SerializeField] private float _baseLeakRatePerSec = 25f;

        [Header("Crew & Manpower Requirements")]
        [SerializeField] private int _minRequiredCrew = 4;
        [SerializeField] private int _maxCrewCapacity = 16;

        [Header("Armament & Cargo")]
        [SerializeField] private int _portCannonsCount = 4;
        [SerializeField] private int _starboardCannonsCount = 4;
        [SerializeField] private float _cannonReloadBaseTime = 5.0f;
        [SerializeField] private int _cargoCapacity = 60;

        // Public Accessors
        public ShipTier Tier => _tier;
        public string ShipClassName => _shipClassName;
        public string Description => _description;
        public GameObject ModelPrefab => _modelPrefab;

        public float BaseMass => _baseMass;
        public float MaxFloodingMass => _maxFloodingMass;
        public float LinearDrag => _linearDrag;
        public float AngularDrag => _angularDrag;
        public Vector3 CenterOfMassOffset => _centerOfMassOffset;

        public float ProbeSubmersionDepth => _probeSubmersionDepth;
        public float BuoyantForcePerProbe => _buoyantForcePerProbe;
        public float SinkingBuoyancyRetention => _sinkingBuoyancyRetention;
        public float WaterlineOffset => _waterlineOffset;
        public float ReserveBuoyancyScale => _reserveBuoyancyScale;
        public float SurfaceAlignmentTorque => _surfaceAlignmentTorque;

        public bool SupportsRowing => _supportsRowing;
        public bool SupportsSails => _supportsSails;
        public float FullCanvasMaxSpeed => _fullCanvasMaxSpeed;
        public float BattleSailsMaxSpeed => _battleSailsMaxSpeed;
        public float RowingSpeed => _rowingSpeed;
        public float ForwardAcceleration => _forwardAcceleration;
        public float ReverseSpeed => _reverseSpeed;
        public float RudderTorque => _rudderTorque;
        public float RudderResponsivenessBattleSails => _rudderResponsivenessBattleSails;
        public float TurnBankingFactor => _turnBankingFactor;

        public float MaxHullHealth => _maxHullHealth;
        public float MaxSailsHealth => _maxSailsHealth;
        public float MaxRudderHealth => _maxRudderHealth;
        public float MaxGunDeckHealth => _maxGunDeckHealth;
        public float MaxBilgeWaterCapacity => _maxBilgeWaterCapacity;
        public float BaseLeakRatePerSec => _baseLeakRatePerSec;

        public int MinRequiredCrew => _minRequiredCrew;
        public int MaxCrewCapacity => _maxCrewCapacity;

        public int PortCannonsCount => _portCannonsCount;
        public int StarboardCannonsCount => _starboardCannonsCount;
        public float CannonReloadBaseTime => _cannonReloadBaseTime;
        public int CargoCapacity => _cargoCapacity;
    }
}
