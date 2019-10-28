using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

using VortexHarmonyInstaller.Delegates;

using VortexHarmonyInstaller;

namespace VortexUnity
{
    public partial class Constants
    {
        internal const int MARGIN = 100;
        internal const string UI_BUNDLE_NAME = "vortexui";
        internal const string VORTEX_OVERLAY_PREFAB_NAME = "VortexOverlay";
        internal const string BUTTON_TEX = "buttonTexture";
        internal const string ASSEMBLY_NAME = "VortexUnity.dll";
    }

    // Exposed for mods to use.
    public partial class Enums
    {
        public enum EGUIStyleID
        {
            TITLE, H1, H2, WINDOW, BUTTON, SETTINGS,
            TRANSPARENT, SELECTED_BUTTON, ACTION_BUTTON,
        };
    }

    // Exposed for mods to use.
    public partial class Util
    {
        // Useful when you just need a texture with a single color.
        public static Texture2D MakeTexture(int iWidth, int iHeight, Color color)
        {
            Color[] pix = new Color[iWidth * iHeight];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;

            Texture2D result = new Texture2D(iWidth, iHeight);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        // Useful when you just need a texture with a single color.
        public static Texture2D TransparentTexture()
        {
            Color color = new Color(0f, 0f, 0f, 0f);
            Color[] pix = new Color[2 * 2];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;

            Texture2D result = new Texture2D(2, 2);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        public static RectOffset RectOffset(int x)
        {
            return new RectOffset(x, x, x, x);
        }

        public static RectOffset RectOffset(int x, int y)
        {
            return new RectOffset(x, x, y, y);
        }
    }

    // Exposed for mods to use.
    public static class Hotkey
    {
        public static bool Up { get { return Input.GetKey(KeyCode.UpArrow); } }
        public static bool Down { get { return Input.GetKey(KeyCode.DownArrow); } }
        public static bool Shift { get { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } }
        public static bool Alt { get { return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt); } }
        public static bool Ctrl { get { return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); } }
    }

    // UMM GUI Style wrapper. Exposed primarily for UMM mods to use.
    public static class UI
    {
        public static GUIStyle bold { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.H2]; } }
        public static GUIStyle h1 { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.H1]; } }
        public static GUIStyle h2 { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.H2]; } }
        public static GUIStyle window { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.WINDOW]; } }
        public static GUIStyle button { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.BUTTON]; } }
        public static GUIStyle settings { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.SETTINGS]; } }
    }

    public class VortexUI : MonoBehaviour
    {
        #region statics
        private static int m_iSelectedModIdx = -1;

        private static List<IExposedMod> m_liMods = null;
        public static List<IExposedMod> Mods { get { return m_liMods; } }

        private static Dictionary<Enums.EGUIStyleID, GUIStyle> m_dictStyleDefs = new Dictionary<Enums.EGUIStyleID, GUIStyle>();
        public static Dictionary<Enums.EGUIStyleID, GUIStyle> StyleDefs { get { return m_dictStyleDefs; } }

        private static Assembly m_UIAssembly = null;
        public static Assembly UIAssembly { get { return m_UIAssembly; } }

        private static VortexUI m_Instance = null;
        public static VortexUI Instance { get { return m_Instance; } }

        internal static string m_strAssetPath = Path.Combine("VortexBundles", "UI");
        internal static AssetBundle m_UIAssetBundle = null;

        internal static GameObject m_goOverlay = null;

        internal static GameObject m_goVortexUI = null;

        private static List<Texture2D> m_liTextures = new List<Texture2D>();
        public static List<Texture2D> BundledTextures { get { return m_liTextures; } }

        internal static Vector2 m_v2ModsScrollPos = new Vector2();
        internal static Vector2 m_v2SettingsScrollPos = new Vector2();

        internal static bool Up { get { return Hotkey.Up; } }
        internal static bool Down { get { return Hotkey.Down; } }
        internal static bool Shift { get { return Hotkey.Shift; } }
        internal static bool Ctrl { get { return Hotkey.Ctrl; } }
        internal static bool Alt { get { return Hotkey.Alt; } }
        #endregion

        private bool m_bIsOpen = false;
        public bool IsOpen { get { return m_bIsOpen; } }

        private bool m_bIsSetup = false;
        public bool IsSetup { get { return m_bIsSetup; } }

        private Vector2 m_v2WindowSize = Vector2.zero;
        private Rect m_rectWindow = new Rect(0, 0, 0, 0);
        private Rect m_rectModsRect = new Rect(0, 0, 0, 0);
        private Rect m_rectSettingsRect = new Rect(0, 0, 0, 0);

        private Resolution m_CurrentResolution;

        public int m_iGlobalFontSize = 16;
        public Font m_GlobalFont;

        internal static void Load(List<IExposedMod> exposedEntries)
        {
            string strFolder = VortexPatcher.CurrentDataPath;
            string strAssemlyPath = Path.Combine(strFolder, Constants.ASSEMBLY_NAME);
            m_UIAssembly = Assembly.LoadFile(strAssemlyPath);
            m_UIAssetBundle = AssetBundle.LoadFromFile(Path.Combine(strFolder, m_strAssetPath, Constants.UI_BUNDLE_NAME));
            if (null == m_UIAssetBundle)
                throw new FileNotFoundException("Couldn't load UI asset bundle");

            m_liTextures = m_UIAssetBundle.LoadAllAssets<Texture2D>().ToList();

            m_goVortexUI = new GameObject(typeof(VortexUI).FullName, typeof(VortexUI));

            m_liMods = exposedEntries;
        }

        private void Awake()
        {
            m_Instance = this;
            DontDestroyOnLoad(this);
        }

        private void Start()
        {
            CalculateWindowPos();
            ToggleWindow(true);
        }

        private void Update()
        {
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            float fDeltaTime = Time.deltaTime;
            foreach (var mod in m_liMods)
                mod.InvokeOnUpdate(fDeltaTime);

            if (Input.GetKeyUp(KeyCode.F12) && Ctrl)
                ToggleWindow();

            if (IsOpen && Input.GetKey(KeyCode.Escape))
                ToggleWindow();
        }

        private void FixedUpdate()
        {
            float fDeltaTime = Time.fixedDeltaTime;
            foreach (var mod in Mods)
                mod.InvokeOnFixedUpdate(fDeltaTime);
        }

        private void LateUpdate()
        {
            float fDeltaTime = Time.deltaTime;
            foreach (var mod in Mods)
                mod.InvokeOnLateUpdate(fDeltaTime);
        }

        private void SetupDefaultGUI()
        {
            GUI.skin.font = m_GlobalFont;
            GUI.skin.button.padding = new RectOffset(10, 10, 3, 3);
            GUI.skin.button.margin = Util.RectOffset(4, 2);

            Texture2D text = Util.MakeTexture(4, 4, new Color(0.81f, 0.45f, 0.06f, 1f));
            GUI.skin.horizontalScrollbarThumb.normal.background = text;
            GUI.skin.verticalScrollbarThumb.normal.background = text;
        }

        private void SetupGUIDictionary()
        {
            SetupDefaultGUI();

            m_dictStyleDefs[Enums.EGUIStyleID.TRANSPARENT] = new GUIStyle();
            m_dictStyleDefs[Enums.EGUIStyleID.TRANSPARENT].font = m_GlobalFont;
            m_dictStyleDefs[Enums.EGUIStyleID.TRANSPARENT].normal.background = Util.MakeTexture(1, 1, new Color(1f, 1f, 1f, 0f));

            m_dictStyleDefs[Enums.EGUIStyleID.TITLE] = new GUIStyle();
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].name = "title";
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].padding = Util.RectOffset(5);
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].font = m_GlobalFont;
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].fontSize = (int)(m_iGlobalFontSize * 1.5f);
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].fontStyle = FontStyle.Bold;
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].normal.textColor = Color.white;
            m_dictStyleDefs[Enums.EGUIStyleID.TITLE].normal.background = Util.MakeTexture(16, 16, new Color(0f, 0f, 0f, 1f));

            m_dictStyleDefs[Enums.EGUIStyleID.WINDOW] = new GUIStyle();
            m_dictStyleDefs[Enums.EGUIStyleID.WINDOW].name = "window";
            m_dictStyleDefs[Enums.EGUIStyleID.WINDOW].padding = Util.RectOffset(5);
            m_dictStyleDefs[Enums.EGUIStyleID.WINDOW].normal.background = Util.MakeTexture(16, 16, new Color(0.29f, 0.29f, 0.29f, 1f));

            m_dictStyleDefs[Enums.EGUIStyleID.H1] = new GUIStyle();
            m_dictStyleDefs[Enums.EGUIStyleID.H1].name = "h1";
            m_dictStyleDefs[Enums.EGUIStyleID.H1].font = m_GlobalFont;
            m_dictStyleDefs[Enums.EGUIStyleID.H1].padding = Util.RectOffset(8);
            m_dictStyleDefs[Enums.EGUIStyleID.H1].fontStyle = FontStyle.Bold;
            m_dictStyleDefs[Enums.EGUIStyleID.H1].normal.textColor = Color.white;
            m_dictStyleDefs[Enums.EGUIStyleID.H1].normal.background = Util.MakeTexture(16, 16, new Color(0.5f, 0.5f, 0.5f, 1f));

            m_dictStyleDefs[Enums.EGUIStyleID.H2] = new GUIStyle();
            m_dictStyleDefs[Enums.EGUIStyleID.H2].name = "h2";
            m_dictStyleDefs[Enums.EGUIStyleID.H2].font = m_GlobalFont;
            m_dictStyleDefs[Enums.EGUIStyleID.H2].padding = Util.RectOffset(5);
            m_dictStyleDefs[Enums.EGUIStyleID.H2].fontStyle = FontStyle.Bold;
            m_dictStyleDefs[Enums.EGUIStyleID.H2].normal.textColor = Color.white;

            Texture2D normalBtnTexture = Util.MakeTexture(1, 1, new Color(0.87f, 0.87f, 0.87f, 1f));
            Texture2D hoverBtnTexture = Util.MakeTexture(1, 1, new Color(0.72f, 0.72f, 0.72f, 1f));
            Texture2D activeBtnTexture = Util.MakeTexture(1, 1, new Color(0.62f, 0.62f, 0.62f, 1f));
            Texture2D focusedBtnTexture = Util.MakeTexture(1, 1, new Color(1f, 1f, 1f, 1f));

            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON] = new GUIStyle();
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].name = "button";
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].font = m_GlobalFont;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].normal.background = normalBtnTexture;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].normal.textColor = Color.black;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].hover.background = hoverBtnTexture;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].hover.textColor = Color.black;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].active.background = activeBtnTexture;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].active.textColor = Color.black;
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].margin = Util.RectOffset(4, 4);
            m_dictStyleDefs[Enums.EGUIStyleID.BUTTON].padding = Util.RectOffset(10, 4);

            m_dictStyleDefs[Enums.EGUIStyleID.SELECTED_BUTTON] = new GUIStyle(StyleDefs[Enums.EGUIStyleID.BUTTON]);
            m_dictStyleDefs[Enums.EGUIStyleID.SELECTED_BUTTON].name = "selected_button";
            m_dictStyleDefs[Enums.EGUIStyleID.SELECTED_BUTTON].normal.background = focusedBtnTexture;

            normalBtnTexture = Util.MakeTexture(1, 1, new Color(0.81f, 0.45f, 0.06f, 1f));
            hoverBtnTexture = Util.MakeTexture(1, 1, new Color(0.68f, 0.44f, 0.2f, 1f));
            activeBtnTexture = Util.MakeTexture(1, 1, new Color(0.88f, 0.64f, 0.4f, 1f));

            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON] = new GUIStyle(StyleDefs[Enums.EGUIStyleID.BUTTON]);
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].name = "action_button";
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].normal.background = normalBtnTexture;
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].normal.textColor = Color.white;
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].hover.background = hoverBtnTexture;
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].hover.textColor = Color.white;
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].active.background = activeBtnTexture;
            m_dictStyleDefs[Enums.EGUIStyleID.ACTION_BUTTON].active.textColor = Color.white;
        }

        private void Setup()
        {
            if (!IsSetup)
            {
                m_bIsSetup = true;
                SetupGUIDictionary();
            }
        }

        private void OnGUI()
        {
            Setup();
            if (IsOpen)
            {
                if (m_CurrentResolution.width != Screen.currentResolution.width || m_CurrentResolution.height != Screen.currentResolution.height)
                {
                    m_CurrentResolution = Screen.currentResolution;
                    CalculateWindowPos();
                }

                GUILayoutOption[] layoutOptions = new GUILayoutOption[] {
                    GUILayout.MaxHeight(Screen.height * 0.8f),
                    GUILayout.MinHeight(Screen.height * 0.8f),
                    GUILayout.MinWidth(Screen.width),
                };

                m_rectWindow = GUILayout.Window(0, m_rectWindow, DrawWindow, "", StyleDefs[Enums.EGUIStyleID.TRANSPARENT], layoutOptions);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            DrawModTable();
            DrawModSettings();
            GUILayout.EndHorizontal();
        }

        private void CalculateWindowPos()
        {
            m_v2WindowSize = new Vector2(Screen.width, Screen.height);
            m_rectWindow = new Rect(m_v2WindowSize.x * 0.1f, m_v2WindowSize.y * 0.1f, m_v2WindowSize.x * 0.7f, m_v2WindowSize.y * 0.7f);
            m_rectModsRect = new Rect(m_rectWindow.x, m_rectWindow.y, m_rectWindow.width * 0.25f, m_rectWindow.height * 0.5f);
            Vector2 v2ModsRectOffset = new Vector2(m_rectModsRect.x + m_rectModsRect.width, m_rectModsRect.y);
            m_rectSettingsRect = new Rect(v2ModsRectOffset.x, v2ModsRectOffset.y, m_v2WindowSize.x - v2ModsRectOffset.x * 2, m_v2WindowSize.y);
        }

        private void DrawModSettings()
        {
            GUILayoutOption[] layoutOptions = new GUILayoutOption[] {
                GUILayout.MaxHeight(m_rectSettingsRect.height),
                GUILayout.MinHeight(m_rectSettingsRect.height),
                GUILayout.MaxWidth(m_rectSettingsRect.width),
                GUILayout.MinWidth(m_rectSettingsRect.width)
            };

            if (m_iSelectedModIdx != -1)
            {
                GUILayout.BeginVertical(GUILayout.ExpandHeight(false));
                GUILayout.Label("Settings", StyleDefs[Enums.EGUIStyleID.TITLE]);
                m_v2SettingsScrollPos = GUILayout.BeginScrollView(m_v2SettingsScrollPos, layoutOptions);

                GUILayout.BeginVertical(StyleDefs[Enums.EGUIStyleID.WINDOW]);
                if (m_iSelectedModIdx != -1)
                    Mods[m_iSelectedModIdx].InvokeOnGUI();

                GUILayout.EndVertical();
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }

        private void DrawModTable()
        {
            GUILayoutOption[] layoutOptions = new GUILayoutOption[] {
                GUILayout.MaxHeight(m_rectModsRect.height),
                GUILayout.MinHeight(m_rectModsRect.height),
                GUILayout.MaxWidth(m_rectModsRect.width),
                GUILayout.MinWidth(m_rectModsRect.width),
            };

            GUILayout.BeginVertical();
            GUILayout.Label("Installed Mods", StyleDefs[Enums.EGUIStyleID.TITLE]);
            GUILayout.Space(5f);

            m_v2ModsScrollPos = GUILayout.BeginScrollView(m_v2ModsScrollPos, layoutOptions);

            GUILayout.BeginVertical(StyleDefs[Enums.EGUIStyleID.TRANSPARENT]);
            for (int i = 0; i < Mods.Count; i++)
            {
                GUIStyle buttonStyle = (m_iSelectedModIdx == i)
                    ? StyleDefs[Enums.EGUIStyleID.SELECTED_BUTTON]
                    : StyleDefs[Enums.EGUIStyleID.BUTTON];

                GUIStyle style = new GUIStyle();
                style.normal.background = Util.MakeTexture(16, 16, new Color(1f, 0f, 0f, 1f));

                if (GUILayout.Button(Mods[i].GetModName(), buttonStyle, GUILayout.ExpandWidth(false)))
                {
                    m_iSelectedModIdx = (m_iSelectedModIdx == i) ? -1 : i;
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close", StyleDefs[Enums.EGUIStyleID.ACTION_BUTTON], GUILayout.ExpandWidth(false)))
            {
                ToggleWindow();
            }
            GUILayout.EndVertical();
        }

        private void ToggleWindow()
        {
            ToggleWindow(!IsOpen);
        }

        private void ToggleWindow(bool isOpen)
        {
            if (isOpen == IsOpen)
                return;

            m_bIsOpen = isOpen;

            try
            {
                LoadVortexOverlay(isOpen);
            }
            catch (Exception e)
            {
                // We didn't manage to load the Overlay.
                //  That's fine, keep going.
                LoggerDelegates.LogInfo(e);
            }

            if (isOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void LoadVortexOverlay(bool value)
        {
            if (value)
            {
                var vortexOverlay = m_UIAssetBundle.LoadAsset(Constants.VORTEX_OVERLAY_PREFAB_NAME);
                m_goOverlay = Instantiate(vortexOverlay) as GameObject;
                m_goOverlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                m_goOverlay.GetComponent<Canvas>().sortingOrder = Int16.MaxValue;

                var children = m_goOverlay.GetComponentsInChildren<RectTransform>();
                var logo = children.Where(child => child.name == "VortexLogo").SingleOrDefault();
                var panel = children.Where(child => child.name == "VortexPanel").SingleOrDefault();

                logo.gameObject.AddComponent(typeof(LogoFade));
                panel.gameObject.AddComponent(typeof(ScrollScript));

                DontDestroyOnLoad(m_goOverlay);
            }
            else
            {
                if (m_goOverlay)
                    Destroy(m_goOverlay);
            }
        }
    }
}