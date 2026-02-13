using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtlasAuto
{
    public class AtlasAutoMenu : EditorWindow
    {
        [MenuItem("Tools/AtlasAuto/Documentation")]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/duplaA/atlasauto");
        }

        [MenuItem("Tools/AtlasAuto/About")]
        public static void OpenAbout()
        {
            EditorUtility.DisplayDialog("About AtlasAuto", 
                "AtlasAuto Vehicle System\n\nAdvanced vehicle physics and tools for Unity.\n\nNow usable.", 
                "OK");
        }

        [MenuItem("Tools/AtlasAuto/Add DriverHUD to Scene")]
        public static void AddDriverHUD()
        {
            // Check if already exists
            if (Object.FindAnyObjectByType<DriverHUD>())
            {
                EditorUtility.DisplayDialog("DriverHUD Exists", "A DriverHUD component already exists in the scene.", "OK");
                return;
            }

            GameObject go = new GameObject("DriverHUD");
            Undo.RegisterCreatedObjectUndo(go, "Create DriverHUD");

            var uiDoc = go.AddComponent<UIDocument>();
            var hud = go.AddComponent<DriverHUD>();

            // Load assets
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Only for testing/Scripts/UI/DriverHUD.uxml");
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset");

            if (visualTree != null) uiDoc.visualTreeAsset = visualTree;
            else Debug.LogError("Could not find DriverHUD.uxml at 'Assets/Only for testing/Scripts/UI/DriverHUD.uxml'");

            if (panelSettings != null) uiDoc.panelSettings = panelSettings;
            else Debug.LogError("Could not find PanelSettings.asset at 'Assets/UI Toolkit/PanelSettings.asset'");

            Selection.activeGameObject = go;
        }
    }
}
