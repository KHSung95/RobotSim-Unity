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
            if (FKController) FKController.enabled = isFK;
            if (IKController) IKController.enabled = !isFK;

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
                        FKController.MoveJoint(i, dir);
                    }
                    else if (!_isFKMode && IKController)
                    {
                        IKController.moveSpeed = speed * 0.5f;
                        IKController.rotateSpeed = speed * 45f;

                        Vector3 lin = Vector3.zero;
                        Vector3 ang = Vector3.zero;

                        if (i < 3) lin[i] = dir;
                        else ang[i - 3] = dir;

                        IKController.MoveTarget(lin, ang);
                    }
                }
            }

            if (_isFKMode) UpdateFKDisplay();
            else UpdateIKDisplay();
        }

        private void UpdateFKDisplay()
        {
            if (FKController == null || _rows == null) return;
            double[] joints = FKController.GetCurrentJoints();
            if (joints == null) return;

            for (int i = 0; i < _rows.Length && i < joints.Length; i++)
            {
                if (_rows[i])
                {
                    _rows[i].NameText.text = "J" + (i + 1);
                    _rows[i].ValueText.text = (joints[i] * Mathf.Rad2Deg).ToString("F1") + "°";
                }
            }
        }

        private void UpdateIKDisplay()
        {
            if (IKController == null || _rows == null) return;
            Vector3 pos = IKController.transform.localPosition;
            Vector3 rot = IKController.transform.localEulerAngles;

            if (_rows.Length > 0) _rows[0].NameText.text = "X";
            if (_rows.Length > 1) _rows[1].NameText.text = "Y";
            if (_rows.Length > 2) _rows[2].NameText.text = "Z";
            if (_rows.Length > 3) _rows[3].NameText.text = "Rx";
            if (_rows.Length > 4) _rows[4].NameText.text = "Ry";
            if (_rows.Length > 5) _rows[5].NameText.text = "Rz";

            if (_rows.Length > 0) _rows[0].ValueText.text = pos.x.ToString("F3");
            if (_rows.Length > 1) _rows[1].ValueText.text = pos.y.ToString("F3");
            if (_rows.Length > 2) _rows[2].ValueText.text = pos.z.ToString("F3");
            if (_rows.Length > 3) _rows[3].ValueText.text = rot.x.ToString("F1") + "°";
            if (_rows.Length > 4) _rows[4].ValueText.text = rot.y.ToString("F1") + "°";
            if (_rows.Length > 5) _rows[5].ValueText.text = rot.z.ToString("F1") + "°";
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
