using UnityEngine;
using RosSharp.Urdf;
using RosSharp.RosBridgeClient;
using System.Linq;

public class RosConnectionMonitor : MonoBehaviour
{
    [Header("Dependencies")]
    public TargetPosePublisher targetPublisher;
    // Optional: Assign the robot root to find joints automatically
    public Transform robotRoot;

    private UrdfJoint[] joints;

    private void Start()
    {
        if (targetPublisher == null)
            targetPublisher = FindObjectOfType<TargetPosePublisher>();

        if (robotRoot != null)
        {
            // Find all UrdfJoint components which are used by ROS# to manage joints
            joints = robotRoot.GetComponentsInChildren<UrdfJoint>();
        }
    }

    private void OnGUI()
    {
        // Define a nice box area on the top right
        float width = 250;
        float height = 300;
        float margin = 10;
        Rect area = new Rect(Screen.width - width - margin, margin, width, height);

        GUI.Box(area, "ROS Connection Monitor");
        GUILayout.BeginArea(new Rect(area.x + 10, area.y + 30, width - 20, height - 40));

        GUILayout.Space(10);

        GUILayout.Label("<b>Current Robot Joints (Deg)</b>");
        if (joints != null && joints.Length > 0)
        {
            foreach (var joint in joints)
            {
                string name = joint.JointName;
                // GetPosition() returns the current state (rad for revolute, meters for prismatic)
                float val = (float)joint.GetPosition() * Mathf.Rad2Deg;
                GUILayout.Label($"{name}: {val:F1}°");
            }
        }
        else
        {
            GUILayout.Label("No UrdfJoints found on RobotRoot");
        }

        GUILayout.EndArea();
    }
}
