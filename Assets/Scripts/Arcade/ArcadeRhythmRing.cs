using System;
using System.Collections;
using UnityEngine;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🎯 리듬 아케이드 모드 전용 착수 리듬 링 인디케이터
    /// - 수면 착수 예정 시점에 맞춰 외부 링이 중심 링으로 수축
    /// - 착수 시 판정 등급별 시각 피드백 연출
    /// </summary>
    public class ArcadeRhythmRing : MonoBehaviour
    {
        [Header("라인 렌더러 참조")]
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private LineRenderer contractingLine;

        [Header("링 비주얼 설정")]
        [SerializeField] private int segments = 48;
        [SerializeField] private float targetRadius = 0.22f;
        [SerializeField] private float startMaxScale = 6.0f;
        [SerializeField] private float lineWidth = 0.035f;

        [Header("색상 팔레트")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.85f, 1.0f, 0.85f);
        [SerializeField] private Color perfectColor = new Color(1.0f, 0.92f, 0.2f, 1.0f);
        [SerializeField] private Color greatColor = new Color(0.2f, 1.0f, 0.4f, 1.0f);
        [SerializeField] private Color goodColor = new Color(0.2f, 0.7f, 1.0f, 1.0f);
        [SerializeField] private Color missColor = new Color(1.0f, 0.3f, 0.3f, 0.9f);

        private Material lineMat;
        private bool isShowing = false;

        private void Awake()
        {
            EnsureLineRenderers();
        }

        private void EnsureLineRenderers()
        {
            if (targetLine == null)
            {
                GameObject tObj = new GameObject("TargetRing");
                tObj.transform.SetParent(transform, false);
                targetLine = tObj.AddComponent<LineRenderer>();
                SetupLineRenderer(targetLine, lineWidth, normalColor);
            }

            if (contractingLine == null)
            {
                GameObject cObj = new GameObject("ContractingRing");
                cObj.transform.SetParent(transform, false);
                contractingLine = cObj.AddComponent<LineRenderer>();
                SetupLineRenderer(contractingLine, lineWidth, normalColor);
            }
        }

        private void SetupLineRenderer(LineRenderer lr, float width, Color color)
        {
            if (lineMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Sprites/Default")
                           ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                lineMat = (s != null) ? new Material(s) : null;
            }

            lr.material = lineMat;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private void DrawCircle(LineRenderer lr, float radius)
        {
            if (lr == null) return;
            float deltaTheta = (2f * Mathf.PI) / segments;
            for (int i = 0; i < segments; i++)
            {
                float theta = i * deltaTheta;
                float x = radius * Mathf.Cos(theta);
                float z = radius * Mathf.Sin(theta);
                lr.SetPosition(i, new Vector3(x, 0.01f, z)); // 수면 z-fighting 방지
            }
        }

        public void Show(Vector3 impactWorldPos)
        {
            transform.position = impactWorldPos;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            isShowing = true;

            EnsureLineRenderers();
            DrawCircle(targetLine, targetRadius);
            SetColors(normalColor);
        }

        public void UpdateProgress(float normalizedTimeRemaining)
        {
            if (!isShowing) return;

            // normalizedTimeRemaining: 1.0(시작 상공) -> 0.0(수면 착수)
            float t = Mathf.Clamp01(normalizedTimeRemaining);
            float currentRadius = Mathf.Lerp(targetRadius, targetRadius * startMaxScale, t);
            DrawCircle(contractingLine, currentRadius);

            float alpha = Mathf.Lerp(1.0f, 0.2f, t);
            Color c = normalColor;
            c.a = alpha;
            if (contractingLine != null)
            {
                contractingLine.startColor = c;
                contractingLine.endColor = c;
            }
        }

        public void PlayHitFeedback(string grade)
        {
            Color hitColor = normalColor;
            if (grade.Contains("PERFECT")) hitColor = perfectColor;
            else if (grade.Contains("GREAT")) hitColor = greatColor;
            else if (grade.Contains("GOOD")) hitColor = goodColor;
            else if (grade.Contains("MISS") || grade.Contains("LATE")) hitColor = missColor;

            SetColors(hitColor);
            StartCoroutine(CoHitPopAndHide());
        }

        private void SetColors(Color col)
        {
            if (targetLine != null) { targetLine.startColor = col; targetLine.endColor = col; }
            if (contractingLine != null) { contractingLine.startColor = col; contractingLine.endColor = col; }
        }

        private IEnumerator CoHitPopAndHide()
        {
            float elapsed = 0f;
            float duration = 0.22f;
            Vector3 startScale = Vector3.one;
            Vector3 targetScale = Vector3.one * 1.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            Hide();
        }

        public void Hide()
        {
            isShowing = false;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (lineMat != null)
            {
                Destroy(lineMat);
                lineMat = null;
            }
        }
    }
}
