using System.Collections.Generic;
using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Physical Island Landmass Barrier & Coastal Repulsion Director.
    /// Provides solid primitive PhysX colliders blocking ships from penetrating island dunes,
    /// cliffs, and settlements on all platforms (including Android APK runtime).
    /// Houses an active coastal shallow buffer trigger that gently pushes hulls seaward
    /// and applies restorative steering torque to steer vessels parallel to shorelines,
    /// while preserving unobstructed deep-water navigation through port dock approaches.
    /// </summary>
    public class IslandLandmassBarrier : MonoBehaviour
    {
        public static readonly List<Vector3> ActiveIslandCenters = new List<Vector3>()
        {
            new Vector3(45f, 0f, 25f),     // PortHaven
            new Vector3(-45f, 0f, 30f),    // Smuggler Cove
            new Vector3(-120f, 0f, 180f),  // Tortuga Haven
            new Vector3(95f, 0f, -85f),    // Treasure Atoll East
            new Vector3(-85f, 0f, -65f)    // Treasure Atoll West
        };

        [Header("Barrier Configuration")]
        [SerializeField] private bool _isPort = true;
        [SerializeField] private float _repulsionForceMultiplier = 12.0f;
        [SerializeField] private float _steerTorqueMultiplier = 14.0f;

        private void OnEnable()
        {
            Vector3 pos = transform.position;
            pos.y = 0f;
            bool found = false;
            for (int i = 0; i < ActiveIslandCenters.Count; i++)
            {
                if ((ActiveIslandCenters[i] - pos).sqrMagnitude < 4.0f)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                ActiveIslandCenters.Add(pos);
            }
        }

        private void OnDisable()
        {
            Vector3 pos = transform.position;
            pos.y = 0f;
            for (int i = ActiveIslandCenters.Count - 1; i >= 0; i--)
            {
                if ((ActiveIslandCenters[i] - pos).sqrMagnitude < 4.0f)
                {
                    ActiveIslandCenters.RemoveAt(i);
                    break;
                }
            }
        }

        public void HandleShallowTriggerStay(Collider other)
        {
            // Only process ship rigidbodies
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            // Only interact with vessels equipped with locomotion
            ShipLocomotionController loco = rb.GetComponent<ShipLocomotionController>();
            if (loco == null) return;

            // Compute local position relative to the port / atoll root
            Vector3 localPos = transform.InverseTransformPoint(rb.position);

            // If this is a port, preserve an open navigational channel in front of the dock
            // (dock extends along +Z between x = -3.8m and +3.8m, z >= 2.6m)
            if (_isPort && localPos.z > 2.6f && Mathf.Abs(localPos.x) < 3.8f)
            {
                return;
            }

            // Calculate horizontal outward direction pointing away from island landmass core
            Vector3 coreLocal = _isPort ? new Vector3(0f, 0f, -1.0f) : Vector3.zero;
            Vector3 coreWorld = transform.TransformPoint(coreLocal);
            Vector3 awayDir = Vector3.ProjectOnPlane(rb.position - coreWorld, Vector3.up);

            if (awayDir.sqrMagnitude < 0.01f)
            {
                awayDir = transform.forward;
                awayDir.y = 0f;
            }
            awayDir.Normalize();

            // 1. Inward velocity cancellation: prevent ships from gliding further into shore
            float inwardSpeed = Vector3.Dot(rb.linearVelocity, -awayDir);
            if (inwardSpeed > 0f)
            {
                rb.linearVelocity -= (-awayDir) * inwardSpeed;
            }

            // 2. Outward coastal current force: push hull back towards deep ocean
            rb.AddForce(awayDir * (rb.mass * _repulsionForceMultiplier), ForceMode.Force);

            // 3. Auto steer-away torque: steer rudder to glance parallel to the coast
            Vector3 shipForward = Vector3.ProjectOnPlane(rb.transform.forward, Vector3.up).normalized;
            float headingDot = Vector3.Dot(shipForward, -awayDir);
            if (headingDot > -0.2f)
            {
                // Positive crossY means ship should steer left/right away from shore
                float steerSign = Mathf.Sign(Vector3.Cross(shipForward, awayDir).y);
                if (Mathf.Abs(steerSign) < 0.1f) steerSign = 1.0f;

                rb.AddTorque(Vector3.up * (steerSign * rb.mass * _steerTorqueMultiplier), ForceMode.Force);
            }
        }

        /// <summary>
        /// Attaches solid primitive colliders and coastal repulsion trigger configured for a port island.
        /// </summary>
        public static IslandLandmassBarrier AttachToPort(Transform portRoot)
        {
            if (portRoot == null) return null;

            Transform existing = portRoot.Find("IslandLandmassBarrier");
            if (existing != null)
            {
                return existing.GetComponent<IslandLandmassBarrier>();
            }

            GameObject barrierGo = new GameObject("IslandLandmassBarrier");
            barrierGo.transform.SetParent(portRoot, false);
            barrierGo.transform.localPosition = Vector3.zero;
            barrierGo.transform.localRotation = Quaternion.identity;
            barrierGo.transform.localScale = Vector3.one;

            IslandLandmassBarrier barrier = barrierGo.AddComponent<IslandLandmassBarrier>();
            barrier._isPort = true;

            // 1. Central Landmass Solid Barrier (dry sand, dunes, settlement)
            // Front face at z = +2.5f leaves dock open (z >= 3.0f)
            BoxCollider centralBox = barrierGo.AddComponent<BoxCollider>();
            centralBox.center = new Vector3(0f, 1.5f, -0.6f);
            centralBox.size = new Vector3(11.0f, 5.0f, 6.2f);
            centralBox.isTrigger = false;

            // 2. Rear Sea-Cliff Solid Barrier
            BoxCollider rearBox = barrierGo.AddComponent<BoxCollider>();
            rearBox.center = new Vector3(0f, 1.8f, -3.5f);
            rearBox.size = new Vector3(12.5f, 6.0f, 5.5f);
            rearBox.isTrigger = false;

            // 3. Port & Starboard Shoreline Wing Barriers
            BoxCollider leftWing = barrierGo.AddComponent<BoxCollider>();
            leftWing.center = new Vector3(-4.8f, 1.2f, -0.5f);
            leftWing.size = new Vector3(4.5f, 4.0f, 6.5f);
            leftWing.isTrigger = false;

            BoxCollider rightWing = barrierGo.AddComponent<BoxCollider>();
            rightWing.center = new Vector3(4.8f, 1.2f, -0.5f);
            rightWing.size = new Vector3(4.5f, 4.0f, 6.5f);
            rightWing.isTrigger = false;

            // 4. Coastal Shallow Repulsion Trigger (buffer zone around landmass)
            GameObject triggerGo = new GameObject("CoastalRepulsionTrigger");
            triggerGo.transform.SetParent(barrierGo.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;
            triggerGo.transform.localRotation = Quaternion.identity;

            BoxCollider shallowCol = triggerGo.AddComponent<BoxCollider>();
            shallowCol.center = new Vector3(0f, 1.0f, -1.5f);
            shallowCol.size = new Vector3(18.0f, 4.0f, 11.0f);
            shallowCol.isTrigger = true;

            var relay = triggerGo.AddComponent<TriggerRelay>();
            relay.TargetBarrier = barrier;

            return barrier;
        }

        /// <summary>
        /// Attaches solid primitive colliders and coastal repulsion trigger configured for a circular treasure atoll.
        /// </summary>
        public static IslandLandmassBarrier AttachToAtoll(Transform atollRoot)
        {
            if (atollRoot == null) return null;

            Transform existing = atollRoot.Find("IslandLandmassBarrier");
            if (existing != null)
            {
                return existing.GetComponent<IslandLandmassBarrier>();
            }

            GameObject barrierGo = new GameObject("IslandLandmassBarrier");
            barrierGo.transform.SetParent(atollRoot, false);
            barrierGo.transform.localPosition = Vector3.zero;
            barrierGo.transform.localRotation = Quaternion.identity;
            barrierGo.transform.localScale = Vector3.one;

            IslandLandmassBarrier barrier = barrierGo.AddComponent<IslandLandmassBarrier>();
            barrier._isPort = false;

            // 1. Central Sandbar Solid Barrier
            BoxCollider atollBox = barrierGo.AddComponent<BoxCollider>();
            atollBox.center = new Vector3(0f, 1.5f, 0f);
            atollBox.size = new Vector3(11.0f, 5.0f, 11.0f);
            atollBox.isTrigger = false;

            // 2. Coastal Shallow Repulsion Trigger
            GameObject triggerGo = new GameObject("CoastalRepulsionTrigger");
            triggerGo.transform.SetParent(barrierGo.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;
            triggerGo.transform.localRotation = Quaternion.identity;

            BoxCollider shallowCol = triggerGo.AddComponent<BoxCollider>();
            shallowCol.center = new Vector3(0f, 1.0f, 0f);
            shallowCol.size = new Vector3(17.0f, 4.0f, 17.0f);
            shallowCol.isTrigger = true;

            var relay = triggerGo.AddComponent<TriggerRelay>();
            relay.TargetBarrier = barrier;

            return barrier;
        }

        public class TriggerRelay : MonoBehaviour
        {
            public IslandLandmassBarrier TargetBarrier;

            private void OnTriggerStay(Collider other)
            {
                if (TargetBarrier != null)
                {
                    TargetBarrier.HandleShallowTriggerStay(other);
                }
            }
        }
    }
}
