using System;
using Plunderlust.Core.Data;
using Plunderlust.Ocean;
using UnityEngine;

namespace Plunderlust.Loot
{
    /// <summary>
    /// Dedicated flotsam floating physics for buoyant loot crates.
    /// Uses analytical dual-Gerstner ocean wave math from OceanWaveManager to float, bob,
    /// and ride on wave crests independently without heavy multi-probe ship buoyancy.
    /// Collectable upon ship collision, triggering cargo salvage and UI alerts.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LootCrate : MonoBehaviour
    {
        [Header("Cargo Manifest")]
        [SerializeField] private CargoItemType _itemType = CargoItemType.Gold;
        [SerializeField] private int _quantity = 25;
        [SerializeField] private float _lifespan = 90.0f;

        [Header("Floating Hydrodynamics")]
        [Tooltip("Nominal water line trim offset relative to wave surface.")]
        [SerializeField] private float _waterlineOffset = -0.05f;
        [Tooltip("Upward Archimedes acceleration power.")]
        [SerializeField] private float _buoyancyPower = 420.0f;
        [Tooltip("Vertical damping opposing wave bob velocity.")]
        [SerializeField] private float _waterDamping = 45.0f;
        [Tooltip("Torque aligning the crate upright with the ocean wave surface slope.")]
        [SerializeField] private float _waveTiltTorque = 28.0f;
        [Tooltip("Horizontal drag preventing crates from sliding endlessly down wave valleys.")]
        [SerializeField] private float _horizontalOceanDrag = 2.4f;

        private Rigidbody _rb;
        private UnityEngine.Camera _mainCam;
        private float _age = 0f;
        private bool _isCollected = false;

        // OnGUI Floating Marker
        private GUIStyle _markerStyle;
        private Texture2D _markerBgTex;
        private bool _stylesInit = false;

        public CargoItemType ItemType => _itemType;
        public int Quantity => _quantity;

        public static event Action<CargoItemType, int, Vector3> OnAnyLootCollected;
        public static event Action<CargoItemType, int, Vector3> OnPlayerLootCollected;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 25f;
            _rb.linearDamping = 1.5f;
            _rb.angularDamping = 2.5f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Initialize(CargoItemType type, int count, float lifespanSeconds = 90f)
        {
            _itemType = type;
            _quantity = count;
            _lifespan = lifespanSeconds;
            _age = 0f;
            _isCollected = false;
        }

        private void FixedUpdate()
        {
            if (_isCollected) return;

            Vector3 pos = transform.position;
            OceanWaveManager.GetWaveHeightAndNormal(pos.x, pos.z, Time.time, out float waveY, out Vector3 waveNormal);

            float targetWaterline = waveY + _waterlineOffset;
            float depth = targetWaterline - pos.y;

            if (depth > 0f)
            {
                // 1. Archimedes Buoyant Lift
                float submergedRatio = Mathf.Clamp01(depth / 0.5f);
                float upwardLift = (submergedRatio * _buoyancyPower) - (_rb.linearVelocity.y * _waterDamping);
                _rb.AddForce(Vector3.up * upwardLift, ForceMode.Acceleration);

                // 2. Wave Surface Normal Alignment Torque
                Vector3 alignTorque = Vector3.Cross(transform.up, waveNormal) * _waveTiltTorque;
                _rb.AddTorque(alignTorque - (_rb.angularVelocity * 4.5f), ForceMode.Acceleration);

                // 3. Oppose Horizontal Ocean Drift
                Vector3 horizVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                _rb.AddForce(-horizVel * _horizontalOceanDrag, ForceMode.Acceleration);
            }
            else
            {
                // Airborne under normal gravity
                _rb.AddForce(Physics.gravity * 0.4f, ForceMode.Acceleration);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            // Gentle decorative yaw drift in water
            transform.Rotate(Vector3.up, 15f * dt, Space.World);

            if (_age >= _lifespan && !_isCollected)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<Plunderlust.Ship.ShipLocomotionController>() != null)
            {
                CollectLoot(other.gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isCollected) return;

            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<Plunderlust.Ship.ShipLocomotionController>() != null)
            {
                CollectLoot(collision.gameObject);
            }
        }

        private void CollectLoot(GameObject collector)
        {
            if (collector == null) return;

            // ABSOLUTE BLOCK: Sinking, destroyed, or foundering ships CANNOT salvage or loot flotsam!
            Plunderlust.Subsystems.ShipSubsystemController subsystems = collector.GetComponentInParent<Plunderlust.Subsystems.ShipSubsystemController>();
            if (subsystems != null && (subsystems.IsSinking || subsystems.IsDead || subsystems.HullIntegrityRatio <= 0.001f))
            {
                return;
            }

            bool isPlayer = collector.CompareTag("Player") || collector.transform.root.CompareTag("Player");
            _isCollected = true;

            // Check for Quartermaster salvage bonus
            int finalQuantity = _quantity;
            Plunderlust.Crew.CrewManager crew = collector.GetComponentInParent<Plunderlust.Crew.CrewManager>();
            if (crew != null && crew.Quartermaster != null)
            {
                finalQuantity = Mathf.RoundToInt(_quantity * (1.0f + crew.Quartermaster.PrimaryPassiveValue));
            }

            // Stow salvaged items into collector ship's cargo hold
            Plunderlust.Cargo.ShipCargoHold cargoHold = collector.GetComponentInParent<Plunderlust.Cargo.ShipCargoHold>();
            int addedAmount = finalQuantity;
            if (cargoHold != null)
            {
                cargoHold.AddCargo(_itemType, finalQuantity, out addedAmount);
            }

            if (addedAmount <= 0)
            {
                // No space in hold! Do not destroy crate, keep floating in water
                _isCollected = false;
                if (isPlayer && UI.PirateCombatHUD.Instance != null)
                {
                    UI.PirateCombatHUD.Instance.ShowBanner($"HOLD FULL! No room for {_quantity}x {_itemType}", new Color(1f, 0.4f, 0.3f), 2.0f);
                }
                return;
            }

            if (addedAmount < finalQuantity)
            {
                // Partial pickup! Deduct collected quantity and keep crate floating
                _quantity = finalQuantity - addedAmount;
                _isCollected = false;
                OnAnyLootCollected?.Invoke(_itemType, addedAmount, transform.position);
                if (isPlayer)
                {
                    OnPlayerLootCollected?.Invoke(_itemType, addedAmount, transform.position);
                    if (UI.PirateCombatHUD.Instance != null)
                    {
                        UI.PirateCombatHUD.Instance.ShowBanner($"SALVAGED +{addedAmount} {_itemType} (Hold Full! {_quantity} left in sea)", new Color(1f, 0.85f, 0.2f), 2.5f);
                    }
                }
                SpawnPickupSplash(transform.position);
                return;
            }

            // Full pickup
            OnAnyLootCollected?.Invoke(_itemType, addedAmount, transform.position);
            if (isPlayer)
            {
                OnPlayerLootCollected?.Invoke(_itemType, addedAmount, transform.position);
            }
            SpawnPickupSplash(transform.position);
            Destroy(gameObject);
        }

        private void SpawnPickupSplash(Vector3 pos)
        {
            GameObject splashGo = new GameObject("LootPickupFX");
            splashGo.transform.position = pos;

            for (int i = 0; i < 4; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.transform.position = pos + (UnityEngine.Random.insideUnitSphere * 0.2f);
                spark.transform.localScale = Vector3.one * 0.12f;

                Renderer r = spark.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(1f, 0.85f, 0.2f); // Gold glimmer

                Collider c = spark.GetComponent<Collider>();
                if (c != null) Destroy(c);

                Rigidbody rb = spark.AddComponent<Rigidbody>();
                rb.AddForce((Vector3.up * 3.5f + UnityEngine.Random.insideUnitSphere * 1.5f), ForceMode.Impulse);
                Destroy(spark, 0.8f);
            }

            Destroy(splashGo, 1.0f);
        }

        private void InitStyles()
        {
            if (_stylesInit) return;

            _markerBgTex = new Texture2D(1, 1);
            _markerBgTex.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.82f));
            _markerBgTex.Apply();

            _markerStyle = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _markerStyle.normal.textColor = new Color(1f, 0.88f, 0.3f);

            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (_isCollected) return;

            // Never draw floating loot indicators through modals or Game Over
            if (UI.GameOverUI.IsGameOver) return;
            if (UI.PauseMenuUI.Instance != null && UI.PauseMenuUI.IsPaused) return;
            if (UI.SeaChartMapUI.Instance != null && UI.SeaChartMapUI.Instance.IsOpen) return;

            if (_mainCam == null) _mainCam = UnityEngine.Camera.main;
            if (_mainCam == null) return;

            float dist = Vector3.Distance(_mainCam.transform.position, transform.position);
            if (dist > 50.0f) return;

            InitStyles();

            Vector3 worldPos = transform.position + (Vector3.up * 1.1f);
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0.2f) return;

            float screenY = Screen.height - screenPos.y;
            float width = 90f;
            float height = 16f;
            float startX = screenPos.x - (width * 0.5f);

            GUI.DrawTexture(new Rect(startX - 2f, screenY - 1f, width + 4f, height + 2f), _markerBgTex);
            GUI.Label(new Rect(startX, screenY, width, height), $"✦ {_itemType} +{_quantity}", _markerStyle);
        }
    }
}
