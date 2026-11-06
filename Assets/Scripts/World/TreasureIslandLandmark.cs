using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Phase 5 Treasure Island Landmark.
    /// Manages an uninhabited tropical sandbar with palm trees and a buried gold chest.
    /// Interacts with TreasureManager when the player holds a Treasure Map.
    /// </summary>
    public class TreasureIslandLandmark : MonoBehaviour
    {
        [Header("Treasure Identity")]
        [SerializeField] private string _islandName = "Skeleton Key Atoll";
        [SerializeField] private int _goldYield = 550;

        private Transform _chestTransform;
        private bool _isExcavated = false;

        public bool IsExcavated => _isExcavated;
        public string IslandName => _islandName;
        public int GoldYield => _goldYield;

        public void MarkExcavated()
        {
            _isExcavated = true;
            if (TreasureManager.Instance != null && _chestTransform != null)
            {
                TreasureManager.Instance.UnregisterTreasureCache(_chestTransform);
            }
        }

        public void InitializeIsland(string name, int gold)
        {
            _islandName = name;
            _goldYield = gold;

            // Locate chest child
            _chestTransform = transform.Find("Chest");
            if (_chestTransform == null) _chestTransform = transform;

            if (TreasureManager.Instance != null)
            {
                TreasureManager.Instance.RegisterTreasureCache(_chestTransform);
            }

            // Ensure solid primitive colliders and coastal repulsion trigger exist
            IslandLandmassBarrier.AttachToAtoll(transform);

            // Ensure physical colliders on sandbars and rocks
            MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        private void Start()
        {
            if (_chestTransform == null) _chestTransform = transform.Find("Chest") ?? transform;
            if (TreasureManager.Instance != null)
            {
                TreasureManager.Instance.RegisterTreasureCache(_chestTransform);
            }
        }

        private void OnDestroy()
        {
            if (TreasureManager.Instance != null && _chestTransform != null)
            {
                TreasureManager.Instance.UnregisterTreasureCache(_chestTransform);
            }
        }

        private void Update()
        {
            // Mobile excavation and landmark interaction is orchestrated by TreasureExcavationDirector
        }
    }
}
