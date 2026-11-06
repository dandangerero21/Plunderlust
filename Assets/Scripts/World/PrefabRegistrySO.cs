using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Prefab & Asset Registry for the Kenney Pirate Kit & Environmental World Generation.
    /// Provides zero-allocation, cached access to all modular 3D architectural pieces,
    /// props, natural landmarks, and special naval encounters.
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabRegistry", menuName = "Plunderlust/World/Prefab Registry")]
    public class PrefabRegistrySO : ScriptableObject
    {
        private static PrefabRegistrySO _instance;
        public static PrefabRegistrySO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PrefabRegistrySO>("PrefabRegistry");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PrefabRegistrySO");
                        if (guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<PrefabRegistrySO>(path);
                        }
                    }
#endif
                    if (_instance == null)
                    {
                        _instance = CreateInstance<PrefabRegistrySO>();
                        _instance.name = "RuntimePrefabRegistry";
                    }
                }
                return _instance;
            }
        }

        [Header("Shared Materials")]
        [SerializeField] private Material _kenneyPaletteMaterial;
        [SerializeField] private Material _ghostShipSpectralMaterial;

        // Model cache dictionary for fast O(1) retrieval without GC or disk hits
        private readonly Dictionary<string, GameObject> _modelCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        public Material KenneyPaletteMaterial => _kenneyPaletteMaterial;
        public Material GhostShipMaterial => _ghostShipSpectralMaterial;

        private void OnEnable()
        {
            EnsureMaterialsLoaded();
        }

        private void EnsureMaterialsLoaded()
        {
            if (_kenneyPaletteMaterial == null)
            {
                _kenneyPaletteMaterial = Resources.Load<Material>("M_KenneyPiratePalette");
            }
#if UNITY_EDITOR
            if (_kenneyPaletteMaterial == null)
            {
                _kenneyPaletteMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
            }
#endif
            if (_ghostShipSpectralMaterial == null)
            {
                _ghostShipSpectralMaterial = CreateGhostSpectralMaterial();
            }
        }

        private static Material CreateGhostSpectralMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader)
            {
                name = "M_SpectralGhostGreen"
            };

            Color spectralGreen = new Color(0.2f, 1.0f, 0.65f, 0.65f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", spectralGreen);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", spectralGreen);

            // Enable emission for ethereal glow
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.15f, 0.85f, 0.45f) * 1.5f);
            }

            return mat;
        }

        /// <summary>
        /// Retrieves a Kenney FBX model prefab by filename, caching for zero future disk hits.
        /// </summary>
        public GameObject GetModelPrefab(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;

            if (_modelCache.TryGetValue(modelName, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject loaded = null;
#if UNITY_EDITOR
            string fbxPath = $"Assets/kenney_pirate-kit/Models/FBX format/{modelName}.fbx";
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
#endif
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>("Models/" + modelName);
            }
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>(modelName);
            }

            if (loaded != null)
            {
                _modelCache[modelName] = loaded;
            }

            return loaded;
        }

        /// <summary>
        /// Spawns a Kenney Pirate Kit model in the scene with solid mesh collider and shared palette material.
        /// </summary>
        public GameObject Spawn(string modelName, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, Material materialOverride = null, bool addMeshCollider = true)
        {
            GameObject prefab = GetModelPrefab(modelName);
            if (prefab == null)
            {
                Debug.LogWarning($"[PrefabRegistry] Missing model asset for '{modelName}'.");
                return null;
            }

            GameObject inst = Instantiate(prefab, parent);
            inst.name = modelName;
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = localRot;
            inst.transform.localScale = localScale;

            EnsureMaterialsLoaded();
            Material targetMat = materialOverride != null ? materialOverride : _kenneyPaletteMaterial;

            if (targetMat != null)
            {
                Renderer[] rends = inst.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rends.Length; i++)
                {
                    rends[i].sharedMaterial = targetMat;
                }
            }

            if (addMeshCollider)
            {
                MeshFilter[] mfs = inst.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < mfs.Length; i++)
                {
                    if (mfs[i].sharedMesh != null && mfs[i].GetComponent<Collider>() == null)
                    {
                        mfs[i].gameObject.AddComponent<MeshCollider>();
                    }
                }
            }

            return inst;
        }
    }
}
