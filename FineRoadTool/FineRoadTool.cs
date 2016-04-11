using ICities;
using UnityEngine;

using System;
using System.Collections.Generic;
using System.Reflection;

using ColossalFramework;
using ColossalFramework.Math;
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

        private NetTool m_tool;
        private BulldozeTool m_bulldozeTool;

        private FieldInfo m_buildErrors;
        private FieldInfo m_elevationField;
        private FieldInfo m_elevationUpField;
        private FieldInfo m_elevationDownField;
        private bool m_keyDisabled;
        private Vector2 m_mousePosition;

        #region Default value storage
        private NetInfo m_current;
        private NetInfo m_elevated;
        private NetInfo m_bridge;
        private NetInfo m_slope;
        private NetInfo m_tunnel;
        private float m_maxSlope;
        private bool m_flattenTerrain;
        private bool m_followTerrain;
        #endregion

        private RoadAIWrapper m_roadAI;
        private Mode m_mode;
        private bool m_straightSlope = false;

        private UIToolOptionsButton m_toolOptionButton;
        private bool m_buttonExists;
        private bool m_activated;
        private bool m_toolEnabled;
        private bool m_bulldozeToolEnabled;
        private int m_slopeErrorCount;

        private int m_nodeCount;
        private List<int> m_nodes = new List<int>();
        private List<int> m_newNodes = new List<int>();

        public static readonly SavedInt savedElevationStep = new SavedInt("elevationStep", settingsFileName, 3, true);

        public static FineRoadTool instance;

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
                    m_toolOptionButton.UpdateInfo();
                }
            }
        }

        public int elevationStep
        {
            get { return m_elevationStep; }
            set { m_elevationStep = Mathf.Clamp(value, 1, 12); }
        }

        public int elevation
        {
            get { return Mathf.RoundToInt(m_elevation / 256f * 12f); }
        }

        public bool isActive
        {
            get { return m_activated; }
        }

        public bool straightSlope
        {
            get { return m_straightSlope; }

            set
            {
                if (value != m_straightSlope)
                {
                    m_straightSlope = value;
                    m_mousePosition = Vector2.zero;
                    UpdatePrefab();
                    m_toolOptionButton.UpdateInfo();
                }
            }
        }

        public void Start()
        {
            instance = this;

            // Getting NetTool
            m_tool = GameObject.FindObjectOfType<NetTool>();
            if (m_tool == null)
            {
                DebugUtils.Log("NetTool not found.");
                enabled = false;
                return;
            }

            // Getting BulldozeTool
            m_bulldozeTool = GameObject.FindObjectOfType<BulldozeTool>();
            if (m_bulldozeTool == null)
            {
                DebugUtils.Log("BulldozeTool not found.");
                enabled = false;
                return;
            }

            // Getting NetTool private fields
            m_buildErrors = m_tool.GetType().GetField("m_buildErrors", BindingFlags.NonPublic | BindingFlags.Instance);
            m_elevationField = m_tool.GetType().GetField("m_elevation", BindingFlags.NonPublic | BindingFlags.Instance);
            m_elevationUpField = m_tool.GetType().GetField("m_buildElevationUp", BindingFlags.NonPublic | BindingFlags.Instance);
            m_elevationDownField = m_tool.GetType().GetField("m_buildElevationDown", BindingFlags.NonPublic | BindingFlags.Instance);

            if (m_buildErrors == null || m_elevationField == null || m_elevationUpField == null || m_elevationDownField == null)
            {
                DebugUtils.Log("NetTool fields not found");
                m_tool = null;
                enabled = false;
                return;
            }

            // Restoring elevation step
            m_elevationStep = savedElevationStep;

            // Creating UI
            CreateToolOptionsButton();

            DebugUtils.Log("Initialized");
        }

        public void Update()
        {
            if (m_tool == null) return;

            try
            {
                // Getting selected prefab
                NetInfo prefab = m_tool.enabled || m_bulldozeTool.enabled ? m_tool.m_prefab : null;

                // Has the prefab/tool changed?
                if (prefab != m_current || m_toolEnabled != m_tool.enabled || m_bulldozeToolEnabled != m_bulldozeTool.enabled)
                {
                    m_toolEnabled = m_tool.enabled;
                    m_bulldozeToolEnabled = m_bulldozeTool.enabled;

                    if (prefab == null && !m_bulldozeTool.enabled)
                        Deactivate();
                    else
                        Activate(prefab);
                }

                // Check new nodes
                if(m_nodeCount != NetManager.instance.m_nodeCount)
                {
                    m_nodeCount = NetManager.instance.m_nodeCount;
                    FixFlags();
                }
            }
            catch (Exception e)
            {
                enabled = false;
                DebugUtils.Log("Update failed");
                DebugUtils.LogException(e);
            }
        }

        public void OnGUI()
        {
            if (!isActive) return;

            Event e = Event.current;

            // Updating the elevation
            if (m_elevation >= 0 || m_elevation <= -256)
                m_elevation = (int)m_elevationField.GetValue(m_tool);
            else
                UpdateElevation();

            // Checking key presses
            if (OptionsKeymapping.elevationUp.IsPressed(e))
            {
                m_elevation += Mathf.RoundToInt(256f * m_elevationStep / 12f);
                UpdateElevation();
                m_mousePosition = Vector2.zero;
            }
            else if (OptionsKeymapping.elevationDown.IsPressed(e))
            {
                m_elevation -= Mathf.RoundToInt(256f * m_elevationStep / 12f);
                UpdateElevation();
                m_mousePosition = Vector2.zero;
            }
            else if (OptionsKeymapping.elevationStepUp.IsPressed(e))
            {
                if (m_elevationStep < 12)
                {
                    m_elevationStep++;
                    savedElevationStep.value = m_elevationStep;
                    m_toolOptionButton.UpdateInfo();
                }
            }
            else if (OptionsKeymapping.elevationStepDown.IsPressed(e))
            {
                if (m_elevationStep > 1)
                {
                    m_elevationStep--;
                    savedElevationStep.value = m_elevationStep;
                    m_toolOptionButton.UpdateInfo();
                }
            }
            else if (OptionsKeymapping.modesCycleRight.IsPressed(e))
            {
                if (m_mode < Mode.Bridge)
                    mode++;
                else
                    mode = Mode.Normal;
                m_toolOptionButton.UpdateInfo();
            }
            else if (OptionsKeymapping.modesCycleLeft.IsPressed(e))
            {
                if (m_mode > Mode.Normal)
                    mode--;
                else
                    mode = Mode.Bridge;
                m_toolOptionButton.UpdateInfo();
            }
            else if (OptionsKeymapping.elevationReset.IsPressed(e))
            {
                m_elevation = 0;
                UpdateElevation();
                m_mousePosition = Vector2.zero;
                m_toolOptionButton.UpdateInfo();
            }
            else if (OptionsKeymapping.toggleStraightSlope.IsPressed(e))
            {
                DebugUtils.Log("toggleStraightSlope");
                straightSlope = !m_straightSlope;
            }

            if (m_straightSlope)
            {
                bool slopeTooSteep = ((ToolBase.ToolErrors)m_buildErrors.GetValue(m_tool) & ToolBase.ToolErrors.SlopeTooSteep) != ToolBase.ToolErrors.None;

                if (e.mousePosition != m_mousePosition)
                {
                    m_mousePosition = e.mousePosition;
                    m_slopeErrorCount = 0;
                    UpdateMaxSlope();
                }
                else if (slopeTooSteep)
                {
                    if (m_slopeErrorCount < 5) m_slopeErrorCount++;
                    UpdateMaxSlope();
                }
                else m_slopeErrorCount = 0;
            }
        }

        private void Activate(NetInfo prefab)
        {
            RestorePrefab();
            m_current = prefab;

            if (prefab == null) return;

            StorePrefab();
            AttachToolOptionsButton();

            // Is it a valid prefab?
            int min, max;
            m_current.m_netAI.GetElevationLimits(out min, out max);

            if ((m_bulldozeTool.enabled || (min == 0 && max == 0)) && !m_buttonExists)
            {
                Deactivate();
                return;
            }

            DisableDefaultKeys();
            m_elevation = (int)m_elevationField.GetValue(m_tool);
            UpdatePrefab();

            m_activated = true;
            m_toolOptionButton.isVisible = true;
            m_toolOptionButton.UpdateInfo();

            DebugUtils.Log("Activated: " + prefab.name + " selected");
        }

        private void Deactivate()
        {
            if (!isActive) return;

            RestorePrefab();
            RestoreDefaultKeys();

            m_toolOptionButton.isVisible = false;
            m_activated = false;

            DebugUtils.Log("Deactivated");
        }

        private void DisableDefaultKeys()
        {
            if (m_keyDisabled) return;

            SavedInputKey emptyKey = new SavedInputKey("", Settings.gameSettingsFile);

            m_elevationUpField.SetValue(m_tool, emptyKey);
            m_elevationDownField.SetValue(m_tool, emptyKey);

            m_keyDisabled = true;
        }

        private void RestoreDefaultKeys()
        {
            if (!m_keyDisabled) return;

            m_elevationUpField.SetValue(m_tool, OptionsKeymapping.elevationUp);
            m_elevationDownField.SetValue(m_tool, OptionsKeymapping.elevationDown);

            m_keyDisabled = false;
        }

        private void UpdateElevation()
        {
            int min, max;
            m_current.m_netAI.GetElevationLimits(out min, out max);

            m_elevation = Mathf.Clamp(m_elevation, min * 256, max * 256);
            if (m_elevationStep < 3) m_elevation = Mathf.RoundToInt(Mathf.RoundToInt(m_elevation / (256f / 12f)) * (256f / 12f));

            if ((int)m_elevationField.GetValue(m_tool) != m_elevation)
            {
                m_elevationField.SetValue(m_tool, m_elevation);
                m_toolOptionButton.UpdateInfo();
            }
        }

        private void UpdateMaxSlope()
        {
            if (m_current == null) return;

            if (NetTool.m_nodePositionsMain.m_size > 1)
            {
                float slope = m_maxSlope;

                if (m_slopeErrorCount < 5)
                {
                    float length = 0;

                    for (int i = 0; i < NetTool.m_nodePositionsMain.m_size - 1; i++)
                    {
                        length += VectorUtils.LengthXZ(NetTool.m_nodePositionsMain.m_buffer[i].m_position - NetTool.m_nodePositionsMain.m_buffer[i + 1].m_position);
                    }

                    if (length != 0)
                    {
                        Vector3 a = NetTool.m_nodePositionsMain.m_buffer[0].m_position;
                        Vector3 b = NetTool.m_nodePositionsMain.m_buffer[NetTool.m_nodePositionsMain.m_size - 1].m_position;

                        slope = Mathf.Clamp(Mathf.Sqrt((a.y - b.y) * (a.y - b.y) / length * length) + 0.000001f, 0, m_maxSlope);
                    }
                }

                m_current.m_maxSlope = slope;
                if (m_elevated != null) m_elevated.m_maxSlope = slope;
                if (m_bridge != null) m_bridge.m_maxSlope = slope;
                if (m_slope != null) m_slope.m_maxSlope = slope;
                if (m_tunnel != null) m_tunnel.m_maxSlope = slope;
            }
        }

        private void StorePrefab()
        {
            if (m_current == null) return;

            m_followTerrain = m_current.m_followTerrain;
            m_flattenTerrain = m_current.m_flattenTerrain;

            m_maxSlope = m_current.m_maxSlope;

            m_roadAI = new RoadAIWrapper(m_current.m_netAI);

            if (m_roadAI.hasElevation)
            {
                m_elevated = m_roadAI.elevated;
                m_bridge = m_roadAI.bridge;
                m_slope = m_roadAI.slope;
                m_tunnel = m_roadAI.tunnel;
            }
        }

        private void RestorePrefab()
        {
            if (m_current == null) return;

            m_current.m_followTerrain = m_followTerrain;
            m_current.m_flattenTerrain = m_flattenTerrain;
            m_current.m_maxSlope = m_maxSlope;

            if (m_elevated != null) m_elevated.m_maxSlope = m_maxSlope;
            if (m_bridge != null) m_bridge.m_maxSlope = m_maxSlope;
            if (m_slope != null) m_slope.m_maxSlope = m_maxSlope;
            if (m_tunnel != null) m_tunnel.m_maxSlope = m_maxSlope;

            if (m_roadAI.hasElevation)
            {
                m_roadAI.info = m_current;
                m_roadAI.elevated = m_elevated;
                m_roadAI.bridge = m_bridge;
                m_roadAI.slope = m_slope;
                m_roadAI.tunnel = m_tunnel;
            }
        }

        private void UpdatePrefab()
        {
            if (m_current == null || !m_roadAI.hasElevation) return;

            RestorePrefab();

            switch (m_mode)
            {
                case Mode.Normal:
                    if (m_straightSlope)
                    {
                        m_current.m_followTerrain = false;
                        m_current.m_flattenTerrain = true;
                    }
                    break;
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
                    }
                    break;
                case Mode.Bridge:
                    if (m_bridge != null)
                    {
                        m_roadAI.info = m_bridge;
                        m_roadAI.elevated = m_bridge;
                    }
                    break;
            }
        }

        private void FixFlags()
        {
            RestorePrefab();
            for (int i = 0; i < NetManager.instance.m_nodes.m_size; i++)
            {
                if ((NetManager.instance.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Underground) == NetNode.Flags.Underground)
                {
                    NetInfo info = NetManager.instance.m_nodes.m_buffer[i].Info;
                    if (info == null || info.m_netAI == null) return;

                    RoadAIWrapper roadAI = new RoadAIWrapper(info.m_netAI);
                    if (roadAI.hasElevation && info != roadAI.tunnel && info != roadAI.slope)
                        NetManager.instance.m_nodes.m_buffer[i].m_flags &= ~NetNode.Flags.Underground;
                }
            }
            UpdatePrefab();
        }

        private void StoreNodes()
        {
            m_nodes.Clear();
            for(int i = 0; i< NetManager.instance.m_nodes.m_size; i++)
            {
                if (NetManager.instance.m_nodes.m_buffer[i].m_flags != NetNode.Flags.None)
                    m_nodes.Add(i);
            }
        }

        private void FindNewNodes()
        {
            m_newNodes.Clear();
            for (int i = 0; i < NetManager.instance.m_nodes.m_size; i++)
            {
                if (NetManager.instance.m_nodes.m_buffer[i].m_flags != NetNode.Flags.None && !m_nodes.Contains(i))
                {
                    m_nodes.Add(i);
                    m_newNodes.Add(i);
                }
            }
        }

        private void CreateToolOptionsButton()
        {
            if (m_toolOptionButton != null) return;

            try
            {
                m_toolOptionButton = UIView.GetAView().AddUIComponent(typeof(UIToolOptionsButton)) as UIToolOptionsButton;

                if (m_toolOptionButton == null)
                {
                    DebugUtils.Log("Couldn't create label");
                    return;
                }

                m_toolOptionButton.autoSize = false;
                m_toolOptionButton.size = new Vector2(36, 36);
                m_toolOptionButton.position = Vector2.zero;
                m_toolOptionButton.isVisible = false;
            }
            catch (Exception e)
            {
                enabled = false;
                DebugUtils.Log("CreateToolOptionsButton failed");
                DebugUtils.LogException(e);
            }
        }

        private void AttachToolOptionsButton()
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
                    m_toolOptionButton.transform.SetParent(button.transform);
                    button.tooltip = null;
                    m_buttonExists = true;
                    return;
                }
            }

            m_toolOptionButton.transform.SetParent(optionBar.transform);

            DebugUtils.Log("ElevationStep not found. Absolute position: " + m_toolOptionButton.absolutePosition);
        }
    }
}
