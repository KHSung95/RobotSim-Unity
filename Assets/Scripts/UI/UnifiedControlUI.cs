using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotSim.Sensors;
using RobotSim.Robot;
using RobotSim.Simulation;
using RobotSim.Utils;

namespace RobotSim.UI
{
    public class UnifiedControlUI : MonoBehaviour
    {
        [Header("References")]
        public GuidanceManager Guidance;
        public PointCloudGenerator PCG;
        public VirtualCameraMount CamMount;

        [Header("Scene Roots")]
        public Transform RobotBase;

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
        private bool _isHandEye = true;
        private Button _settingsOkBtn;

        // Control Panel
        private GameObject _controlPanel;
        private RobotAxisRow[] _rows;

        private void InitializeReferences()
        {
            if (Guidance == null) Guidance = FindObjectOfType<GuidanceManager>();
            if (PCG == null) PCG = FindObjectOfType<PointCloudGenerator>();
            if (CamMount == null) CamMount = FindObjectOfType<VirtualCameraMount>();
            if (RosJogAdapter == null) RosJogAdapter = FindFirstObjectByType<RobotSim.Control.RosJogAdapter>(FindObjectsInactive.Include);
            if (StateProvider == null) StateProvider = FindFirstObjectByType<RobotStateProvider>(FindObjectsInactive.Include);

            if (RobotBase == null)
            {
                var baseObj = GameObject.Find("base_link");
                if (baseObj != null) RobotBase = baseObj.transform;
                else RobotBase = GameObject.Find("ur5e")?.transform;
            }

            // Find UI Roots more robustly
            var canvas = FindObjectOfType<Canvas>();
            Transform uiRoot = canvas?.transform.Find("UIRoot");
            Navbar = uiRoot?.Find("Navbar")?.gameObject;
            if(Navbar != null)
            {
                _settingsToggle = FindUISub<Toggle>(Navbar, "Button_Settings");
                _masterToggle = FindUISub<Toggle>(Navbar, "Button_Master");
            }

            ConsolePanel = uiRoot?.Find("Console")?.gameObject;

            _settingsModal = uiRoot?.Find("SettingsModal")?.gameObject;
            if(_settingsModal != null)
            {
                _settingsThreshold = FindUISub<TMP_InputField>(_settingsModal, "Input_Threshold");
                _settingsOkBtn = FindUISub<Button>(_settingsModal, "Button_Ok");
            }

            Sidebar = uiRoot?.Find("Sidebar")?.gameObject;
            if (Sidebar != null)
            {
                _visionFeed = FindUISub<RawImage>(Sidebar, "Feed");
                _rgbToggle = FindUISub<Toggle>(Sidebar, "Toggle_RGB");
                _depthToggle = FindUISub<Toggle>(Sidebar, "Toggle_Depth");

                // Find Modules by Name (created by UIBuilder as title + "_Module")
                _operationModule = Sidebar.transform.FindDeepChild("OPERATION MODE_Module")?.gameObject;
                if(_operationModule)
                {
                    _captureBtn = FindUISub<Button>(_operationModule, "Capture");
                    _guidanceBtn = FindUISub<Button>(_operationModule, "Guidance");
                }
                _masterModule = Sidebar.transform.FindDeepChild("MASTER MODE_Module")?.gameObject;
                if(_masterModule)
                {
                    _captureMasterBtn = FindUISub<Button>(_masterModule, "CaptureMaster");
                }
                // Initialize state
                if (_masterModule) _masterModule.SetActive(false);
                if (_operationModule) _operationModule.SetActive(true);

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
            else
            {
                Debug.LogError("<b>[UnifiedControlUI]</b> Sidebar NOT found!");
            }
        }

        private void BindJogBtn(Button btn, int index, float dir)
        {
            if (btn == null) return;
            var jog = btn.GetComponent<JogButton>();
            if (jog == null) jog = btn.gameObject.AddComponent<JogButton>();
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
            if (Sidebar == null) return;
            
            if (_fkToggle) _fkToggle.onValueChanged.AddListener((v) => { if (v) SetMode(true); });
            if (_ikToggle) _ikToggle.onValueChanged.AddListener((v) => { if (v) SetMode(false); });

            if (_captureBtn) _captureBtn.onClick.AddListener(() =>
            {
                Guidance?.CaptureCurrent();
            });
            if (_guidanceBtn) _guidanceBtn.onClick.AddListener(() => Guidance?.RunGuidance());
            
            if (_captureMasterBtn) _captureMasterBtn.onClick.AddListener(() =>
            {
                Guidance?.CaptureMaster();
            });
            if (_rgbToggle) _rgbToggle.onValueChanged.AddListener((v) => { if (v) SetVisionMode(true); });
            if (_depthToggle) _depthToggle.onValueChanged.AddListener((v) => { if (v) SetVisionMode(false); });

            if (Navbar)
            {
                if (_masterToggle)
                {
                    _masterToggle.onValueChanged.AddListener(OnMasterModeChanged);
                }
                
                if (_settingsToggle)
                {
                    _settingsToggle.onValueChanged.AddListener((v) => {
                         if (_settingsModal) _settingsModal.SetActive(v);
                    });
                }
            }
            if (_settingsModal)
            {
                var _settingsCloseBtn = _settingsModal.transform.FindDeepChild("Button_Cancel")?.gameObject?.GetComponent<Button>();
                // Settings Modal 닫기 이벤트 추가
                if (_settingsCloseBtn != null)
                { 
                    bindSettingModalClose(ref _settingsCloseBtn);
                }
                var _settingsXBtn = _settingsModal.transform.FindDeepChild("Button_X")?.gameObject?.GetComponent<Button>();
                if (_settingsXBtn != null)
                {
                    bindSettingModalClose(ref _settingsXBtn);
                }
                if (_settingsOkBtn)
                {
                    bindSettingModalClose(ref _settingsOkBtn);
                }
            }
        }

        private void bindSettingModalClose(ref Button btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                _settingsModal.SetActive(false);
                if (_settingsToggle) _settingsToggle.isOn = false;
            });
        }

        private void OnMasterModeChanged(bool isMaster)
        {
            // Switch entire modules
            if (_operationModule) _operationModule.SetActive(!isMaster);
            if (_masterModule) _masterModule.SetActive(isMaster);
        }

        public void SetVisionMode(bool isRGB)
        {
            if (_rgbToggle) _rgbToggle.SetIsOnWithoutNotify(isRGB);
            if (_depthToggle) _depthToggle.SetIsOnWithoutNotify(!isRGB);

            // Sync visuals manually since we used SetIsOnWithoutNotify
            _rgbToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();
            if (CamMount != null)
            {
                var cam = CamMount.GetComponent<Camera>();
                if (cam)
                {
                    if (cam.targetTexture == null)
                    {
                        RenderTexture rt = new RenderTexture(512, 512, 16);
                        rt.name = "VisionRT";
                        cam.targetTexture = rt;
                    }

                    if (_visionFeed != null)
                    {
                        _visionFeed.texture = cam.targetTexture;
                        _visionFeed.color = Color.white;
                    }
                }
            }
        }

        private void Start()
        {
            InitializeReferences();
            BindEvents();
            SetMode(true);
            SetVisionMode(true);
        }

        public void SetMode(bool isFK)
        {
            _isFKMode = isFK;
            
            // Sync toggles without triggering events
            if (_fkToggle) _fkToggle.SetIsOnWithoutNotify(isFK);
            if (_ikToggle) _ikToggle.SetIsOnWithoutNotify(!isFK);

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
                if(row == null) continue;

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

        public void SetCameraMount(bool isHandEye)
        {
            if (CamMount == null) return;
        }
    }

}
