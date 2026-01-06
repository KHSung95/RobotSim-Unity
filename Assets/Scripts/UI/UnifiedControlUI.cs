using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotSim.Sensors;
using RobotSim.Robot;
using RobotSim.Simulation;
using RobotSim.Utils;
using Unity.VisualScripting;

namespace RobotSim.UI
{
    public class UnifiedControlUI : MonoBehaviour
    {
        [Header("References")]
        public GuidanceManager Guidance;
        public PointCloudGenerator PCG;
        public VirtualCameraMount CamMount;

        [Header("Controllers")]
        public Robot.RobotStateProvider StateProvider;
        public Control.RosJogAdapter RosJogAdapter;

        private GameObject Navbar;
        private Toggle _settingsToggle, _masterToggle;

        private GameObject ConsolePanel;

        private GameObject Sidebar;

        // Vision Reference
        private RawImage _visionFeed;
        private Toggle _rgbToggle, _depthToggle;

        // Operation References
        private Button _captureMasterBtn;
        private Button _captureBtn, _guidanceBtn;

        // Jogging References
        private bool _isFKMode = true;
        private Toggle _fkToggle, _ikToggle;
        private Slider _speedSlider;
        private Button _eStopBtn;

        // Panel Switch References (Modules)
        private GameObject _operationModule;
        private GameObject _masterModule;

        // Modal References
        private GameObject _settingsModal;
        private TMP_InputField _settingsThreshold;
        private Button _settingsOkBtn;

        // Camera Mount Settings
        private Toggle _handEyeToggle;
        private Toggle _birdEyeToggle;

        // Control Panel
        private GameObject _controlPanel;
        private RobotAxisRow[] _rows;

        private void InitializeReferences()
        {
            Guidance ??= FindObjectOfType<GuidanceManager>();
            PCG ??= FindObjectOfType<PointCloudGenerator>();
            CamMount ??= FindObjectOfType<VirtualCameraMount>();
            RosJogAdapter ??= FindFirstObjectByType<Control.RosJogAdapter>(FindObjectsInactive.Include);
            StateProvider ??= FindFirstObjectByType<RobotStateProvider>(FindObjectsInactive.Include);

            // Find UI Roots more robustly
            Transform uiRoot = FindObjectOfType<Canvas>()?.transform.Find("UIRoot");

            Navbar = uiRoot?.Find("Navbar")?.gameObject;
            if (Navbar != null)
            {
                _masterToggle = FindUISub<Toggle>(Navbar, "Button_Master");
                _settingsToggle = FindUISub<Toggle>(Navbar, "Button_Settings");
            }

            ConsolePanel = uiRoot?.Find("Console")?.gameObject;

            Sidebar = uiRoot?.Find("Sidebar")?.gameObject;
            if (Sidebar != null)
            {
                _visionFeed = FindUISub<RawImage>(Sidebar, "Feed");
                _rgbToggle = FindUISub<Toggle>(Sidebar, "Toggle_RGB");
                _depthToggle = FindUISub<Toggle>(Sidebar, "Toggle_Depth");

                // Find Modules by Name (created by UIBuilder as title + "_Module")
                _operationModule = Sidebar.transform.FindDeepChild("OPERATION MODE_Module")?.gameObject;
                if (_operationModule)
                {
                    _captureBtn = FindUISub<Button>(_operationModule, "Capture");
                    _guidanceBtn = FindUISub<Button>(_operationModule, "Guidance");
                }
                _masterModule = Sidebar.transform.FindDeepChild("MASTER MODE_Module")?.gameObject;
                if (_masterModule)
                {
                    _captureMasterBtn = FindUISub<Button>(_masterModule, "CaptureMaster");
                }
                // Initialize state
                _masterModule?.SetActive(false);
                _operationModule?.SetActive(true);

                _fkToggle = FindUISub<Toggle>(Sidebar, "Toggle_FK");
                _ikToggle = FindUISub<Toggle>(Sidebar, "Toggle_IK");

                _speedSlider = FindUISub<Slider>(Sidebar, "Slider");
                _eStopBtn = FindUISub<Button>(Sidebar, "EStop");

                _controlPanel = Sidebar.transform.FindDeepChild("ControlPanel")?.gameObject;

                if (_controlPanel != null)
                {
                    _rows = _controlPanel.GetComponentsInChildren<RobotAxisRow>();
                    for (int i = 0; i < _rows.Length; i++)
                    {
                        // Bind JogButtons explicitly to the buttons found in RobotAxisRow
                        BindJogBtn(_rows[i].SubBtn, i, -1);
                        BindJogBtn(_rows[i].AddBtn, i, 1);
                    }
                }
            }

            _settingsModal = uiRoot?.Find("SettingsModal")?.gameObject;
            if (_settingsModal != null)
            {
                _settingsThreshold = FindUISub<TMP_InputField>(_settingsModal, "Input_Threshold");
                _settingsOkBtn = FindUISub<Button>(_settingsModal, "Button_Ok");
                _handEyeToggle = FindUISub<Toggle>(_settingsModal, "Toggle_Handeye");
                _birdEyeToggle = FindUISub<Toggle>(_settingsModal, "Toggle_Birdeye");
            }
        }

        private void BindJogBtn(Button btn, int index, float dir)
        {
            if (btn == null) return;

            var jog = btn.GetOrAddComponent<JogButton>();
            jog.AxisIndex = index;
            jog.Direction = dir;
        }

        private T FindUISub<T>(GameObject root, string name) where T : Component
        {
            var t = root.transform.FindDeepChild(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        private void BindEvents()
        {
            // [수정] Sidebar가 없어도 Navbar나 SettingsModal은 동작해야 함 (Early Return 제거)
            if (Sidebar != null)
            {
                _fkToggle?.onValueChanged.AddListener((v) => { if (v) SetControllerMode(true); });
                _ikToggle?.onValueChanged.AddListener((v) => { if (v) SetControllerMode(false); });

                _captureBtn?.onClick.AddListener(() => Guidance?.CaptureCurrent());
                _guidanceBtn?.onClick.AddListener(() => Guidance?.RunGuidance());

                _rgbToggle?.onValueChanged.AddListener((v) => { if (v) SetVisionMode(true); });
                _depthToggle?.onValueChanged.AddListener((v) => { if (v) SetVisionMode(false); });

                _speedSlider?.onValueChanged.AddListener((v) =>
                {
                    if (RosJogAdapter != null) RosJogAdapter.SpeedMultiplier = v;
                });
            }

            if (Navbar != null)
            {
                _masterToggle?.onValueChanged.AddListener(OnMasterModeChanged);
                _settingsToggle?.onValueChanged.AddListener((v) =>
                {
                    _settingsModal?.SetActive(v);
                });
            }

            if (_masterModule != null)
            {
                _captureMasterBtn?.onClick.AddListener(() => Guidance?.CaptureMaster());
            }

            if (_settingsModal != null)
            {
                var _settingsCloseBtn = _settingsModal.transform.FindDeepChild("Button_Cancel")?.GetComponent<Button>();
                if (_settingsCloseBtn) bindSettingModalClose(ref _settingsCloseBtn);

                var _settingsXBtn = _settingsModal.transform.FindDeepChild("Button_X")?.GetComponent<Button>();
                if (_settingsXBtn) bindSettingModalClose(ref _settingsXBtn);

                if (_settingsOkBtn) bindSettingModalClose(ref _settingsOkBtn);

                 _handEyeToggle?.onValueChanged.AddListener((v) => { if (v) SetCameraMountMode(true); });
                 _birdEyeToggle?.onValueChanged.AddListener((v) => { if (v) SetCameraMountMode(false); });
            }
        }

        private void bindSettingModalClose(ref Button btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                _settingsModal.SetActive(false);
                if (_settingsToggle) _settingsToggle.isOn = false;
            });
        }

        private void OnMasterModeChanged(bool isMaster)
        {
            // Switch entire modules
            _operationModule?.SetActive(!isMaster);
            _masterModule?.SetActive(isMaster);
        }

        public void SetVisionMode(bool isRGB)
        {
             _rgbToggle?.SetIsOnWithoutNotify(isRGB);
             _depthToggle?.SetIsOnWithoutNotify(!isRGB);

            // Sync visuals manually since we used SetIsOnWithoutNotify
            _rgbToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();
            if (CamMount != null)
            {
                RenderTexture rt = new RenderTexture(512, 512, 16);
                    rt.name = "VisionRT";
                if (_visionFeed != null)
                {
                    _visionFeed.texture = rt;
                }
                CamMount.Cam.targetTexture = rt;
                CamMount.GetComponent<VisionModeController>()?.SetVisionMode(isRGB);
            }
        }

        private void Start()
        {
            InitializeReferences();
            BindEvents();
            SetControllerMode(true);
            SetVisionMode(true);
            SetCameraMountMode(true);
        }

        public void SetControllerMode(bool isFK)
        {
            _isFKMode = isFK;

            // Sync toggles without triggering events
            _fkToggle?.SetIsOnWithoutNotify(isFK);
            _ikToggle?.SetIsOnWithoutNotify(!isFK);

            // Sync visuals manually since we used SetIsOnWithoutNotify
            _fkToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();

            InitializeLabels(isFK);
        }

        private void InitializeLabels(bool isFK)
        {
            if (_rows == null) return;
            if (isFK)
            {
                // Revert to original generic names
                for (int i = 0; i < _rows.Length; i++)
                {
                    _rows[i].NameText.text = "J" + (i + 1);
                }
            }
            else
            {
                // Use Rx, Ry, Rz instead of Pitch/Yaw/Roll
                string[] ikLabels = { "X", "Y", "Z", "Rx", "Ry", "Rz" };
                for (int i = 0; i < _rows.Length && i < ikLabels.Length; i++)
                {
                    _rows[i].NameText.text = ikLabels[i];
                }
            }
        }


        private void Update()
        {
            float speed = _speedSlider ? _speedSlider.value : 0.5f;
            if (_rows == null) return;

            // Check jogging using the RobotAxisRow buttons components
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (row == null) continue;

                var negJog = row.SubBtn.GetComponent<JogButton>();
                var posJog = row.AddBtn.GetComponent<JogButton>();

                float dir = 0;
                if (negJog != null && negJog.IsPressed) dir = -1;
                if (posJog != null && posJog.IsPressed) dir = 1;

                if (dir != 0 && RosJogAdapter != null)
                {
                    // Sync speed multiplier with slider
                    RosJogAdapter.SpeedMultiplier = speed;

                    if (_isFKMode)
                    {
                        // FK Mode: Joint Jogging via MoveIt Servo
                        RosJogAdapter.JointJog(i, dir);
                    }
                    else
                    {
                        // IK Mode: Cartesian Jogging via MoveIt Servo (Twist)
                        // Align UI interactions with ROS coordinate display (X, Y, Z, Rx, Ry, Rz)
                        Vector3 lin = Vector3.zero;
                        Vector3 ang = Vector3.zero;

                        // Based on RobotStateProvider.cs conversion:
                        // ROS X (Forward) = Unity Z
                        // ROS Y (Left)    = Unity -X
                        // ROS Z (Up)      = Unity Y

                        if (i == 0) // UI X (ROS X)
                            lin.z = dir;
                        else if (i == 1) // UI Y (ROS Y)
                            lin.x = -dir;
                        else if (i == 2) // UI Z (ROS Z)
                            lin.y = dir;
                        else if (i == 3) // UI Rx (ROS Rx)
                            ang.z = dir;
                        else if (i == 4) // UI Ry (ROS Ry)
                            ang.x = -dir;
                        else if (i == 5) // UI Rz (ROS Rz)
                            ang.y = dir;

                        RosJogAdapter.Jog(lin, ang);
                    }
                }
            }

            // Only update the active display to prevent overwriting shared text fields
            if (_isFKMode)
                UpdateFKDisplay();
            else
                UpdateIKDisplay();
        }

        private void UpdateFKDisplay()
        {
            if (_rows == null || _rows.Length < 6 || StateProvider == null) return;

            float[] jointAngles = StateProvider.JointAnglesDegrees;
            if (jointAngles == null || jointAngles.Length < 6) return;

            for (int i = 0; i < 6; i++)
            {
                _rows[i].ValueText.text = $"{jointAngles[i]:F1}°";
            }
        }

        private void UpdateIKDisplay()
        {
            if (_rows == null || _rows.Length < 6 || StateProvider == null) return;

            Vector3 pos = StateProvider.TcpPositionRos;
            Vector3 rot = StateProvider.TcpRotationEulerRos;

            // X, Y, Z (m -> mm)
            _rows[0].ValueText.text = $"{(pos.x * 1000):F1}";
            _rows[1].ValueText.text = $"{(pos.y * 1000):F1}";
            _rows[2].ValueText.text = $"{(pos.z * 1000):F1}";

            // R, P, Y (Euler)
            _rows[3].ValueText.text = $"{rot.x:F1}";
            _rows[4].ValueText.text = $"{rot.y:F1}";
            _rows[5].ValueText.text = $"{rot.z:F1}";
        }

        public void SetCameraMountMode(bool isHandEye)
        {
            // [추가] CamMount 내부 로직 호출 (Transform Parent 변경 및 갱신 포함)
            CamMount?.SetMountMode(isHandEye ? CameraMountType.HandEye : CameraMountType.BirdEye);

            // [추가] UI Toggle 상태 동기화
            _handEyeToggle?.SetIsOnWithoutNotify(isHandEye);
            _birdEyeToggle?.SetIsOnWithoutNotify(!isHandEye);

            // [추가] Toggle 시각적 상태 업데이트 (ToggleTabManager가 있을 경우)
            _handEyeToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();

            // 성공 로그
            Debug.Log($"[UnifiedControlUI] Camera Mount switched to: {(isHandEye ? "Hand-Eye" : "Bird-Eye")}");
        }
    }
}
