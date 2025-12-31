/* 
 * Custom definition for ROS2 control_msgs/JointJog 
 * Removing 'accelerations' which causes deserialization errors in rosbridge/ROS2.
 */

using Newtonsoft.Json;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace RosSharp.RosBridgeClient.MessageTypes.Control
{
    public class JointJogRos2 : Message
    {
        [JsonIgnore]
        public const string RosMessageName = "control_msgs/JointJog";

        public Header header;
        public string[] joint_names;
        public double[] displacements;
        public double[] velocities;
        public double duration;

        public JointJogRos2()
        {
            header = new Header();
            joint_names = new string[0];
            displacements = new double[0];
            velocities = new double[0];
            duration = 0.0;
        }
    }
}
