using UnityEngine;
using Newtonsoft.Json;
using RobotSim.Utils;

using RosSharp;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

using Pose = RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose;
using RosPoint = RosSharp.RosBridgeClient.MessageTypes.Geometry.Point;
using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;

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
                Connector = FindFirstObjectByType<RosConnector>();
            }
        }

        public void SendMoveRequest(System.Action<bool> onComplete = null)
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

            // 1. Convert Unity Pose to ROS Pose using ROS# standard extensions
            // This performs (z, -x, y) for position and (-z, x, -y, w) for rotation
            Vector3 rosPos = targetTransform.position.Unity2Ros();
            Quaternion rosRot = targetTransform.rotation.Unity2Ros();

            var poseMsg = new Pose
            {
                position = new RosPoint(rosPos.x, rosPos.y, rosPos.z),
                orientation = new RosQuaternion(rosRot.x, rosRot.y, rosRot.z, rosRot.w)
            };

            var request = new CustomServiceMessages.MoveToPoseRequest(poseMsg);

            Debug.Log($"[MoveRobotToPoseClient] Sending request to {ServiceName}...");

            Connector.RosSocket.CallService<CustomServiceMessages.MoveToPoseRequest, CustomServiceMessages.MoveToPoseResponse>(
                ServiceName,
                (response) => {
                    bool success = response != null && response.success;
                    OnServiceResponse(response);
                    if (onComplete != null) UnityMainThreadDispatcher.Instance().Enqueue(() => onComplete(success));
                },
                request
            );
        }

        private void OnServiceResponse(CustomServiceMessages.MoveToPoseResponse response)
        {
            if (response == null) return;
            if (response.success)
                Debug.Log($"[MoveRobotToPoseClient] Success: {response.message}");
            else
                Debug.LogWarning($"[MoveRobotToPoseClient] Failed: {response.message}");
        }
    }
}
