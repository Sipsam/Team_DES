# V3 검증 요약

## 전체 경기 자동 완주 기록

Unity 6000.3.27f1 Windows Development Player 두 프로세스가 실제 Relay를 사용했다. 두 회차 모두 정상 스폰에서 출발한 뒤 공동 봉인·보석 픽업·복로·결승을 통과했다.

| 회차 / Build | 코드 기준 | 결과 |
| --- | --- | --- |
| Run16 / FullMatchBuild16 | 전체 경기 통합 기준 ce0760012b72014167853d7a96a3fa3ed927fae4. Build15 Player 데이터에 Development 스크립트 빌드 적용. | Finished, 승자 0, Host Victory / Client Defeat, 2,545.908초, 리스폰 0/0, 종료 0/0 |
| PlaytestFinalFull / V3PlaytestDevelopmentFinal | 플레이테스트 개선 기준 c4b6e942d7102dc532dab0c3d87c2ebd5bccf61a. Development Windows 빌드로 재생. | Finished, 승자 0, Host Victory / Client Defeat, 2,551.247초, 리스폰 0/0, 종료 0/0 |

| 과거 빌드 | Assembly-CSharp.dll SHA-256 |
| --- | --- |
| FullMatchBuild16 | 92b71e149c8edb225cc10b86f9f6d01ad1c38f2c4fbba1e011708d088d0222dc |
| V3PlaytestDevelopmentFinal | 415ac2d79348726bc417a0101d837ad1d68885589412c3e70890ac77bd2115cb |
| V3Playtest (일반 Windows) | 6a4bd814f1bb3282efcf8896dc34cdfde30e5d9c5aca350bc2f9773d830cb40f |

코드 기준은 검증된 구현과 연결되는 Git 버전이다. 과거 두 바이너리의 바이트는 위 해시로 식별한다. Build16의 소스 해시 목록 16개 중 14개가 줄바꿈 차이를 허용했을 때 ce07600과 일치한다. 두 파일은 다르므로 해당 커밋의 체크아웃만으로 같은 DLL을 재현한다고 볼 수 없다. 소스 해시 목록, 두 Development Player 및 관련 원본 로그는 로컬 외부 백업에 보존했다. 동작을 재현하려면 [빌드 및 Relay 재생 절차](PlaytestGuide.md#development-build-and-automated-relay-checks)를 사용한다. 역사적 Build16의 스크립트 전용 갱신에는 먼저 Build15 Player 데이터가 필요하다.

## 주요 회귀와 결함 기록

- Finish16은 보석 없는 Client의 선행 진입을 거부하고 Host의 실제 진입을 승인했다. 양쪽 Finished와 454.441초가 일치했다.
- 최종 Development 회귀에서 Foundation, 막대·다리 연결 끊김, 강 복구, 성채 재접속, 산악 R 입력, PvP 및 보석 수명주기를 확인했다.
- 초기 지연·손실 검사에서 Host의 원격 위치 복제 검사가 한 번 실패했다. 빌드와 플레이를 분리한 기준/최종 지연·손실 회차는 통과했다. 초기 실패 원인은 미확정이다.
- 일반 Player의 Windows 데스크톱 시험은 실제 Relay 생성·참가·시작·Client 재입장·Host 종료·재생성, 마우스 캡처와 Escape를 확인했다. 개발 전체 재생이 일반 Player에서 빠지는 것도 검사했다.
- 완주 과정에서 발견한 산악 피어 진행 장벽, Big summit 체크포인트 이름 비교, 강 재생 초기화, 복로 단계 전환 및 결승 후 종료를 보완했다. 플레이테스트 개선에는 E 입력 소비, 개발 패널 입력, 카메라 충돌 검사 버퍼, HUD 착지 판정 간격이 포함됐다.

## 성능 표본

Windows 11, Ryzen 7 4700U/Radeon, D3D11 1024×720, 같은 PC의 두 Development Player가 실제 Relay로 통신했다. GC는 누적 할당, RTT는 약 5초 간격 표본이다.

| 회차 | 평균 FPS Host / Client | 누적 GC MiB Host / Client | RTT p50 / p95 / 최대, Host와 Client |
| --- | --- | --- | --- |
| Run16 | 59.6 / 59.6 | 1,000.71 / 149.44 | 150/184/2792 ms, 150/183/2791 ms |
| PlaytestFinalFull | 59.1 / 59.4 | 993.64 / 144.94 | 151/184/881 ms, 150/184/757 ms |

Run16 왕로 산악은 약 905초와 Host 누적 GC 707.67 MiB를 기록했다. 최대 프레임 GC는 Run16 두 피어 1.76 MiB, 최종 회차 Host 0.43 MiB / Client 0.32 MiB였다. 캡처 빈도와 Relay 표본 조건이 다르므로 RTT 및 FPS 변화의 원인은 확정할 수 없다. CPU/GPU·물리 마커 및 RPC/NetworkVariable 바이트의 완주 전체 수집은 범위 밖이었다.

## 프로젝트 정리 후 재검증

`c4b6e94`에 정리 변경을 적용한 별도 새 클론을 Unity 6000.3.27f1로 임포트했다. C# 컴파일, Build Settings의 V3 두 씬 로드, 저장 씬·프리팹 GUID 대조와 애니메이터 확인이 통과했다. V3 씬의 누락 스크립트는 0개였다. URP Volume/Renderer의 기존 미해결 GUID 11개는 정리 전후 동일했다. 새 일반 빌드 패킹에 KayKit 던전 텍스처가 포함됐다.

| 정리 후 재생성 | Assembly-CSharp.dll SHA-256 |
| --- | --- |
| 일반 Windows Player | 65a85e4592bff2d4738cc5ed51ea76c9605d952b6e84bd9d330e1e96004eaf37 |
| Development Windows Player | 1067980089b932e0d05fa0600dea6616fc71a930004c1f0e9e2b0933254035b2 |

- 보존한 일반 Player와 재생성한 일반 Player에서 각각 실제 Relay 방 생성, 참가, 시작, Client 재입장, Host 종료 및 새 방을 검증했다. 실제 Windows 입력으로 마우스 캡처와 해제를 확인했다.
- 새 Development Player는 Foundation, 보석 수명주기, 성채·지하, PvP·결승, 복로 R 입력을 검증했다. 성채·지하에서 두 피어가 퍼즐, 공동 봉인, 보석 E, 함정 복로와 하수로 재접속을 통과했다.
- PvP 첫 회차는 Client 던지기 이동 거리 검사에서 중단됐다. 같은 바이너리를 단독으로 재실행해 타격·잡기·저항·던지기, 보석 탈취, 보석 없는 플레이어 거부와 Client 승리를 양쪽에서 확인했다. 초기 실패 원인은 미확정이다.
- 왕로 연속 막대 계획과 복로 9구간 `Route.json`을 현재 에셋에서 다시 생성했다. 42분 전체 경기를 정리 후 다시 실행하는 대신 위의 구간 회귀를 실행했다. 이 절의 검증은 동일 PC의 두 프로세스에서 수행했다.

## 2026-11-22 산악 재개 상태

- `V3MountainCandidate9`에서 산악 32m 막대 Relay를 재검증했다. Host와 Client의 종료 코드가 모두 0이고, 양쪽이 `GameplayCheckpoint_Big summit`에 도착했으며 역할 교대 9회를 기록했다.
- 같은 Candidate 9에서 전체 경기 회차는 산악을 통과한 뒤 다리 공급 지점에서 중단됐다. 별도 Candidate 9 다리 회차도 양쪽 모두 두 입력 목재 운반의 12초 제한을 통과하지 못했다.
- 보존한 `V3Development` Assembly-CSharp.dll 해시 `0FD3C2C7596D0D69C768823F46FAFBF61810DB0402C5BF3B5209B469106CD8C0`로 다리 격리 회귀를 다시 실행했으며, 양쪽 종료 코드 0과 외곽/복로 붕괴 다리 통과를 확인했다.
- Candidate 9 assembly 해시는 `440B8BD50CAFEF9FC642875C0F21CF5034F2920EF352E63E8488ECC17B402FD5`다. 기존 정상 바이너리와 후보 소스의 다리 동작 불일치가 확인되었으며, 새 Development Candidate 빌드 생성은 execute-method 호출이 종료 코드 1로 차단돼 완료하지 못했다.

## 2026-11-22 V3 산악과 월드 최종 검증

- Windows D3D11에서 두 Development Player가 실제 Relay로 연결됐다. 정상 스폰과 자동 시작부터 동쪽 산악 경로, 다리 목재 13개, 협곡과 공동 뗏목, 성채, 지하 공동 봉인과 실제 보석 획득, 개인 뗏목, 붕괴 다리, 개인 발사 9회씩, 결승까지 한 세션으로 완료했다.
- 양쪽 Finished, 종료 코드 0/0, 리스폰 0/0이다. 두 피어의 승자와 보석 소유자는 Host(ID 0)이고 서버 타이머는 2687.190408732537초다. 정상 경로에는 순간이동과 강제 Phase 전환이 없다. `V3CastleReplayFull2`의 Development Assembly-CSharp.dll SHA-256은 `7659744372651E80303CFE102C802240A69ADCA93092D773CBC411730F7E417B`다.
- 성채 보행 격리는 Host 33°와 Client 137°로 정문, 네 퍼즐 방, 키패드, 지하 문턱을 통과했다. 성채·지하 격리와 최종 후보의 다리·결승 및 협곡·하부 부두 격리도 각각 종료 코드 0/0을 기록했다. 이전의 Tuned 연속 계획 6개와 동쪽→중앙→동쪽 Relay 증거를 재사용했다.
- Unity 6000.3.27f1에서 두 V3 씬과 일반·Development Windows 빌드를 검증했다. 두 씬의 누락 스크립트는 0개다. 이전 감사와 같은 URP Volume/Renderer GUID 11개가 남아 있다. 일반 Player의 Assembly-CSharp.dll SHA-256은 `0523FA5FD29A216E9141834C3863BB4BA8FAE054645AE669D5C7F544FCD7FCDD`다.
- 협곡·강 렌더 표면과 기존 충돌 메시의 이음부를 대조했다. D3D11에서 다리 아래 깊이, 강변 암벽, 성채 표식과 석재, 지하 발광 용암을 확인했다. 강의 플레이 구간 고저차는 10m다.

## 승인된 DeepGullies / FinalPolish 산악

- 승인 결과의 소스 기준은 `6498cd4`다. 원격 `main`과 일치하는 깨끗한 체크아웃에서 Editor D3D11 공식 캡처 14장(1800×1200)을 푸시 이후 게시했다. 로컬 `Screenshots/`의 파일명은 해당 커밋을 가리킨다.
- DeepGullies 저장 메시의 구조, 렌더 메시와 MeshCollider 참조 일치 및 첫 막대 교차 구간의 보행 분리를 확인했다. 산악 정적 검사에서 29개 지점의 방향 연결 94개와 착지 지지 표본을 대조했고 연속 계획 4개가 통과했다.
- 앞선 Windows 격리 Relay에서는 양쪽이 정상 스폰에서 서쪽 선반을 경유해 Big Summit에 도착했다. 별도 복로 격리에서는 개인 발사를, 산기슭 격리에서는 스폰 카메라와 Trail Entrance까지의 이동을 확인했다. Revision 2에서는 기록된 Relay 좌표를 사용해 양쪽 각각 막대 운반 109구간과 지표 표본 12,962개를 Collider에서 대조했다. 이 Windows 실행 및 기록 대조는 앞선 후보의 검증이다.
- 승인된 FinalPolish는 DeepGullies 메시와 두 저장 씬을 그대로 사용한다. Unity D3D11에서 Forward+ 조명, 셰이더 컴파일, 재질 및 텍스처 참조, 씬 재개방, 산악 서브메시와 재질 슬롯 각 1개를 확인했다. 비산악 대조 프레임은 픽셀 단위로 일치했다.
- 최종 통합에서 최신 DeepGullies 메시와 FinalPolish 재질이 함께 패킹된 Windows Player의 지형, 충돌 및 D3D11 화면을 확인해야 한다. 산악 최종화에서는 기존 격리 결과를 사용했다.
- 선택적 로컬 증거는 `Docs/Development/Level/V3/MountainRedesign/Verification.md`와 그 아래 `RidgelineReview/DeepGullies/FinalPolish/Verification.md`에 있다.

## V3 플레이테스트 피드백 1 · 체크포인트와 복구

- 11월 22일 로컬 `V3Playtest`의 실행 파일, 어셈블리, 패킹된 씬을 포함한 7개 파일 해시가 당시 일반 Player 빌드 기록과 일치했다. `3805710`은 추정 소스이며, 이번 변경은 원격과 일치한 최신 `main` `97611f3`에서 시작했다.
- Unity 6000.3.27f1 Windows Development Player에서 같은 PC의 두 프로세스로 격리 Relay를 실행했다. 왕로 막대의 실제 충돌과 복로 다리의 발판 붕괴 및 낙하로 새 복구 위치를 검증했다. 성채 지하문은 왕로와 복로 양쪽에서 체크포인트 복제 및 슬롯별 복구를 확인했다.
- 실제 공동 봉인과 보석 획득으로 복로 제단 복구 지점을 설정했다. 지하문 복구 후에도 복로, 성채 관문 및 열린 봉인이 유지됐다. 성채 함정의 두 번째 피격은 복로 지하문 복구를 사용했다. 기존 Big summit과 뗏목 복구, 보석의 구간별 재배치·재획득 회귀도 통과했다.
- 격리 Relay 6개 사례가 양쪽 종료 코드 0으로 완료됐다. 성채 재접속 후 양쪽 출구 통과도 확인했다. 일부 격리 테스트는 구간 배치, 단계 설정 또는 사망 호출을 검증용으로 사용했다. 선택적 로컬 기록은 `Docs/Development/CheckpointRecovery/V1/Verification.md`에 있다.

## 남은 확인

최신 소스의 전체 월드 회귀, 전체 자동 경기 및 E2E Relay, DeepGullies와 FinalPolish를 함께 패킹한 Windows Player, 최종 배포 빌드는 사용자가 시작하는 최종 통합 단계로 보류했다. 이전 전체 경기의 결과는 해당 후보 빌드의 기록이다.

사람 2인의 별도 PC 전체 경기, 실제 조작과 밸런스, 장시간 네트워크 임페어먼트와 실기 성능은 개별 검증이 필요하다. 위 타이머는 한 PC에서 동작한 두 자동화 피어의 결과다. 상세 원본은 선택적으로 참고할 수 있는 로컬 증거다. 새 클론에서의 재현 절차는 [빌드 및 Relay 재생 절차](PlaytestGuide.md#development-build-and-automated-relay-checks)를 따른다.
