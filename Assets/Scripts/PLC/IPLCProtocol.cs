using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPLCProtocol
{
    bool IsConnected { get; }
    void Connect(string ip, int port);
    void Disconnect();

    // 신호 읽기/쓰기
    bool ReadBit(int address);
    void WriteBit(int address, bool value);

    // 데이터(워드) 읽기/쓰기
    ushort ReadRegister(int address);
    void WriteRegister(int address, ushort value);
}