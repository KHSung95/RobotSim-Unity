using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using RobotSim.Sensors;
using System.Collections.Generic;

namespace RobotSim.Editor
{
    [InitializeOnLoad]
    public class UIBuilder : EditorWindow
    {
        // --- Premium Theme Definition ---
        private static class Theme
        {
            public static Color BgMain = new Color(0.05f, 0.05f, 0.07f, 0.95f); // Deep Midnight
            public static Color BgPanel = new Color(0.12f, 0.12f, 0.15f, 1f);   // Slate Panel
            public static Color BgSec = new Color(0.15f, 0.15f, 0.18f, 1f);     // Lighter Section
            public static Color Accent = new Color(0f, 0.8f, 1f, 1f);           // Neon Cyan
            public static Color TextMain = new Color(0.95f, 0.95f, 1f, 1f);     // Crisp White
            public static Color TextDim = new Color(0.6f, 0.6f, 0.7f, 1f);      // Steel Gray
            public static Color Danger = new Color(1f, 0.15f, 0.25f, 1f);         // Vivid Red
            public static Color Master = new Color(1f, 0.75f, 0.1f, 1f);
            public static ColorBlock SetButtonColor(ColorBlock cb, Color c)
            {
                cb.normalColor = BgMain;
                cb.selectedColor = BgMain;
                cb.pressedColor = c;
                return cb;
            }
            public static ColorBlock SetMonoButtonColor(ColorBlock cb)
            {
                cb.normalColor = TextMain;
                cb.selectedColor = TextMain;
                cb.pressedColor = Color.white;
                return cb;
            }
        }


        [MenuItem("CLE Robotics/Build Golden Layout")]
        public static void BuildGoldenLayout()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Ensure EventSystem exists
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            // Cleanup
            var oldRoot = canvas.transform.Find("UIRoot");
            if (oldRoot) DestroyImmediate(oldRoot.gameObject);

            GameObject root = new GameObject("UIRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root);

            // 1. NavBar - Stretched Left
            GameObject nav = CreatePanel(root.transform, "Navbar", Theme.BgMain);
            Anchor(nav, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f)); // Full height
            nav.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 0); // Fixed Width 60

            VerticalLayoutGroup vNav = nav.AddComponent<VerticalLayoutGroup>();
            vNav.padding = new RectOffset(5, 5, 5, 5);
            vNav.spacing = 5;
            vNav.childAlignment = TextAnchor.UpperCenter;
            vNav.childControlWidth = true; vNav.childControlHeight = true;
            vNav.childForceExpandHeight = false;

            // 1. Master Mode (Lightning)
            // Color: Yellow-Orange mix -> Gold/Amber
            CreateNavButton(nav.transform, "Master", "Master", Theme.Master, null);

            // 2. Settings (Gear)
            // Color: Theme Accent (Cyan)
            CreateNavButton(nav.transform, "Settings", "Setting", Theme.Accent, null);

            // 2. Sidebar
            GameObject side = CreatePanel(root.transform, "Sidebar", Theme.BgPanel);
            Anchor(side, new Vector2(1, 0), Vector2.one, new Vector2(1, 0.5f));
            side.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 0);

            VerticalLayoutGroup vSide = side.AddComponent<VerticalLayoutGroup>();
            vSide.padding = new RectOffset(15, 15, 15, 15);
            vSide.spacing = 15;
            vSide.childControlWidth = true; vSide.childControlHeight = true; vSide.childForceExpandHeight = false;

            // Header
            var header = CreateText(side.transform, "CLE Robotics", 18, FontStyles.Bold, Theme.TextMain);
            header.alignment = TextAlignmentOptions.Center;

            // VISION SYSTEMModule
            CreateModule(side.transform, "VISION SYSTEM", (p) =>
            {
                var row = CreateRow(p, "Header");
                CreateText(row.transform, "Vision", 18, FontStyles.Bold, Theme.TextMain);

                // Toggles Row
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;

                // Visual Manager for tabs
                var visualManager = row.AddComponent<RobotSim.UI.ToggleTabManager>();

                // Functional Toggles
                var masterData = CreateInteractiveToggle(row.transform, "Pointcloud", true, 24, null);

                visualManager.Tabs = new List<RobotSim.UI.ToggleTabManager.TabItem> { masterData };
                visualManager.Initialize();

                GameObject feed = new GameObject("Feed", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
                feed.transform.SetParent(p, false);
                feed.GetComponent<RawImage>().color = Color.white;
                feed.GetComponent<LayoutElement>().minHeight = 180;

                var overlay = CreateText(feed.transform, "LIVE FEED", 10, FontStyles.Normal, Color.red);
                Anchor(overlay.gameObject, Vector2.zero, Vector2.one, Vector2.one);
                overlay.alignment = TextAlignmentOptions.TopRight;
                overlay.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -10);
            });

            // OPERATION MODE
            CreateModule(side.transform, "OPERATION MODE", (p) =>
            {
                var row = CreateRow(p, "Header");
                CreateText(row.transform, "Operation Mode", 18, FontStyles.Bold, Theme.TextMain); // Master Color

                // Group 1: Normal Mode (Capture + Guidance)
                var btnRowNormal = CreateRow(p, "OpButtons_Normal");
                btnRowNormal.GetComponent<LayoutElement>().minHeight = 60; 
                var b1 = CreateBigButton(btnRowNormal.transform, "Capture", "CAPTURE", Theme.Accent);
                var b2 = CreateBigButton(btnRowNormal.transform, "Guidance", "GUIDANCE", Theme.Accent);
                b1.GetComponent<Button>().onClick.AddListener(() => Debug.Log("Capture Pressed"));
                
                b1.GetComponent<LayoutElement>().minHeight = 50; 
                b2.GetComponent<LayoutElement>().minHeight = 50;
            });

            // MASTER MODE Module (Initially Hidden)
            CreateModule(side.transform, "MASTER MODE", (p) =>
            {
                var row = CreateRow(p, "Header");
                CreateText(row.transform, "Master Mode", 18, FontStyles.Bold, Theme.Master); // Master Color

                var btnRowMaster = CreateRow(p, "OpButtons_Master");
                btnRowMaster.GetComponent<LayoutElement>().minHeight = 60;
                var bMaster = CreateBigButton(btnRowMaster.transform, "CaptureMaster", "CAPTURE MASTER", Theme.Master); 
                bMaster.GetComponent<LayoutElement>().minHeight = 50;
                
                // Set the module itself to be managed by script, but we need a reference.
                // We'll name the module object clearly in CreateModule.
            }).SetActive(false);
            

            // JOGGING
            CreateModule(side.transform, "JOGGING", (p) =>
            {
                var le = p.GetComponent<LayoutElement>();
                if (le == null) le = p.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1;

                var row = CreateRow(p, "Header");
                CreateText(row.transform, "Direct Control", 18, FontStyles.Bold, Theme.TextMain); // Added Label

                // 2. 간격을 0으로 만들 아이템들을 담을 '묶음 그룹' 생성
                var tightGroup = new GameObject("TightGroup", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                tightGroup.transform.SetParent(p, false);// 묶음 그룹 설정 (간격 0)

                var tgGroup = tightGroup.GetComponent<VerticalLayoutGroup>();
                tgGroup.spacing = 0;
                tgGroup.childControlWidth = true;
                tgGroup.childControlHeight = true;
                tgGroup.childForceExpandHeight = false;

                // Tabs (FK / IK) - Increased height to 40 for better clickability
                var tabs = CreateRow(tightGroup.transform, "Tabs");
                tabs.GetComponent<HorizontalLayoutGroup>().spacing = 4;

                // Toggle Group for mutual exclusivity
                var tGroup = tabs.AddComponent<ToggleGroup>();
                tGroup.allowSwitchOff = false;

                // Visual Manager for persistent active state
                var visualManager = tabs.AddComponent<RobotSim.UI.ToggleTabManager>();

                var fk = CreateInteractiveToggle(tabs.transform, "FK", true, 40, tGroup);
                var ik = CreateInteractiveToggle(tabs.transform, "IK", false, 40, tGroup);

                visualManager.Tabs = new List<RobotSim.UI.ToggleTabManager.TabItem> { fk, ik };
                visualManager.Initialize();

                // --- 3. Universal Control Panel (하나만 생성!) ---
                GameObject controlPanel = new GameObject("ControlPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                controlPanel.transform.SetParent(tightGroup.transform, false);
                controlPanel.GetComponent<Image>().color = Theme.BgMain;

                var vGroup = controlPanel.GetComponent<VerticalLayoutGroup>();
                vGroup.padding = new RectOffset(0, 0, 16, 16);
                vGroup.spacing = 16;
                vGroup.childControlWidth = true;
                vGroup.childControlHeight = true;
                vGroup.childForceExpandHeight = true;

                for (int i = 0; i < 6; i++)
                {
                    // 이름 규칙: "Row_0", "Row_1" ... (FK/IK 구분 없음)
                    // 초기 라벨: 그냥 "J1" 등으로 해두고 런타임에 덮어씌움
                    CreateAxisRow(controlPanel.transform, i);
                }
                CreateSpacer(p);
                // Speed Slider
                var logicRow = CreateRow(p, "Speed");
                logicRow.GetComponent<LayoutElement>().minHeight = 24;
                CreateText(logicRow.transform, "SPEED", 11, FontStyles.Bold, Theme.TextDim).GetComponent<LayoutElement>().minWidth = 50;
                CreateSlider(logicRow.transform, "Slider");

                CreateSpacer(p);

                // E-Stop
                var estop = CreateBigButton(p.transform, "EStop", "EMERGENCY STOP", Theme.Danger);
                estop.GetComponent<LayoutElement>().minHeight = 45;
                estop.GetComponent<Image>().color = Theme.Danger;

                var estopBtn = estop.GetComponent<Button>();
                estopBtn.colors = Theme.SetMonoButtonColor(estopBtn.colors);
            });

            // 3. Console
            GameObject console = CreatePanel(root.transform, "Console", new Color(0, 0, 0, 0.8f));
            Anchor(console, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0));
            var rtC = console.GetComponent<RectTransform>();
            rtC.offsetMin = new Vector2(60, 0);
            rtC.offsetMax = new Vector2(-360, 160);

            var consoleTxt = CreateText(console.transform, "", 12, FontStyles.Normal, Theme.TextDim);
            //consoleTxt.alignment = TextAlignmentOptions.TopLeft;
            var rtCT = consoleTxt.GetComponent<RectTransform>();
            rtCT.anchorMin = Vector2.zero; rtCT.anchorMax = Vector2.one;
            rtCT.offsetMin = new Vector2(15, 10); rtCT.offsetMax = new Vector2(-15, -10);

            if (Camera.main)
            {
                Camera.main.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
                // 16:9 Viewport Fitting
                float navW = 60f / 1920f; // ~0.03125
                float sideW = 360f / 1920f; // ~0.1875
                Camera.main.rect = new Rect(navW, 0f, 1f - navW - sideW, 1f);
            }

            // 4. Settings Modal (Draggable) - Ensure it's a direct child of UIRoot
            BuildSettingsModal(root.transform);

            int layer = LayerMask.NameToLayer("UI");
            // true를 넣으면 비활성화된 자식들까지 모두 포함합니다.
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allChildren)
            {
                t.gameObject.layer = layer;
            }
        }

        // --- Helpers ---
        private static void Stretch(GameObject o)
        {
            var rt = o.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        }

        private static void Anchor(GameObject o, Vector2 min, Vector2 max, Vector2 piv)
        {
            var rt = o.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = piv; rt.anchoredPosition = Vector2.zero;
        }

        private static GameObject CreatePanel(Transform p, string n, Color c)
        {
            GameObject o = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            o.transform.SetParent(p, false);
            o.GetComponent<Image>().color = c;
            return o;
        }

        private static GameObject CreateSpacer(Transform p)
        {
            GameObject o = new GameObject("Spacer", typeof(LayoutElement));
            o.transform.SetParent(p, false);
            return o;
        }

        private static GameObject CreateModule(Transform p, string title, System.Action<Transform> content)
        {
            // Name the object specifically so we can find it later (e.g. "OPERATION MODE_Module")
            GameObject m = CreatePanel(p, title + "_Module", Theme.BgSec);
            var v = m.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 16; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false;
            content(m.transform);

            return m;
        }

        private static GameObject CreateRow(Transform p, string n)
        {
            GameObject r = new GameObject(n, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            r.transform.SetParent(p, false);
            var h = r.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            r.GetComponent<LayoutElement>().minHeight = 28;
            return r;
        }

        private static TextMeshProUGUI CreateText(Transform p, string s, float sz, FontStyles style, Color c)
        {
            GameObject o = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            o.transform.SetParent(p, false);
            var t = o.GetComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = sz; t.fontStyle = style; t.color = c;
            t.raycastTarget = false; // FIX: Make text transparent to raycasts (clicks)
            var rt = o.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            return t;
        }

        private static void CreateNavButton(Transform p, string name, string resourcePrefix, Color activeColor, ToggleGroup group)
        {
            GameObject obj = CreatePanel(p, "Button_" + name, Color.clear); // Transparent background
            var le = obj.GetComponent<LayoutElement>();
            le.minHeight = 60; // 60x60 (matches NavBar width)
            le.minWidth = 60;

            // Icon Image
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(obj.transform, false);
            Stretch(iconObj);
            // Add padding inside the button
            var rt = iconObj.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(12, 12); 
            rt.offsetMax = new Vector2(-12, -12);

            var img = iconObj.GetComponent<Image>();
            img.raycastTarget = false; // Toggle target is the parent panel

            // Toggle Component on Parent
            var toggle = obj.AddComponent<Toggle>();
            toggle.group = group;
            toggle.transition = Selectable.Transition.None;
            
            // Image (Raycast Target) usually needs to be on the toggle object. 
            // CreatePanel makes an Image on 'obj', which is our target graphic for clicks.
            // We'll set that Image to fully transparent but raycastable.
            var bgImg = obj.GetComponent<Image>();
            bgImg.color = Color.clear; 
            toggle.targetGraphic = bgImg;

            // NavBarToggle Script
            var navToggle = obj.AddComponent<RobotSim.UI.NavBarToggle>();
            navToggle.Toggle = toggle;
            navToggle.TargetImage = img;
            navToggle.ActiveColor = activeColor;
            navToggle.InactiveColor = Theme.TextDim; // Dimmed when off

            // Load Sprites
            // Assumes files are in Assets/Resources/
            // e.g. "Master_Off", "Master_On"
            navToggle.IconOff = Resources.Load<Sprite>(resourcePrefix + "_Off");
            navToggle.IconOn = Resources.Load<Sprite>(resourcePrefix + "_On");

            // Allow fallback if resources are missing (shows white square)
            if(navToggle.IconOff == null) Debug.LogWarning($"[UIBuilder] Missing Resource: {resourcePrefix}_Off");

            // Initialize visual
            navToggle.UpdateVisuals(false); // Start off
        }

        private static void CreateAxisRow(Transform p, int i) {
            string number = (i + 1).ToString();
            GameObject r = CreateRow(p, "Row_" + number);
            r.GetComponent<LayoutElement>().minHeight = 24;

            // Attach the container component
            var rowRef = r.AddComponent<RobotSim.UI.RobotAxisRow>();

            var hGroup = r.GetComponent<HorizontalLayoutGroup>();
            hGroup.spacing = 16;
            hGroup.padding = new RectOffset(16, 16, 0, 0);
            hGroup.childControlWidth = true;
            hGroup.childControlHeight = true;
            hGroup.childForceExpandHeight = true;
            hGroup.childForceExpandWidth = true; // Fix: Don't force stretch buttons

            var l = CreateText(r.transform, "", 18, FontStyles.Normal, Theme.TextDim);
            l.name = "Name";
            l.alignment = TextAlignmentOptions.Center;
            l.enableWordWrapping = false;
            l.overflowMode = TextOverflowModes.Truncate;
            l.GetComponent<LayoutElement>().preferredWidth = 0;
            // Assign to reference
            rowRef.NameText = l;

            var val = CreateText(r.transform, "", 18, FontStyles.Normal, Theme.TextMain);
            val.name = "Value";
            val.alignment = TextAlignmentOptions.Center;
            val.enableWordWrapping = false;
            val.overflowMode = TextOverflowModes.Truncate;
            val.GetComponent<LayoutElement>().preferredWidth = 30;
            // Assign to reference
            rowRef.ValueText = val;

            rowRef.SubBtn = CreateSmallBtn(r.transform, "Sub", "-");
            rowRef.AddBtn = CreateSmallBtn(r.transform, "Add", "+");
        }
        
        private static Button CreateSmallBtn(Transform p, string name, string t) {
            GameObject b = CreatePanel(p, name, Theme.BgPanel);
            b.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 20); 
            var btn = b.AddComponent<Button>();
            b.GetComponent<LayoutElement>().minWidth = 24;
            b.GetComponent<LayoutElement>().flexibleWidth = 0;
            var txt = CreateText(b.transform, t, 12, FontStyles.Normal, Theme.TextMain); 
            txt.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static GameObject CreateBigButton(Transform p, string name, string t, Color c)
        {
            GameObject b = CreatePanel(p, name, c);
            b.GetComponent<Image>().color = Color.white;
            var btn = b.AddComponent<Button>();
            btn.targetGraphic = b.GetComponent<Image>();
            btn.colors = Theme.SetButtonColor(btn.colors, c);

            var lElement = b.GetComponent<LayoutElement>();
            lElement.flexibleWidth = 1;
            lElement.minHeight = 35;

            var txt = CreateText(b.transform, t, 12, FontStyles.Bold, Color.white);
            txt.alignment = TextAlignmentOptions.Center;

            return b;
        }

        // Real Toggle logic with improved Cyan/Black active state and ToggleGroup support
        private static RobotSim.UI.ToggleTabManager.TabItem CreateInteractiveToggle(Transform p, string t, bool startOn, float height, ToggleGroup group)
        {
            GameObject go = CreatePanel(p, "Toggle_" + t, Theme.BgPanel);
            var LE = go.GetComponent<LayoutElement>();
            LE.minWidth = 50;
            LE.minHeight = height;

            var toggle = go.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None; // Recommended for custom color management
            toggle.group = group;
            toggle.isOn = startOn;

            var img = go.GetComponent<Image>();
            img.raycastTarget = true;
            toggle.targetGraphic = img;

            var txt = CreateText(go.transform, t, 12, FontStyles.Bold, startOn ? Color.black : Theme.TextDim);
            txt.alignment = TextAlignmentOptions.Center;

            // Return references for the visual manager to handle
            return new RobotSim.UI.ToggleTabManager.TabItem
            {
                Toggle = toggle,
                Background = img,
                Text = txt
            };
        }


        private static void CreateSlider(Transform p, string n)
        {
            // Container for Slider
            GameObject s = new GameObject(n, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            s.transform.SetParent(p, false);
            var le = s.GetComponent<LayoutElement>();
            le.minHeight = 30; // Slightly reduced from 40 to 30 as requested
            le.flexibleWidth = 1;

            var sliderComp = s.GetComponent<Slider>();

            // Background (Visual Bar)
            GameObject bg = CreatePanel(s.transform, "BG", Color.black);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(1, 0.5f);
            bgRect.sizeDelta = new Vector2(0, 6);
            bgRect.anchoredPosition = Vector2.zero;

            // Fill Area
            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(s.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(0, 6);
            fillAreaRect.anchoredPosition = Vector2.zero;

            GameObject fill = CreatePanel(fillArea.transform, "Fill", Theme.Accent);
            fill.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            sliderComp.fillRect = fill.GetComponent<RectTransform>();

            // Handle Area
            GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(s.transform, false);
            Stretch(handleArea);
            
            // Fix: Offset Handle Area by half of handle width (10px) on both sides
            // This ensures the handle (20px wide) stops with its right edge exactly at the container's right edge.
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.offsetMin = new Vector2(10, 0); 
            handleAreaRect.offsetMax = new Vector2(-10, 0); 

            // Handle Visual
            GameObject handle = CreatePanel(handleArea.transform, "Handle", Color.white);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20); 
            
            sliderComp.handleRect = handle.GetComponent<RectTransform>();
            sliderComp.targetGraphic = handle.GetComponent<Image>();
            sliderComp.value = 0.5f;
        }

        private static void BuildSettingsModal(Transform root)
        {
            // 1. 모달 루트 및 레이아웃 설정
            GameObject modal = CreatePanel(root, "SettingsModal", Theme.BgPanel);
            Anchor(modal, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var rt = modal.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420, 0);

            var csf = modal.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vModal = modal.AddComponent<VerticalLayoutGroup>();
            vModal.childControlWidth = true;
            vModal.childControlHeight = true;
            vModal.childForceExpandHeight = false;

            // --- A. 상단 제목 표시줄 (TitleBar) ---
            var titleBar = CreateRow(modal.transform, "TitleBar");
            titleBar.AddComponent<Image>().color = Theme.BgMain;
            var tbLayout = titleBar.GetComponent<HorizontalLayoutGroup>();

            // 패딩과 정렬 설정
            tbLayout.padding = new RectOffset(15, 0, 0, 0); // 오른쪽 끝 밀착을 위해 padding.right는 0
            tbLayout.spacing = 0;
            tbLayout.childControlWidth = true;
            tbLayout.childControlHeight = true;
            tbLayout.childForceExpandWidth = false; // 자식들이 전체 너비를 채우도록 설정
            titleBar.GetComponent<LayoutElement>().minHeight = 38;

            // [A-Left] 아이콘과 제목을 묶을 왼쪽 그룹 생성
            var leftGroup = new GameObject("LeftContent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            leftGroup.transform.SetParent(titleBar.transform, false);
            var lgLayout = leftGroup.GetComponent<HorizontalLayoutGroup>();
            lgLayout.spacing = 8;
            lgLayout.childControlWidth = true;
            lgLayout.childControlHeight = true;
            lgLayout.childForceExpandWidth = false; // 내용물 크기만큼만 차지
            lgLayout.childForceExpandHeight = false; // 내용물 크기만큼만 차지
            lgLayout.childAlignment = TextAnchor.MiddleLeft;

            // 이 그룹이 모든 남은 공간을 차지하게 하여 버튼을 우측으로 밀어냄
            var lgLE = leftGroup.AddComponent<LayoutElement>();
            lgLE.flexibleWidth = 1;

            // A-1. 설정 아이콘 (leftGroup의 자식으로 변경)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(leftGroup.transform, false);
            var iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>("Setting_On");
            iconImg.raycastTarget = false;
            var iconLE = iconObj.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 18; iconLE.preferredHeight = 18;

            // A-2. 제목 텍스트 (leftGroup의 자식으로 변경)
            var titleTxt = CreateText(leftGroup.transform, "Settings", 14, FontStyles.Normal, Theme.TextMain);
            titleTxt.GetComponent<LayoutElement>().flexibleWidth = 0;

            // A-3. 우측 끝 X 버튼 (titleBar의 직계 자식, flexibleWidth를 0으로 설정)
            var xBtn = CreateSmallBtn(titleBar.transform, "Button_X", "X");
            var xBtnLE = xBtn.GetComponent<LayoutElement>();
            xBtnLE.preferredWidth = 38;
            xBtnLE.preferredHeight = 38;
            xBtnLE.flexibleWidth = 0; // 버튼은 필요한 크기만 차지
            xBtn.GetComponent<Image>().color = Color.clear;

            // --- B. 메인 컨텐츠 영역 ---
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(modal.transform, false);
            var vContent = content.GetComponent<VerticalLayoutGroup>();
            vContent.padding = new RectOffset(25, 25, 25, 20);
            vContent.spacing = 22;
            vContent.childControlWidth = true; vContent.childControlHeight = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // B-1. Threshold 입력 행
            var row1 = CreateRow(content.transform, "Threshold");
            CreateText(row1.transform, "Max Deviation Threshold:", 14, FontStyles.Normal, Theme.TextMain);
            
            // InputField Creation
            var inputField = CreateInputField(row1.transform, "Input_Threshold", "2.0");
            inputField.GetComponent<LayoutElement>().preferredWidth = 75;
            inputField.GetComponent<LayoutElement>().minHeight = 26;

            CreateText(row1.transform, "mm", 14, FontStyles.Normal, Theme.TextDim).GetComponent<LayoutElement>().preferredWidth = 30;

            // B-2. Mounting 행
            var row2 = CreateRow(content.transform, "Mount");
            row2.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            // row2의 높이가 토글 높이(40)보다 작아서 짤리는 현상 방지: flex/minHeight 조정
            row2.GetComponent<LayoutElement>().minHeight = 30; 

            CreateText(row2.transform, "Camera Mounting:", 14, FontStyles.Normal, Theme.TextMain);

            var toggleArea = new GameObject("Toggle_Mount", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ToggleGroup), typeof(ContentSizeFitter));
            toggleArea.transform.SetParent(row2.transform, false);
            var vToggle = toggleArea.GetComponent<VerticalLayoutGroup>();
            vToggle.spacing = 10; vToggle.childAlignment = TextAnchor.UpperRight;
            vToggle.childControlHeight = true;
            toggleArea.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            var tGroup = toggleArea.GetComponent<ToggleGroup>();

            // Visual Manager 추가
            var visualManager = toggleArea.AddComponent<RobotSim.UI.ToggleTabManager>();

            // Toggle Height 18
            var t1 = CreateInteractiveToggle(toggleArea.transform, "Handeye", true, 18, tGroup);
            var t2 = CreateInteractiveToggle(toggleArea.transform, "Birdeye", false, 18, tGroup);

            visualManager.Tabs = new List<RobotSim.UI.ToggleTabManager.TabItem> { t1, t2 };
            visualManager.Initialize(); // Initialize visuals immediately

            // --- C. 하단 버튼 영역 (Footer) ---
            var footer = CreateRow(content.transform, "Footer");
            footer.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            footer.GetComponent<HorizontalLayoutGroup>().spacing = 12;

            var okBtn = CreateBigButton(footer.transform, "Button_Ok", "OK", Theme.BgSec);
            okBtn.GetComponent<LayoutElement>().preferredWidth = 90;

            var cancelBtn = CreateBigButton(footer.transform, "Button_Cancel", "Cancel", Theme.BgSec);
            cancelBtn.GetComponent<LayoutElement>().preferredWidth = 90;

            //// Add Border/Shadow visual if possible (Outline for now)
            //var outline = modal.AddComponent<Outline>();
            //outline.effectColor = Theme.Accent;
            //outline.effectDistance = new Vector2(1, -1);

            //// Drag Handler
            modal.AddComponent<RobotSim.UI.DragWindow>();
            modal.SetActive(false);
        }

        private static GameObject CreateInputField(Transform p, string n, string defaultVal)
        {
            GameObject o = CreatePanel(p, n, Color.black);
            var ifComponent = o.AddComponent<TMP_InputField>();
            
            // Text Area (Child)
            GameObject textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(o.transform, false);
            Anchor(textArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            var rt = textArea.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(5, 5); rt.offsetMax = new Vector2(-5, -5);

            // Text Component
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(textArea.transform, false);
            Anchor(textObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            var txt = textObj.GetComponent<TextMeshProUGUI>();
            txt.fontSize = 14; 
            txt.color = Theme.TextMain;
            txt.alignment = TextAlignmentOptions.Center;

            ifComponent.textViewport = textArea.GetComponent<RectTransform>();
            ifComponent.textComponent = txt;
            ifComponent.text = defaultVal;
            ifComponent.targetGraphic = o.GetComponent<Image>(); 

            return o;
        }
    }
}