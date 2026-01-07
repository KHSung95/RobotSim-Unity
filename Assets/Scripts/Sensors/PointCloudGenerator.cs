using UnityEngine;
using System.Collections.Generic;
using System;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RobotSim.Utils;

namespace RobotSim.Sensors
{
    [RequireComponent(typeof(Camera))]
    public class PointCloudGenerator : MonoBehaviour
    {
        [Header("Scan Settings")]
        public int Width = 160;
        public int Height = 120;
        public float MaxDistance = 2.0f;
        public LayerMask ScanTypes = -1;

        [Header("Sensor Simulation")]
        public float NoiseLevel = 0.0005f;

        // Internal Data
        private List<Vector3> masterPoints = new List<Vector3>();
        private List<Vector3> scanPoints = new List<Vector3>();

        public List<Vector3> MasterPoints => masterPoints;
        public List<Vector3> ScanPoints => scanPoints;

        private Camera cam;

        // Events for consumers (GuidanceManager)
        public event Action OnMasterCaptured;
        public event Action OnScanCaptured;

        private void Start()
        {
            cam = GetComponent<Camera>();
        }

        public void ClearScan()
        {
            scanPoints.Clear();
            OnScanCaptured?.Invoke();
        }

        // --- Capture Logic ---

        public void CaptureMaster()
        {
            // Capture in Camera Local Space (Eye-in-Hand)
            Matrix4x4 matrix = this.transform.worldToLocalMatrix;
            
            CaptureInternal(matrix, ref masterPoints);
            OnMasterCaptured?.Invoke();
            Debug.Log($"[PointCloudGenerator] Captured Master: {masterPoints.Count} points. Frame: CameraLocal");
        }

        public void CaptureScan()
        {
            // Capture in Camera Local Space
            Matrix4x4 matrix = this.transform.worldToLocalMatrix;

            CaptureInternal(matrix, ref scanPoints);
            OnScanCaptured?.Invoke();
            Debug.Log($"[PointCloudGenerator] Captured Scan: {scanPoints.Count} points. Frame: CameraLocal");
        }

        private void CaptureInternal(Matrix4x4 T_ref_world, ref List<Vector3> points)
        {
            points.Clear();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 viewPos = new Vector3((float)x / Width, (float)y / Height, 0);
                    Ray ray = cam.ViewportPointToRay(viewPos);

                    if (Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ScanTypes))
                    {
                        Vector3 hitPoint = hit.point;
                        if (NoiseLevel > 0) hitPoint += UnityEngine.Random.insideUnitSphere * NoiseLevel;

                        Vector3 pos = T_ref_world.MultiplyPoint3x4(hitPoint);
                        points.Add(pos);
                    }
                }
            }
        }
    }
}