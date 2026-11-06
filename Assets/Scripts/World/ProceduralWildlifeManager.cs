using System.Collections.Generic;
using Plunderlust.Audio;
using Plunderlust.Loot;
using Plunderlust.Ocean;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Zero-Asset Procedural Primitive Wildlife Manager.
    /// Procedurally constructs and animates lightweight ambient fauna using stripped Unity primitives:
    /// - Circling Seagulls: Flapping articulated wings, thermal circling over atolls/ports and following ships.
    /// - Ocean Sharks: Dark charcoal dorsal fins cutting through wave surfaces around sunken wrecks and crates.
    /// - Breaching Whales: Giant arched cetaceans breaching majestically out of deep ocean channels.
    /// Zero heap allocations in runtime update loops.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class ProceduralWildlifeManager : MonoBehaviour
    {
        public static ProceduralWildlifeManager Instance { get; private set; }

        [Header("Population Limits")]
        [SerializeField] private int _maxSeagulls = 6;
        [SerializeField] private int _maxSharks = 2;
        [SerializeField] private int _maxWhales = 1; // Breaching authentic 3D Humpback Whales (Whale.fbx)

        [Header("Shared Wildlife Materials")]
        private Material _gullBodyMaterial;
        private Material _gullBeakMaterial;
        private Material _gullWingMaterial;
        private Material _sharkFinMaterial;
        private Material _whaleMaterial;
        private Material _whaleBellyMaterial;
        private Material _whaleSpoutMaterial;
        private Material _whaleEyeMaterial;

        private Transform _playerTransform;
        private readonly List<ProceduralSeagull> _seagulls = new List<ProceduralSeagull>();
        private readonly List<ProceduralSharkFin> _sharks = new List<ProceduralSharkFin>();
        private readonly List<ProceduralWhale> _whales = new List<ProceduralWhale>();

        private float _whaleTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureMaterial();
        }

        private void Start()
        {
            FindPlayer();
            SpawnInitialWildlife();
        }

        private void FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        private void EnsureMaterial()
        {
            if (_gullBodyMaterial == null)
            {
                _gullBodyMaterial = CreateSolidMaterial("M_SeagullBody", new Color(0.96f, 0.97f, 0.98f)); // Pure white/cream
                _gullBeakMaterial = CreateSolidMaterial("M_SeagullBeak", new Color(1.0f, 0.76f, 0.08f));  // Warm beak yellow
                _gullWingMaterial = CreateSolidMaterial("M_SeagullWing", new Color(0.88f, 0.90f, 0.93f)); // Feather soft grey
                _sharkFinMaterial = CreateSolidMaterial("M_SharkFin", new Color(0.12f, 0.14f, 0.16f));    // Charcoal slate
                _whaleMaterial = CreateSolidMaterial("M_WhaleNavy", new Color(0.10f, 0.14f, 0.22f));      // Oceanic slate navy
                _whaleBellyMaterial = CreateSolidMaterial("M_WhaleBelly", new Color(0.85f, 0.89f, 0.92f)); // Soft seafoam ivory underbelly
                _whaleSpoutMaterial = CreateSolidMaterial("M_WhaleSpout", new Color(0.95f, 0.98f, 1.0f));  // Frothing white spray
                _whaleEyeMaterial = CreateSolidMaterial("M_WhaleEye", new Color(0.04f, 0.04f, 0.05f));     // Obsidian eye
            }
        }

        private static Material CreateSolidMaterial(string matName, Color color)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Unlit/Color");

            Material mat = new Material(s)
            {
                name = matName,
                color = color
            };

            // Explicitly ensure no texture atlas is bound so primitives render with solid color
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);

            return mat;
        }

        private void SpawnInitialWildlife()
        {
            // 1. Seagulls (Flock circling Port Royal + Ship Followers)
            Vector3 havenPos = new Vector3(45f, 18f, 25f);
            int atollGulls = Mathf.Max(4, _maxSeagulls - 4);
            for (int i = 0; i < atollGulls; i++)
            {
                CreateSeagull(havenPos, orbitRadius: Random.Range(18f, 38f), altitude: Random.Range(14f, 24f), followPlayer: false);
            }

            // Ship follower gulls
            int followerGulls = Mathf.Min(4, _maxSeagulls - atollGulls);
            for (int i = 0; i < followerGulls; i++)
            {
                CreateSeagull(Vector3.zero, orbitRadius: 12f, altitude: 9f, followPlayer: true);
            }

            // 2. Sharks around known wreckage
            var wrecks = FindObjectsByType<LootableWreck>(FindObjectsInactive.Exclude);
            for (int i = 0; i < wrecks.Length && _sharks.Count < _maxSharks; i++)
            {
                CreateShark(wrecks[i].transform.position, patrolRadius: Random.Range(9f, 15f));
            }
            if (_sharks.Count == 0)
            {
                // Ambient shark patrol in central channel
                CreateShark(new Vector3(10f, 0f, 65f), patrolRadius: 14f);
                CreateShark(new Vector3(-55f, 0f, 75f), patrolRadius: 16f);
            }

            // 3. Breaching Whales in deep waters
            for (int i = 0; i < _maxWhales; i++)
            {
                // Deep oceanic channels far away from port islands
                Vector3 whalePos = (i == 0) ? new Vector3(-160f, -5f, 210f) : new Vector3(175f, -5f, -145f);
                CreateWhale(whalePos);
            }
        }

        private void CreateSeagull(Vector3 center, float orbitRadius, float altitude, bool followPlayer)
        {
            GameObject gullRoot = new GameObject("ProceduralSeagull");
            gullRoot.transform.SetParent(transform, false);

            // Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(gullRoot.transform, false);
            body.transform.localScale = new Vector3(0.18f, 0.08f, 0.42f);
            StripCollider(body);
            ApplyMaterial(body, _gullBodyMaterial);

            // Beak (Yellow)
            GameObject beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beak.name = "Beak";
            beak.transform.SetParent(body.transform, false);
            beak.transform.localPosition = new Vector3(0f, -0.1f, 0.6f);
            beak.transform.localScale = new Vector3(0.35f, 0.35f, 0.4f);
            StripCollider(beak);
            ApplyMaterial(beak, _gullBeakMaterial);

            // Left Wing
            GameObject leftWingPivot = new GameObject("LeftWingPivot");
            leftWingPivot.transform.SetParent(gullRoot.transform, false);
            leftWingPivot.transform.localPosition = new Vector3(-0.09f, 0.02f, 0.05f);

            GameObject leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWing.transform.SetParent(leftWingPivot.transform, false);
            leftWing.transform.localPosition = new Vector3(-0.25f, 0f, 0f);
            leftWing.transform.localScale = new Vector3(0.5f, 0.025f, 0.18f);
            StripCollider(leftWing);
            ApplyMaterial(leftWing, _gullWingMaterial);

            // Right Wing
            GameObject rightWingPivot = new GameObject("RightWingPivot");
            rightWingPivot.transform.SetParent(gullRoot.transform, false);
            rightWingPivot.transform.localPosition = new Vector3(0.09f, 0.02f, 0.05f);

            GameObject rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWing.transform.SetParent(rightWingPivot.transform, false);
            rightWing.transform.localPosition = new Vector3(0.25f, 0f, 0f);
            rightWing.transform.localScale = new Vector3(0.5f, 0.025f, 0.18f);
            StripCollider(rightWing);
            ApplyMaterial(rightWing, _gullWingMaterial);

            ProceduralSeagull gull = gullRoot.AddComponent<ProceduralSeagull>();
            gull.Initialize(leftWingPivot.transform, rightWingPivot.transform, center, orbitRadius, altitude, followPlayer);
            _seagulls.Add(gull);
        }

        private static Mesh _cachedSharkFinMesh;

        private static Mesh GetOrCreateSharkFinMesh()
        {
            if (_cachedSharkFinMesh != null) return _cachedSharkFinMesh;

            Mesh mesh = new Mesh { name = "SharkDorsalFinMesh" };

            // 7 perimeter points defining organic swept-back shark fin airfoil + 1 center spine point
            Vector3[] profile = new Vector3[]
            {
                new Vector3(0f, -0.06f,  0.32f), // 0: Base front
                new Vector3(0f,  0.14f,  0.15f), // 1: Mid leading edge
                new Vector3(0f,  0.42f, -0.05f), // 2: Curved swept tip
                new Vector3(0f,  0.22f, -0.12f), // 3: Upper trailing edge
                new Vector3(0f,  0.08f, -0.15f), // 4: Trailing notch
                new Vector3(0f, -0.01f, -0.18f), // 5: Lower trailing
                new Vector3(0f, -0.06f, -0.22f), // 6: Base rear
                new Vector3(0f,  0.08f,  0.02f)  // 7: Central spine bulb
            };

            float[] halfWidths = new float[]
            {
                0.024f, // 0: Base front
                0.010f, // 1: Mid leading
                0.004f, // 2: Tip
                0.006f, // 3: Upper trailing
                0.006f, // 4: Notch
                0.010f, // 5: Lower trailing
                0.020f, // 6: Base rear
                0.028f  // 7: Central bulb
            };

            int n = 8;
            Vector3[] vertices = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                vertices[i]     = profile[i] + new Vector3(-halfWidths[i], 0f, 0f); // Left (-X)
                vertices[i + n] = profile[i] + new Vector3( halfWidths[i], 0f, 0f); // Right (+X)
            }

            List<int> tris = new List<int>(78);

            // Left flank fan around vertex 7
            for (int i = 0; i < 6; i++)
            {
                tris.Add(7);
                tris.Add(i);
                tris.Add(i + 1);
            }

            // Right flank fan around vertex 7 + n
            for (int i = 0; i < 6; i++)
            {
                tris.Add(7 + n);
                tris.Add(i + 1 + n);
                tris.Add(i + n);
            }

            // Boundary strip around perimeter (0 to 6)
            for (int i = 0; i < 6; i++)
            {
                tris.Add(i);
                tris.Add(i + 1 + n);
                tris.Add(i + 1);

                tris.Add(i);
                tris.Add(i + n);
                tris.Add(i + 1 + n);
            }

            // Bottom base quad (6 -> 0)
            tris.Add(6);
            tris.Add(0 + n);
            tris.Add(0);

            tris.Add(6);
            tris.Add(6 + n);
            tris.Add(0 + n);

            mesh.vertices = vertices;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _cachedSharkFinMesh = mesh;
            return _cachedSharkFinMesh;
        }

        private void CreateShark(Vector3 center, float patrolRadius)
        {
            GameObject sharkRoot = new GameObject("ProceduralShark");
            sharkRoot.transform.SetParent(transform, false);

            // Instantiate authentic 3D Shark model (Shark.fbx)
            // Local offset submerges body so only the dorsal fin slices through the surface
            GameObject sharkModel = SeaAnimalModelRegistry.SpawnModel(
                SeaAnimalType.Shark,
                sharkRoot.transform,
                new Vector3(0f, -0.32f, 0f),
                Quaternion.identity,
                Vector3.one * 0.28f
            );

            // Pass null for tailPivot so AmbientSeaAnimalSwimmer smoothly controls body swimming
            ProceduralSharkFin shark = sharkRoot.AddComponent<ProceduralSharkFin>();
            shark.Initialize(center, patrolRadius, null);
            _sharks.Add(shark);
        }

        private void CreateWhale(Vector3 openSeaCenter)
        {
            GameObject whaleRoot = new GameObject("ProceduralWhale");
            whaleRoot.transform.SetParent(transform, false);

            // Container for dynamic body pitch, bank, and dive angles
            GameObject pitchModel = new GameObject("WhalePitchModel");
            pitchModel.transform.SetParent(whaleRoot.transform, false);

            // Instantiate authentic 3D Whale model (Whale.fbx)
            GameObject whaleModel = SeaAnimalModelRegistry.SpawnModel(
                SeaAnimalType.Whale,
                pitchModel.transform,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 0.85f
            );

            // 6. Blowhole Spout System (Vertical rising mist plumes)
            GameObject spoutRoot = new GameObject("SpoutPlumeRoot");
            spoutRoot.transform.SetParent(pitchModel.transform, false);
            spoutRoot.transform.localPosition = new Vector3(0f, 0.65f, 1.2f);

            Transform[] spoutColumns = new Transform[4];
            for (int s = 0; s < 4; s++)
            {
                GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"SpoutColumn_{s}";
                col.transform.SetParent(spoutRoot.transform, false);
                float xOff = (s == 0) ? -0.06f : (s == 1) ? 0.06f : (s == 2) ? 0f : 0f;
                float zOff = (s == 2) ? -0.08f : (s == 3) ? 0.08f : 0f;
                col.transform.localPosition = new Vector3(xOff, 1.0f, zOff);
                col.transform.localScale = new Vector3(0.14f, 1.0f, 0.14f);
                StripCollider(col);
                ApplyMaterial(col, _whaleSpoutMaterial);
                spoutColumns[s] = col.transform;
            }
            spoutRoot.SetActive(false);

            // Initialize ProceduralWhale component
            ProceduralWhale whale = whaleRoot.AddComponent<ProceduralWhale>();
            whale.Initialize(openSeaCenter, pitchModel.transform, null, null, null, null, null, spoutRoot.transform, spoutColumns);
            _whales.Add(whale);
        }

        private void Update()
        {
            if (_playerTransform == null) FindPlayer();

            // Periodic whale breach triggers
            _whaleTimer += Time.deltaTime;
            if (_whaleTimer >= 45f && _whales.Count > 0)
            {
                _whaleTimer = 0f;
                int idx = Random.Range(0, _whales.Count);
                if (_whales[idx] != null && !_whales[idx].IsBreaching)
                {
                    _whales[idx].TriggerBreach();
                }
            }
        }

        private static void StripCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && material != null)
            {
                r.sharedMaterial = material;
            }
        }
    }

    /// <summary>
    /// Articulated wing flapping, thermal gliding, and ship-following behavior for seagulls.
    /// </summary>
    public class ProceduralSeagull : MonoBehaviour
    {
        private Transform _leftWingPivot;
        private Transform _rightWingPivot;
        private Vector3 _orbitCenter;
        private float _orbitRadius;
        private float _targetAltitude;
        private bool _followPlayer;

        private float _angle;
        private float _flapPhase;
        private float _glideTimer = 0f;
        private bool _isGliding = false;
        private Transform _playerTransform;

        public void Initialize(Transform leftWing, Transform rightWing, Vector3 center, float radius, float altitude, bool followPlayer)
        {
            _leftWingPivot = leftWing;
            _rightWingPivot = rightWing;
            _orbitCenter = center;
            _orbitRadius = radius;
            _targetAltitude = altitude;
            _followPlayer = followPlayer;
            _angle = Random.Range(0f, 360f);
            _flapPhase = Random.Range(0f, Mathf.PI * 2f);

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_followPlayer && _playerTransform != null)
            {
                _orbitCenter = _playerTransform.position - (_playerTransform.forward * 12.0f);
            }

            // Orbital circulation
            _angle += dt * 26.0f;
            float rad = _angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * _orbitRadius, 0f, Mathf.Sin(rad) * _orbitRadius);
            Vector3 targetPos = _orbitCenter + offset;
            targetPos.y = _targetAltitude + Mathf.Sin(Time.time * 0.8f + _flapPhase) * 1.5f;

            Vector3 fwd = (targetPos - transform.position).normalized;
            if (fwd.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd, Vector3.up), dt * 4.5f);
            }
            transform.position = targetPos;

            // Wing Flap vs Thermal Glide
            _glideTimer += dt;
            if (_glideTimer >= 4.5f)
            {
                _glideTimer = 0f;
                _isGliding = Random.value > 0.45f;
            }

            float flapAngle = 0f;
            if (!_isGliding)
            {
                flapAngle = Mathf.Sin(Time.time * 14.0f + _flapPhase) * 32.0f;
            }
            else
            {
                flapAngle = 4.0f; // Static slightly dihedral glide pose
            }

            if (_leftWingPivot != null) _leftWingPivot.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
            if (_rightWingPivot != null) _rightWingPivot.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
        }
    }

    /// <summary>
    /// Surface water patrol behavior and realistic wake sway for shark fins.
    /// </summary>
    public class ProceduralSharkFin : MonoBehaviour
    {
        private Vector3 _patrolCenter;
        private float _patrolRadius;
        private float _angle;
        private float _angularSpeed;
        private float _patrolDirection;
        private Transform _tailPivot;
        private bool _isInitialized = false;

        public void Initialize(Vector3 center, float radius, Transform tailPivot)
        {
            _patrolCenter = center;
            _patrolRadius = Mathf.Max(8f, radius);
            _tailPivot = tailPivot;
            _angle = Random.Range(0f, 360f);
            float linearSpeed = Random.Range(3.2f, 4.6f);
            _angularSpeed = (linearSpeed / _patrolRadius) * Mathf.Rad2Deg;
            _patrolDirection = (Random.value > 0.5f) ? 1.0f : -1.0f;
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            float dt = Time.deltaTime;
            _angle += dt * _angularSpeed * _patrolDirection;
            float rad = _angle * Mathf.Deg2Rad;

            // 1. Analytical Position on Orbit
            Vector3 pos = _patrolCenter + new Vector3(Mathf.Cos(rad) * _patrolRadius, 0f, Mathf.Sin(rad) * _patrolRadius);

            // 2. Analytical Gerstner Wave Surface Tracking (Instantly locked to wave surface, eliminates phase lag / floating)
            OceanWaveManager.GetWaveHeightAndNormal(pos.x, pos.z, Time.time, out float waveY, out Vector3 waveNormal);
            pos.y = waveY;
            transform.position = pos;

            // 3. Exact Analytical Forward Tangent along Orbit (Zero jitter, exact forward vector)
            Vector3 forward = new Vector3(-Mathf.Sin(rad) * _patrolDirection, 0f, Mathf.Cos(rad) * _patrolDirection).normalized;

            // 4. Undulating Caudal Tail Swimming Oscillation
            float tailFrequency = 4.2f;
            float tailAngle = Mathf.Sin(Time.time * tailFrequency) * 15.0f;
            if (_tailPivot != null)
            {
                _tailPivot.localRotation = Quaternion.Euler(0f, tailAngle, 0f);
            }

            // 5. Inward Bank Angle (Realistic hydrodynamic banking into the turn)
            float bankAngle = -8.0f * _patrolDirection;
            Quaternion yawRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, -tailAngle * 0.15f, bankAngle);

            // 6. Align directly with Wave Normal
            Quaternion waveTilt = Quaternion.FromToRotation(Vector3.up, waveNormal);
            transform.rotation = waveTilt * yawRot;
        }
    }

    /// <summary>
    /// Deep-ocean breaching arc physics, articulated tail curling, blowhole spouting, and acoustic wave splashes for giant whales.
    /// </summary>
    public class ProceduralWhale : MonoBehaviour
    {
        private Vector3 _openSeaCenter;
        private Transform _pitchModel;
        private Transform _leftFlipperPivot;
        private Transform _rightFlipperPivot;
        private Transform _pedunclePivot1;
        private Transform _pedunclePivot2;
        private Transform _flukePivot;
        private Transform _spoutRoot;
        private Transform[] _spoutColumns;

        private bool _isBreaching = false;
        private float _breachProgress = 0f;
        private float _breachHeading = 0f;
        private Vector3 _breachStartPos;

        // Submerged cruising
        private float _cruiseAngle = 0f;
        private float _cruiseRadius = 38.0f;
        private float _cruiseSpeed = 12.0f;

        // FX State
        private bool _hasSpouted = false;
        private bool _hasDiveSplashed = false;
        private bool _hasFlukeSplapped = false;
        private float _spoutLifetime = 0f;

        public bool IsBreaching => _isBreaching;

        public void Initialize(Vector3 center, Transform pitchModel, Transform leftFlipper, Transform rightFlipper,
            Transform peduncle1, Transform peduncle2, Transform fluke, Transform spoutRoot, Transform[] spoutColumns)
        {
            _openSeaCenter = center;
            _pitchModel = pitchModel;
            _leftFlipperPivot = leftFlipper;
            _rightFlipperPivot = rightFlipper;
            _pedunclePivot1 = peduncle1;
            _pedunclePivot2 = peduncle2;
            _flukePivot = fluke;
            _spoutRoot = spoutRoot;
            _spoutColumns = spoutColumns;

            _cruiseAngle = Random.Range(0f, 360f);
            _cruiseSpeed = Random.Range(9f, 14f);
            _cruiseRadius = Random.Range(32f, 46f);

            transform.position = center + new Vector3(0f, -4.5f, 0f);
        }

        public void TriggerBreach()
        {
            if (_isBreaching) return;

            // Avoid breaching if near islands, harbors, or docks
            for (int i = 0; i < ProceduralDolphin.IslandCenters.Length; i++)
            {
                Vector3 isl = ProceduralDolphin.IslandCenters[i];
                float distSq = (transform.position.x - isl.x) * (transform.position.x - isl.x) + (transform.position.z - isl.z) * (transform.position.z - isl.z);
                if (distSq < (ProceduralDolphin.IslandBufferRadius + 25f) * (ProceduralDolphin.IslandBufferRadius + 25f))
                {
                    return;
                }
            }

            _isBreaching = true;
            _breachProgress = 0f;
            _breachHeading = Random.Range(0f, 360f);
            _breachStartPos = transform.position;
            _hasSpouted = false;
            _hasDiveSplashed = false;
            _hasFlukeSplapped = false;
            _spoutLifetime = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Spout plume dissipation timer
            if (_spoutLifetime > 0f)
            {
                _spoutLifetime -= dt;
                if (_spoutLifetime <= 0f && _spoutRoot != null)
                {
                    _spoutRoot.gameObject.SetActive(false);
                }
                else if (_spoutRoot != null)
                {
                    // Plume billows and expands upward
                    float plumeScaleY = Mathf.Lerp(3.8f, 0.4f, 1f - (_spoutLifetime / 1.8f));
                    float plumeScaleXZ = Mathf.Lerp(0.8f, 1.8f, 1f - (_spoutLifetime / 1.8f));
                    _spoutRoot.localScale = new Vector3(plumeScaleXZ, plumeScaleY, plumeScaleXZ);
                }
            }

            if (!_isBreaching)
            {
                UpdateSubmergedCruising(dt);
                return;
            }

            UpdateBreachSequence(dt);
        }

        private void UpdateSubmergedCruising(float dt)
        {
            // Organic wide ocean circular cruise
            _cruiseAngle += dt * _cruiseSpeed;
            float rad = _cruiseAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * _cruiseRadius, -4.5f, Mathf.Sin(rad) * _cruiseRadius);
            Vector3 targetPos = _openSeaCenter + offset;

            // Avoid swimming over island shallows or wooden docks
            for (int i = 0; i < ProceduralDolphin.IslandCenters.Length; i++)
            {
                Vector3 isl = ProceduralDolphin.IslandCenters[i];
                float distSq = (targetPos.x - isl.x) * (targetPos.x - isl.x) + (targetPos.z - isl.z) * (targetPos.z - isl.z);
                if (distSq < ProceduralDolphin.IslandBufferRadius * ProceduralDolphin.IslandBufferRadius)
                {
                    Vector3 away = new Vector3(targetPos.x - isl.x, 0f, targetPos.z - isl.z);
                    if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
                    targetPos = isl + away.normalized * (ProceduralDolphin.IslandBufferRadius + 15f);
                    targetPos.y = -4.5f;
                    break;
                }
            }

            Vector3 fwd = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            transform.position = targetPos;
            if (fwd.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }

            // Gentle swimming tail oscillation
            float tailWave = Mathf.Sin(Time.time * 2.2f);
            if (_pedunclePivot1 != null) _pedunclePivot1.localRotation = Quaternion.Euler(0f, tailWave * 4.5f, 0f);
            if (_pedunclePivot2 != null) _pedunclePivot2.localRotation = Quaternion.Euler(0f, tailWave * 6.5f, 0f);
            if (_flukePivot != null) _flukePivot.localRotation = Quaternion.Euler(0f, tailWave * 8.5f, 0f);

            // Flippers trimmed
            if (_leftFlipperPivot != null) _leftFlipperPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);
            if (_rightFlipperPivot != null) _rightFlipperPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);

            if (_pitchModel != null) _pitchModel.localRotation = Quaternion.identity;
        }

        private void UpdateBreachSequence(float dt)
        {
            _breachProgress += dt * 0.22f; // ~4.5 second majestic breach cycle
            float t = _breachProgress;

            if (t >= 1.0f)
            {
                _isBreaching = false;
                transform.position = _openSeaCenter + new Vector3(0f, -4.5f, 0f);
                return;
            }

            // 1. Parabolic trajectory: Submerged (-4.8m) -> Peaks at (+4.5m) -> Dives back under
            float height = Mathf.Sin(t * Mathf.PI) * 9.3f - 4.8f;
            float forwardOffset = t * 24.0f;

            Vector3 breachFwd = Quaternion.Euler(0f, _breachHeading, 0f) * Vector3.forward;
            Vector3 pos = _breachStartPos + (breachFwd * forwardOffset);
            pos.y = height;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, _breachHeading, 0f);

            // 2. Cinematic Pitch & Bank Arc
            float pitch;
            float roll = Mathf.Sin(t * Mathf.PI) * 16.0f; // Subtle organic roll during breach
            if (t < 0.32f)
            {
                // Rocketing upward toward surface (nose pitched up toward the sky)
                pitch = Mathf.Lerp(-35f, -20f, t / 0.32f);
            }
            else if (t < 0.55f)
            {
                // Cresting over the waves (smooth arching transition)
                float normT = (t - 0.32f) / 0.23f;
                pitch = Mathf.Lerp(-20f, 15f, Mathf.SmoothStep(0f, 1f, normT));
            }
            else if (t < 0.85f)
            {
                // Headfirst dive back into the ocean (nose pitched downward)
                float normT = (t - 0.55f) / 0.30f;
                pitch = Mathf.Lerp(15f, 45f, Mathf.SmoothStep(0f, 1f, normT));
            }
            else
            {
                // Leveling out underwater
                float normT = (t - 0.85f) / 0.15f;
                pitch = Mathf.Lerp(45f, 0f, normT);
            }

            if (_pitchModel != null)
            {
                _pitchModel.localRotation = Quaternion.Euler(pitch, 0f, roll);
            }

            // 3. Articulated Tail Peduncle & Fluke Flexing
            float tailPitch1 = 0f;
            float tailPitch2 = 0f;

            if (t < 0.32f)
            {
                // Tail flexes downward powering ascent
                tailPitch1 = -18f;
                tailPitch2 = -12f;
            }
            else if (t < 0.55f)
            {
                // Aligns with arched back at apex
                tailPitch1 = Mathf.Lerp(-18f, 8f, (t - 0.32f) / 0.23f);
                tailPitch2 = Mathf.Lerp(-12f, 6f, (t - 0.32f) / 0.23f);
            }
            else if (t < 0.86f)
            {
                // THE ICONIC FLUKE LIFT: Tail curls UPWARD out of the water while head dives!
                float normT = (t - 0.55f) / 0.31f;
                tailPitch1 = Mathf.Lerp(8f, 38f, Mathf.SmoothStep(0f, 1f, normT));
                tailPitch2 = Mathf.Lerp(6f, 26f, Mathf.SmoothStep(0f, 1f, normT));
            }
            else
            {
                // Slap & level out
                float normT = (t - 0.86f) / 0.14f;
                tailPitch1 = Mathf.Lerp(38f, 0f, normT);
                tailPitch2 = Mathf.Lerp(26f, 0f, normT);
            }

            if (_pedunclePivot1 != null) _pedunclePivot1.localRotation = Quaternion.Euler(tailPitch1, 0f, 0f);
            if (_pedunclePivot2 != null) _pedunclePivot2.localRotation = Quaternion.Euler(tailPitch2, 0f, 0f);
            if (_flukePivot != null) _flukePivot.localRotation = Quaternion.Euler(tailPitch2 * 0.5f, 0f, 0f);

            // 4. Pectoral Flippers Spreading
            float flipperAngle = Mathf.Sin(t * Mathf.PI) * 22.0f;
            if (_leftFlipperPivot != null) _leftFlipperPivot.localRotation = Quaternion.Euler(0f, -flipperAngle * 0.4f, flipperAngle);
            if (_rightFlipperPivot != null) _rightFlipperPivot.localRotation = Quaternion.Euler(0f, flipperAngle * 0.4f, -flipperAngle);

            // 5. Blowhole Spout Trigger (As head breaks surface)
            if (t >= 0.30f && !_hasSpouted)
            {
                _hasSpouted = true;
                _spoutLifetime = 1.8f;
                if (_spoutRoot != null)
                {
                    _spoutRoot.gameObject.SetActive(true);
                    _spoutRoot.localScale = new Vector3(0.6f, 0.2f, 0.6f);
                }
                NauticalAudioManager.Instance?.PlaySFX(SFXType.WaveSplash);
            }

            // 6. Dive Water Entry Splash (When head strikes water)
            if (t >= 0.65f && !_hasDiveSplashed)
            {
                _hasDiveSplashed = true;
                NauticalAudioManager.Instance?.PlaySFX(SFXType.WaveSplash);
            }

            // 7. Fluke Slap Splash (When high-arched flukes slap down into ocean)
            if (t >= 0.88f && !_hasFlukeSplapped)
            {
                _hasFlukeSplapped = true;
                NauticalAudioManager.Instance?.PlaySFX(SFXType.WaveSplash);
            }
        }
    }
}
