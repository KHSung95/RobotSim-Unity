using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using Newtonsoft.Json;

namespace RosSharp.RosBridgeClient.MessageTypes.Custom
{
    public class ProcessSceneRequest : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "custom_services/ProcessScene";

        public string command;
        public float threshold;
        public PointCloud2 input_cloud;

        public ProcessSceneRequest()
        {
            this.command = "";
            this.threshold = 0.0f;
            this.input_cloud = new PointCloud2();
        }

        public ProcessSceneRequest(string command, float threshold, PointCloud2 input_cloud)
        {
            this.command = command;
            this.threshold = threshold;
            this.input_cloud = input_cloud;
        }
    }

    public class ProcessSceneResponse : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "custom_services/ProcessScene";

        public bool success;
        public float match_score;
        public PointCloud2 result_cloud;
        public float[] correction_matrix; // float32[16]

        public ProcessSceneResponse()
        {
            this.success = false;
            this.match_score = 0.0f;
            this.result_cloud = new PointCloud2();
            this.correction_matrix = new float[16];
        }
    }
}
