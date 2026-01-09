using UnityEngine;
using RosSharp;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Moveit;
using RosSharp.RosBridgeClient.MessageTypes.Shape;
using RosSharp.RosBridgeClient.MessageTypes.Std;

using RosPose = RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose;
using RosPoint = RosSharp.RosBridgeClient.MessageTypes.Geometry.Point;
using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;
using RobotSim.Robot;
using RobotSim.Sensors;

namespace RobotSim.ROS
{
    /// <summary>
    /// Synchronizes the Camera's collision state between World (Bird-Eye) and Robot (Hand-Eye).
    /// Features: Automatic Collider-based Sizing and Startup Scene Cleanup.
    /// </summary>
    public class AttachableCollisionObjectPublisher : MonoBehaviour
    {
        [Header("ROS Settings")]
        public string CameraId = "camera";
        public string WorldFrame = "base_link";
        
        // Topic Names
        public string CollisionObjectTopic = "/collision_object";
        public string AttachedCollisionObjectTopic = "/attached_collision_object";

        [Header("Model References")]
        public RobotStateProvider StateProvider;
        
        [Header("Collision Support")]
        [Tooltip("If null, will try to get from current GameObject")]
        public Collider CollisionCollider;

        [Header("Hand-Eye Settings")]
        public string[] TouchLinks = new string[] { "tool0", "wrist_3_link" };

        private RosConnector _rosConnector;
        
        // Publication IDs for optimized publishing
        private string _coPublicationId;
        private string _acoPublicationId;

        // MoveIt Constants
        private const sbyte ADD = 0;
        private const sbyte REMOVE = 1;

        private void Start()
        {
            _rosConnector = GetComponent<RosConnector>();
            if (_rosConnector == null) _rosConnector = FindFirstObjectByType<RosConnector>();
            if (StateProvider == null) StateProvider = FindFirstObjectByType<RobotStateProvider>();
            if (CollisionCollider == null) CollisionCollider = GetComponent<Collider>();

            // Setup publishers when ready
            SetupPublishers();
        }

        private void SetupPublishers()
        {
            if (_rosConnector == null || _rosConnector.RosSocket == null)
            {
                // If not connected yet, the connector's internal events will handle reconnection, 
                // but we can also trigger this on a delayed check or event if needed.
                return;
            }

            // 1. Advertise topics and store Publication IDs
            _coPublicationId = _rosConnector.RosSocket.Advertise<CollisionObject>(CollisionObjectTopic);
            _acoPublicationId = _rosConnector.RosSocket.Advertise<AttachedCollisionObject>(AttachedCollisionObjectTopic);

            // 2. [Startup Cleanup] Remove any existing objects with this name from Viz/MoveIt
            CleanupExistingOnRos();
        }

        private void CleanupExistingOnRos()
        {
            if (string.IsNullOrEmpty(_coPublicationId)) return;

            // [Global Cleanup] Use special ID 'all' to clear entire PlanningScene
            var clearAllCO = new CollisionObject { id = "all", operation = REMOVE };
            clearAllCO.header = new Header { frame_id = WorldFrame };
            _rosConnector.RosSocket.Publish(_coPublicationId, clearAllCO);

            var clearAllACO = new AttachedCollisionObject
            {
                // Detach all objects from all links
                @object = new CollisionObject { id = "all", operation = REMOVE },
                link_name = "" // Empty link matches all links for 'all' ID
            };
            _rosConnector.RosSocket.Publish(_acoPublicationId, clearAllACO);

            Debug.Log("[CameraCollisionPublisher] Global cleanup sent to ROS (Cleared all objects).");
        }

        public void Synchronize(CameraMountType mode)
        {
            // Re-check IDs in case they failed to initialize at Start
            if (string.IsNullOrEmpty(_coPublicationId) || string.IsNullOrEmpty(_acoPublicationId))
            {
                SetupPublishers();
                if (string.IsNullOrEmpty(_coPublicationId)) return;
            }

            if (mode == CameraMountType.HandEye)
            {
                // [Transition] World(Remove) -> Attached(Add)
                RemoveCollisionObject();
                AddAttachedCollisionObject();
            }
            else
            {
                // [Transition] Attached(Detach) -> World(Add)
                RemoveAttachedCollisionObject();
                AddCollisionObject();
            }
        }

        #region Collision Object (World)
        private void AddCollisionObject()
        {
            var msg = CreateStandardCollisionObject();
            msg.operation = ADD; 
            
            if (StateProvider != null && StateProvider.RobotBase != null)
            {
                Vector3 relPos = StateProvider.RobotBase.InverseTransformPoint(transform.position);
                Quaternion relRot = Quaternion.Inverse(StateProvider.RobotBase.rotation) * transform.rotation;
                
                msg.header.frame_id = WorldFrame;
                msg.primitive_poses[0] = CreateRosPose(relPos.Unity2Ros(), relRot.Unity2Ros());
            }

            _rosConnector.RosSocket.Publish(_coPublicationId, msg);
        }

        private void RemoveCollisionObject()
        {
            var msg = new CollisionObject { id = CameraId, operation = REMOVE };
            msg.header = new Header { frame_id = WorldFrame };
            _rosConnector.RosSocket.Publish(_coPublicationId, msg);
        }

        private CollisionObject CreateStandardCollisionObject()
        {
            return new CollisionObject
            {
                header = new Header { frame_id = WorldFrame, stamp = new RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time(0, 0) },
                id = CameraId,
                primitives = new SolidPrimitive[] { CreatePrimitiveFromCollider() },
                primitive_poses = new RosPose[1]
            };
        }
        #endregion

        #region Attached Collision Object (Robot)
        private void AddAttachedCollisionObject()
        {
            string attachLink = StateProvider != null ? StateProvider.ToolLinkName : "tool0";
            
            var msg = new AttachedCollisionObject
            {
                link_name = attachLink,
                touch_links = TouchLinks,
                @object = CreateStandardCollisionObject()
            };

            if (StateProvider != null && StateProvider.TcpTransform != null)
            {
                Vector3 localPos = StateProvider.TcpTransform.InverseTransformPoint(transform.position);
                Quaternion localRot = Quaternion.Inverse(StateProvider.TcpTransform.rotation) * transform.rotation;
                
                msg.@object.header.frame_id = attachLink;
                msg.@object.primitive_poses[0] = CreateRosPose(localPos.Unity2Ros(), localRot.Unity2Ros());
            }

            _rosConnector.RosSocket.Publish(_acoPublicationId, msg);
        }

        private void RemoveAttachedCollisionObject()
        {
            var msg = new AttachedCollisionObject
            {
                @object = new CollisionObject { id = CameraId, operation = REMOVE },
                link_name = StateProvider != null ? StateProvider.ToolLinkName : "tool0"
            };
            
            _rosConnector.RosSocket.Publish(_acoPublicationId, msg);
        }
        #endregion

        #region Geometry Helpers
        private SolidPrimitive CreatePrimitiveFromCollider()
        {
            SolidPrimitive primitive = new SolidPrimitive();
            Vector3 size = transform.lossyScale;

            if (CollisionCollider != null)
            {
                if (CollisionCollider is BoxCollider box)
                {
                    primitive.type = SolidPrimitive.BOX;
                    size.x *= box.size.x;
                    size.y *= box.size.y;
                    size.z *= box.size.z;
                    // Unity (x,y,z) -> ROS (z,x,y) for Box
                    primitive.dimensions = new double[] { size.z, size.x, size.y };
                }
                else if (CollisionCollider is SphereCollider sphere)
                {
                    primitive.type = SolidPrimitive.SPHERE;
                    float maxScale = Mathf.Max(size.x, size.y, size.z);
                    primitive.dimensions = new double[] { sphere.radius * maxScale };
                }
                else if (CollisionCollider is CapsuleCollider capsule)
                {
                    primitive.type = SolidPrimitive.CYLINDER;
                    float radiusScale = Mathf.Max(size.x, size.z);
                    float height = capsule.height * size.y;
                    float radius = capsule.radius * radiusScale;
                    primitive.dimensions = new double[] { height, radius };
                }
                else
                {
                    // Default to small Box if unsupported collider
                    primitive.type = SolidPrimitive.BOX;
                    primitive.dimensions = new double[] { 0.05, 0.05, 0.1 };
                }
            }
            else
            {
                // Fallback fixed size box
                primitive.type = SolidPrimitive.BOX;
                primitive.dimensions = new double[] { 0.1, 0.05, 0.05 };
            }

            return primitive;
        }

        private RosPose CreateRosPose(Vector3 pos, Quaternion rot)
        {
            return new RosPose
            {
                position = new RosPoint(pos.x, pos.y, pos.z),
                orientation = new RosQuaternion(rot.x, rot.y, rot.z, rot.w)
            };
        }
        #endregion
    }
}
