using UnityEngine;
using UnityEngine.UI;

namespace RobotSim.UI
{
    /// <summary>
    /// Handles visual state changes for NavBar buttons (Sprite swap + Color tint).
    /// </summary>
    public class NavBarToggle : MonoBehaviour
    {
        public Toggle Toggle;
        public Image TargetImage;
        
        [Header("Sprites")]
        public Sprite IconOff;
        public Sprite IconOn;
        
        [Header("Colors")]
        public Color ActiveColor = Color.white;
        public Color InactiveColor = new Color(0.6f, 0.6f, 0.7f, 1f); // Theme.TextDim

        private void Start()
        {
            if (Toggle != null)
            {
                Toggle.onValueChanged.AddListener(UpdateVisuals);
                // Initialize
                UpdateVisuals(Toggle.isOn);
            }
        }
        
        public void UpdateVisuals(bool isOn)
        {
            if (TargetImage == null) return;

            // Swap Sprite
            if (IconOn != null && IconOff != null)
            {
                TargetImage.sprite = isOn ? IconOn : IconOff;
            }

            // Apply Tint
            TargetImage.color = isOn ? ActiveColor : InactiveColor;
        }
        
        public void ForceUpdate()
        {
             if (Toggle != null) UpdateVisuals(Toggle.isOn);
        }
    }
}
