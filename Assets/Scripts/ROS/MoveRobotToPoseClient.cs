using UnityEngine;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

using Transform = UnityEngine.Transform;
using Pose = RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose;
using Point = RosSharp.RosBridgeClient.MessageTypes.Geometry.Point;
using Quaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;

namespace RobotSim.ROS
{
    // Define the custom service messages for ROS# (RosSharp)
    namespace CustomServiceMessages
    {
        public class MoveToPoseRequest : Message

        {
            [JsonIgnore]
            public const string RosMessageName = "custom_services/MoveToPose";

            // matching the .srv definition: target_pose
            public Pose target_pose;

            public MoveToPoseRequest()
            {
                this.target_pose = new Pose();
            }

            public MoveToPoseRequest(Pose target_pose)
            {
                this.target_pose = target_pose;
            }
        }

        public class MoveToPoseResponse : Message
        {
            [JsonIgnore]
            public const string RosMessageName = "custom_services/MoveToPose";

            public bool success;
            public string message;

            public MoveToPoseResponse()
            {
                this.success = false;
                this.message = "";
            }
        }
    }

    public class MoveRobotToPoseClient : MonoBehaviour
    {
        [Header("ROS Settings")]
        public RosConnector Connector;
        [Tooltip("The service name must match the ROS 2 node.")]
        public string ServiceName = "/move_robot_to_pose";

        [Header("Target Settings")]
        [Tooltip("The Unity object representing the goal pose.")]
        public Transform targetTransform;

        [Header("Visual Feedback")]
        public bool showGizmos = true;
        public Color gizmoColor = Color.cyan;
        public float gizmoRadius = 0.1f;

        private void Start()
        {
            if (Connector == null) Connector = GetComponent<RosConnector>();
            if (Connector == null)
            {
                // Fallback: try to find one in the scene
                Connector = FindFirstObjectByType<RosConnector>();
            }
        }

        /// <summary>
        /// Call this method to send the request to ROS 2 via ROS#.
        /// </summary>
        public void SendMoveRequest()
        {
            if (Connector == null || Connector.RosSocket == null)
            {
                Debug.LogError("[MoveRobotToPoseClient] ROS Connector not ready or RosSocket is null.");
                return;
            }
            if (targetTransform == null)
            {
                Debug.LogError("[MoveRobotToPoseClient] Target Transform is not assigned.");
                return;
            }

            // 1. Convert Unity Coordinate System to ROS Coordinate System
            // Using the manual conversion pattern found in MovePlanClient.cs
            // Position: Unity(x,y,z) -> ROS(z, -x, y)
            var rosPos = new Point(
                targetTransform.position.z,
                -targetTransform.position.x,
                targetTransform.position.y
            );

            // Rotation: Unity(x,y,z,w) -> ROS(-z, x, -y, w)
            var relRot = targetTransform.rotation;
            var rosRot = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion(
                -relRot.z,
                relRot.x,
                -relRot.y,
                relRot.w
            );

            // 2. Create the Request Message
            var poseMsg = new Pose
            {
                position = rosPos,
                orientation = rosRot
            };

            var request = new CustomServiceMessages.MoveToPoseRequest(poseMsg);

            // 3. Send Service Request
            Debug.Log($"[MoveRobotToPoseClient] Sending request to {ServiceName}...");

            Connector.RosSocket.CallService<CustomServiceMessages.MoveToPoseRequest, CustomServiceMessages.MoveToPoseResponse>(
                ServiceName,
                OnServiceResponse,
                request
            );
        }

        private void OnServiceResponse(CustomServiceMessages.MoveToPoseResponse response)
        {
            if (response == null)
            {
                Debug.LogError("[MoveRobotToPoseClient] Service call returned null response.");
                return;
            }

            // 4. Handle Response
            if (response.success)
            {
                Debug.Log($"[MoveRobotToPoseClient] Success: {response.message}");
            }
            else
            {
                Debug.LogWarning($"[MoveRobotToPoseClient] Failed: {response.message}");
            }
        }

        private void OnDrawGizmos()
        {
            if (showGizmos && targetTransform != null)
            {
                Gizmos.color = gizmoColor;
                Gizmos.DrawWireSphere(targetTransform.position, gizmoRadius);

                // Draw forward direction
                Gizmos.DrawLine(targetTransform.position, targetTransform.position + targetTransform.forward * 0.3f);
            }
        }
    }
}
