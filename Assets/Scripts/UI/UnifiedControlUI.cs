using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotSim.Sensors;
using RobotSim.Simulation;
using RosSharp.RosBridgeClient;
using RosSharp.Urdf;
using System.Collections;
using System.Linq;

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
        public TargetJointController FKController;
        public TargetPoseController IKController;
        public RobotSim.Control.RosJogAdapter RosJogAdapter; // Changed from LocalJogIKAdapter

        [Header("UI Roots (Auto-Found)")]
        public GameObject Sidebar;
        public GameObject ConsolePanel;
        public GameObject SettingsModal;

        // Vision Reference
        private RawImage _visionFeed;
        private Toggle _rgbToggle, _depthToggle;

        // Operation References
        private Button _captureBtn, _guidanceBtn;

        // Jogging References
        private bool _isFKMode = true;
        private Slider _speedSlider;
        private Button _eStopBtn;

        // Control Panel
        private GameObject _controlPanel;
        private RobotAxisRow[] _rows;

        private void InitializeReferences()
        {
            if (Guidance == null) Guidance = FindObjectOfType<GuidanceManager>();
            if (PCG == null) PCG = FindObjectOfType<PointCloudGenerator>();
            if (CamMount == null) CamMount = FindObjectOfType<VirtualCameraMount>();
            if (FKController == null) FKController = FindObjectOfType<TargetJointController>();
            if (IKController == null) IKController = FindObjectOfType<TargetPoseController>();
            if (RosJogAdapter == null) RosJogAdapter = FindObjectOfType<RobotSim.Control.RosJogAdapter>();

            if (RobotBase == null)
            {
                var baseObj = GameObject.Find("base_link");
                if (baseObj != null) RobotBase = baseObj.transform;
                else RobotBase = GameObject.Find("ur5e")?.transform;
            }

            // Find UI Roots more robustly
            var canvas = FindObjectOfType<Canvas>();
            Transform uiRoot = canvas?.transform.Find("UIRoot");
            Sidebar = uiRoot?.Find("Sidebar")?.gameObject;
            ConsolePanel = uiRoot?.Find("Console")?.gameObject;

            if (Sidebar != null)
            {
                Debug.Log($"<b>[UnifiedControlUI]</b> Sidebar found: {Sidebar.name}");
                _visionFeed = FindUISub<RawImage>(Sidebar.transform, "Feed");
                _rgbToggle = FindUISub<Toggle>(Sidebar.transform, "Toggle_RGB");
                _depthToggle = FindUISub<Toggle>(Sidebar.transform, "Toggle_Depth");

                _captureBtn = FindUISub<Button>(Sidebar.transform, "Capture");
                _guidanceBtn = FindUISub<Button>(Sidebar.transform, "Guidance");

                _speedSlider = FindUISub<Slider>(Sidebar.transform, "SpeedSlider");
                _eStopBtn = FindUISub<Button>(Sidebar.transform, "EStop");

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

        private T FindUISub<T>(Transform root, string name) where T : Component
        {
            var t = root.FindDeepChild(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        private void BindEvents()
        {
            if (Sidebar == null) return;
            var tabs = Sidebar.transform.FindDeepChild("Tabs");
            var fkTab = tabs?.Find("FK")?.GetComponent<Button>();
            var ikTab = tabs?.Find("IK")?.GetComponent<Button>();

            if (fkTab) fkTab.onClick.AddListener(() => SetMode(true));
            if (ikTab) ikTab.onClick.AddListener(() => SetMode(false));

            if (_captureBtn) _captureBtn.onClick.AddListener(() => Guidance?.CaptureMaster());
            if (_guidanceBtn) _guidanceBtn.onClick.AddListener(() => Guidance?.RunGuidance());

            if (_rgbToggle) _rgbToggle.onValueChanged.AddListener((v) => { if (v) SetVisionMode(true); });
            if (_depthToggle) _depthToggle.onValueChanged.AddListener((v) => { if (v) SetVisionMode(false); });
        }

        public void SetVisionMode(bool isRGB)
        {
            if (_rgbToggle) _rgbToggle.SetIsOnWithoutNotify(isRGB);
            if (_depthToggle) _depthToggle.SetIsOnWithoutNotify(!isRGB);

            var camObj = GameObject.Find("VisionCamera");
            if (camObj != null)
            {
                var cam = camObj.GetComponent<Camera>();
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
            if (FKController) FKController.enabled = true; // Always enable FK
            if (IKController) IKController.enabled = false; // Disable old IK
            if (RosJogAdapter) RosJogAdapter.enabled = !isFK;

            UpdateTabVisuals(isFK);
        }

        private void UpdateTabVisuals(bool isFK)
        {
            if (Sidebar == null) return;
            var tabs = Sidebar.transform.FindDeepChild("Tabs");
            if (tabs == null) return;

            var fkTabImg = tabs.Find("FK")?.GetComponent<Image>();
            var ikTabImg = tabs.Find("IK")?.GetComponent<Image>();

            Color accent = new Color(0f, 0.8f, 1f, 1f);
            Color bg = new Color(0.12f, 0.12f, 0.15f, 1f);

            if (fkTabImg) fkTabImg.color = isFK ? accent : bg;
            if (ikTabImg) ikTabImg.color = !isFK ? accent : bg;

            var fkTxt = fkTabImg?.GetComponentInChildren<TextMeshProUGUI>();
            var ikTxt = ikTabImg?.GetComponentInChildren<TextMeshProUGUI>();

            if (fkTxt) fkTxt.color = isFK ? Color.black : Color.white;
            if (ikTxt) ikTxt.color = !isFK ? Color.black : Color.white;
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

                if (dir != 0)
                {
                    if (_isFKMode && FKController)
                    {
                        FKController.RotationSpeed = speed * 45f;
                        // Use JogJoint (Velocity) for smooth movement instead of MoveJoint (Absolute Position)
                        FKController.JogJoint(i, dir);
                    }
                    else if (!_isFKMode && RosJogAdapter)
                    {
                        // ROS Mode (Servo): Continuous Hold
                        // RosJogAdapter.Jog handles "Hold" logic internally (tracks velocity).
                        // We just need to assert "I am pressing".
                        // Logic:
                        // If Pressed, send Velocity.
                        // If NOT Pressed, do nothing (RosJogAdapter will timeout/stop).

                        Vector3 lin = Vector3.zero;
                        Vector3 ang = Vector3.zero;
                        float val = 0;

                        if (negJog.IsPressed) val = -1;
                        if (posJog.IsPressed) val = 1;

                        if (val != 0)
                        {
                            // Sync speed multiplier with slider
                            RosJogAdapter.SpeedMultiplier = speed;

                            // Map row index to Linear/Angular axes
                            // Rows 0,1,2 -> X,Y,Z (Linear)
                            // Rows 3,4,5 -> Rx,Ry,Rz (Angular)
                            if (i < 3) lin[i] = val;
                            else ang[i - 3] = val;

                            RosJogAdapter.Jog(lin, ang);
                        }
                    }
                }
            }

            if (_isFKMode) UpdateFKDisplay();
            else UpdateIKDisplay();
        }

        private void UpdateFKDisplay()
        {
            if (FKController == null || FKController.RobotRoot == null || _rows == null) return;
            
            // Access the publisher to get the correct order of joint names
            var publisher = FKController.GetComponent<TargetJointPublisher>();
            if (publisher == null) return;
            string[] names = publisher.JointNames;

            // Find valid joints in the robot hierarchy
            var joints = FKController.RobotRoot.GetComponentsInChildren<UrdfJoint>();

            for (int i = 0; i < _rows.Length && i < names.Length; i++)
            {
                if (_rows[i])
                {
                    _rows[i].NameText.text = "J" + (i + 1);
                    
                    // Find the specific joint by name
                    var targetJoint = joints.FirstOrDefault(j => j.JointName == names[i]);
                    if (targetJoint != null)
                    {
                        // GetPosition usually returns Radians
                        float angleDeg = (float)targetJoint.GetPosition() * Mathf.Rad2Deg;
                        _rows[i].ValueText.text = angleDeg.ToString("F1") + "°";
                    }
                    else
                    {
                        _rows[i].ValueText.text = "--";
                    }
                }
            }
        }

        private void UpdateIKDisplay()
        {
            if (_rows == null || _rows.Length < 6) return;

            Transform tcp = null;
            if (FKController != null && FKController.RobotRoot != null)
            {
                // Find tool0 or common end-effector links
                tcp = FKController.RobotRoot.FindDeepChild("tool0");
                if (tcp == null) tcp = FKController.RobotRoot.FindDeepChild("wrist_3_link");
            }

            if (tcp == null || RobotBase == null)
            {
                for (int i = 0; i < 6; i++) _rows[i].ValueText.text = "N/A";
                return;
            }

            // 1. Calculate Relative Position (Unity Coords)
            Vector3 relativePos = RobotBase.InverseTransformPoint(tcp.position);
            
            // 2. Map Unity to ROS Coordinates: 
            // ROS X (Forward) = Unity Z
            // ROS Y (Left) = Unity -X
            // ROS Z (Up) = Unity Y
            float rosX = relativePos.z;
            float rosY = -relativePos.x;
            float rosZ = relativePos.y;

            // 3. Calculate Relative Rotation
            Quaternion relativeRot = Quaternion.Inverse(RobotBase.rotation) * tcp.rotation;
            Vector3 euler = relativeRot.eulerAngles;
            // Normalizing to -180 to 180 for readability
            if (euler.x > 180) euler.x -= 360;
            if (euler.y > 180) euler.y -= 360;
            if (euler.z > 180) euler.z -= 360;

            // Labels
            _rows[0].NameText.text = "X";
            _rows[1].NameText.text = "Y";
            _rows[2].NameText.text = "Z";
            _rows[3].NameText.text = "Roll";
            _rows[4].NameText.text = "Pitch";
            _rows[5].NameText.text = "Yaw";

            // Values
            _rows[0].ValueText.text = rosX.ToString("F3");
            _rows[1].ValueText.text = rosY.ToString("F3");
            _rows[2].ValueText.text = rosZ.ToString("F3");
            
            _rows[3].ValueText.text = euler.x.ToString("F1") + "°";
            _rows[4].ValueText.text = euler.y.ToString("F1") + "°";
            _rows[5].ValueText.text = euler.z.ToString("F1") + "°";
        }

        public void SetCameraMount(bool isHandEye)
        {
            if (CamMount == null) return;
        }
    }

    public static class TransformDeepChildExtension
    {
        public static Transform FindDeepChild(this Transform aParent, string aName)
        {
            var result = aParent.Find(aName);
            if (result != null) return result;
            foreach (Transform child in aParent)
            {
                result = child.FindDeepChild(aName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
