using UnityEngine;
using System.Collections.Generic;
using RobotSim.Sensors;
using RobotSim.ROS.Services;
using RobotSim.Robot;

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
        public MovePlanClient Mover;
        public RobotStateProvider RobotState;
        
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
            if (Mover == null) Mover = FindObjectOfType<MovePlanClient>();
            if (RobotState == null) RobotState = FindObjectOfType<RobotStateProvider>();
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

        [ContextMenu("Capture Scan (Current)")]
        public void CaptureCurrent()
        {
             if (TargetObject == null || RobotBase == null) return;

             // 1. Calculate T_ic for current scan
             Matrix4x4 T_ic_scan = T_ib.inverse * (RobotBase.worldToLocalMatrix * CamMount.transform.localToWorldMatrix);

             // 2. Capture Scan
             if (PCG != null) 
             {
                 PCG.CaptureScan(T_ic_scan);
             }
             Debug.Log("[GuidanceManager] Current Scan Captured.");
        }

        [ContextMenu("Run Guidance (Move)")]
        public void RunGuidance()
        {
            if (TargetObject == null || RobotBase == null) return;

            // 3. Mock Registration Correction Calculation
            currentObjectPose = new Pose(TargetObject.transform.position, TargetObject.transform.rotation);
            Matrix4x4 m_master_world = Matrix4x4.TRS(masterObjectPose.position, masterObjectPose.rotation, Vector3.one);
            Matrix4x4 m_current_world = Matrix4x4.TRS(currentObjectPose.position, currentObjectPose.rotation, Vector3.one);
            
            // Correction in Install Frame: T_corr = T_master^-1 * T_current
            // Note: This logic assumes RobotBase is the reference for T_ib
            LastCorrectionMatrix = T_ib.inverse * (RobotBase.worldToLocalMatrix * m_current_world);
            
            // Deviation for debug
            LastDeviationDist = Vector3.Distance(masterObjectPose.position, currentObjectPose.position);
            
            Debug.Log($"[GuidanceManager] Correction Calculated. Deviation: {LastDeviationDist:F4}m");

            // 4. Move Robot via IK
            // Strategy: Apply Correction Matrix to Current TCP Pose to find New TCP Pose
            // NewTCP = Correction * CurrentTCP? 
            // Actually, if Object moved by T, Robot should move by T to maintain relative pose.
            // T_corr represents the transformation of the object.
            
            if (Mover != null && RobotState != null)
            {
                // Decompose Correction Matrix
                Vector3 moveDelta = m_current_world.GetColumn(3) - m_master_world.GetColumn(3);
                Quaternion rotDelta = currentObjectPose.rotation * Quaternion.Inverse(masterObjectPose.rotation);

                // Current TCP World Pose
                Vector3 currentTcpPos = RobotState.TcpTransform.position;
                Quaternion currentTcpRot = RobotState.TcpTransform.rotation;

                // Target TCP World Pose
                Vector3 targetPos = currentTcpPos + moveDelta;
                Quaternion targetRot = rotDelta * currentTcpRot; // Apply rotation delta

                // Create a temporary target shim for MovePlanClient
                GameObject shim = new GameObject("GuidanceTarget_Shim");
                shim.transform.position = targetPos;
                shim.transform.rotation = targetRot;

                Debug.Log($"[GuidanceManager] Executing IK Move to {targetPos}");
                Mover.PlanAndExecute(shim.transform);

                Destroy(shim, 1.0f); // Cleanup
            }
            else
            {
                Debug.LogWarning("[GuidanceManager] Cannot Move: Mover or RobotState is missing.");
            }
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
