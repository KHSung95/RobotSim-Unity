using UnityEngine;
using UnityEngine.UI;
using RobotSim.Robot;
using RobotSim.Control;
using Unity.VisualScripting;

namespace RobotSim.UI
{
    /// <summary>
    /// Handles the Robot Jogging UI components, separating logic from UnifiedControlUI.
    /// Includes FK/IK mode switching, Speed control, and Axis jogging buttons.
    /// </summary>
    [System.Serializable]
    public class RobotJogUIHandler
    {
        [Header("References")]
        public RobotStateProvider StateProvider;
        public RosJogAdapter JogAdapter;

        [Header("UI Elements")]
        public Toggle FkToggle;
        public Toggle IkToggle;
        public Slider SpeedSlider;
        public RobotAxisRow[] AxisRows;

        private bool _isFKMode = true;

        public void Initialize(RobotStateProvider stateProvider, RosJogAdapter jogAdapter)
        {
            StateProvider = stateProvider;
            JogAdapter = jogAdapter;
            SetControllerMode(true);
        }

        public void BindEvents()
        {
            FkToggle?.onValueChanged.AddListener((v) => { if (v) SetControllerMode(true); });
            IkToggle?.onValueChanged.AddListener((v) => { if (v) SetControllerMode(false); });

            SpeedSlider?.onValueChanged.AddListener((v) =>
            {
                if (JogAdapter != null) JogAdapter.SpeedMultiplier = v;
            });

            if (AxisRows != null)
            {
                for (int i = 0; i < AxisRows.Length; i++)
                {
                    BindJogBtn(AxisRows[i].SubBtn, i, -1);
                    BindJogBtn(AxisRows[i].AddBtn, i, 1);
                }
            }
        }

        private void BindJogBtn(Button btn, int index, float dir)
        {
            if (btn == null) return;
            var jog = btn.gameObject.GetOrAddComponent<JogButton>();
            jog.AxisIndex = index;
            jog.Direction = dir;
        }

        public void SetControllerMode(bool isFK)
        {
            _isFKMode = isFK;

            FkToggle?.SetIsOnWithoutNotify(isFK);
            IkToggle?.SetIsOnWithoutNotify(!isFK);

            FkToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();

            InitializeLabels(isFK);
        }

        private void InitializeLabels(bool isFK)
        {
            if (AxisRows == null) return;
            if (isFK)
            {
                for (int i = 0; i < AxisRows.Length; i++)
                {
                    AxisRows[i].NameText.text = "J" + (i + 1);
                }
            }
            else
            {
                string[] ikLabels = { "X", "Y", "Z", "Rx", "Ry", "Rz" };
                for (int i = 0; i < AxisRows.Length && i < ikLabels.Length; i++)
                {
                    AxisRows[i].NameText.text = ikLabels[i];
                }
            }
        }

        public void Update()
        {
            float speed = SpeedSlider ? SpeedSlider.value : 0.5f;
            if (AxisRows == null) return;

            for (int i = 0; i < AxisRows.Length; i++)
            {
                var row = AxisRows[i];
                if (row == null) continue;

                var negJog = row.SubBtn.GetComponent<JogButton>();
                var posJog = row.AddBtn.GetComponent<JogButton>();

                float dir = 0;
                if (negJog != null && negJog.IsPressed) dir = -1;
                if (posJog != null && posJog.IsPressed) dir = 1;

                if (dir != 0 && JogAdapter != null)
                {
                    JogAdapter.SpeedMultiplier = speed;

                    if (_isFKMode)
                    {
                        JogAdapter.JointJog(i, dir);
                    }
                    else
                    {
                        // Calculate Twist Vector
                        Vector3 lin = Vector3.zero;
                        Vector3 ang = Vector3.zero;

                        if (i == 0) lin.z = dir;      // ROS X
                        else if (i == 1) lin.x = -dir; // ROS Y
                        else if (i == 2) lin.y = dir;  // ROS Z
                        else if (i == 3) ang.z = dir;  // ROS Rx
                        else if (i == 4) ang.x = -dir; // ROS Ry
                        else if (i == 5) ang.y = dir;  // ROS Rz

                        JogAdapter.Jog(lin, ang);
                    }
                }
            }

            if (_isFKMode) UpdateFKDisplay();
            else UpdateIKDisplay();
        }

        private void UpdateFKDisplay()
        {
            if (AxisRows == null || AxisRows.Length < 6 || StateProvider == null) return;
            float[] jointAngles = StateProvider.JointAnglesDegrees;
            if (jointAngles == null || jointAngles.Length < 6) return;

            for (int i = 0; i < 6; i++)
            {
                AxisRows[i].ValueText.text = $"{jointAngles[i]:F1}°";
            }
        }

        private void UpdateIKDisplay()
        {
            if (AxisRows == null || AxisRows.Length < 6 || StateProvider == null) return;
            Vector3 pos = StateProvider.TcpPositionRos;
            Vector3 rot = StateProvider.TcpRotationEulerRos;

            AxisRows[0].ValueText.text = $"{(pos.x * 1000):F1}";
            AxisRows[1].ValueText.text = $"{(pos.y * 1000):F1}";
            AxisRows[2].ValueText.text = $"{(pos.z * 1000):F1}";
            AxisRows[3].ValueText.text = $"{rot.x:F1}";
            AxisRows[4].ValueText.text = $"{rot.y:F1}";
            AxisRows[5].ValueText.text = $"{rot.z:F1}";
        }
    }
}
