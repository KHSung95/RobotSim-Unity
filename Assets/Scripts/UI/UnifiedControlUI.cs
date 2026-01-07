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

        [Header("Controllers")]
        public Robot.RobotStateProvider StateProvider;
        public Control.RosJogAdapter RosJogAdapter;
        
        private RobotJogUIHandler JogHandler = new();

        private GameObject Navbar;
        private Toggle _settingsToggle, _masterToggle;
        private GameObject ConsolePanel;
        private GameObject Sidebar;
        
        // Vision Reference
        private RawImage _visionFeed;
        private Toggle _masterViewToggle;

        // Operation References
        private Button _captureMasterBtn;
        private Button _captureBtn, _guidanceBtn;

        // Common References
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

        private void InitializeReferences()
        {
            Guidance ??= FindObjectOfType<GuidanceManager>();
            PCG ??= FindObjectOfType<PointCloudGenerator>();
            CamMount ??= FindObjectOfType<VirtualCameraMount>();
            RosJogAdapter ??= FindFirstObjectByType<RobotSim.Control.RosJogAdapter>(FindObjectsInactive.Include);
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
                _masterViewToggle = FindUISub<Toggle>(Sidebar, "Toggle_Master Data");

                // Find Modules by Name
                _operationModule = Sidebar.transform.FindDeepChild("OPERATION MODE_Module")?.gameObject;
                if (_operationModule != null)
                {
                    _captureBtn = FindUISub<Button>(_operationModule, "Capture");
                    _guidanceBtn = FindUISub<Button>(_operationModule, "Guidance");
                }
                _masterModule = Sidebar.transform.FindDeepChild("MASTER MODE_Module")?.gameObject;
                if (_masterModule != null)
                {
                    _captureMasterBtn = FindUISub<Button>(_masterModule, "CaptureMaster");
                }
                
                // Initialize state
                _masterModule?.SetActive(false);
                _operationModule?.SetActive(true);

                _eStopBtn = FindUISub<Button>(Sidebar, "EStop");

                // Initialize Jog Handler
                JogHandler.FkToggle = FindUISub<Toggle>(Sidebar, "Toggle_FK");
                JogHandler.IkToggle = FindUISub<Toggle>(Sidebar, "Toggle_IK");
                JogHandler.SpeedSlider = FindUISub<Slider>(Sidebar, "Slider");
                
                _controlPanel = Sidebar.transform.FindDeepChild("ControlPanel")?.gameObject;
                if (_controlPanel != null)
                {
                    JogHandler.AxisRows = _controlPanel.GetComponentsInChildren<RobotAxisRow>();
                }

                JogHandler.Initialize(StateProvider, RosJogAdapter);
            }

            _settingsModal = uiRoot?.Find("SettingsModal")?.gameObject;
            if (_settingsModal != null)
            {
                Debug.Log("[UnifiedControlUI] SettingsModal found.");
                _settingsThreshold = FindUISub<TMP_InputField>(_settingsModal, "Input_Threshold");
                _settingsOkBtn = FindUISub<Button>(_settingsModal, "Button_Ok");
                _handEyeToggle = FindUISub<Toggle>(_settingsModal, "Toggle_Handeye");
                _birdEyeToggle = FindUISub<Toggle>(_settingsModal, "Toggle_Birdeye");
                
                if (_handEyeToggle == null) Debug.LogWarning("[UnifiedControlUI] Toggle_Handeye NOT found in SettingsModal!");
                if (_birdEyeToggle == null) Debug.LogWarning("[UnifiedControlUI] Toggle_Birdeye NOT found in SettingsModal!");
            }
            else
            {
                Debug.LogWarning("[UnifiedControlUI] SettingsModal NOT found under UIRoot!");
            }
        }

        private T FindUISub<T>(GameObject root, string name) where T : Component
        {
            var t = root.transform.FindDeepChild(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        private void BindEvents()
        {
            if (Sidebar != null)
            {
                JogHandler.BindEvents();
                
                _captureBtn?.onClick.AddListener(() => Guidance?.AnalyzeScene());
                _guidanceBtn?.onClick.AddListener(() => Guidance?.RunGuidance());

                _masterViewToggle?.onValueChanged.AddListener(v => { PCG?.ToggleMasterDataRender(v); });
            }

            if (Navbar != null)
            {
                _masterToggle?.onValueChanged.AddListener(OnMasterModeChanged);
                _settingsToggle?.onValueChanged.AddListener(v => _settingsModal?.SetActive(v));
            }

            if (_masterModule != null)
            {
                _captureMasterBtn?.onClick.AddListener(() => Guidance?.CaptureMaster());
            }

            if (_settingsModal != null)
            {
                bindSettingModalClose("Button_Close");
                bindSettingModalClose("Button_X");
                bindSettingModalClose("Button_Ok");

                _handEyeToggle?.onValueChanged.AddListener(v => { if (v) SetCameraMountMode(true); });
                _birdEyeToggle?.onValueChanged.AddListener(v => { if (v) SetCameraMountMode(false); });
            }
        }

        private void bindSettingModalClose(string btnName)
        {
            var btn = _settingsModal.transform.FindDeepChild(btnName)?.GetComponent<Button>();
            btn?.onClick.RemoveAllListeners();
            btn?.onClick.AddListener(() =>
            {
                _settingsModal?.SetActive(false);
                if (_settingsToggle != null) _settingsToggle.isOn = false;
            });
        }

        private void OnMasterModeChanged(bool isMaster)
        {
            _operationModule?.SetActive(!isMaster);
            _masterModule?.SetActive(isMaster);
        }

        private void BindVisionFeed()
        {
            RenderTexture rt = new RenderTexture(512, 512, 16) { name = "VisionRT" };

            if (_visionFeed != null)
                _visionFeed.texture = rt;

            CamMount?.setTargetTexture(rt);
        }

        private void Start()
        {
            InitializeReferences();
            BindEvents();
            BindVisionFeed();
            SetCameraMountMode(true);
        }

        private void Update() => JogHandler.Update();

        public void SetCameraMountMode(bool isHandEye)
        {
            CamMount?.SetMountMode(isHandEye ? CameraMountType.HandEye : CameraMountType.BirdEye);

            _handEyeToggle?.SetIsOnWithoutNotify(isHandEye);
            _birdEyeToggle?.SetIsOnWithoutNotify(!isHandEye);

            _handEyeToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();

            Debug.Log($"[UnifiedControlUI] Camera Mount switched to: {(isHandEye ? "Hand-Eye" : "Bird-Eye")}");
        }
    }
}