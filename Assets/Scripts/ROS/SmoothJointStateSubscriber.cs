using UnityEngine;
using System.Collections.Generic;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.Urdf;

namespace RobotSim.ROS
{
    public class SmoothJointStateSubscriber : UnitySubscriber<JointState>
    {
        public UrdfRobot Robot;
        public float SmoothingSpeed = 10f; // Interpolation strength

        private Dictionary<string, UrdfJoint> jointMap = new Dictionary<string, UrdfJoint>();
        private Dictionary<string, float> targetPositions = new Dictionary<string, float>();
        
        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/unity/joint_states";
            if (Robot == null) Robot = GetComponent<UrdfRobot>();
            
            // Index Joints
            var joints = Robot.GetComponentsInChildren<UrdfJoint>();
            foreach (var j in joints)
            {
                if (!string.IsNullOrEmpty(j.JointName))
                    jointMap[j.JointName] = j;
            }

            base.Start();
        }

        protected override void ReceiveMessage(JointState message)
        {
            // Update Goals
            long count = message.name.Length;
            for (int i = 0; i < count; i++)
            {
                string name = message.name[i];
                if (i < message.position.Length)
                {
                    double pos = message.position[i];
                    lock (targetPositions)
                    {
                         if (targetPositions.ContainsKey(name))
                             targetPositions[name] = (float)pos;
                         else
                             targetPositions.Add(name, (float)pos);
                    }
                }
            }
        }

        private void Update()
        {
            lock (targetPositions)
            {
                foreach (var kvp in targetPositions)
                {
                    var name = kvp.Key;
                    var targetVal = kvp.Value;

                    if (jointMap.ContainsKey(name))
                    {
                        var joint = jointMap[name];
                        float current = (float)joint.GetPosition(); // Assuming Rad
                        
                        // Lerp for smoothness
                        float next = Mathf.Lerp(current, targetVal, Time.deltaTime * SmoothingSpeed);
                        float diff = next - current;
                        
                        // Apply Delta
                        joint.UpdateJointState(diff); 
                    }
                }
            }
        }
    }
}
