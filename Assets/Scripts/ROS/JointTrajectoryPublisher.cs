using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Trajectory;

namespace RosSharp.RosBridgeClient
{
    public class JointTrajectoryPublisher : UnityPublisher<JointTrajectory>
    {
        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/unity/joint_trajectory";
            base.Start();
        }

        public void PublishTrajectory(JointTrajectory trajectory)
        {
            // Unity side doesn't need to worry about the stamp anymore, 
            // the bridge (v2.3) will inject it.
            Publish(trajectory);
        }
    }
}
