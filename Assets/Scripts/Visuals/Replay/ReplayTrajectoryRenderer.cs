using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Visuals.Replay
{
    /// <summary>
    /// 🎨 리플레이 궤적 라인 렌더링, 3D 돌 아바타 연출, 마커 스폰 전담 모듈
    /// </summary>
    public class ReplayTrajectoryRenderer
    {
        private LineRenderer trajectoryLine;
        private GameObject replayStoneAvatar;
        private readonly List<GameObject> markerObjects = new List<GameObject>();

        public void Initialize(Transform parent, Color pathColor)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Sprites/Default")
                                 ?? Shader.Find("Unlit/Color");

            if (trajectoryLine == null)
            {
                GameObject lineObj = new GameObject("TopDownReplay_TrajectoryLine");
                lineObj.transform.SetParent(parent);
                trajectoryLine = lineObj.AddComponent<LineRenderer>();

                Material lineMat = (unlitShader != null) ? new Material(unlitShader) : new Material(Shader.Find("Standard"));
                lineMat.color = pathColor;

                trajectoryLine.material = lineMat;
                trajectoryLine.useWorldSpace = true;
                trajectoryLine.positionCount = 0;
                trajectoryLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trajectoryLine.receiveShadows = false;
                trajectoryLine.enabled = false;
            }
        }

        public void CreateReplayStoneAvatar(Transform parent, GameObject stonePrefab)
        {
            if (replayStoneAvatar != null)
            {
                if (Application.isPlaying) Object.Destroy(replayStoneAvatar);
                else Object.DestroyImmediate(replayStoneAvatar);
                replayStoneAvatar = null;
            }

            if (stonePrefab != null)
            {
                replayStoneAvatar = Object.Instantiate(stonePrefab, parent);
                replayStoneAvatar.name = "TopDownReplay_StoneAvatar";

                var ss = replayStoneAvatar.GetComponent<SkippingStone>();
                if (ss != null) Object.Destroy(ss);
                var rb = replayStoneAvatar.GetComponent<Rigidbody>();
                if (rb != null) Object.Destroy(rb);
                var tr = replayStoneAvatar.GetComponent<TrailRenderer>();
                if (tr != null) Object.Destroy(tr);

                foreach (var col in replayStoneAvatar.GetComponentsInChildren<Collider>(true))
                {
                    if (Application.isPlaying) Object.Destroy(col);
                    else Object.DestroyImmediate(col);
                }

                foreach (var rend in replayStoneAvatar.GetComponentsInChildren<Renderer>(true))
                {
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                replayStoneAvatar.SetActive(false);
            }
        }

        public void ClearMarkers()
        {
            foreach (var m in markerObjects)
            {
                if (m != null)
                {
                    if (Application.isPlaying) Object.Destroy(m);
                    else Object.DestroyImmediate(m);
                }
            }
            markerObjects.Clear();
        }

        public void UpdateVisualsScale(float orthoSize)
        {
            float dynamicW = Mathf.Clamp(orthoSize * 0.0128f, 0.32f, 6.0f);
            float ringW = Mathf.Clamp(orthoSize * 0.0056f, 0.12f, 2.6f);

            if (trajectoryLine != null)
            {
                trajectoryLine.startWidth = dynamicW;
                trajectoryLine.endWidth = dynamicW;
            }

            float markerScale = Mathf.Clamp(orthoSize / 390f, 0.15f, 2.5f);
            foreach (var m in markerObjects)
            {
                if (m != null)
                {
                    m.transform.localScale = new Vector3(markerScale, 1f, markerScale);
                    LineRenderer lr = m.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        lr.startWidth = ringW;
                        lr.endWidth = ringW;
                    }
                }
            }

            if (replayStoneAvatar != null && replayStoneAvatar.activeSelf)
            {
                float avatarScale = Mathf.Clamp(orthoSize * 0.18f, 2.5f, 25f);
                replayStoneAvatar.transform.localScale = new Vector3(avatarScale, avatarScale, avatarScale);
            }
        }

        public IEnumerator DrawTrajectoryRoutine(
            List<Vector3> trajectoryPathPoints, 
            List<SkippingStone.BounceRecord> markerRecords, 
            float baseReplayLevel, 
            float cachedFinalDist, 
            Transform parent, 
            DualCameraSetup dualCam,
            ReplayCameraController camCtrl,
            System.Action<float> onTerrainSync,
            Color pathColor)
        {
            if (trajectoryPathPoints == null || trajectoryPathPoints.Count < 2) yield break;

            if (trajectoryLine == null)
            {
                Initialize(parent, pathColor);
            }

            trajectoryLine.enabled = true;
            trajectoryLine.positionCount = 0;

            if (replayStoneAvatar != null) replayStoneAvatar.SetActive(true);

            // 1. 시작점 마커 즉시 스폰
            if (markerRecords.Count > 0 && markerRecords[0].grade == "START")
            {
                SpawnBounceMarker(markerRecords[0], 0, parent, baseReplayLevel, camCtrl.CurrentOrthoSize);
            }

            // 2. 60fps 부드러운 드로잉 애니메이션
            float totalDrawDuration = Mathf.Clamp(cachedFinalDist / 40f, 6.0f, 16.0f);
            float elapsed = 0f;

            List<Vector3> drawnPoints = new List<Vector3>(trajectoryPathPoints.Count + 10) { trajectoryPathPoints[0] };
            trajectoryLine.positionCount = 1;
            trajectoryLine.SetPosition(0, trajectoryPathPoints[0]);

            int nextMarkerIdx = 1;

            while (elapsed < totalDrawDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / totalDrawDuration);

                float pointProgress = progress * (trajectoryPathPoints.Count - 1);
                int baseIdx = Mathf.FloorToInt(pointProgress);
                int nextIdx = Mathf.Min(baseIdx + 1, trajectoryPathPoints.Count - 1);
                float segFraction = pointProgress - baseIdx;

                Vector3 currentLeadPos = Vector3.Lerp(trajectoryPathPoints[baseIdx], trajectoryPathPoints[nextIdx], segFraction);

                // 3D 도약 포물선
                float heightFactor = 4f * segFraction * (1f - segFraction);
                float dynamicLeadY = baseReplayLevel + 0.45f + (heightFactor * 4.5f);

                // 60fps 고속 선 갱신
                while (drawnPoints.Count <= baseIdx)
                {
                    int addIdx = drawnPoints.Count;
                    drawnPoints.Add(trajectoryPathPoints[addIdx]);
                    trajectoryLine.positionCount = drawnPoints.Count;
                    trajectoryLine.SetPosition(addIdx, trajectoryPathPoints[addIdx]);
                }

                if (drawnPoints.Count == baseIdx + 1)
                {
                    drawnPoints.Add(currentLeadPos);
                    trajectoryLine.positionCount = drawnPoints.Count;
                    trajectoryLine.SetPosition(drawnPoints.Count - 1, currentLeadPos);
                }
                else
                {
                    drawnPoints[drawnPoints.Count - 1] = currentLeadPos;
                    trajectoryLine.SetPosition(drawnPoints.Count - 1, currentLeadPos);
                }

                // 돌 아바타 위치 및 피치 회전
                if (replayStoneAvatar != null)
                {
                    Vector3 segDir = (trajectoryPathPoints[nextIdx] - trajectoryPathPoints[baseIdx]).normalized;
                    if (segDir.sqrMagnitude < 0.001f) segDir = Vector3.forward;

                    float avatarBaseScale = Mathf.Clamp(camCtrl.CurrentOrthoSize * 0.18f, 2.5f, 25f);
                    float jumpScale = avatarBaseScale * (1f + heightFactor * 0.8f);
                    float vyFactor = (1f - 2f * segFraction);
                    float pitchAngle = vyFactor * 32f;

                    replayStoneAvatar.transform.position = new Vector3(currentLeadPos.x, dynamicLeadY, currentLeadPos.z);
                    replayStoneAvatar.transform.localScale = new Vector3(jumpScale, jumpScale, jumpScale);
                    replayStoneAvatar.transform.rotation = Quaternion.LookRotation(segDir, Vector3.up) * Quaternion.Euler(-pitchAngle, 0f, 0f);
                }

                // 카메라 추종
                if (dualCam != null)
                {
                    camCtrl.CurrentCamCenter = new Vector3(currentLeadPos.x, baseReplayLevel + 80f, currentLeadPos.z + 15f);
                    dualCam.SetReplayTopDownView(camCtrl.CurrentCamCenter, camCtrl.CurrentOrthoSize);
                    onTerrainSync?.Invoke(camCtrl.CurrentCamCenter.z);
                }

                // 바운스 마커 스폰
                while (nextMarkerIdx < markerRecords.Count - 1 && markerRecords[nextMarkerIdx].distance <= currentLeadPos.z)
                {
                    SpawnBounceMarker(markerRecords[nextMarkerIdx], nextMarkerIdx, parent, baseReplayLevel, camCtrl.CurrentOrthoSize);
                    nextMarkerIdx++;
                }

                yield return null;
            }

            // 남은 모든 마커 일괄 스폰
            while (nextMarkerIdx < markerRecords.Count)
            {
                SpawnBounceMarker(markerRecords[nextMarkerIdx], nextMarkerIdx, parent, baseReplayLevel, camCtrl.CurrentOrthoSize);
                nextMarkerIdx++;
            }

            // 최종 라인 완성
            trajectoryLine.positionCount = trajectoryPathPoints.Count;
            for (int p = 0; p < trajectoryPathPoints.Count; p++)
            {
                trajectoryLine.SetPosition(p, trajectoryPathPoints[p]);
            }

            // 종료 시 마지막 돌 위치 안착
            if (dualCam != null && trajectoryPathPoints.Count > 0)
            {
                Vector3 lastPos = trajectoryPathPoints[trajectoryPathPoints.Count - 1];
                camCtrl.CurrentCamCenter = new Vector3(lastPos.x, baseReplayLevel + 80f, lastPos.z);
                dualCam.SetReplayTopDownView(camCtrl.CurrentCamCenter, camCtrl.CurrentOrthoSize);
                onTerrainSync?.Invoke(camCtrl.CurrentCamCenter.z);
            }

            if (replayStoneAvatar != null && trajectoryPathPoints.Count > 0)
            {
                Vector3 lastPos = trajectoryPathPoints[trajectoryPathPoints.Count - 1];
                float avatarBaseScale = Mathf.Clamp(camCtrl.CurrentOrthoSize * 0.18f, 2.5f, 25f);
                replayStoneAvatar.transform.position = new Vector3(lastPos.x, baseReplayLevel + 0.45f, lastPos.z);
                replayStoneAvatar.transform.localScale = new Vector3(avatarBaseScale, avatarBaseScale, avatarBaseScale);
                replayStoneAvatar.transform.rotation = Quaternion.identity;
                replayStoneAvatar.SetActive(true);
            }
        }

        private void SpawnBounceMarker(SkippingStone.BounceRecord record, int index, Transform parent, float baseReplayLevel, float orthoSize)
        {
            if (record.grade.Contains("RING_BOOST"))
            {
                SpawnStripedRandomRingMarker(record, index, parent, baseReplayLevel, orthoSize);
                return;
            }

            GameObject marker = new GameObject($"ReplayMarker_{index}_{record.grade}");
            marker.transform.SetParent(parent);
            Vector3 markerPos = new Vector3(record.position.x, baseReplayLevel + 0.12f, record.position.z);
            marker.transform.position = markerPos;

            LineRenderer lr = marker.AddComponent<LineRenderer>();
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = new Material(unlitShader);

            Color mColor = GetMarkerColorByGrade(record.grade, index);
            float baseRadius = (index == 0) ? 18f : (record.grade == "FINISH") ? 22f : 16f;
            float ringWidth = Mathf.Clamp(orthoSize * 0.007f, 0.15f, 3.2f);

            mat.color = mColor;
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startWidth = ringWidth;
            lr.endWidth = ringWidth;

            int segments = 48;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * baseRadius, 0f, Mathf.Sin(angle) * baseRadius));
            }

            markerObjects.Add(marker);
        }

        private void SpawnStripedRandomRingMarker(SkippingStone.BounceRecord record, int index, Transform parent, float baseReplayLevel, float orthoSize)
        {
            GameObject markerRoot = new GameObject($"ReplayMarker_{index}_RandomRing_Striped");
            markerRoot.transform.SetParent(parent);
            markerRoot.transform.position = new Vector3(record.position.x, baseReplayLevel + 0.12f, record.position.z);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material redMat = new Material(unlitShader) { color = new Color(1f, 0.15f, 0.15f, 1f) };
            Material whiteMat = new Material(unlitShader) { color = Color.white };

            float outerRadius = 24f;
            float ringWidth = Mathf.Clamp(orthoSize * 0.009f, 0.25f, 4.0f);
            int totalSegments = 12;
            int arcResolution = 5;

            for (int seg = 0; seg < totalSegments; seg++)
            {
                GameObject arcObj = new GameObject($"RingArc_{seg}");
                arcObj.transform.SetParent(markerRoot.transform, false);

                LineRenderer lr = arcObj.AddComponent<LineRenderer>();
                lr.material = (seg % 2 == 0) ? redMat : whiteMat;
                lr.useWorldSpace = false;
                lr.loop = false;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startWidth = ringWidth;
                lr.endWidth = ringWidth;
                lr.positionCount = arcResolution;

                float startAngle = seg * (Mathf.PI * 2f / totalSegments);
                float endAngle = (seg + 1) * (Mathf.PI * 2f / totalSegments);

                for (int p = 0; p < arcResolution; p++)
                {
                    float t = (float)p / (arcResolution - 1);
                    float curAngle = Mathf.Lerp(startAngle, endAngle, t);
                    lr.SetPosition(p, new Vector3(Mathf.Cos(curAngle) * outerRadius, 0f, Mathf.Sin(curAngle) * outerRadius));
                }
            }

            markerObjects.Add(markerRoot);
        }

        private Color GetMarkerColorByGrade(string grade, int index)
        {
            if (index == 0 || grade == "START") return new Color(0.2f, 1f, 0.4f, 1f);
            if (grade == "FINISH") return new Color(1f, 0.22f, 0.22f, 1f);
            if (grade.Contains("PERFECT")) return Color.green;
            if (grade.Contains("GREAT")) return Color.cyan;
            if (grade.Contains("GOOD")) return Color.yellow;
            return new Color(1.0f, 0.55f, 0.15f, 1.0f);
        }
    }
}
