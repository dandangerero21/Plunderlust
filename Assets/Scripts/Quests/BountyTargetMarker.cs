using UnityEngine;
using Plunderlust.AI;
using Plunderlust.Subsystems;

namespace Plunderlust.Quests
{
    /// <summary>
    /// Attached to guaranteed bounty target vessels.
    /// Tracks defeat and reports fulfillment to BountyQuestManager.
    /// Zero heap allocations in update loops.
    /// </summary>
    public class BountyTargetMarker : MonoBehaviour
    {
        public string BountyId { get; private set; }
        public string CaptainName { get; private set; }
        public int GoldReward { get; private set; }

        private bool _isFulfilled = false;
        private AIShipBrain _brain;
        private ShipSubsystemController _subsystems;

        public void Initialize(string bountyId, string captainName, int goldReward)
        {
            BountyId = bountyId;
            CaptainName = captainName;
            GoldReward = goldReward;
            _brain = GetComponent<AIShipBrain>();
            _subsystems = GetComponent<ShipSubsystemController>();
        }

        private void Awake()
        {
            if (_brain == null) _brain = GetComponent<AIShipBrain>();
            if (_subsystems == null) _subsystems = GetComponent<ShipSubsystemController>();
        }

        private void Update()
        {
            if (_isFulfilled) return;

            // Check if ship was plundered, foundered, or is currently sinking
            bool plundered = (_brain != null && _brain.IsPlundered);
            bool sinkingOrDead = (_subsystems != null && (_subsystems.IsDead || _subsystems.IsSinking));

            if (plundered || sinkingOrDead)
            {
                TriggerFulfillment();
            }
        }

        private void OnDestroy()
        {
            if (!_isFulfilled && !string.IsNullOrEmpty(BountyId))
            {
                TriggerFulfillment();
            }
        }

        public void TriggerFulfillment()
        {
            if (_isFulfilled) return;
            _isFulfilled = true;

            if (BountyQuestManager.Instance != null)
            {
                BountyQuestManager.Instance.OnBountyTargetDefeated(BountyId);
            }
        }
    }
}
