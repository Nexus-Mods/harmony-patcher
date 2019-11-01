using System.Collections;
using UnityEngine.UI;
using UnityEngine;

using VortexHarmonyInstaller.Delegates;

namespace VortexUnity
{
    public class LogoFade : MonoBehaviour
    {
        private const float FADE_TIME = 1f;
        private const float MIN_ALPHA = 0.5f;

        private Color m_InitialColor;
        public Color m_WantedColor;
        private Image m_LogoRef = null;
        private float m_fStartTime = 0f;

        void Start()
        {
            m_fStartTime = Time.time;
            m_LogoRef = GetComponent<Image>();
            m_InitialColor = m_LogoRef.color;
            m_WantedColor = new Color(m_InitialColor.r, m_InitialColor.g, m_InitialColor.b, MIN_ALPHA);
            if (m_LogoRef != null)
                StartCoroutine(FadeLoop());
        }

        private IEnumerator FadeLoop()
        {
            while (true)
            {
                float t = (Mathf.Sin(Time.realtimeSinceStartup - m_fStartTime) * FADE_TIME);
                m_LogoRef.color = Color.Lerp(m_InitialColor, m_WantedColor, t);

                yield return null;
            }
        }
    }
}