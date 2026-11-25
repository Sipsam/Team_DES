# 양날의 검 (Twin Edge)

두 플레이어가 함께 유적에 도달한 뒤, 단 하나의 보석을 들고 출발점으로 돌아오는 Windows용 온라인 3D 협동 퍼즐·경쟁 게임입니다.

## 핵심 플레이

- 왕로에서는 회전 막대로 산악을 건너고, 목재로 다리를 완성한 뒤 함께 뗏목을 젓습니다.
- 성채와 지하에서는 서로 다른 문양과 숫자를 전달하고, 상대에게 발판을 열어 줍니다.
- 복로에서는 공동 봉인을 푼 뒤 보석을 두고 싸웁니다. 보석을 가진 채 산길 상단 입구의 결승 구역에 도착하면 승리합니다.

## 사용 기술

- Unity **6000.3.27f1**
- Universal Render Pipeline (URP)
- Netcode for GameObjects (NGO)와 Unity Transport
- Unity Authentication 및 Unity Relay
- Unity Input System

## 개발 시작

```powershell
git clone https://github.com/Sipsam/Team_DES.git
cd Team_DES
```

1. Unity Hub에서 Unity 6000.3.27f1과 Windows Player 빌드 지원을 설치합니다.
2. Hub에 저장소 루트를 추가한 뒤 패키지 복원과 에셋 임포트가 끝날 때까지 기다립니다.
3. `Assets/Game/Gameplay/Scenes/V3Gameplay.unity`를 열어 Play합니다.
4. 지형만 편집하려면 `Assets/Game/Level/Scenes/LevelBlockoutV3.unity`를 엽니다.
5. Relay 개발 및 테스트에는 프로젝트에 연결된 팀 Unity Services 환경의 Authentication과 Relay를 사용합니다.

## Windows Playtest

1. Unity에서 **Tools > Twin Edge > Gameplay > Build V3 Windows Playtest Player**를 선택합니다.
2. `Builds/V3Playtest/`의 런타임 파일을 폴더 단위로 전달합니다.
3. 두 플레이어는 같은 빌드를 각자 풀고 `V3Gameplay.exe`를 실행합니다.
4. Host는 **Create room**으로 방을 만들고 **Copy**로 방 코드를 공유합니다.
5. Client는 코드를 붙여 넣고 **Join room**을 선택합니다.
6. 두 플레이어의 씬, 스폰, 카메라 초기화가 끝나면 자동으로 경기가 시작됩니다. 산길을 따라 시작 봉우리로 올라갑니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| 마우스 / WASD | 시점 / 이동 |
| 왼쪽 Shift / Space | 달리기 / 점프 |
| E | 막대·뗏목·퍼즐·보석 등 상호작용 |
| 왼쪽 클릭 / F | 공격 / 잡기·던지기·저항 |
| R | 복로 산악 발사 충전 및 발사 |
| Esc | 마우스 해제, 활성 상호작용 취소, 로비 메뉴 |

## 주요 폴더

| 경로 | 내용 |
| --- | --- |
| `Assets/Game/Gameplay/` | V3 진입 씬, 런타임, 프리팹, 빌드 도구, 회귀 테스트 |
| `Assets/Game/Level/` | V3 지형 씬, 데이터, 생성기와 검증 도구 |
| `Assets/_Project/` | 플레이어, 세션, 상태, 리스폰 등 V3 공용 코드 |
| `Packages/`, `ProjectSettings/` | 패키지 버전과 Unity 설정 |
| `Tools/Playtest/` | 두 프로세스 Relay 테스트 실행기 |

## 문서

- [통합 기획서](Docs/GameDesign/TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf)
- [게임 구조](Docs/Architecture/GameArchitecture.md)
- [게임플레이 시스템](Docs/Architecture/GameplaySystems.md)
- [멀티플레이](Docs/Architecture/Multiplayer.md)
- [플레이테스트 가이드](Docs/Playtest/PlaytestGuide.md)
- [검증 요약](Docs/Playtest/ValidationSummary.md)

## 2026-11-25 플레이테스트 변경 사항

Windows에서 Unity **6000.3.27f1**로 프로젝트를 연 뒤, Play Mode를 끈 상태에서 아래 메뉴를 사용합니다.

- **솔로 플레이** — **Tools → Twin Edge → Play Solo**를 선택합니다. 한 창에서 `F6` 또는 **Switch player** 버튼으로 조작할 캐릭터를 바꿉니다. 두 캐릭터가 각각 목재를 잡거나 공용 뗏목에 타면, 한쪽 입력으로 함께 운반하거나 노를 저을 수 있습니다.
- **자동 재생** — **Tools → Twin Edge → Run Full Replay**를 선택합니다. 두 캐릭터가 정해진 경로와 행동에 따라 플레이하고, 한 창에서 경기를 볼 수 있습니다. 두 모드는 같은 PC에서 연결하므로 방 코드가 필요 없습니다.
- **관전 카메라** — 우클릭을 누른 채 드래그해 회전하고, 휠로 확대·축소합니다. 휠 클릭으로 기본 시점에 돌아갑니다.

[상세 실행 방법과 결과 저장 위치](Docs/Playtest/PlaytestGuide.md#2026-11-25-플레이테스트-변경-사항)
