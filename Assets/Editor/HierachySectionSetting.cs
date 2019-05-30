using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HierachySection.Editor
{
    class HierachySectionSetting : ScriptableObject
    {
        public const string ASSET_PARENT_FOLDER = "Assets";
        public const string ASSET_SUB_FOLDER = "Editor";
        public const string ASSET_NAME = "HierachySectionSetting.asset";

        public static readonly string SETTING_ASSET_FOLDER = ASSET_PARENT_FOLDER + '/' + ASSET_SUB_FOLDER;
        public static readonly string SETTING_ASSET_PATH = SETTING_ASSET_FOLDER + '/' + ASSET_NAME;

        public static readonly Color DEFAULT_FOREGROUND_COLOR = Color.white;
        public static readonly Color DEFAULT_BACKGROUND_COLOR = Color.black;

        public static readonly Color DEFAULT_HIGHTLIGHT_FOREGROUND_COLOR = Color.white;
        public static readonly Color DEFAULT_HIGHTLIGHT_BACKGROUND_COLOR = Color.yellow;


        [SerializeField]
        bool autoSetEditorOnlyTag;

        [SerializeField]
        Color foregroundColor;

        [SerializeField]
        Color backgroundColor;

        [SerializeField]
        Color hilightForegroundColor;

        [SerializeField]
        Color hilightBackgroundColor;

        public bool AutoSetEditorOnlyTag => autoSetEditorOnlyTag;
        public Color ForegroundColor => foregroundColor;
        public Color BackgroundColor => backgroundColor;
        public Color HilightForegroundColor => hilightForegroundColor;
        public Color HilightBackgroundColor => hilightBackgroundColor;


        public static HierachySectionSetting GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HierachySectionSetting>(SETTING_ASSET_PATH);

            if (settings == null)
            {
                settings = CreateDefaultSettings();
            }

            return settings;
        }

        internal static void RemoveExistSetting()
        {
            AssetDatabase.DeleteAsset(SETTING_ASSET_PATH);
        }

        internal static HierachySectionSetting CreateDefaultSettings()
        {
            var settings = ScriptableObject.CreateInstance<HierachySectionSetting>();

            settings.autoSetEditorOnlyTag = true;
            settings.foregroundColor = DEFAULT_FOREGROUND_COLOR;
            settings.backgroundColor = DEFAULT_BACKGROUND_COLOR;
            settings.hilightForegroundColor = DEFAULT_HIGHTLIGHT_FOREGROUND_COLOR;
            settings.hilightBackgroundColor = DEFAULT_HIGHTLIGHT_BACKGROUND_COLOR;

            if (!AssetDatabase.IsValidFolder(SETTING_ASSET_FOLDER))
            {
                AssetDatabase.CreateFolder(ASSET_PARENT_FOLDER, ASSET_SUB_FOLDER);
            }

            AssetDatabase.CreateAsset(settings, SETTING_ASSET_PATH);
            AssetDatabase.SaveAssets();

            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    static class HierachySectionSettingGUI
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new SettingsProvider("Project/HierachySection", SettingsScope.Project)
            {
                label = "HierachySection",
                guiHandler = (searchContext) =>
                {
                    var settings = HierachySectionSetting.GetSerializedSettings();

                    if (settings == null)
                    {
                        return;
                    }

                    EditorGUILayout.PropertyField(settings.FindProperty("autoSetEditorOnlyTag"), new GUIContent("Auto set \" EditorOnly \" tag"));
                    EditorGUILayout.PropertyField(settings.FindProperty("foregroundColor"), new GUIContent("Foreground"));
                    EditorGUILayout.PropertyField(settings.FindProperty("backgroundColor"), new GUIContent("Background"));
                    EditorGUILayout.PropertyField(settings.FindProperty("hilightForegroundColor"), new GUIContent("Hilight Foreground"));
                    EditorGUILayout.PropertyField(settings.FindProperty("hilightBackgroundColor"), new GUIContent("Hilight Background"));

                    settings.ApplyModifiedProperties();

                    EditorGUILayout.Space();
                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("Use Default", GUILayout.MaxWidth(100)))
                    {
                        HierachySectionSetting.RemoveExistSetting();
                    }

                    if (GUILayout.Button("Refresh", GUILayout.MaxWidth(100)))
                    {
                        HierachySection.RefreshSectionPrompt();
                    }

                    EditorGUILayout.EndHorizontal();
                },
                keywords = new HashSet<string>(new[] { "ForegroundColor", "BackgroundColor", "HilightColor" })
            };

            return provider;
        }
    }
}
