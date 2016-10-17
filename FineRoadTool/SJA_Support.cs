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

        public static bool anarchyEnabled
        {
            get
            {
                if (modExists)
                {
                    return (bool)m_anarchyEnabled.GetValue(m_instance);
                }
                return false;
            }

            set
            {
                if (!modExists || anarchyEnabled == value) return;

                if (value)
                {
                    DebugUtils.Log("Enabling anarchy");
                    m_enableAnarchy.Invoke(m_instance, null);
                }
                else
                {
                    DebugUtils.Log("Disabling anarchy");
                    m_disableAnarchy.Invoke(m_instance, null);
                }

                m_updateUI.Invoke(m_instance, null);
            }
        }

        public static bool modExists
        {
            get
            {
                return m_instance != null;
            }
        }

        public static void Init()
        {
            try
            {
                Type SJA_Behaviour = Type.GetType("SharpJunctionAngles.SharpJunctionAngleBehaviour, SharpJunctionAngles");

                if (SJA_Behaviour != null)
                {
                    m_instance = GameObject.FindObjectOfType(SJA_Behaviour);

                    if (m_instance != null)
                    {
                        DebugUtils.Log("SharpJunctionAngle found.");

                        m_anarchyEnabled = m_instance.GetType().GetField("anarchyEnabled", BindingFlags.Instance | BindingFlags.NonPublic);

                        m_enableAnarchy = m_instance.GetType().GetMethod("enableAnarchy");
                        m_disableAnarchy = m_instance.GetType().GetMethod("disableAnarchy");
                        m_updateUI = m_instance.GetType().GetMethod("updateUI", BindingFlags.Instance | BindingFlags.NonPublic);
                    }
                }
            }
            catch (Exception e)
            {
                m_instance = null;
                DebugUtils.LogException(e);
            }
        }
    }
}
