# V3 게임 구조

최신 게임 규칙의 기획 원본은 [통합 기획서](../GameDesign/TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf)에 있다.
UI·음향·연출 기준과 uGUI 전환 경계는 [프레젠테이션 설계](../GameDesign/PresentationDesign.md)를 따른다. 믹서, 큐와 음량 저장은 [오디오 구조](AudioArchitecture.md)에 기록한다.

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

`PlayerMovement`는 소유자 카메라의 수평 Forward와 Right를 기준으로 이동한다. Development 재생기는 목표 월드 방향을 같은 축에 투영한 스틱 값을 Input System으로 보낸다. 성채 재생은 정문·현관·퍼즐 문턱 양쪽의 웨이포인트를 사용하고, 기존 CharacterController와 서버 판정을 거친다.

## 경기 흐름

1. `WaitingForPlayers`에서 Client Ready를 확인한 Host가 시작을 승인하면 `Preparing`으로 이동한다. 두 피어의 스폰·씬·카메라 준비가 끝나면 서버가 3초 카운트다운을 열고, 종료 시각부터 경기 시간을 계산하며 `Outbound`로 전환한다.
2. `Outbound`에서 산악 → 다리 → 협곡 → 강 → 성채 → 지하를 협동으로 진행한다.
3. 두 플레이어가 제단에 도달하면 `SealPending`이 된다. 공동 봉인 해제 승인 후 1.5초 연출을 거쳐 획득할 수 있다. 첫 서버 승인 보석 획득이 `Return`과 2초 연출을 함께 시작한다. [입력과 재입장 계약](Multiplayer.md#서버-연출-구간)
4. 지하부터 산악까지 복귀하며 보석을 두고 경쟁한다. 결승 조건을 만족하면 서버가 `Finished`와 승자, 정지 타이머를 동기화한다.

## 레벨 편집과 생성물

LevelSection, 안정된 마커 ID와 설정 자산을 통해 씬과 런타임이 연결된다. 생성기는 선택한 범위의 `Generated`를 교체하고 `Manual`에 있는 제작자 편집을 유지한다. 게임플레이 오브젝트는 생성기 소유 계층 밖에 둔다.

V3 산악의 `MountainConfigV3`는 바깥 암릉과 횡단 능선을 월드 좌표로 정의한다. 생성기는 비대칭 사면과 공유 암석 선반을 만들고, 그 위에 막대 착지용 지지면과 협곡을 맞춘다. 남쪽 산기슭의 보행 단면도 같은 지질 메시와 Collider에 이어진다. `Mountain.V3.InitialSpawn`은 산기슭, `Mountain.V3.Start`는 막대의 홈, `Mountain.V3.ReturnFinish`는 시작 정상에서 산기슭으로 내려가는 입구다. 착지 시 막대는 설정에 지정된 지지 좌표와 저장 Collider를 함께 검사한다. 연결 검사는 이 Collider에서 두 캡슐과 막대의 회전 및 연속 이동을 검증한다.

하부와 중앙 착지면은 기존 지지 범위를 포함하는 불규칙한 암석 테두리를 사용한다. 서쪽 안부의 침식 홈은 산기슭에서 주 능선으로 이어지던 보행 연결을 끊으며, 서쪽 선반은 막대 경로에서 계속 접근할 수 있다. DeepGullies는 착지 지지면을 보존하면서 일부 봉우리 사이의 기존 골을 깊게 한다. 저장된 `ConnectedRidgeline` 메시를 렌더러와 MeshCollider가 공유한다. 산기슭의 완만한 초입과 V3 최초 스폰의 카메라 피치 −5°가 첫 시야를 결정한다.

왕로 막대 구간의 황토색 테두리는 실제 암석 표면을 따라 생성된다. 봉우리별로 0.8m 간격에서 서버와 같은 다섯 지점 지지 및 캐릭터 캡슐 여유를 검사하고, 판정이 바뀌는 구간을 가는 띠로 표시한다. 테두리에는 Collider가 없으며 저장된 지형 메시와 FinalPolish 재질을 그대로 사용한다.

DeepGullies의 정상 림과 암반 기저 접합부를 좁은 범위에서 둥글게 연결한다. 생성기는 지지 타원 주변에 여유를 두고 기존 바닥으로 이어지는 높이 차이만 완화한다. 저장 메시의 삼각형 연결과 평면 좌표, 봉우리 마커 및 서쪽 침식 홈은 유지한다.

저장한 산악의 Top, Perspective, World Top과 주요 지상 시점을 **Tools > Twin Edge > V3 Mountain > Capture Design Review**에서 캡처할 수 있다. 검토용 이미지는 로컬 `Docs/Development/Level/V3/MountainRedesign/RidgelineReview/After/`에 저장된다.

생성 디렉터리는 새 GUID로 추가된다. 저장된 씬·프리팹·설정에서 사용 중인 생성 파일과 폴더 메타를 커밋한다. 저장하지 않은 상태와 Undo가 이전 생성물을 참조할 수 있다. 생성, 플레이 또는 Undo 세션을 끝내고 씬을 저장한 뒤 Unity를 정상 종료한다. `Tools/ProjectAudit/clean-generated.ps1`은 새 배치 세션에서 전체 비생성 에셋의 전이 의존성과 직렬화된 소유 경로를 검사한다. 형식과 출처를 확인할 수 있는 생성 폴더만 정리 대상으로 취급한다. 변경된 씬 및 새 메시와 함께 Git 삭제 목록을 검토한다.

협곡의 석조 계단은 `ValleyConfigV3`의 다리 끝과 하류 부두 접근점을 직선으로 잇는다. 단차는 최대 0.16m이며 양쪽 난간을 따라 기반 암반에 연결된다. 계단과 난간은 각각 렌더러와 Collider가 같은 메시를 사용한다. 생성기는 협곡의 공유 경로와 계단 접합 지형을 함께 갱신한다. 기존 저장 씬에서 해당 구간만 다시 생성하려면 **Tools > Twin Edge > V3 Bridge River > Refresh Canyon Stairway**를 사용한다.

## V3 월드 재질

팀 텍스처, 임포트 설정 및 GUID는 `Assets/Terrain1_Test/`부터 `Assets/Terrain4_Test/`까지 추적한다. 전달용 UnityPackage는 로컬에 보존하며 새 클론에서 패키지를 다시 가져올 필요는 없다. V3 렌더링 셰이더와 협곡·성채 벽·지하 돌벽 파생 재질은 `Assets/Game/Level/Data/V3WorldMaterials/`에 있다.

산악과 자연 암벽은 세 축의 월드 공간 투영을 혼합한다. 석조 바닥·벽과 지붕은 월드 공간의 면 방향으로 타일링한다. 강은 저장 메시의 폭과 중심선 누적 거리로 수면 UV를 만들고 하류 방향으로 두 레이어를 움직인다. `RiverGameplay`도 같은 Water 재질을 참조한다. 동굴 외피와 런타임 하수로는 Canyon Rock의 색상·노멀·AO를 공유하는 어두운 파생 재질을 사용한다. 생성기는 동일한 재질을 사용하며 충돌과 마커는 별도로 유지한다.

산악의 `Mountain_ConnectedGeology`는 서브메시 하나와 `MountainBlend` 재질 슬롯 하나를 사용한다. 이 재질은 팀의 Top, Middle, Bottom 색상 맵과 노멀 맵을 월드 공간에서 세 방향으로 투영한다. 승인된 FinalPolish는 능선 기준 높이, 경사와 월드 공간 색 변화로 정상부, 절벽, 골을 혼합한다. 절벽 측면 투영에 약한 수직 결을 남기고, 완만한 암석에 제한적으로 토양과 이끼색을 더한다. `HorizonRock`은 원경 능선에 같은 Cliff 색상·노멀 맵을 더 큰 스케일로 투영한다. PC URP의 Forward+에서 산악 레이어는 클러스터 광원 변형을 사용한다. 산악 지형과 재질의 검증 범위는 [검증 요약](../Playtest/ValidationSummary.md#승인된-deepgullies--finalpolish-산악)을 참고한다.

`V3 Geological Context`는 다리 아래 협곡, 외곽 능선, 강 끝 암벽과 성채 기단을 렌더링한다. 협곡과 강의 `TerrainSurface`는 기존 Collider 메시에서 생성한 별도 렌더 메시다. 보행로, 수로, 접합부의 정점은 충돌 메시와 일치한다. 성채 지붕·바닥과 동굴 용암은 원본 텍스처와 노멀을 공유하는 V3 파생 재질을 사용한다.

성채의 평평한 안뜰과 지하문 뒤 연결부에는 기단의 석재 바닥을 사용한다. 지하 계단의 수평 데크는 같은 높이에서 겹치는 면을 잘라 한 번씩 렌더링한다. 기존 계단과 안뜰 Collider는 저장 씬에 그대로 있고, 계단탑 남쪽 벽과 안쪽 천장·바닥은 지하 입구를 남기고 외부를 가린다. 계단탑과 내부에서 보이는 북동쪽 성벽은 기존 벽 텍스처를 트라이플래너 방식으로 투영한다.

게임플레이 장식 효과는 저장 월드와 별도의 런타임 Particle System에서 URP Particles/Unlit 공유 재질을 사용한다. D3D11에서 지형 재질과 같은 카메라 깊이 검사를 적용한다. [입자 수명과 캐릭터 바인딩](GameplaySystems.md#캐릭터-애니메이션과-월드-효과)

**Tools > Twin Edge > Audit project dependencies (read only)**는 Unity 의존성, GUID 및 미리보기 씬 로드를 검사한다. 내부 결과는 무시된 `Docs/Development/Architecture/V1/Audit/`에 생성된다. V1/V2 씬과 프로토타입은 Git에서 열 수 있다. [과거 프로젝트 복원 절차](../Archive/Prototype_v1.md)


## V3 공개 지도 아틀라스

`CourseMapBake.Bake`는 저장된 `LevelBlockoutV3`의 승인 DeepGullies 산악, 다리, 협곡 계단, 강과 부두, 성채 외벽과 기반, 지하 계단 및 동굴 지형을 선택한다. 지정 메시의 월드 좌표 삼각형을 +X 오른쪽, +Z 위쪽으로 래스터화한다. 실내는 FortressConfig의 공개 방 평면을 사용한다. 하수도 지오메트리와 함정, 장애물, 이동 발판 순서, 경로 그래프는 입력 목록에서 제외한다. 월드 렌더러, 저장 씬과 Collider에는 쓰지 않는다.

Unity에서 **Tools > Twin Edge > Presentation > Bake Public Course Map** 또는 `-executeMethod TwinEdge.Presentation.Editor.CourseMapBake.Bake`를 실행한다. 저장된 메시, 공개 공간의 구성 또는 마커 위치가 변경되면 재생성한다. `Assets/Game/UI/Resources/V3CourseMap.asset`에 월드 X/Z 범위, 확대 기준과 마커가 기록된다. 외부 아틀라스는 목표 1.6 m/px, 실내·지하 별도 시트는 0.65 m/px로 생성한다. 구간별 반경은 산악 150m, 다리 60m, 협곡·강 120m, 성채 외부 60m, 성채 내부 40m, 지하 계단·공동 50m, 결승 120m다. 구간 전환은 0.32초 안정화 뒤 반영한다. 화면에는 사전 생성 지도와 재사용 아이콘을 합성한다.

## 결과 화면과 새 경기 수명

`SessionPresentation`은 확정된 경기 결과에 전용 uGUI 화면을 표시한다. 결과와 로비의 캐릭터 미리보기에는 렌더러와 Animator, 미리보기 카메라와 조명만 사용한다. 화면을 닫을 때 PlayableGraph, 렌더 텍스처와 미리보기 오브젝트를 해제한다.

재경기에서는 NGO가 `V3Gameplay`를 Single로 다시 열고 `LevelBlockoutV3`를 Additive로 다시 연다. 저장된 레벨을 기반으로 기믹의 런타임 바인딩과 초기 스폰을 다시 구성한다. 세션 루트는 NGO의 DontDestroyOnLoad 수명을 사용하며, 새 부트스트랩이 기존 루트에 연결한다. 로컬 닉네임, Master 음량, 미니맵 표시 설정은 기존 PlayerPrefs를 통해 유지한다. [서버 동의 및 복구](Multiplayer.md#결과와-재경기)
