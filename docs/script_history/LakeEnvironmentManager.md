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

### [2026-08-25] 모듈러 시퀀스(SM, N-Slots 변주, EM) 스트리밍 구조 완성
- **수정 목적**: 시작 맵(SM), 가변 N개 슬롯 변주 루프, 엔딩 맵(EM)을 지원하면서도 빈 설정 시 기존 단일 BG_01과 완벽 동일 동작 보장.
- **핵심 구조**:
  - ChunkSlot 구조체 및 loopSlots 자유 리스트 도입.
  - GetMapPrefabForChunk(chunkIndex, targetZ) 다형성 결정 로직 탑재.
  - 레거시 mapPrefabs, cycleMode, aseBGChunk0 100% 하위 호환 유지.
