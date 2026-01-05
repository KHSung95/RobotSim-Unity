using Modbus.Device;
using System;
using System.Net.Sockets;
using UnityEngine;

public class LSModbusClient : IPLCProtocol
{
    private TcpClient _tcpClient;
    private ModbusIpMaster _master;
    public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

    public void Connect(string ip, int port)
    {
        try
        {
            _tcpClient = new TcpClient(ip, port);
            _master = ModbusIpMaster.CreateIp(_tcpClient);
            Debug.Log($"[PLC] Connected to {ip}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PLC] Connection Failed: {e.Message}");
        }
    }

    public bool ReadBit(int address)
    {
        if (!IsConnected) return false;
        // LS Modbus TCP는 보통 Coil 주소를 0부터 시작합니다.
        bool[] inputs = _master.ReadCoils(1, (ushort)address, 1);
        return inputs[0];
    }

    public void WriteBit(int address, bool value)
    {
        if (!IsConnected) return;
        _master.WriteSingleCoil(1, (ushort)address, value);
    }

    public ushort ReadRegister(int address)
    {
        if (!IsConnected) return 0;
        ushort[] registers = _master.ReadHoldingRegisters(1, (ushort)address, 1);
        return registers[0];
    }

    public void WriteRegister(int address, ushort value)
    {
        if (!IsConnected) return;
        _master.WriteSingleRegister(1, (ushort)address, value);
    }

    public void Disconnect()
    {
        _tcpClient?.Close();
    }
}