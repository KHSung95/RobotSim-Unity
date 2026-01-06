using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using Newtonsoft.Json;

namespace RosSharp.RosBridgeClient.MessageTypes.CustomServices
{
    public class CalculateICPRequest : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "custom_services/CalculateICP";

        public PointCloud2 master_point_cloud;
        public PointCloud2 current_point_cloud;

        public CalculateICPRequest()
        {
            this.master_point_cloud = new PointCloud2();
            this.current_point_cloud = new PointCloud2();
        }

        public CalculateICPRequest(PointCloud2 master, PointCloud2 current)
        {
            this.master_point_cloud = master;
            this.current_point_cloud = current;
        }
    }

    public class CalculateICPResponse : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "custom_services/CalculateICP";

        public float[] transformation_matrix;

        public CalculateICPResponse()
        {
            this.transformation_matrix = new float[16];
        }
    }
}
