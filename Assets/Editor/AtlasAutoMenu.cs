using UnityEditor;
using UnityEngine;

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
    }
}
