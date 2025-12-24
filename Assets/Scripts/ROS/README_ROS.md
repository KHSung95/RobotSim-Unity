# UR5e Unity-ROS2 Guidance System

This package provides a bidirectional interface between Unity (Frontend) and ROS2 (Backend) for guiding a UR5e robot.

## 📂 Key Scripts (`Assets/Scripts/ROS/`)

### 1. Target Guidance
- **`TargetPosePublisher.cs`**: 
  - Attached to the **Target Marker** (Ghost Object).
  - Sends the target pose to ROS2 (`/unity/target_pose`).
  - Converts Unity coordinates (Left-handed) to ROS coordinates (Right-handed) automatically relative to the Robot Base.
  - **Usage**: Press **`Enter`** to send the current target pose.
- **`TargetPoseController.cs`**: 
  - Provides runtime control for the Target Marker.
  - **Controls**: `WASD` + `QE` (Move), `Tab` (Toggle Rotate/Move), `Space` (Toggle Local/Global).

### 2. System Integration
- **`ClockPublisher.cs`**: 
  - Publishes Unity time to `/clock` for synchronization when `use_sim_time` is true in ROS2.
- **`RosConnectionMonitor.cs`**: 
  - Displays a real-time HUD showing:
    - Last sent target position.
    - Current robot joint angles (received from ROS2).

## 🛠️ Setup Guide

### 1. Robot Setup (Joints)
1. Import UR5e URDF.
2. Add **`Joint State Writer`** (ROS#) to each joint link (`shoulder_link`, etc.).
3. Add **`Joint State Subscriber`** (ROS#) to the Robot Root.
   - Topic: `/joint_states`
4. **Auto Setup**: Select the Subscriber and click **"Auto Fill Joint Names"** (Custom Editor Tool).

### 2. Target Setup (Ghost)
1. Create an empty object (e.g., `TargetMarker`) and attach:
   - `TargetPoseController`
   - `TargetPosePublisher`
     - **Target Object**: Self (`TargetMarker`)
     - **Reference Object**: Robot's `base_link`
     - **Publish Key**: `Enter`

### 3. Obstacle Setup (Collision Objects)
1. Select an object in the scene (e.g., Table, Obstacle).
2. Add **`CollisionObjectPublisher`**.
3. Set **Primitive Type** (Box, Sphere, Cylinder, Capsule).
4. Set **Id** (e.g., "Table").
5. Ensure a Collider (BoxCollider etc.) is attached for accurate sizing.
6. Press Play. The object will be added to the MoveIt2 Planning Scene.

### 4. Tool/Gripper Setup (Attached Objects)
1. Create/Import your tool model (e.g., Camera, Gripper) as a child of the Robot's flange link (e.g., `tool0`) in Unity Hierarchy for easy positioning.
2. Add **`ToolPublisher`** component to the tool object.
3. **Tool Id**: Unique name (e.g., "MainCamera").
4. **Attach Link Name**: The name of the link it's attached to (e.g., `tool0`).
5. **Attach Link Transform**: Drag the Unity GameObject of `tool0` here.
6. **Touch Links**: Add `tool0` and `wrist_3_link` to this list to ignore self-collisions.
7. Press Play. The tool will be attached to the robot in ROS (RViz).

### 5. ROS2 Execution
```bash
# Launch the backend bridge
ros2 launch ur5e_unity_bridge bringup.launch.py use_sim_time:=true
```

## ✅ Troubleshooting
- **Robot not moving?** Check `RosConnectionMonitor`. If joint angles are changing but the model isn't moving, check `Is Kinematic` on Rigidbody components.
- **No data?** Ensure Topic names match exactly (`/joint_states` vs `/ur/joint_states`).
- **Obstacles not showing in RViz?** Ensure "Scene Objects" is enabled in RViz "Motion Planning" plugin. Check `/collision_object` topic.
- **Attached Tool Jumping?** Ensure `Attachment Link Transform` is correct and `Frame Id` matches the ROS link name.

### 6. Joint Control Mode (Forward Kinematics)
In addition to Target Pose (IK), you can control joints directly.
1. Create a game object (e.g. `JointController`).
2. Attach `TargetJointPublisher` and `TargetJointController`.
3. Press **Play**.
4. Use Keys **`1`~`6`** to select joint, **`Left/Right`** arrow to rotate, **`Enter`** to Publish.

#### ⚠️ REQUIRED: Python Backend Update
You must update your **`ur5e_unity_bridge_node.py`** to listen to the new topic.
Add this subscriber and callback:

```python
from sensor_msgs.msg import JointState

# In __init__:
self.joint_sub = self.create_subscription(
    JointState,
    '/unity/target_joints',
    self.joint_callback,
    10
)

# In the class:
def joint_callback(self, msg):
    try:
        self.get_logger().info(f"Received Joint Target: {msg.position[:6]}")
        # Command MoveIt
        self.move_group.set_joint_value_target(msg.position)
        
        # Plan and Execute
        success = self.move_group.go(wait=True)
        self.move_group.stop()
        self.move_group.clear_pose_targets()
        
        if success:
            self.get_logger().info("Joint Target Execution Succeeded")
        else:
            self.get_logger().error("Joint Target Execution Failed")
    except Exception as e:
        self.get_logger().error(f"Joint Callback Error: {e}")
```

