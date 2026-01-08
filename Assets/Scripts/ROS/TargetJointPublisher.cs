using UnityEngine;
using RobotSim.Robot;

using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RobotSim.ROS
{
    public class TargetJointPublisher : UnityPublisher<JointState>
    {
        [Header("Model Reference")]
        public RobotStateProvider StateProvider;

        [Header("ROS Settings")]
        public string FrameId = "base_link";

        // Joint Settings (Automated via RobotStateProvider)
        private string[] JointNames;
        
        // Internal state
        private JointState message;
        public double[] CurrentJointAngles; // In Radians

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/joint_commands";
            
            // Architectural Sync: Get joint names from the Model (RobotStateProvider)
            if (StateProvider == null) StateProvider = GetComponent<RobotStateProvider>();
            if (StateProvider == null) StateProvider = FindObjectOfType<RobotStateProvider>();

            if (StateProvider != null)
            {
                if (StateProvider.JointNames == null) StateProvider.InitializeReferences();
                JointNames = StateProvider.JointNames;
                FrameId = StateProvider.BaseFrameId; 
            }
            else
            {
                Debug.LogError("[TargetJointPublisher] No RobotStateProvider found! Cannot initialize joint names.");
                JointNames = new string[0];
            }

            base.Start();
            InitializeMessage();
        }

        private void InitializeMessage()
        {
            message = new JointState
            {
                header = new Header { frame_id = FrameId },
                name = JointNames,
                position = new double[JointNames.Length],
                velocity = new double[JointNames.Length],
                effort = new double[JointNames.Length]
            };
            
            CurrentJointAngles = new double[JointNames.Length];
        }

        public void PublishJoints(double[] jointAnglesRad)
        {
            if (jointAnglesRad.Length != JointNames.Length)
            {
                Debug.LogError($"Joint angle count mismatch! Expected {JointNames.Length}, got {jointAnglesRad.Length}");
                return;
            }

            // Update internal state
            System.Array.Copy(jointAnglesRad, CurrentJointAngles, jointAnglesRad.Length);

            // Update Message
            message.header.Update();
            // Force timestamp 0 to avoid sync issues
            message.header.stamp = new RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time(0, 0);

            message.position = jointAnglesRad;
            
            Publish(message);
        }
    }
}
