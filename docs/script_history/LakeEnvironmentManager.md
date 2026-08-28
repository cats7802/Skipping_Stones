# 📜 Script History: LakeEnvironmentManager.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 씬 전역 지형/수면/산맥 무한 스트리밍 오케스트레이션.
- 비거리(0~4500m)에 따른 동적 낮/노을/황혼/밤 조명 및 스카이박스 테마 전환.
- 모듈러 시퀀스(SM ➜ N개 자유 슬롯 루프 변주 ➜ EM) 및 레거시 BG_01 단일 복제 스트리밍 통합 제어.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **기존 단일 BG_01 테스트 환경 호환성 파괴 금지**:
  - startMapPrefab, loopSlots, endingMapPrefab이 비어있을 때 씬의 BG_01을 그대로 단일 복제 스트리밍하여 기존 테스트 환경과 100% 동일하게 구동할 것.
- ❌ **복제 청크 내 발판 및 PP 중복 스폰 방치 금지**:
  - 1번 이후 복제 청크(Section_1, Section_2...) 생성 시 Platform 및 Player_Position을 즉시 자동 정리할 것.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-28] 메쉬 지형(MeshCollider) 및 가변 슬롯 맵 청크 실측 동기화
- **수정 목적**: 메쉬 지형 전환에 맞추어 `MeshCollider`, `Terrain`, `WaterSurface`를 전수 감지하여 500m/1500m 등 가변 지형 길이를 100% 동적 실측.
- **핵심 구조**:
  - `AutoDetectChunkSize()`에서 `MeshCollider` 결합 바운드 최우선 측정.
  - 레거시 하드코딩된 프리팹 이름 검색을 슬롯 프리팹 동적 인스턴스화로 단일화.
  - 빌드 및 컴파일 0 Warnings, 0 Errors 완료.
