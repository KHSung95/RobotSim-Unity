using UnityEngine;
using UnityEngine.UI;

public class PLCManager : MonoBehaviour
{
    private IPLCProtocol _plc;
    public string ip = "127.0.0.1";
    public int port = 502; // 서버 포트와 맞추세요 (502 또는 5020)

    [Header("Monitoring UI")]
    public Image lampAddr0; // Unity 에디터에서 drag & drop 으로 연결

    void Start()
    {
        // 처음에는 LS Modbus 방식을 사용하도록 설정
        _plc = new LSModbusClient();
        _plc.Connect(ip, port);
    }

    void Update()
    {
        // PLC 연결 상태 확인
        if(_plc != null && _plc.IsConnected)
        {
            // 실시간으로 0번 어드레스 읽기 (비전 시작 신호 등)
            bool status = _plc.ReadBit(0);

            // UI 램프 색상 변경
            UpdateLamp(status);
        }
    }

    private void UpdateLamp(bool isOn)
    {
        if(lampAddr0 != null)
        {
            // On - 초록색, off - 빨간색
            lampAddr0.color = isOn ? Color.green : Color.red;
        }
    }

    public void UIBtn_ForceToggle(int address)
    {
        // 현재 상태를 읽어서 반대로 뒤집기 (Toggle)
        bool current = _plc.ReadBit(address);
        _plc.WriteBit(address, !current);
    }

    public void CheckStatus(int address)
    {
        bool status = _plc.ReadBit(address);
        Debug.Log($"Address {address} status: {status}");
    }

    void OnApplicationQuit()
    {
        _plc?.Disconnect();
    }
}