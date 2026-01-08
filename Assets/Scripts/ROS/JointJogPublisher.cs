using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Control; // JointJogRos2 네임스페이스 확인 필요
using RosSharp.RosBridgeClient.MessageTypes.Std;

using RobotSim.Robot;
using RobotSim.Control;

namespace RobotSim.ROS
{
    [RequireComponent(typeof(RosJogAdapter))]
    public class JointJogPublisher : UnityPublisher<JointJogRos2>
    {
        private string FrameId = "base_link";
        private RobotStateProvider StateProvider;

        private string[] _allJointNames;

        // 메시지 객체 재사용을 위한 캐싱
        private JointJogRos2 _cachedMessage;

        // 배열 할당 최소화를 위해 미리 할당
        private string[] _singleNameArray = new string[1];
        private double[] _singleVelArray = new double[1];
        private double[] _emptyDispArray = new double[0];

        protected override void Start()
        {
            base.Start();
            InitializeMessage();
        }

        private void InitializeMessage()
        {
            // 한 번만 생성하고 내용만 바꿔서 씁니다.
            _cachedMessage = new JointJogRos2
            {
                header = new Header { frame_id = FrameId },
                joint_names = _singleNameArray,
                velocities = _singleVelArray,
                displacements = _emptyDispArray,
                duration = 0
            };
        }

        public void SetRobotStateProvider(RobotStateProvider rsp)
        {
            StateProvider = rsp;
            if (StateProvider != null)
            {
                if (StateProvider.JointNames == null) StateProvider.InitializeReferences();
                _allJointNames = StateProvider.JointNames;
                FrameId = StateProvider.BaseFrameId;
            }
        }

        public void PublishJog(int jointIndex, float velocity)
        {
            if (_allJointNames == null || _allJointNames.Length == 0) return;
            if (jointIndex < 0 || jointIndex >= _allJointNames.Length) return;

            // 1. 메시지 내용 업데이트 (new 할당 없음)
            _cachedMessage.header.Update(); // 시퀀스 증가 및 타임스탬프 갱신

            // 2. 데이터 주입
            _singleNameArray[0] = _allJointNames[jointIndex]; // 이름 갱신
            _singleVelArray[0] = (double)velocity;            // 속도 갱신

            // 4. 발행
            Publish(_cachedMessage);
        }
    }
}