using UnityEngine;
using RobotSim.Robot;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosPoint = RosSharp.RosBridgeClient.MessageTypes.Geometry.Point;
using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;
using PoseStamped = RosSharp.RosBridgeClient.MessageTypes.Geometry.PoseStamped;

namespace RosSharp.RosBridgeClient
{
    public class TargetPosePublisher : UnityPublisher<PoseStamped>
    {
        [Tooltip("The Unity Object to track and publish as Target Pose")]
        public Transform TargetObject;
        
        [Tooltip("Optional: Frame of reference (e.g. Robot Base). If null, uses local transform of TargetObject.")]
        public Transform ReferenceObject;

        [Tooltip("Frame ID for the ROS Message, usually base_link or world")]
        public string FrameId = "base_link";

        private PoseStamped message;

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/unity/target_pose";
            
            // Architectural Sync: Get frame and reference from the Model (RobotStateProvider)
            var stateProvider = GetComponentInParent<RobotStateProvider>();
            if (stateProvider == null) stateProvider = FindFirstObjectByType<RobotStateProvider>();
            
            if (stateProvider != null)
            {
                if (ReferenceObject == null) ReferenceObject = stateProvider.RobotBase;
                FrameId = stateProvider.BaseFrameId;
            }

            base.Start();
            InitializeMessage();
        }

        private void InitializeMessage()
        {
            message = new PoseStamped
            {
                header = new Header { frame_id = FrameId }
            };
        }



        [Header("Manual Trigger")]
        [Tooltip("Hot key to publish the target pose (e.g. Space). If None, use external controller.")]
        public KeyCode PublishKey = KeyCode.Return;

        private void Update()
        {
            // Update Header (Sequence and Time) always to keep it fresh
            message.header.Update();
            
            if (Input.GetKeyDown(PublishKey))
            {
                PublishCurrentPose();
            }
        }

        public void PublishCurrentPose()
        {
            if (TargetObject == null) return;

            // 1. Calculate current Unity Pose relative to Reference
            Vector3 relUnityPos;
            Quaternion relUnityRot;

            if (ReferenceObject != null)
            {
                relUnityPos = ReferenceObject.InverseTransformPoint(TargetObject.position);
                relUnityRot = Quaternion.Inverse(ReferenceObject.rotation) * TargetObject.rotation;
            }
            else
            {
                relUnityPos = TargetObject.localPosition;
                relUnityRot = TargetObject.localRotation;
            }

            // 2. Convert to ROS Coordinate System using ROS# standard extensions
            Vector3 rosPos = relUnityPos.Unity2Ros();
            Quaternion rosRot = relUnityRot.Unity2Ros();

            message.pose.position = new RosPoint(rosPos.x, rosPos.y, rosPos.z);
            message.pose.orientation = new RosQuaternion(rosRot.x, rosRot.y, rosRot.z, rosRot.w);

            Publish(message);
            Debug.Log($"[TargetPosePublisher] Published relative pose to {Topic} using Unity2Ros extensions.");
        }
    }
}
