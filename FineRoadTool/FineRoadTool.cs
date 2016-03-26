using ICities;
using UnityEngine;

using System;
using System.Reflection;

using ColossalFramework;
using ColossalFramework.UI;

namespace FineRoadTool
{
    public class FineRoadToolLoader : LoadingExtensionBase
    {
        private GameObject m_gameObject;

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (m_gameObject == null)
            {
                m_gameObject = new GameObject("FineRoadTool");
                m_gameObject.AddComponent<FineRoadTool>();
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
        public const string settingsFileName = "FineRoadTool";

        private int m_elevation = 0;
        private int m_elevationStep = 3;

        private int m_min;
        private int m_max;

        private NetTool m_tool;
        private BulldozeTool m_bulldozeTool;
        private FieldInfo m_elevationField;

        #region Default value storage
        private NetInfo m_current;
        private NetInfo m_elevated;
        private NetInfo m_bridge;
        private NetInfo m_slope;
        private NetInfo m_tunnel;
        private bool m_flattenTerrain;
        private bool m_followTerrain;
        #endregion

        private RoadAIWrapper m_roadAI;
        private Mode m_mode;

        private UILabel m_label;
        private bool m_buttonExists;

        public static readonly SavedInt elevationStep = new SavedInt("elevationStep", settingsFileName, 3, true);

        public enum Mode
        {
            Normal,
            Ground,
            Elevated,
            Bridge
        }

        public Mode mode
        {
            get { return m_mode; }
            set
            {
                if (value != m_mode)
                {
                    m_mode = value;
                    UpdatePrefab();
                }
            }
        }

        public bool isActive
        {
            get
            {
                bool canElevate = m_min != 0 || m_max != 0;
                return m_tool != null && m_current != null && m_roadAI != null && (canElevate || m_buttonExists);
            }
        }

        public void Start()
        {
            m_tool = GameObject.FindObjectOfType<NetTool>();
            if (m_tool == null)
            {
                DebugUtils.Log("NetTool not found.");
                enabled = false;
                return;
            }

            m_bulldozeTool = GameObject.FindObjectOfType<BulldozeTool>();
            if (m_bulldozeTool == null)
            {
                DebugUtils.Log("BulldozeTool not found.");
                enabled = false;
                return;
            }

            m_elevationField = m_tool.GetType().GetField("m_elevation", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m_elevationField == null)
            {
                DebugUtils.Log("NetTool m_elevation field not found");
                m_tool = null;
                enabled = false;
                return;
            }

            try
            {
                m_tool.GetType().GetField("m_buildElevationUp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(m_tool, new SavedInputKey("", Settings.gameSettingsFile));
                m_tool.GetType().GetField("m_buildElevationDown", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(m_tool, new SavedInputKey("", Settings.gameSettingsFile));
            }
            catch (Exception e)
            {
                DebugUtils.Log("Couldn't disable NetTool elevation keys");
                DebugUtils.LogException(e);
                m_tool = null;
                enabled = false;
                return;
            }

            m_elevationStep = elevationStep;

            CreateLabel();

            DebugUtils.Log("Initialized");
        }

        public void Update()
        {
            if (m_tool == null) return;

            if (m_label.isVisible != isActive) m_label.isVisible = isActive;

            NetInfo prefab = m_tool.enabled ? m_tool.m_prefab : null;
            if (m_bulldozeTool.enabled) prefab = m_current;

            if (prefab != m_current)
            {
                RestorePrefab();
                m_elevated = m_bridge = m_slope = m_tunnel = null;
                m_min = m_max = 0;
                m_current = prefab;

                if (m_current == null) return;
                StorePrefab();
                AttachLabel();

                if (isActive)
                {
                    DebugUtils.Log(m_current.name + " selected");
                    UpdatePrefab();
                    m_elevation = (int)m_elevationField.GetValue(m_tool);
                }
            }
        }

        public void OnGUI()
        {
            if (!isActive) return;

            Event e = Event.current;

            if (m_elevation >= 0 && m_elevation <= -256)
                m_elevation = (int)m_elevationField.GetValue(m_tool);
            else
                UpdateElevation();

            if (OptionsKeymapping.elevationUp.IsPressed(e))
            {
                m_elevation += Mathf.RoundToInt(256f * m_elevationStep / 12f);
                UpdateElevation();
            }
            else if (OptionsKeymapping.elevationDown.IsPressed(e))
            {
                m_elevation -= Mathf.RoundToInt(256f * m_elevationStep / 12f);
                UpdateElevation();
            }
            else if (OptionsKeymapping.elevationStepUp.IsPressed(e))
            {
                if (m_elevationStep < 12)
                {
                    m_elevationStep++;
                    elevationStep.value = m_elevationStep;
                    DebugUtils.Log("Elevation step set at " + m_elevationStep + "m");
                }
            }
            else if (OptionsKeymapping.elevationStepDown.IsPressed(e))
            {
                if (m_elevationStep > 1)
                {
                    m_elevationStep--;
                    elevationStep.value = m_elevationStep;
                    DebugUtils.Log("Elevation step set at " + m_elevationStep + "m");
                }
            }
            else if (OptionsKeymapping.modesCycleRight.IsPressed(e))
            {
                if (m_mode < Mode.Bridge)
                    mode++;
                else
                    mode = Mode.Normal;
            }
            else if (OptionsKeymapping.modesCycleLeft.IsPressed(e))
            {
                if (m_mode > Mode.Normal)
                    mode--;
                else
                    mode = Mode.Bridge;
            }
            else if (OptionsKeymapping.elevationReset.IsPressed(e))
            {
                m_elevation = 0;
                UpdateElevation();
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

                int elevation = Mathf.RoundToInt(m_elevation / 256f * 12f);
                m_label.text += "\n" + elevation + "m";
            }
        }

        private void UpdateElevation()
        {
            if (!isActive) return;

            m_elevation = Mathf.Clamp(m_elevation, m_min * 256, m_max * 256);
            if (m_elevationStep < 3) m_elevation = Mathf.RoundToInt(Mathf.RoundToInt(m_elevation / (256f / 12f)) * (256f / 12f));

            m_elevationField.SetValue(m_tool, m_elevation);
        }

        private void StorePrefab()
        {
            if (m_current == null) return;

            m_current.m_netAI.GetElevationLimits(out m_min, out m_max);

            m_roadAI = new RoadAIWrapper(m_current.m_netAI);
            if (!m_roadAI.hasElevation) return;

            m_elevated = m_roadAI.elevated;
            m_bridge = m_roadAI.bridge;
            m_slope = m_roadAI.slope;
            m_tunnel = m_roadAI.tunnel;
            m_followTerrain = m_current.m_followTerrain;
            m_flattenTerrain = m_current.m_flattenTerrain;
        }

        private void RestorePrefab()
        {
            if (m_current == null || !m_roadAI.hasElevation) return;

            m_roadAI.info = m_current;
            m_roadAI.elevated = m_elevated;
            m_roadAI.bridge = m_bridge;
            m_roadAI.slope = m_slope;
            m_roadAI.tunnel = m_tunnel;
            m_current.m_followTerrain = m_followTerrain;
            m_current.m_flattenTerrain = m_flattenTerrain;
        }

        private void UpdatePrefab()
        {
            if (m_current == null || !m_roadAI.hasElevation) return;

            RestorePrefab();

            switch (m_mode)
            {
                case Mode.Ground:
                    m_roadAI.elevated = m_current;
                    m_roadAI.bridge = null;
                    m_roadAI.slope = null;
                    m_roadAI.tunnel = m_current;
                    m_current.m_followTerrain = false;
                    m_current.m_flattenTerrain = true;
                    break;
                case Mode.Elevated:
                    if (m_elevated != null)
                    {
                        m_roadAI.info = m_elevated;
                        m_roadAI.elevated = m_elevated;
                        m_roadAI.bridge = null;
                        m_roadAI.tunnel = m_elevated;
                    }
                    break;
                case Mode.Bridge:
                    if (m_bridge != null)
                    {
                        m_roadAI.info = m_bridge;
                        m_roadAI.elevated = m_bridge;
                        m_roadAI.tunnel = m_bridge;
                    }
                    break;
            }
        }

        private void CreateLabel()
        {
            if (m_label != null) return;

            m_label = UIView.GetAView().AddUIComponent(typeof(UILabel)) as UILabel;

            if (m_label == null)
            {
                DebugUtils.Log("Couldn't create label");
                return;
            }

            m_label.autoSize = false;
            m_label.size = new Vector2(36, 36);
            m_label.position = Vector2.zero;
            m_label.textColor = Color.white;
            m_label.textScale = 0.7f;
            m_label.dropShadowOffset = new Vector2(2, -2);
            m_label.useDropShadow = true;
            m_label.backgroundSprite = "OptionBaseDisabled";

            m_label.textAlignment = UIHorizontalAlignment.Center;
            m_label.wordWrap = true;

            m_label.text = "3m\nNrm\n0m";
            m_label.tooltip = "Fine Road Tool " + ModInfo.version + "\nCtrl + Up/Down : Change elevation step\nCtrl + Left/Right : Change mode";
            m_label.isVisible = false;

            m_label.eventZOrderChanged += (c, p) =>
            {
                if (m_label.parent is UIMultiStateButton)
                    m_label.relativePosition = Vector2.zero;
                else
                    m_label.relativePosition = new Vector2(36, 0);
            };
        }

        private void AttachLabel()
        {
            m_buttonExists = false;

            UIPanel optionBar = UIView.Find<UIPanel>("OptionsBar");

            if (optionBar == null)
            {
                DebugUtils.Log("OptionBar not found!");
                return;
            }

            foreach (UIComponent panel in optionBar.components)
            {
                if (panel is UIPanel && panel.isVisible)
                {
                    UIMultiStateButton button = panel.Find<UIMultiStateButton>("ElevationStep");
                    if (button == null) continue;
                    m_label.transform.SetParent(button.transform);
                    m_buttonExists = true;
                    return;
                }
            }

            m_label.transform.SetParent(optionBar.transform);
        }
    }
}
