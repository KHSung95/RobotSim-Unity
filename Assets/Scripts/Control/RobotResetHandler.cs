using UnityEngine;
using RobotSim.ROS;
using RobotSim.Robot;

namespace RobotSim.Control
{
    /// <summary>
    /// Handles robot resetting to a specific joint configuration when 'R' is pressed.
    /// Requirements: 
    /// 1. Robot must be "selected" (currently simplified to any 'R' press while script is active).
    /// 2. Publishes to the same topic as TargetJointPublisher.
    /// </summary>
    public class RobotResetHandler : MonoBehaviour
    {
        [Header("References")]
        public TargetJointPublisher JointPublisher;
        public RobotStateProvider StateProvider;

        [Header("Settings")]
        public KeyCode ResetKey = KeyCode.R;

        // Hardcoded Reset Position (from user request)
        // Unity Degrees: [-90.2, -120.2, 144.7, -20.0, 91.0, 0.8]
        private readonly double[] ResetPositions = {
            -1.5743, -2.0979, 2.5255, 
            -0.3491, 1.5882, 0.0140 
        };

        private void Start()
        {
            if (JointPublisher == null) JointPublisher = GetComponent<TargetJointPublisher>();
            if (JointPublisher == null) JointPublisher = GetComponentInParent<TargetJointPublisher>();
            
            if (StateProvider == null) StateProvider = GetComponent<RobotStateProvider>();
            if (StateProvider == null) StateProvider = GetComponentInParent<RobotStateProvider>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(ResetKey))
            {
                TriggerReset();
            }
        }

        public void TriggerReset()
        {
            if (JointPublisher == null)
            {
                Debug.LogError("[RobotResetHandler] TargetJointPublisher not found! Cannot reset.");
                return;
            }

            Debug.Log("[RobotResetHandler] Triggering Reset to Homing Position...");
            
            // Note: We assume the joint order in ResetPositions matches the JointNames discovered by StateProvider.
            // Typical UR joint order: shoulder_pan, shoulder_lift, elbow, wrist_1, wrist_2, wrist_3.
            JointPublisher.PublishJoints(ResetPositions);
        }
    }
}
