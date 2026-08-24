using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class MapPIPManager : MonoBehaviour
{
    public static MapPIPManager Instance { get; private set; }

    [Header("카메라 참조")]
    public Camera mapCamera;

    [Header("지형 가로폭 자동 피팅")]
    [Tooltip("Ground 전체 지형 가로폭에 맞춰 카메라 위치 및 시야각을 자동 최적화할지 여부")]
    public bool autoFitGroundBounds = true;

    [Header("PIP 화면 영역 (상단 UI 침범 방지)")]
    [Range(0f, 1f)] public float pipX = 0.03f;
    [Range(0.1f, 1f)] public float pipWidth = 0.94f;
    [Range(0.1f, 0.4f)] public float pipHeight = 0.23f;
    public float topMarginPixels = 60f; // 상단 재화/도감 UI 바(54px) 아래로 배치

    private void Awake()
    {
        Instance = this;
        FindMapCamera();
    }

    private void OnEnable()
    {
        Instance = this;
        FindMapCamera();
    }

    public void FindMapCamera()
    {
        if (mapCamera == null)
        {
            // 1. MAP_Camera 이름 우선 탐색
            var mapGo = GameObject.Find("MAP_Camera") ?? GameObject.Find("Map_Camera") ?? GameObject.Find("MapCamera");
            if (mapGo != null)
            {
                mapCamera = mapGo.GetComponent<Camera>();
            }

            // 2. Sample_Camera 탐색
            if (mapCamera == null)
            {
                var sampleGo = GameObject.Find("Sample_Camera") ?? GameObject.Find("sample_camera") ?? GameObject.Find("SampleCamera");
                if (sampleGo != null)
                {
                    mapCamera = sampleGo.GetComponent<Camera>();
                }
            }

            // 3. 씬 내 모든 카메라 중 탐색
            if (mapCamera == null)
            {
                var allCams = Resources.FindObjectsOfTypeAll<Camera>();
                foreach (var c in allCams)
                {
                    if (c.gameObject.scene.isLoaded)
                    {
                        if (c.name.IndexOf("map", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            c.name.IndexOf("sample", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            mapCamera = c;
                            break;
                        }
                    }
                }
            }
        }

        // 보조 맵 카메라의 중복 AudioListener 컴포넌트 자동 정리
        if (mapCamera != null)
        {
            var al = mapCamera.GetComponent<AudioListener>();
            if (al != null)
            {
                if (Application.isPlaying) Destroy(al);
                else DestroyImmediate(al);
            }
        }
    }

    public void FitCameraToGroundBounds()
    {
        if (mapCamera == null) return;

        Renderer groundRenderer = null;

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            // 1. BG_01 하위의 Ground 메쉬 렌더러 최우선 직접 탐색
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root.name.StartsWith("BG_01", System.StringComparison.OrdinalIgnoreCase))
                {
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r.name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
                        {
                            groundRenderer = r;
                            break;
                        }
                    }
                }
                if (groundRenderer != null) break;
            }

            // 2. 만약 BG_01 루트가 아니면 씬 내 단독 Ground 렌더러 탐색
            if (groundRenderer == null)
            {
                foreach (var root in activeScene.GetRootGameObjects())
                {
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r.name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
                        {
                            groundRenderer = r;
                            break;
                        }
                    }
                    if (groundRenderer != null) break;
                }
            }
        }

        // 오직 Ground 메쉬의 단독 Bounds만을 기준으로 계산 (주변 수면 및 원거리 배경 제외)
        Bounds groundBounds = (groundRenderer != null) 
            ? groundRenderer.bounds 
            : new Bounds(new Vector3(-1.4f, 16.7f, 767.8f), new Vector3(698f, 53f, 1534f));

        Vector3 center = groundBounds.center;
        float spanZ = groundBounds.size.z; // 강변 좌우 폭 (가로)
        float spanX = groundBounds.size.x; // 강물 전방 길이 (세로)

        // PIP 창의 화면 픽셀 비율 (가로/세로)
        float sw = Mathf.Max(320f, (float)Screen.width);
        float sh = Mathf.Max(480f, (float)Screen.height);
        float pipPixelW = sw * pipWidth;
        float pipPixelH = sh * pipHeight;
        float pipAspect = Mathf.Max(0.5f, pipPixelW / Mathf.Max(1f, pipPixelH));

        // 직교(Orthographic) 정탑다운 카메라로 오직 Ground 메쉬만 100% 꽉 채움
        mapCamera.orthographic = true;
        float optimalOrtho = Mathf.Max(spanX * 0.5f, spanZ / (2.0f * pipAspect)) * 1.01f;
        mapCamera.orthographicSize = optimalOrtho;

        // 수직 정탑다운 (Euler: 90, 90, 0) 시점으로 Ground 메쉬 정중앙에 정확히 조망
        mapCamera.transform.position = new Vector3(center.x, center.y + 400f, center.z);
        mapCamera.transform.rotation = Quaternion.Euler(90f, 90f, 0f);

        mapCamera.nearClipPlane = 0.5f;
        mapCamera.farClipPlane = 2000f;
    }

    public void UpdatePIPState(bool isPositioningPhase)
    {
        FindMapCamera();

        if (mapCamera != null)
        {
            if (isPositioningPhase)
            {
                mapCamera.targetDisplay = 0; // 메인 게임 뷰(Display 1)로 강제 지정
                mapCamera.depth = 15;        // 메인 카메라(Depth 0)보다 높은 15로 설정
                mapCamera.clearFlags = CameraClearFlags.Skybox;
                mapCamera.enabled = true;

                if (autoFitGroundBounds)
                {
                    FitCameraToGroundBounds();
                }

                float sh = Mathf.Max(1f, (float)Screen.height);
                float topMarginNormalized = topMarginPixels / sh;
                float viewportY = Mathf.Clamp01(1f - topMarginNormalized - pipHeight);

                mapCamera.rect = new Rect(pipX, viewportY, pipWidth, pipHeight);
            }
            else
            {
                mapCamera.enabled = false;
            }
        }
    }

    public Rect GetScreenPixelRect()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        return new Rect(sw * pipX, topMarginPixels, sw * pipWidth, sh * pipHeight);
    }
}
