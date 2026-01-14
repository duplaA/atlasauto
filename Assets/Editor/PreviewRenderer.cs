using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using Unity.VisualScripting;
using TMPro;

namespace AtlasAuto
{
    internal class WireframeParentReference : MonoBehaviour
    {
        public GameObject parent;
    }

    public class PreviewRenderer
    {
        const float sensitivity = 0.3f;
        RenderTexture rt;
        Scene previewScene;

        GameObject rendering;
        Bounds renderingBounds;

        UnityEngine.Camera camera;

        GameObject currentHovered;
        GameObject currentHoveredWireframe;


        float yaw;
        float pitch;
        float distance;

        struct Materials
        {
            public Material objectOutline;
            public Material wheelOutline;
            public Material hoverOutline;
        }

        Materials materials;

        List<GameObject> cosmetics = new List<GameObject>();

        Dictionary<string, GameObject> carParts;

        float maxDistance;

        static readonly RaycastHit[] raycastHits = new RaycastHit[16];
        readonly List<GameObject> hoverables = new();
        readonly Dictionary<GameObject, Material> baseMatByWireframe = new();
        MeshRenderer currentHoveredMR;
        Material currentHoveredBaseMat;

        bool RaycastWireframes(Ray ray, out RaycastHit closestHit)
        {
            var physics = previewScene.GetPhysicsScene();
            int hitCount = physics.Raycast(ray.origin, ray.direction, raycastHits);

            float bestDist = float.MaxValue;
            closestHit = default;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = raycastHits[i];
                if (!hit.collider) continue;

                if (!hit.collider.TryGetComponent<WireframeParentReference>(out _))
                    continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    closestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        public void InitRenderer(int size = 512)
        {
            previewScene = EditorSceneManager.NewPreviewScene();

            camera = new GameObject("RenderCam", typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();
            camera.fieldOfView = 75f;
            camera.backgroundColor = Color.blue;
            camera.transform.position = new Vector3(0, 1, -3);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.scene = previewScene;

            rt = new RenderTexture(size, size, 26, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;

            materials.objectOutline = makeMaterial(Color.black);
            materials.wheelOutline = makeMaterial(Color.green);
            materials.hoverOutline = makeMaterial(Color.red);

            SceneManager.MoveGameObjectToScene(camera.gameObject, previewScene);
        }

        Material makeMaterial(Color col)
        {
            var mat = new Material(Shader.Find("Hidden/Internal-Colored"));
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.SetColor("_Color", col);
            return mat;
        }

        public void AddRenderObject(GameObject obj, Dictionary<string, GameObject> carParts)
        {
            this.carParts = carParts;
            if (rendering != null)
            {
                Object.DestroyImmediate(rendering);
            }

            GameObject clone = Object.Instantiate(obj);
            clone.GetComponentsInChildren<Collider>().ToList().ForEach(Collider.DestroyImmediate);
            rendering = clone;
            SceneManager.MoveGameObjectToScene(clone, previewScene);

            Bounds bounds = CalculateBounds(clone);
            float radius = bounds.extents.magnitude;

            distance = radius / Mathf.Sin(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            maxDistance = distance * 1.2f;
            yaw = 0;
            pitch = 0;

            Vector3 position = bounds.center - Vector3.back * (distance / 1.2f);

            camera.transform.position = position;
            camera.transform.LookAt(bounds.center);
            renderingBounds = bounds;
        }

        public void ApplyCosmetics(GameObject exclude = null)
        {
            ClearHover();
            RemoveCosmetics(exclude);
            ApplyWireframeTo(rendering, materials.objectOutline);

            foreach (var key in carParts.Keys)
            {
                var obj = carParts[key];
                if (obj == exclude) continue;
                if (key.Contains("wheel"))
                {
                    ApplyWireframeTo(obj, materials.wheelOutline, true);
                }
            }
        }

        public void RemoveCosmetics(GameObject exclude = null)
        {
            ClearHoverImmediate();

            hoverables.Clear();
            baseMatByWireframe.Clear();

            cosmetics.ForEach(o => { if (o != exclude) Object.DestroyImmediate(o); });
            cosmetics.Clear();
        }


        public void PositionCameraBy(Vector2 pos)
        {
            if (rendering == null) return;


            yaw += pos.x * sensitivity;
            pitch -= pos.y * sensitivity;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            UpdateCameraFromAngles();
        }

        public void Zoom(float level)
        {
            distance += level * sensitivity;
            distance = Mathf.Clamp(distance, 2, maxDistance);
            UpdateCameraFromAngles();
        }

        void UpdateCameraFromAngles()
        {
            Vector3 target = renderingBounds.center;

            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            float cosPitch = Mathf.Cos(pitchRad);

            Vector3 dir = new Vector3(
                cosPitch * Mathf.Sin(yawRad),
                Mathf.Sin(pitchRad),
                cosPitch * Mathf.Cos(yawRad)
            );

            camera.transform.position = target - dir * distance;
            camera.transform.LookAt(target);
        }


        public static Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            return b;
        }

        public void Clean()
        {
            if (camera != null)
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
            }

            if (rt != null)
            {
                rt.Release();
                Object.DestroyImmediate(rt);
            }

            EditorSceneManager.CloseScene(previewScene, true);
        }

        public RenderTexture RenderFrame()
        {
            camera.Render();
            return rt;
        }

        public void Click(VehicleGui g)
        {
            Debug.Log(currentHovered.name);
            g.GoToWheel(currentHovered.name);
        }

        public void CheckForHover(Vector2 pos)
        {
            if (camera == null || rendering == null) { ClearHoverImmediate(); return; }

            var ray = camera.ViewportPointToRay(pos);
            var physics = previewScene.GetPhysicsScene();

            int hitCount = physics.Raycast(ray.origin, ray.direction, raycastHits);

            GameObject best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var h = raycastHits[i];
                var col = h.collider;
                if (!col) continue;

                var go = col.gameObject;

                if (!go) continue;

                if (!baseMatByWireframe.ContainsKey(go)) continue;

                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = go;
                }
            }

            if (best == null)
            {
                ClearHoverImmediate();
                return;
            }

            if (best == currentHoveredWireframe)
                return;

            ClearHoverImmediate();

            currentHoveredWireframe = best;
            if (!currentHoveredWireframe) { currentHoveredWireframe = null; return; }

            currentHoveredMR = currentHoveredWireframe.GetComponent<MeshRenderer>();
            if (!currentHoveredMR) { currentHoveredWireframe = null; return; }

            currentHoveredBaseMat = baseMatByWireframe[currentHoveredWireframe];
            currentHoveredMR.sharedMaterial = materials.hoverOutline;
            currentHovered = best;
        }

        void ClearHoverImmediate()
        {
            if (currentHoveredWireframe && currentHoveredMR)
            {
                if (baseMatByWireframe.TryGetValue(currentHoveredWireframe, out var baseMat))
                    currentHoveredMR.sharedMaterial = baseMat;
            }

            currentHoveredWireframe = null;
            currentHoveredMR = null;
            currentHoveredBaseMat = null;
        }


        void ClearHover()
        {
            if (currentHoveredWireframe != null)
            {
                if (currentHoveredWireframe)
                {
                    var mr = currentHoveredWireframe.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sharedMaterial =
                            currentHovered != null && currentHovered.name.Contains("wheel")
                                ? materials.wheelOutline
                                : materials.objectOutline;
                    }
                }
            }

            currentHoveredWireframe = null;
            currentHovered = null;
        }

        Mesh CreateBoundsLineMesh(Bounds bounds)
        {
            Mesh m = new Mesh();
            m.name = "Wireframe";

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            Vector3[] v = new Vector3[8]
            {
                c + new Vector3(-e.x, -e.y, -e.z), // 0
                c + new Vector3( e.x, -e.y, -e.z), // 1
                c + new Vector3( e.x, -e.y,  e.z), // 2
                c + new Vector3(-e.x, -e.y,  e.z), // 3
                c + new Vector3(-e.x,  e.y, -e.z), // 4
                c + new Vector3( e.x,  e.y, -e.z), // 5
                c + new Vector3( e.x,  e.y,  e.z), // 6
                c + new Vector3(-e.x,  e.y,  e.z) // 7
            };

            int[] lines = new int[]
            {
                0,1, 1,2, 2,3, 3,0,
                4,5, 5,6, 6,7, 7,4,
                0,4, 1,5, 2,6, 3,7
            };

            m.SetVertices(v);
            m.SetIndices(lines, MeshTopology.Lines, 0);
            m.RecalculateBounds();
            return m;
        }

        void ApplyWireframeTo(GameObject o, Material mat, bool canHover = false)
        {
            Bounds bounds = CalculateBounds(o);
            Mesh wireframeMesh = CreateBoundsLineMesh(bounds);

            GameObject wireframe = new GameObject(o.name + "_AWF");
            wireframe.AddComponent<MeshFilter>().mesh = wireframeMesh;

            var mr = wireframe.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            wireframe.AddComponent<WireframeParentReference>().parent = o;

            if (canHover)
            {
                BoxCollider collider = wireframe.AddComponent<BoxCollider>();
                collider.size = bounds.size;
                collider.center = bounds.center;

                hoverables.Add(wireframe);
                baseMatByWireframe[wireframe] = mat;
            }

            SceneManager.MoveGameObjectToScene(wireframe, previewScene);
            cosmetics.Add(wireframe);
        }
    }
}
