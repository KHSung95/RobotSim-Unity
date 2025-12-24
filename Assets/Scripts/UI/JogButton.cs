using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotSim.UI
{
    public class JogButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsPressed { get; private set; }
        public int AxisIndex;
        public float Direction; // +1 or -1

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
        }
    }
}
