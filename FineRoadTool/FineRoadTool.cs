using ICities;
using UnityEngine;

using System;
using System.Diagnostics;
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

    public class FineRoadTool : MonoBehaviour, ISimulationManager
    {
        public const string settingsFileName = "FineRoadTool";

        private int m_elevation = 0;
        private int m_elevationStep = 3;

        private NetTool m_netTool;
        private BulldozeTool m_bulldozeTool;
        private BuildingTool m_buildingTool;

        #region Reflection to private field/methods
        private FieldInfo m_buildErrors;
        private FieldInfo m_elevationField;
        private FieldInfo m_elevationUpField;
        private FieldInfo m_elevationDownField;
        private FieldInfo m_buildingElevationField;
        private FieldInfo m_controlPointCountField;
        private FieldInfo m_upgrading;
        #endregion

        private bool m_keyDisabled;
        private Vector2 m_mousePosition;

        private NetInfo m_current;
        private InfoManager.InfoMode m_infoMode = (InfoManager.InfoMode)(-1);

        private Mode m_mode;
        private bool m_straightSlope = false;

        private UIToolOptionsButton m_toolOptionButton;
        private UIButton m_upgradeButtonTemplate;
        private bool m_buttonExists;
        private bool m_activated;
        private bool m_toolEnabled;
        private bool m_bulldozeToolEnabled;
        private int m_slopeErrorCount;
        private bool m_init;

        private int m_fixNodesCount = 0;
        private ushort m_fixTunnelsCount = 0;
        private Stopwatch m_stopWatch = new Stopwatch();

        private int m_segmentCount;
        private int m_controlPointCount;
        private NetTool.ControlPoint[] m_controlPoints;
        private NetTool.ControlPoint[] m_cachedControlPoints;

        public static readonly SavedInt savedElevationStep = new SavedInt("elevationStep", settingsFileName, 3, true);

        public static FineRoadTool instance;

        #region ISimulationManager
        public virtual void GetData(FastList<ColossalFramework.IO.IDataContainer> data) { }
        public virtual string GetName() { return gameObject.name; }
        public virtual ThreadProfiler GetSimulationProfiler() { return new ThreadProfiler(); }
        public virtual void LateUpdateData(SimulationManager.UpdateMode mode) { }
        public virtual void UpdateData(SimulationManager.UpdateMode mode) { }
        #endregion

        public Mode mode
        {
            get { return m_mode; }
            set
            {
                if (value != m_mode)
                {
                    m_mode = value;

                    RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
                    if (prefab == null) return;

                    prefab.mode = m_mode;
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

                    RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
                    if (prefab == null) return;

                    prefab.Update();
                    m_toolOptionButton.UpdateInfo();
                }
            }
        }

        public void Start()
        {
            instance = this;

            // Getting NetTool
            m_netTool = GameObject.FindObjectOfType<NetTool>();
            if (m_netTool == null)
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

            // Getting BuildingTool
            m_buildingTool = GameObject.FindObjectOfType<BuildingTool>();
            if (m_buildingTool == null)
            {
                DebugUtils.Log("BuildingTool not found.");
                enabled = false;
                return;
            }

            // Getting NetTool private fields
            m_buildErrors = m_netTool.GetType().GetField("m_buildErrors", BindingFlags.NonPublic | BindingFlags.Instance);
            m_elevationField = m_netTool.GetType().GetField("m_elevation", BindingFlags.NonPublic | BindingFlags.Instance);
            m_elevationUpField = m_netTool.GetType().GetField("m_buildElevationUp", BindingFlags.NonPublic | BindingFlags.Instance);
            m_elevationDownField = m_netTool.GetType().GetField("m_buildElevationDown", BindingFlags.NonPublic | BindingFlags.Instance);
            m_buildingElevationField = m_buildingTool.GetType().GetField("m_elevation", BindingFlags.NonPublic | BindingFlags.Instance);
            m_controlPointCountField = m_netTool.GetType().GetField("m_controlPointCount", BindingFlags.NonPublic | BindingFlags.Instance);
            m_upgrading = m_netTool.GetType().GetField("m_upgrading", BindingFlags.NonPublic | BindingFlags.Instance);

            if (m_buildErrors == null || m_elevationField == null || m_elevationUpField == null || m_elevationDownField == null || m_buildingElevationField == null || m_controlPointCountField == null || m_upgrading == null)
            {
                DebugUtils.Log("NetTool fields not found");
                m_netTool = null;
                enabled = false;
                return;
            }

            // Getting Upgrade button template
            try
            {
                m_upgradeButtonTemplate = GameObject.Find("RoadsSmallPanel").GetComponent<GeneratedScrollPanel>().m_OptionsBar.Find<UIButton>("Upgrade");
            }
            catch
            {
                DebugUtils.Log("Upgrade button template not found");
            }

            // Restoring elevation step
            m_elevationStep = savedElevationStep;

            // Creating UI
            CreateToolOptionsButton();

            // Store segment count
            m_segmentCount = NetManager.instance.m_segmentCount;

            // Getting control points
            try
            {
                m_controlPoints = m_netTool.GetType().GetField("m_controlPoints", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m_netTool) as NetTool.ControlPoint[];
                m_cachedControlPoints = m_netTool.GetType().GetField("m_cachedControlPoints", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m_netTool) as NetTool.ControlPoint[];
            }
            catch
            {
                DebugUtils.Log("ControlPoints not found");
            }

            // Init dictionary
            RoadPrefab.Initialize();
            RoadPrefab.singleMode = true;

            // Fix nodes
            FixNodes();

            // Registering manager
            SimulationManager.RegisterSimulationManager(this);

            DebugUtils.Log("Initialized");
        }

        public void Update()
        {
            if (m_netTool == null) return;

            try
            {
                // Getting selected prefab
                NetInfo prefab = m_netTool.enabled || m_bulldozeTool.enabled ? m_netTool.m_prefab : null;

                // Has the prefab/tool changed?
                if (prefab != m_current || m_toolEnabled != m_netTool.enabled || m_bulldozeToolEnabled != m_bulldozeTool.enabled)
                {
                    m_toolEnabled = m_netTool.enabled;
                    m_bulldozeToolEnabled = m_bulldozeTool.enabled;

                    if (prefab == null && !m_bulldozeTool.enabled)
                        Deactivate();
                    else
                        Activate(prefab);
                }

                // Plopping intesection?
                if (m_buildingTool.enabled && !RoadPrefab.singleMode)
                {
                    int elevation = (int)m_buildingElevationField.GetValue(m_buildingTool);
                    RoadPrefab.singleMode = (elevation == 0);
                }
                else
                    RoadPrefab.singleMode = !m_netTool.enabled && !m_bulldozeTool.enabled;
            }
            catch (Exception e)
            {
                DebugUtils.Log("Update failed");
                DebugUtils.LogException(e);

                try
                {
                    Deactivate();
                    RoadPrefab.singleMode = false;
                }
                catch { }
            }
        }

        public void OnDisable()
        {
            Deactivate();
            RoadPrefab.singleMode = false;
        }

        public virtual void SimulationStep(int subStep)
        {
            if (!enabled) return;

            // Resume fixes
            if (m_fixNodesCount != 0 || m_fixTunnelsCount != 0)
            {
                RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
                if (prefab != null) prefab.Restore();

                if (m_fixTunnelsCount != 0) FixTunnels();
                if (m_fixNodesCount != 0) FixNodes();

                if (prefab != null) prefab.Update();
            }

            if (!isActive && !m_bulldozeTool.enabled) return;

            try
            {
                // Check if segment have been created/deleted/updated
                if (m_segmentCount != NetManager.instance.m_segmentCount || (bool)m_upgrading.GetValue(m_netTool))
                {
                    m_segmentCount = NetManager.instance.m_segmentCount;

                    RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
                    if (prefab != null) prefab.Restore();

                    m_fixTunnelsCount = 0;
                    m_fixNodesCount = 0;

                    FixTunnels();
                    FixNodes();

                    if (prefab != null) prefab.Update();
                }

                if (!isActive) return;

                // Fix first control point elevation
                int count = (int)m_controlPointCountField.GetValue(m_netTool);
                if (count != m_controlPointCount && m_controlPointCount == 0 && count == 1)
                {
                    if (FixControlPoint(0))
                    {
                        m_elevation = Mathf.RoundToInt(Mathf.RoundToInt(m_controlPoints[0].m_elevation / m_elevationStep) * m_elevationStep * 256f / 12f);
                        UpdateElevation();
                        m_toolOptionButton.UpdateInfo();
                    }
                }
                // Fix last control point elevation
                else if (count == ((m_netTool.m_mode == NetTool.Mode.Curved || m_netTool.m_mode == NetTool.Mode.Freeform) ? 2 : 1))
                {
                    FixControlPoint(count);
                }
                m_controlPointCount = count;
            }
            catch (Exception e)
            {
                DebugUtils.Log("SimulationStep failed");
                DebugUtils.LogException(e);
            }
        }

        public void OnGUI()
        {
            try
            {
                Event e = Event.current;

                if (m_buildingTool.enabled && RoadPrefab.singleMode)
                {
                    // Checking key presses
                    if (OptionsKeymapping.elevationUp.IsPressed(e) || OptionsKeymapping.elevationDown.IsPressed(e))
                    {
                        RoadPrefab.singleMode = false;
                        BuildingInfo info = m_buildingTool.m_prefab;
                        if (info != null)
                        {
                            // Reset cached value
                            FieldInfo cachedMaxElevation = info.m_buildingAI.GetType().GetField("m_cachedMaxElevation", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (cachedMaxElevation != null) cachedMaxElevation.SetValue(info.m_buildingAI, -1);

                            int min, max;
                            info.m_buildingAI.GetElevationLimits(out min, out max);

                            int elevation = (int)m_buildingElevationField.GetValue(m_buildingTool);
                            elevation += OptionsKeymapping.elevationUp.IsPressed(Event.current) ? 1 : -1;

                            m_buildingElevationField.SetValue(m_buildingTool, Mathf.Clamp(elevation, min, max));
                        }
                        e.Use();
                    }
                    return;
                }
                else if (m_buildingTool.enabled && OptionsKeymapping.elevationReset.IsPressed(e))
                {
                    m_buildingElevationField.SetValue(m_buildingTool, 0);
                }

                if (!isActive) return;

                // Updating the elevation
                if (m_elevation >= 0 || m_elevation <= -256)
                {
                    int currentElevation = (int)m_elevationField.GetValue(m_netTool);
                    if (m_elevation != currentElevation)
                    {
                        m_elevation = currentElevation;
                        m_toolOptionButton.UpdateInfo();
                    }
                }
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
                    if (m_mode < Mode.Tunnel)
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
                        mode = Mode.Tunnel;
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
                    straightSlope = !m_straightSlope;
                    m_toolOptionButton.UpdateInfo();
                }

                if (m_straightSlope)
                {
                    bool slopeTooSteep = ((ToolBase.ToolErrors)m_buildErrors.GetValue(m_netTool) & ToolBase.ToolErrors.SlopeTooSteep) != ToolBase.ToolErrors.None;

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

                if (m_mode == Mode.Tunnel && InfoManager.instance.CurrentMode != InfoManager.InfoMode.Traffic)
                {
                    if (m_infoMode == (InfoManager.InfoMode)(-1))
                        m_infoMode = InfoManager.instance.CurrentMode;

                    InfoManager.instance.SetCurrentMode(InfoManager.InfoMode.Traffic, InfoManager.SubInfoMode.Default);
                }
                else if (m_mode != Mode.Tunnel && m_infoMode != (InfoManager.InfoMode)(-1))
                {
                    InfoManager.instance.SetCurrentMode(m_infoMode, InfoManager.SubInfoMode.Default);
                    m_infoMode = (InfoManager.InfoMode)(-1);
                }
            }
            catch (Exception e)
            {
                DebugUtils.Log("OnGUI failed");
                DebugUtils.LogException(e);
            }
        }

        private void Activate(NetInfo info)
        {
            RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
            if (prefab != null) prefab.Restore();

            m_current = info;
            prefab = RoadPrefab.GetPrefab(info);

            AttachToolOptionsButton(prefab);

            // Is it a valid prefab?
            int min, max;
            m_current.m_netAI.GetElevationLimits(out min, out max);

            if ((m_bulldozeTool.enabled || (min == 0 && max == 0)) && !m_buttonExists)
            {
                Deactivate();
                return;
            }

            DisableDefaultKeys();
            m_elevation = (int)m_elevationField.GetValue(m_netTool);
            if (prefab != null) prefab.mode = m_mode;

            m_segmentCount = NetManager.instance.m_segmentCount;
            m_controlPointCount = 0;

            m_activated = true;
            m_toolOptionButton.isVisible = true;
            m_toolOptionButton.UpdateInfo();

            DebugUtils.Log("Activated: " + info.name + " selected");
        }

        private void Deactivate()
        {
            if (!isActive) return;

            RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
            if (prefab != null) prefab.Restore();
            m_current = null;

            RestoreDefaultKeys();

            if (m_toolOptionButton != null)
                m_toolOptionButton.isVisible = false;

            m_activated = false;

            DebugUtils.Log("Deactivated");
        }

        private void DisableDefaultKeys()
        {
            if (m_keyDisabled) return;

            SavedInputKey emptyKey = new SavedInputKey("", Settings.gameSettingsFile);

            m_elevationUpField.SetValue(m_netTool, emptyKey);
            m_elevationDownField.SetValue(m_netTool, emptyKey);

            m_keyDisabled = true;
        }

        private void RestoreDefaultKeys()
        {
            if (!m_keyDisabled) return;

            m_elevationUpField.SetValue(m_netTool, OptionsKeymapping.elevationUp);
            m_elevationDownField.SetValue(m_netTool, OptionsKeymapping.elevationDown);

            m_keyDisabled = false;
        }

        private void UpdateElevation()
        {
            int min, max;
            m_current.m_netAI.GetElevationLimits(out min, out max);

            m_elevation = Mathf.Clamp(m_elevation, min * 256, max * 256);
            if (m_elevationStep < 3) m_elevation = Mathf.RoundToInt(Mathf.RoundToInt(m_elevation / (256f / 12f)) * (256f / 12f));

            if ((int)m_elevationField.GetValue(m_netTool) != m_elevation)
            {
                m_elevationField.SetValue(m_netTool, m_elevation);
                m_toolOptionButton.UpdateInfo();
            }
        }

        private void UpdateMaxSlope()
        {
            if (m_current == null) return;

            RoadPrefab prefab = RoadPrefab.GetPrefab(m_current);
            if (prefab == null) return;

            if (NetTool.m_nodePositionsMain.m_size > 1)
            {
                float slope = prefab.defaultSlope;

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

                        slope = Mathf.Clamp(Mathf.Sqrt((a.y - b.y) * (a.y - b.y) / (length * length)) + 0.000001f, 0, prefab.defaultSlope);
                    }
                }

                prefab.SetMaxSlope(slope);
            }
        }

        private void FixNodes()
        {
            m_stopWatch.Reset();
            m_stopWatch.Start();

            NetNode[] nodes = NetManager.instance.m_nodes.m_buffer;

            bool singleMode = RoadPrefab.singleMode;
            RoadPrefab.singleMode = false;

            uint max = NetManager.instance.m_nodes.m_size;
            for (int i = m_fixNodesCount; i < max; i++)
            {
                if (nodes[i].m_flags == NetNode.Flags.None || (nodes[i].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.Untouchable) continue;

                if (m_stopWatch.ElapsedMilliseconds >= 1 && i > m_fixNodesCount + 16)
                {
                    m_fixNodesCount = i;
                    RoadPrefab.singleMode = singleMode;
                    return;
                }

                NetInfo info = nodes[i].Info;
                if ((nodes[i].m_flags & NetNode.Flags.Underground) == NetNode.Flags.Underground)
                {
                    if (info == null || info.m_netAI == null) continue;

                    RoadPrefab prefab = RoadPrefab.GetPrefab(info);
                    if (prefab == null) continue;

                    if (info != prefab.roadAI.tunnel && info != prefab.roadAI.slope && !info.m_netAI.IsUnderground())
                    {
                        nodes[i].m_elevation = 0;
                        nodes[i].m_flags = nodes[i].m_flags & ~NetNode.Flags.Underground;

                        // Updating terrain
                        TerrainModify.UpdateArea(nodes[i].m_bounds.min.x, nodes[i].m_bounds.min.z, nodes[i].m_bounds.max.x, nodes[i].m_bounds.max.z, true, true, false);
                    }
                }
            }

            RoadPrefab.singleMode = singleMode;
            m_fixNodesCount = 0;
        }

        private void FixTunnels()
        {
            m_stopWatch.Reset();
            m_stopWatch.Start();

            bool singleMode = RoadPrefab.singleMode;
            RoadPrefab.singleMode = false;

            NetNode[] nodes = NetManager.instance.m_nodes.m_buffer;
            NetSegment[] segments = NetManager.instance.m_segments.m_buffer;

            uint max = NetManager.instance.m_segments.m_size;
            for (ushort i = m_fixTunnelsCount; i < max; i++)
            {
                if (segments[i].m_flags == NetSegment.Flags.None || (segments[i].m_flags & NetSegment.Flags.Untouchable) == NetSegment.Flags.Untouchable) continue;

                if (m_stopWatch.ElapsedMilliseconds >= 1 && i > m_fixTunnelsCount + 16)
                {
                    m_fixTunnelsCount = i;
                    RoadPrefab.singleMode = singleMode;
                    return;
                }

                NetInfo info = segments[i].Info;

                ushort startNode = segments[i].m_startNode;
                ushort endNode = segments[i].m_endNode;

                RoadPrefab prefab = RoadPrefab.GetPrefab(info);
                if (prefab == null) continue;

                // Is it a tunnel?
                if (info == prefab.roadAI.tunnel)
                {
                    // Make sure tunnels have underground flag
                    if ((nodes[startNode].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                        nodes[startNode].m_flags = nodes[startNode].m_flags | NetNode.Flags.Underground;

                    if ((nodes[endNode].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                        nodes[endNode].m_flags = nodes[endNode].m_flags | NetNode.Flags.Underground;

                    // Convert tunnel entrance?
                    if (IsEndTunnel(ref nodes[startNode]))
                    {
                        // Oops wrong way! Invert the segment
                        segments[i].m_startNode = endNode;
                        segments[i].m_endNode = startNode;

                        Vector3 dir = segments[i].m_startDirection;

                        segments[i].m_startDirection = segments[i].m_endDirection;
                        segments[i].m_endDirection = dir;

                        segments[i].m_flags = segments[i].m_flags ^ NetSegment.Flags.Invert;

                        segments[i].CalculateSegment(i);

                        // Make it a slope
                        segments[i].Info = prefab.roadAI.slope;
                        NetManager.instance.UpdateSegment(i);

                        if ((nodes[startNode].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                            nodes[startNode].m_flags = nodes[startNode].m_flags & ~NetNode.Flags.Underground;
                    }
                    else if (IsEndTunnel(ref nodes[endNode]))
                    {
                        // Make it a slope
                        segments[i].Info = prefab.roadAI.slope;
                        NetManager.instance.UpdateSegment(i);

                        if ((nodes[endNode].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                            nodes[endNode].m_flags = nodes[endNode].m_flags & ~NetNode.Flags.Underground;
                    }
                }
                // Is it a slope?
                else if (info == prefab.roadAI.slope)
                {
                    // Convert to tunnel?
                    if (!IsEndTunnel(ref nodes[startNode]) && !IsEndTunnel(ref nodes[endNode]))
                    {
                        if ((nodes[startNode].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                            nodes[startNode].m_flags = nodes[startNode].m_flags | NetNode.Flags.Underground;
                        if ((nodes[endNode].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                            nodes[endNode].m_flags = nodes[endNode].m_flags | NetNode.Flags.Underground;

                        // Make it a tunnel
                        segments[i].Info = prefab.roadAI.tunnel;
                        segments[i].UpdateBounds(i);

                        // Updating terrain
                        TerrainModify.UpdateArea(segments[i].m_bounds.min.x, segments[i].m_bounds.min.z, segments[i].m_bounds.max.x, segments[i].m_bounds.max.z, true, true, false);

                        NetManager.instance.UpdateSegment(i);
                    }
                }
            }

            RoadPrefab.singleMode = singleMode;
            m_fixTunnelsCount = 0;
        }

        private bool FixControlPoint(int point)
        {
            if (m_controlPoints == null) return false;

            NetInfo info = m_current;

            // Pulling from a node?
            if (m_controlPoints[point].m_node != 0)
            {
                info = NetManager.instance.m_nodes.m_buffer[m_controlPoints[point].m_node].Info;
                if (info == null) info = m_current;
            }
            // Pulling from a segment?
            else if (m_controlPoints[point].m_segment != 0)
            {
                info = NetManager.instance.m_segments.m_buffer[m_controlPoints[point].m_segment].Info;
                if (info == null) info = m_current;
            }
            else return false;

            float pointElevation = m_controlPoints[point].m_position.y - NetSegment.SampleTerrainHeight(info, m_controlPoints[point].m_position, false, 0f);
            float diff = pointElevation - m_controlPoints[point].m_elevation;

            // Are we off?
            if (diff <= -1f || diff >= 1f)
            {
                m_controlPoints[point].m_elevation = pointElevation;
                m_cachedControlPoints[point].m_elevation = pointElevation;
            }

            return true;
        }

        private static bool IsEndTunnel(ref NetNode node)
        {
            if ((node.m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.Untouchable && (node.m_flags & NetNode.Flags.Underground) == NetNode.Flags.Underground)
                return false;

            int count = 0;

            for (int i = 0; i < 8; i++)
            {
                int segment = node.GetSegment(i);
                if (segment == 0 || (NetManager.instance.m_segments.m_buffer[segment].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.Created) continue;

                NetInfo info = NetManager.instance.m_segments.m_buffer[segment].Info;

                RoadPrefab prefab = RoadPrefab.GetPrefab(info);
                if (prefab == null) return true;

                if (info != prefab.roadAI.tunnel && info != prefab.roadAI.slope) return true;

                count++;
            }

            if (TerrainManager.instance.SampleRawHeightSmooth(node.m_position) > node.m_position.y + 8f)
                return false;

            return count == 1;
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

        private void AttachToolOptionsButton(RoadPrefab prefab)
        {
            m_buttonExists = false;

            RoadsOptionPanel[] panels = GameObject.FindObjectsOfType<RoadsOptionPanel>();

            foreach (RoadsOptionPanel panel in panels)
            {
                // Find the visible RoadsOptionPanel
                if (panel.component.isVisible)
                {
                    // Put the main button in ElevationStep
                    UIMultiStateButton button = panel.component.Find<UIMultiStateButton>("ElevationStep");
                    if (button != null)
                    {
                        m_toolOptionButton.transform.SetParent(button.transform);
                        button.tooltip = null;
                        m_buttonExists = true;
                    }

                    // Add Upgrade button if needed
                    List<NetTool.Mode> list = new List<NetTool.Mode>(panel.m_Modes);
                    if (m_upgradeButtonTemplate != null && prefab != null && prefab.hasVariation && !list.Contains(NetTool.Mode.Upgrade))
                    {
                        UITabstrip toolMode = panel.component.Find<UITabstrip>("ToolMode");
                        if (toolMode != null)
                        {
                            list.Add(NetTool.Mode.Upgrade);
                            panel.m_Modes = list.ToArray();

                            toolMode.AddTab("Upgrade", m_upgradeButtonTemplate, false);

                            DebugUtils.Log("Upgrade button added.");
                        }
                    }

                    return;
                }

                // No visible RoadsOptionPanel found. Put the main button in OptionsBar instead
                UIPanel optionBar = UIView.Find<UIPanel>("OptionsBar");

                if (optionBar == null)
                {
                    DebugUtils.Log("OptionBar not found!");
                    return;
                }
                m_toolOptionButton.transform.SetParent(optionBar.transform);
            }
        }
    }
}
