using System;
using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.UI;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Phase 5 Treasure Map & Sunken Cache Discovery Engine.
    /// Tracks procedural treasure islands across the infinite world, guides the player via
    /// salvaged Treasure Maps, and awards massive gold chests, rare commodities, and relics upon excavation.
    /// </summary>
    public class TreasureManager : MonoBehaviour
    {
        public static TreasureManager Instance { get; private set; }

        private readonly List<Transform> _activeTreasureCaches = new List<Transform>();
        private GameObject _playerShip;
        private ShipCargoHold _playerCargo;

        public static event Action<Vector3, int> OnTreasureDiscovered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            FindPlayerShip();
        }

        private void FindPlayerShip()
        {
            _playerShip = GameObject.FindGameObjectWithTag("Player");
            if (_playerShip != null)
            {
                _playerCargo = _playerShip.GetComponent<ShipCargoHold>();
            }
        }

        public void RegisterTreasureCache(Transform cacheTransform)
        {
            if (cacheTransform != null && !_activeTreasureCaches.Contains(cacheTransform))
            {
                _activeTreasureCaches.Add(cacheTransform);
            }
        }

        public void UnregisterTreasureCache(Transform cacheTransform)
        {
            _activeTreasureCaches.Remove(cacheTransform);
        }

        public int ActiveTreasureCount => _activeTreasureCaches.Count;

        public bool HasTreasureMap()
        {
            if (_playerCargo == null) FindPlayerShip();
            return _playerCargo != null && _playerCargo.GetItemCount(CargoItemType.TreasureMap) > 0;
        }

        public bool GetNearestTreasureLocation(out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            if (_playerShip == null) FindPlayerShip();
            if (_playerShip == null || _activeTreasureCaches.Count == 0) return false;

            Vector3 playerPos = _playerShip.transform.position;
            Transform nearest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < _activeTreasureCaches.Count; i++)
            {
                Transform cache = _activeTreasureCaches[i];
                if (cache == null) continue;

                float sqrDist = (cache.position - playerPos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = cache;
                }
            }

            if (nearest == null) return false;
            worldPos = nearest.position;
            return true;
        }

        public bool GetNearestTreasureBearing(out float distanceMeters, out float bearingDegrees)
        {
            distanceMeters = 0f;
            bearingDegrees = 0f;

            if (_playerShip == null) FindPlayerShip();
            if (_playerShip == null || _activeTreasureCaches.Count == 0) return false;

            Vector3 playerPos = _playerShip.transform.position;
            Transform nearest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < _activeTreasureCaches.Count; i++)
            {
                Transform cache = _activeTreasureCaches[i];
                if (cache == null) continue;

                float sqrDist = (cache.position - playerPos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = cache;
                }
            }

            if (nearest == null) return false;

            distanceMeters = Mathf.Sqrt(minSqrDist);
            Vector3 dir = (nearest.position - playerPos).normalized;
            bearingDegrees = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (bearingDegrees < 0f) bearingDegrees += 360f;

            return true;
        }

        public bool DigUpTreasureCache(GameObject cacheObject, int goldYield)
        {
            if (_playerCargo == null) FindPlayerShip();
            if (_playerCargo == null || cacheObject == null) return false;

            if (!HasTreasureMap())
            {
                Debug.LogWarning("[TreasureManager] You need a deciphered Treasure Map to open this cache!");
                return false;
            }

            // Consume 1 Treasure Map
            _playerCargo.ConsumeCargo(CargoItemType.TreasureMap, 1);

            // Award Gold Jackpot
            _playerCargo.AddCargo(CargoItemType.Gold, goldYield, out _);

            // Bonus Goods
            _playerCargo.AddCargo(CargoItemType.Sugar, UnityEngine.Random.Range(4, 9), out _);
            _playerCargo.AddCargo(CargoItemType.Spices, UnityEngine.Random.Range(3, 7), out _);
            _playerCargo.AddCargo(CargoItemType.Rum, UnityEngine.Random.Range(4, 8), out _);

            if (PirateCombatHUD.Instance != null)
            {
                PirateCombatHUD.Instance.ShowBanner($"🏴‍☠️ BURIED TREASURE UNCOVERED: +{goldYield} GOLD!", new Color(1f, 0.85f, 0.2f), 4.5f);
            }

            OnTreasureDiscovered?.Invoke(cacheObject.transform.position, goldYield);
            UnregisterTreasureCache(cacheObject.transform);

            // Destroy excavated chest or open it
            Destroy(cacheObject);
            return true;
        }
    }
}
