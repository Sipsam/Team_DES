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

## 남은 확인

사람 2인의 별도 PC 전체 경기, 실제 조작과 밸런스, 장시간 네트워크 임페어먼트와 실기 성능은 개별 검증이 필요하다. 자동 타이머 42분대는 사람 플레이 시간의 관측값이 아니다. 상세 원본은 보조 로컬 증거이며 기록이 없는 새 클론에서도 현재 검증을 시작할 수 있다.
