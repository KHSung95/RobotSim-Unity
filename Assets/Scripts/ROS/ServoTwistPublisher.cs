using UnityEngine;

using RosSharp;
using RosSharp.RosBridgeClient;

using TwistStamped = RosSharp.RosBridgeClient.MessageTypes.Geometry.TwistStamped;
using Twist = RosSharp.RosBridgeClient.MessageTypes.Geometry.Twist;
using RosVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;
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

        public void PublishCommand(Vector3 linear, Vector3 angular)
        {
            Vector3 rosLin = linear.Unity2Ros();
            Vector3 rosAng = angular.Unity2Ros();

            var msg = new TwistStamped
            {
                header = new Header { frame_id = FrameId },
                twist = new Twist
                {
                    linear = new RosVector3(rosLin.x, rosLin.y, rosLin.z),
                    angular = new RosVector3(rosAng.x, rosAng.y, rosAng.z)
                }
            };
            
            // Populating proper ROS timestamp
            // Note: If using ClockPublisher, this matches the /clock topic
            msg.header.Update();

            Publish(msg);
        }
    }
}
