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

### 3. ROS2 Execution
```bash
# Launch the backend bridge
ros2 launch ur5e_unity_bridge bringup.launch.py use_sim_time:=true
```

## ✅ Troubleshooting
- **Robot not moving?** Check `RosConnectionMonitor`. If joint angles are changing but the model isn't moving, check `Is Kinematic` on Rigidbody components.
- **No data?** Ensure Topic names match exactly (`/joint_states` vs `/ur/joint_states`).
