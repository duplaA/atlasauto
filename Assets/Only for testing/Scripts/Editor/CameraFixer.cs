using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to fix camera rendering issues (mainly for blue screen of death).
/// </summary>
public class CameraFixer : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/AtlasAuto/Fix Cameras")]
    public static void FixCameras()
    {
        int count = 0;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.targetTexture != null)
            {
                Debug.Log($"[CameraFixer] Cleared targetTexture on '{cam.gameObject.name}'");
                cam.targetTexture = null;
                count++;
                EditorUtility.SetDirty(cam); 
            }
        }
        Debug.Log($"[CameraFixer] Fixed {count} cameras.");
    }
#endif
}
