using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.UIElements;

public class PlcConnector : MonoBehaviour
{
    private TcpClient _client;
    private NetworkStream _stream;
    private bool _isConnected;

    private string serverIp = "127.0.0.1";
    private int serverPort = 6000;

    [Header("PLC Memory (Polling)")]
    public int carType;
    public int carSeq1;
    public int carSeq2;
    public int visionSection;

    public int bodyNum1;
    public int bodyNum2;
    public int bodyNum3;
    public int bodyNum4;

    public bool isVisionUpdate = false;
    public bool isVisionStart = false;
    public bool isVisionEnd = false;
    public bool isVisionReset = false;
    public bool isVisionPass = false;

    async void Start()
    {
        await ConnectToServer();
    }

    async Task ReceiveLoop()
    {
        byte[] buffer = new byte[1024];

        while (_isConnected && _stream != null)
        {
            try
            {
                if (_stream.DataAvailable) 
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ParseServerData(buffer, bytesRead);
                    }
                }
            }
            catch(Exception e)
            {
                Debug.LogError($"[Unity] Receive Error : {e.Message}");
                break;
            }

            await Task.Delay(10); // 10ms 주기로 데이터 확인
        }
    }

    public async void SendSignal(int address, bool value)
    {
        if (!_isConnected || _stream == null) return;

        try
        {
            // 단순 규약: [Address(4bytes)][Value(4bytes)]
            byte[] addrBytes = BitConverter.GetBytes(address);
            byte[] valBytes = BitConverter.GetBytes(value);

            byte[] packet = new byte[8];
            Array.Copy(addrBytes, 0, packet, 0, 4);
            Array.Copy(valBytes, 0, packet, 4, 4);

            await _stream.WriteAsync(packet, 0, packet.Length);
            Debug.Log($"[Unity -> PLC] 전송: Addr {address}, Val {value}");
        }
        catch (Exception e)
        {
            Debug.LogError($"전송 에러: {e.Message}");
        }
    }

    void ParseServerData(byte[] data, int length)
    {
        for (int i = 0; i <= length - 8; i += 8)
        {
            int addr = BitConverter.ToInt32(data, i);
            int val = BitConverter.ToInt32(data, i + 4);

            Debug.Log($"[PLC -> Unity] Recv Addr: {addr}, Val: {val}");

            switch (addr)
            {
                case 4101:
                    carType = val;
                    break;
                case 4102:
                    carSeq1 = val;
                    break;
                case 4103:
                    carSeq2 = val;
                    break;
                case 4106:
                    visionSection = val;
                    break;
                case 4108:
                    bodyNum1 = val;
                    break;
                case 4109:
                    bodyNum2 = val;
                    break;
                case 4110:
                    bodyNum3 = val;
                    break;
                case 4111:
                    bodyNum4 = val;
                    break;
                case 43000:
                    isVisionUpdate = (val == 1);
                    break;
                case 43001:
                    isVisionStart = (val == 1);
                    break;
                case 43002:
                    isVisionEnd = (val == 1);
                    break;
                case 43003:
                    isVisionReset = (val == 1);
                    break;
                case 43004:
                    isVisionPass = (val == 1);
                    break;
            }
        }
    }

    public void SendVisionResult(bool isOk)
    {
        if (!_isConnected || _stream == null)
        {
            return;
        }

        int address = isOk ? 45000 : 45001;
        byte[] packet = CreateWritePacket(address, 1);

        try
        {
            _stream.Write(packet, 0, packet.Length);
            Debug.Log($"[Unity -> PLC] VISION Result Sent : {(isOk ? "OK" : "NG")}");
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    private byte[] CreateWritePacket(int addr, int val)
    {
        byte[] packet = new byte[8];
        Array.Copy(BitConverter.GetBytes(addr), 0, packet, 0, 4);
        Array.Copy(BitConverter.GetBytes(val), 0, packet, 4, 4);
        return packet;
    }

    async Task ConnectToServer()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(serverIp, serverPort);

            _stream = _client.GetStream();
            _isConnected = true;

            _ = ReceiveLoop();

            Debug.Log("PLC 서버 연결 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"PlcConnector - ConnectToServer Error : {e.Message}");
        }
    }
}