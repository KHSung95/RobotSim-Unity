using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RobotSim.ROS
{
    public class ServoTwistPublisher : UnityPublisher<TwistStamped>
    {
        [Header("Servo Configuration")]
        public string FrameId = "base_link";

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/servo_node/delta_twist_cmds_unity";
            base.Start();
        }

        public void PublishCommand(UnityEngine.Vector3 linear, UnityEngine.Vector3 angular)
        {
            // Unity (Left-Handed, Y-up) -> ROS (Right-Handed, Z-up)
            // ROS X (Forward) = Unity Z (Forward)
            // ROS Y (Left)    = Unity -X (Left)
            // ROS Z (Up)      = Unity Y (Up)
            
            var msg = new TwistStamped
            {
                header = new Header { frame_id = FrameId },
                twist = new Twist
                {
                    linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3 { x = linear.z, y = -linear.x, z = linear.y },
                    angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3 { x = angular.z, y = -angular.x, z = angular.y } 
                }
            };
            
            // Populating proper ROS timestamp
            // Note: If using ClockPublisher, this matches the /clock topic
            uint secs = (uint)UnityEngine.Time.time;
            uint nsecs = (uint)((UnityEngine.Time.time - secs) * 1e9);
            msg.header.stamp = new RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time((int)secs, nsecs);

            Publish(msg);
        }
    }
}
