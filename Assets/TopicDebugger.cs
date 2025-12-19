using UnityEngine;
using RosSharp.RosBridgeClient;

public class TopicDebugger : UnitySubscriber<RosSharp.RosBridgeClient.MessageTypes.Sensor.JointState>
{
    protected override void ReceiveMessage(RosSharp.RosBridgeClient.MessageTypes.Sensor.JointState message)
    {
        // 무조건 로그를 찍어서 통신 성공 여부 확인
        Debug.Log($"[DEBUG] 메시지 수신 성공! {Time.time}");
    }
}