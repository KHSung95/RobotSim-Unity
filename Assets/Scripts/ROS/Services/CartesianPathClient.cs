using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Moveit;

using RosPose = RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose;

namespace RobotSim.ROS.Services
{
    public class CartesianPathClient : MonoBehaviour
    {
        [Header("ROS Settings")]
        public RosConnector Connector;
        public string ServiceName = "/compute_cartesian_path";

        [Header("Cartesian Settings")]
        public string GroupName = "ur_manipulator";
        public string LinkName = "tool0";
        public string ReferenceFrame = "base_link";

        [Header("Path Parameters")]
        public double MaxStep = 0.01;
        public double JumpThreshold = 0.0;
        public bool AvoidCollisions = true;

        private JointTrajectoryPublisher trajectoryPublisher;

        private void Start()
        {
            if (Connector == null) Connector = GetComponent<RosConnector>();
            trajectoryPublisher = GetComponent<JointTrajectoryPublisher>();
        }

        public void CallService(RosPose[] waypoints)
        {
            if (Connector == null || Connector.RosSocket == null)
            {
                Debug.LogError("[CartesianPathClient] ROS Connector not ready.");
                return;
            }

            var request = new GetCartesianPathRequest
            {
                header = new RosSharp.RosBridgeClient.MessageTypes.Std.Header { frame_id = ReferenceFrame },
                group_name = GroupName,
                link_name = LinkName,
                waypoints = waypoints,
                max_step = MaxStep,
                jump_threshold = JumpThreshold,
                avoid_collisions = AvoidCollisions
            };

            Connector.RosSocket.CallService<GetCartesianPathRequest, GetCartesianPathResponse>(
                ServiceName,
                ServiceResponseHandler,
                request
            );
        }

        private void ServiceResponseHandler(GetCartesianPathResponse response)
        {
            if (response == null)
            {
                Debug.LogError("[CartesianPathClient] Service call failed: No response.");
                return;
            }

            Debug.Log($"[CartesianPathClient] Path fraction: {response.fraction * 100:F1}%, Points: {response.solution?.joint_trajectory?.points?.Length}");

            if (response.fraction > 0.9f && response.solution != null)
            {
                ExecutePath(response.solution);
            }
            else
            {
                Debug.LogWarning($"[CartesianPathClient] Incomplete path generated ({response.fraction * 100:F1}%). Execution aborted.");
            }
        }

        private void ExecutePath(RobotTrajectory solution)
        {
            if (trajectoryPublisher == null)
            {
                Debug.LogError("[CartesianPathClient] JointTrajectoryPublisher not found on this GameObject. Cannot execute path.");
                return;
            }

            Debug.Log($"[CartesianPathClient] Sending path ({solution.joint_trajectory.points.Length} pts) to bridge...");
            trajectoryPublisher.PublishTrajectory(solution.joint_trajectory);
        }
    }
}
