using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Ship
{
    /// <summary>
    /// Procedural rowing animation controller for rowboats and paddle vessels.
    /// Hinges paddles at the innermost handle pivot on the gunwale and dynamically
    /// drives independent Port and Starboard oar strokes based on forward rowing and differential steering:
    /// - Steering Left (A) rows the Right (Starboard) paddle while the Left rests.
    /// - Steering Right (D) rows the Left (Port) paddle while the Right rests.
    /// - Forward (W) rows both paddles in sync.
    /// - Releasing keys returns paddles to rest and coasts to a stop.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class BoatPaddleAnimator : MonoBehaviour
    {
        [Header("Paddle Transforms")]
        [Tooltip("Transform of the Port (Left) paddle, hinged at its inner handle.")]
        [SerializeField] private Transform _portPaddle;
        [Tooltip("Transform of the Starboard (Right) paddle, hinged at its inner handle.")]
        [SerializeField] private Transform _starboardPaddle;
        [Tooltip("Original combined paddles node (automatically split into Port/Starboard if provided).")]
        [SerializeField] private Transform _paddlesNode;

        [Header("Rowing Stroke Dynamics")]
        [SerializeField] private float _baseStrokeRate = 1.4f;
        [Tooltip("Sweep angle in degrees (aft pull and forward recovery).")]
        [SerializeField] private float _sweepAngle = 28f;
        [Tooltip("Dip angle in degrees (blades dipping into water during power stroke).")]
        [SerializeField] private float _dipAngle = 13f;
        [Tooltip("Feather angle in degrees (blades twisting flat during recovery).")]
        [SerializeField] private float _featherAngle = 8f;
        [SerializeField] private float _returnToRestSpeed = 4.5f;

        private ShipLocomotionController _locomotion;
        private Rigidbody _rb;

        private Quaternion _portBaseRot = Quaternion.identity;
        private Quaternion _starboardBaseRot = Quaternion.identity;

        private float _portPhase = 0f;
        private float _starboardPhase = 0f;
        private bool _isSplit = false;

        private void Awake()
        {
            _locomotion = GetComponent<ShipLocomotionController>() ?? GetComponentInParent<ShipLocomotionController>();
            _rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

            InitializePaddles();
        }

        public void InitializePaddles()
        {
            if (_portPaddle != null && _starboardPaddle != null)
            {
                _portBaseRot = _portPaddle.localRotation;
                _starboardBaseRot = _starboardPaddle.localRotation;
                return;
            }

            // Find combined paddles node if not assigned
            if (_paddlesNode == null)
            {
                Transform[] allChildren = GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    if (child.name.ToLower().Contains("paddle") || child.name.ToLower().Contains("oar"))
                    {
                        _paddlesNode = child;
                        break;
                    }
                }
            }

            if (_paddlesNode != null && !_isSplit)
            {
                MeshFilter mf = _paddlesNode.GetComponent<MeshFilter>();
                MeshRenderer mr = _paddlesNode.GetComponent<MeshRenderer>();
                if (mf != null && mf.sharedMesh != null && mr != null)
                {
                    SplitCombinedMeshIntoHingedPaddles(mf.sharedMesh, mr.sharedMaterial);
                }
            }
        }

        /// <summary>
        /// Splits Kenney's combined paddles mesh into separate Port and Starboard oar GameObjects,
        /// re-centering each mesh so local (0, 0, 0) is the innermost handle hinge on the gunwale.
        /// </summary>
        private void SplitCombinedMeshIntoHingedPaddles(Mesh sourceMesh, Material mat)
        {
            Transform parentModel = _paddlesNode.parent != null ? _paddlesNode.parent : transform;

            // In Kenney boat-row-small, the innermost handle vertices are:
            // Port: (-0.5540, 0.8194, -0.1754)
            // Starboard: (+0.5540, 0.8194, -0.1754)
            Vector3 portHingeLocal = new Vector3(-0.5540f, 0.8194f, -0.1754f);
            Vector3 stbdHingeLocal = new Vector3(0.5540f, 0.8194f, -0.1754f);

            Vector3[] srcVerts = sourceMesh.vertices;
            Vector3[] srcNormals = sourceMesh.normals;
            Vector2[] srcUVs = sourceMesh.uv;
            int[] srcTris = sourceMesh.triangles;

            // Separate Port (X < 0) and Starboard (X > 0)
            BuildOarMesh(srcVerts, srcNormals, srcUVs, srcTris, true, portHingeLocal, out Mesh portMesh);
            BuildOarMesh(srcVerts, srcNormals, srcUVs, srcTris, false, stbdHingeLocal, out Mesh stbdMesh);

            // Create Port Paddle GameObject
            GameObject portGo = new GameObject("Paddle_Port");
            portGo.transform.SetParent(parentModel, false);
            portGo.transform.localPosition = portHingeLocal;
            portGo.transform.localRotation = Quaternion.identity;
            MeshFilter portMf = portGo.AddComponent<MeshFilter>();
            portMf.sharedMesh = portMesh;
            MeshRenderer portMr = portGo.AddComponent<MeshRenderer>();
            portMr.sharedMaterial = mat;

            // Create Starboard Paddle GameObject
            GameObject stbdGo = new GameObject("Paddle_Starboard");
            stbdGo.transform.SetParent(parentModel, false);
            stbdGo.transform.localPosition = stbdHingeLocal;
            stbdGo.transform.localRotation = Quaternion.identity;
            MeshFilter stbdMf = stbdGo.AddComponent<MeshFilter>();
            stbdMf.sharedMesh = stbdMesh;
            MeshRenderer stbdMr = stbdGo.AddComponent<MeshRenderer>();
            stbdMr.sharedMaterial = mat;

            _portPaddle = portGo.transform;
            _starboardPaddle = stbdGo.transform;
            _portBaseRot = Quaternion.identity;
            _starboardBaseRot = Quaternion.identity;

            // Disable original combined paddles renderer
            MeshRenderer combinedMr = _paddlesNode.GetComponent<MeshRenderer>();
            if (combinedMr != null) combinedMr.enabled = false;

            _isSplit = true;
        }

        private void BuildOarMesh(Vector3[] srcVerts, Vector3[] srcNormals, Vector2[] srcUVs, int[] srcTris, bool isPort, Vector3 hingeOffset, out Mesh resultMesh)
        {
            resultMesh = new Mesh();
            resultMesh.name = isPort ? "Mesh_Paddle_Port" : "Mesh_Paddle_Starboard";

            System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();
            int[] remap = new int[srcVerts.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            for (int t = 0; t < srcTris.Length; t += 3)
            {
                int i0 = srcTris[t];
                int i1 = srcTris[t + 1];
                int i2 = srcTris[t + 2];

                bool belongs = isPort ? (srcVerts[i0].x < 0f) : (srcVerts[i0].x > 0f);
                if (belongs)
                {
                    tris.Add(GetOrAddVert(i0, srcVerts, srcNormals, srcUVs, hingeOffset, verts, normals, uvs, remap));
                    tris.Add(GetOrAddVert(i1, srcVerts, srcNormals, srcUVs, hingeOffset, verts, normals, uvs, remap));
                    tris.Add(GetOrAddVert(i2, srcVerts, srcNormals, srcUVs, hingeOffset, verts, normals, uvs, remap));
                }
            }

            resultMesh.SetVertices(verts);
            resultMesh.SetNormals(normals);
            resultMesh.SetUVs(0, uvs);
            resultMesh.SetTriangles(tris, 0);
            resultMesh.RecalculateBounds();
        }

        private int GetOrAddVert(int srcIdx, Vector3[] srcVerts, Vector3[] srcNormals, Vector2[] srcUVs, Vector3 hingeOffset,
            System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Vector2> uvs, int[] remap)
        {
            if (remap[srcIdx] != -1) return remap[srcIdx];

            int newIdx = verts.Count;
            // Shift vertex relative to the hinge pivot
            verts.Add(srcVerts[srcIdx] - hingeOffset);
            normals.Add(srcNormals != null && srcIdx < srcNormals.Length ? srcNormals[srcIdx] : Vector3.up);
            uvs.Add(srcUVs != null && srcIdx < srcUVs.Length ? srcUVs[srcIdx] : Vector2.zero);
            remap[srcIdx] = newIdx;
            return newIdx;
        }

        private void Update()
        {
            if (_portPaddle == null || _starboardPaddle == null) return;

            float dt = Time.deltaTime;

            float throttle = _locomotion != null ? _locomotion.ThrottleInput : 0f;
            float steer = _locomotion != null ? _locomotion.SteerInput : 0f;

            // Differential Oar Power:
            // Forward rowing: both oars pull.
            // Steer Left (A): right paddle rows vigorously; left paddle rests.
            // Steer Right (D): left paddle rows vigorously; right paddle rests.
            float portPower = 0f;
            float stbdPower = 0f;
            float directionSign = 1f;

            if (throttle >= -0.05f)
            {
                // Forward or neutral throttle
                portPower = Mathf.Clamp01(throttle + Mathf.Max(0f, steer));
                stbdPower = Mathf.Clamp01(throttle + Mathf.Max(0f, -steer));
                directionSign = 1f;
            }
            else
            {
                // Reverse back-paddling
                portPower = Mathf.Clamp01(-throttle + Mathf.Max(0f, -steer));
                stbdPower = Mathf.Clamp01(-throttle + Mathf.Max(0f, steer));
                directionSign = -1f;
            }

            // Animate Port (Left) Paddle
            if (portPower > 0.05f)
            {
                _portPhase += dt * _baseStrokeRate * (0.8f + (portPower * 0.7f)) * Mathf.PI * 2f * directionSign;
                EvaluateOarRotation(_portPaddle, _portBaseRot, _portPhase, true, portPower);
            }
            else
            {
                // Smoothly rest left paddle on gunwale
                _portPaddle.localRotation = Quaternion.Slerp(_portPaddle.localRotation, _portBaseRot, _returnToRestSpeed * dt);
            }

            // Animate Starboard (Right) Paddle
            if (stbdPower > 0.05f)
            {
                _starboardPhase += dt * _baseStrokeRate * (0.8f + (stbdPower * 0.7f)) * Mathf.PI * 2f * directionSign;
                EvaluateOarRotation(_starboardPaddle, _starboardBaseRot, _starboardPhase, false, stbdPower);
            }
            else
            {
                // Smoothly rest right paddle on gunwale
                _starboardPaddle.localRotation = Quaternion.Slerp(_starboardPaddle.localRotation, _starboardBaseRot, _returnToRestSpeed * dt);
            }
        }

        private void EvaluateOarRotation(Transform paddle, Quaternion baseRot, float phase, bool isPort, float power)
        {
            // Stroke cycles:
            // Sin(phase): sweep aft (power pull) and forward (recovery)
            // Cos(phase): dip down into water during drive (positive), lift above during recovery (negative)
            float sweep = Mathf.Sin(phase);
            float dip = Mathf.Cos(phase);
            float feather = Mathf.Sin(phase * 2f);

            // Angles for Port oar (extends along -X):
            // - Rotate around Z tilts blade down into water.
            // - Rotate around Y sweeps blade aft (negative Y sweeps -X towards -Z).
            // - Rotate around X feathers blade.
            float sideSign = isPort ? 1f : -1f;

            float pitchAngle = (dip * _dipAngle * power) * sideSign;
            float yawAngle = (sweep * _sweepAngle * power) * sideSign;
            float rollAngle = (feather * _featherAngle * power) * sideSign;

            Quaternion strokeRot = Quaternion.Euler(pitchAngle, yawAngle, rollAngle);
            paddle.localRotation = baseRot * strokeRot;
        }
    }
}
