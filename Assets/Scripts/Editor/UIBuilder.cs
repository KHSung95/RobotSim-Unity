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
            public static Color Danger = new Color(1f, 0.2f, 0.3f, 1f);         // Vivid Red
        }


        [MenuItem("CLE Robotics/Build Golden Layout")]
        public static void BuildGoldenLayout()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                // Note: User can also use Prefabs if preferred, but we build programmatically for now.
                canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Cleanup
            var oldRoot = canvas.transform.Find("UIRoot");
            if (oldRoot) DestroyImmediate(oldRoot.gameObject);

            GameObject root = new GameObject("UIRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root);

            // 1. Navbar
            GameObject nav = CreatePanel(root.transform, "NavBar", Theme.BgMain);
            Anchor(nav, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f));
            nav.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 0);

            VerticalLayoutGroup vNav = nav.AddComponent<VerticalLayoutGroup>();
            vNav.padding = new RectOffset(5, 5, 30, 20);
            vNav.spacing = 25;
            vNav.childAlignment = TextAnchor.UpperCenter;
            vNav.childControlWidth = true; vNav.childControlHeight = true;

            CreateNavIcon(nav.transform, "PWR", Theme.Accent);
            CreateNavIcon(nav.transform, "VIS", Theme.TextDim);
            CreateNavIcon(nav.transform, "CTL", Theme.TextDim);

            GameObject space = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            space.transform.SetParent(nav.transform, false);
            space.GetComponent<LayoutElement>().flexibleHeight = 1;
            CreateNavIcon(nav.transform, "SET", Theme.TextDim);

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
                var toggleRow = CreateRow(row.transform, "Toggles");
                toggleRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
                toggleRow.GetComponent<HorizontalLayoutGroup>().spacing = 5;
                // Functional Toggles
                CreateInteractiveToggle(toggleRow.transform, "RGB", true);
                CreateInteractiveToggle(toggleRow.transform, "Depth", false);

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
                CreateText(row.transform, "Operation Mode", 18, FontStyles.Bold, Theme.TextMain); // Added Label

                var btnRow = CreateRow(p, "OpButtons");
                btnRow.GetComponent<LayoutElement>().minHeight = 60; // Increased Height
                var b1 = CreateBigButton(btnRow.transform, "Capture", "CAPTURE", Theme.BgMain);
                var b2 = CreateBigButton(btnRow.transform, "Guidence", "GUIDANCE", Theme.BgMain);

                b1.GetComponent<LayoutElement>().minHeight = 50; // Explicitly larger
                b2.GetComponent<LayoutElement>().minHeight = 50;
            });

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

                // Tabs (FK / IK)
                var tabs = CreateRow(tightGroup.transform, "Tabs");
                tabs.GetComponent<HorizontalLayoutGroup>().spacing = 0;
                CreateTabButton(tabs.transform, "FK", "FK (Joint)", true);
                CreateTabButton(tabs.transform, "IK", "IK (TCP)", false);

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

                // Speed Slider
                var logicRow = CreateRow(p, "Logic");
                logicRow.GetComponent<LayoutElement>().minHeight = 24;
                CreateText(logicRow.transform, "SPEED", 11, FontStyles.Bold, Theme.TextDim).GetComponent<LayoutElement>().minWidth = 50;
                CreateSlider(logicRow.transform, "SpeedSlider");

                // E-Stop
                var estop = CreateBigButton(p.transform, "EStop", "EMERGENCY STOP", Theme.Danger);
                estop.GetComponent<LayoutElement>().minHeight = 45;
            });

            // 3. Console
            GameObject console = CreatePanel(root.transform, "Console", new Color(0, 0, 0, 0.8f));
            Anchor(console, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0));
            var rtC = console.GetComponent<RectTransform>();
            rtC.offsetMin = new Vector2(60, 0);
            rtC.offsetMax = new Vector2(-360, 160);

            var consoleTxt = CreateText(console.transform, "[System] Ready...", 12, FontStyles.Normal, Theme.TextDim);
            //consoleTxt.alignment = TextAlignmentOptions.TopLeft;
            var rtCT = consoleTxt.GetComponent<RectTransform>();
            rtCT.anchorMin = Vector2.zero; rtCT.anchorMax = Vector2.one;
            rtCT.offsetMin = new Vector2(15, 10); rtCT.offsetMax = new Vector2(-15, -10);

            if (Camera.main)
            {
                Camera.main.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
                Camera.main.rect = new Rect(0.04f, 0.16f, 1f - 0.187f - 0.04f, 1f - 0.16f);
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

        private static void CreateModule(Transform p, string title, System.Action<Transform> content)
        {
            GameObject m = CreatePanel(p, title + "_Module", Theme.BgSec);
            var v = m.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 16; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false;
            content(m.transform);
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

        private static void CreateNavIcon(Transform p, string txt, Color c)
        {
            var t = CreateText(p, txt, 12, FontStyles.Bold, c);
            t.alignment = TextAlignmentOptions.Center;
            t.gameObject.AddComponent<LayoutElement>().minHeight = 40;
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
            b.AddComponent<Button>();
            b.AddComponent<LayoutElement>().minHeight = 35;
            b.AddComponent<LayoutElement>().flexibleWidth = 1;
            var txt = CreateText(b.transform, t, 12, FontStyles.Bold, Color.white);
            txt.alignment = TextAlignmentOptions.Center;
            return b;
        }

        private static void CreateTabButton(Transform p, string name, string t, bool active)
        {
            var b = CreateBigButton(p, name, t, active ? Theme.Accent : Theme.BgPanel);
            b.GetComponent<LayoutElement>().minHeight = 28;
            if (active) b.GetComponentInChildren<TextMeshProUGUI>().color = Color.black;
        }

        // Real Toggle logic
        private static void CreateInteractiveToggle(Transform p, string t, bool startOn)
        {
            GameObject go = CreatePanel(p, "Toggle_" + t, Theme.BgPanel);
            go.AddComponent<LayoutElement>().minWidth = 50;
            go.AddComponent<LayoutElement>().minHeight = 24;

            var toggle = go.AddComponent<Toggle>();
            toggle.isOn = startOn;

            // Checkmark
            var img = go.GetComponent<Image>();
            toggle.targetGraphic = img;

            toggle.onValueChanged.AddListener((val) =>
            {
                img.color = val ? Theme.Accent : Theme.BgPanel;
                go.GetComponentInChildren<TextMeshProUGUI>().color = val ? Color.black : Theme.TextDim;
            });

            img.color = startOn ? Theme.Accent : Theme.BgPanel;

            var txt = CreateText(go.transform, t, 10, FontStyles.Bold, startOn ? Color.black : Theme.TextDim);
            txt.alignment = TextAlignmentOptions.Center;
        }


        private static void CreateSlider(Transform p, string n)
        {
            GameObject s = new GameObject(n, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            s.transform.SetParent(p, false);
            s.GetComponent<LayoutElement>().minHeight = 12;
            s.GetComponent<LayoutElement>().flexibleWidth = 1;

            // Background
            GameObject bg = CreatePanel(s.transform, "BG", Color.black);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);

            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(s.transform, false);
            Stretch(fillArea);

            GameObject fill = CreatePanel(fillArea.transform, "Fill", Theme.Accent);
            s.GetComponent<Slider>().fillRect = fill.GetComponent<RectTransform>();

            // Handle
            GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(s.transform, false);
            Stretch(handleArea);

            GameObject handle = CreatePanel(handleArea.transform, "Handle", Color.white);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(16, 0);
            s.GetComponent<Slider>().handleRect = handle.GetComponent<RectTransform>();
            s.GetComponent<Slider>().targetGraphic = handle.GetComponent<Image>();

            s.GetComponent<Slider>().value = 0.5f;

            // CreateSlider 함수 맨 마지막에 추가
            // 부모인 logicRow의 레이아웃을 강제로 업데이트하여 
            // 자식인 Slider(s)의 크기를 먼저 확정 짓습니다.
            //LayoutRebuilder.ForceRebuildLayoutImmediate(p.GetComponent<RectTransform>());

            // 그 다음 Slider 내부의 자식들이 0으로 맞춰지게 합니다.
            bg.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            fill.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        }
    }
}