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
        private List<LinkCollisionSensor> collisionSensors = new List<LinkCollisionSensor>();
        
        // State
        private int selectedJointIndex = 0;
        private double[] currentJoints; // Radians
        private double[] previousJoints; // For Rollback
        private float moveAmount = 0; // Added for shared state between MoveJoint and ApplyMove

        public double[] GetCurrentJoints() => currentJoints; 

        private void Start()
        {
            publisher = GetComponent<TargetJointPublisher>();
            
            // Initial state
            currentJoints = new double[publisher.JointNames.Length];
            previousJoints = new double[publisher.JointNames.Length];
            
            RefreshJointReferences();
        }

        [ContextMenu("Refresh References")]
        public void RefreshJointReferences()
        {
            jointMap.Clear();
            collisionSensors.Clear();

            if (RobotRoot != null)
            {
                var joints = RobotRoot.GetComponentsInChildren<UrdfJoint>();
                foreach (var j in joints)
                {
                    if (!string.IsNullOrEmpty(j.JointName) && !jointMap.ContainsKey(j.JointName))
                        jointMap.Add(j.JointName, j);
                }

                // Find all sensors
                collisionSensors.AddRange(RobotRoot.GetComponentsInChildren<LinkCollisionSensor>());
                
                // Initialize state
                for (int i = 0; i < publisher.JointNames.Length; i++)
                {
                    string name = publisher.JointNames[i];
                    if (jointMap.ContainsKey(name))
                    {
                        currentJoints[i] = jointMap[name].GetPosition();
                        previousJoints[i] = currentJoints[i];
                    }
                }
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

        private void Update()
        {
            HandleSelectionInput();
            
            // Store safe state before moving
            System.Array.Copy(currentJoints, previousJoints, currentJoints.Length);

            if (HandleRotationInput())
            {
                // If we moved, check for collision
                if (IsAnyLinkColliding())
                {
                    // Rollback!
                    System.Array.Copy(previousJoints, currentJoints, currentJoints.Length);
                    
                    // Re-apply safe state to physical joints
                    string name = publisher.JointNames[selectedJointIndex];
                    if (jointMap.ContainsKey(name))
                    {
                         // Calculate reverse delta to put it back
                         float rollbackDelta = (float)(previousJoints[selectedJointIndex] - (previousJoints[selectedJointIndex] + (moveAmount))); // This is complex, easier to use absolute set if possible
                         // Simply update with 0 delta to sync or use specific absolute method if Siemens ROS# has one.
                         // Siemens ROS# UpdateJointState is incremental. 
                         jointMap[name].UpdateJointState(-(float)moveAmount); 
                    }
                }
            }

            if (Input.GetKeyDown(PublishKey))
            {
                publisher.PublishJoints(currentJoints);
            }
        }

        public void MoveJoint(int jointIndex, float direction)
        {
            selectedJointIndex = jointIndex;
            moveAmount = direction * RotationSpeed * Time.deltaTime * Mathf.Deg2Rad;
            ApplyMove();
        }

        private void ApplyMove()
        {
             currentJoints[selectedJointIndex] += moveAmount;
                
             // Simple wrapping
             if (currentJoints[selectedJointIndex] > Mathf.PI) currentJoints[selectedJointIndex] -= 2 * Mathf.PI;
             if (currentJoints[selectedJointIndex] < -Mathf.PI) currentJoints[selectedJointIndex] += 2 * Mathf.PI;
                
             // Apply to Visual Robot
             string name = publisher.JointNames[selectedJointIndex];
             if (jointMap.ContainsKey(name))
             {
                 jointMap[name].UpdateJointState((float)moveAmount);
             }
        }

        private bool HandleRotationInput()
        {
            float move = 0f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
                move = 1f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                move = -1f;

            if (move != 0)
            {
                moveAmount = move * RotationSpeed * Time.deltaTime * Mathf.Deg2Rad;
                ApplyMove();
                return true;
            }
            return false;
        }

        private bool IsAnyLinkColliding()
        {
            foreach(var sensor in collisionSensors)
            {
                if (sensor.IsColliding) return true;
            }
            return false;
        }

        private void OnGUI()
        {
            // Simple HUD
            float width = 300;
            float height = 280;
            Rect area = new Rect(10, Screen.height - height - 10, width, height);

            GUI.Box(area, "Target Joint Controller (FK + Collision)");
            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 30, width - 20, height - 40));

            GUILayout.Label($"Selected Joint: {publisher.JointNames[selectedJointIndex]}");
            GUILayout.Label("Controls: 1-6 Select, +/- Rotate, Enter Publish");
            
            bool colliding = IsAnyLinkColliding();
            string status = colliding ? "<color=red>COLLISION DETECTED</color>" : "<color=green>SAFE</color>";
            GUILayout.Label($"Status: {status}");

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
