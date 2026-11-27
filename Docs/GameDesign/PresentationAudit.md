# Twin Edge V3 Presentation Audit

2026-11-27 KST · V3-PRESENTATION-AUDIT · 기준 `7f17e6c1505af8fb4c3386c22c995dea6faa8468`. Standard 경로의 Explore, Review, Finalize를 한 세션에서 수행했다.

이 문서는 승인 전 조사 기록이다. 당시의 미연결 자산과 미구현 판정은 기준 커밋에 해당한다. 현행 결과 재생과 런타임 클립 및 효과는 [프레젠테이션 설계](PresentationDesign.md)와 [애니메이션 구현 계약](../Architecture/GameplaySystems.md#캐릭터-애니메이션과-월드-효과)을 참조한다. 후속 개발 순서는 [로드맵](../Workflow/ROADMAP.md)을 따른다.

## 기준과 증거

- [확정 통합 기획서](TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf)의 Windows 2인, 캐주얼 로우폴리, 공동 봉인 → 첫 보석 획득 → 복로 경쟁, 보석 소지 결승이 방향을 정한다. 왕로에서도 PvP가 가능하다. 현행 조작·구간은 [Gameplay Systems](../Architecture/GameplaySystems.md), [멀티플레이](../Architecture/Multiplayer.md), [실행 안내](../../README.md) 및 저장 소스에서 대조했다. PDF의 즉시·무예고 붕괴, 다리 복로 복구 지점, 하수도 오답 페널티는 각각 피드백 06·01·09를 반영한 현재 동작과 차이가 있다. 온보딩 문구는 현행 동작을 따른다. 별도 레벨·UI 상세 기획서는 저장소와 Docs Git 이력에서 확인되지 않아 세부 화면 명세는 미확인이다.
- [Build Settings](../../ProjectSettings/EditorBuildSettings.asset) → [V3Gameplay](../../Assets/Game/Gameplay/Scenes/V3Gameplay.unity) → [LevelBlockoutV3](../../Assets/Game/Level/Scenes/LevelBlockoutV3.unity) 및 [V3NetworkPlayer](../../Assets/Game/Gameplay/Prefabs/V3NetworkPlayer.prefab)를 추적했다. 두 번째 씬은 부트스트랩이 Additive로 연결한다. [NetworkDebugUI](../../Assets/_Project/Scripts/Networking/NetworkDebugUI.cs)는 저장 씬·프리팹에 연결되지 않은 이전 UI이며 입력의 포인터 검사 호출은 남아 있다. 현재 OnGUI와 Editor/Development 전용 F3의 범위는 [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs)에서 확인했다.
- [Validation Summary](../Playtest/ValidationSummary.md)의 피드백 01–09, Unity 6000.3.27f1 Windows D3D11 일반 Player `2079709`의 방·스폰·시작 smoke 및 [2차 체크리스트](../Playtest/SecondHumanPlaytest.md)를 사용했다. 선택적 로컬 자료 중 `Docs/Development/SecondHumanPlaytest/V1/Lobby.png`, `HostStart.png`와 공식 `Screenshots/Underground_perspective_v3_2079709.png`를 열어 대조했다. 전자는 기본 회색 패널, 산악 배경 및 시작 HUD를, 후자는 지하 발판 레인과 용암의 공간 대비를 보여준다. 정지 이미지로 타이밍·가청성·초보자 이해도는 판정할 수 없다.

**상태 기준** COMPLETE는 명시한 제한 범위의 동작과 완성 기준이 기존 증거에서 충족된 경우다. IMPLEMENTED는 연결 확인, PARTIAL은 일부만 연결, NEEDS_POLISH는 개선할 표현이 확인된 경우다. NOT_IMPLEMENTED는 조사한 활성 경로에서 해당 기능이 없음을 확인한 경우다. UNUSED_ASSET은 현재 활성 연결이 없는 개별 자원, NEEDS_HUMAN_REVIEW는 플레이 체감 평가, UNCONFIRMED는 판단할 자료가 부족한 경우다.

## UI와 UX

| 항목 | 상태와 근거 |
| --- | --- |
| 메인 타이틀·배경·시작·종료 | NEEDS_POLISH. `TWIN EDGE` 텍스트와 대기 카메라로 보이는 산악 지형, Create/Join 및 Quit이 같은 패널에 있다. 독립 로고, 전용 배경 구성, 메뉴 전환 효과와 메인 소개·조작 화면은 NOT_IMPLEMENTED. 경기 중 Esc 메뉴에는 기본 조작 문구가 있다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |
| 방 코드·참가 | IMPLEMENTED. 코드 표시와 시스템 클립보드 Copy, 코드 입력, Host/Client 생성·참가. 일반 Player에서 방 생성·참가·자동 시작 smoke를 완료했다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) · [2차 체크리스트](../Playtest/SecondHumanPlaytest.md) |
| 로비 상태·오류·준비 | PARTIAL. 진행/연결 문구, busy 중 입력 차단, 2인 카운트, 파트너 대기·준비·자동 시작 안내가 있다. 오류는 예외 메시지를 포함하는 공용 상태 줄이고 별도 단계 표시, 다음 행동 안내, Copy 완료 표시가 없다. [세션 관리자](../../Assets/_Project/Scripts/Networking/MultiplayerSessionManager.cs) · [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |
| 재접속과 상대 상태 | PARTIAL. 파트너 재접속 대기 문구, 연결 종료 메시지, 같은 코드 재입장 시 슬롯·진행 복구가 있다. Host 종료 시 새 방이 필요하다. 자동 재접속, 전용 재시도 흐름, 경기 HUD의 지속적인 상대 상태 패널은 없다. 보석 소유 및 일부 협동 상태는 별도로 표시된다. [멀티플레이](../Architecture/Multiplayer.md) · [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |
| 스태미나·PvP·보석 | IMPLEMENTED. 스태미나 숫자, 공격·잡기 쿨다운, 잡힘·F 저항 진행·던지기 충전 및 내/상대 보석 소유, 바닥 드롭과 재배치를 표시한다. HUD에 플레이어 HP를 추가할 근거는 없다. [Contest HUD](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.Contest.cs) · [보석 HUD](../../Assets/Game/Gameplay/Runtime/GemOwnership.cs) |
| Phase·목표·타이머 | PARTIAL. 왕로·봉인 대기·복로 문구와 전체 목표 및 조건부 구간 목표가 있다. 경기 시간은 서버 시작/종료 시각으로 계산하며 일반 HUD는 결과에서만 노출한다. 상시 타이머는 NOT_IMPLEMENTED, 화면 노출 여부는 디자인 결정 사항이다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) · [경기 상태](../../Assets/_Project/Scripts/Game/GameStateManager.cs) |
| 상호작용·구간 HUD | IMPLEMENTED. 현재 대상 Prompt, 막대 역할과 착지 후 교대 지시, 다리 납품 수, 뗏목 내구도·피해 강조, 성채·지하·봉인 진행, 복로 발사 조작·충전·고도·예상 접촉 상태가 조건부 표시된다. 막대의 실제 착지 판정 상세는 개발 전용이다. R 링은 본인에게 표시된다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) · [막대](../../Assets/Game/Gameplay/Runtime/CooperativePole.cs) · [발사](../../Assets/Game/Gameplay/Runtime/MountainReturnLaunch.cs) · [뗏목](../../Assets/Game/Gameplay/Runtime/RiverGameplay.Presentation.cs) |
| 체크포인트와 리스폰 | PARTIAL. 마지막 위치명과 누적 리스폰 횟수, 지하의 상황별 복구 문구는 있다. 활성화 순간이나 복구 이유를 알려주는 공통 안내가 없다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) · [지하 HUD](../../Assets/Game/Gameplay/Runtime/UndergroundGameplay.Layout.cs) |
| 설정·나가기 | NOT_IMPLEMENTED. 사용자용 감도, 오디오, 해상도·화면 모드, 그래픽 설정 및 저장이 없다. 카메라 감도는 직렬화된 값이다. PARTIAL. Esc는 커서를 해제하고 방 나가기를 제공한다. 일시정지·나가기 확인 화면은 없다. [카메라](../../Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs) · [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |
| 결과 | PARTIAL. 각 피어의 VICTORY/DEFEAT와 정지 경과 시간이 HUD에 표시된다. Esc → Leave room으로 방을 나갈 수 있다. 전용 결과 화면, 결과 전용 로비 복귀 동작과 재경기 흐름은 NOT_IMPLEMENTED. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) · [경기 상태](../../Assets/_Project/Scripts/Game/GameStateManager.cs) |
| 스타일·해상도 | NEEDS_POLISH. 기본 GUI 스킨의 작은 영어 텍스트, 좌상단 박스, 동일 계층의 줄과 버튼을 사용한다. 성채 문양과 강 주황 피해 강조 등 구간별 의미 색은 보존 가치가 있다. 폭 360/390px 상한 및 높이 제약이 있고 스크롤 영역이 없어 작은 창, 긴 오류·번역, 동시 HUD 줄에서 잘림·가독성 저하 위험이 있다. 실제 다중 해상도 잘림은 UNCONFIRMED. 아이콘·타이포·여백·색·패널의 공통 체계 및 월드 조화는 후보 작업으로 둔다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |

### 게임 안에서 처음 배우는 조작

아래 표현의 연결은 확인했다. 외부 체크리스트 없이 처음 접한 두 사람이 행동을 이해하는지는 전체 행에서 NEEDS_HUMAN_REVIEW다.

| 구간 | 게임 내 설명과 비어 있는 정보 |
| --- | --- |
| 산악 막대 | IMPLEMENTED. 잡기·역할·이동·교대 지시와 황토색 지지 테두리가 있다. 두 사람이 처음 다른 역할을 맡을 때 입력과 안전한 교대 시점을 이해하는지 확인한다. [막대](../../Assets/Game/Gameplay/Runtime/CooperativePole.cs) · [레벨 구조](../Architecture/GameArchitecture.md) |
| 다리 목재 | PARTIAL. 근접 Prompt와 납품 카운트가 있다. 두 사람의 입력 합성·설치 승인·반납을 언제 읽고 이해하는지 확인한다. [건설](../../Assets/Game/Gameplay/Runtime/BridgeGameplay.Construction.cs) |
| 공동·개인 뗏목 | IMPLEMENTED. 탑승 Prompt, 노젓기/조향 안내, 스태미나 사용과 내구도 표기가 있다. 분리 후 조향의 차이와 피해 복구를 이해하는지 확인한다. [강 HUD](../../Assets/Game/Gameplay/Runtime/RiverGameplay.Presentation.cs) |
| 성채 | IMPLEMENTED. 송신/수신 벽 안내, 문양 버튼, 승인·오답·기록·키패드와 하수도 입력 피드백이 있다. 피드백 08·09 경로를 유지하고 두 시점에서 안내 발견과 다음 행동을 평가한다. [성채 레이아웃](../../Assets/Game/Gameplay/Runtime/CastleGameplay.Layout.cs) · [복로](../../Assets/Game/Gameplay/Runtime/CastleGameplay.Return.cs) |
| 지하 발판·공동 봉인 | IMPLEMENTED. 전환 순서, 발판 잔여 시간, 봉인 참여 상태와 대상 Prompt가 있다. 상대편을 열어 준다는 인과관계 및 봉인 완료와 첫 픽업의 차이를 이해하는지 평가한다. [지하 HUD](../../Assets/Game/Gameplay/Runtime/UndergroundGameplay.Layout.cs) |
| PvP·보석 | PARTIAL. Esc 메뉴의 클릭/F와 상황별 저항·던지기·드롭 문구가 있다. 첫 대면에서 잡기 제한, 소유자에게 일어나는 변화, 보석을 먼저 집어도 되는 시점을 전달하는 별도 안내가 없다. [Contest HUD](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.Contest.cs) · [보석](../../Assets/Game/Gameplay/Runtime/GemOwnership.cs) |
| 복로 다리·발사 | PARTIAL. 다리는 간격 점프 문구와 지면 전조를 제공하고, 발사는 HUD와 소유자 착지 미리보기가 있다. 한 칸/두 칸 조작 선택, 위험 접촉과 예측 보류의 차이를 설명 없이 구별하는지 확인한다. [다리](../../Assets/Game/Gameplay/Runtime/BridgeGameplay.Return.cs) · [발사](../../Assets/Game/Gameplay/Runtime/MountainReturnLaunch.cs) |
| 결승 | PARTIAL. 복로 목표와 네 경계 표식이 있다. 미소유 선착은 승리하지 않고 지지 상태와 경계 유지가 필요하다는 전용 진입 안내는 없다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |

## Audio

NOT_IMPLEMENTED는 아래 조사 범위에서 공통으로 적용한다. 전체 Assets 런타임 C#의 AudioSource/AudioClip/AudioMixer 및 재생 호출, 저장 씬·프리팹의 오디오 컴포넌트, 일반 오디오 확장자와 AudioImporter를 검색했다. 재생 연결, 믹서, 재사용할 AudioClip이 발견되지 않았다. 활성 씬의 카메라 AudioListener는 확인했으며 기존 smoke는 활성 Listener 하나를 검사한다. Listener만으로 효과음 재생을 입증할 수 없다. 오디오를 포함한 추가 자료가 작업공간 밖에 있는지는 UNCONFIRMED다. [씬](../../Assets/Game/Gameplay/Scenes/V3Gameplay.unity) · [Smoke](../../Assets/Game/Gameplay/Testing/V3GameplaySmoke.cs)

| 범위 | 확인 결과 |
| --- | --- |
| BGM | 메인·로비·왕로·복로·첫 보석 전환·승/패의 음악 재생 경로와 전환 믹스가 없다. |
| 환경음 | 산악 바람·강물·물보라·협곡·성채·동굴·용암의 루프, 위치 송출, 구역 간 연결이 없다. 강 수면·비와 지하 용암은 시각 표현이다. |
| 플레이 SFX | 발걸음·달리기·점프·착지, 막대·목재·설치·붕괴, 뗏목·피격, 버튼·문·함정 전조/공격, 봉인·보석 획득/드롭/탈취, 공격/피격/잡기/던지기, 발사·체크포인트·승/패 재생 연결이 없다. |
| 구조 | 거리 감쇠, 2D/3D 라우팅, 믹서/볼륨 저장, 중복 억제, 네트워크 상태에서 소리까지의 매핑이 없다. 서버 승인과 소유자 입력을 구분할 기존 상태는 재사용 가능하다. |

## VFX와 월드 피드백

| 항목 | 상태와 근거 |
| --- | --- |
| 공격·피격·잡기·던지기 | PARTIAL. 복제된 자세와 이동 반응·HUD는 있다. 별도 타격/잡기 VFX는 조사한 연결에서 찾지 못했다. 피드백 05 자세·카메라·망토 보존. [Contest](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.Contest.cs) |
| 점프·착지·발사 | PARTIAL. Jump 포즈, 충전 HUD 및 링은 있다. 별도 먼지·발사·착지 임팩트는 확인되지 않았다. 기존 예측은 양쪽 소유자 범위에서 COMPLETE, 일반 착지의 표현과 구분한다. [발사](../../Assets/Game/Gameplay/Runtime/MountainReturnLaunch.cs) · [검증 요약](../Playtest/ValidationSummary.md) |
| 막대·목재·붕괴 | IMPLEMENTED. 막대 끝점과 역할 안내·지지 테두리, 목재의 설치 상태, 다리 주황 전조 후 렌더/Collider 제거. 별도 설치 먼지·파편은 확인되지 않았다. 피드백 03·04·06의 테두리·전조는 보호한다. [막대](../../Assets/Game/Gameplay/Runtime/CooperativePole.cs) · [건설](../../Assets/Game/Gameplay/Runtime/BridgeGameplay.Construction.cs) · [붕괴](../../Assets/Game/Gameplay/Runtime/BridgeGameplay.Return.cs) |
| 강·뗏목 | 선체 주황 피격과 탑승자 HUD는 검증한 피격 범위에서 COMPLETE. IMPLEMENTED. 수면 셰이더, 복로 강 구간 ParticleSystem 비, 포식자 LineRenderer 전조와 턱, 소용돌이 모델이 있다. 전용 피격 물보라·노젓기 물결은 확인되지 않았다. [강 Presentation](../../Assets/Game/Gameplay/Runtime/RiverGameplay.Presentation.cs) · [월드 재질](../Architecture/GameArchitecture.md#v3-월드-재질) |
| 성채·하수도 | 구현·격리 검증된 버튼 승인 하강과 기록, 하수도 녹색 승인 표시 및 복원은 해당 범위에서 COMPLETE. 전체 완료를 강조하는 독립 효과는 확인되지 않았다. [성채 레이아웃](../../Assets/Game/Gameplay/Runtime/CastleGameplay.Layout.cs) · [복로](../../Assets/Game/Gameplay/Runtime/CastleGameplay.Return.cs) |
| 지하·회랑 함정 | IMPLEMENTED. 전조 오브젝트와 구간별 시간에 따라 불·화살·가시 등의 가시성/위치가 변한다. 저장 씬이 참조한 불 프리팹에는 ParticleSystem이 있다. 실제 가독성과 오디오 없는 상황에서 전조를 알아채는지는 NEEDS_HUMAN_REVIEW. 피드백 09의 위치·시야·공격 표현을 유지한다. [지하 함정](../../Assets/Game/Gameplay/Runtime/UndergroundGameplay.Hazards.cs) · [성채 함정](../../Assets/Game/Gameplay/Runtime/CastleGameplay.Return.cs) |
| 봉인·보석·Phase | PARTIAL. 봉인 상태와 모델, 보석의 위치/소유 표시, 주 경로 발판 영구 활성화와 HUD 변화가 있다. 획득·드롭·탈취를 서로 구별하는 전용 효과와 첫 픽업의 공통 전환 큐는 확인되지 않았다. [지하](../../Assets/Game/Gameplay/Runtime/UndergroundGameplay.Layout.cs) · [보석](../../Assets/Game/Gameplay/Runtime/GemOwnership.cs) |
| 체크포인트·결승 | PARTIAL. 체크포인트 HUD와 결승 경계·결과 텍스트는 있다. 범용 활성화·복구 효과와 승리 효과는 확인되지 않았다. [부트스트랩](../../Assets/Game/Gameplay/Runtime/V3GameplayBootstrap.cs) |

## Animation

활성 씬의 PlayerPrefab → V3NetworkPlayer → CharacterWithAnimation Animator → [CharacterAnimation.controller](../../Assets/06_Character_Animation_Test/CharacterAnimation.controller)의 참조를 확인했다. Base Layer 하나에 Idle/Walk/Run/Jump/Walk_Carry 상태가 있고 파라미터와 상태 전이 목록은 비어 있다. [GameplayCharacter](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.cs)가 root motion을 끄고 소유자 motion byte를 복제해 양쪽에서 0.12초 crossfade를 실행한다. 공격·피격·잡기·던지기는 복제 상태/서버 시각과 별도 절차적 자세를 쓴다. Animator 클립의 프레임 단위 동기나 별도 NetworkAnimator 연결은 확인되지 않았다.

| 항목 | 상태와 근거 |
| --- | --- |
| Idle·Walk·Run | IMPLEMENTED. 수평 이동량을 0.08초 이상 간격으로 표본화하고 4m/s를 경계로 Run을 선택, SprintSpeedScale로 Run 재생 속도를 조정한다. 보행 3.5m/s, 달리기 8.4m/s는 검증 수치다. 발 미끄러짐, 감속 구간, 피어별 전환 순간의 불일치는 NEEDS_HUMAN_REVIEW이며 현 조사에서 결함으로 확정하지 않았다. [캐릭터](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.cs) · [검증 요약](../Playtest/ValidationSummary.md) |
| 도약·낙하·착지 | PARTIAL. 공중에 Jump를 공유하며 발사 착지는 Idle로 처리한다. 독립 낙하·착지 클립과 연결은 확인되지 않았다. 최소한 별도 포즈/타이밍 작업이 필요하며 새 클립 여부는 프로토타입 후 결정한다. [캐릭터](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.cs) |
| 공격·피격·잡기·들림·저항·던지기 | IMPLEMENTED. 공격·피격·잡기·들림·던지기 자세가 연결됐다. F 저항은 진행 표시와 탈출 상태가 있고 독립 저항 루프는 확인되지 않았다. 피드백 05의 기존 자세·시간·카메라를 재설계하지 않는다. [Contest](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.Contest.cs) · [검증 요약](../Playtest/ValidationSummary.md) |
| 막대·목재·뗏목 | PARTIAL. 막대 축/회전에 Walk_Carry/Jump, 이동 중 목재 운반에 Walk_Carry, 뗏목은 Idle과 좌/우 팔의 절차적 노젓기·동기화된 선체 자리 표현을 쓴다. 정지 운반/그립, 탑승, 전신 노젓기 전용 클립은 확인되지 않았다. 손과 막대·널빤지·노가 어긋나는지 실제 양쪽 시점에서 확인한 뒤 리그 보정/신규 모션을 판단한다. [캐릭터](../../Assets/Game/Gameplay/Runtime/GameplayCharacter.cs) |
| 버튼·문·봉인·보석 | PARTIAL. 장치의 상판·문·봉인·보석은 상태에 따라 이동·가시성·표시를 바꾼다. 플레이어 전용 버튼, 봉인, 보석 픽업 연기는 확인되지 않았다. 원본 FBX의 PickUp은 미연결 후보이며 버튼·봉인 전용 클립의 보유 여부는 UNCONFIRMED다. [성채](../../Assets/Game/Gameplay/Runtime/CastleGameplay.Layout.cs) · [지하](../../Assets/Game/Gameplay/Runtime/UndergroundGameplay.Layout.cs) · [보석](../../Assets/Game/Gameplay/Runtime/GemOwnership.cs) |
| 승·패 | UNUSED_ASSET. FBX 원본의 Victory/Defeat은 활성 컨트롤러에 없다. 경기 결과 연기는 NOT_IMPLEMENTED. [팀 모션 인수 기록](../../Assets/06_Character_Animation_Test/UsageAndValidation.md) |

## 경기 연출

| 순간 | 화면에 전달되는 내용과 간극 |
| --- | --- |
| 실행 → 두 사람 시작 | PARTIAL. 산악 대기 카메라와 방 화면에서 양쪽 스폰·왕로 목표로 넘어간다. 브랜드 인트로, 두 캐릭터 소개, 별도 시작 큐가 없다. |
| 구간 진입 → 퍼즐 완료 | PARTIAL. 근접 Prompt, 벽 안내, 탑승/역할/발판 등 상태 변화가 행동을 알린다. 새 구간의 목표와 완료를 공통 방식으로 연결하는 큐가 없다. 성채·하수도의 최근 피드백은 유지한다. |
| 두 사람이 봉인 해제 → 첫 보석 획득 | PARTIAL. 앞 단계는 보석을 집을 수 있게 하고 뒤 단계가 실제 Return 트리거다. HUD 목표와 보석 소유·경로·복로 강 비가 바뀐다. 전환 지점에서 두 피어가 같은 순간을 읽게 하는 사운드·VFX·애니메이션 큐가 부족하다. 보석을 집은 사람과 추격자가 각각 해야 할 행동을 이해하는지는 NEEDS_HUMAN_REVIEW다. |
| 추격 → 결승 → 결과·종료 | PARTIAL. 경계, 보석 소유 목표, 승/패와 정지 시간은 있다. 결승 진입, 승리 확정, 종료 후 행동을 잇는 표현과 독립 결과 화면이 없다. |

위 흐름은 [경기 상태](../../Assets/_Project/Scripts/Game/GameStateManager.cs), [보석 승인](../../Assets/Game/Gameplay/Runtime/GemOwnership.cs), [씬과 경기 흐름](../Architecture/GameArchitecture.md) 및 [일반 Player smoke](../Playtest/ValidationSummary.md) 기준이다.

## 기존 에셋과 재사용

| 자원과 출처 | 활성 연결·재사용 조건 |
| --- | --- |
| 팀 캐릭터 FBX 및 [애니메이션 작업 폴더](../../Assets/06_Character_Animation_Test) | Idle/Walk/Run/Jump/Walk_Carry 복사 클립은 사용 중. 원본 `Assets/02_Medieval_Fantasy_Models/00_Characters/01_Characters/CHAR_PLAYER_BASE_01.fbx`의 Victory/Defeat/PickUp/Run_Carry는 미연결 후보. 같은 캐릭터 리그에서 아바타·포즈·클립 경계와 두 피어를 검사한 뒤 파생 컨트롤러에 연결할 수 있다. Punch/RecieveHit 등도 목록에 있으나 현재 PvP 대체 후보에서 제외한다. [인수 기록](../../Assets/06_Character_Animation_Test/UsageAndValidation.md) |
| 팀 [05 기믹](../../Assets/05_Gimmick_Working_Assets)·[07 수정 기믹](../../Assets/07_Revised_Gimmick_Models) | V3Gameplay의 실제 GUID로 05의 뗏목·노·목재 상자·성문·제단·봉인·보석·지하 화염, 07의 다리 판자/틀·문양 발판·물고기/소용돌이·성채 화염을 확인했다. 월드 문양과 보석/봉인 메시·재질 및 불 파티클을 UI 모티프/전환 효과의 출발점으로 검토한다. 월드 메시나 원본 프리팹 변경 없이 파생 표현의 스케일·피벗·URP D3D11·과다 발광/입자를 확인해야 한다. [씬](../../Assets/Game/Gameplay/Scenes/V3Gameplay.unity) · [팀 검토본 설명](../../Assets/07_Revised_Gimmick_Models/README.md) |
| 팀 지형 텍스처 및 [V3WorldMaterials](../../Assets/Game/Level/Data/V3WorldMaterials) | 강 Water, Canyon Rock과 용암 등 현재 파생 재질 사용. 메뉴 배경 구도와 효과의 색/재질 기준으로 재사용 가능. 리뷰용 원형 물 표면과 실제 워터 렌더 경로는 별도다. [World materials](../Architecture/GameArchitecture.md#v3-월드-재질) |
| 외부 KayKit Dungeon Remastered | `Assets/KayKit/addons/kaykit_dungeon_remastered/Assets/obj/dungeon_texture.png`는 활성 씬의 목재 상자 프리팹 → `91_Materials/MedievalMatte_58.mat`에 연결된다. 배포자는 Dungeon Remastered 1.1에서 이름이 바뀐 Dungeon Pack을 CC0 1.0으로 표시한다. 반입본의 정확한 버전·원본 증빙과 모든 참조의 실제 화면 노출은 UNCONFIRMED이며 추가 사용 전에 대조한다. [배포자 페이지](https://kaylousberg.itch.io/kaykit-dungeon-pack) |
| Audio, UI 이미지와 폰트 | 재생할 기존 AudioClip은 확인되지 않았다. 활성 UI는 GUI 기본 스킨이며 전용 브랜드 로고·폰트·아이콘 세트의 승인은 확인되지 않았다. 팀 월드 문양을 참고하고 신규 음원·이미지·폰트는 작업 승인과 출처 검증 후 검토한다. |

## 승인 대기 개발 후보

P1은 다음 Presentation 계획에서 우선 검토, P2는 선행 결과 및 2차 플레이테스트 후, P3는 범위 확인 후 선택이다. 아래 완료 조건과 화면·사운드·기술 방향은 제안이다. 기존 판정과 조작, 역할/타이밍, Collider 및 동기화 권한을 그대로 사용한다.

| ID와 우선순위 | 목표·근거·범위와 에셋 | 선행·Domain·경로·복잡도 | 관찰 가능한 완료와 검증 |
| --- | --- | --- | --- |
| V3-PRES-UI · P1 | 공통 UI 스타일과 메인·로비. 기본 IMGUI와 연결 문구의 계층·상태를 개선한다. 기존 월드 배경·문양 참고, 승인 로고/폰트/아이콘 필요 여부 결정. 제목·시작·종료·코드 복사·오류·준비·재입장 표기를 포함한다. | UI · Standard · 중간. 브랜드·언어 결정. | 문구와 다음 행동을 찾을 수 있고 승인 해상도에서 잘림이 없다. D3D11 PC UI, Host/Client 생성/참가·오류·이탈/재입장 및 커서 입력 확인. |
| V3-PRES-GUIDANCE · P1 | HUD와 상황별 온보딩을 합친다. 현재 텍스트·월드 라벨·링·뗏목 피해·퍼즐 상태를 사용해 목표/역할/경고 우선순위를 정리한다. 분리된 튜토리얼 맵은 선택 범위 밖이다. | UI·Gameplay · Standard · 중간~높음. UI 기준과 2차 관찰. | 첫 플레이 2인이 외부 설명 없이 위 구간의 다음 행동·실패 이유·결승 요구를 설명할 수 있다. 짧은 구간별 초보자 관찰과 양쪽 역할/HUD, 긴 문구 및 해상도 확인. |
| V3-PRES-AUDIO · P1 | 무음 상태에서 믹서·Master/Music/SFX·저장·2D/3D·거리 감쇠 및 승인 이벤트 연결을 만들고 핵심 큐를 먼저 제공한다. 우선 함정 전조, 피해, 퍼즐 승인, 봉인/보석, 결과. 신규/팀 제공 음원 필요. | Gameplay·UI · Standard · 중간~높음. 음향 방향·사용권 확인. 설정 UI는 아래와 공유. | 승인 이벤트가 각 피어에서 의도한 횟수로 재생되고 재입장·상태 복원 시 이전 큐가 울리지 않는다. 양쪽 가청 거리, 소음 우선순위, 음소거/저장·복원 및 Listener 1개를 확인한다. |
| V3-PRES-MATCH · P1 | 시작·공동 봉인·첫 픽업·복로·결승을 하나의 흐름으로 연결하고 결과 화면을 제공한다. 기존 제단/보석/경계와 Victory/Defeat 후보를 사용한다. 추가 타격·잡기 자세는 범위에서 제외한다. | UI·Gameplay·Rendering · Standard · 높음. UI, Audio, 연출 강도·결과 동선 결정. | 봉인 해제, 첫 픽업, 이후 탈취가 구분되고 양쪽이 복로 목표와 각자 승/패·정지 시간을 확인한다. 두 피어의 상태 전환, 늦은 재접속, 결과에서 방 나가기, 이동·입력·기존 판정 회귀 확인. |
| V3-PRES-SETTINGS · P2 | 감도·오디오·해상도/화면 모드·그래픽 선택과 저장, Esc의 설정·종료 동선을 통합한다. 기존 카메라 값과 URP 품질 설정 사용. | UI·Rendering · Standard · 중간. UI 및 Audio의 볼륨 계약, 지원 옵션 결정. | 재실행 후 값 복원, 적용 취소/되돌리기, 커서와 방 상태 정상. D3D11 창/전체화면, 두 피어 상태와 Esc 조작 확인. |
| V3-PRES-WORLD-AUDIO · P2 | Audio 기반 위에 나머지 플레이 SFX, 환경 루프, 메인·로비·왕로·복로 음악을 추가한다. 소재는 출처 확인 후 확보한다. | Gameplay · Standard · 중간~높음. Audio와 Match 큐 계약. | 구간·단계 전환에 맞는 루프/음악이 겹쳐 쌓이지 않고 중요한 전조가 들린다. 양쪽 이동·정박·복구·구역 경계 샘플과 가청성 평가. |
| V3-PRES-MOTION-FX · P3 | 2차에서 확인된 공백에 한해 발/착지·목재·노와 물보라·복구 표현, 손 배치·낙하/착지 자세를 보완한다. 기존 클립/리그·강 재질과 팀 파생 VFX 우선. 전용 클립은 필요가 입증되면 제작한다. | Gameplay·Rendering · Standard · 중간~높음. Guidance, 2차 결과 및 Match 표현 규칙. | 두 피어에서 조작·상태와 표현이 맞고 효과가 지형·위험 표시를 가리지 않는다. 보행/달리기·착지·운반·탑승 D3D11 샘플, 기존 PvP와 전조에 대한 직접 회귀. |

권장 순서는 UI 기준과 Audio 기반을 준비하고, 2차 관찰을 HUD·온보딩에 반영하면서 경기 전환·결과를 설계한 뒤 설정과 환경음을 연결하고 남은 동작·효과만 보완하는 것이다. 기존 붕괴 전조, 착지 예측 링, 망토·PvP 대응 자세, 뗏목 피격 강조, 성채·하수도 승인 표시와 지하 함정 전조를 신규 제작 항목으로 올리지 않는다.

## 감사 당시의 디자인 질문과 남은 확인

- UI의 한/영 지원, 로고와 타이포·메뉴 배경의 방향, 경기 중 상시 시간 및 상대에게 공개할 정보의 범위를 정한다.
- 처음 보는 사람에게 입력과 규칙을 공개하는 시점, 안내 건너뛰기·재표시, 복로에서 숨겨야 할 정보와 결과 화면의 로비/재경기 선택을 정한다. 재경기 또는 온라인 일시정지는 승인 시 세션/규칙 범위를 먼저 검토한다.
- 봉인과 첫 픽업의 연출 강도, 카메라·입력 유지, 색/빛/사운드 강도 및 감소 옵션, 음악 분위기와 팀 제작·외부 음원 범위를 정한다. 분리 해제·픽업·탈취에 동일한 큐를 쓰면 세 상태의 구분이 어려워질 수 있다.
- 별도 UI·레벨 상세 기획과 외부 자산 사용권은 자료 확보 전 UNCONFIRMED. 실제 발 미끄러짐·네트워크 시점차, 해상도별 잘림, 온보딩 및 경쟁 전환 전달력은 사람 검토가 남았다. 별도 PC 두 명의 2차 결과는 대기 중이다.
- 이번 작업의 검증 범위는 문서 L0다. 새 Unity 실행, 빌드, Relay, 캡처는 NOT_RUN. 전체 경기·전체 월드·E2E Relay는 사용자가 시작하는 최종 통합까지 DEFERRED.
