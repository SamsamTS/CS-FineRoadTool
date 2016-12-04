using ICities;
using UnityEngine;

using System;

using ColossalFramework;
using ColossalFramework.UI;
/*using System.Linq;
using ColossalFramework.Plugins;
using ColossalFramework.Steamworks;*/

namespace FineRoadTool
{
    public class ModInfo : IUserMod
    {
        public ModInfo()
        {
            try
            {
                // Creating setting file
                GameSettings.AddSettingsFile(new SettingsFile[] { new SettingsFile() { fileName = FineRoadTool.settingsFileName } });
            }
            catch (Exception e)
            {
                DebugUtils.Log("Could load/create the setting file.");
                DebugUtils.LogException(e);
            }
        }

        public string Name
        {
            get { return "Fine Road Tool " + version; }
        }

        public string Description
        {
            get { return "More road tool options"; }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                UIHelper group = helper.AddGroup(Name) as UIHelper;
                UIPanel panel = group.self as UIPanel;

                UICheckBox checkBox = (UICheckBox)group.AddCheckbox("Reduce rail catenary masts", FineRoadTool.reduceCatenary.value, (b) =>
                {
                    FineRoadTool.reduceCatenary.value = b;
                    if (FineRoadTool.instance != null)
                    {
                        FineRoadTool.instance.UpdateCatenary();
                    }
                });
                checkBox.tooltip = "Reduce the number of catenary mast of rail lines from 3 to 1 per segment.\n";

                group.AddSpace(10);

                panel.gameObject.AddComponent<OptionsKeymapping>();

                group.AddSpace(10);

                group.AddButton("Reset tool window position", () =>
                {
                    UIToolOptionsButton.savedWindowX.Delete();
                    UIToolOptionsButton.savedWindowY.Delete();

                    if (UIToolOptionsButton.toolOptionsPanel)
                        UIToolOptionsButton.toolOptionsPanel.absolutePosition = new Vector3(-1000, -1000);
                });

                /*PublishedFileId SJA = new PublishedFileId(553184329);
                if (Steam.active && Steam.workshop.GetSubscribedItems().Contains(SJA))
                {
                    Steam.workshop.Unsubscribe(SJA);

                    PublishedFileId FRA = new PublishedFileId(802066100);
                    Steam.workshop.eventWorkshopItemInstalled += (id) =>
                    {
                        if (id == FRA)
                        {
                            foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
                            {
                                if (plugin.publishedFileID == FRA)
                                {
                                    plugin.isEnabled = true;
                                }
                            }
                        }
                    };
                        
                    Steam.workshop.Subscribe(FRA);
                }*/
            }
            catch (Exception e)
            {
                DebugUtils.Log("OnSettingsUI failed");
                DebugUtils.LogException(e);
            }
        }

        public const string version = "1.2.2";
    }
}
