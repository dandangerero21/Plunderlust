using System.Collections.Generic;
using Plunderlust.Core.Data;
using Plunderlust.Ocean;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Loot
{
    /// <summary>
    /// Spawns buoyant LootCrates in the surrounding waters when a ship sinks.
    /// Distributes gold, ship repair timber/canvas, trade commodities, and treasure maps.
    /// </summary>
    public class LootableWreck : MonoBehaviour
    {
        [Header("Loot Generation")]
        [SerializeField] private GameObject _lootCratePrefab;
        [SerializeField] private int _minCrates = 3;
        [SerializeField] private int _maxCrates = 5;
        [SerializeField] private float _scatterRadius = 9.0f;

        [Header("Modular Bundle Drops (Bite-sized so small hulls can collect all)")]
        [SerializeField] private int _timberPerCrateMin = 3;
        [SerializeField] private int _timberPerCrateMax = 5;
        [SerializeField] private int _canvasPerCrateMin = 2;
        [SerializeField] private int _canvasPerCrateMax = 4;
        [SerializeField] private int _goldPerCrateMin = 50;
        [SerializeField] private int _goldPerCrateMax = 90;

        private ShipSubsystemController _subsystems;
        private bool _hasDroppedLoot = false;

        private void Awake()
        {
            _subsystems = GetComponent<ShipSubsystemController>();
            if (_subsystems != null)
            {
                _subsystems.OnShipStartedSinking += HandleShipSinking;
            }
        }

        private void OnDestroy()
        {
            if (_subsystems != null)
            {
                _subsystems.OnShipStartedSinking -= HandleShipSinking;
            }
        }

        private void HandleShipSinking()
        {
            if (_hasDroppedLoot) return;
            _hasDroppedLoot = true;

            SpawnLootFlotsam();
        }

        /// <summary>
        /// Explicitly spawn buoyant flotsam around this wreck (e.g. from static procedural reefs).
        /// </summary>
        public void SpawnFlotsam()
        {
            SpawnLootFlotsam();
        }

        private void SpawnLootFlotsam()
        {
            int crateCount = Random.Range(_minCrates, _maxCrates + 1);
            List<CargoSlotDTO> lootItems = GenerateCargoManifest();

            for (int i = 0; i < crateCount; i++)
            {
                CargoSlotDTO item = i < lootItems.Count ? lootItems[i] : new CargoSlotDTO(CargoItemType.Gold, Random.Range(30, 80));

                // Scatter around the sinking ship
                float angle = (float)i / crateCount * Mathf.PI * 2.0f;
                float radius = Random.Range(_scatterRadius * 0.5f, _scatterRadius);
                Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);

                SpawnCrate(spawnPos, item.itemType, item.quantity);
            }
        }

        private List<CargoSlotDTO> GenerateCargoManifest()
        {
            List<CargoSlotDTO> items = new List<CargoSlotDTO>
            {
                // Bite-sized shipwright bundles: ensures small boats don't get clogged
                new CargoSlotDTO(CargoItemType.Timber, Random.Range(_timberPerCrateMin, _timberPerCrateMax + 1)),
                new CargoSlotDTO(CargoItemType.Canvas, Random.Range(_canvasPerCrateMin, _canvasPerCrateMax + 1)),
                new CargoSlotDTO(CargoItemType.Gold, Random.Range(_goldPerCrateMin, _goldPerCrateMax + 1)),
                new CargoSlotDTO(CargoItemType.Timber, Random.Range(_timberPerCrateMin, _timberPerCrateMax + 1))
            };

            // Chance for bonus trade goods or rare treasure map
            float mapRoll = Random.value;
            if (mapRoll < 0.25f)
            {
                items.Add(new CargoSlotDTO(CargoItemType.TreasureMap, 1));
            }
            else
            {
                CargoItemType[] bonusGoods = { CargoItemType.Sugar, CargoItemType.Rum, CargoItemType.Spices, CargoItemType.Cannonballs };
                CargoItemType picked = bonusGoods[Random.Range(0, bonusGoods.Length)];
                items.Add(new CargoSlotDTO(picked, Random.Range(3, 6)));
            }

            return items;
        }

        private void SpawnCrate(Vector3 worldPos, CargoItemType type, int count)
        {
            // Position crate at the current wave height
            float waveY = OceanWaveManager.GetWaveHeight(worldPos.x, worldPos.z, Time.time);
            worldPos.y = waveY + 0.15f;

            GameObject crateGo;

            if (_lootCratePrefab != null)
            {
                crateGo = Instantiate(_lootCratePrefab, worldPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            else
            {
                // Instantiate Kenney Crate FBX model if available, or clean procedural crate
                GameObject crateModelPrefab = Resources.Load<GameObject>("Models/crate");
#if UNITY_EDITOR
                if (crateModelPrefab == null)
                {
                    crateModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_pirate-kit/Models/FBX format/crate.fbx");
                }
#endif
                crateGo = new GameObject($"LootCrate_{type}");
                crateGo.transform.position = worldPos;
                crateGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                if (crateModelPrefab != null)
                {
                    GameObject model = Instantiate(crateModelPrefab, crateGo.transform);
                    model.name = "CrateModel";
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;

                    Material paletteMat = Resources.Load<Material>("M_KenneyPiratePalette");
#if UNITY_EDITOR
                    if (paletteMat == null)
                    {
                        paletteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                    }
#endif
                    if (paletteMat != null)
                    {
                        Renderer[] r = model.GetComponentsInChildren<Renderer>();
                        foreach (var rend in r) rend.sharedMaterial = paletteMat;
                    }
                }
                else
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(crateGo.transform, false);
                    cube.transform.localScale = Vector3.one * 0.85f;
                    Renderer rend = cube.GetComponent<Renderer>();
                    if (rend != null) rend.material.color = new Color(0.65f, 0.45f, 0.2f);
                    Collider c = cube.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                }

                BoxCollider col = crateGo.AddComponent<BoxCollider>();
                col.size = new Vector3(0.9f, 0.9f, 0.9f);

                Rigidbody rb = crateGo.AddComponent<Rigidbody>();
                rb.mass = 25f;
                rb.linearDamping = 1.5f;
                rb.angularDamping = 2.5f;

                // Pop upward impulse as crate surfaces from the wreck
                rb.AddForce(Vector3.up * Random.Range(2.5f, 4.0f) + Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
            }

            LootCrate crateComp = crateGo.GetComponent<LootCrate>();
            if (crateComp == null)
            {
                crateComp = crateGo.AddComponent<LootCrate>();
            }

            crateComp.Initialize(type, count);
        }
    }
}
