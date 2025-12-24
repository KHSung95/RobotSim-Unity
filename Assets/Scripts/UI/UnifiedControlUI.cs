using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotSim.Sensors;
using RobotSim.Simulation;
using RosSharp.RosBridgeClient;
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
        private Toggle _modeFK, _modeIK;
        private Slider _speedSlider;
        private Button _eStopBtn;

        // FK/IK Panels
        private GameObject _fkPanel;
        private GameObject _ikPanel;

        // Dynamic Axis References (6 FK rows + 6 IK rows)
        private TextMeshProUGUI[] _fkValues = new TextMeshProUGUI[6];
        private JogButton[] _fkNegBtns = new JogButton[6];
        private JogButton[] _fkPosBtns = new JogButton[6];

        private TextMeshProUGUI[] _ikValues = new TextMeshProUGUI[6];
        private JogButton[] _ikNegBtns = new JogButton[6];
        private JogButton[] _ikPosBtns = new JogButton[6];

        // Start removed - used at bottom

        private void InitializeReferences()
        {
            if (Guidance == null) Guidance = FindObjectOfType<GuidanceManager>();
            if (PCG == null) PCG = FindObjectOfType<PointCloudGenerator>();
            if (CamMount == null) CamMount = FindObjectOfType<VirtualCameraMount>();
            if (FKController == null) FKController = FindObjectOfType<TargetJointController>();
            if (IKController == null) IKController = FindObjectOfType<TargetPoseController>();

            // Try to find robot base if null
            if (RobotBase == null)
            {
                 var baseObj = GameObject.Find("base_link"); // Common ROS name
                 if(baseObj != null) RobotBase = baseObj.transform;
                 else RobotBase = GameObject.Find("ur5e")?.transform; // Fallback
            }

            // Find UI Elements
            Sidebar = GameObject.Find("CommandSidebar");
            ConsolePanel = GameObject.Find("SystemConsole");
            
            _visionFeed = FindUI<RawImage>("CameraFeed");
            _rgbToggle = FindUI<Toggle>("RGBToggle");
            _depthToggle = FindUI<Toggle>("DepthToggle");

            _captureBtn = FindUI<Button>("CaptureBtn");
            _guidanceBtn = FindUI<Button>("GuidanceBtn");

            _modeFK = FindUI<Toggle>("ModeFK");
            _modeIK = FindUI<Toggle>("ModeIK");
            _speedSlider = FindUI<Slider>("SpeedSlider");
            _eStopBtn = FindUI<Button>("EStopBtn");

            // Panels
            _fkPanel = FindUI<Transform>("FKPanel")?.gameObject;
            _ikPanel = FindUI<Transform>("IKPanel")?.gameObject;

            // Bind Rows
            for (int i = 0; i < 6; i++)
            {
                // FK
                _fkValues[i] = FindUI<TextMeshProUGUI>($"FK_Row_{i}/Value");
                _fkNegBtns[i] = BindJogBtn($"FK_Row_{i}/BtnNeg", i, -1);
                _fkPosBtns[i] = BindJogBtn($"FK_Row_{i}/BtnPos", i, 1);

                // IK
                _ikValues[i] = FindUI<TextMeshProUGUI>($"IK_Row_{i}/Value");
                _ikNegBtns[i] = BindJogBtn($"IK_Row_{i}/BtnNeg", i, -1);
                _ikPosBtns[i] = BindJogBtn($"IK_Row_{i}/BtnPos", i, 1);
            }
        }

        private JogButton BindJogBtn(string path, int index, float dir)
        {
            var btn = FindUI<Button>(path);
            if (btn == null) return null;
            var jog = btn.gameObject.AddComponent<JogButton>();
            jog.AxisIndex = index;
            jog.Direction = dir;
            return jog;
        }

        private T FindUI<T>(string name) where T : Component
        {
             var found = GameObject.Find(name);
             if (found != null) return found.GetComponent<T>();
             if (Sidebar != null)
             {
                 var t = Sidebar.transform.FindDeepChild(name);
                 if (t != null) return t.GetComponent<T>();
             }
             return null;
        }

        private void BindEvents()
        {
            if(_modeFK) _modeFK.onValueChanged.AddListener((v) => { if(v) SetMode(true); });
            if(_modeIK) _modeIK.onValueChanged.AddListener((v) => { if(v) SetMode(false); });

            if(_captureBtn) _captureBtn.onClick.AddListener(() => Guidance?.CaptureMaster());
            if(_guidanceBtn) _guidanceBtn.onClick.AddListener(() => Guidance?.RunGuidance());
            
            // Toggles
            if(_rgbToggle) _rgbToggle.onValueChanged.AddListener((v) => { if(v) SetVisionMode(true); });
        }

        public void SetVisionMode(bool isRGB)
        {
            // Simple Logic: Toggle between two cameras or just change property?
            // For now, let's just ensure we have *a* camera feed.
            
            // 1. Find the Vision Camera
            var camObj = GameObject.Find("VisionCamera");
            if (camObj == null) camObj = GameObject.Find("WristCamera");
            
            if (camObj != null)
            {
                var cam = camObj.GetComponent<Camera>();
                if (cam)
                {
                    // 2. Ensure Render Texture
                    if (cam.targetTexture == null)
                    {
                        RenderTexture rt = new RenderTexture(512, 512, 16);
                        rt.name = "VisionRT";
                        cam.targetTexture = rt;
                    }

                    // 3. Assign to UI
                    if (_visionFeed != null)
                    {
                        _visionFeed.texture = cam.targetTexture;
                        _visionFeed.color = Color.white; // Make visible
                    }
                }
            }
            else
            {
                Debug.LogWarning("VisionCamera or WristCamera not found in scene!");
            }
        }

        private void Start()
        {
            InitializeReferences();
            BindEvents();
            SetMode(true); // Default FK
            SetVisionMode(true); // Init Camera Feed
        }
        public void SetMode(bool isFK)
        {
            if (FKController) FKController.enabled = isFK;
            if (IKController) IKController.enabled = !isFK;

            if (_fkPanel) _fkPanel.SetActive(isFK);
            if (_ikPanel) _ikPanel.SetActive(!isFK);
        }

        public void SetCameraMount(bool isHandEye)
        {
            if (CamMount == null) return;
            // TODO: Implement actual transform parenting logic
        }

        private void Update()
        {
            bool isFK = _modeFK != null && _modeFK.isOn;
            float speed = _speedSlider ? _speedSlider.value : 1.0f;
            
            // Handle Movements & Update UI Texts
            if (isFK)
            {
                UpdateJogging(_fkNegBtns, _fkPosBtns, true, speed);
                
                // Update FK Labels
                if (FKController != null)
                {
                    double[] joints = FKController.GetCurrentJoints();
                    if (joints != null)
                    {
                        for (int i = 0; i < 6 && i < joints.Length; i++)
                        {
                            if (_fkValues[i]) _fkValues[i].text = (joints[i] * Mathf.Rad2Deg).ToString("F1") + "°";
                        }
                    }
                }
            }
            else
            {
                UpdateJogging(_ikNegBtns, _ikPosBtns, false, speed);
                
                // Update IK Labels
                if (IKController)
                {
                    Vector3 pos = IKController.transform.localPosition;
                    Vector3 rot = IKController.transform.localEulerAngles;
                    if (_ikValues[0]) _ikValues[0].text = pos.x.ToString("F3");
                    if (_ikValues[1]) _ikValues[1].text = pos.y.ToString("F3");
                    if (_ikValues[2]) _ikValues[2].text = pos.z.ToString("F3");
                    if (_ikValues[3]) _ikValues[3].text = rot.x.ToString("F1") + "°";
                    if (_ikValues[4]) _ikValues[4].text = rot.y.ToString("F1") + "°";
                    if (_ikValues[5]) _ikValues[5].text = rot.z.ToString("F1") + "°";
                }
            }
        }

        private void UpdateJogging(JogButton[] negs, JogButton[] pos, bool isFK, float speed)
        {
            for (int i = 0; i < 6; i++)
            {
                float dir = 0;
                if (negs[i] != null && negs[i].IsPressed) dir = -1;
                if (pos[i] != null && pos[i].IsPressed) dir = 1;

                if (dir != 0)
                {
                    if (isFK && FKController)
                    {
                        FKController.RotationSpeed = speed * 45f; 
                        FKController.MoveJoint(i, dir);
                    }
                    else if (!isFK && IKController)
                    {
                        IKController.moveSpeed = speed * 0.5f;
                        IKController.rotateSpeed = speed * 45f;
                        
                        Vector3 lin = Vector3.zero;
                        Vector3 ang = Vector3.zero;
                        
                        if (i < 3) lin[i] = dir;
                        else ang[i-3] = dir;

                        IKController.MoveTarget(lin, ang);
                    }
                }
            }
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
