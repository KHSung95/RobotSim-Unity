using RobotSim.Robot;
using RobotSim.Utils;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Action;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Moveit;
using RosSharp.RosBridgeClient.MessageTypes.Shape;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSim.ROS.Services
{
    public class MovePlanClient : MonoBehaviour
    {
        [Header("ROS Settings")]
        public RosConnector Connector;
        public string ActionName = "/move_group/_action"; // Default MoveGroup action topic base

        [Header("Planning Settings")]
        public string GroupName = "ur_manipulator";
        public string LinkName = "tool0";
        public string ReferenceFrame = "base_link";
        public float PlanningTime = 5.0f;

        private JointTrajectoryPublisher trajectoryPublisher;
        private string publicationId;
        private string subscriptionId;

        // Assuming these message definitions exist as per user instruction
        // We act as a "Publisher" for the Goal and "Subscriber" for the Result
        
        private void Start()
        {
            if (Connector == null) Connector = GetComponent<RosConnector>();
            trajectoryPublisher = GetComponent<JointTrajectoryPublisher>();

            // Architectural Sync: Get planning metadata from the Model (RobotStateProvider)
            var stateProvider = GetComponentInParent<RobotStateProvider>();
            if (stateProvider == null) stateProvider = FindFirstObjectByType<RobotStateProvider>();

            if (stateProvider != null)
            {
                GroupName = stateProvider.MoveGroupName;
                LinkName = stateProvider.FlangeLinkName;
                ReferenceFrame = stateProvider.BaseFrameId;
            }
            
            // Subscribe to Result
            if (Connector != null)
            {
                subscriptionId = Connector.RosSocket.Subscribe<MoveGroupActionResult>(
                    ActionName + "/result", 
                    ResultHandler
                );
                
                // Advertise Goal
                // Topic convention: {ActionName}/goal
                publicationId = Connector.RosSocket.Advertise<MoveGroupActionGoal>(ActionName + "/goal");
            }
        }

        public void PlanAndExecute(UnityEngine.Transform targetTransform)
        {
            if (Connector == null || Connector.RosSocket == null)
            {
                Debug.LogError("[MovePlanClient] ROS Connector not ready.");
                return;
            }

            // 1. Convert Unity Pose to ROS Pose
            var rosPos = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(targetTransform.position.z, -targetTransform.position.x, targetTransform.position.y);
            var relRot = targetTransform.rotation;
            var rosRot = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion(-relRot.z, relRot.x, -relRot.y, relRot.w);

            // 2. Construct constraints
            var goalConstraint = new Constraints
            {
                name = "goal_pose",
                position_constraints = new PositionConstraint[]
                {
                    new PositionConstraint
                    {
                        header = new Header { frame_id = ReferenceFrame },
                        link_name = LinkName,
                        constraint_region = new BoundingVolume
                        {
                            primitive_poses = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose[] { 
                                new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose { 
                                    position = new Point { x = rosPos.x, y = rosPos.y, z = rosPos.z },
                                    orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion { x = 0, y = 0, z = 0, w = 1 }
                                } 
                            },
                             primitives = new SolidPrimitive[] {
                                new SolidPrimitive { type = SolidPrimitive.SPHERE, dimensions = new double[] { 0.001 } } 
                            }
                        },
                        weight = 1.0
                    }
                },
                orientation_constraints = new OrientationConstraint[]
                {
                    new OrientationConstraint
                    {
                        header = new Header { frame_id = ReferenceFrame },
                        link_name = LinkName,
                        orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion { x = rosRot.x, y = rosRot.y, z = rosRot.z, w = rosRot.w },
                        absolute_x_axis_tolerance = 0.01,
                        absolute_y_axis_tolerance = 0.01,
                        absolute_z_axis_tolerance = 0.01,
                        weight = 1.0
                    }
                }
            };
            uint secs = (uint)UnityEngine.Time.time;
            uint nsecs = (uint)((UnityEngine.Time.time - secs) * 1e9);

            // 3. Construct the Action Goal
            var actionGoal = new MoveGroupActionGoal
            {
                header = new Header { frame_id = ReferenceFrame}, // Using standard C# DateTime extension if avail, or manual
                goalInfo = new GoalInfo(),
                args = new MoveGroupGoal
                {
                    request = new MotionPlanRequest
                    {
                        group_name = GroupName,
                        goal_constraints = new Constraints[] { goalConstraint },
                        allowed_planning_time = PlanningTime,
                        max_velocity_scaling_factor = 0.5,
                        max_acceleration_scaling_factor = 0.5,
                        num_planning_attempts = 5
                    },
                    planning_options = new PlanningOptions
                    {
                        plan_only = false, // Execute immediately!
                        replan = true,
                        replan_attempts = 5
                    }
                }
            };
            
            // Fix timestamp manually if extension is missing, assuming RosSharp Time
            actionGoal.header.Update();
            actionGoal.goalInfo.stamp = actionGoal.header.stamp;

            Debug.Log($"[MovePlanClient] Publishing MoveGroup Action Goal to {ActionName}/goal...");
            Connector.RosSocket.Publish(publicationId, actionGoal);
        }

        private void ResultHandler(MoveGroupActionResult result)
        {
            if (result == null) return;
            
            // Check success
            // MoveItErrorCodes: 1 = SUCCESS
            if (result.result) 
            {
                Debug.Log($"[MovePlanClient] Action Succeeded! Planning Time: {result.values.planning_time}. Executing local visualization...");
                
                // The result contains 'planned_trajectory' (RobotTrajectory)
                // We forward this to the local JointTrajectoryPublisher to visualize/move the Unity robot
                if (trajectoryPublisher != null && result.values.planned_trajectory != null)
                {
                   trajectoryPublisher.PublishTrajectory(result.values.planned_trajectory.joint_trajectory);
                }
            }
            else
            {
                Debug.LogWarning("[MovePlanClient] Action Failed");
            }
        }
    }
}
