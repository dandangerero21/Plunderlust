using System.Collections.Generic;
using UnityEngine;

namespace Plunderlust.Combat
{
    /// <summary>
    /// Zero-GC pre-allocated object pool for naval cannonballs.
    /// Pre-warms instances on startup to eliminate runtime allocations during broadside volleys.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        [Header("Pool Configuration")]
        [SerializeField] private CannonballProjectile _cannonballPrefab;
        [SerializeField] private int _initialPoolSize = 64;
        [SerializeField] private Transform _poolContainer;

        private readonly Queue<CannonballProjectile> _availablePool = new Queue<CannonballProjectile>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PrewarmPool();
        }

        private void PrewarmPool()
        {
            if (_poolContainer == null)
            {
                _poolContainer = new GameObject("Cannonball_Pool_Container").transform;
                _poolContainer.SetParent(transform);
            }

            for (int i = 0; i < _initialPoolSize; i++)
            {
                CannonballProjectile projectile = CreateNewProjectileInstance();
                projectile.gameObject.SetActive(false);
                _availablePool.Enqueue(projectile);
            }
        }

        private CannonballProjectile CreateNewProjectileInstance()
        {
            CannonballProjectile instance;

            if (_cannonballPrefab != null)
            {
                instance = Instantiate(_cannonballPrefab, _poolContainer);
            }
            else
            {
                // Fallback runtime procedural cannonball if no prefab assigned
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Procedural_Cannonball";
                go.transform.SetParent(_poolContainer);
                go.transform.localScale = Vector3.one * 0.35f;

                // Configure standard cannonball dark iron appearance
                Renderer rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = Resources.Load<Material>("M_Cannonball");
                    if (mat != null)
                    {
                        rend.sharedMaterial = mat;
                    }
                    else
                    {
                        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                        if (s != null)
                        {
                            mat = new Material(s);
                            mat.color = new Color(0.12f, 0.12f, 0.13f);
                            rend.material = mat;
                        }
                        else
                        {
                            rend.material.color = new Color(0.12f, 0.12f, 0.13f);
                        }
                    }
                }

                // Remove collider so raycast does not self-intersect
                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                instance = go.AddComponent<CannonballProjectile>();
            }

            instance.Initialize(this);
            return instance;
        }

        /// <summary>
        /// Retrieves an available cannonball from the pool and launches it with specified kinematics.
        /// Zero heap allocations.
        /// </summary>
        public CannonballProjectile SpawnCannonball(Vector3 position, Vector3 initialVelocity, float damage, GameObject owner)
        {
            CannonballProjectile projectile;

            if (_availablePool.Count > 0)
            {
                projectile = _availablePool.Dequeue();
            }
            else
            {
                // Dynamic expansion if initial pool exhausted
                projectile = CreateNewProjectileInstance();
            }

            projectile.Launch(position, initialVelocity, damage, owner);
            return projectile;
        }

        /// <summary>
        /// Returns an inactive cannonball back to the queue.
        /// </summary>
        public void ReturnCannonball(CannonballProjectile projectile)
        {
            if (projectile == null) return;
            projectile.transform.SetParent(_poolContainer);
            _availablePool.Enqueue(projectile);
        }
    }
}
