using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class InfoDisplay : MonoBehaviour
{
    public PlcConnector plc;

    [Header("UI Text Objects")]
    public TextMeshProUGUI textCarType;
    public TextMeshProUGUI textCarseq1;
    public TextMeshProUGUI textCarseq2;
    public TextMeshProUGUI textVisionSection;
    public TextMeshProUGUI textBody1;
    public TextMeshProUGUI textBody2;
    public TextMeshProUGUI textBody3;
    public TextMeshProUGUI textBody4;
    public TextMeshProUGUI textVisionUpdate;
    public TextMeshProUGUI textVisionStart;
    public TextMeshProUGUI textVisionEnd;
    public TextMeshProUGUI textVisionReset;
    public TextMeshProUGUI textVisionPass;

    void Update()
    {
        if (plc == null) return;

        if (textCarType != null) textCarType.text = $"Car Type: {plc.carType.ToString()}";

        if (textCarseq1 != null) textCarseq1.text = $"Car Seq 1: {DecodeWord(plc.carSeq1)}";
        if (textCarseq2 != null) textCarseq2.text = $"Car Seq 2: {DecodeWord(plc.carSeq2)}";

        if (textBody1 != null) textBody1.text = $"Body NO.1: {DecodeWord(plc.bodyNum1)}";
        if (textBody2 != null) textBody2.text = $"Body NO.2: {DecodeWord(plc.bodyNum2)}";
        if (textBody3 != null) textBody3.text = $"Body NO.3: {DecodeWord(plc.bodyNum3)}";
        if (textBody4 != null) textBody4.text = $"Body NO.4: {DecodeWord(plc.bodyNum4)}";

        if (textVisionSection != null) textVisionSection.text = $"Vision Section: {plc.visionSection.ToString()}";

        if (textVisionUpdate != null)
        {
            if (plc.isVisionUpdate)
            {
                textVisionUpdate.text = "VisionUpdate : TRUE";
                textVisionUpdate.color = Color.green;
            }
            else
            {
                textVisionUpdate.text = "VisionUpdate : FALSE";
                textVisionUpdate.color = Color.gray;
            }
        }
        if (textVisionStart != null)
        {
            if(plc.isVisionStart)
            {
                textVisionStart.text = "VisionStart : TRUE";
                textVisionStart.color = Color.green;
            }
            else
            {
                textVisionStart.text = "VisionUpdate : FALSE";
                textVisionStart.color = Color.gray;
            }
        }
        if (textVisionEnd != null)
        {
            if (plc.isVisionEnd)
            {
                textVisionEnd.text = "VisionEnd : TRUE";
                textVisionEnd.color = Color.green;
            }
            else
            {
                textVisionEnd.text = "VisionEnd : FALSE";
                textVisionEnd.color = Color.gray;
            }
        }
        if (textVisionReset != null)
        {
            if (plc.isVisionReset)
            {
                textVisionReset.text = "VisionReset : TRUE";
                textVisionReset.color = Color.green;
            }
            else
            {
                textVisionReset.text = "VisionReset : FALSE";
                textVisionReset.color = Color.gray;
            }
        }
        if (textVisionPass != null)
        {
            if (plc.isVisionPass)
            {
                textVisionPass.text = "VisionPass : TRUE";
                textVisionPass.color = Color.green;
            }
            else
            {
                textVisionPass.text = "VisionPass : FALSE";
                textVisionPass.color = Color.gray;
            }
        }
    }

    string DecodeWord(int wordValue)
    {
        if (wordValue == 0) return "--";

        char char1 = (char)(wordValue & 0xFF);
        char char2 = (char)((wordValue >> 8) & 0xFF);

        return $"{char1}{char2}";
    }
}
