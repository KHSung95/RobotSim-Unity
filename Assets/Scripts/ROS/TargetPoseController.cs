using UnityEngine;

namespace RosSharp.RosBridgeClient
{
    [RequireComponent(typeof(TargetPosePublisher))]
    public class TargetPoseController : MonoBehaviour
    {
        [Header("Control Settings")]
        public float moveSpeed = 0.5f;
        public float rotateSpeed = 45f;
        public float boostMultiplier = 3f;
        public RobotSim.ROS.Services.MovePlanClient MovePlanClient;

        [Header("Visuals")]
        public bool showGuides = true;

        private enum ControlMode { Translate, Rotate }
        private ControlMode currentMode = ControlMode.Translate;
        private bool useLocalSpace = true;

        private void Start()
        {
            if (MovePlanClient == null) MovePlanClient = FindFirstObjectByType<RobotSim.ROS.Services.MovePlanClient>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            HandleInput();

            // Execute MovePlan on Enter
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (MovePlanClient != null)
                {
                    Debug.Log("[TargetPoseController] Enter pressed. Triggering MoveGroup Plan...");
                    MovePlanClient.PlanAndExecute(this.transform);
                }
                else
                {
                    Debug.LogWarning("[TargetPoseController] MovePlanClient not found. Cannot execute.");
                }
            }
        }

        public void MoveTarget(Vector3 translation, Vector3 rotation)
        {
             float dt = Time.deltaTime;
             
             if (translation != Vector3.zero)
             {
                 if (useLocalSpace)
                    transform.Translate(translation * moveSpeed * dt, Space.Self);
                 else
                    transform.Translate(translation * moveSpeed * dt, Space.World);
             }

             if (rotation != Vector3.zero)
             {
                 if (useLocalSpace)
                    transform.Rotate(rotation * rotateSpeed * dt, Space.Self);
                 else
                    transform.Rotate(rotation * rotateSpeed * dt, Space.World);
             }
        }

        private void HandleInput()
        {
            // Toggle Mode: Tab
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                currentMode = currentMode == ControlMode.Translate ? ControlMode.Rotate : ControlMode.Translate;
            }

            // Toggle Space: G
            if (Input.GetKeyDown(KeyCode.G))
            {
                useLocalSpace = !useLocalSpace;
            }

            float multiplier = Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f;
            
            // Re-map keyboard input to calls
            Vector3 moveDir = Vector3.zero;
            Vector3 rotDir = Vector3.zero;
            
            if (currentMode == ControlMode.Translate)
            {
                if (Input.GetKey(KeyCode.W)) moveDir.z += 1;
                if (Input.GetKey(KeyCode.S)) moveDir.z -= 1;
                if (Input.GetKey(KeyCode.A)) moveDir.x -= 1;
                if (Input.GetKey(KeyCode.D)) moveDir.x += 1;
                if (Input.GetKey(KeyCode.Q)) moveDir.y += 1; 
                if (Input.GetKey(KeyCode.E)) moveDir.y -= 1; 
                MoveTarget(moveDir * multiplier, Vector3.zero);
            }
            else 
            {
                if (Input.GetKey(KeyCode.W)) rotDir.x += 1; 
                if (Input.GetKey(KeyCode.S)) rotDir.x -= 1;
                if (Input.GetKey(KeyCode.A)) rotDir.y -= 1; 
                if (Input.GetKey(KeyCode.D)) rotDir.y += 1;
                if (Input.GetKey(KeyCode.Q)) rotDir.z += 1; 
                if (Input.GetKey(KeyCode.E)) rotDir.z -= 1;
                MoveTarget(Vector3.zero, rotDir * multiplier);
            }
        }

        private void DrawGuides()
        {
            // Simple visual debug for axes
            // Only visible if Gizmos are enabled in Game View, OR in Scene View
            float len = 0.2f; // Shortened for marker scale
            Vector3 origin = transform.position;
            
            // Draw Target Sphere
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent Red
            Gizmos.DrawSphere(origin, 0.05f); // 5cm radius marker
            
            // X - Red (Right)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + transform.right * len);
            // Y - Green (Up)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + transform.up * len);
            // Z - Blue (Forward)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(origin, origin + transform.forward * len);
        }

        // Changed from OnGUI to OnDrawGizmos to work in Scene View specifically
        private void OnDrawGizmos()
        {
            if (showGuides) DrawGuides();
        }

        private void OnGUI()
        {
            if (!showGuides) return;
            
            // Simple UI instructions
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            // Transparent background for readability
            GUI.Box(new Rect(0,0, 300, 150), "");
            GUILayout.Label("<b>Target Control</b>");
            GUILayout.Label($"Mode (Tab): <b>{currentMode}</b>");
            GUILayout.Label($"Space (G): <b>{(useLocalSpace ? "Local" : "Global")}</b>");
            GUILayout.Label("Move: <b>W/S/A/D/Q/E</b>");
            GUILayout.Label("Boost: <b>Shift</b>");
            GUILayout.Label("Execute Move: <b>Enter</b>");
            GUILayout.EndArea();
        }
    }
}
