#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EmergencyRoad.Editor
{
    [CustomPropertyDrawer(typeof(EmergencyRoadPlayerVehicleDefinition))]
    public sealed class EmergencyRoadPlayerVehicleDefinitionDrawer : PropertyDrawer
    {
        private const int FieldCount = 4;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            return EditorGUIUtility.singleLineHeight * (FieldCount + 1) +
                   EditorGUIUtility.standardVerticalSpacing * FieldCount;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty prefab = property.FindPropertyRelative("prefab");
            SerializedProperty displayName = property.FindPropertyRelative("displayName");
            SerializedProperty price = property.FindPropertyRelative("price");
            SerializedProperty hornClip = property.FindPropertyRelative("hornClip");

            string title = !string.IsNullOrWhiteSpace(displayName.stringValue)
                ? displayName.stringValue
                : prefab.objectReferenceValue != null
                    ? prefab.objectReferenceValue.name
                    : label.text;

            Rect line = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, prefab, new GUIContent("Prefab xe"));

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, displayName, new GUIContent("Tên hiển thị"));

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, price, new GUIContent("Giá mở khóa"));

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, hornClip, new GUIContent("Âm còi riêng"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }

    [InitializeOnLoad]
    public static class EmergencyRoadVehicleCatalogMigration
    {
        private const string CatalogPath = "Assets/EmergencyRoad/Resources/EmergencyRoadCatalog.asset";

        static EmergencyRoadVehicleCatalogMigration()
        {
            EditorApplication.delayCall += SynchronizeCatalog;
        }

        [MenuItem("Tools/Emergency Road/Sync Vehicle Catalog")]
        private static void SynchronizeCatalog()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EmergencyRoadCatalog catalog = AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath);
            if (catalog == null)
                return;

            bool changed = catalog.SynchronizePlayerVehicleData();

            // OnEnable can migrate the values before this delayed editor callback.
            // Marking the asset dirty here guarantees that migrated entries are persisted.
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            if (changed)
                Debug.Log("[Emergency Road] Đã đồng bộ danh sách xe: Prefab, tên, giá và âm còi.");
        }
    }
}
#endif
