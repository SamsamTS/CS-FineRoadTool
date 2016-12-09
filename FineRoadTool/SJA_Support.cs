using UnityEngine;

using System;
using System.Reflection;

namespace FineRoadTool
{
    internal class SJA_Support
    {
        private static FieldInfo m_anarchyEnabled;
        private static object m_instance;

        private static MethodInfo m_enableAnarchy;
        private static MethodInfo m_disableAnarchy;
        private static MethodInfo m_updateUI;

        private static Type m_SJA_Behaviour = Type.GetType("SharpJunctionAngles.SharpJunctionAngleBehaviour, SharpJunctionAngles");

        private static int m_tries;

        public static bool anarchyEnabled
        {
            get
            {
                try
                {
                    if (modExists && InstanceFound())
                    {
                        return (bool)m_anarchyEnabled.GetValue(m_instance);
                    }
                }
                catch { }

                return false;
            }

            set
            {
                try
                {
                    if (!modExists || !InstanceFound() || anarchyEnabled == value) return;

                    if (value)
                    {
                        m_enableAnarchy.Invoke(m_instance, null);
                    }
                    else
                    {
                        m_disableAnarchy.Invoke(m_instance, null);
                    }

                    m_updateUI.Invoke(m_instance, null);
                }
                catch { }
            }
        }

        public static bool modExists
        {
            get
            {
                return m_SJA_Behaviour != null;
            }
        }

        public static void Init()
        {
            try
            {
                m_tries = 0;
                m_instance = null;
                m_SJA_Behaviour = Type.GetType("SharpJunctionAngles.SharpJunctionAngleBehaviour, SharpJunctionAngles");

                if (m_SJA_Behaviour != null)
                {
                    m_anarchyEnabled = m_SJA_Behaviour.GetField("anarchyEnabled", BindingFlags.Instance | BindingFlags.NonPublic);

                    m_enableAnarchy = m_SJA_Behaviour.GetMethod("enableAnarchy");
                    m_disableAnarchy = m_SJA_Behaviour.GetMethod("disableAnarchy");
                    m_updateUI = m_SJA_Behaviour.GetMethod("updateUI", BindingFlags.Instance | BindingFlags.NonPublic);
                }
            }
            catch (Exception e)
            {
                m_instance = null;
                DebugUtils.LogException(e);
            }
        }

        private static bool InstanceFound()
        {
            try
            {
                if (m_instance == null && m_tries++ < 10)
                {
                    m_instance = GameObject.Find("SharpJunctionAngles").GetComponent(m_SJA_Behaviour);

                    if (m_tries >= 10)
                    {
                        m_SJA_Behaviour = null;
                    }
                }

                return m_instance != null;
            }
            catch
            {
                m_SJA_Behaviour = null;
                m_instance = null;
                return false;
            }
        }
    }
}
