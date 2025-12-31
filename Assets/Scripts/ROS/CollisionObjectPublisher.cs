using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Moveit;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Shape;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RosSharp.RosBridgeClient
{
    public class CollisionObjectPublisher : UnityPublisher<CollisionObject>
    {
        [Header("Collision Object Settings")]
        public string Id;
        public string FrameId = "base_link";

        [Header("Geometry")]
        [Tooltip("The Reference Object (e.g. Robot Base) to calculate relative pose.")]
        public UnityEngine.Transform ReferenceFrame;
        
        // Supported Shape Types
        public enum ShapeType { Box, Sphere, Cylinder, Capsule }
        public ShapeType shapeType = ShapeType.Box;

        private CollisionObject message;

        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/collision_object";
            base.Start();
            
            if (string.IsNullOrEmpty(Id))
                Id = gameObject.name;

            InitializeMessage();
            PublishCollisionObject(0); // 0 = ADD
        }

        private void OnDestroy()
        {
            // Only attempt to publish remove if we are cleaning up logic at runtime,
            // NOT when the application is shutting down (Connector might be dead).
            // We can check if RosSocket is still alive implicitly via trying to access it safely or catching.
            try 
            {
                if (Application.isPlaying) 
                    PublishCollisionObject(1); // 1 = REMOVE
            }
            catch {}
        }

        private void InitializeMessage()
        {
            message = new CollisionObject
            {
                header = new Header { frame_id = FrameId },
                id = Id,
                operation = 0 // ADD
            };

            // Initialize Lists
            message.primitives = new SolidPrimitive[1];
            message.primitive_poses = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose[1];
        }

        public void PublishCollisionObject(sbyte operation)
        {
            // Update Header normally first (fills frame_id etc)
            message.header.Update();
            
            // CRITICAL FIX: Overwrite timestamp to 0. 
            // This forces MoveIt/TF to treat this pose as 'the latest available' 
            // and ignores any time synchronization diffs between Unity and ROS PC.
            // Also fixes issues when use_sim_time is inconsistent.
            // Note: ROS2 Header uses BuiltinInterfaces.Time, not Std.Time
            message.header.stamp = new RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time(0, 0); 
            
            message.operation = operation;

            if (operation == 0) // ADD or MOVE
            {
                UpdateGeometry();
                UpdatePose();
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
                    // BoxCollider size is in local space. Multiply by scale.
                    // Note: BoxCollider.size is the full width/height/depth.
                    size.x *= box.size.x;
                    size.y *= box.size.y;
                    size.z *= box.size.z;
                }
                else if (collider is SphereCollider sphere)
                {
                    // Sphere radius is local.
                    float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                    float diameter = sphere.radius * 2 * maxScale;
                    size = new UnityEngine.Vector3(diameter, diameter, diameter);
                }
                else if (collider is CapsuleCollider capsule) // Cylinder is often represented by CapsuleCollider in Unity or MeshCollider
                {
                    // Approximate Capsule/Cylinder
                     float maxRadiusScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
                     size.x = capsule.radius * 2 * maxRadiusScale; // Diameter X
                     size.z = size.x;
                     size.y = capsule.height * transform.lossyScale.y; // Height Y
                }
            }
            
            // ROS Dimensions
            // Box: x, y, z
            // Sphere: radius
            // Cylinder: height, radius
            
            switch (shapeType)
            {
                case ShapeType.Box:
                    // Unity Z (Forward) -> ROS X (Forward)
                    // Unity X (Right)   -> ROS -Y (Right)
                    // Unity Y (Up)      -> ROS Z (Up)
                    primitive.dimensions = new double[] { size.z, size.x, size.y };
                    break;
                case ShapeType.Sphere:
                    primitive.dimensions = new double[] { size.x * 0.5f }; // Radius
                    break;
                case ShapeType.Cylinder:
                    primitive.dimensions = new double[] { size.y, size.x * 0.5f }; // Height=Y, Radius=X/2
                    break;
                case ShapeType.Capsule:
                    // Mapping Capsule to Cylinder for now as SolidPrimitive has no CAPSULE
                    primitive.dimensions = new double[] { size.y, size.x * 0.5f }; 
                    break;
            }

            message.primitives[0] = primitive;
        }

        private int GetPrimitiveTypeByte()
        {
            switch (shapeType)
            {
                case ShapeType.Box: return SolidPrimitive.BOX;
                case ShapeType.Sphere: return SolidPrimitive.SPHERE;
                case ShapeType.Cylinder: return SolidPrimitive.CYLINDER;
                case ShapeType.Capsule: return SolidPrimitive.CYLINDER; // Approximate as Cylinder
            }
            return SolidPrimitive.BOX;
        }

        private void UpdatePose()
        {
            UnityEngine.Vector3 unityPos;
            UnityEngine.Quaternion unityRot;

            if (ReferenceFrame != null)
            {
                unityPos = ReferenceFrame.InverseTransformPoint(transform.position);
                unityRot = UnityEngine.Quaternion.Inverse(ReferenceFrame.rotation) * transform.rotation;
            }
            else
            {
                unityPos = transform.localPosition;
                unityRot = transform.localRotation;
            }

            // Convert to ROS Coordinate System (Right Handed)
            message.primitive_poses[0] = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose
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

        [ContextMenu("Publish Object")]
        public void PublishNow()
        {
            InitializeMessage();
            
            Debug.Log("publish collision object");
            PublishCollisionObject(0); // Add
        }
    }
}
