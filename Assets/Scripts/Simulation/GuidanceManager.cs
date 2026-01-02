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
        public Transform TargetObject;

        [Header("Industrial Logic (T_ic)")]
        public Transform RobotFlange; // TCP
        public VirtualCameraMount CamMount;
        public MovePlanClient Mover;
        public RobotStateProvider RobotState;

        [Header("Saved Masters")]
        public Matrix4x4 T_ib = Matrix4x4.identity;
        public Matrix4x4 T_tb_master = Matrix4x4.identity;

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
            // 시작 시 PCG에게 "우리의 기준은 로봇 베이스다"라고 알려줌
            if (PCG != null && RobotState.RobotBase != null)
            {
                PCG.SetRobotBase(RobotState.RobotBase);
            }
        }

        [ContextMenu("Capture Master")]
        public void CaptureMaster()
        {
            // 타겟 위치 정보 불필요. 그냥 로봇이 보고 있는 걸 찍어서 베이스 기준으로 저장.
            if (PCG != null)
            {
                PCG.CaptureMaster();
                Debug.Log("[GuidanceManager] Master Cloud Captured (Base Frame).");
            }
        }

        [ContextMenu("Capture Scan (Current)")]
        public void CaptureCurrent()
        {
            // 현재 보고 있는 걸 찍어서 베이스 기준으로 저장.
            if (PCG != null)
            {
                PCG.CaptureScan();
                Debug.Log("[GuidanceManager] Current Scan Captured (Base Frame).");
            }
        }

        [ContextMenu("Run Guidance (Move)")]
        public void RunGuidance()
        {
            if (TargetObject == null || RobotState == null) return;

            currentObjectPose = new Pose(TargetObject.position, TargetObject.rotation);
            Matrix4x4 m_current_world = Matrix4x4.TRS(currentObjectPose.position, currentObjectPose.rotation, Vector3.one);
            Matrix4x4 m_master_world = Matrix4x4.TRS(masterObjectPose.position, masterObjectPose.rotation, Vector3.one);

            LastCorrectionMatrix = T_ib.inverse * (RobotState.RobotBase.worldToLocalMatrix * m_current_world);
            LastDeviationDist = Vector3.Distance(masterObjectPose.position, currentObjectPose.position);

            Debug.Log($"[GuidanceManager] Correction Calculated. Deviation: {LastDeviationDist:F4}m");

            if (Mover != null && RobotState != null)
            {
                Vector3 moveDelta = m_current_world.GetColumn(3) - m_master_world.GetColumn(3);
                Quaternion rotDelta = currentObjectPose.rotation * Quaternion.Inverse(masterObjectPose.rotation);

                Vector3 currentTcpPos = RobotState.TcpTransform.position;
                Quaternion currentTcpRot = RobotState.TcpTransform.rotation;

                Vector3 targetPos = currentTcpPos + moveDelta;
                Quaternion targetRot = rotDelta * currentTcpRot;

                GameObject shim = new GameObject("GuidanceTarget_Shim");
                shim.transform.position = targetPos;
                shim.transform.rotation = targetRot;

                Debug.Log($"[GuidanceManager] Executing IK Move to {targetPos}");
                Mover.PlanAndExecute(shim.transform);

                Destroy(shim, 1.0f);
            }
        }
    }
}