using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RosSharp.RosBridgeClient
{
    public class TargetJointPublisher : UnityPublisher<JointState>
    {
        [Header("ROS Settings")]
        public string FrameId = "base_link";

        [Header("Joint Settings")]
        public string[] JointNames = { "shoulder_pan_joint", "shoulder_lift_joint", "elbow_joint", "wrist_1_joint", "wrist_2_joint", "wrist_3_joint" };
        
        // Internal state
        private JointState message;
        public double[] CurrentJointAngles; // In Radians

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/joint_commands";
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
