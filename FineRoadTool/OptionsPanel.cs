using ICities;
using UnityEngine;

using System;
using System.Reflection;

using ColossalFramework;
using ColossalFramework.UI;

namespace FineRoadTool
{
    class OptionsPanel : OptionsKeymappingPanel
    {
        public static readonly SavedInputKey elevationUp = new SavedInputKey(Settings.buildElevationUp, Settings.gameSettingsFile, DefaultSettings.buildElevationUp, true);
        public static readonly SavedInputKey elevationDown = new SavedInputKey(Settings.buildElevationDown, Settings.gameSettingsFile, DefaultSettings.buildElevationDown, true);
        public static readonly SavedInputKey elevationReset = new SavedInputKey("elevationReset", FineRoadTool.settingsFileName, SavedInputKey.Encode(KeyCode.Home, false, false, false), true);

        public static readonly SavedInputKey elevationStepUp = new SavedInputKey("elevationStepUp", FineRoadTool.settingsFileName, SavedInputKey.Encode(KeyCode.UpArrow, true, false, false), true);
        public static readonly SavedInputKey elevationStepDown = new SavedInputKey("elevationStepDown", FineRoadTool.settingsFileName, SavedInputKey.Encode(KeyCode.DownArrow, true, false, false), true);

        public static readonly SavedInputKey modesCycleRight = new SavedInputKey("modesCycleRight", FineRoadTool.settingsFileName, SavedInputKey.Encode(KeyCode.RightArrow, true, false, false), true);
        public static readonly SavedInputKey modesCycleLeft = new SavedInputKey("modesCycleLeft", FineRoadTool.settingsFileName, SavedInputKey.Encode(KeyCode.LeftArrow, true, false, false), true);

        private int count = 0;

        private void Awake()
        {
            AddKeymapping("Elevation Up", elevationUp);
            AddKeymapping("Elevation Down", elevationDown);
            AddKeymapping("Reset Elevation", elevationReset);
            AddKeymapping("Elevation Step Up", elevationStepUp);
            AddKeymapping("Elevation Step Down", elevationStepDown);
            AddKeymapping("Cycle Modes Right", modesCycleRight);
            AddKeymapping("Cycle Modes Left", modesCycleLeft);
        }

        private void AddKeymapping(string label, SavedInputKey savedInputKey)
        {
            UIPanel uIPanel = component.AttachUIComponent(UITemplateManager.GetAsGameObject("KeyBindingTemplate")) as UIPanel;
            if (count++ % 2 == 1) uIPanel.backgroundSprite = null;

            UILabel uILabel = uIPanel.Find<UILabel>("Name");
            UIButton uIButton = uIPanel.Find<UIButton>("Binding");
            uIButton.eventKeyDown += (c, e) => { typeof(OptionsKeymappingPanel).GetMethod("OnBindingKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, new object[] { c, e }); };
            uIButton.eventMouseDown += (c, e) => { typeof(OptionsKeymappingPanel).GetMethod("OnBindingMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, new object[] { c, e }); };

            uILabel.text = label;
            uIButton.text = savedInputKey.ToLocalizedString("KEYNAME");
            uIButton.objectUserData = savedInputKey;
        }
    }

}
