using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.Ocean
{
    /// <summary>
    /// Lightweight, mobile-optimized procedural naval wake and bow wash system.
    /// Emits expanding, fading V-shaped stern wake ripples and dynamic bow wash foam
    /// aligned strictly to live ocean wave surface heights with zero per-frame heap allocations.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShipWakeVFX : MonoBehaviour
    {
        [Header("Wake Configuration")]
        [SerializeField] private float _emissionInterval = 0.16f;
        [SerializeField] private float _wakeLifetime = 2.8f;
        [SerializeField] private float _initialScale = 2.2f;
        [SerializeField] private float _expansionRate = 3.6f;
        [SerializeField] private float _minSpeedKnots = 1.0f;
        [SerializeField] private Vector3 _sternOffset = new Vector3(0f, 0.05f, -2.4f);
        [SerializeField] private Vector3 _portBowOffset = new Vector3(-0.9f, 0.05f, 2.0f);
        [SerializeField] private Vector3 _starboardBowOffset = new Vector3(0.9f, 0.05f, 2.0f);

        private ShipLocomotionController _locomotion;
        private Rigidbody _rb;

        // Preallocated circular buffer of wake quads (Zero runtime GC)
        private const int PoolSize = 28;
        private struct WakePlaque
        {
            public GameObject Go;
            public Transform Transform;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock PropBlock;
            public Vector3 Position;
            public Vector3 Forward;
            public float Age;
            public float MaxLifetime;
            public bool Active;
        }

        private readonly WakePlaque[] _wakePool = new WakePlaque[PoolSize];
        private int _poolIndex = 0;
        private float _emitTimer = 0f;

        // Dynamic bow wash foam transforms
        private Transform _portBowFoam;
        private Transform _starboardBowFoam;
        private TrailRenderer _portSternTrail;
        private TrailRenderer _starboardSternTrail;
        private Material _wakeMaterial;
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private Vector3 _lastBoxSize;

        private void Awake()
        {
            _locomotion = GetComponent<ShipLocomotionController>();
            _rb = GetComponent<Rigidbody>();

            CreateWakeMaterial();
            BuildWakePool();
            BuildSternTrails();
            BuildBowFoam();
        }

        private void Start()
        {
            RefreshHullBounds();
        }

        public void RefreshHullBounds()
        {
            // ponytail: dynamically snap wake emission and bow wash to exact hull bounds
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                _lastBoxSize = box.size;
                float halfLen = box.size.z * 0.5f;
                float halfWidth = box.size.x * 0.5f;
                _sternOffset = new Vector3(box.center.x, 0.05f, box.center.z - halfLen + 0.15f);
                _portBowOffset = new Vector3(box.center.x - halfWidth * 0.7f, 0.05f, box.center.z + halfLen * 0.7f);
                _starboardBowOffset = new Vector3(box.center.x + halfWidth * 0.7f, 0.05f, box.center.z + halfLen * 0.7f);
                _initialScale = Mathf.Clamp(box.size.x * 0.95f, 1.1f, 3.8f);
            }
        }

        private void CreateWakeMaterial()
        {
            _wakeMaterial = Resources.Load<Material>("M_WakeFoam");
            if (_wakeMaterial == null)
            {
                Shader wakeShader = Shader.Find("Plunderlust/Ocean/WakeFoam");
                if (wakeShader == null) wakeShader = Shader.Find("Universal Render Pipeline/Unlit");

                _wakeMaterial = new Material(wakeShader)
                {
                    name = "M_ProceduralShipWake"
                };
            }
            if (_wakeMaterial.HasProperty(ColorProp)) _wakeMaterial.SetColor(ColorProp, new Color(0.92f, 0.98f, 1.0f, 0.85f));
            if (_wakeMaterial.HasProperty("_BaseColor")) _wakeMaterial.SetColor("_BaseColor", new Color(0.92f, 0.98f, 1.0f, 0.85f));
        }

        private void BuildSternTrails()
        {
            float halfWidth = 0.5f;
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) halfWidth = Mathf.Clamp(box.size.x * 0.35f, 0.35f, 1.5f);

            _portSternTrail = CreateSternTrail("PortSternTrail", new Vector3(-halfWidth, 0.05f, _sternOffset.z));
            _starboardSternTrail = CreateSternTrail("StarboardSternTrail", new Vector3(halfWidth, 0.05f, _sternOffset.z));
        }

        private TrailRenderer CreateSternTrail(string name, Vector3 localOffset)
        {
            GameObject trailGo = new GameObject(name);
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = localOffset;

            TrailRenderer tr = trailGo.AddComponent<TrailRenderer>();
            tr.material = _wakeMaterial;
            tr.time = 2.0f;
            tr.startWidth = 0.55f;
            tr.endWidth = 0.05f;
            tr.alignment = LineAlignment.TransformZ;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.92f, 0.98f, 1f), 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.80f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            tr.colorGradient = g;
            return tr;
        }

        private void BuildWakePool()
        {
            GameObject container = new GameObject("ShipWakeContainer");
            container.transform.SetParent(null, false); // Keep in world space

            Mesh quadMesh = CreateHorizontalQuadMesh();

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject plaqueGo = new GameObject($"WakePlaque_{i}");
                plaqueGo.transform.SetParent(container.transform, false);

                MeshFilter mf = plaqueGo.AddComponent<MeshFilter>();
                mf.mesh = quadMesh;

                MeshRenderer mr = plaqueGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _wakeMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _wakePool[i] = new WakePlaque
                {
                    Go = plaqueGo,
                    Transform = plaqueGo.transform,
                    Renderer = mr,
                    PropBlock = new MaterialPropertyBlock(),
                    Active = false
                };
                plaqueGo.SetActive(false);
            }
        }

        private void BuildBowFoam()
        {
            Mesh quadMesh = CreateHorizontalQuadMesh();

            _portBowFoam = CreateBowPlaque("PortBowFoam", quadMesh);
            _starboardBowFoam = CreateBowPlaque("StarboardBowFoam", quadMesh);
        }

        private Transform CreateBowPlaque(string name, Mesh mesh)
        {
            GameObject bowGo = new GameObject(name);
            bowGo.transform.SetParent(transform, false);

            MeshFilter mf = bowGo.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            MeshRenderer mr = bowGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _wakeMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            bowGo.transform.localScale = Vector3.one * 1.4f;
            bowGo.SetActive(false);
            return bowGo.transform;
        }

        private static Mesh CreateHorizontalQuadMesh()
        {
            Mesh m = new Mesh { name = "HorizontalQuad" };
            m.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f)
            };
            m.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            m.RecalculateBounds();
            return m;
        }

        private void Update()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null && box.size != _lastBoxSize)
            {
                RefreshHullBounds();
            }

            float speedKnots = 0f;
            if (_locomotion != null)
            {
                speedKnots = Mathf.Abs(_locomotion.CurrentSpeedKnots);
            }
            else if (_rb != null)
            {
                speedKnots = _rb.linearVelocity.magnitude * 1.94384f;
            }

            float dt = Time.deltaTime;
            float time = Time.time;

            bool isMoving = speedKnots >= _minSpeedKnots;
            if (_portSternTrail != null)
            {
                _portSternTrail.emitting = isMoving;
                Vector3 p = transform.TransformPoint(new Vector3(_portSternTrail.transform.localPosition.x, 0.05f, _sternOffset.z));
                p.y = OceanWaveManager.GetWaveHeight(p.x, p.z, time) + 0.08f;
                _portSternTrail.transform.position = p;
            }
            if (_starboardSternTrail != null)
            {
                _starboardSternTrail.emitting = isMoving;
                Vector3 s = transform.TransformPoint(new Vector3(_starboardSternTrail.transform.localPosition.x, 0.05f, _sternOffset.z));
                s.y = OceanWaveManager.GetWaveHeight(s.x, s.z, time) + 0.08f;
                _starboardSternTrail.transform.position = s;
            }

            // 1. Emit Stern Wake Plaques when moving
            if (isMoving)
            {
                _emitTimer += dt;
                float interval = Mathf.Clamp(_emissionInterval * (6.0f / Mathf.Max(1.0f, speedKnots)), 0.08f, 0.25f);

                if (_emitTimer >= interval)
                {
                    _emitTimer = 0f;
                    EmitSternWake(speedKnots);
                }

                UpdateBowWash(speedKnots, time);
            }
            else
            {
                if (_portBowFoam != null && _portBowFoam.gameObject.activeSelf) _portBowFoam.gameObject.SetActive(false);
                if (_starboardBowFoam != null && _starboardBowFoam.gameObject.activeSelf) _starboardBowFoam.gameObject.SetActive(false);
            }

            // 2. Animate, Expand, and Fade Active Wake Plaques
            for (int i = 0; i < PoolSize; i++)
            {
                if (!_wakePool[i].Active) continue;

                _wakePool[i].Age += dt;
                float t = _wakePool[i].Age / _wakePool[i].MaxLifetime;

                if (t >= 1.0f)
                {
                    _wakePool[i].Active = false;
                    _wakePool[i].Go.SetActive(false);
                    continue;
                }

                // Lateral V-shape expansion: wakes spread outward much wider than they lengthen
                float scaleX = _initialScale + (t * _expansionRate * 2.4f);
                float scaleZ = _initialScale + (t * _expansionRate * 0.9f);
                _wakePool[i].Transform.localScale = new Vector3(scaleX, 1f, scaleZ);

                // Sample exact live ocean wave surface height so foam hugs the moving swells
                Vector3 pos = _wakePool[i].Position;
                float waveY = OceanWaveManager.GetWaveHeight(pos.x, pos.z, time);
                pos.y = waveY + 0.08f;
                _wakePool[i].Transform.position = pos;

                // Fade alpha out smoothly
                float alpha = (1.0f - t) * 0.85f;
                _wakePool[i].PropBlock.SetColor(ColorProp, new Color(0.92f, 0.98f, 1.0f, alpha));
                _wakePool[i].PropBlock.SetColor("_BaseColor", new Color(0.92f, 0.98f, 1.0f, alpha));
                _wakePool[i].Renderer.SetPropertyBlock(_wakePool[i].PropBlock);
            }
        }

        private void EmitSternWake(float speedKnots)
        {
            Vector3 worldSternPos = transform.TransformPoint(_sternOffset);
            float waveY = OceanWaveManager.GetWaveHeight(worldSternPos.x, worldSternPos.z, Time.time);
            worldSternPos.y = waveY + 0.08f;

            _wakePool[_poolIndex].Position = worldSternPos;
            _wakePool[_poolIndex].Forward = transform.forward;
            _wakePool[_poolIndex].Age = 0f;
            _wakePool[_poolIndex].MaxLifetime = _wakeLifetime * Mathf.Clamp(speedKnots / 6.0f, 0.7f, 1.4f);
            _wakePool[_poolIndex].Active = true;

            _wakePool[_poolIndex].Transform.position = worldSternPos;
            _wakePool[_poolIndex].Transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            _wakePool[_poolIndex].Transform.localScale = Vector3.one * _initialScale;

            _wakePool[_poolIndex].PropBlock.SetColor(ColorProp, new Color(0.92f, 0.98f, 1.0f, 0.85f));
            _wakePool[_poolIndex].PropBlock.SetColor("_BaseColor", new Color(0.92f, 0.98f, 1.0f, 0.85f));
            _wakePool[_poolIndex].Renderer.SetPropertyBlock(_wakePool[_poolIndex].PropBlock);

            _wakePool[_poolIndex].Go.SetActive(true);

            _poolIndex = (_poolIndex + 1) % PoolSize;
        }

        private void UpdateBowWash(float speedKnots, float time)
        {
            float speedRatio = Mathf.Clamp01(speedKnots / 10.0f);
            float bowScale = Mathf.Lerp(1.2f, 2.6f, speedRatio);

            if (_portBowFoam != null)
            {
                if (!_portBowFoam.gameObject.activeSelf) _portBowFoam.gameObject.SetActive(true);
                Vector3 pPos = transform.TransformPoint(_portBowOffset);
                pPos.y = OceanWaveManager.GetWaveHeight(pPos.x, pPos.z, time) + 0.05f;
                _portBowFoam.position = pPos;
                _portBowFoam.localScale = new Vector3(bowScale * 0.7f, 1f, bowScale * 1.3f);
            }

            if (_starboardBowFoam != null)
            {
                if (!_starboardBowFoam.gameObject.activeSelf) _starboardBowFoam.gameObject.SetActive(true);
                Vector3 sPos = transform.TransformPoint(_starboardBowOffset);
                sPos.y = OceanWaveManager.GetWaveHeight(sPos.x, sPos.z, time) + 0.05f;
                _starboardBowFoam.position = sPos;
                _starboardBowFoam.localScale = new Vector3(bowScale * 0.7f, 1f, bowScale * 1.3f);
            }
        }
    }
}
