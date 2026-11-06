using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plunderlust.World
{
    public enum SeaAnimalType
    {
        Whale,
        Shark,
        Dolphin,
        MantaRay,
        Fish1,
        Fish2,
        Fish3
    }

    /// <summary>
    /// Master 3D Sea Animal Model Registry & Wildlife Instantiator.
    /// Loads, instantiates, materials, and animates all 7 authentic 3D marine models:
    /// 1. Whale (Whale.fbx) - Majestic breaching cetacean with deep navy/ivory two-tone shading.
    /// 2. Shark (Shark.fbx) - Charcoal predatory shark cutting through waves with dorsal fin.
    /// 3. Dolphin (Dolphin.fbx) - Playful bow-riding cetacean leaping across wave crests.
    /// 4. Manta Ray (Manta ray.fbx) - Graceful giant ray gliding through turquoise shallows.
    /// 5. Fish 1 (Fish1.fbx) - Shimmering tropical azure blue tang.
    /// 6. Fish 2 (Fish2.fbx) - Golden canary reef angel fish.
    /// 7. Fish 3 (Fish3.fbx) - Vibrant coral-orange clownfish.
    /// Zero heap allocation during gameplay; cached prefab references.
    /// </summary>
    public static class SeaAnimalModelRegistry
    {
        private static readonly Dictionary<SeaAnimalType, GameObject> _prefabCache = new Dictionary<SeaAnimalType, GameObject>();

        // Material Palette Cache
        private static Material _matWhaleDorsal;
        private static Material _matWhaleBelly;
        private static Material _matSharkFin;
        private static Material _matDolphinSlate;
        private static Material _matDolphinBelly;
        private static Material _matMantaSlate;
        private static Material _matMantaBelly;
        private static Material _matFishAzure;
        private static Material _matFishGold;
        private static Material _matFishCoral;

        private static bool _materialsInitialized = false;

        public static void EnsureMaterials()
        {
            if (_materialsInitialized) return;

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            if (litShader == null) litShader = Shader.Find("Unlit/Color");

            _matWhaleDorsal = CreateLitMaterial("M_WhaleNavy", litShader, new Color(0.10f, 0.15f, 0.24f), 0.55f);
            _matWhaleBelly  = CreateLitMaterial("M_WhaleBelly", litShader, new Color(0.85f, 0.89f, 0.92f), 0.45f);

            _matSharkFin    = CreateLitMaterial("M_SharkPredator", litShader, new Color(0.12f, 0.14f, 0.17f), 0.60f);

            _matDolphinSlate = CreateLitMaterial("M_DolphinSlate", litShader, new Color(0.18f, 0.30f, 0.40f), 0.65f);
            _matDolphinBelly = CreateLitMaterial("M_DolphinBelly", litShader, new Color(0.88f, 0.92f, 0.95f), 0.55f);

            _matMantaSlate   = CreateLitMaterial("M_MantaTeal", litShader, new Color(0.08f, 0.18f, 0.22f), 0.70f);
            _matMantaBelly   = CreateLitMaterial("M_MantaBelly", litShader, new Color(0.82f, 0.90f, 0.92f), 0.50f);

            _matFishAzure    = CreateLitMaterial("M_FishAzure", litShader, new Color(0.15f, 0.65f, 0.95f), 0.75f);
            _matFishGold     = CreateLitMaterial("M_FishGold", litShader, new Color(1.0f, 0.80f, 0.12f), 0.75f);
            _matFishCoral    = CreateLitMaterial("M_FishCoral", litShader, new Color(0.96f, 0.38f, 0.15f), 0.75f);

            _materialsInitialized = true;
        }

        private static Material CreateLitMaterial(string name, Shader shader, Color color, float smoothness)
        {
            Material mat = new Material(shader)
            {
                name = name,
                color = color
            };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        /// <summary>
        /// Retrieves the base FBX prefab for the specified sea animal type.
        /// </summary>
        public static GameObject GetPrefab(SeaAnimalType type)
        {
            if (_prefabCache.TryGetValue(type, out GameObject cached) && cached != null)
            {
                return cached;
            }

            string fbxFileName = GetFileNameForType(type);
            GameObject loaded = null;

            // 1. Load from runtime Resources (Assets/Resources/SeaAnimals/)
            loaded = Resources.Load<GameObject>($"SeaAnimals/{fbxFileName}");

#if UNITY_EDITOR
            // 2. Editor Fallbacks
            if (loaded == null)
            {
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/SeaAnimals/{fbxFileName}.fbx");
            }
            if (loaded == null)
            {
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/sea animals/FBX/{fbxFileName}.fbx");
            }
#endif

            if (loaded != null)
            {
                _prefabCache[type] = loaded;
            }
            else
            {
                Debug.LogWarning($"[SeaAnimalModelRegistry] Could not locate FBX asset for '{fbxFileName}'.");
            }

            return loaded;
        }

        private static string GetFileNameForType(SeaAnimalType type)
        {
            switch (type)
            {
                case SeaAnimalType.Whale: return "Whale";
                case SeaAnimalType.Shark: return "Shark";
                case SeaAnimalType.Dolphin: return "Dolphin";
                case SeaAnimalType.MantaRay: return "Manta ray";
                case SeaAnimalType.Fish1: return "Fish1";
                case SeaAnimalType.Fish2: return "Fish2";
                case SeaAnimalType.Fish3: return "Fish3";
                default: return "Fish1";
            }
        }

        /// <summary>
        /// Instantiates, configures, materials, and adds lifelike swimming animation to a 3D sea animal model.
        /// </summary>
        public static GameObject SpawnModel(SeaAnimalType type, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            EnsureMaterials();

            GameObject prefab = GetPrefab(type);
            if (prefab == null) return null;

            GameObject inst = UnityEngine.Object.Instantiate(prefab, parent);
            inst.name = $"{type}_Model";
            inst.transform.localPosition = localPos;

            // ponytail: FBX imported with +Z forward, no extra rotation needed
            Quaternion modelBaseRot = Quaternion.identity;
            inst.transform.localRotation = localRot * modelBaseRot;
            inst.transform.localScale = localScale;

            // Strip any colliders from the animal model so they never block player movement or raycasts
            Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                UnityEngine.Object.Destroy(cols[i]);
            }

            // Apply appropriate materials
            ApplyMaterials(inst, type);

            // Configure animation playback
            ConfigureAnimation(inst, type);

            return inst;
        }

        private static void ApplyMaterials(GameObject inst, SeaAnimalType type)
        {
            Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                r.shadowCastingMode = (type == SeaAnimalType.Whale || type == SeaAnimalType.Shark)
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;

                int matCount = r.sharedMaterials.Length;
                Material[] targetMats = new Material[matCount];

                for (int m = 0; m < matCount; m++)
                {
                    targetMats[m] = GetMaterialForTypeSlot(type, m);
                }

                r.sharedMaterials = targetMats;
            }
        }

        private static Material GetMaterialForTypeSlot(SeaAnimalType type, int slotIndex)
        {
            switch (type)
            {
                case SeaAnimalType.Whale:
                    return slotIndex == 1 ? _matWhaleBelly : _matWhaleDorsal;
                case SeaAnimalType.Shark:
                    return _matSharkFin;
                case SeaAnimalType.Dolphin:
                    return slotIndex == 1 ? _matDolphinBelly : _matDolphinSlate;
                case SeaAnimalType.MantaRay:
                    return slotIndex == 1 ? _matMantaBelly : _matMantaSlate;
                case SeaAnimalType.Fish1:
                    return _matFishAzure;
                case SeaAnimalType.Fish2:
                    return _matFishGold;
                case SeaAnimalType.Fish3:
                    return _matFishCoral;
                default:
                    return _matFishAzure;
            }
        }

        private static void ConfigureAnimation(GameObject inst, SeaAnimalType type)
        {
            // 1. Play Legacy/FBX Animation component if available
            Animation anim = inst.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                anim.wrapMode = WrapMode.Loop;
                anim.Play();
            }

            // 2. Attach Ambient Hydrodynamic Motion script for smooth lifelike undulation
            AmbientSeaAnimalSwimmer swimmer = inst.AddComponent<AmbientSeaAnimalSwimmer>();
            swimmer.Initialize(type);
        }
    }

    /// <summary>
    /// Adds natural hydrodynamic swimming undulation, tail oscillation, and wing fluttering
    /// to 3D sea animal models, applied organically on top of base model orientation.
    /// </summary>
    public class AmbientSeaAnimalSwimmer : MonoBehaviour
    {
        private SeaAnimalType _type;
        private float _phaseOffset;
        private float _frequency;
        private float _tailYawAmplitude;
        private float _wingPitchAmplitude;
        private Quaternion _baseLocalRotation;

        public void Initialize(SeaAnimalType type)
        {
            _type = type;
            _baseLocalRotation = transform.localRotation;
            _phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            switch (type)
            {
                case SeaAnimalType.Whale:
                    _frequency = 1.4f;
                    _tailYawAmplitude = 2.0f;
                    _wingPitchAmplitude = 2.5f;
                    break;
                case SeaAnimalType.Shark:
                    _frequency = 4.2f;
                    _tailYawAmplitude = 8.5f;
                    _wingPitchAmplitude = 1.5f;
                    break;
                case SeaAnimalType.Dolphin:
                    _frequency = 5.2f;
                    _tailYawAmplitude = 3.0f;
                    _wingPitchAmplitude = 4.5f;
                    break;
                case SeaAnimalType.MantaRay:
                    _frequency = 2.2f;
                    _tailYawAmplitude = 1.5f;
                    _wingPitchAmplitude = 8.0f;
                    break;
                default: // Fish 1, 2, 3
                    _frequency = 6.8f;
                    _tailYawAmplitude = 10.0f;
                    _wingPitchAmplitude = 3.0f;
                    break;
            }
        }

        private void Update()
        {
            float t = Time.time * _frequency + _phaseOffset;

            if (_type == SeaAnimalType.MantaRay)
            {
                // Wing undulation / hydrodynamic bank
                float roll = Mathf.Sin(t) * _wingPitchAmplitude;
                transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, roll);
            }
            else if (_type == SeaAnimalType.Whale || _type == SeaAnimalType.Dolphin)
            {
                // Cetacean dorso-ventral fluke undulation (Pitch)
                float flukeWave = Mathf.Sin(t) * _wingPitchAmplitude;
                transform.localRotation = _baseLocalRotation * Quaternion.Euler(flukeWave, 0f, 0f);
            }
            else
            {
                // Shark and Tropical Fish lateral caudal swimming flutter (Yaw)
                float yaw = Mathf.Sin(t) * _tailYawAmplitude;
                transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
