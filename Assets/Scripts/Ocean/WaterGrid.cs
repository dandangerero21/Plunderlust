using UnityEngine;

namespace Plunderlust.Ocean
{
    /// <summary>
    /// Generates a clean, mobile-optimized low-poly Caribbean ocean surface.
    /// Vertex displacement is calculated in real-time on GPU matching analytical Gerstner wave mathematics,
    /// providing a calm, beautiful tropical sea with animated crystalline caustics, multi-tone color gradient,
    /// and rhythmic shoreline surf.
    /// Operates with zero per-frame CPU allocations.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterGrid : MonoBehaviour
    {
        [Header("Grid Geometry")]
        [Tooltip("Resolution along each axis. 100 gives crisp low-poly wave curvature.")]
        [SerializeField] private int _gridResolution = 100;
        [Tooltip("Total span of dynamic wave surface in meters. 2800m provides an endless open ocean horizon.")]
        [SerializeField] private float _gridSize = 2800.0f;
        [SerializeField] private Transform _followTarget;

        private Mesh _mesh;
        private Vector3[] _baseVertices;
        private MeshRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            EnsureMaterial();
            EnsureOceanDirectors();
        }

        private void Start()
        {
            FindFollowTarget();
            GenerateGrid();
            EnsureShipWakes();
        }

        private void FindFollowTarget()
        {
            if (_followTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _followTarget = player.transform;
            }
        }

        private void EnsureMaterial()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) return;

            if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader == null || !_renderer.sharedMaterial.shader.name.Contains("StylizedOcean"))
            {
                Material oceanMat = Resources.Load<Material>("M_LowPolyOcean");
#if UNITY_EDITOR
                if (oceanMat == null)
                {
                    oceanMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_LowPolyOcean.mat");
                }
#endif
                if (oceanMat != null)
                {
                    _renderer.sharedMaterial = oceanMat;
                }
            }

            // Fallback runtime safety check: ensure the shader is StylizedOcean
            if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader == null || !_renderer.sharedMaterial.shader.name.Contains("StylizedOcean"))
            {
                Shader s = Shader.Find("Plunderlust/Ocean/StylizedOcean");
                if (s != null)
                {
                    if (_renderer.sharedMaterial != null)
                    {
                        _renderer.sharedMaterial.shader = s;
                    }
                    else
                    {
                        _renderer.sharedMaterial = new Material(s) { name = "M_RuntimeStylizedOcean" };
                    }
                }
            }
        }

        private void EnsureOceanDirectors()
        {
            // Automatically ensure OceanFloraAndFaunaDirector is present to spawn dolphins, flying fish, flotsam & sandbars
            if (FindAnyObjectByType<Plunderlust.World.OceanFloraAndFaunaDirector>() == null)
            {
                GameObject directorGo = new GameObject("OceanFloraAndFaunaDirector");
                directorGo.AddComponent<Plunderlust.World.OceanFloraAndFaunaDirector>();
            }
        }

        private void EnsureShipWakes()
        {
            // Hook ShipWakeVFX to player ship
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.GetComponent<ShipWakeVFX>() == null)
            {
                player.AddComponent<ShipWakeVFX>();
            }

            // Hook ShipWakeVFX to all existing AI ships in the scene
            var aiShips = FindObjectsByType<Plunderlust.AI.AIShipBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < aiShips.Length; i++)
            {
                if (aiShips[i] != null && aiShips[i].GetComponent<ShipWakeVFX>() == null)
                {
                    aiShips[i].gameObject.AddComponent<ShipWakeVFX>();
                }
            }
        }

        private void GenerateGrid()
        {
            _mesh = new Mesh();
            _mesh.name = "DynamicOceanMesh";

            int vertCount = (_gridResolution + 1) * (_gridResolution + 1);
            _baseVertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            float halfSize = _gridSize * 0.5f;

            int vertIndex = 0;
            for (int z = 0; z <= _gridResolution; z++)
            {
                // ponytail: non-linear geometric spacing gives dense wave detail near ship (~3-5m) and vast horizon coverage out to 1400m
                float v = (float)z / _gridResolution;
                float signedV = (v - 0.5f) * 2.0f;
                float mappedV = Mathf.Sign(signedV) * Mathf.Pow(Mathf.Abs(signedV), 1.6f);
                float posZ = mappedV * halfSize;

                for (int x = 0; x <= _gridResolution; x++)
                {
                    float u = (float)x / _gridResolution;
                    float signedU = (u - 0.5f) * 2.0f;
                    float mappedU = Mathf.Sign(signedU) * Mathf.Pow(Mathf.Abs(signedU), 1.6f);
                    float posX = mappedU * halfSize;

                    _baseVertices[vertIndex] = new Vector3(posX, 0f, posZ);
                    normals[vertIndex] = Vector3.up;
                    uvs[vertIndex] = new Vector2(u, v);
                    vertIndex++;
                }
            }

            int triIndex = 0;
            int[] triangles = new int[_gridResolution * _gridResolution * 6];

            for (int z = 0; z < _gridResolution; z++)
            {
                for (int x = 0; x < _gridResolution; x++)
                {
                    int row1 = z * (_gridResolution + 1) + x;
                    int row2 = (z + 1) * (_gridResolution + 1) + x;

                    triangles[triIndex++] = row1;
                    triangles[triIndex++] = row2;
                    triangles[triIndex++] = row1 + 1;

                    triangles[triIndex++] = row1 + 1;
                    triangles[triIndex++] = row2;
                    triangles[triIndex++] = row2 + 1;
                }
            }

            _mesh.vertices = _baseVertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            // Expand bounds so GPU vertex displacement never causes frustum culling flicker
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(_gridSize * 1.5f, 60.0f, _gridSize * 1.5f));

            GetComponent<MeshFilter>().mesh = _mesh;
        }

        private void Update()
        {
            if (_mesh == null || _baseVertices == null) return;
            if (_followTarget == null) FindFollowTarget();

            Vector3 centerPos = _followTarget != null ? _followTarget.position : Vector3.zero;

            // Gently snap to increments to prevent wave texture crawling while following the ship
            float snapStep = 2.5f;
            float snappedX = Mathf.Floor(centerPos.x / snapStep) * snapStep;
            float snappedZ = Mathf.Floor(centerPos.z / snapStep) * snapStep;
            transform.position = new Vector3(snappedX, 0f, snappedZ);
        }
    }
}