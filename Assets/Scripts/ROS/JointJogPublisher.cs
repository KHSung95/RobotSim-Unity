using UnityEngine;
using RobotSim.Robot;

using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.MessageTypes.Control;

namespace RobotSim.ROS
{
    // Using JointJogRos2 instead of the standard JointJog
    public class JointJogPublisher : UnityPublisher<JointJogRos2>
    {
        [Header("ROS Settings")]
        public string FrameId = "base_link";

        // Joint Settings (Automated via RobotStateProvider)
        private string[] JointNames;
        
        private JointJogRos2 message;

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/unity/joint_jog";
            
            // Architectural Sync: Get joint names from the Model (RobotStateProvider)
            var stateProvider = GetComponent<RobotStateProvider>();
            if (stateProvider != null)
            {
                if (stateProvider.JointNames == null) stateProvider.InitializeReferences();
                JointNames = stateProvider.JointNames;
                FrameId = stateProvider.BaseFrameId;
            }
            base.Start();
            InitializeMessage();
        }

        private void InitializeMessage()
        {
            message = new JointJogRos2
            {
                header = new Header { frame_id = FrameId },
                joint_names = JointNames,
                velocities = new double[JointNames.Length],
                displacements = new double[0], // Optional, leaving empty
                duration = 0
            };
        }

        public void PublishJog(int jointIndex, float velocity)
        {
            if (JointNames == null || JointNames.Length == 0)
            {
                Debug.LogError("[JointJogPublisher] JointNames is empty! Cannot publish jog.");
                return;
            }

            if (jointIndex < 0 || jointIndex >= JointNames.Length)
            {
                Debug.LogWarning($"[JointJogPublisher] Invalid joint index: {jointIndex}");
                return;
            }

            // Create a message containing ONLY the single joint we are moving
            var msg = new JointJogRos2
            {
                header = new Header { frame_id = FrameId },
                joint_names = new string[] { JointNames[jointIndex] },
                velocities = new double[] { (double)velocity },
                displacements = new double[0],
                duration = 0
            };

            msg.header.Update();
            // Populating proper ROS timestamp
            uint secs = (uint)UnityEngine.Time.time;
            uint nsecs = (uint)((UnityEngine.Time.time - secs) * 1e9);
            msg.header.stamp = new RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time((int)secs, nsecs);

            // Debug log to verify Unity contents
            if (Time.frameCount % 30 == 0) // Log approx once per second
            {
                 Debug.Log($"[JointJogPublisher] Publishing: {msg.joint_names[0]} at velocity {msg.velocities[0]}");
            }

            Publish(msg);
        }
    }
}
