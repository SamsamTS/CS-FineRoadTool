using ICities;
using UnityEngine;

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using ColossalFramework;
using ColossalFramework.Threading;
using ColossalFramework.UI;
using ColossalFramework.Globalization;

namespace FineRoadTool
{
    public class FineRoadToolLoader : LoadingExtensionBase
    {
        private GameObject m_gameObject;
        private FineRoadTool m_watcher;

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (m_gameObject == null)
            {
                m_gameObject = new GameObject("FineRoadTool");
                m_watcher = m_gameObject.AddComponent<FineRoadTool>();
            }
        }

        public override void OnLevelUnloading()
        {
            if (m_gameObject != null)
            {
                GameObject.Destroy(m_gameObject);
                m_gameObject = null;
            }
        }
    }

    public class FineRoadTool : MonoBehaviour
    {
        private SavedInputKey m_elevationUp;
        private SavedInputKey m_elevationDown;

        private int m_elevation = 0;
        private int m_elevationStep = 3;

        private FieldInfo m_elevationField;

        private NetTool m_tool;
        private NetInfo m_current;
        private NetInfo m_elevated;
        private NetInfo m_bridge;
        private RoadAI m_roadAI;
        private Mode m_mode;

        private UILabel m_label;

        public enum Mode
        {
            Normal,
            Ground,
            Elevated,
            Bridge
        }

        public Mode mode
        {
            set
            {
                if(value != m_mode)
                {
                    m_mode = value;
                    UpdatePrefab();
                }
            }

            get { return m_mode; }
        }

        public void Start()
        {
            m_tool = GameObject.FindObjectOfType<NetTool>();
            if(m_tool != null)
            {
                DebugUtils.Log("Initialized");
            }

            m_elevationUp = new SavedInputKey(Settings.buildElevationUp, Settings.gameSettingsFile, DefaultSettings.buildElevationUp, true);
            m_elevationDown = new SavedInputKey(Settings.buildElevationDown, Settings.gameSettingsFile, DefaultSettings.buildElevationDown, true);

            m_elevationField = m_tool.GetType().GetField("m_elevation", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void Update()
        {
            if (m_tool == null) return;

            NetInfo prefab = m_tool.m_prefab as NetInfo;

            if(prefab != m_current)
            {
                m_tool.m_elevationDivider = 1024;
                RestorePrefab();
                m_current = prefab;
                StorePrefab();
                UpdatePrefab();

                //try
                //{
                    if (m_label == null)
                    {
                        UIMultiStateButton button = UIView.Find<UIPanel>("RoadsOptionPanel(RoadsPanel)").Find<UIMultiStateButton>("ElevationStep");

                        if (button != null)
                        {
                            button.tooltip = "Ctrl + Up/Down : Change elevation step\nCtrl + Left/Right : Change mode";
                            button.isInteractive = false;

                            m_label = button.AddUIComponent<UILabel>();
                            m_label.autoSize = false;
                            m_label.size = button.size;
                            m_label.position = Vector2.zero;
                            m_label.textColor = Color.white;
                            m_label.outlineColor = Color.black;
                            m_label.outlineSize = 1;
                            m_label.useOutline = true;
                            m_label.backgroundSprite = "IconPolicyBaseCircleDisabled";

                            m_label.textAlignment = UIHorizontalAlignment.Center;
                            m_label.wordWrap = true;

                            m_label.text = "3m\nNrm";
                            m_label.Show(true);
                        }
                    }
                //}
                //catch { }
            }
        }

        public void OnGUI()
        {
            if (m_current == null || !m_tool.enabled) return;

            Event e = Event.current;

            if (m_elevationUp.IsPressed(e))
            {
                m_elevation += Mathf.RoundToInt(256f * m_elevationStep / 12f);
                if (m_elevation < 0 && m_elevation > -256) m_elevation = 0;
                UpdateElevation();
            }
            else if (m_elevationDown.IsPressed(e))
            {
                m_elevation -= Mathf.RoundToInt(256f * m_elevationStep / 12f);
                if (m_elevation < 0 && m_elevation > -256) m_elevation = -256;
                UpdateElevation();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow && e.control)
            {
                if (m_elevationStep < 12) m_elevationStep++;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow && e.control)
            {
                if (m_elevationStep > 1) m_elevationStep--;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.RightArrow && e.control)
            {
                if (m_mode < Mode.Bridge)
                    mode++;
                else
                    mode = Mode.Normal;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.LeftArrow && e.control)
            {
                if (m_mode > Mode.Normal)
                    mode--;
                else
                    mode = Mode.Bridge;
            }

            if (m_label != null)
            {
                m_label.text = m_elevationStep + "m\n";

                switch (m_mode)
                {
                    case Mode.Normal:
                        m_label.text += "Nrm";
                        break;
                    case Mode.Ground:
                        m_label.text += "Gnd";
                        break;
                    case Mode.Elevated:
                        m_label.text += "Elv";
                        break;
                    case Mode.Bridge:
                        m_label.text += "Bdg";
                        break;
                }
            }
        }

        private void UpdateElevation()
        {
            m_tool.m_elevationDivider = 1024;

            int min, max;
            m_roadAI.GetElevationLimits(out min, out max);

            m_elevation = Mathf.Clamp(m_elevation, min * 256, max * 256);
            if (m_elevationStep < 3) m_elevation = Mathf.RoundToInt(Mathf.RoundToInt(m_elevation / (256f / 12f)) * (256f / 12f));

            m_elevationField.SetValue(m_tool, m_elevation);
        }

        private void StorePrefab()
        {
            if (m_current == null) return;

            m_roadAI = m_current.m_netAI as RoadAI;
            if (m_roadAI == null) return;

            m_elevated = m_roadAI.m_elevatedInfo;
            m_bridge = m_roadAI.m_bridgeInfo;
        }

        private void RestorePrefab()
        {
            if (m_current == null || m_roadAI == null) return;

            m_roadAI.m_info = m_current;
            m_roadAI.m_elevatedInfo = m_elevated;
            m_roadAI.m_bridgeInfo = m_bridge;
        }

        private void UpdatePrefab()
        {
            if (m_current == null || m_roadAI == null) return;

            switch(m_mode)
            {
                case Mode.Normal:
                    m_roadAI.m_info = m_current;
                    m_roadAI.m_elevatedInfo = m_elevated;
                    m_roadAI.m_bridgeInfo = m_bridge;
                    break;
                case Mode.Ground:
                    m_roadAI.m_info = m_current;
                    m_roadAI.m_elevatedInfo = m_current;
                    m_roadAI.m_bridgeInfo = null;
                    break;
                case Mode.Elevated:
                    m_roadAI.m_info = m_elevated;
                    m_roadAI.m_elevatedInfo = m_elevated;
                    m_roadAI.m_bridgeInfo = null;
                    break;
                case Mode.Bridge:
                    m_roadAI.m_info = m_bridge;
                    m_roadAI.m_elevatedInfo = null;
                    m_roadAI.m_bridgeInfo = m_bridge;
                    break;
            }
        }
    }
}
