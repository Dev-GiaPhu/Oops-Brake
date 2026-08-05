using UnityEditor;
using UnityEngine;

namespace EmergencyRoad.Editor
{
    [CustomEditor(typeof(EmergencyRoadGameplaySettingsComponent))]
    public sealed class EmergencyRoadGameplaySettingsComponentEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor settingsEditor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property=serializedObject.FindProperty("settings");
            EditorGUILayout.LabelField("CẤU HÌNH GAMEPLAY TRONG SCENE",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(property,new GUIContent("Gameplay Settings Asset"));
            serializedObject.ApplyModifiedProperties();
            if(property.objectReferenceValue==null){EditorGUILayout.HelpBox("Kéo EmergencyRoadGameplaySettings vào đây. Game sẽ dùng đúng cấu hình được gắn trên object này.",MessageType.Error);return;}
            EditorGUILayout.Space(6);
            CreateCachedEditor(property.objectReferenceValue,null,ref settingsEditor);
            settingsEditor.OnInspectorGUI();
        }

        private void OnDisable(){if(settingsEditor!=null)DestroyImmediate(settingsEditor);}
    }
}
