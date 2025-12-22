using UnityEngine;
using System.Collections.Generic;
using RosSharp.Urdf;
using System.Linq;

namespace RosSharp.RosBridgeClient
{
    [RequireComponent(typeof(TargetJointPublisher))]
    public class TargetJointController : MonoBehaviour
    {
        private TargetJointPublisher publisher;
        
        [Header("Control Settings")]
        public float RotationSpeed = 30.0f; // Degrees per second
        public KeyCode PublishKey = KeyCode.Return;
        
        [Header("Robot Connection")]
        [Tooltip("Assign the Robot Root GameObject here to visualize movement locally.")]
        public Transform RobotRoot; 
        private Dictionary<string, UrdfJoint> jointMap = new Dictionary<string, UrdfJoint>();
        
        // State
        private int selectedJointIndex = 0;
        private double[] currentJoints; // Radians

        private void Start()
        {
            publisher = GetComponent<TargetJointPublisher>();
            
            // Initial state (zeros)
            currentJoints = new double[publisher.JointNames.Length];
            
            // Find UrdfJoints and map them
            if (RobotRoot != null)
            {
                var joints = RobotRoot.GetComponentsInChildren<UrdfJoint>();
                foreach (var j in joints)
                {
                    if (!string.IsNullOrEmpty(j.JointName))
                    {
                        if (!jointMap.ContainsKey(j.JointName))
                            jointMap.Add(j.JointName, j);
                    }
                }
                
                // Sync currentJoints with actual robot state initially
                for (int i = 0; i < publisher.JointNames.Length; i++)
                {
                    string name = publisher.JointNames[i];
                    if (jointMap.ContainsKey(name))
                    {
                        currentJoints[i] = jointMap[name].GetPosition();
                    }
                }
            }
        }

        private void Update()
        {
            HandleSelectionInput();
            HandleRotationInput();

            if (Input.GetKeyDown(PublishKey))
            {
                publisher.PublishJoints(currentJoints);
            }
        }

        private void HandleSelectionInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) selectedJointIndex = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) selectedJointIndex = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) selectedJointIndex = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) selectedJointIndex = 3;
            if (Input.GetKeyDown(KeyCode.Alpha5)) selectedJointIndex = 4;
            if (Input.GetKeyDown(KeyCode.Alpha6)) selectedJointIndex = 5;
            
            selectedJointIndex = Mathf.Clamp(selectedJointIndex, 0, currentJoints.Length - 1);
        }

        private void HandleRotationInput()
        {
            float move = 0f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
                move = 1f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                move = -1f;

            if (move != 0)
            {
                // Update current joint angle locally
                float delta = move * RotationSpeed * Time.deltaTime * Mathf.Deg2Rad;
                currentJoints[selectedJointIndex] += delta;
                
                // Simple wrapping
                if (currentJoints[selectedJointIndex] > Mathf.PI) currentJoints[selectedJointIndex] -= 2 * Mathf.PI;
                if (currentJoints[selectedJointIndex] < -Mathf.PI) currentJoints[selectedJointIndex] += 2 * Mathf.PI;
                
                // Apply to Visual Robot immediately
                string name = publisher.JointNames[selectedJointIndex];
                if (jointMap.ContainsKey(name))
                {
                    jointMap[name].UpdateJointState(delta);
                }
            }
        }

        private void OnGUI()
        {
            // Simple HUD
            float width = 300;
            float height = 250;
            Rect area = new Rect(10, Screen.height - height - 10, width, height);

            GUI.Box(area, "Target Joint Controller (FK)");
            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 30, width - 20, height - 40));

            GUILayout.Label($"Selected Joint: {publisher.JointNames[selectedJointIndex]}");
            GUILayout.Label("Controls: 1-6 Select, +/- Rotate, Enter Publish");
            if(RobotRoot != null) GUILayout.Label("<color=green>Visualizing on Robot</color>");
            else GUILayout.Label("<color=yellow>No Robot Assigned (Data Only)</color>");
            
            GUILayout.Space(10);
            
            for (int i = 0; i < currentJoints.Length; i++)
            {
                string marker = (i == selectedJointIndex) ? ">> " : "   ";
                float deg = (float)currentJoints[i] * Mathf.Rad2Deg;
                GUILayout.Label($"{marker}{publisher.JointNames[i]}: {deg:F1}°");
            }

            GUILayout.EndArea();
        }
    }
}
