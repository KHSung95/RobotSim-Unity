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

        // Hardcoded Reset Position (from user request)
        // Unity Degrees: [-90, -120, 120, -180, -90, 180]
        // Radian conversion:
        private readonly double[] ResetPositions = {
            -1.5708, -2.0944, 2.0944, 
            -3.13, -1.5708, 3.13 
        };

        private void Start()
        {
            if (JointPublisher == null) JointPublisher = GetComponent<TargetJointPublisher>();
            if (JointPublisher == null) JointPublisher = GetComponentInParent<TargetJointPublisher>();
            
            if (StateProvider == null) StateProvider = GetComponent<RobotStateProvider>();
            if (StateProvider == null) StateProvider = GetComponentInParent<RobotStateProvider>();
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
