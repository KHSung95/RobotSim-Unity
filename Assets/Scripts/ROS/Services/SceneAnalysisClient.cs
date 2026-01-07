using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Custom;
using System;
using RobotSim.Utils;

namespace RobotSim.ROS.Services
{
    public class SceneAnalysisClient : MonoBehaviour
    {
        [Header("ROS Settings")]
        public RosConnector Connector;
        public string ServiceName = "/process_scene";

        private UnityMainThreadDispatcher _dispatcher;

        private void Start()
        {
            if (Connector == null) Connector = GetComponent<RosConnector>();
            if (Connector == null) Connector = FindFirstObjectByType<RosConnector>();
            
            // Cache Dispatcher on Main Thread
            _dispatcher = UnityMainThreadDispatcher.Instance();
        }

        public void SendAnalysisRequest(string command, float threshold, RosSharp.RosBridgeClient.MessageTypes.Sensor.PointCloud2 cloud, Action<ProcessSceneResponse> onResponse)
        {
            if (Connector == null || Connector.RosSocket == null)
            {
                Debug.LogError("[SceneAnalysisClient] ROS Connector not ready.");
                return;
            }

            var request = new ProcessSceneRequest(command, threshold, cloud);
            
            Debug.Log($"[SceneAnalysisClient] Sending '{command}' request to {ServiceName}...");
            
            Connector.RosSocket.CallService<ProcessSceneRequest, ProcessSceneResponse>(
                ServiceName,
                (response) => {
                    Debug.Log($"[SceneAnalysisClient] Response received from ROS (Success: {response.success})");
                    // Back to main thread for Unity logic if needed
                    // Back to main thread for Unity logic if needed
                    if (_dispatcher != null)
                    {
                        _dispatcher.Enqueue(() => {
                            Debug.Log("[SceneAnalysisClient] Dispatching response to Main Thread");
                            onResponse?.Invoke(response);
                        });
                    }
                    else
                    {
                        Debug.LogError("[SceneAnalysisClient] Dispatcher is null. Cannot dispatch response.");
                    }
                },
                request
            );
        }
    }
}
