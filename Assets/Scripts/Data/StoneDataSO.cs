using UnityEngine;

namespace SkippingStones.Data
{
    /// <summary>
    /// 개별 조약돌의 고유 데이터 및 프리팹/아이콘 직접 참조 ScriptableObject
    /// - 문자열 경로 로딩(Resources.Load) 의존성을 탈피하고 인스펙터 직접 참조 보장
    /// </summary>
    [CreateAssetMenu(fileName = "StoneData_", menuName = "Skipping Stones/Stone Data", order = 10)]
    public class StoneDataSO : ScriptableObject
    {
        [Header("🪨 기본 식별 정보")]
        [Tooltip("고유 식별 ID (예: default, flat_slate, emerald_pebble, crimson_flint)")]
        public string id = "default";

        [Tooltip("인게임/도감 노출 이름")]
        public string displayName = "기본 조약돌";

        [TextArea(2, 4)]
        [Tooltip("도감 및 설명")]
        public string description = "표면이 매끄러워 물수제비에 최적화된 기본 돌";

        [Header("📦 3D 프리팹 및 비주얼 (직접 참조)")]
        [Tooltip("인게임 및 쇼케이스에서 사용되는 3D 돌 프리팹")]
        public GameObject prefab;

        [Tooltip("도감/상점/UI 노출용 아이콘")]
        public Sprite icon;

        [Header("💎 해금 및 경제 정보")]
        [Tooltip("기본 해금 여부")]
        public bool isDefaultUnlocked = false;

        [Tooltip("해금에 필요한 골드 비용 (0이면 무료/기본)")]
        public int unlockGoldCost = 0;

        [Tooltip("해금에 필요한 다이아 비용")]
        public int unlockDiaCost = 0;

        [Header("⚡ 물리/게임플레이 특성 배율")]
        [Tooltip("바운스 반사력 배율 (기본 1.0)")]
        public float bounceMultiplier = 1.0f;

        [Tooltip("수평 비행 속도 배율 (기본 1.0)")]
        public float speedMultiplier = 1.0f;

        [Tooltip("물수제비 회전 안정성 계수 (기본 1.0)")]
        public float stabilityMultiplier = 1.0f;
    }
}
