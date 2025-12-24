using UnityEngine;
using System.Collections.Generic;
using RobotSim.Sensors;

namespace RobotSim.Simulation
{
    public class GuidanceManager : MonoBehaviour
    {
        [Header("References")]
        public PointCloudGenerator PCG;
        public GameObject TargetObject; // The Car Door
        public Transform RobotBase;

        [Header("Industrial Logic (T_ic)")]
        public Transform RobotFlange; // TCP
        public VirtualCameraMount CamMount;
        
        [Header("Saved Masters")]
        public Matrix4x4 T_ib = Matrix4x4.identity; // Base to Install Pose (Reference)
        public Matrix4x4 T_tb_master = Matrix4x4.identity; // Robot Pose during Master scan
        
        [Header("Results")]
        public Matrix4x4 LastCorrectionMatrix = Matrix4x4.identity;
        public float LastDeviationDist = 0f;
        
        private Pose masterObjectPose;
        private Pose currentObjectPose;

        private void Start()
        {
            if (PCG == null) PCG = FindObjectOfType<PointCloudGenerator>();
            if (CamMount == null) CamMount = FindObjectOfType<VirtualCameraMount>();
        }

        [ContextMenu("Capture Master")]
        public void CaptureMaster()
        {
            if (TargetObject == null || RobotBase == null || RobotFlange == null) return;

            // Save Ground Truth Pose for Mock Registration
            masterObjectPose = new Pose(TargetObject.transform.position, TargetObject.transform.rotation);

            // 1. Install Pose Matrix (T_ib) 
            // We use the TargetObject's current pose as the "Install Pose" reference
            T_ib = RobotBase.worldToLocalMatrix * TargetObject.transform.localToWorldMatrix;
            
            // 2. Transformation Matrix for points: T_ic = T_ib.inverse * (Base <- Camera)
            Matrix4x4 T_ic = T_ib.inverse * (RobotBase.worldToLocalMatrix * CamMount.transform.localToWorldMatrix);

            // 3. Capture Point Cloud and store in Install Frame
            if (PCG != null) 
            {
                PCG.CaptureMaster(T_ic);
            }
            
            Debug.Log("[GuidanceManager] Master Captured. Install Pose & Master Scan stored.");
        }

        [ContextMenu("Run Guidance Scan")]
        public void RunGuidance()
        {
            if (TargetObject == null || RobotBase == null || RobotFlange == null) return;

            // 1. Calculate T_ic for current scan
            Matrix4x4 T_ic_scan = T_ib.inverse * (RobotBase.worldToLocalMatrix * CamMount.transform.localToWorldMatrix);

            // 2. Capture and Compare
            if (PCG != null) 
            {
                PCG.CaptureScan(T_ic_scan);
            }

            // 3. Mock Registration Correction Calculation
            currentObjectPose = new Pose(TargetObject.transform.position, TargetObject.transform.rotation);
            Matrix4x4 m_master_world = Matrix4x4.TRS(masterObjectPose.position, masterObjectPose.rotation, Vector3.one);
            Matrix4x4 m_current_world = Matrix4x4.TRS(currentObjectPose.position, currentObjectPose.rotation, Vector3.one);
            
            // Correction in Install Frame: T_corr = T_master^-1 * T_current
            LastCorrectionMatrix = T_ib.inverse * (RobotBase.worldToLocalMatrix * m_current_world);
            
            LastDeviationDist = Vector3.Distance(masterObjectPose.position, currentObjectPose.position);
            
            Debug.Log($"[GuidanceManager] Scan Complete. T_ic formula applied. Deviation: {LastDeviationDist:F4}m");
        }

        public string GetMatrixString()
        {
            Matrix4x4 m = LastCorrectionMatrix;
            return string.Format(
                "[{0:F2}, {1:F2}, {2:F2}, {3:F2}]\n" +
                "[{4:F2}, {5:F2}, {6:F2}, {7:F2}]\n" +
                "[{8:F2}, {9:F2}, {10:F2}, {11:F2}]\n" +
                "[{12:F2}, {13:F2}, {14:F2}, {15:F2}]",
                m.m00, m.m01, m.m02, m.m03,
                m.m10, m.m11, m.m12, m.m13,
                m.m20, m.m21, m.m22, m.m23,
                m.m30, m.m31, m.m32, m.m33
            );
        }
    }
}
