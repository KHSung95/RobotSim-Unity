using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Moveit;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Shape;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RosSharp.RosBridgeClient
{
    public class ToolPublisher : UnityPublisher<AttachedCollisionObject>
    {
        [Header("Tool Settings")]
        [Tooltip("Unique ID for the tool object")]
        public string ToolId = "gripper";
        
        [Tooltip("The robot link name to attach this tool to (e.g. tool0, wrist_3_link)")]
        public string AttachLinkName = "tool0";
        
        [Tooltip("List of robot links that are allowed to touch this tool without collision error")]
        public string[] TouchLinks = new string[] { "wrist_3_link", "tool0" };

        [Header("Geometry")]
        [Tooltip("The Reference Link (usually same as AttachLinkName) for relative pose.")]
        public UnityEngine.Transform AttachLinkTransform; // Should correspond to AttachLinkName
        
        public enum ShapeType { Box, Sphere, Cylinder, Capsule }
        public ShapeType shapeType = ShapeType.Box;

        private AttachedCollisionObject message;

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/attached_collision_object";
            base.Start();
            
            if (string.IsNullOrEmpty(ToolId))
                ToolId = gameObject.name;

            InitializeMessage();
            PublishTool(0); // ADD
        }

        private void OnDestroy()
        {
            try
            {
                if (Application.isPlaying) 
                    PublishTool(1); // REMOVE
            }
            catch {}
        }

        private void InitializeMessage()
        {
            message = new AttachedCollisionObject
            {
                link_name = AttachLinkName,
                touch_links = TouchLinks,
                @object = new CollisionObject
                {
                    header = new Header { frame_id = AttachLinkName }, // Attached objects usually strictly relative to link
                    id = ToolId,
                    operation = 0 // ADD
                }
            };

            // Initialize Lists
            message.@object.primitives = new SolidPrimitive[1];
            message.@object.primitive_poses = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose[1];
        }

        public void PublishTool(sbyte operation)
        {
            // Update Header
            // Attached Object: header frame_id should be the link we attach to
            message.@object.header.frame_id = AttachLinkName;
            
            // Set timestamp to 0 to avoid TF sync issues
            message.@object.header.stamp = new RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time(0, 0);
            
            message.@object.operation = operation;
            message.link_name = AttachLinkName;

            if (operation == 0) // ADD
            {
                UpdateGeometry();
                UpdatePose();
            }
            else if (operation == 1) // REMOVE
            {
               // For REMOVE, we just need ID and operation REMOVE. 
               // MoveIt detaches it.
            }

            Publish(message);
        }

        private void UpdateGeometry()
        {
            SolidPrimitive primitive = new SolidPrimitive();
            primitive.type = (byte)GetPrimitiveTypeByte();
            
            // Try to get Collider for accurate sizing
            Collider collider = GetComponent<Collider>();
            UnityEngine.Vector3 size = transform.lossyScale; // Fallback
            
            if (collider != null)
            {
                if (collider is BoxCollider box)
                {
                    size.x *= box.size.x;
                    size.y *= box.size.y;
                    size.z *= box.size.z;
                }
                else if (collider is SphereCollider sphere)
                {
                    float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                    float diameter = sphere.radius * 2 * maxScale;
                    size = new UnityEngine.Vector3(diameter, diameter, diameter);
                }
                else if (collider is CapsuleCollider capsule)
                {
                     float maxRadiusScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
                     size.x = capsule.radius * 2 * maxRadiusScale;
                     size.z = size.x;
                     size.y = capsule.height * transform.lossyScale.y;
                }
            }
            
            switch (shapeType)
            {
                case ShapeType.Box:
                    // Unity Z (Forward) -> ROS X (Forward)
                    // Unity X (Right)   -> ROS -Y (Right)
                    // Unity Y (Up)      -> ROS Z (Up)
                    primitive.dimensions = new double[] { size.z, size.x, size.y };
                    break;
                case ShapeType.Sphere:
                    primitive.dimensions = new double[] { size.x * 0.5f };
                    break;
                case ShapeType.Cylinder:
                    primitive.dimensions = new double[] { size.y, size.x * 0.5f };
                    break;
                case ShapeType.Capsule:
                    primitive.dimensions = new double[] { size.y, size.x * 0.5f }; 
                    break;
            }

            message.@object.primitives[0] = primitive;
        }

        private int GetPrimitiveTypeByte()
        {
            switch (shapeType)
            {
                case ShapeType.Box: return SolidPrimitive.BOX;
                case ShapeType.Sphere: return SolidPrimitive.SPHERE;
                case ShapeType.Cylinder: return SolidPrimitive.CYLINDER;
                case ShapeType.Capsule: return SolidPrimitive.CYLINDER;
            }
            return SolidPrimitive.BOX;
        }

        private void UpdatePose()
        {
            UnityEngine.Vector3 unityPos;
            UnityEngine.Quaternion unityRot;

            // Calculate pose relative to the ATTACH LINK
            if (AttachLinkTransform != null)
            {
                unityPos = AttachLinkTransform.InverseTransformPoint(transform.position);
                unityRot = UnityEngine.Quaternion.Inverse(AttachLinkTransform.rotation) * transform.rotation;
            }
            else
            {
                // Fallback: assume we are already children of the link in Unity hierarchy or similar
                unityPos = transform.localPosition;
                unityRot = transform.localRotation;
            }

            // Convert to ROS Coordinate System (Right Handed)
            message.@object.primitive_poses[0] = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose
            {
                position = new Point
                {
                    x = unityPos.z,
                    y = -unityPos.x,
                    z = unityPos.y
                },
                orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion
                {
                    x = -unityRot.z,
                    y = unityRot.x,
                    z = -unityRot.y,
                    w = unityRot.w
                }
            };
        }

        [ContextMenu("Publish Tool")]
        public void PublishNow()
        {
            InitializeMessage();
            PublishTool(0); // Add
        }
        
        [ContextMenu("Detach Tool")]
        public void DetachNow()
        {
            InitializeMessage();
            PublishTool(1); // Remove/Detach
        }
    }
}
