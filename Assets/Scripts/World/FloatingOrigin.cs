using System;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Phase 5 Floating Origin Coordinator.
    /// Eliminates 32-bit floating-point precision degradation, mesh tearing, and physics jitter
    /// by periodically recentering the active world hierarchy around the player coordinate system.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class FloatingOrigin : MonoBehaviour
    {
        public static FloatingOrigin Instance { get; private set; }

        [Header("Floating Origin Configuration")]
        [Tooltip("Distance from coordinate origin (0, 0, 0) in meters before triggering a recentering shift.")]
        [SerializeField] private float _shiftThreshold = 750.0f;

        private Transform _playerTransform;
        private Vector3 _accumulatedWorldOffset = Vector3.zero;

        public static event Action<Vector3> OnOriginShifted; // (offset vector)
        public Vector3 AccumulatedOffset => _accumulatedWorldOffset;

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
            FindPlayerTransform();
        }

        private void FindPlayerTransform()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
        }

        private void LateUpdate()
        {
            if (_playerTransform == null)
            {
                FindPlayerTransform();
                if (_playerTransform == null) return;
            }

            Vector3 pos = _playerTransform.position;
            if (Mathf.Abs(pos.x) > _shiftThreshold || Mathf.Abs(pos.z) > _shiftThreshold)
            {
                ExecuteOriginShift(new Vector3(pos.x, 0f, pos.z));
            }
        }

        public void ExecuteOriginShift(Vector3 offset)
        {
            _accumulatedWorldOffset += offset;

            // 1. Shift all root scene GameObjects
            GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject root = rootObjects[i];
                if (root == null) continue;
                root.transform.position -= offset;
            }

            // 2. Adjust all physics Rigidbodies directly to maintain PhysX stability
            Rigidbody[] allBodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allBodies.Length; i++)
            {
                Rigidbody rb = allBodies[i];
                if (rb != null)
                {
                    rb.position -= offset;
                }
            }

            Debug.Log($"<color=cyan>[FloatingOrigin]</color> Shifted world coordinate origin by {offset}. Total Offset: {_accumulatedWorldOffset}");
            OnOriginShifted?.Invoke(offset);
        }
    }
}
