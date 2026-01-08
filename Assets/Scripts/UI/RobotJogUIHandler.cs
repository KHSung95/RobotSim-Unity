using UnityEngine;
using UnityEngine.UI;
using RobotSim.Robot;
using RobotSim.Control;
using Unity.VisualScripting;
using UnityEngine.EventSystems;
using System;
using RosSharp;

namespace RobotSim.UI
{
    public class JogButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Settings")]
        public int AxisIndex;       // 0:X, 1:Y, 2:Z, 3:Rx, 4:Ry, 5:Rz
        public float Direction;     // 1: Positive, -1: Negative

        // 이벤트 최적화: Action을 통해 구독자에게 알림
        public static event Action<int, float> OnJogStateChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            // 버튼이 눌리면: 해당 축으로 Direction 속도 적용
            OnJogStateChanged?.Invoke(AxisIndex, Direction);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 버튼을 떼면: 속도를 0으로 보냄
            OnJogStateChanged?.Invoke(AxisIndex, 0f);
        }
    }
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

        private float[] currentInputs = new float[6];

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
            
            JogAdapter.SpeedMultiplier = 0.5f;
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
            JogButton.OnJogStateChanged += HandleJogInput;
        }

        private void BindJogBtn(Button btn, int index, float dir)
        {
            if (btn == null) return;
            var jog = btn.gameObject.GetOrAddComponent<JogButton>();
            jog.AxisIndex = index;
            jog.Direction = dir;
        }

        // 1. 이벤트 핸들러: 입력 상태만 갱신 (매우 가벼움)
        private void HandleJogInput(int axisIndex, float value)
        {
            if (axisIndex >= 0 && axisIndex < currentInputs.Length)
            {
                currentInputs[axisIndex] = value;
            }
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
            if (AxisRows == null) return;

            if (_isFKMode) UpdateFKDisplay();
            else UpdateIKDisplay();

            bool isAnyInput = false;
            for (int i = 0; i < 6; i++) if (currentInputs[i] != 0) isAnyInput = true;
            if (!isAnyInput) return;

            if (_isFKMode)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (currentInputs[i] != 0)
                    {
                        JogAdapter.JointJog(i, currentInputs[i]);
                        break;
                    }
                }
            }
            else
            {
                Vector3 moveDir = new Vector3(currentInputs[0], currentInputs[1], currentInputs[2]);
                Vector3 rotDir = new Vector3(currentInputs[3], currentInputs[4], currentInputs[5]);

                JogAdapter.Jog(moveDir, rotDir);
            }
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
            Vector3 pos = StateProvider.TcpPositionRos.Ros2Unity();
            Vector3 rot = StateProvider.TcpRotationEulerRos.Ros2Unity();

            AxisRows[0].ValueText.text = $"{(pos.x * 1000):F1}";
            AxisRows[1].ValueText.text = $"{(pos.y * 1000):F1}";
            AxisRows[2].ValueText.text = $"{(pos.z * 1000):F1}";
            AxisRows[3].ValueText.text = $"{rot.x:F1}";
            AxisRows[4].ValueText.text = $"{rot.y:F1}";
            AxisRows[5].ValueText.text = $"{rot.z:F1}";
        }
    }
}
