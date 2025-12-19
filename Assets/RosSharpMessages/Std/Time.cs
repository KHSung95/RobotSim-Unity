/* 
 * This message is auto-generated or manually created to match std_msgs/Time or builtin_interfaces/Time
 */

using Newtonsoft.Json;

namespace RosSharp.RosBridgeClient.MessageTypes.Std
{
    public class Time : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "std_msgs/Time"; // In ROS2, often builtin_interfaces/Time, but std_msgs/Time works via bridge usually

        public uint secs;
        public uint nsecs;

        public Time()
        {
            secs = 0;
            nsecs = 0;
        }

        public Time(uint secs, uint nsecs)
        {
            this.secs = secs;
            this.nsecs = nsecs;
        }
    }
}
