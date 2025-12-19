/* 
 * This message is auto-generated or manually created to match rosgraph_msgs/Clock
 */

using Newtonsoft.Json;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RosSharp.RosBridgeClient.MessageTypes.Rosgraph
{
    public class Clock : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "rosgraph_msgs/Clock";

        public RosSharp.RosBridgeClient.MessageTypes.Std.Time clock;

        public Clock()
        {
            clock = new RosSharp.RosBridgeClient.MessageTypes.Std.Time();
        }

        public Clock(RosSharp.RosBridgeClient.MessageTypes.Std.Time clock)
        {
            this.clock = clock;
        }
    }
}
