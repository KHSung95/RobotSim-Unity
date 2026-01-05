using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace RobotSim.UI
{
    /// <summary>
    /// Manages a group of Toggles to behave like Tabs with persistent color states.
    /// This ensures that the active tab remains highlighted (e.g. Cyan/Black) 
    /// until another tab in the same ToggleGroup is selected.
    /// </summary>
    public class ToggleTabManager : MonoBehaviour
    {
        [System.Serializable]
        public class TabItem
        {
            public Toggle Toggle;
            public Image Background;
            public TextMeshProUGUI Text;
        }

        [Header("Tabs Configuration")]
        public List<TabItem> Tabs = new List<TabItem>();

        [Header("Visual Theme")]
        public Color ActiveColor = new Color(0f, 0.8f, 1f, 1f);          // Neon Cyan
        public Color InactiveColor = new Color(0.12f, 0.12f, 0.15f, 1f); // Slate Panel
        public Color ActiveTextColor = Color.black;
        public Color InactiveTextColor = new Color(0.6f, 0.6f, 0.7f, 1f); // Steel Gray

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            foreach (var tab in Tabs)
            {
                if (tab.Toggle != null)
                {
                    tab.Toggle.onValueChanged.AddListener((isOn) => UpdateVisuals());
                }
            }
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            foreach (var tab in Tabs)
            {
                if (tab.Toggle == null) continue;

                bool isOn = tab.Toggle.isOn;

                if (tab.Background != null)
                    tab.Background.color = isOn ? ActiveColor : InactiveColor;

                if (tab.Text != null)
                    tab.Text.color = isOn ? ActiveTextColor : InactiveTextColor;
            }
        }

        // Helper to find a tab by name (for programmatic setups)
        public TabItem GetTabByName(string name)
        {
            return Tabs.Find(t => t.Toggle != null && t.Toggle.name.Contains(name));
        }
    }
}
