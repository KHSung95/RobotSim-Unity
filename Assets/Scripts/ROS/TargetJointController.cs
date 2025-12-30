using UnityEngine;
using System.Collections.Generic;
using RosSharp.Urdf;
using RobotSim.Utils;
using System.Linq;

namespace RosSharp.RosBridgeClient
{
    [RequireComponent(typeof(TargetJointPublisher))]
    [RequireComponent(typeof(JointJogPublisher))]
    public class TargetJointController : MonoBehaviour
    {
        private TargetJointPublisher publisher;
        private JointJogPublisher jogPublisher;
        
        [Header("Control Settings")]
        public float RotationSpeed = 30.0f; // Degrees per second
        public KeyCode PublishKey = KeyCode.Return;
        public bool AutoPublish = true; 

        [Header("Networking")]
        public float PublishFrequency = 20.0f; // Hz
        private float _lastPublishTime;
        
        [Header("Robot Connection")]
        [Tooltip("Assign the Robot Root GameObject here to visualize movement locally.")]
        public Transform RobotRoot; 
        public TargetPoseController IKTargetController; // Reference to the IK target object
        
        private Transform _tcpTransform;
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
            jogPublisher = GetComponent<JointJogPublisher>();
            
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

                // Cache TCP reference
                _tcpTransform = RobotRoot.FindDeepChild("tool0");
                if (_tcpTransform == null) _tcpTransform = RobotRoot.FindDeepChild("wrist_3_link");

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
            // [Passive Mode] Continuous Sync: Always align internal state with visual feedback
            SyncInternalStateWithActual();
            
            // Sync IK target with current TCP to prevent jumps when switching modes
            SyncIKTargetWithTCP();

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
                }
            }

            if (Input.GetKeyDown(PublishKey))
            {
                publisher.PublishJoints(currentJoints);
            }
        }

        private void SyncIKTargetWithTCP()
        {
            if (_tcpTransform == null && RobotRoot != null)
            {
                _tcpTransform = RobotRoot.FindDeepChild("tool0");
                if (_tcpTransform == null) _tcpTransform = RobotRoot.FindDeepChild("wrist_3_link");
            }

            // Only sync if IK controller is NOT currently enabled/active (to avoid fighting user input)
            if (IKTargetController != null && !IKTargetController.enabled && _tcpTransform != null)
            {
                IKTargetController.transform.position = _tcpTransform.position;
                IKTargetController.transform.rotation = _tcpTransform.rotation;
            }
        }

        private void SyncInternalStateWithActual()
        {
            if (RobotRoot == null) return;

            for (int i = 0; i < publisher.JointNames.Length; i++)
            {
                string name = publisher.JointNames[i];
                if (jointMap.ContainsKey(name))
                {
                    currentJoints[i] = jointMap[name].GetPosition();
                }
            }
        }

        public void MoveJoint(int jointIndex, float direction)
        {
            selectedJointIndex = jointIndex;
            moveAmount = direction * RotationSpeed * Time.deltaTime * Mathf.Deg2Rad;
            ApplyMove();

            if (AutoPublish && publisher != null)
            {
                if (Time.time - _lastPublishTime > (1.0f / PublishFrequency))
                {
                    publisher.PublishJoints(currentJoints);
                    _lastPublishTime = Time.time;
                }
            }
        }

        public void JogJoint(int jointIndex, float direction)
        {
            if (jogPublisher == null) return;
            
            selectedJointIndex = jointIndex;
            float velocity = direction * RotationSpeed * Mathf.Deg2Rad; // rad/s
            
            jogPublisher.PublishJog(jointIndex, velocity);
        }

        public void SetJointConfiguration(double[] newJoints)
        {
            if (newJoints.Length != currentJoints.Length)
            {
                Debug.LogWarning("Joint count mismatch");
                return;
            }

            // Passive Mode: Only update internal command state.
            // Visuals will be updated by ROS feedback (SmoothJointStateSubscriber).
            for (int i = 0; i < publisher.JointNames.Length; i++)
            {
                currentJoints[i] = newJoints[i];
            }
        }

        private void ApplyMove()
        {
             // [Passive Mode] Instead of accumulating internal state, 
             // we read the ACTUAL position of the robot in Unity (updated by Subscriber).
             string name = publisher.JointNames[selectedJointIndex];
             if (jointMap.ContainsKey(name))
             {
                 float actualCurrentPos = (float)jointMap[name].GetPosition();
                 currentJoints[selectedJointIndex] = actualCurrentPos + moveAmount;
                    
                 // Simple wrapping
                 if (currentJoints[selectedJointIndex] > Mathf.PI) currentJoints[selectedJointIndex] -= 2 * Mathf.PI;
                 if (currentJoints[selectedJointIndex] < -Mathf.PI) currentJoints[selectedJointIndex] += 2 * Mathf.PI;
                 
                 // Note: We do NOT call UpdateJointState here. 
                 // We only update the 'currentJoints' buffer used for publishing.
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

                // Rate limited auto-publish for keyboard too
                if (AutoPublish && (Time.time - _lastPublishTime > (1.0f / PublishFrequency)))
                {
                    publisher.PublishJoints(currentJoints);
                    _lastPublishTime = Time.time;
                }

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
