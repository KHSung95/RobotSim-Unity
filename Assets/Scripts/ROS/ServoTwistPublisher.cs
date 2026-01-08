using UnityEngine;
using RosSharp;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;
using Twist = RosSharp.RosBridgeClient.MessageTypes.Geometry.Twist;
using TwistStamped = RosSharp.RosBridgeClient.MessageTypes.Geometry.TwistStamped;

using RobotSim.Robot;
using RobotSim.Control;

namespace RobotSim.ROS
{
    [RequireComponent(typeof(RosJogAdapter))]
    public class ServoTwistPublisher : UnityPublisher<TwistStamped>
    {
        private string FrameId = "base_link";
        private RobotStateProvider StateProvider;

        protected override void Start()
        {
            base.Start();
        }

        public void SetRobotStateProvider(RobotStateProvider rsp)
        {
            StateProvider = rsp;
            if (StateProvider != null)
            {
                if (StateProvider.JointNames == null) StateProvider.InitializeReferences();
                FrameId = StateProvider.BaseFrameId;
            }
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
