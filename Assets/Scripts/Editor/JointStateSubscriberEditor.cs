using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using RosSharp.Urdf;

namespace RosSharp.RosBridgeClient
{
    [CustomEditor(typeof(JointStateSubscriber))]
    public class JointStateSubscriberEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            JointStateSubscriber subscriber = (JointStateSubscriber)target;

            if (GUILayout.Button("Auto Fill Joint Names from Writers"))
            {
                AutoFillJointNames(subscriber);
            }
        }

        private void AutoFillJointNames(JointStateSubscriber subscriber)
        {
            if (subscriber.JointStateWriters == null || subscriber.JointStateWriters.Count == 0)
            {
                Debug.LogWarning("Please assign 'Joint State Writers' first!");
                return;
            }

            List<string> newNames = new List<string>();
            bool errorFound = false;

            foreach (var writer in subscriber.JointStateWriters)
            {
                if (writer == null) continue;

                UrdfJoint urdfJoint = writer.GetComponent<UrdfJoint>();
                if (urdfJoint != null)
                {
                    newNames.Add(urdfJoint.JointName);
                }
                else
                {
                    Debug.LogError($"Writer on {writer.name} does not have a UrdfJoint component!");
                    errorFound = true;
                    newNames.Add("MISSING_URDF_JOINT");
                }
            }

            if (!errorFound)
            {
                Undo.RecordObject(subscriber, "Auto Fill Joint Names");
                subscriber.JointNames = newNames;
                Debug.Log($"Successfully updated {newNames.Count} joint names from UrdfJoints.");
            }
        }
    }
}
