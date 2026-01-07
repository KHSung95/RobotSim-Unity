using UnityEngine;
using System.Collections.Generic;
using System;

namespace RobotSim.Utils
{
    /// <summary>
    /// A thread-safe class which holds a queue with actions to execute on the next Update() method.
    /// It can be used to make calls to the main thread for functionality that comes from other threads
    /// (e.g. ROS Websocket callbacks).
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static UnityMainThreadDispatcher _instance;

        public static UnityMainThreadDispatcher Instance()
        {
            if (!_instance)
            {
                _instance = FindFirstObjectByType<UnityMainThreadDispatcher>();
                if (!_instance)
                {
                    var obj = new GameObject("UnityMainThreadDispatcher");
                    _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}
