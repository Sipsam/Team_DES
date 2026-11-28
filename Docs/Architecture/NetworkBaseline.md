# V3 네트워크 트래픽 및 복구 기준

## 실제 연결과 권한

V3Gameplay 저장 씬과 설치 패키지에서 Unity 6000.3.27f1, NGO 2.13.3, UTP 2.7.4, Multiplayer Services 2.4.1을 확인했다. NGO Tick Rate는 30Hz, Fixed Timestep은 0.02초다. 저장된 로컬 UDP 주소는 기본값이다. 실제 방은 `MultiplayerSessionManager`의 Unity Authentication 및 `WithRelayNetwork()` 경로에서 생성한다. Dev 피어의 UTP 드라이버 설정에 Relay와 보안 플래그가 설정된 것을 확인했다. 씬 관리는 활성화되어 있고 NetworkObject ID 재사용은 꺼져 있다.

`NetworkSpawnCoordinator`는 서버 승인 슬롯·닉네임·스폰을 복원한다. `GameStateManager`는 두 Runtime Ready, Client Ready와 Host Start를 검사하고 서버 시계로 Phase, 카운트다운 및 연출 sequence를 복제한다. `OwnerNetworkTransform`은 소유자의 위치와 회전을 복제한다. 서버는 받은 위치를 사용해 근접 상호작용과 승자를 판정한다. 캐릭터의 스태미나·공격·잡기·망토·체크포인트와 막대·다리·뗏목·성채·지하·보석·발사의 승인 및 스냅샷은 기존 서버 계약을 따른다.

| 경계 | 검사 |
| --- | --- |
| 대기실 | 현재 슬롯과 연결 ID에서 온 Client Ready, 현재 Ready의 Host Start, 두 Runtime Ready |
| 경기 행동 | `ActionRevision`과 서버 연출 기한, 소유자·슬롯·거리·생명 ID 및 기믹 revision |
| 보석 | 보석 revision·생명 ID·거리·시야. 첫 승인만 소유자를 정하고 첫 복로 연출을 커밋한다. |
| 재입장·재경기 | 같은 경기의 복구는 기존 승인 슬롯, 새 경기는 새 GameState·플레이어·기믹 NetworkObject ID로 식별한다. |

막대·목재·뗏목의 연속 입력 코드상 최소 간격은 0.05초다. 달리기 갱신은 0.1초다. 두 값은 활성 코드 경로가 허용하는 호출 상한의 근거다. 실제 호출 및 패킷 전송 빈도는 프레임·입력·조건·채널에 따라 달라진다. [멀티플레이 계약](Multiplayer.md) · [게임플레이 계약](GameplaySystems.md)

## 계측 방법

| 관측 계층 | 계측과 한계 |
| --- | --- |
| 게임 동작과 서버 승인 | 실제 입력·상태·단계·분기 기록. 메시지 마커는 NGO 메시지 타입을 읽으므로 개별 RPC 메서드명, 로컬 Host 실행 횟수, 변수별 변경 횟수는 별도 확인이 필요하다. |
| NGO 메시지 | 설치된 `ProfilingHooks`가 등록하는 `SerializeAndEnqueue` 및 `DeserializeAndHandle` 마커의 호출 횟수와 CPU 시간을 수집한다. `NetworkVariableDeltaMessage`는 여러 변수의 변경을 담을 수 있으며, `NetworkTransformMessage`는 위치 복제 계열이다. 송수신 배치 마커도 별도로 수집한다. Multiplayer Tools는 패키지에 설치되어 있지 않아 NGO 직렬화 바이트 및 RPC별 바이트는 `NOT_MEASURED`다. |
| UTP 드라이버 | `GetStatistics` 누적 카운터의 구간 차분으로 피어별 Tx/Rx 바이트와 패킷을 계산한다. UDP 인터페이스 근처의 AnalyticsLayer에서 Relay·DTLS·UTP 제어 프레임·재전송을 포함하고 IP/UDP 헤더를 추산한다. L2, 소켓 이후 유실, Relay 서버 내부·별도 인증/세션 HTTP 및 독립 와이어 캡처는 범위 밖이다. |
| UTP 신뢰성 | `GetCurrentRtt`는 신뢰성 파이프라인의 최근 RTT(`LastRtt`)다. `GetConnectionStatistics`는 smoothed RTT도 제공한다. 신뢰성 sent/received/dropped/resent는 연결 수명의 누적값이다. drop은 시퀀스 간극이고 resend는 늦은 ACK에도 발생한다. 시뮬레이터 설정값과는 별도로 해석한다. |
| 프레임과 자원 | 프레임 간격, 지원되는 GPU 프레임 타이밍, 전체 프레임 GC, 프로브 자체 스톱워치, NetworkObject 수를 기록한다. 네트워크 전용 할당과 전체 소켓/잡 CPU는 집계에서 판별할 수 없다. |

UTP 2.7.4의 `ModifyNetworkSimulatorParameters`를 Development 드라이버에 적용한다. 보내는 쪽에 ms 단위의 지연과 지터, 0–100 단위의 손실 확률을 설정한다. A는 0/0/0, B는 50/10/0, C는 100/30/1, D는 150/50/3이다. B–D에서는 두 피어가 송신 방향에 설정하고 수신 손실은 0으로 둔다. 설치된 UTP 송신 잡은 지터에서 음수 변동을 0으로 제한한다. RTT는 피어의 송신과 상대의 응답에 영향을 받으며, 표의 지연은 각 피어의 송신 설정이다. 설정 읽기와 실측 RTT를 대조하며 연결 복구에는 원래 설정을 다시 읽어 확인한다. E는 원격 연결을 끊고 기존 방에 수동으로 재참가하는 별도 사례다. [재현 명령](../Playtest/PlaytestGuide.md#network-traffic-and-resilience-baseline)

## 검증 상태

`NET-01`의 Host 종료 사유 유실과 Client 단절 감지 전의 재전송은 별도 관측이다. [V3-NET-CLOSE-01 검증](../Playtest/ValidationSummary.md#v3-net-close-01--graceful-session-shutdown)은 기존 Profile C의 송신 요청·드라이버 수명과 Client 분류를 같은 PC UTC로 대조한다. 정상 퇴장 메시지가 반대 피어에 실제 도착한 구간과 송수신이 차단된 구간을 각각 비교한다.

측정 표와 판정은 [검증 요약](../Playtest/ValidationSummary.md)에서 관리한다. 선택 로컬 증거는 Git이 무시하는 `Docs/Development/NetworkBaseline/V1/`에 저장한다. 원시 증거의 참가 코드는 실행 종료 시 정리한다.
