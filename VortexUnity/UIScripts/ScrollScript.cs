using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VortexUnity
{
    public class ScrollScript : MonoBehaviour
    {
        public float m_fScrollSpeed = -5f;
        private float m_rectWidth = 20f;
        private Vector2 m_v2InitialPos = Vector2.zero;

        private RectTransform[] m_children = null;

        void Start()
        {
            m_v2InitialPos = new Vector2(0f, Screen.height * 0.5f);
            transform.position = m_v2InitialPos;
            Rect rect = GetComponent<RectTransform>().rect;
            m_rectWidth = rect.width;

            m_children = GetComponentsInChildren<RectTransform>()
                .Where(child => child.transform != transform)
                .ToArray();

            if (m_children.Length >= 2)
            {
                m_children[0].transform.localPosition = new Vector2(m_rectWidth, 0f);
                m_children[1].transform.localPosition = new Vector2(-m_rectWidth, 0f);
            }
        }

        void Update()
        {
            float fNewPos = Mathf.Repeat(Time.realtimeSinceStartup * m_fScrollSpeed, m_rectWidth);
            transform.position = m_v2InitialPos + (Vector2.right * fNewPos);
        }
    }
}