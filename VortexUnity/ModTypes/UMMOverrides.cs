using UnityEngine;
using VortexUnity;

namespace UnityModManagerNet
{
    public partial class UnityModManager
    {
        // UMM GUI Style wrapper. Exposed primarily for UMM mods to use.
        public class UI : MonoBehaviour
        {
            private static UI m_instance = null;
            public static UI Instance
            {
                get
                {
                    if (m_instance == null)
                    {
                        GameObject bullshit = new GameObject(typeof(UI).FullName, typeof(UI));
                        m_instance = bullshit.GetComponent<UI>();
                        bullshit.transform.SetParent(VortexUI.Instance.transform);
                    }

                    return m_instance;
                }
            }

            public static GUIStyle bold { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.H2]; } }
            public static GUIStyle h1 { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.H1]; } }
            public static GUIStyle h2 { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.H2]; } }
            public static GUIStyle window { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.WINDOW]; } }
            public static GUIStyle button { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.BUTTON]; } }
            public static GUIStyle settings { get { return VortexUI.StyleDefs[Enums.EGUIStyleID.SETTINGS]; } }
        }
    }
}
