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

## V3 플레이테스트 피드백 4·5 · 카메라 기준 조작과 달리기

- 원격과 일치한 `main` `7f16d56`에서 시작했다. Unity 6000.3.27f1 Windows Development Player를 빌드하고 같은 PC의 두 D3D11 프로세스로 실제 Relay를 실행했다. `cameraControls`에서 양쪽 종료 코드는 0이었다. Development Player의 `Assembly-CSharp.dll` SHA-256은 `6439fd9414552eb68c26fd237d453840953194c6202f42ec4e78d16faf7842cb`다.
- 두 피어 각각 방위 0°·90°·180°·270°와 기울기 −25°·65°에서 WASD 32개 조합을 확인했다. 평지 시험장에서 걷기 3.500m/s와 달리기 8.400m/s를 실측했다. 기존 V3 달리기 기준인 5.6m/s의 1.5배다. 점프 상승 1.436m와 착지, 두께 0.2m 벽 앞 정지, 서버 스태미나 소모·회복, 로컬·원격 Run 애니메이션과 양쪽 최종 위치 복제를 확인했다.
- 저장된 시작 봉우리에서 서로 다른 카메라를 쓰는 축 담당의 수평 이동과 회전 담당의 A/D 방향 반전 및 W/S 고도를 확인했다. 회전 중 고정 축과 32m 길이가 유지됐다. 시험장과 막대 그립 접근의 시작 위치는 테스트에서 배치했다. 사람 조작감과 발 미끄러짐은 이 자동화로 평가하지 않았다. 선택적 로컬 기록은 `Docs/Development/CameraControls/V1/Verification.md`에 있다.
- 같은 빌드의 `poleRecovery`와 `bridgeDisconnect`도 양쪽 종료 코드 0으로 통과했다. 막대에는 기존 입력 검증과 충돌 복구를, 다리에는 두 피어의 협동 운반·회전과 연결 끊김 후 복구를 확인했다.

## V3 플레이테스트 피드백 3·6 · 왕로 막대

- Unity 6000.3.27f1 Windows Development 빌드에서 승인된 DeepGullies 지형과 FinalPolish 재질을 함께 사용했다. D3D11 실제 Relay의 `cameraControls`와 `poleRecovery`는 각각 양쪽 종료 코드 0을 기록했다. 최종 Development Player의 `Assembly-CSharp.dll` SHA-256은 `79B599526EB3F7042B41E87E670304B2C9783BA2806C77D314CA64E4ACFFE195`다.
- 시작 봉우리에서 카메라 기준 Pivot WASD, 보행 3.500m/s, 달리기 8.400m/s 및 스태미나 소비를 확인했다. 공중에 있는 Orbit 끝점도 두 피어에서 32m 길이를 유지하며 함께 이동했다. 몸통 중앙만 가로막은 시험에서는 예전 막대 캐스트가 장애물을 감지했고 새 규칙으로 회전이 계속됐다. 실제 캐릭터 캡슐 충돌은 실패와 개별 복구를 일으켰다.
- 저장 콜라이더의 기존 방향 연결 94개를 모두 보존하고 9개를 추가했다. 추가된 9개 호에서 이전 막대 쿼리가 실제 암석을 감지하는 위치를 기록했다. 승인 경로 4개와 대표 경로 3개의 연속 계획이 통과했다. 경사 45° 이하의 지형 연결 검사에서 시작 봉우리와 주 능선 및 Final 사이의 보행 분리가 유지됐다. 선택적 로컬 기록은 `Docs/Development/CooperativePole/V1/Verification.md`에 있다.
- 동쪽 경로의 첫 착지에서는 높이 차이 4.122m인 두 봉우리에서 실제 지형 지지를 유지했다. 막대 몸통이 시험용 장애물과 겹쳐 있는 동안 착지, Pivot 이동, 역할 교대가 통과했다. 이동 뒤 양쪽 끝점 간격은 32.000m였고 두 피어의 끝점 좌표는 일치했다. 경계 메시 29개가 생성됐고 표시용 Collider는 0개였다. 첫 착지의 두 플레이어 카메라에서 황토색 경계 표시를 확인했다.
- 같은 빌드의 동쪽 산악 격리 Relay에서 그립 위치에 배치한 뒤 정상 이동과 RPC로 264개 동작과 역할 교대 9회를 완료했다. 두 피어가 모두 Big Summit 체크포인트에 도착했다. 그립 해제 후 검증용으로 외곽에 배치한 Host는 일반 중력으로 낙하해 해당 체크포인트로 복구됐으며 Client 위치는 유지됐다. 두 프로세스 종료 코드는 0이었다.

## V3 플레이테스트 피드백 7·26 · 산악 라운딩과 착지 예측

- 저장된 DeepGullies의 렌더러와 MeshCollider가 같은 메시를 공유한다. 기존 삼각형과 X/Z 좌표를 보존했고, 29개 봉우리에서 기존 유효 지지 표본 62,448개를 모두 유지했다. 유효 지지 표본의 최대 높이 변화는 0.2mm였다.
- 기존 방향 연결 103개와 승인·대표 연속 계획 7개를 확인했다. 경사 45° 이하의 면 연결에서 최초 스폰과 Start 사이의 접근로가 유지됐고 둘 다 작은 봉우리와 Final부터 분리됐다. 서쪽 침식 홈 중심부의 정점 148개는 높이가 같았다.
- Windows D3D11 격리 Relay의 동쪽 막대 경로에서 두 피어가 모두 264개 동작과 역할 교대 9회를 거쳐 Big Summit에 도착했다. 첫 착지에서 높이 차이 4.130m인 두 봉우리의 지지를 유지한 채 함께 이동했다. 막대 길이는 32.000m였고 이동 후 두 피어가 같은 끝점 좌표를 받았다.
- 최종 Windows Development Player에서 두 피어가 각자의 R 충전 링을 확인했다. 방향 8개, 발사 고도 3개, 충전량 3개의 조합에서 지지 가능한 접촉, 위험 접촉, 예측 보류가 모두 나왔다. R 해제·Esc·리스폰으로 표시가 사라졌고, 예열 후 예측 계산 20회의 관리 힙 할당 측정값은 피어당 0바이트였다.
- 최종 빌드의 격리 Relay에서 양쪽 모두 Final에서 Start까지 복로 9구간을 착지했고 종료 코드는 0/0이었다. 18회 모두 예상 봉우리와 실제 봉우리가 일치했다. 착지 위치 오차는 0.080–0.108m였다. 최종 `Assembly-CSharp.dll` SHA-256은 `4E94BC4E82B53051DF52180B35B5F083F5DCC9A770B0E730B203D783C03B5D14`다. 선택적 로컬 기록은 `Docs/Development/MountainGeometryLaunch/V1/Verification.md`에 있다.

## V3 플레이테스트 피드백 8·9·10·11·27 · 플레이어 외형과 PvP

- Unity 6000.3.27f1에서 저장된 V3 캐릭터 프리팹 및 씬 검증을 통과했다. Windows D3D11 Development Player의 격리 Relay에서 두 피어가 머리 위 메시 제거와 캡슐 유지, 서로 다른 망토 색과 양쪽 표시 일치를 확인했다. 리스폰과 잡힌 상태에서의 연결 해제 후 새 NGO ID로 재접속해도 같은 슬롯의 색상을 복원했다.
- 기존 main의 거리 2.650m를 3.445m로 조정했다. 평지에서 3.425m 타격은 성공하고 3.465m, 후방, 벽 너머 및 자기 타격은 거부됐다. 기록된 서버 타격은 공격 자세의 접촉 시각에서 0.004초 뒤에 발생했고 두 피어에서 후속 자세와 피격 반응을 관찰했다.
- 잡기 중 보행 3.500m/s, 달리기 8.400m/s를 측정했다. 양쪽 피어에서 잡힌 자세와 달리는 잡기 자세를 확인했다. 잡는 플레이어의 카메라는 상대 위로 시점을 확보했고 시험용 벽 앞에서 멈췄다. 저항, 던지기의 벽 충돌, 스태미나 고갈 및 회복, 보석 보호 시간·드롭·탈취 및 동시 픽업 회귀가 통과했다.
- 별도 `cameraControls` 회귀에서 카메라 기준 이동, 스태미나 달리기, 점프, 막대 보행·달리기, 공중 회전 담당을 둔 이동과 양쪽 위치 복제를 확인했다. 최종 `contest`, `contestLifecycle`, `cameraControls`의 종료 코드는 각각 0/0이었다.
- 같은 PC에서 실행한 자동 피어와 스테이션 배치에 한정된다. 최종 Development Player의 `Assembly-CSharp.dll` SHA-256은 `3F577FFECFFEBF38066457B5671E119FFD274FEDCA14B5969F492CF86879CC41`다. 선택적 로컬 기록은 `Docs/Development/PlayerAppearancePvP/V1/Verification.md`에 있다.

## V3 플레이테스트 피드백 2·25 · 붕괴 다리와 직선 계단

- Unity 6000.3.27f1 Windows D3D11 Development Player의 `bridge`와 `canyon` 격리 Relay를 같은 PC의 두 피어로 실행했다. 두 회차 모두 종료 코드 0/0이다. 검증한 `Assembly-CSharp.dll` SHA-256은 `CFCFB0380F76016ADD46859BCF34697BC62F89A7531406C8A9DE00FE7AAE25D0`다.
- 다리에서 4칸 대기, Host 3칸과 Client 반대 방향 2칸, 직접 접촉을 검증했다. 전조 중 지지가 남고 이후 두 피어의 렌더러와 Collider가 제거됐다. 범위 밖 높이와 옆길은 판정에서 제외됐다. 두 피어의 제거 개수와 판자 목록이 서버의 선택과 일치했다. 붕괴 후 추락은 협곡 쪽 다리 진입 체크포인트에서 복구했고 제거 상태가 유지됐다. 두 피어는 목재 13회 설치와 왕로 횡단, 복로의 일반 점프 28회와 달리기 점프 6회를 각각 완료했다.
- 저장 씬의 계단 및 난간 렌더·Collider 공유, 하류 수로 표본과 다리·부두 접합부를 확인했다. 30·60·120fps의 CharacterController 재생에서 3.5m/s 보행과 8.4m/s 달리기로 양방향을 통과했다. 격리 Relay에서 Host는 왕로 하강을 보행, 복로 상승을 달리기로 완료했고 Client는 반대로 완료했다. 계단에는 점프 입력을 사용하지 않았다.
- 격리 회차는 초기 위치와 단계 전환을 테스트에서 설정했다. 전체 월드와 전체 경기는 최종 통합으로 보류했다. 선택적 로컬 기록은 `Docs/Development/BridgeStairway/V1/Verification.md`에 있다.

## V3 플레이테스트 피드백 12·13·24 · 강과 뗏목

- 기준 main `5109724`의 왕로와 복로는 장애물 10개를 공유했다. 같은 강 구간에 5개를 추가해 각 단계가 15개를 복제한다. 암초 3→5, 유목 2→3, 물고기 2→2, 악어 1→2, 소용돌이 2→3이다.
- 저장된 수로의 표본 372개, 양쪽 부두의 건식 접근, 뗏목 3척의 정박 위치와 공동 뗏목 폭의 장애물 회피 경로를 확인했다. 하류 부두 쪽 협곡 계단은 30·60·120fps의 CharacterController 검사에서 보행·달리기로 양방향 통과했다.
- Windows D3D11 격리 Relay의 `river`에서 두 탑승자가 공동 뗏목으로 상류 부두에 도착했고, 복로에서는 A/D로 장애물을 우회해 두 개인 뗏목이 하류 부두에 도착했다. 각 단계에서 두 피어의 장애물 개수가 15개였다. 복로 무입력 이동과 저장 수면의 UV 및 셰이더를 대조해 물리 유속과 수면 스크롤이 하류 방향임을 확인했다.
- `riverRecovery`에서는 공동 뗏목 5회, 개인 뗏목 3회의 실제 암초 피해와 선체 플래시를 양쪽에서 확인했다. HUD는 피해를 입은 뗏목의 탑승자에게만 강조됐다. 쿨다운 중 접촉은 새 피격 표시를 만들지 않았다. 임계치 복구, 다른 뗏목의 상태 유지와 재접속 검사도 통과했다.
- Unity 6000.3.27f1 Development Player, 같은 PC의 실제 Relay 두 프로세스에서 두 케이스 모두 종료 코드 0/0이었다. `Assembly-CSharp.dll` SHA-256은 횡단 `737CCC737A176DBB0243D26DD396EBB64EEF41135B68621DF8CDC8D9203D5845`, 복구 `1DF269EF3A6E82B97D2A9ACB9E50D99A4DC559DDEE2C39307DC8571C5AFFCEA1`다. 복구에서는 시험용 암초를 사용했다. 이후 변경된 재생 경로는 횡단에서만 쓰인다. 선택적 로컬 기록은 `Docs/Development/RiverRaft/V1/Verification.md`에 있다.

## V3 플레이테스트 피드백 14–19 · 성채 환경과 퍼즐 UX

- 최종 저장 씬에서 안뜰·지하문 입구·계단 접합부와 동굴 진입부의 겹치는 바닥 면을 정리했다. 기존 Collider 직렬화 레코드 914개와 체크포인트 관련 레코드 3개가 기준 씬과 일치했다. Unity 6000.3.27f1 D3D11 Editor에서 저장 씬 검증과 통로 다섯 시점의 차폐 검토를 통과했다.
- 같은 Windows PC의 D3D11 Development Player 두 개로 `castleUnderground`를 실제 Relay에서 재생했다. 두 피어가 각 방의 송신·수신 안내, 서버 승인 발판의 하강, 오답과 단계 전환 시 복원, 네 단계의 하수도 기록과 양방향 계단·체크포인트를 확인했다. 복로에서 함정 통로와 하수도를 통과하고, 기존 12개 문양으로 하수도를 연 뒤 재접속 후에도 개방 상태가 복원됐다. 종료 코드는 0/0이다.
- 격리 회차의 `Assembly-CSharp.dll` SHA-256은 `859E51D82632F36CA788825CB21BEEB40F9914EB6034499FE69C7C4CA39727E1`이다. 이 회차와 별도로 최종 저장 씬의 입구 천장 연장과 동굴 진입부 렌더 접합을 검증했다. 기존 보행 Collider와 런타임 퍼즐 코드는 두 검증에서 동일했다. 첫 회차는 호스트가 기존 슬롯을 해제하기 전에 Client가 재접속을 시도해 중단됐다. 재생기를 호스트의 슬롯 해제를 기다리도록 수정한 뒤 재접속 두 번을 통과했다. 선택적 로컬 기록은 `Docs/Development/CastlePuzzle/V1/Verification.md`에 있다.

## 남은 확인

최신 소스의 전체 월드 회귀, 전체 자동 경기 및 E2E Relay, DeepGullies와 FinalPolish를 함께 패킹한 Windows Player, 최종 배포 빌드는 사용자가 시작하는 최종 통합 단계로 보류했다. 이전 전체 경기의 결과는 해당 후보 빌드의 기록이다.

사람 2인의 별도 PC 전체 경기, 실제 조작과 밸런스, 장시간 네트워크 임페어먼트와 실기 성능은 개별 검증이 필요하다. 위 타이머는 한 PC에서 동작한 두 자동화 피어의 결과다. 상세 원본은 선택적으로 참고할 수 있는 로컬 증거다. 새 클론에서의 재현 절차는 [빌드 및 Relay 재생 절차](PlaytestGuide.md#development-build-and-automated-relay-checks)를 따른다.
