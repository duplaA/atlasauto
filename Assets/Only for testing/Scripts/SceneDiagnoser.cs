using UnityEngine;

public class SceneDiagnoser : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== SCENE DIAGNOSIS START ===");
        
        // 1. Check ALL Cameras (mainly for blue screen of death)
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"Found {cameras.Length} cameras.");

        foreach (var cam in cameras)
        {
            string status = cam.gameObject.activeInHierarchy ? "ACTIVE" : "INACTIVE";
            string target = cam.targetTexture != null ? $"Output: {cam.targetTexture.name} ({cam.targetTexture.width}x{cam.targetTexture.height})" : "Output: SCREEN";
            string clear = cam.clearFlags.ToString();
            string color = cam.backgroundColor.ToString();
            
            Debug.Log($"[CAMERA] '{cam.name}' | {status} | Depth: {cam.depth} | Clear: {clear} | {target} | Color: {color}");

            // If Main Camera is rendering to a texture, clear it.
            if (cam.CompareTag("MainCamera") && cam.targetTexture != null)
            {
                Debug.LogError($"[FIX] Main Camera '{cam.name}' was rendering to a texture! Resetting to Screen.");
                cam.targetTexture = null;
            }
            
            // If Main Camera is SolidColor (Blue), force Skybox
            if (cam.CompareTag("MainCamera") && cam.clearFlags == CameraClearFlags.SolidColor)
            {
                Debug.LogWarning($"[FIX] Main Camera '{cam.name}' was Clearing to SolidColor. Forcing Skybox.");
                cam.clearFlags = CameraClearFlags.Skybox;
            }
        }
        
        Debug.Log("=== SCENE DIAGNOSIS END ===");
    }
}
