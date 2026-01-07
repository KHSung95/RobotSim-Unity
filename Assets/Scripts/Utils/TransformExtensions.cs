using UnityEngine;
using System.Collections.Generic;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RobotSim.Utils
{
    public static class TransformExtensions
    {
        public static Transform FindDeepChild(this Transform aParent, string aName)
        {
            var result = aParent.Find(aName);
            if (result != null) return result;
            foreach (Transform child in aParent)
            {
                result = child.FindDeepChild(aName);
                if (result != null) return result;
            }
            return null;
        }

        public static PointCloud2 ToPointCloud2(this List<Vector3> points)
        {
            var msg = new PointCloud2();
            msg.header = new Header { frame_id = "unity_camera" }; 
            msg.height = 1;
            msg.width = (uint)points.Count;
            msg.is_bigendian = false;
            msg.is_dense = true;

            // Define Fields: x, y, z
            msg.fields = new PointField[3];
            msg.fields[0] = new PointField { name = "x", offset = 0, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[1] = new PointField { name = "y", offset = 4, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[2] = new PointField { name = "z", offset = 8, datatype = PointField.FLOAT32, count = 1 };
            msg.point_step = 12;
            msg.row_step = msg.point_step * msg.width;

            // Convert Data
            byte[] byteArray = new byte[msg.row_step * msg.height];
            int offset = 0;
            foreach (var p in points)
            {
                // Raw Unity Coordinates
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(p.x), 0, byteArray, offset + 0, 4);
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(p.y), 0, byteArray, offset + 4, 4);
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(p.z), 0, byteArray, offset + 8, 4);
                offset += 12;
            }
            msg.data = byteArray;

            return msg;
        }
    }
}
