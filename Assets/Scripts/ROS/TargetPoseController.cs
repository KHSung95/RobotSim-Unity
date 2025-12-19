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

        [Header("Visuals")]
        public bool showGuides = true;

        private enum ControlMode { Translate, Rotate }
        private ControlMode currentMode = ControlMode.Translate;
        private bool useLocalSpace = true;

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // Toggle Mode: Tab
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                currentMode = currentMode == ControlMode.Translate ? ControlMode.Rotate : ControlMode.Translate;
                Debug.Log($"Control Mode: {currentMode}");
            }

            // Toggle Space: G
            if (Input.GetKeyDown(KeyCode.G))
            {
                useLocalSpace = !useLocalSpace;
                Debug.Log($"Space: {(useLocalSpace ? "Local" : "Global")}");
            }

            float multiplier = Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f;
            float dt = Time.deltaTime;

            if (currentMode == ControlMode.Translate)
            {
                Vector3 moveDir = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) moveDir.z += 1;
                if (Input.GetKey(KeyCode.S)) moveDir.z -= 1;
                if (Input.GetKey(KeyCode.A)) moveDir.x -= 1;
                if (Input.GetKey(KeyCode.D)) moveDir.x += 1;
                if (Input.GetKey(KeyCode.Q)) moveDir.y += 1; // Up
                if (Input.GetKey(KeyCode.E)) moveDir.y -= 1; // Down

                if (useLocalSpace)
                    transform.Translate(moveDir * moveSpeed * multiplier * dt, Space.Self);
                else
                    transform.Translate(moveDir * moveSpeed * multiplier * dt, Space.World);
            }
            else // Rotate
            {
                Vector3 rotDir = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) rotDir.x += 1; // Pitch
                if (Input.GetKey(KeyCode.S)) rotDir.x -= 1;
                if (Input.GetKey(KeyCode.A)) rotDir.y -= 1; // Yaw
                if (Input.GetKey(KeyCode.D)) rotDir.y += 1;
                if (Input.GetKey(KeyCode.Q)) rotDir.z += 1; // Roll
                if (Input.GetKey(KeyCode.E)) rotDir.z -= 1;

                if (useLocalSpace)
                    transform.Rotate(rotDir * rotateSpeed * multiplier * dt, Space.Self);
                else
                    transform.Rotate(rotDir * rotateSpeed * multiplier * dt, Space.World);
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
            GUILayout.Label("Execute: <b>Enter</b>");
            GUILayout.EndArea();
        }
    }
}
