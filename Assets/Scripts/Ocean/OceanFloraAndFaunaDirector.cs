using System.Collections.Generic;
using Plunderlust.Ocean;
using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Ocean Environment & Atmosphere Director.
    /// Transforms the open sea from a barren void into a living, vibrant Caribbean world:
    /// 1. Playful Dolphin Pods: Sleek cetaceans swimming alongside flagships and leaping gracefully out of the water.
    /// 2. Schools of Leaping Flying Fish: Shimmering schools skipping across wave crests ahead of ships.
    /// 3. Ambient Floating Flotsam & Drift Props: Weathered rum barrels, drift crates, and bobbing kelp beds.
    /// 4. Submerged Sandbar Reefs: Shallow underwater shelves creating glowing turquoise lagoons around islands.
    /// Operates with zero per-frame heap allocations.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class OceanFloraAndFaunaDirector : MonoBehaviour
    {
        public static OceanFloraAndFaunaDirector Instance { get; private set; }

        private static Transform _cachedPlayer;
        public static Transform PlayerTransform
        {
            get
            {
                if (_cachedPlayer == null)
                {
                    GameObject p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) _cachedPlayer = p.transform;
                }
                return _cachedPlayer;
            }
        }

        [Header("Marine Fauna Settings")]
        [SerializeField] private int _dolphinCount = 1;
        [SerializeField] private int _fishSchoolCount = 2;
        [SerializeField] private int _mantaRayCount = 1;

        private Transform _playerTransform;

        // Shared Procedural Materials
        private Material _dolphinSlateMat;
        private Material _dolphinBellyMat;
        private Material _flyingFishMat;
        private Material _flotsamWoodMat;
        private Material _kelpMat;
        private Material _sandbarMat;
        private Material _splashFoamMat;

        // Runtime Entities
        private readonly List<ProceduralDolphin> _dolphins = new List<ProceduralDolphin>(4);
        private readonly List<ProceduralFishSchool> _fishSchools = new List<ProceduralFishSchool>(4);
        private readonly List<ProceduralMantaRay> _mantaRays = new List<ProceduralMantaRay>(3);
        private readonly List<Transform> _flotsamProps = new List<Transform>(24);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateMaterials();
        }

        private void Start()
        {
            FindPlayer();
            SpawnDolphinPod();
            SpawnFishSchools();
            SpawnMantaRays();
            SpawnOceanFlotsam();
            SpawnCoastalSandbars();
        }

        private void FindPlayer()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _playerTransform = playerGo.transform;
        }

        private void CreateMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            _dolphinSlateMat = CreateMaterial("M_DolphinSlate", litShader, new Color(0.18f, 0.28f, 0.38f), 0.65f); // Sleek ocean slate
            _dolphinBellyMat = CreateMaterial("M_DolphinBelly", litShader, new Color(0.88f, 0.92f, 0.95f), 0.55f); // Pearl ivory
            _flyingFishMat = CreateMaterial("M_FlyingFish", litShader, new Color(0.20f, 0.78f, 0.88f), 0.85f);   // Shimmering tropical azure
            _flotsamWoodMat = CreateMaterial("M_FlotsamWood", litShader, new Color(0.42f, 0.28f, 0.16f), 0.20f);  // Weathered timber
            _kelpMat = CreateMaterial("M_OceanKelp", litShader, new Color(0.12f, 0.38f, 0.20f), 0.40f);         // Sea kelp olive green
            _sandbarMat = CreateMaterial("M_SubmergedSandbar", litShader, new Color(0.86f, 0.76f, 0.54f), 0.15f); // Warm coastal sand

            _splashFoamMat = Resources.Load<Material>("M_WakeFoam");
            if (_splashFoamMat == null)
            {
                Shader foamShader = Shader.Find("Plunderlust/Ocean/WakeFoam");
                if (foamShader == null) foamShader = litShader;
                _splashFoamMat = new Material(foamShader) { name = "M_DolphinSplashFoam" };
            }
            if (_splashFoamMat.HasProperty("_Color")) _splashFoamMat.SetColor("_Color", new Color(0.95f, 0.99f, 1.0f, 0.90f));
            if (_splashFoamMat.HasProperty("_BaseColor")) _splashFoamMat.SetColor("_BaseColor", new Color(0.95f, 0.99f, 1.0f, 0.90f));
        }

        private static Material CreateMaterial(string name, Shader shader, Color color, float smoothness)
        {
            Material mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        // =====================================================================
        // 1. PLAYFUL DOLPHIN PODS
        // =====================================================================

        private void SpawnDolphinPod()
        {
            GameObject podContainer = new GameObject("DolphinPod");
            podContainer.transform.SetParent(transform, false);

            for (int i = 0; i < _dolphinCount; i++)
            {
                GameObject dolphinGo = new GameObject($"Dolphin_{i}");
                dolphinGo.transform.SetParent(podContainer.transform, false);

                GameObject pitchRoot = new GameObject("PitchRoot");
                pitchRoot.transform.SetParent(dolphinGo.transform, false);

                // Instantiate authentic 3D Dolphin model (Dolphin.fbx)
                GameObject dolphinModel = SeaAnimalModelRegistry.SpawnModel(
                    SeaAnimalType.Dolphin,
                    pitchRoot.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one * 0.22f
                );

                // Authentic water trail behind dolphin (zero static squares)
                TrailRenderer dolphinTrail = dolphinGo.AddComponent<TrailRenderer>();
                dolphinTrail.material = _splashFoamMat;
                dolphinTrail.time = 1.3f;
                dolphinTrail.startWidth = 0.5f;
                dolphinTrail.endWidth = 0.05f;
                dolphinTrail.alignment = LineAlignment.TransformZ;
                dolphinTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dolphinTrail.receiveShadows = false;

                Gradient g = new Gradient();
                g.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.92f, 0.98f, 1f), 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                dolphinTrail.colorGradient = g;

                // Initialize Controller
                ProceduralDolphin dolphin = dolphinGo.AddComponent<ProceduralDolphin>();
                dolphin.Initialize(pitchRoot.transform, null, dolphinTrail, i * 1.8f);
                _dolphins.Add(dolphin);
            }
        }

        // =====================================================================
        // 1.5. MAJESTIC MANTA RAY PODS
        // =====================================================================

        private void SpawnMantaRays()
        {
            GameObject mantaContainer = new GameObject("MantaRayPod");
            mantaContainer.transform.SetParent(transform, false);

            for (int m = 0; m < _mantaRayCount; m++)
            {
                GameObject mantaGo = new GameObject($"MantaRay_{m}");
                mantaGo.transform.SetParent(mantaContainer.transform, false);

                // Instantiate authentic 3D Manta Ray model (Manta ray.fbx)
                GameObject model = SeaAnimalModelRegistry.SpawnModel(
                    SeaAnimalType.MantaRay,
                    mantaGo.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one * 0.35f
                );

                ProceduralMantaRay manta = mantaGo.AddComponent<ProceduralMantaRay>();
                manta.Initialize(m * 2.5f);
                _mantaRays.Add(manta);
            }
        }

        // =====================================================================
        // 2. SCHOOLS OF LEAPING FLYING FISH
        // =====================================================================

        private void SpawnFishSchools()
        {
            GameObject fishContainer = new GameObject("FishSchoolsContainer");
            fishContainer.transform.SetParent(transform, false);

            for (int s = 0; s < _fishSchoolCount; s++)
            {
                GameObject schoolGo = new GameObject($"FishSchool_{s}");
                schoolGo.transform.SetParent(fishContainer.transform, false);

                ProceduralFishSchool school = schoolGo.AddComponent<ProceduralFishSchool>();
                school.Initialize(_flyingFishMat, s * 4.5f);
                _fishSchools.Add(school);
            }
        }

        // =====================================================================
        // 3. AMBIENT OCEAN FLOTSAM & DRIFT PROPS
        // =====================================================================

        private void SpawnOceanFlotsam()
        {
            GameObject flotsamContainer = new GameObject("OceanFlotsamContainer");
            flotsamContainer.transform.SetParent(transform, false);

            // Pre-seed 12 ambient flotsam clusters along popular sailing channels
            Vector3[] seedLocations = new Vector3[]
            {
                new Vector3(-45f, 0f, 40f),
                new Vector3(55f, 0f, 35f),
                new Vector3(-85f, 0f, 95f),
                new Vector3(70f, 0f, 110f),
                new Vector3(-120f, 0f, -30f),
                new Vector3(90f, 0f, -60f),
                new Vector3(-25f, 0f, 130f),
                new Vector3(30f, 0f, -110f),
                new Vector3(-110f, 0f, 60f),
                new Vector3(115f, 0f, 50f),
                new Vector3(-60f, 0f, -85f),
                new Vector3(40f, 0f, 85f)
            };

            for (int c = 0; c < seedLocations.Length; c++)
            {
                Vector3 center = seedLocations[c];

                // 1. Floating weathered Rum Barrel
                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = $"FlotsamBarrel_{c}";
                barrel.transform.SetParent(flotsamContainer.transform, false);
                barrel.transform.position = center + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
                barrel.transform.localScale = new Vector3(0.65f, 0.45f, 0.65f);
                barrel.transform.rotation = Quaternion.Euler(Random.Range(8f, 25f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                ApplyMaterial(barrel, _flotsamWoodMat);
                _flotsamProps.Add(barrel.transform);

                // 2. Waterlogged Drift Crate
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = $"FlotsamCrate_{c}";
                crate.transform.SetParent(flotsamContainer.transform, false);
                crate.transform.position = center + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                crate.transform.localScale = new Vector3(0.85f, 0.65f, 0.85f);
                crate.transform.rotation = Quaternion.Euler(Random.Range(5f, 18f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
                ApplyMaterial(crate, _flotsamWoodMat);
                _flotsamProps.Add(crate.transform);

                // 3. Floating Kelp / Seaweed Patch (Flat organic disc bobbing on wave)
                GameObject kelp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                kelp.name = $"KelpPatch_{c}";
                kelp.transform.SetParent(flotsamContainer.transform, false);
                kelp.transform.position = center + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                kelp.transform.localScale = new Vector3(3.2f, 0.04f, 2.8f);
                kelp.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                ApplyMaterial(kelp, _kelpMat);
                _flotsamProps.Add(kelp.transform);
            }
        }

        // =====================================================================
        // 4. SUBMERGED COASTAL SANDBAR REEFS (LAGOON TURQUOISE EFFECT)
        // =====================================================================

        private void SpawnCoastalSandbars()
        {
            GameObject sandbarContainer = new GameObject("SubmergedSandbarsContainer");
            sandbarContainer.transform.SetParent(transform, false);

            // Submerged sand shelves around key island harbors (depth ~1.8m to 2.5m)
            // The StylizedOcean depth shader naturally transforms water over these into glowing turquoise shallows!
            Vector3[] lagoonCenters = new Vector3[]
            {
                new Vector3(38f, -2.0f, 22f),    // Port Royal haven approach
                new Vector3(-110f, -2.2f, 165f), // Tortuga corsair shallows
                new Vector3(-45f, -1.8f, -75f),  // Smuggler's cove reef
                new Vector3(75f, -2.1f, -65f),   // Treasure atoll sandbar
                new Vector3(-80f, -2.3f, 45f)    // Dead Man's channel sandbank
            };

            for (int i = 0; i < lagoonCenters.Length; i++)
            {
                GameObject sandShelf = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sandShelf.name = $"SandbarShelf_{i}";
                sandShelf.transform.SetParent(sandbarContainer.transform, false);
                sandShelf.transform.position = lagoonCenters[i];
                sandShelf.transform.localScale = new Vector3(Random.Range(38f, 52f), 1.2f, Random.Range(32f, 46f));
                sandShelf.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                ApplyMaterial(sandShelf, _sandbarMat);
            }
        }

        private void Update()
        {
            float time = Time.time;

            // Gently bob all ambient flotsam on live ocean wave height
            for (int i = 0; i < _flotsamProps.Count; i++)
            {
                Transform prop = _flotsamProps[i];
                if (prop == null) continue;

                Vector3 pos = prop.position;
                float waveY = OceanWaveManager.GetWaveHeight(pos.x, pos.z, time);
                pos.y = waveY - 0.12f; // Low waterline submersion
                prop.position = pos;
            }
        }

        private static GameObject CreateSplashQuad(string name, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            Mesh m = new Mesh { name = "SplashQuad" };
            m.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f)
            };
            m.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mf.mesh = m;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }

    /// <summary>
    /// Individual procedural dolphin performing swimming escort and acrobatic leaping breaches.
    /// </summary>
    public class ProceduralDolphin : MonoBehaviour
    {
        public static readonly Vector3[] IslandCenters = new Vector3[]
        {
            new Vector3(45f, 0f, 25f),     // PortHaven
            new Vector3(-45f, 0f, 30f),    // Smuggler Cove
            new Vector3(-120f, 0f, 180f),  // Tortuga Haven
            new Vector3(95f, 0f, -85f),    // Treasure Atoll East
            new Vector3(-85f, 0f, -65f)    // Treasure Atoll West
        };
        public const float IslandBufferRadius = 45f;

        private Transform _pitchRoot;
        private Transform _tailPivot;
        private TrailRenderer _dolphinTrail;

        private Transform _playerTransform;
        private float _currentSpeed = 2.0f;
        private float _flankSide = 1.0f;
        private float _flankSwitchTimer = 22f;
        private float _idleAngle = 0f;

        private bool _isLeaping = false;
        private float _leapProgress = 0f;
        private float _leapTimer = 0f;
        private float _nextLeapTime = 18f;

        private Vector3 _leapStartPos;
        private Vector3 _leapApexPos;
        private Vector3 _leapEndPos;

        public void Initialize(Transform pitchRoot, Transform tailPivot, TrailRenderer dolphinTrail, float startOffset)
        {
            _pitchRoot = pitchRoot;
            _tailPivot = tailPivot;
            _dolphinTrail = dolphinTrail;

            _flankSide = (startOffset % 2.0f > 1.0f) ? -1.0f : 1.0f;
            _flankSwitchTimer = Random.Range(20f, 35f);
            _idleAngle = Random.Range(0f, 360f);
            _currentSpeed = Random.Range(1.8f, 2.4f);
            _nextLeapTime = Random.Range(20f, 36f) + startOffset;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;

            if (_playerTransform != null)
            {
                Vector3 pFwd = _playerTransform.forward;
                pFwd.y = 0f;
                if (pFwd.sqrMagnitude < 0.001f) pFwd = Vector3.forward;
                pFwd.Normalize();
                Vector3 pRight = Vector3.Cross(Vector3.up, pFwd);

                Vector3 spawnPos = _playerTransform.position + (pFwd * 6f) + (pRight * (_flankSide * 8.5f));
                float waveY = OceanWaveManager.GetWaveHeight(spawnPos.x, spawnPos.z, Time.time);
                spawnPos.y = waveY - 0.35f;
                transform.position = spawnPos;
                transform.rotation = Quaternion.LookRotation(pFwd, Vector3.up);
            }

            if (_dolphinTrail != null)
            {
                _dolphinTrail.Clear();
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                _playerTransform = OceanFloraAndFaunaDirector.PlayerTransform;
                if (_playerTransform == null) return;
            }

            float dt = Time.deltaTime;
            float time = Time.time;

            if (_isLeaping)
            {
                UpdateLeapArc(dt, time);
            }
            else
            {
                UpdateCruisingSwim(dt, time);
            }
        }

        private void UpdateCruisingSwim(float dt, float time)
        {
            // Switch bow-riding flanks periodically for natural playful pacing
            _flankSwitchTimer -= dt;
            if (_flankSwitchTimer <= 0f)
            {
                _flankSwitchTimer = Random.Range(24f, 42f);
                _flankSide = -_flankSide;
            }

            Vector3 playerPos = _playerTransform.position;
            Vector3 playerFwd = _playerTransform.forward;
            playerFwd.y = 0f;
            if (playerFwd.sqrMagnitude < 0.001f) playerFwd = Vector3.forward;
            playerFwd.Normalize();
            Vector3 playerRight = Vector3.Cross(Vector3.up, playerFwd);

            // Read actual ship speed in m/s
            var loco = _playerTransform.GetComponent<ShipLocomotionController>();
            float playerSpeedMs = loco != null ? Mathf.Abs(loco.CurrentSpeedKnots * 0.5144f) : 0f;

            Vector3 targetPos;
            if (playerSpeedMs > 0.6f)
            {
                // Underway: escort just off the bow wave on the active flank
                targetPos = playerPos + (playerFwd * 7.5f) + (playerRight * (_flankSide * 8.0f));
            }
            else
            {
                // Idle/anchored: slow, graceful circular perimeter orbit
                _idleAngle += dt * 0.22f; // ~28s per full lazy orbit
                targetPos = playerPos + new Vector3(Mathf.Cos(_idleAngle) * 14f, 0f, Mathf.Sin(_idleAngle) * 14f);
            }

            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            float distToTarget = toTarget.magnitude;
            Vector3 desiredDir = distToTarget > 0.1f ? (toTarget / distToTarget) : transform.forward;

            // Anticipatory island avoidance (smooth directional blend, NEVER hard snap)
            for (int i = 0; i < IslandCenters.Length; i++)
            {
                Vector3 isl = IslandCenters[i];
                float distSq = (transform.position.x - isl.x) * (transform.position.x - isl.x) + (transform.position.z - isl.z) * (transform.position.z - isl.z);
                float avoidRadius = IslandBufferRadius + 14f;
                if (distSq < avoidRadius * avoidRadius)
                {
                    Vector3 away = transform.position - isl;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.001f)
                    {
                        float factor = 1.0f - Mathf.Clamp01(Mathf.Sqrt(distSq) / avoidRadius);
                        desiredDir = Vector3.Slerp(desiredDir, away.normalized, factor * 0.85f).normalized;
                    }
                }
            }

            // Smooth forward steering: rotate smoothly towards target heading
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 60f * dt);

            // Realistic dolphin speed: calm cruising speed (1.8 to 2.8 m/s max)
            float desiredSpeed = 2.0f;
            if (playerSpeedMs > 1.0f)
            {
                // Keep company with moving ship, capped at a natural 2.8 m/s (approx 5.4 knots)
                desiredSpeed = Mathf.Clamp(playerSpeedMs * 0.95f + 0.25f, 1.8f, 2.8f);
            }
            else
            {
                desiredSpeed = 1.8f;
            }

            if (distToTarget > 18.0f)
            {
                // Gently catch up if ship pulled ahead
                desiredSpeed = Mathf.Min(desiredSpeed * 1.25f, 3.2f);
            }
            else if (distToTarget < 2.0f)
            {
                // Ease back when comfortably alongside
                desiredSpeed *= 0.85f;
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, dt * 1.2f);

            // Physical forward motion: ALWAYS move strictly forward along facing direction (zero sideways drift)
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Vector3 newPos = transform.position + (fwd * (_currentSpeed * dt));
            float waveY = OceanWaveManager.GetWaveHeight(newPos.x, newPos.z, time);
            newPos.y = waveY - 0.35f; // Swim naturally submerged just below wave surface
            transform.position = newPos;

            // Tail swimming flutter (slow, organic rhythmic sweep)
            float tailWiggle = Mathf.Sin(time * 4.2f) * 12.0f;
            if (_tailPivot != null) _tailPivot.localRotation = Quaternion.Euler(tailWiggle, 0f, 0f);

            // Periodic graceful leap
            _leapTimer += dt;
            if (_leapTimer >= _nextLeapTime)
            {
                _leapTimer = 0f;
                _nextLeapTime = Random.Range(24f, 42f);
                StartLeap(transform.position, fwd);
            }
        }

        private void StartLeap(Vector3 startPos, Vector3 forward)
        {
            // Prevent leaping into landmasses
            for (int i = 0; i < IslandCenters.Length; i++)
            {
                Vector3 isl = IslandCenters[i];
                float distSq = (startPos.x - isl.x) * (startPos.x - isl.x) + (startPos.z - isl.z) * (startPos.z - isl.z);
                if (distSq < (IslandBufferRadius + 10f) * (IslandBufferRadius + 10f))
                {
                    return;
                }
            }

            _isLeaping = true;
            _leapProgress = 0f;
            _leapStartPos = startPos;
            // Short, gentle, graceful leap arc (5.0m distance, 1.4m height)
            _leapApexPos = startPos + (forward * 2.5f) + (Vector3.up * Random.Range(1.3f, 1.6f));
            _leapEndPos = startPos + (forward * 5.0f);
            _flankSide = -_flankSide; // Switch flanks on dive!
        }

        private void UpdateLeapArc(float dt, float time)
        {
            _leapProgress += dt / 1.35f; // ~1.35s graceful slow-motion arc
            float t = Mathf.Clamp01(_leapProgress);

            // Smooth parabolic Bezier trajectory
            Vector3 p0 = _leapStartPos;
            Vector3 p1 = _leapApexPos;
            Vector3 p2 = _leapEndPos;
            Vector3 pos = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;

            Vector3 tangent = 2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1);
            if (tangent.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
            transform.position = pos;

            if (_tailPivot != null)
            {
                _tailPivot.localRotation = Quaternion.Euler(Mathf.Sin(time * 8f) * 14f, 0f, 0f);
            }

            if (t >= 1.0f)
            {
                _isLeaping = false;
                transform.position = _leapEndPos;
                Vector3 landingFwd = (p2 - p1);
                landingFwd.y = 0f;
                if (landingFwd.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(landingFwd.normalized, Vector3.up);
                }
            }
        }
    }

    /// <summary>
    /// Individual procedural school of flying fish that skip together across wave crests.
    /// </summary>
    public class ProceduralFishSchool : MonoBehaviour
    {
        private const int FishCount = 3;
        private readonly Transform[] _fish = new Transform[FishCount];
        private Vector3[] _localOffsets = new Vector3[FishCount];

        private Transform _playerTransform;
        private Vector3 _startPos;
        private Vector3 _direction;
        private bool _isFlying = false;
        private float _flightProgress = 0f;
        private float _respawnTimer = 0f;
        private float _nextFlightTime = 5f;

        public void Initialize(Material fishMat, float startDelay)
        {
            _nextFlightTime = Random.Range(18f, 32f) + startDelay;

            for (int i = 0; i < FishCount; i++)
            {
                // Instantiate authentic 3D tropical fish models (Fish1.fbx, Fish2.fbx, Fish3.fbx)
                SeaAnimalType fishType = (i % 3 == 0) ? SeaAnimalType.Fish1 : (i % 3 == 1) ? SeaAnimalType.Fish2 : SeaAnimalType.Fish3;
                GameObject f = SeaAnimalModelRegistry.SpawnModel(fishType, transform, Vector3.zero, Quaternion.identity, Vector3.one * 0.045f);

                if (f == null)
                {
                    f = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    f.name = $"Fish_{i}";
                    f.transform.SetParent(transform, false);
                    f.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
                    f.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    ApplyMat(f, fishMat);
                }

                _fish[i] = f.transform;
                _localOffsets[i] = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.1f, 0.1f), Random.Range(-0.8f, 0.8f));
                f.SetActive(false);
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                _playerTransform = OceanFloraAndFaunaDirector.PlayerTransform;
                if (_playerTransform == null) return;
            }

            float dt = Time.deltaTime;
            float time = Time.time;

            if (_isFlying)
            {
                _flightProgress += dt * 0.75f; // ~1.3s skip across water
                float t = Mathf.Clamp01(_flightProgress);

                // Parabolic skip: hops 1.2m above the live wave surface
                Vector3 currentCenter = _startPos + _direction * (t * 18.0f);
                float waveY = OceanWaveManager.GetWaveHeight(currentCenter.x, currentCenter.z, time);
                float hopY = Mathf.Sin(t * Mathf.PI) * 1.2f;
                currentCenter.y = waveY + hopY + 0.1f;

                transform.position = currentCenter;
                transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

                for (int i = 0; i < FishCount; i++)
                {
                    if (_fish[i] != null)
                    {
                        Vector3 jitter = new Vector3(0f, Mathf.Sin(time * 18f + i) * 0.08f, 0f);
                        _fish[i].localPosition = _localOffsets[i] + jitter;
                    }
                }

                if (t >= 1.0f)
                {
                    _isFlying = false;
                    for (int i = 0; i < FishCount; i++)
                    {
                        if (_fish[i] != null) _fish[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                _respawnTimer += dt;
                if (_respawnTimer >= _nextFlightTime)
                {
                    _respawnTimer = 0f;
                    _nextFlightTime = Random.Range(24f, 45f);

                    // Launch in front of the moving ship in open ocean
                    Vector3 shipPos = _playerTransform.position;
                    Vector3 shipFwd = _playerTransform.forward;
                    _startPos = shipPos + (shipFwd * Random.Range(14f, 22f)) + (Random.insideUnitSphere * 4f);

                    // Do not spawn fish flights over island terrain or docks
                    for (int i = 0; i < ProceduralDolphin.IslandCenters.Length; i++)
                    {
                        Vector3 isl = ProceduralDolphin.IslandCenters[i];
                        float distSq = (_startPos.x - isl.x) * (_startPos.x - isl.x) + (_startPos.z - isl.z) * (_startPos.z - isl.z);
                        if (distSq < ProceduralDolphin.IslandBufferRadius * ProceduralDolphin.IslandBufferRadius)
                        {
                            return;
                        }
                    }

                    _direction = Quaternion.Euler(0f, Random.Range(-45f, 45f), 0f) * shipFwd;
                    _direction.y = 0f;
                    _direction.Normalize();

                    _isFlying = true;
                    _flightProgress = 0f;
                    for (int i = 0; i < FishCount; i++)
                    {
                        if (_fish[i] != null) _fish[i].gameObject.SetActive(true);
                    }
                }
            }
        }

        private static void ApplyMat(GameObject go, Material mat)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }

    /// <summary>
    /// Graceful giant manta ray cruising at shallow depths beneath the Caribbean water surface,
    /// banking organically into turns and undulating pectoral wings.
    /// </summary>
    public class ProceduralMantaRay : MonoBehaviour
    {
        private Transform _playerTransform;
        private Vector3 _glideCenter;
        private float _orbitAngle;
        private float _orbitRadius;
        private float _glideSpeed;
        private float _depth;
        private float _direction;

        public void Initialize(float startOffset)
        {
            _orbitAngle = startOffset * 55f;
            _orbitRadius = Random.Range(24f, 42f);
            _glideSpeed = Random.Range(3.2f, 4.8f);
            _depth = Random.Range(1.6f, 2.8f);
            _direction = (Random.value > 0.5f) ? 1.0f : -1.0f;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            _glideCenter = (_playerTransform != null) ? _playerTransform.position : Vector3.zero;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                _playerTransform = OceanFloraAndFaunaDirector.PlayerTransform;
                if (_playerTransform == null) return;
            }

            float dt = Time.deltaTime;
            float time = Time.time;

            // Slowly follow player area
            _glideCenter = Vector3.Lerp(_glideCenter, _playerTransform.position, dt * 0.4f);

            _orbitAngle += (_glideSpeed / _orbitRadius) * dt * Mathf.Rad2Deg * _direction;
            float rad = _orbitAngle * Mathf.Deg2Rad;

            Vector3 pos = _glideCenter + new Vector3(Mathf.Cos(rad) * _orbitRadius, 0f, Mathf.Sin(rad) * _orbitRadius);

            // Avoid swimming onto islands
            for (int i = 0; i < ProceduralDolphin.IslandCenters.Length; i++)
            {
                Vector3 isl = ProceduralDolphin.IslandCenters[i];
                float distSq = (pos.x - isl.x) * (pos.x - isl.x) + (pos.z - isl.z) * (pos.z - isl.z);
                if (distSq < ProceduralDolphin.IslandBufferRadius * ProceduralDolphin.IslandBufferRadius)
                {
                    Vector3 away = new Vector3(pos.x - isl.x, 0f, pos.z - isl.z);
                    if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
                    pos = isl + away.normalized * (ProceduralDolphin.IslandBufferRadius + 6f);
                    break;
                }
            }

            float waveY = OceanWaveManager.GetWaveHeight(pos.x, pos.z, time);
            pos.y = waveY - _depth;
            transform.position = pos;

            // Forward heading vector
            Vector3 forward = new Vector3(-Mathf.Sin(rad) * _direction, 0f, Mathf.Cos(rad) * _direction).normalized;

            // Hydrodynamic banking into the turn + gentle glide pitch
            float bankAngle = -12.0f * _direction;
            float pitchAngle = Mathf.Sin(time * 0.8f + _orbitAngle * 0.1f) * 3.5f;

            Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(pitchAngle, 0f, bankAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 3.5f);
        }
    }
}
