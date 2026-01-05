using UnityEngine;
using System.Collections.Generic;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.Urdf;

namespace RobotSim.Robot
{
    public class SmoothJointStateSubscriber : UnitySubscriber<JointState>
    {
        public float SmoothingSpeed = 10f; // Interpolation strength
        public RobotStateProvider StateProvider;

        private Dictionary<string, float> targetPositions = new Dictionary<string, float>();
        
        protected override void Start()
        {
            if (string.IsNullOrEmpty(Topic)) Topic = "/unity/joint_states";
            if (StateProvider == null) StateProvider = GetComponent<RobotStateProvider>();
            if (StateProvider == null) StateProvider = GetComponentInParent<RobotStateProvider>();
            
            if (StateProvider == null)
            {
                Debug.LogWarning("[SmoothJointStateSubscriber] RobotStateProvider not found! Using fallback joint discovery.");
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

                    if (StateProvider != null && StateProvider.JointMap.ContainsKey(name))
                    {
                        var joint = StateProvider.JointMap[name];
                        float current = (float)joint.GetPosition(); // Assuming Rad
                        
                        // [Fix] Shortest Path Interpolation (in Radians)
                        float angleDiff = targetVal - current;
                        
                        // Unwrap (-PI to PI)
                        while (angleDiff > Mathf.PI) angleDiff -= 2 * Mathf.PI;
                        while (angleDiff < -Mathf.PI) angleDiff += 2 * Mathf.PI;

                        // Apply smoothing to the delta
                        float step = Mathf.Lerp(0, angleDiff, Time.deltaTime * SmoothingSpeed);
                        
                        // Apply Delta
                        joint.UpdateJointState(step); 
                    }
                }
            }
        }
    }
}
