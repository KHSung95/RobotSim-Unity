using UnityEngine;

public class RobotController : MonoBehaviour
{
    public PlcConnector plc;
    private int _prevSection = -1;

    void Update()
    {
        if (plc.visionSection != _prevSection)
        {
            _prevSection = plc.visionSection;
            HandleSectionChange(_prevSection);
        }
    }

    void HandleSectionChange(int section)
    {
        if (section == 1)
        {
            Debug.Log("로봇 이동 시작!");
            // 이동 완료 후 PLC에 신호 보내기 (예: D4300.2 검사완료)
            // plc.SendSignal(43002, 1); 
        }
    }
}