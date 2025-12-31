using System;
using UnityEngine;
using RobotSim.Robot;
using RosSharp.RosBridgeClient.MessageTypes.Std;
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
            Vector3 currentUnityPos;
            Quaternion currentUnityRot;

            if (ReferenceObject != null)
            {
                currentUnityPos = ReferenceObject.InverseTransformPoint(TargetObject.position);
                currentUnityRot = Quaternion.Inverse(ReferenceObject.rotation) * TargetObject.rotation;
            }
            else
            {
                currentUnityPos = TargetObject.localPosition;
                currentUnityRot = TargetObject.localRotation;
            }

            // 2. Convert to ROS Coordinate System
            // Position: (x, y, z) -> (z, -x, y)
            message.pose.position.x = currentUnityPos.z;
            message.pose.position.y = -currentUnityPos.x;
            message.pose.position.z = currentUnityPos.y;

            // Rotation: (x, y, z, w) -> (-z, x, -y, w)
            message.pose.orientation.x = -currentUnityRot.z;
            message.pose.orientation.y = currentUnityRot.x;
            message.pose.orientation.z = -currentUnityRot.y;
            message.pose.orientation.w = currentUnityRot.w;

            Publish(message);
            
            LastSentPosition = currentUnityPos;

            // Detailed Debug
            string logInfo = $"[TargetPosePublisher] Sent!\n" +
                             $"Unity Local Pos: {currentUnityPos.ToString("F4")}\n" +
                             $"ROS Pos (z,-x,y): ({message.pose.position.x:F4}, {message.pose.position.y:F4}, {message.pose.position.z:F4})";
            Debug.Log(logInfo);
        }

        public Vector3 LastSentPosition { get; private set; }
    }
}
