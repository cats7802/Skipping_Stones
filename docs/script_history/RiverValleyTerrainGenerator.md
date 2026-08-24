# 📜 Script History: RiverValleyTerrainGenerator.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 절차적 굽이치는 강골짜기 지형(Ground 메쉬, 지형 버텍스, 높이맵, 스플라인 곡선) 동적 생성 및 관리.
- ScriptableObject 기반 `RiverValleyTerrainPreset` 저장/로드 연동.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **강폭, 산맥 높이, 굴곡 오프셋 상수를 내부에 고정하지 말 것** ➜ `RiverValleyTerrainPreset` 데이터 기반으로 완벽히 동적 연산할 것.
- ❌ **메쉬 피벗(Pivot) 및 월드 좌표계 불일치 방지** ➜ `GetRiverCenterX(z)` 반환 시 월드 좌표계 기준과 메쉬 로컬 기준을 명확히 구분하여 제공할 것.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)
### [2026-08-23] 3대 지형 프리셋(개울가/시골하천/넓은강) 및 런타임 저장/로드 구축
- **수정 목적**: 지형 수치 튜닝 편의성 극대화 및 원클릭 프리셋 전환 체계 확립.
