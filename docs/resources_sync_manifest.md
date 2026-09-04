# 📦 Assets/Resources 동기화 매니페스트 (Resources Sync Manifest)

> **원칙 (Strict Rule)**:
> 1. 원본 에셋(3D 모델, 프리팹, 머티리얼, 셰이더 등)이 수정되면, `Assets/Resources/` 내의 복제본도 **100% 즉시 1:1로 복사/동기화**하여 구버전 에셋이 로딩되는 불일치 버그를 원천 차단합니다.
> 2. 가능하면 `GameController` 등 코드에서 `Resources.Load` 대신 인스펙터 직렬화 및 원본 경로를 직접 참조하도록 점진적으로 전환합니다.

---

## 📋 현재 복제/동기화 관리 에셋 목록

| No | 에셋 종류 | 원본 파일 경로 (Original Source) | Resources 복제본 경로 (Runtime Clone) | 동기화 상태 |
| :-: | :--- | :--- | :--- | :-: |
| 1 | **캐릭터 모델** | `Assets/3D/Character/Test_Chr.fbx` | `Assets/Resources/Test_Chr.fbx` | ✅ 동기화됨 |
| 2 | **애니메이션 컨트롤러** | `Assets/3D/Character/Test_Chr_CTRL.controller` | `Assets/Resources/Test_Chr_CTRL.controller` | ✅ 동기화됨 |
| 3 | **조약돌 프리팹** | `Assets/3D/prefab/Stone.prefab` | `Assets/Resources/Stone.prefab` | ✅ 동기화됨 |
| 4 | **투구 캐릭터 프리팹** | `Assets/3D/prefab/Thrower_001.prefab` | `Assets/Resources/Thrower_001.prefab` | ✅ 동기화됨 |
| 5 | **배경 프리팹** | `Assets/3D/prefab/BG_01.prefab` | `Assets/Resources/BG_01.prefab` | ✅ 동기화됨 |
| 6 | **조약돌 머티리얼** | `Assets/Materials/Stone_Pebble_Mat.mat` | `Assets/Resources/Stone_Pebble_Mat.mat` | ✅ 동기화됨 |
| 7 | **트레일 머티리얼** | `Assets/Materials/StoneTrail_Mat.mat` | `Assets/Resources/StoneTrail_Mat.mat` | ✅ 동기화됨 |
| 8 | **수면 셰이더/머티리얼** | `Assets/3D/BG/Water_MAT.mat` | `Assets/Resources/Water_MAT.mat` | ✅ 동기화됨 |
| 9 | **스카이박스 머티리얼** | `Assets/Materials/Skybox_Procedural_MAT.mat` | `Assets/Resources/Skybox_Procedural_MAT.mat` | ✅ 동기화됨 |
| 10 | **오디오 사운드** | `Assets/Resources/Audio/` (효과음 9종) | `Assets/Resources/Audio/` | ✅ 자체 보유 |
| 11 | **부스트 패드 프리팹** | `Assets/prefab/BoostPad.prefab` | `Assets/Resources/BoostPad.prefab` | ✅ 등록 완료 |
| 12 | **장애물 바위 프리팹** | `Assets/prefab/ObstacleRock.prefab` | `Assets/Resources/ObstacleRock.prefab` | ✅ 등록 완료 |
| 13 | **타겟 과녁 프리팹** | `Assets/prefab/TargetZone.prefab` | `Assets/Resources/TargetZone.prefab` | ✅ 등록 완료 |
| 14 | **친구 깃발 프리팹** | `Assets/prefab/FriendFlag.prefab` | `Assets/Resources/FriendFlag.prefab` | ✅ 등록 완료 |
| 15 | **점핑 물고기 프리팹** | `Assets/prefab/JumpingFish.prefab` | `Assets/Resources/JumpingFish.prefab` | ✅ 등록 완료 |
| 16 | **연잎 군락 프리팹** | `Assets/prefab/LilyPadCluster.prefab` | `Assets/Resources/LilyPadCluster.prefab` | ✅ 등록 완료 |
| 17 | **스타일라이즈드 수면 머티리얼** | `Assets/Materials/M_StylizedWater.mat` | `Assets/Resources/M_StylizedWater.mat` | ✅ 동기화됨 |
| 18 | **터치 버튼 스프라이트 (L/O/R)** | `Assets/2D/UI/Touch_Button_*.png` | `Assets/Resources/Touch_Button_*.png` | ✅ 동기화됨 |
| 19 | **로비 쇼케이스 프리팹** | `Assets/prefab/Lobby.prefab` | `Assets/Resources/Lobby.prefab` | ✅ 동기화됨 |
| 20 | **캐릭터 프리팹 일체 (4종)** | `Assets/prefab/Character/*.prefab` (Kai 추가) | `Assets/Resources/Character/*.prefab` | ✅ 동기화됨 |
| 21 | **돌 프리팹 일체 (4종)** | `Assets/prefab/Stone/*.prefab` | `Assets/Resources/Stone/*.prefab` | ✅ 동기화됨 |
| 22 | **랜덤 링 3D 모델** | `Assets/3D/Ingame_Object/Random_Ring.fbx` | `Assets/Resources/Random_Ring.fbx` | ✅ 동기화됨 |
| 23 | **연잎/연꽃 프리팹 (5종)** | `Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_LilyPad*.prefab` | `Assets/Resources/LilyPad_1~5.prefab` | ✅ 동기화됨 |
| 24 | **나룻터 나무 선착장** | `Assets/prefab/Lakeside_WoodenPier.prefab` | `Assets/Resources/Lakeside_WoodenPier.prefab` | ✅ 동기화됨 |

---

## 🔄 동기화 체크리스트
- [x] 외부 원본 머티리얼/셰이더 수정 시 `Resources/` 복제본 즉시 덮어쓰기
- [x] 신규 프리팹(`Thrower_001`, `Stone`) 변경 시 런타임 경로 즉시 반영
- [x] 빌드 시 구버전 에셋 캐시 로딩 방지 검증
