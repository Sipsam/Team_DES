# V3 게임 구조

최신 게임 규칙의 기획 원본은 [통합 기획서](../GameDesign/TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf)에 있다.

## 씬과 코드

| 경로 | 역할 |
| --- | --- |
| `Assets/Game/Gameplay/Scenes/V3Gameplay.unity` | Build Settings 첫 씬. 부트스트랩, NGO, 게임 상태, 퍼즐 네트워크 컴포넌트, 로비/HUD |
| `Assets/Game/Level/Scenes/LevelBlockoutV3.unity` | 두 번째 빌드 씬. 지형, 루트, 충돌체, 섹션과 바인딩 마커 |
| `Assets/Game/Gameplay/{Runtime,Prefabs,Editor,Testing}` | 게임 동작, V3 플레이어 변형과 기믹 프리팹, 저장 에셋 검증 및 Windows 빌드, Development 회귀 |
| `Assets/Game/Level/{Runtime,Editor,Data}` | 레벨 설정과 마커, V3 생성·시각화·검증, 영속 메시·재질 |
| `Assets/_Project/{Scripts,Prefabs}` | 세션, 스폰, 상태, 이동, 상호작용, 리스폰과 V3 변형의 베이스 프리팹 |
| `Assets/Settings`, `Assets/InputSystem_Actions.inputactions`, `Assets/DefaultNetworkPrefabs.asset` | URP, Input System, NGO 공용 목록 |
| `Assets/02_*`–`Assets/07_*`, `Assets/KayKit` | 팀 제작 원본, 설치 도구가 검색하는 에셋, 임포터와 모델 의존성 |

런타임과 Editor 소스는 각각 Unity의 `Assembly-CSharp`와 `Assembly-CSharp-Editor`에 컴파일된다. 저장된 컴포넌트, 중첩 프리팹과 설치 도구에서 경로·타입·GUID를 함께 사용한다. 에셋 이동 시 `.meta`와 GUID를 유지한다.

`V3GameplayBootstrap`은 이미 로드된 레벨을 검사한 뒤 필요 시 `LevelBlockoutV3`를 Additive로 로드한다. 저장 씬의 마커와 지형을 바탕으로 다리·뗏목·성채 기계·지하 장치·결승을 런타임에 구성한다. 레벨 미리보기 카메라는 비활성화하고 각 피어에서 자기 캐릭터의 카메라를 사용한다. 지형 검토에는 레벨 씬을 단독으로 연다.

## 경기 흐름

1. `WaitingForPlayers`에서 두 피어가 연결되면 `Preparing`으로 이동한다. 서버는 승인된 스폰을 적용한 두 피어가 씬 바인딩, 네트워크 오브젝트 생성과 로컬 카메라 초기화를 완료한 것을 확인한 뒤 `Outbound`를 한 번 시작한다.
2. `Outbound`에서 산악 → 다리 → 협곡 → 강 → 성채 → 지하를 협동으로 진행한다.
3. 두 플레이어가 제단에 도달하면 `SealPending`이 된다. 공동 봉인 해제 후 첫 서버 승인 보석 픽업이 `Return`을 시작한다.
4. 지하부터 산악까지 복귀하며 보석을 두고 경쟁한다. 결승 조건을 만족하면 서버가 `Finished`와 승자, 정지 타이머를 동기화한다.

## 레벨 편집과 생성물

LevelSection, 안정된 마커 ID와 설정 자산을 통해 씬과 런타임이 연결된다. 생성기는 선택한 범위의 `Generated`를 교체하고 `Manual`에 있는 제작자 편집을 유지한다. 게임플레이 오브젝트는 생성기 소유 계층 밖에 둔다.

생성 디렉터리는 새 GUID로 추가된다. 저장된 씬·프리팹·설정에서 사용 중인 생성 파일과 폴더 메타를 커밋한다. 저장하지 않은 상태와 Undo가 이전 생성물을 참조할 수 있다. 생성, 플레이 또는 Undo 세션을 끝내고 씬을 저장한 뒤 Unity를 정상 종료한다. `Tools/ProjectAudit/clean-generated.ps1`은 새 배치 세션에서 전체 비생성 에셋의 전이 의존성과 직렬화된 소유 경로를 검사한다. 형식과 출처를 확인할 수 있는 생성 폴더만 정리 대상으로 취급한다. 변경된 씬 및 새 메시와 함께 Git 삭제 목록을 검토한다.

## V3 월드 재질

팀 텍스처, 임포트 설정 및 GUID는 `Assets/Terrain1_Test/`부터 `Assets/Terrain4_Test/`까지 추적한다. 전달용 UnityPackage는 로컬에 보존하며 새 클론에서 패키지를 다시 가져올 필요는 없다. V3 렌더링 셰이더와 협곡·성채 벽·지하 돌벽 파생 재질은 `Assets/Game/Level/Data/V3WorldMaterials/`에 있다.

산악과 자연 암벽은 세 축의 월드 공간 투영을 혼합한다. 석조 바닥·벽과 지붕은 월드 공간의 면 방향으로 타일링한다. 강은 저장 메시의 폭과 중심선 누적 거리로 수면 UV를 만들고 하류 방향으로 두 레이어를 움직인다. `RiverGameplay`도 같은 Water 재질을 참조한다. 동굴 외피와 런타임 하수로는 Canyon Rock의 색상·노멀·AO를 공유하는 어두운 파생 재질을 사용한다. 생성기는 동일한 재질을 사용하며 충돌과 마커는 별도로 유지한다.

**Tools > Twin Edge > Audit project dependencies (read only)**는 Unity 의존성, GUID 및 미리보기 씬 로드를 검사한다. 내부 결과는 무시된 `Docs/Development/Architecture/V1/Audit/`에 생성된다. V1/V2 씬과 프로토타입은 Git에서 열 수 있다. [과거 프로젝트 복원 절차](../Archive/Prototype_v1.md)
