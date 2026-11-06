using Plunderlust.Ocean;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Combat
{
    /// <summary>
    /// Zero-allocation ballistic projectile for cannonballs.
    /// Simulates gravity trajectory, detects ocean wave water impacts via OceanWaveManager,
    /// and queries ShipSubsystemController for localized subsystem damage.
    /// Managed by ProjectilePool.
    /// </summary>
    public class CannonballProjectile : MonoBehaviour
    {
        [Header("Ballistic Properties")]
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _damage = 75f;
        [SerializeField] private float _maxLifetime = 5.0f;
        [SerializeField] private float _projectileRadius = 0.35f;
        [SerializeField] private LayerMask _collisionMask = ~0;

        private ProjectilePool _pool;
        private Vector3 _velocity;
        private Vector3 _currentPosition;
        private float _age = 0f;
        private bool _isActive = false;
        private GameObject _ownerShip;

        // Pre-allocated non-alloc raycast buffer (zero GC)
        private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[8];

        public void Initialize(ProjectilePool pool)
        {
            _pool = pool;
        }

        public void Launch(Vector3 spawnPos, Vector3 initialVelocity, float damage, GameObject owner)
        {
            _currentPosition = spawnPos;
            transform.position = spawnPos;
            _velocity = initialVelocity;
            _damage = damage;
            _ownerShip = owner;
            _age = 0f;
            _isActive = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isActive) return;

            float dt = Time.deltaTime;
            _age += dt;

            if (_age >= _maxLifetime)
            {
                Despawn();
                return;
            }

            // 1. Advance Ballistic Velocity & Position
            _velocity.y += _gravity * dt;
            Vector3 nextPosition = _currentPosition + (_velocity * dt);
            Vector3 travelDir = nextPosition - _currentPosition;
            float travelDist = travelDir.magnitude;

            // 2. Thick SphereCast collision against ship colliders
            if (travelDist > 0.001f)
            {
                int hitCount = Physics.SphereCastNonAlloc(_currentPosition, _projectileRadius, travelDir.normalized, s_HitBuffer, travelDist, _collisionMask, QueryTriggerInteraction.Ignore);
                
                // Fallback direct Raycast if spherecast grazed or missed
                if (hitCount == 0)
                {
                    hitCount = Physics.RaycastNonAlloc(_currentPosition, travelDir.normalized, s_HitBuffer, travelDist + _projectileRadius, _collisionMask, QueryTriggerInteraction.Ignore);
                }

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = s_HitBuffer[i];
                    if (hit.collider == null) continue;

                    // Avoid self-collision with launching ship under any circumstance
                    if (_ownerShip != null)
                    {
                        if (hit.collider.gameObject == _ownerShip ||
                            hit.collider.transform.IsChildOf(_ownerShip.transform) ||
                            hit.collider.transform.root == _ownerShip.transform.root)
                        {
                            continue;
                        }

                        ShipSubsystemController hitSub = hit.collider.GetComponentInParent<ShipSubsystemController>();
                        ShipSubsystemController ownerSub = _ownerShip.GetComponent<ShipSubsystemController>() ?? _ownerShip.GetComponentInParent<ShipSubsystemController>();
                        if (hitSub != null && ownerSub != null && hitSub == ownerSub)
                        {
                            continue;
                        }

                        // Ignore any hit on the owner or its root during initial launch clearance
                        if (_age < 0.20f && Vector3.Distance(_currentPosition, _ownerShip.transform.position) < 10.0f)
                        {
                            if (hit.collider.transform.root == _ownerShip.transform.root) continue;
                        }
                    }

                    Vector3 contactPoint = hit.point != Vector3.zero ? hit.point : hit.collider.ClosestPoint(_currentPosition);

                    // Check for ShipSubsystemController on target or parent
                    ShipSubsystemController subsystems = hit.collider.GetComponentInParent<ShipSubsystemController>();
                    if (subsystems != null)
                    {
                        var hitSystem = subsystems.ApplyLocalizedDamage(contactPoint, _damage, _velocity, _ownerShip);
                        Debug.Log($"<color=red><b>[CANNONBALL HIT!]</b></color> Struck <b>{hit.collider.transform.root.name}</b> on <b>{hitSystem}</b> for {_damage} DMG! (Hull HP: {subsystems.HullHealth:F0}, Bilge: {subsystems.FloodingRatio * 100:F0}%)");

                        // Apply physical kinetic impact impulse to the struck ship
                        Rigidbody targetRb = hit.collider.GetComponentInParent<Rigidbody>();
                        if (targetRb != null)
                        {
                            targetRb.AddForceAtPosition(_velocity.normalized * (_damage * 45f), contactPoint, ForceMode.Impulse);
                        }
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>[CANNONBALL IMPACT]</color> Hit terrain or non-ship object: {hit.collider.name}");
                    }

                    Despawn();
                    return;
                }
            }

            _currentPosition = nextPosition;
            transform.position = _currentPosition;

            // 3. Water Surface Detection: grace period ensures muzzle clearance
            if (_age > 0.08f)
            {
                float waveHeight = OceanWaveManager.GetWaveHeight(_currentPosition.x, _currentPosition.z, Time.time);
                if (_currentPosition.y <= waveHeight)
                {
                    SpawnOceanSplash(_currentPosition);
                    Despawn();
                }
            }
        }

        private void SpawnOceanSplash(Vector3 pos)
        {
            GameObject splashGo = new GameObject("WaterSplashFX");
            splashGo.transform.position = pos;

            for (int i = 0; i < 5; i++)
            {
                GameObject droplet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                droplet.name = "Droplet";
                droplet.transform.position = pos + (UnityEngine.Random.insideUnitSphere * 0.15f);
                droplet.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.08f, 0.14f);

                Renderer r = droplet.GetComponent<Renderer>();
                if (r != null)
                {
                    Material splashMat = Resources.Load<Material>("M_WaterSplash");
                    if (splashMat != null)
                    {
                        r.sharedMaterial = splashMat;
                    }
                    else
                    {
                        r.material.color = new Color(0.85f, 0.95f, 1.0f, 0.9f); // Ocean foam white
                    }
                }

                Collider col = droplet.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Rigidbody rb = droplet.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                Vector3 fountainDir = (Vector3.up * 2.5f + UnityEngine.Random.insideUnitSphere).normalized;
                rb.AddForce(fountainDir * UnityEngine.Random.Range(2.5f, 5.0f), ForceMode.Impulse);

                Destroy(droplet, UnityEngine.Random.Range(0.6f, 1.0f));
            }

            Destroy(splashGo, 1.2f);
        }

        private void Despawn()
        {
            _isActive = false;
            gameObject.SetActive(false);
            if (_pool != null)
            {
                _pool.ReturnCannonball(this);
            }
        }
    }
}
