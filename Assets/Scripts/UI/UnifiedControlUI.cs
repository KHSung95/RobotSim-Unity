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
        public VirtualCameraMount CamMount;

        [Header("Controllers")]
        public RobotStateProvider StateProvider;
        public Control.RosJogAdapter RosJogAdapter;
        
        private RobotJogUIHandler JogHandler = new();

        private GameObject Navbar;
        private Toggle _settingsToggle, _masterToggle;
        private Button _quitBtn;
        private GameObject ConsolePanel;
        private TextMeshProUGUI _consoleText;
        private System.Collections.Generic.Queue<string> _logQueue = new();
        private const int MaxLogLines = 50;
        private GameObject Sidebar;
        
        // Vision Reference
        private RawImage _visionFeed;
        private Toggle _pointViewToggle;

        // Operation References
        private Button _captureMasterBtn;
        private Button _captureBtn, _guidanceBtn, _syncBtn;

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

        // Temporary Settings State (for Apply on OK)
        private string _tempThresholdStr;
        private bool _tempIsHandEye;

        private void InitializeReferences()
        {
            Guidance ??= FindObjectOfType<GuidanceManager>();
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
                _quitBtn = FindUISub<Button>(Navbar, "Button_Quit");
            }

            ConsolePanel = uiRoot?.Find("Console")?.gameObject;
            if (ConsolePanel != null)
            {
                _consoleText = ConsolePanel.transform.FindDeepChild("Text")?.GetComponent<TextMeshProUGUI>();
            }
            Sidebar = uiRoot?.Find("Sidebar")?.gameObject;
            if (Sidebar != null)
            {
                _visionFeed = FindUISub<RawImage>(Sidebar, "Feed");
                _pointViewToggle = FindUISub<Toggle>(Sidebar, "Toggle_Pointcloud");

                // Find Modules by Name
                _operationModule = Sidebar.transform.FindDeepChild("OPERATION MODE_Module")?.gameObject;
                if (_operationModule != null)
                {
                    _captureBtn = FindUISub<Button>(_operationModule, "Capture");
                    _guidanceBtn = FindUISub<Button>(_operationModule, "Guidance");
                    _syncBtn = FindUISub<Button>(_operationModule, "Sync");
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
                _settingsThreshold = FindUISub<TMP_InputField>(_settingsModal, "Input_Threshold");
                _settingsOkBtn = FindUISub<Button>(_settingsModal, "Button_Ok");
                _handEyeToggle = FindUISub<Toggle>(_settingsModal, "Toggle_Handeye");
                _birdEyeToggle = FindUISub<Toggle>(_settingsModal, "Toggle_Birdeye");
                
                if (_handEyeToggle == null) Debug.LogWarning("[UnifiedControlUI] Toggle_Handeye NOT found in SettingsModal!");
                if (_birdEyeToggle == null) Debug.LogWarning("[UnifiedControlUI] Toggle_Birdeye NOT found in SettingsModal!");
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
                _syncBtn?.onClick.AddListener(() => Guidance?.SyncSceneToRos());

                _pointViewToggle?.onValueChanged.AddListener(v => {
                    Guidance?.SetPointCloudVisible(v);
                });
            }

            if (Navbar != null)
            {
                _masterToggle?.onValueChanged.AddListener(OnMasterModeChanged);
                _settingsToggle?.onValueChanged.AddListener(v => {
                    if (_settingsModal != null)
                    {
                        _settingsModal.SetActive(v);
                        if (v && Guidance != null && _settingsThreshold != null)
                        {
                            // Initialize temp state from current settings
                            _tempThresholdStr = (Guidance.ErrorThreshold * 1000f).ToString("F1");
                            _tempIsHandEye = (CamMount.MountType == CameraMountType.HandEye);
                            
                            _settingsThreshold.text = _tempThresholdStr;
                            _handEyeToggle?.SetIsOnWithoutNotify(_tempIsHandEye);
                            _birdEyeToggle?.SetIsOnWithoutNotify(!_tempIsHandEye);
                            
                            // Visual refresh for modal toggles
                            _handEyeToggle?.GetComponentInParent<ToggleTabManager>()?.UpdateVisuals();
                        }
                    }
                });
                _quitBtn?.onClick.AddListener(() => {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
            }

            if (_masterModule != null)
            {
                _captureMasterBtn?.onClick.AddListener(() =>
                {
                    Guidance?.CaptureMaster();
                    // Just set isOn = false, which triggers onValueChanged listener 
                    // and ensures ToggleTabManager visuals update.
                    if (_masterToggle != null) _masterToggle.isOn = false;
                });
            }

            if (_settingsModal != null)
            {
                bindSettingModalClose("Button_Cancel");
                bindSettingModalClose("Button_X");
                
                _handEyeToggle?.onValueChanged.AddListener(v => { if (v) _tempIsHandEye = true; });
                _birdEyeToggle?.onValueChanged.AddListener(v => { if (v) _tempIsHandEye = false; });

                _settingsThreshold?.onValueChanged.AddListener(v => _tempThresholdStr = v);
                
                _settingsOkBtn?.onClick.AddListener(() => {
                    ApplySettingsFromModal();
                    _settingsModal?.SetActive(false);
                    if (_settingsToggle != null) _settingsToggle.isOn = false;
                });
            }
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (_consoleText == null) return;

            string color = "white";
            if (type == LogType.Error || type == LogType.Exception) color = "#ff4444";
            else if (type == LogType.Warning) color = "#ffbb00";
            else if (type == LogType.Log) color = "#cccccc";

            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string entry = $"<color=#888888>[{timestamp}]</color> <color={color}>{logString}</color>";

            _logQueue.Enqueue(entry);
            while (_logQueue.Count > MaxLogLines)
                _logQueue.Dequeue();

            _consoleText.text = string.Join("\n", _logQueue.ToArray());
        }

        private void ApplySettingsFromModal()
        {
            // 1. Apply Threshold
            if (!string.IsNullOrEmpty(_tempThresholdStr))
            {
                if (float.TryParse(_tempThresholdStr, out float res))
                {
                    if (Guidance != null)
                    {
                        float thresholdInMeters = res / 1000f;
                        Guidance.ErrorThreshold = thresholdInMeters;
                        Debug.Log($"[UnifiedControlUI] Threshold applied: {res}mm");
                    }
                }
            }

            // 2. Apply Camera Mode
            SetCameraMountMode(_tempIsHandEye);
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
            Screen.fullScreen = false; // Ensure runtime starts windowed
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