using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Relays PhysX trigger stay events from island coastal buffer colliders
    /// back to the parent IslandLandmassBarrier director.
    /// Standalone first-class MonoBehaviour for robust Unity serialization.
    /// </summary>
    public class CoastalTriggerRelay : MonoBehaviour
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
