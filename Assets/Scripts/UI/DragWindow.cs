using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotSim.UI
{
    public class DragWindow : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        private RectTransform dragRectTransform;
        private Canvas canvas;

        void Awake()
        {
            dragRectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            dragRectTransform.SetAsLastSibling(); // Bring to front
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (canvas == null) return;
            dragRectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }
    }
}
