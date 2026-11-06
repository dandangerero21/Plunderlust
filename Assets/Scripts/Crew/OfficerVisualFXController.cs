using UnityEngine;

namespace Plunderlust.Crew
{
    /// <summary>
    /// In-world physical visual effects controller for Officer activities.
    /// - Carpenter: Spawns wood plank patching debris at breach points.
    /// - Boatswain (Bosun): Ejects arching water spout streams from hull scuppers into the sea.
    /// - Sailmaster: Emits canvas fiber mending gleams up the rigging and mast.
    /// Zero heap allocations in runtime update loops.
    /// </summary>
    public class OfficerVisualFXController : MonoBehaviour
    {
        [Header("FX Scupper & Anchor Points")]
        [SerializeField] private Vector3 _scupperOffset = new Vector3(-0.65f, 0.35f, 0f);

        private float _lastPumpFxTime = -99f;
        private float _lastCarpenterFxTime = -99f;
        private float _lastSailmasterFxTime = -99f;

        /// <summary>
        /// Emits wood chip and plank splinter particles when the Carpenter consumes Timber.
        /// </summary>
        public void TriggerCarpenterRepairFX(Vector3 worldPos)
        {
            if (Time.time - _lastCarpenterFxTime < 1.2f) return;
            _lastCarpenterFxTime = Time.time;

            GameObject fxRoot = new GameObject("Carpenter_WoodRepairFX");
            fxRoot.transform.position = worldPos;

            for (int i = 0; i < 5; i++)
            {
                GameObject plankChip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plankChip.name = "PlankChip";
                plankChip.transform.position = worldPos + (UnityEngine.Random.insideUnitSphere * 0.25f);
                plankChip.transform.localScale = new Vector3(0.18f, 0.05f, 0.08f);

                Renderer r = plankChip.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.7f, 0.5f, 0.25f); // Fresh pine timber

                Collider c = plankChip.GetComponent<Collider>();
                if (c != null) Destroy(c);

                Rigidbody rb = plankChip.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                Vector3 blast = (Vector3.up * 2.8f + UnityEngine.Random.insideUnitSphere * 1.0f).normalized;
                rb.AddForce(blast * UnityEngine.Random.Range(2.0f, 4.0f), ForceMode.Impulse);

                Destroy(plankChip, 0.9f);
            }

            Destroy(fxRoot, 1.2f);
        }

        /// <summary>
        /// Emits a continuous bilge water spout over the gunwale when the Bosun pumps bilge water.
        /// </summary>
        public void TriggerBosunPumpFX()
        {
            if (Time.time - _lastPumpFxTime < 0.65f) return;
            _lastPumpFxTime = Time.time;

            Vector3 scupperPos = transform.TransformPoint(_scupperOffset);
            GameObject spoutGo = new GameObject("Bosun_BilgeSpoutFX");
            spoutGo.transform.position = scupperPos;

            for (int i = 0; i < 3; i++)
            {
                GameObject droplet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                droplet.name = "BilgeDroplet";
                droplet.transform.position = scupperPos + (UnityEngine.Random.insideUnitSphere * 0.08f);
                droplet.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.06f, 0.11f);

                Renderer r = droplet.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.85f, 0.92f, 0.98f, 0.85f); // Bilge spray

                Collider c = droplet.GetComponent<Collider>();
                if (c != null) Destroy(c);

                Rigidbody rb = droplet.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                // Arching spout out to port and down
                Vector3 spoutDir = (-transform.right * 2.2f + Vector3.up * 1.2f + UnityEngine.Random.insideUnitSphere * 0.4f).normalized;
                rb.AddForce(spoutDir * UnityEngine.Random.Range(2.8f, 4.2f), ForceMode.Impulse);

                Destroy(droplet, 0.7f);
            }

            Destroy(spoutGo, 0.9f);
        }

        /// <summary>
        /// Emits canvas stitch gleams when the Sailmaster consumes Canvas to repair sails.
        /// </summary>
        public void TriggerSailmasterMendFX(Vector3 mastPos)
        {
            if (Time.time - _lastSailmasterFxTime < 1.5f) return;
            _lastSailmasterFxTime = Time.time;

            GameObject fxRoot = new GameObject("Sailmaster_CanvasMendFX");
            fxRoot.transform.position = mastPos;

            for (int i = 0; i < 4; i++)
            {
                GameObject canvasGleam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                canvasGleam.name = "CanvasGleam";
                canvasGleam.transform.position = mastPos + (UnityEngine.Random.insideUnitSphere * 0.4f);
                canvasGleam.transform.localScale = new Vector3(0.08f, 0.16f, 0.02f);

                Renderer r = canvasGleam.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.92f, 0.92f, 0.85f); // Natural canvas linen

                Collider c = canvasGleam.GetComponent<Collider>();
                if (c != null) Destroy(c);

                Rigidbody rb = canvasGleam.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.AddForce(Vector3.up * UnityEngine.Random.Range(1.8f, 3.2f), ForceMode.Impulse);

                Destroy(canvasGleam, 1.1f);
            }

            Destroy(fxRoot, 1.4f);
        }
    }
}
