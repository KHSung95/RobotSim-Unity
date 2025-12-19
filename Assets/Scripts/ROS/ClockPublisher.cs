using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Rosgraph;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosTime =RosSharp.RosBridgeClient.MessageTypes.Std.Time;

namespace RosSharp.RosBridgeClient
{
    public class ClockPublisher : UnityPublisher<Clock>
    {
        Clock message;

        protected override void Start()
        {
            base.Start();
            InitializeMessage();
        }

        private void InitializeMessage()
        {
            message = new Clock { clock = new RosTime() };
        }

        private void Update()
        {
            // Unity Time.time is seconds since start of game
            uint secs = (uint)UnityEngine.Time.time;
            uint nsecs = (uint)((UnityEngine.Time.time - secs) * 1e9);

            message.clock.secs = secs;
            message.clock.nsecs = nsecs;

            Publish(message);
        }
    }
}
