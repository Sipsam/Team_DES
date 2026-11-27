# Twin Edge V3 오디오

[프레젠테이션 설계](../GameDesign/PresentationDesign.md)의 D와 F 정책을 Unity 오디오에 적용한다. 런타임은 일회성 큐와 음악·환경음 레이어를 재생한다.

## 재생과 믹서

- V3 부트스트랩에 `AudioService` 하나를 두고, 같은 씬의 16개 AudioSource를 재사용한다. 세션 종료에서 음원과 중복 제한 상태를 해제한다. 씬이 내려가면 서비스와 소스도 제거된다.
- `AudioService.Play(AudioCueId, position)`은 카탈로그의 라우팅과 피치 범위를 사용한다. 위치가 필요한 큐에는 실제 월드 좌표를 전달한다. 3D 큐는 모노와 선형 감쇠를 사용하고 최대 거리 바깥에서는 새 소스를 시작하지 않는다. UI, 준비, 체크포인트, 복구와 결과는 2D다.
- 큐별 재생 간격과 동시 수를 제한한다. 풀이 가득 차면 우선순위 숫자가 더 큰 소스만 교체한다. 함정·다리 전조와 결과는 낮은 우선순위의 큐가 선점할 수 없다. 다리 큐의 간격 제한은 한 번에 가까운 여러 발판이 붕괴하는 구간의 겹침을 줄인다.
- 믹서는 Master 아래에 Music, Gameplay SFX, Ambience, UI, Character Voice를 둔다. 큐별 이득과 함께 Gameplay SFX는 −7 dB, UI는 −6 dB의 기본 버스 여유를 둔다. Music −16 dB, Ambience −18 dB, Character Voice −9 dB의 내부 여유를 둔다.
- 메뉴 카메라가 월드를 그리면 해당 카메라의 Listener를 사용하고, 그 외에는 저장된 경기 카메라의 Listener를 사용한다. 대기실 미리보기에는 Listener가 없다. 기존 화면 전환이 카메라와 Listener를 함께 전환한다.

## Master 저장 계약

`AudioService.MasterPercent`는 0–100 정수 값을 반환하며 기본은 100이다. `SetMasterPercent(int)`는 범위를 제한하고 로컬 PlayerPrefs 키 `TwinEdge.Audio.MasterPercent`에 즉시 저장한다. 양수는 믹서 파라미터 `MasterVolumeDb`에 `20 × log10(percent / 100)`로 적용한다. 0은 −80 dB와 Listener 출력 0을 함께 적용한다. Master 아래의 기존 및 이후 소스에 이 값이 적용되며, 서비스의 Start에서 재실행 후 복원한다. V3-PRES-08은 이 API를 사용해 슬라이더에 연결한다.

## 이벤트 소유권

| 소리 | 트리거 |
| --- | --- |
| 메뉴 버튼, 확인, 오류, 코드 복사 | uGUI 로컬 입력과 생성·참여 결과, 클립보드 쓰기. 준비 및 시작 버튼은 해당 승인 큐를 사용한다. |
| 준비·취소, 시작, 승패 | Client의 복제된 준비 변화, Host의 승인된 Preparing 진입, 로컬 플레이어 기준 서버 승자 변화. NetworkVariable 초기 동기화는 재생을 시작하지 않는다. |
| 공격·타격·잡기·던지기·저항 | 복제된 공격 시작과 실제 접촉 타임스탬프, 캐리와 던지기 전환. 저항 성공은 다른 캐리 해제와 구분해 서버가 한 번 알린다. |
| 함정 | 시각 효과가 사용하는 성채 활성화 시각과 지하 주기에서 전조와 발동 경계를 한 번 소비한다. 접촉은 기존 피해 또는 넉백 승인 경로에서 알린다. |
| 다리·뗏목 | 새 경고·붕괴 목록 항목과 뗏목 피해 타임스탬프 및 파손·회복 전환. 다리 3칸·0.65초 조건과 뗏목 피해 쿨다운은 기존 값이다. |
| 성채·하수도·지하 | 승인 또는 오답은 해당 입력 피어에만 전송한다. 음원은 문양에 따라 달라지지 않는다. 공개된 성채 완료 및 문 개방을 복제 상태에서 감지하고 각 레인에서 열린 문의 위치를 사용한다. 협동 스위치와 발판은 승인 경로에서 스위치와 첫 활성 발판의 위치를 사용한다. 하수도의 오답은 기존과 같이 진행에 영향을 주지 않는다. |
| 봉인·보석 | 봉인은 지하의 Unsealed 전환에 연결한다. 보석 획득·드롭·탈취·재배치는 기존 HUD가 읽는 복제 상태와 월드 위치를 사용한다. 첫 로컬 관측은 기준값으로 사용한다. |
| 체크포인트·복구 | HUD와 같은 소유 플레이어의 복제값을 사용한다. 복구와 동시에 체크포인트가 바뀌면 복구음 하나를 낸다. |

일시적인 서버 사건에는 하나의 서버 전용 `WorldCueRpc`를 공유한다. 위치와 서버 시각을 사용하고 오래된 이벤트는 버린다. 공개 이벤트는 Host 포함 클라이언트 경로에서 한 번 처리한다. 연결된 로컬 플레이어가 준비된 세션만 월드 큐를 소비한다. 오디오는 게임플레이 판정을 변경하지 않는다.

## 큐 추가와 음원 배포

1. `AudioCueId` 끝에 새 항목을 추가하고 [CoreAudioCues](../../Assets/Game/Audio/Resources/CoreAudioCues.asset)에 클립과 믹서 버스, 볼륨, 피치, 공간 설정과 동시 제한을 지정한다.
2. 짧은 효과음은 미리 로드하는 PCM이다. 월드용은 32 kHz 모노, 기존 2D 전용 클립은 원래 스테레오 샘플레이트를 사용한다. 장시간 트랙은 Vorbis 스트리밍을 사용한다.
3. [AudioCredits](../../Assets/Game/Audio/AudioCredits.txt)에 원본 경로, 사용처와 라이선스를 기록한다. Windows 빌더는 고지와 [CC0 전문](../../Assets/Game/Audio/CC0-1.0.txt)을 플레이어와 함께 복사한다.
4. [Playtest Guide](../Playtest/PlaytestGuide.md#v3-audio-foundation-and-core-sfx)의 신호 및 Relay 검증을 실행한다.

## 코스 음악과 환경음

[AudioService](../../Assets/Game/Audio/Runtime/AudioService.cs)는 [V3CourseAudio](../../Assets/Game/Audio/Resources/V3CourseAudio.asset)를 읽는다. [CourseAudio](../../Assets/Game/Audio/Runtime/CourseAudio.cs)는 연결, 로컬 플레이어, 서버 Phase·승자를 우선 적용한다. 구역은 HUD에서 이미 사용하는 `CourseMapData.At`의 높이와 경계를 읽고, 연속 0.65초 동안 관측한 구역을 확정한다. 새 로컬 플레이어로 진입하면 즉시 해당 위치를 읽는다. 이어서 음악과 배경음의 1.15초 및 0.85초 페이드를 각각 갱신한다.

| 로컬 구역 또는 상태 | 왕로와 SealPending 음악 | Return 음악 | 환경음 |
| --- | --- | --- | --- |
| 메뉴, 방 접속, 대기실, Preparing | Ancient Mysteries | — | — |
| 산악·결승 | Sunset Plains | Hope | 산악 바람 |
| 다리·협곡 | Sunset Walk | Determined Pursuit | 협곡 바람 |
| 강 | The Old Tower Inn | Hope | 강 자연음 |
| 성채 외부 | The Bard’s Tale | Determined Pursuit | 새와 외부 자연음 |
| 성채 내부 | The Bard’s Tale | Determined Pursuit | 실내 바람과 잔향 |
| 계단 | Whispers in the Fog | Determined Pursuit | 실내 바람과 잔향 |
| 지하 | Whispers in the Fog | Determined Pursuit | 동굴 바람과 물방울 |
| Finished | 승리 Sunset Plains, 패배 Whispers in the Fog | — | — |

- [LoopPlayback](../../Assets/Game/Audio/Runtime/LoopPlayback.cs)은 음악과 배경음 레이어마다 두 소스를 재사용한다. 같은 클립으로의 요청은 재생 위치를 유지한다. 두 슬롯이 사용 중인 상태에서 세 번째 요청이 오면 더 큰 쪽은 유지하고 다른 쪽을 비운 뒤 최신 목표로 전환한다. 강물, 비, 용암, 화염과 로컬 충전의 다섯 소스를 더해 지속음은 총 아홉 슬롯으로 제한한다. 기존 16개의 일회성 소스와 분리된다.
- 강물과 용암은 저장된 수면과 용암 경계에서 Listener에 가까운 표면 지점을 사용한다. 용암의 48m 거리 감쇠는 용암과 그 위의 지하 발판 사이 높이 차를 포함한다. 비는 실제 복로 강의 비 구역에서만 재생한다. 화염은 지하의 활성 화염 중 가장 가까운 오브젝트를 사용한다. 각 지점에서 선형 거리 감쇠와 재생 거리 제한을 적용한다.
- `AudioService.StopSession()`은 경기 음악, 환경음, 충전음, 기존 큐와 간격 상태를 비운다. 대기실과 메뉴가 공유하는 재생 중 테마는 세션 간 유지한다. 부트스트랩이 비활성화될 때는 `StopSession(keepMenuMusic: false)`로 메뉴 소스도 정지한다.
- V3-PRES-06 연결 계약은 `GameStateManager.FoundationPhaseChanged`와 현재 `Phase`다. CourseAudio는 현재 Phase를 읽고 Outbound, 첫 보석 획득에 따른 Return 및 서버 결과에 따른 Finished에 맞는 테마를 선택한다. 카운트다운 중에는 메뉴 음악을 유지한다. 별도의 음악 시작 RPC나 재생 재시도는 필요 없다. V3-PRES-08은 기존 Master API를 사용한다.

## 이동과 추가 효과음

- 보행과 달리기는 약 75ms 간격으로 실제 수평 변위를 읽고 이동 거리에 맞춰 발걸음을 재생한다. 소유자는 CharacterController의 접지 상태, 원격 캐릭터는 기존 복제 모션을 이용한다. 발걸음 재생은 막대, 뗏목, 잡힌 상태, 넉백, 발사 이동 중 제한한다. 리스폰과 큰 순간이동은 기준점을 재설정한다. 일반 착지음은 공중 표본이 0.22초 이상 이어지고 정점에서 0.42m 넘게 하강했을 때 재생한다.
- 공격 시작, 실제 타격 반응과 던지기 타임스탬프로 음성을 재생한다. 캐릭터별 최소 간격과 카탈로그 피치 범위를 사용한다.
- 막대 잡기·교대·접촉, 목재 들기·설치와 뗏목 탑승은 기존 서버 승인과 월드 큐에 연결한다. 목재 운반음은 운반자 변위에 맞춘다. 노젓기는 복제된 좌석, 구동 입력과 실제 선체 속도를 사용한다. 발사 충전·발사·착지는 복제된 발사 상태를 사용하며, 로컬 충전 루프는 입력 취소와 승인 상태 종료에 맞춰 감쇠한다.
- [AudioCredits](../../Assets/Game/Audio/AudioCredits.txt)는 신규 음원별 저자, 원본, 확인일, 사용 위치 및 가공 내역을 기록한다. CC0 전문과 함께 Windows 빌드에 동봉한다.

### 경기 시작과 전환 큐

카운트다운은 기존 UI Click과 Confirm을 사용한다. 서버 시각에서 현재 박자를 결정하며 지난 박자를 보충 재생하지 않는다. Host의 Start 승인음과 지하 봉인음은 기존 이벤트를 사용한다. 첫 획득은 서버 승인 세계 큐에서 기존 GemPickup을 한 번 전달하고 이후 보석 상태 알림은 기존 획득·탈취·드롭 큐를 사용한다. 봉인 후에는 왕로 음악을, 재획득 후에는 진행 중인 복로 음악을 유지한다. 연출음에 공용 mixer와 Master 및 세션 종료 정리를 적용한다.

## 결과와 재경기

확정 결과는 기존 Winner 변경에서 로컬 Win 또는 Lose 효과음을 한 번 재생한다. 결과 화면 자체는 승패음을 재생하지 않는다. Finished 동기화에는 새 Winner 이벤트가 없으며, CourseAudio는 복원된 서버 슬롯을 읽어 승리의 Sunset Plains 또는 패배의 Whispers in the Fog를 선택한다. 재경기 씬 전환과 방 나가기는 이전 소스와 반복음을 정리하고 대기실·메인 메뉴에서 기존 메뉴 음악을 재생한다. 새 서비스가 기존 Master 설정을 복원한다.
