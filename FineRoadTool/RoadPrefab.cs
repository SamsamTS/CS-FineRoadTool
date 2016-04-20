using System.Collections.Generic;

namespace FineRoadTool
{

    public enum Mode
    {
        Normal,
        Ground,
        Elevated,
        Bridge,
        Tunnel,
        Single
    }

    public class RoadPrefab
    {
        private NetInfo m_prefab;
        private NetInfo m_elevated;
        private NetInfo m_bridge;
        private NetInfo m_slope;
        private NetInfo m_tunnel;
        private float m_maxSlope;
        private bool m_flattenTerrain;
        private bool m_followTerrain;

        private RoadAIWrapper m_roadAI;
        private Mode m_mode;
        private bool m_hasElevation;

        private static Dictionary<NetInfo, RoadPrefab> m_roadPrefabs;
        private static bool m_singleMode;

        private RoadPrefab(NetInfo prefab)
        {
            m_roadAI = new RoadAIWrapper(prefab.m_netAI);

            m_prefab = prefab;

            m_followTerrain = prefab.m_followTerrain;
            m_flattenTerrain = prefab.m_flattenTerrain;

            m_maxSlope = prefab.m_maxSlope;

            if (m_hasElevation = m_roadAI.hasElevation)
            {
                m_elevated = m_roadAI.elevated;
                m_bridge = m_roadAI.bridge;
                m_slope = m_roadAI.slope;
                m_tunnel = m_roadAI.tunnel;
            }
        }

        public static void Initialize()
        {
            m_roadPrefabs = new Dictionary<NetInfo, RoadPrefab>();

            for (uint i = 0; i < PrefabCollection<NetInfo>.PrefabCount(); i++)
            {
                NetInfo info = PrefabCollection<NetInfo>.GetPrefab(i);

                RoadPrefab prefab = new RoadPrefab(info);
                if (prefab.m_hasElevation && !m_roadPrefabs.ContainsKey(info))
                {
                    m_roadPrefabs.Add(info, prefab);

                    if (prefab.m_roadAI.elevated != null && !m_roadPrefabs.ContainsKey(prefab.m_roadAI.elevated))
                        m_roadPrefabs.Add(prefab.m_roadAI.elevated, prefab);
                    if (prefab.m_roadAI.bridge != null && !m_roadPrefabs.ContainsKey(prefab.m_roadAI.bridge))
                        m_roadPrefabs.Add(prefab.m_roadAI.bridge, prefab);
                    if (prefab.m_roadAI.slope != null && !m_roadPrefabs.ContainsKey(prefab.m_roadAI.slope))
                        m_roadPrefabs.Add(prefab.m_roadAI.slope, prefab);
                    if (prefab.m_roadAI.tunnel != null && !m_roadPrefabs.ContainsKey(prefab.m_roadAI.tunnel))
                        m_roadPrefabs.Add(prefab.m_roadAI.tunnel, prefab);
                }
            }
        }

        public static RoadPrefab GetPrefab(NetInfo info)
        {
            if (info != null && m_roadPrefabs.ContainsKey(info)) return m_roadPrefabs[info];

            return null;
        }

        public static bool singleMode
        {
            get { return m_singleMode; }
            set
            {
                if (value == m_singleMode) return;
                m_singleMode = false;

                foreach (RoadPrefab prefab in m_roadPrefabs.Values)
                {
                    if (value)
                    {
                        prefab.mode = Mode.Single;
                    }
                    else
                    {
                        prefab.Restore();
                    }
                }

                m_singleMode = value;
                if (value) DebugUtils.Log("Intersection support activated");
                else DebugUtils.Log("Intersection support deactivated");
            }
        }

        public void Restore()
        {
            if (m_prefab == null) return;

            if (m_singleMode)
            {
                singleMode = false;
                return;
            }

            m_prefab.m_followTerrain = m_followTerrain;
            m_prefab.m_flattenTerrain = m_flattenTerrain;
            m_prefab.m_maxSlope = m_maxSlope;

            if (m_elevated != null) m_elevated.m_maxSlope = m_maxSlope;
            if (m_bridge != null) m_bridge.m_maxSlope = m_maxSlope;
            if (m_slope != null) m_slope.m_maxSlope = m_maxSlope;
            if (m_tunnel != null) m_tunnel.m_maxSlope = m_maxSlope;

            if (m_hasElevation)
            {
                m_roadAI.info = m_prefab;
                m_roadAI.elevated = m_elevated;
                m_roadAI.bridge = m_bridge;
                m_roadAI.slope = m_slope;
                m_roadAI.tunnel = m_tunnel;
            }
        }

        public Mode mode
        {
            get { return m_mode; }
            set
            {
                if (m_prefab == null || !m_hasElevation) return;

                m_mode = value;
                Update();
            }
        }

        public NetInfo prefab
        {
            get { return m_prefab; }
        }

        public RoadAIWrapper roadAI
        {
            get { return m_roadAI; }
        }

        public bool hasElevation
        {
            get { return m_hasElevation; }
        }

        public bool hasVariation
        {
            get { return m_elevated != null || m_bridge != null || m_slope != null || m_tunnel != null; }
        }

        public float defaultSlope
        {
            get { return m_maxSlope; }
        }

        public void SetMaxSlope(float slope)
        {
            m_prefab.m_maxSlope = slope;

            if (m_elevated != null) m_elevated.m_maxSlope = slope;
            if (m_bridge != null) m_bridge.m_maxSlope = slope;
            if (m_slope != null) m_slope.m_maxSlope = slope;
            if (m_tunnel != null) m_tunnel.m_maxSlope = slope;
        }

        public void Update()
        {
            if (m_prefab == null || !m_hasElevation) return;

            Restore();

            switch (m_mode)
            {
                case Mode.Normal:
                    if (FineRoadTool.instance.straightSlope)
                    {
                        m_prefab.m_followTerrain = false;
                        m_prefab.m_flattenTerrain = true;
                    }
                    break;
                case Mode.Ground:
                    m_roadAI.elevated = m_prefab;
                    m_roadAI.bridge = null;
                    m_roadAI.slope = null;
                    m_roadAI.tunnel = m_prefab;
                    m_prefab.m_followTerrain = false;
                    m_prefab.m_flattenTerrain = true;
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
                case Mode.Tunnel:
                    if (m_tunnel != null && m_slope != null)
                    {
                        m_roadAI.info = m_tunnel;
                        m_roadAI.elevated = m_tunnel;
                        m_roadAI.bridge = null;
                        m_roadAI.slope = m_tunnel;
                    }
                    break;
                case Mode.Single:
                    m_roadAI.elevated = null;
                    m_roadAI.bridge = null;
                    m_roadAI.slope = null;
                    m_roadAI.tunnel = null;
                    m_prefab.m_followTerrain = false;
                    m_prefab.m_flattenTerrain = true;
                    break;
            }
        }
    }
}
