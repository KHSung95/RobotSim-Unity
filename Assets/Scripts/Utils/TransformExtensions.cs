using UnityEngine;
using System.Collections.Generic;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RobotSim.Sensors; // For PointData
using System;

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

        // --- Vector3 List Extensions (Legacy/Simple) ---

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

        public static List<Vector3> TransformPoints(this List<Vector3> points, Matrix4x4 matrix)
        {
            if (points == null) return null;
            var result = new List<Vector3>(points.Count);
            foreach (var p in points) result.Add(matrix.MultiplyPoint3x4(p));
            return result;
        }

        // --- PointData List Extensions (New) ---

        public static PointCloud2 ToPointCloud2(this List<PointData> points)
        {
            var msg = new PointCloud2();
            msg.header = new Header { frame_id = "unity_camera" }; // Or generic frame
            msg.height = 1;
            msg.width = (uint)points.Count;
            msg.is_bigendian = false;
            msg.is_dense = true;

            // Fields: x, y, z, normal_x, normal_y, normal_z, rgb
            msg.fields = new PointField[7];
            msg.fields[0] = new PointField { name = "x", offset = 0, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[1] = new PointField { name = "y", offset = 4, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[2] = new PointField { name = "z", offset = 8, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[3] = new PointField { name = "normal_x", offset = 12, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[4] = new PointField { name = "normal_y", offset = 16, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[5] = new PointField { name = "normal_z", offset = 20, datatype = PointField.FLOAT32, count = 1 };
            msg.fields[6] = new PointField { name = "rgb", offset = 24, datatype = PointField.FLOAT32, count = 1 }; // Packed float

            msg.point_step = 32; // 3 x 4 + 3 x 4 + 4 + 4(padding) = 32 bytes aligned
            msg.row_step = msg.point_step * msg.width;

            byte[] byteArray = new byte[msg.row_step * msg.height];
            int offset = 0;

            foreach (var p in points)
            {
                // Position
                Buffer.BlockCopy(BitConverter.GetBytes(p.Position.x), 0, byteArray, offset + 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(p.Position.y), 0, byteArray, offset + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(p.Position.z), 0, byteArray, offset + 8, 4);

                // Normal
                Buffer.BlockCopy(BitConverter.GetBytes(p.Normal.x), 0, byteArray, offset + 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(p.Normal.y), 0, byteArray, offset + 16, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(p.Normal.z), 0, byteArray, offset + 20, 4);

                // Color (Packed RGB -> Float)
                // ROS PCL expects: (int)r << 16 | (int)g << 8 | (int)b
                int rgbInt = (p.Color.r << 16) | (p.Color.g << 8) | p.Color.b;
                // Reinterpret as float
                float rgbFloat = BitConverter.ToSingle(BitConverter.GetBytes(rgbInt), 0);
                
                Buffer.BlockCopy(BitConverter.GetBytes(rgbFloat), 0, byteArray, offset + 24, 4);

                offset += 32;
            }
            msg.data = byteArray;
            return msg;
        }

        public static List<PointData> TransformPoints(this List<PointData> points, Matrix4x4 matrix)
        {
            if (points == null) return null;
            var result = new List<PointData>(points.Count);

            foreach (var p in points)
            {
                Vector3 newPos = matrix.MultiplyPoint3x4(p.Position);
                Vector3 newNorm = matrix.MultiplyVector(p.Normal).normalized; // Rotate normal
                
                result.Add(new PointData(newPos, newNorm, p.Color));
            }
            return result;
        }

        public static List<Vector3> Points(this List<PointData> points)
        {
            if (points == null) return null;
            var result = new List<Vector3>(points.Count);
            foreach (var p in points) result.Add(p.Position);
            return result;
        }
    }
}
