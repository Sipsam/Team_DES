# Twin Edge V3 기술 품질·안정성 감사

V3-QA-AUDIT는 `48d03add26e080fab81b02f12df528d20c212982`의 활성 플레이 경로를 조사했다. 시작 시 로컬 HEAD, origin/main, 실제 원격 main이 일치했다. PRES-01~10과 PLAYTEST-03은 완료 상태다. `1ca281b` 이후 감사 기준까지의 변경은 문서 5개이며 3차 Player의 소스와 감사 기준의 게임 코드·에셋은 같다. 기존 따로 저장된 디자인 PDF는 미추적 상태로 보존했다.

WORKERS V2의 Explore → Diagnose → Validate → Review → Finalize를 동일 세션에서 수행했다. QA-A의 자료가 QA-B~F의 조사 범위를 정했고 QA-G가 결과를 후속 작업으로 연결했다. 추적 변경 범위는 문서다. 격리 시험의 수정과 진단 Player는 선택적 로컬 무시 경로 `Docs/Development/QualityAudit/V1/`에 있다. [실행 범위](../Playtest/PlaytestGuide.md#scoped-validation) · [기존 검증](../Playtest/ValidationSummary.md) · [후속 일정](../Workflow/ROADMAP.md)

## QA-A · 기존 검증과 배포

- 1차 일반 Player `Builds/V3Playtest/`의 208개 파일과 2차 `2079709` Player 208개 파일은 기존 매니페스트와 해시를 대조했다. 1차 ZIP은 기존 기록 및 로컬 Builds에서 찾을 수 없었으며 GitHub 릴리스 목록은 비어 있었다.
- 3차 일반 Player `1ca281b`의 같은 PC 실제 D3D11 Relay 준비·시작·수동 재입장은 기존 스모크 근거다. 기존 공식 기록의 PRES-10 캡처 18개와 이전 보호 목록 198개를 로컬 파일과 해시로 다시 대조했다.
- 2차·3차 제출은 해당 체크리스트와 Git 변경 이력, 로컬 증거 이름, GitHub 이슈·PR 목록에서 확인되지 않았다. 두 라운드 모두 `HUMAN_REVIEW_REQUIRED`로 대기 중이다. 별도 PC, 실제 IME와 포커스, 접근성·밸런스·청취와 시각 품질은 자동화 결과와 구분한다. [2차 체크리스트](../Playtest/SecondHumanPlaytest.md) · [3차 체크리스트](../Playtest/ThirdHumanPlaytest.md)

## QA-B · 렌더링과 전체 지도

| 경로 | 활성 구조와 확인 범위 |
| --- | --- |
| 경기 카메라 | `V3Gameplay`의 standby 카메라가 소유자 추적·충돌 카메라가 된다. Additive 레벨에 저장된 미리보기 카메라는 부트스트랩이 비활성화한다. 메뉴·대기실에는 월드 카메라가 전환되고 활성 Listener도 함께 전환된다. |
| 대기실·결과 | 격리 레이어 카메라가 캐릭터와 해당 조명을 대기실의 320² RT 두 개와 결과의 512² RT 한 개에 별도로 그린다. 미리보기 카메라는 HDR, MSAA와 URP 그림자를 사용하지 않는다. PRES-10 실측은 대기실 1,230,736 B, 결과 1,573,832 B다. 화면 전환에서 RT와 PlayableGraph를 해제한다. 저장 월드 카메라와 런타임 미리보기의 URP Overlay Stack 연결은 발견되지 않았다. |
| PC URP / D3D11 | URP 17.3.0의 Standalone 품질 1은 `PC_RPAsset`과 `PC_Renderer`를 사용한다. Forward+, render scale 1, MSAA 1, VSync 0, SRP Batcher 활성이다. Player Settings 정적 배칭 활성·동적 배칭 비활성, 주광원 그림자맵 2048, 그림자 거리 50m와 4 cascades, Renderer의 활성 SSAO와 RP의 Volume Profile 참조를 확인했다. 실제 SRP/인스턴싱 적중률과 SSAO·Volume·그림자 패스 비용은 미계측이다. |
| 월드 및 컬링 | 카메라의 절두체 컬링 외에 저장된 두 씬의 OcclusionCullingData 참조는 0이다. LevelBlockoutV3의 저장 GameObject 1,189개는 모두 static flags 0이며 LODGroup 레코드는 0개다. V3WorldMaterials의 저장 재질 8개는 인스턴싱 플래그 0이다. 런타임·프리팹 확장 뒤의 실제 가시 Renderer 수, LOD 분포와 GPU 인스턴싱 효과는 Frame Debugger 후보로 남겼다. |
| 재질과 투명 표면 | 산악의 `ConnectedRidgeline`과 `MountainBlend`는 승인된 트라이플래너 월드 표현을 사용한다. 물, 원경, 실내·지하 파생 재질과 VFX 파티클 공유 재질은 별도 경로다. 세계와 UI의 투명 픽셀·오버드로를 분리할 GPU 캡처는 미계측이다. [저장 월드 재질](GameArchitecture.md#v3-월드-재질) |
| 전체 지도 | `CourseMapPresentation`은 Screen Space Overlay Canvas에서 사전 생성 시트·아이콘을 갱신한다. 구역과 층 전환은 시트 선택과 필터링을 바꾼다. 지도 전용 Camera, 매 프레임 새 RenderTexture 또는 월드 렌더 패스 추가는 소스에서 발견되지 않았다. 월드 카메라는 계속 활성이다. Texture/UV, 마커, 위치·회전·색 갱신과 반투명 모달은 독립 비용 후보로 남긴다. |

### 기존 성능 표본

PRES-10의 Windows 11 D3D11 Development Player 두 개, 1600×900, Host 60/Client 30fps 제한, VSync 0, 연출 100%, 미니맵 표시, 구간당 120프레임을 재사용한다. 결과 화면은 별도 같은 방 재경기 실행의 양쪽 60fps 표본이다. 메인 스레드 표본은 프레임 제한 대기를 포함한다. 원시 Run 경로와 빌드 해시는 기존 [대표 성능 기록](../Playtest/ValidationSummary.md#대표-자원과-성능)에 연결된다.

| 구간 | Host 평균 간격 | 할당 B/프레임 | 표본의 한계 |
| --- | ---: | ---: | --- |
| 최초 메뉴 | 18.58ms | 205,228 | 월드 준비 후 메뉴 직후 2.23초 |
| 2인 대기실 | 16.68ms | 1,480 | 초상 RT 2개 |
| 왕로 산악 HUD | 18.20ms | 2,235 | 지도 비교 전 이동·화면 레이아웃 작업이 뒤따름 |
| 전체 지도 | 28.61ms | 3,543 | 24.56ms GPU 평균(양수 99표본), Draw Calls/Batches 각각 7,810 |
| 성채 키패드 | 16.69ms | 2,200 | 별도 성채 실행 |
| 결과 | 16.66ms | 1,456 | 별도 재경기 실행 |

전체 지도 7,810은 월드와 UI를 합친 카운터다. 과거 HUD와 지도는 카메라·시점과 측정 전 동작이 일치하지 않는다. 전체 지도 Canvas.BuildBatch 평균은 Host 0.02ms, Client 0.01ms였고 UI Layout 계측은 비어 있었다. 이전 유효 GPU 기록에서 같은 프레임 타이밍의 중복 여부는 확인할 수 없다. 이전 실행의 0 렌더 카운터는 수집 불가 처리했다. 산악·메뉴·대기실·키패드·결과 이외에 다리·협곡·강·성채 외부·지하의 기존 증거는 캡처나 격리 동작 결과다.

### 격리 지도·메뉴 비교

`PlayerBuild-20261128-045548559`에서 기준 소스를 별도 작업 트리에 두고 Development 전용 드라이버에 계측을 추가했다. Unity 6000.3.27f1 Windows 11 Pro 10.0.26300, Ryzen 7 4700U, AMD Radeon Graphics(31.0.21924.61), RAM 약 7.42GiB, 1920×1080 데스크톱이다. `MapMenuRelay-20261128-050138163`은 같은 PC 실제 Relay 두 피어, D3D11, Unity 보고 1600×900, Host 60/Client 30fps, VSync 0, 연출 100%에서 실행했다. 표본당 2초 예열 후 연속 600프레임(Host 약 10초, Client 약 20초). 아래 표는 Host다. 카메라를 정지시킨 뒤 Host 위치 (−49.559, 77.311, −369.154), 방향 (355°, 30.069°, 0°)의 변위·회전은 로그 정밀도에서 0이었다. Client의 자기 카메라도 구간 비교 동안 정지했다.

| 상태 | 평균 / 중앙값 / P95 / P99 (ms) | GC B/프레임 / Gen0 횟수 | Main FrameTiming (ms) |
| --- | --- | --- | ---: |
| 예열 후 메뉴 | 16.67 / 16.66 / 17.00 / 17.15 | 120.16 / 0 | 0.97 |
| 메뉴–설정 왕복 3회 후 메뉴 | 16.69 / 16.67 / 17.00 / 17.31 | 120.16 / 0 | 0.99 |
| 미니맵 켬, 전체 지도 닫힘 | 16.72 / 16.66 / 17.80 / 18.82 | 2,456 / 2 | 4.68 |
| 미니맵 끔, 전체 지도 닫힘 | 16.70 / 16.66 / 17.80 / 18.45 | 2,271 / 2 | 4.58 |
| 전체 월드 지도 | 16.68 / 16.65 / 17.93 / 18.44 | 2,259 / 2 | 4.71 |
| 전체 지도 산악 시트 | 16.88 / 16.70 / 18.38 / 27.98 | 2,259 / 2 | 5.79 |
| 전체 지도 결승 시트 | 16.68 / 16.64 / 17.64 / 18.09 | 2,259 / 2 | 4.56 |
| 전체 월드 지도, 카메라 마스크 0 | 16.69 / 16.62 / 18.38 / 20.39 | 2,259 / 2 | 5.56 |
| 미니맵·전체 지도 닫힘, 마스크 0 | 16.68 / 16.66 / 17.80 / 18.36 | 2,271 / 1 | 4.42 |
| 미니맵·전체 지도 닫힘, 재측정 | 16.68 / 16.64 / 17.70 / 18.60 | 2,461 / 1 | 5.12 |
| 전체 월드 지도, 재측정 | 16.67 / 16.67 / 17.83 / 18.57 | 2,452 / 1 | 4.75 |

최초 메뉴 120프레임은 기존 샘플러로 18.95ms와 205,171 B/프레임을 재현했다. 같은 실행의 120프레임 로비는 16.70ms, 1,479 B/프레임, 왕로 HUD는 16.69ms, 2,232 B/프레임이다. 메뉴 왕복 구간 자체, 초기 폰트·텍스처·오디오·월드의 개별 비용, 로비↔메뉴의 반복 할당은 미계측이다. 집계에 테스트 오버헤드가 포함되어 있고 예열 후 프레임의 120B를 특정 게임 경로에 귀속할 수 없다.

프레임 간격은 60/30fps 대기를 포함한다. Main FrameTiming은 프레임 시작부터 main 작업 종료까지의 값이며 모든 대기가 제거된 CPU 계산 비용으로 해석할 수 없다. Host의 산악 Physics.Simulate 표본 평균은 0.16–0.22ms이고 Canvas.BuildBatch는 0.01–0.02ms였다. Host 스냅샷의 Mono used는 메뉴 약 6.37MB, 산악·지도 10.2–11.1MB, Unity 할당 카운터는 메뉴 약 137.3MB와 산악·지도 144.7–145.2MB, Unity 예약은 약 292.8–294.9MB다. Unity allocator 통계에는 프로세스 전체 네이티브 RSS와 GPU 메모리가 포함되지 않는다. 구간별 스냅샷 차이로 메모리 누수를 판정할 수 없다.

첫 실행에서 GPU, draws, batches, Render Thread 및 UI Layout은 유효값을 얻지 못했다. 측정 중 Windows 메인 창 핸들은 0으로 관측됐다. 표의 결승 시트는 원시 로그 라벨 `cavern-sheet`와 달리 마지막 버튼이다. 정지 카메라 표에서 기존 28.61ms 평균은 재현되지 않았지만 이 조건은 정상 화면 프레젠트와 GPU 작업량을 증명하지 않는다. 월드 마스크 0은 피어의 물리·네트워크와 UI를 유지한 채 카메라 가시 월드만 제외하기 위한 대조이며 전체 렌더러 기능·오버드로의 비용 분해를 대체하지 않는다.

### 실제 창 D3D11 재측정

`RenderBuild-20261128-050927560`의 격리 소스에서 Frame Timing Stats를 활성화했다. `RenderRelay-20261128-051259914`은 위와 같은 산악 시점과 두 피어 설정에서 창을 1280×720 → 1600×900으로 다시 적용했다. 두 Windows 창 핸들과 양수 GPU·Draw·Render Thread 표본을 확인했다. 모든 상태에서 두 피어가 맞춰 들어간 뒤 2초 예열과 600프레임을 수집했다. 카메라의 위치와 방향은 각 피어 내부에서 고정됐다. 표는 Host 기준이다. GPU는 타임스탬프가 중복되지 않은 양수 표본 평균이다.

| 상태 | 평균 / 중앙값 / P95 / P99 (ms) | GPU ms (유효 표본 수) | Draw Calls / Batches 각각 | Main / Present wait / Render Thread (ms) | GC B/프레임 / Gen0 횟수 |
| --- | --- | --- | ---: | --- | --- |
| 미니맵·전체 지도 닫힘 | 37.97 / 36.53 / 56.63 / 68.21 | 32.09 (445) | 7,853.96 | 16.07 / 21.88 / 5.66 | 2,899 / 1 |
| 전체 월드 지도 | 39.45 / 38.28 / 54.32 / 68.64 | 33.25 (449) | 7,874.26 | 16.69 / 22.75 / 5.69 | 2,897 / 1 |
| 전체 지도 지하 시트 | 43.98 / 41.22 / 68.15 / 84.31 | 35.95 (469) | 7,875.43 | 17.20 / 26.79 / 5.64 | 2,906 / 2 |
| 전체 월드 지도, 마스크 0 | 16.68 / 16.65 / 17.85 / 18.52 | 3.38 (600) | 16.97 | 6.32 / 10.33 / 0.33 | 2,817 / 1 |
| 미니맵·전체 지도 닫힘, 마스크 0 | 16.67 / 16.72 / 17.87 / 18.36 | 3.27 (600) | 10.98 | 6.07 / 10.58 / 0.30 | 2,831 / 1 |
| 미니맵·전체 지도 닫힘, 재측정 | 38.02 / 36.93 / 53.49 / 63.89 | 31.68 (449) | 7,866.20 | 16.40 / 21.61 / 5.61 | 2,905 / 1 |
| 전체 월드 지도, 재측정 | 38.53 / 36.61 / 55.25 / 65.59 | 32.51 (455) | 7,878.42 | 16.54 / 21.95 / 5.65 | 2,896 / 1 |

60fps 예산 16.67ms 초과는 전체 지도 닫힘과 표시 양쪽에서 재현됐다. 미니맵을 끈 기본 HUD 대비 전체 지도에서 약 20 Draw Calls/Batches가 추가됐고, 측정 초기 쌍의 GPU 평균 차이는 1.16ms였다. 전체 월드 지도 표시를 유지한 채 두 피어의 가시 월드 마스크를 0으로 설정하자 Host의 7,874→17 draws와 GPU 33.25→3.38ms를 관측했다. Client도 7,874→17 draws와 GPU 32.93→4.65ms로 낮아졌다. 이 카메라와 부하에서 60fps 예산을 가장 크게 초과시킨 작업은 가시 월드 렌더링이었다. 카메라 마스크 변경에는 캐릭터와 월드 지오메트리 및 그에 의존하는 렌더 작업이 함께 포함된다. 개별 메시·그림자·SSAO·셰이더 비용, UI 오버드로와 층 전환 자체의 시간은 분리하지 못했다.

지하 시트 표본은 플레이어가 산악 위치에 있는 상태다. 지하 씬의 렌더링 성능과 시트 전환 프레임은 별도 측정이 필요하다. 창이 없는 표본과 이 표본의 GPU 부하는 다르며 단일 Player와 별도 PC 결과는 아직 없다. 역사적 28.61ms와 최신 표본의 절대값을 비교할 때 두 창의 프레젠트 상태와 계측 설정 차이를 고려한다. 표의 Present wait는 Unity FrameTiming의 main-thread Present 대기다. 프레임 간격·main·Render Thread·GPU 시간의 구간은 겹칠 수 있다. UI Layout, 개별 Animation 비용, 장기 관리/네이티브 메모리는 미계측이다.

두 격리 Relay 실행은 양쪽 PASS 및 종료 코드 0을 남겼으며 지도 계측 뒤 종료했다. 샘플, 준비 및 시작 검사, 빌드 명령과 실행 파일·관리 어셈블리 SHA-256은 각 Run 경로와 `Source-20261128-045458280/Reproduction.md`에 보존한다.

### V3-PERF-01 패스 분리

`Attribution-20261128-063724901`은 `a764f14`에 격리 계측을 적용한 Development Player를 사용했다. Host와 Client가 실제 Relay의 Outbound에서 고정 산악 카메라로 각각 2초 예열 후 150프레임을 기록했다. 1600×900, D3D11, VSync 0, 연출 100%, 60fps Host와 30fps Client 조건이며 실제 창을 다시 적용한 이후의 Host 수치다. 시작과 종료 기준의 차이는 짧은 대조의 변동 범위를 보여 준다.

| 조건 | GPU FrameTiming ms | P95 프레임 간격 ms | Draw Calls | 불투명 GPU ms | 깊이·노멀 GPU ms | SSAO GPU ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 기준 | 36.58 | 56.00 | 7,817 | 27.52 | 1.61 | 5.88 |
| Main Light 그림자 제외 | 32.88 | 59.51 | 7,812 | 25.12 | 1.83 | 5.56 |
| Additional Light 그림자 제외 | 30.78 | 61.26 | 7,809 | 22.83 | 2.55 | 4.65 |
| SSAO 제외 | 17.24 | 27.41 | 3,992 | 15.25 | — | — |
| 카메라 Post Processing 끔 | 34.30 | 60.98 | 7,818 | 23.90 | 2.16 | 6.36 |
| 투명 레이어 제외 | 29.89 | 50.74 | 7,664 | 21.55 | 2.10 | 4.81 |
| 기준 재측정 | 31.54 | 53.93 | 7,808 | 23.61 | 2.33 | 4.25 |

기준에서 Opaque CPU 마커는 2.68–2.80ms, 깊이·노멀은 3.20–3.28ms였다. SSAO 제외는 깊이·노멀 프리패스와 불투명 셰이더의 SSAO 경로를 함께 제거했다. 가장 큰 GPU 작업은 불투명 패스였다. 두 그림자 마커는 이 시점에서 각각 약 0.0005ms였고 투명 기준은 0.05ms였다. 카메라에 Post Processing이 기본 활성화되어 있지 않았다. GPU 카운터의 구간은 서로 겹칠 수 있고, 별도 Forward+, 오버레이 UI, 파티클 렌더 및 대기실 미리보기 GPU 시간은 이 표본에서 `NOT_MEASURED`다. 상세 마커와 변형별 실측은 선택 로컬 자료 `Docs/Development/RenderingOptimization/V1/Attribution-20261128-063724901/`에 있다.

`Geometry-20261128-065418724`의 같은 산악 시점에서 런타임 활성 Renderer는 3,954개였다. `GameState` 아래의 MeshRenderer 3,209개는 이 시점의 CPU 프러스텀과 교차했고 Unity의 `isVisible`도 참이었다. 이 수는 화면의 픽셀 기여가 가려진 렌더러를 포함할 수 있다. 그중 고정 다리 프레임은 원본 92개 MeshRenderer를 26개 모듈에 반복해 총 2,392개였다. 저장 YAML의 MeshRenderer 963개와 런타임 수를 분리한다.

같은 실험에서 산악 렌더러를 제외하자 Host GPU가 33.40ms 기준에서 13.09ms로 내려갔다. 산악을 유지한 160m 청크 실험은 519,159개 인덱스를 47개로 나눴고, 프러스텀에 걸린 31개 청크는 417,861개 인덱스였다. 3회 평균 GPU는 29.80ms였으나 26.82–32.49ms의 변동과 약 64개 추가 Draw Calls가 있었다. Depth Priming Auto는 대응 기준 평균 31.76ms에서 29.74ms였고 세 번째 표본은 기준보다 느렸다. Opaque Copy 제외는 짧은 표본에서 32.34ms였다. 기존 산악 출력과 SSAO를 보존하기 위해 산악 분할, Depth Priming 및 Opaque Copy 실험 설정은 제외했다. SSAO와 그림자 품질, 투명 렌더, Forward+, 재질과 셰이더를 보존했다.

## QA-C · CPU, GC와 메모리

| 경로 | 소스에서 확인한 일과 검증할 비용 |
| --- | --- |
| 초기화 | 부트스트랩의 Additive 월드 로드, 마커·Collider와 저장 섹션 바인딩, UI 트리·폰트·텍스처 준비, 오디오 카탈로그·초기 메뉴 트랙 경로는 초기 비용 후보다. 최초 메뉴 205,228 B/프레임의 표본에는 초기화 직후 프레임이 섞여 있다. 원인별 할당 콜스택을 구분하려면 첫 씬 진입부터 샘플링해야 한다. |
| HUD·세션·지도 | `SessionPresentation.Update`가 피어 목록 및 화면을 갱신하고 `GameplayPresentation.Refresh`를 호출한다. HUD에는 매 프레임 대상·기믹 조회와 값에 기반한 문자열 생성 후보가 있으며 많은 텍스트 대입은 동일 값 비교로 보호한다. 지도는 List·아이콘을 재사용하며 전체 지도 제목 연결, 마커의 UV·Transform·가시성 갱신이 남는다. 이름표 시야는 버퍼를 재사용하는 약 0.1초 간격 쿼리다. Esc 경로의 씬 탐색과 UI 생성 시 GetComponent는 호출 경계가 다르다. |
| 캐릭터·카메라·기믹 | `GameplayInput`은 수명당 입력 에셋 인스턴스를 생성·해제한다. `ThirdPersonCamera`는 Update/LateUpdate의 입력·추적과 카메라 충돌을 처리한다. `GameplayCharacter`는 스태미나·서버 PvP fixed step, 소유자 모션 샘플·지지 확인과 LateUpdate 계열의 손 그립·렌더 보정을 수행한다. 다리 목재, 막대 끝점과 뗏목의 고정 스텝 지지·접촉 쿼리 및 기믹 검증 LINQ는 활동 중 우선 프로파일 후보다. |
| 전환·이벤트·자원 | `MatchTransitionPresentation`은 복제된 일련번호·시간을 소비한다. 미리보기는 건설·화면 전환에서 생성 및 정리하고, VFX는 공유 재질·씬당 풀(160개, 48m)을 사용한다. 관측 입자 최대치는 다리 Host 30/Client 27, 강 28이다. 부트스트랩의 월드 찾기와 생성, 카메라·그립의 초기 GetComponent, MaterialPropertyBlock 색 적용을 상시 할당과 구분한다. 기존 재경기 두 회의 표본 카운트는 일치했지만 구독·Coroutine·Material 또는 네이티브 메모리의 장기 누적은 미계측이다. |

위 표는 감사 시점의 소스 검토다. V3-PERF-02는 `323a324`의 Unity 할당 스택에서 초기 126프레임의 49.54MB 중 35.62MB를 산악 경계 생성 경로에 귀속했다. 주요 하위 경로는 `CooperativePole.TryGround`의 `Physics.RaycastAll` 24.08MB, `ClearPath`의 환경 필터 델리게이트 8.00MB와 `Physics.OverlapCapsule` 배열 2.00MB였다. 첫 프레임의 uGUI `TextGenerator` 생성도 약 2.36MB를 사용했다. 네이티브 글리프 생성, 셰이더 준비와 지도 아틀라스의 개별 초기 CPU 비용은 `NOT_MEASURED`다.

정상 대기실에서 연결 인원 표시, 닉네임 변환과 슬롯 문자열은 256 B/프레임을 사용했다. 결과 화면의 닉네임과 경기 시간은 164 B/프레임이었다. 대응 표본에는 네트워크와 엔진 할당도 포함된다. [V3-PERF-02 검증](../Playtest/ValidationSummary.md#v3-perf-02--cpu-gc-오디오와-자원-수명)

## QA-D · 네트워크와 세션

NGO 2.13.3, Unity Transport 2.7.4, Multiplayer Services 2.4.1의 lockfile을 확인했다. 저장 NGO tick은 30Hz다. 각 스냅샷의 실제 변경과 와이어 전송 빈도는 가변이다. 일반 이동은 `OwnerNetworkTransform`과 캐릭터 모션 상태를 복제하고 서버는 받은 위치로 거리·기믹·결승을 검증한다. 기믹 중에는 막대/목재/뗏목의 권위 스냅샷을 양쪽 표시가 읽고, 그립·손 본은 로컬에서 계산한다. [멀티플레이 계약](Multiplayer.md)

| 호출·복제 | 책임과 소스 설정 빈도 |
| --- | --- |
| `ReportFoundationReadyRpc`, `SetLobbyReadyRpc`, `RequestMatchStartRpc`, 재경기 투표·재구성 | 연결 및 씬/슬롯 승인 계열의 이벤트 호출. Phase, 두 Runtime Ready, Ready·승자·전환·재경기 상태는 서버 변수다. 서버 시계로 카운트다운·봉인·복로 창을 소비한다. |
| 상호작용·막대·다리·뗏목 | 상호작용과 목재/뗏목 승인·이탈은 이벤트 RPC. 막대·운반·노젓기 연속 입력의 소스 최소 간격은 0.05초이며 최대 약 20회/초/각 활성 발신 경로다. 서버가 슬롯·역할·revision·접촉을 확인하고 끝점, 진행·붕괴, 선체·피해 상태를 공개 복제한다. |
| 캐릭터·보석·발사 | 점프·공격·잡기·저항·보석 획득·발사 티켓은 입력/승인 이벤트다. 달리기 의도는 활성 중 약 0.1초 주기이며 권위 전환과 생명 ID를 검사한다. 보석은 서버 승인 revision과 소유자, 보호·드롭을 유지한다. |
| 성채·지하·오디오·표시 | 키패드 `SubmitNumberRpc`, 개별 힌트·입력자 전용 피드백, 하수도·공동 발판·봉인에 서버 위치·단계·revision 검사가 있다. 표시용 손 제스처, `WorldCueRpc`와 일시적 효과는 승인 사건이다. 메뉴·지도·음악의 로컬 갱신은 별도로 처리한다. |

ActionRevision은 전환 sequence와 입력 창을 함께 검증한다. 생명 ID, 기믹 및 보석 revision, 서버 슬롯과 새 경기의 네트워크 오브젝트 수명이 이전 요청을 차단하는 서로 다른 경계다. PRES-07·10은 Finished 재입장, 두 번의 같은 방 재경기, 새 경기 재입장과 퇴장 후 새 방을 격리 검증했다. 같은 PC 정상 회선의 3차 스모크는 초기 Runtime Ready와 수동 재입장을 검증했다. 지연·중복·순서 변동에서 각 경계가 유지되는지와 연결 해제 뒤 장기 자원 정리는 추가 확인 대상이다.

감사의 `UnityTransport.GetCurrentRtt`는 산악 9구간의 끝에서 Host 100–117ms, Client 100ms를 보고했다. 같은 PC의 실제 Relay 순간 표본이다. 이후 V3-NET-BASELINE은 창을 연 두 피어의 대기실과 이동을 연속 표본화하고, UTP 드라이버 송수신량·신뢰성 통계와 설정 읽기·차이를 기록했다. 설치 UTP의 `ModifyNetworkSimulatorParameters`를 Development 드라이버에 적용한다. 수신 시퀀스 캘리브레이션, NGO 메시지 단위와 UTP 전송 계층의 정의 및 미계측 범위는 [네트워크 기준](NetworkBaseline.md)에서 관리한다.

## 오디오 · QA-C/D/E

`AudioService` 일회성 16슬롯 + `CourseAudio` 지속음 9슬롯으로 PRES-10의 피어별 활성 오브젝트 AudioSource 25개, Listener 1개와 일치한다. 120프레임 Host 오디오 메모리는 약 3.45–3.79MB, 아틀라스 네 장의 런타임 크기는 5,216,272 B였다. 종료 뒤 메뉴 음악이 재생됐고 초기 대기실과 재경기 후 대기실 카운트가 일치했다. V3-PERF-02의 기준·후보는 실제 재생 1–3개와 DSP·Streaming CPU, 메인 스레드 오디오 갱신, Decode 버퍼를 계측했다. 독립 Decode CPU와 교전 시 일회성 큐 분포는 `NOT_MEASURED`다. [구간별 오디오 표본](../Playtest/ValidationSummary.md#오디오-cpu와-스트리밍)

큐별 최소 간격·동시 재생 상한과 우선순위를 적용한다. 발걸음은 75ms 수준으로 이동 거리를 샘플링하며 음성은 사건 시각과 최소 간격을 사용한다. 위치 갱신은 강물·용암 표면과 가장 가까운 활성 화염, 상태 갱신은 구역·스테레오 음악과 앰비언스 페이드·충전에 해당한다. 이전 WASAPI의 음량·트랙·시작음 대조는 출력 근거이며 실제 청취 평가는 사람 플레이테스트에 남는다. [오디오 아키텍처](AudioArchitecture.md)

## QA-E · 게임플레이와 Presentation 위험

- 다리 후반 지지 검사와 목재 운반은 실패·통과 로그가 모두 있다. Run-015 Host는 13개 설치 뒤 `Plank_004` 지지를 기대했지만 이를 관측하지 못했고 Client에는 별도 NullReferenceException이 있었다. PRES-09의 동시 실행 실패에서는 양쪽 Driver가 `Two-input carry 8`에서 멈췄으며 위치가 달랐다. 실패 프레임의 입력, 물리 fixed tick, 서버 상태 revision, 지지 Collider·노멀과 두 피어 위치가 시간 정렬되지 않아 경로·하중·Driver 타이밍의 영향을 분리할 수 없다. 감사에서 이 두 사례의 신규 재현은 시행하지 않았다.
- 키패드와 Esc는 키패드/지도 등 현재 모달 → 기믹 취소 또는 일반 메뉴의 입력 소유권을 공유한다. PRES-10 초기 스크린 클릭+숫자 패드 합산 실패는 테스트가 렌더 타깃에서 실제 창으로 돌아오는 Canvas 갱신 구간에서 있었고 이후 실제 E·클릭·숫자 패드·삭제·오답·승인·재입장 검사가 양쪽에서 통과했다. 고정 2560×1440 렌더 타깃과 실제 창 검증을 구분한다.
- 전환 시 카운트다운 3초, 공동 봉인 뒤 1.5초, 첫 복로 2초 서버 입력 창을 사용한다. 연출 강도 0·50·100%의 필수 안내와 입력 기한 검증이 있다. 배너 캡처 이력은 서버 무효 입력이나 실제 플레이어의 화면 누락에 대한 증거로 쓸 수 없다.
- 보석 소유·피격 드롭·재획득·리스폰과 결과는 서버 Phase·revision·지지/체류 조건을 사용한다. PRES-09의 보석 경합·복구와 PRES-10의 서버 결승·정지 시간·재입장·재경기는 집중 근거다. 전체 경로에서 보석 경합과 정리의 최신 연속 확인은 최종 통합으로 보류한다. [게임플레이 계약](GameplaySystems.md) · [검증 상세](../Playtest/ValidationSummary.md)

## QA-F · 에셋 및 빌드

- `Dependencies-20261128-050646263`의 Unity 6000.3.27f1 읽기 전용 `TwinEdge.Levels.Editor.ProjectDependencyAudit.Run`이 통과했다. Unity AssetDatabase가 패키지 경로까지 확인했고, 저장 V3 두 미리보기 씬의 누락 스크립트는 0개였다. 탐색한 로컬 프리팹 YAML에 직렬화된 null MonoScript 필드는 없었다. 이전 정리 감사의 11개 `source,guid` 쌍이 정확히 재현됐다. DefaultVolumeProfile 4개, PC_Renderer 7개다. 실제 Player 씬 로드 실패나 새로 누락된 V3 게임플레이 GUID는 확인되지 않았다. 기존 패키지·디버그 리소스 직렬화 상태의 영향은 URP 필드와 실제 렌더 오류를 대조하기 전 확정할 수 없다.

V3-PERF-01에서 설치된 URP 17.3.0과 Global Settings를 대조했다. `PC_Renderer`의 7개 참조는 URP가 이전 필드로 표시한 `probeVolumeResources`에 있다. 현재 `ProbeVolumeDebugResources`와 `ProbeVolumeRuntimeResources`의 대응 에셋은 설치된 Core 패키지에서 모두 확인됐다. PC URP의 Light Probe System은 Light Probe Groups를 사용한다. `DefaultVolumeProfile` 자체는 글로벌 기본 프로필로 연결되어 있으며, 미해결 4개는 `OutlineVolumeComponent`, `OasisFogVolumeComponent`, `TestAnimationCurveVolumeComponent`, `TestVolume`의 스크립트다. Outline과 Oasis Fog의 직렬화 값은 각각 비활성과 밀도 0이고, 저장 PC Renderer의 기능은 SSAO 한 개다. 네 스크립트는 프로젝트 및 설치된 패키지에서 찾을 수 없었다. 기존 참조와 QA-13의 상태를 보존한다. 선택 로컬 근거는 `Docs/Development/RenderingOptimization/V1/UrpRefs-20261128-063645426691/Audit.txt`다.
- 생성 에셋 74개 중 비생성 에셋을 루트로 두었을 때 65개가 전이 의존성에 남고 9개가 검토 후보였다. 저장 레벨 씬의 직접·전이 생성 의존성은 60개, 별도 산악 테스트 씬은 5개였다. 수치에는 레거시 씬과 편집 도구의 참조도 반영된다. 검토 후보의 안전한 삭제 여부와 실제 빌드 공간 절감분은 확인하지 않았다. 이번 감사는 정리 도구의 삭제 단계 없이 실행됐다.

- `Artifacts-20261128-050050256`의 Python 읽기 전용 재대조는 2차 ZIP 208개(207개 런타임 파일과 안내)와 3차 ZIP 213개(211개 런타임·고지 파일과 안내 2개)를 확인했다. ZIP SHA-256은 각각 `D0C2CF1CD44F2737315CD6F86368774A9304349CE91D6EA59BB3E51D37691C1A`, `8AFDAC3647728C9DD44A2F3ABEA535BD1EF82F98AF956538F07ECCD1BB09808F`다. 3차는 Player 230,367,685 B, ZIP 123,772,983 B이며 한국어 안내, Noto Sans KR OFL, 오디오 출처와 CC0 전문을 확인했다. 팀 원본 및 그 밖의 제3자 자산의 권리 확인은 이 배포 고지 대조의 범위 밖이다.
- 3차 BuildReport에 포함된 KayKit `dungeon_texture.png`는 제공자의 Dungeon Remastered 1.0 저장소와 Git blob `3c36806274363fbe9edb04e8e4d2b46287778418`이 일치했다. 원본 고지는 CC0다. 기존 ZIP의 세 고지는 폰트와 오디오용이며 KayKit의 출처는 감사 기록에 남긴다. [제공자의 CC0 고지](https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0/blob/b0ca9bd96a8072ab36a3a5464f00ed1e06a16d07/LICENSE.txt)
- 3차 일반 Player의 두 packed 씬과 기존 BuildReport 구성을 대조했다. 저장 Build Settings는 `V3Gameplay`, `LevelBlockoutV3`를 사용한다. ZIP에 개발 소스, 로그, PDB 또는 DoNotShip 파일은 없었다. 일반 Player 어셈블리에서 F3 진단과 Relay·Showcase 심볼이 빠져 있었다. 기존 3차 배포 검사 역시 실제 F3가 진단을 표시하지 않았음을 확인했다.
- 저장 V3 레벨 YAML에는 MeshFilter 963개와 MeshRenderer 963개가 있다. 프리팹 인스턴스 및 런타임 생성분을 포함한 활성 Renderer/GPU draw 수와 구분한다. 3차 빌드 로그의 비압축 user assets 124.8MB 중 텍스처 92.8MB(74.3%), 메시 16.3MB(13.1%), 오디오 8.3MB(6.7%)였다. 리포트 상 산악/협곡/성채/용암 normal map은 각각 최대 5.3MB, `ConnectedRidgeline.asset`은 4.0MB다.
- 원본 확장자 표본은 텍스처 35개 237,816,347 B, 모델 원본 31개 3,591,538 B, 오디오 49개 14,448,543 B로, 각 분류 내 byte-identical 그룹은 0개였다. 생성 YAML 메시와 임포터 변형, 유사·중첩 지오메트리는 이 중복 결과의 범위 밖이다. 과거 프로토타입과 원본 메시·텍스처는 복구 및 생성기 입력 후보로 유지한다. 저장 의존성 스캔과 실제 packed 포함 여부, `Resources` 및 이름 기반 에셋 로드를 함께 검토한 뒤에만 정리 안전성을 평가한다. [프로젝트 의존성 검사](GameArchitecture.md#v3-월드-재질)

## QA-G · Findings와 우선순위

현 감사로 확인된 P0은 없다. 기술 표본의 이상과 실제 플레이어 영향은 다른 열에서 판단하고 측정 공백에는 수정 우선순위를 부여하지 않았다.

| ID / 상태 / 우선순위 | 영향과 근거 | 원인, 수정 위험도, 제안과 종료 검사 |
| --- | --- | --- |
| QA-01 · `REPRODUCED` · P1 | 최초 유효 두 창 Host는 지도 닫힘/표시 GPU 32.09/33.25ms였다. V3-PERF-01의 최신 기준은 33.47/32.95ms였으며 고정 프레임 결합과 지도 배경 캡처 후 22.08/4.26ms로 줄었다. | 두 창의 지도 닫힘에서 60fps 예산 초과가 남는다. 산악 불투명과 SSAO·깊이 작업, 다른 기기와 뷰의 성능 범위는 후속 검증 대상이다. [전후 측정](../Playtest/ValidationSummary.md#v3-perf-01--월드와-전체-지도-렌더링) |
| QA-02 · `REPRODUCED` · P2 | V3-PERF-02에서 최초 메뉴 194,073→151,373 B/프레임을 측정했다. 산악 경계 생성의 델리게이트를 제거해 시작 구간과 최초 메뉴에서 합계 8.00MB를 줄였다. 대기실 Host의 전체 GC 중앙값은 1,864→1,608 B/프레임, 결과는 1,812→1,648 B/프레임이었다. | 산악 초기 지면·겹침 검사 배열과 엔진 초기화 비용이 남아 있다. 예열한 실제 창의 메뉴는 488 B/프레임이다. [비교와 계측 범위](../Playtest/ValidationSummary.md#v3-perf-02--cpu-gc-오디오와-자원-수명) |
| QA-03 · `INTERMITTENT` · P1 | PRES-08 headless Run-015의 Host가 복로 7→4에서 접지된 직후 `Plank_004` 중심 레이 검사를 실패했다. Run-021/023, PRES-09 022255와 격리 재실행은 각각 다른 붕괴 배치에서 통과했다. | V3-STABILITY-01의 동일 배치 주 검증 5회는 두 피어 모두 통과했다. 원본에서 실제 캡슐 접촉을 기록하지 않아 원인 미확정으로 남겼다. [검증](../Playtest/ValidationSummary.md#v3-stability-01--다리-지지-및-목재-운반) |
| QA-04 · `INTERMITTENT` · P1 | PRES-09 023322의 두 피어는 여덟 번째 설치를 확인한 뒤 아홉 번째 목재 운반(`Two-input carry 8`)에서 실패했다. 별도 Unity 배치 빌드가 동시에 실행됐다. 격리 재실행은 13개 설치와 복로 보행 30·달리기 점프 5회를 통과했다. | 원본 아홉 번째 실패의 원인은 미확정이다. 별도의 네 Player 비교에서 여덟 번째 운반의 같은 증상을 재현했다. 서버가 지연된 Client 복제 위치에 대해 다음 손잡이를 기존 2.5m 보유 범위 밖으로 옮긴 뒤 운반을 해제했다. 범위를 넘기는 이동을 보류하도록 수정했고 격리 및 네 Player 환경에서 8·9번째 운반을 각 10회 통과했다. V3-NET-BASELINE의 결정적 픽스처는 보류·실제 복제 후 재개·반납 분기를 직접 관측했다. [기존 재현](../Playtest/ValidationSummary.md#v3-stability-01--다리-지지-및-목재-운반) · [직접 분기 검증](../Playtest/ValidationSummary.md#다리-보류-가드) |
| QA-05 · `CONFIRMED_ISSUE` · P2 | 막대 공중의 수정 후 1,072표본은 1mm 미만. 착지→정지 전환은 최대 0.174m 손 오차가 남아 있다. | 포즈 전환/렌더 루트 가설. 사람이 얼마나 자주 볼지 미확인. 포즈만 조정하고 두 피어 이동·정지·역할 전환 거리와 기존 승인 기믹을 비교. 위험 중간. |
| QA-06 · `ENVIRONMENT_LIMITATION` | 실제 2560×1440 요청이 1920×1080 데스크톱에서 제한됨. 고정 RT 검사는 통과. | 1440p 디스플레이에서 창·전체화면, Esc/키패드·IME를 확인. 코드 변경 선행 조건 없음. |
| QA-07 · `RESOLVED` (캡처 한정) · P3 | 초기 지도 대체색. 동일 Editor의 두 번째 예열 공식 캡처에서 정상 표현 및 게시 해시 확인. | 원인 계측 없음. 실제 Player 정상 콜드 스타트에서 대체색이 보고될 경우 셰이더 로드와 Warmup을 구분. 월드 재질 변경 근거 없음. |
| QA-08 · `INTERMITTENT` (캡처 한정) · P3 | 최초 지하 복로 배너 동시 캡처 누락. 동일 공식 빌드 격리 재시도 성공. | 사건·UI 표시 프레임·캡처 시각을 대조할 때까지 Driver와 실제 UI의 구분 유지. 변경 위험 낮음(테스트 한정). |
| QA-09 · `RESOLVED` (테스트 한정) | PRES-10 최초 키패드 창 클릭 합산 검사 이력. Canvas 복귀 타이밍 조정 뒤 Run-KeypadVerified 양쪽 통과. | 실제 창 리그레션 재발 시 입력 포커스, Canvas 갱신 시각과 서버 revision을 함께 확인한다. |
| QA-10 · 오디오·단기 수명 및 네트워크 기준 | V3-PERF-02에서 Audio DSP·Streaming, UI 반복 전환과 자원을 측정했다. V3-NET-BASELINE에서 정상 Relay 대기실·이동의 피어별 UTP 송수신, RTT, 신뢰성 간극·재전송과 NGO RPC·변수 Δ·Transform 메시지 유형별 표본을 기록했다. | 개별 RPC와 변수·직렬화 바이트, 독립 와이어·장기 메모리와 Decode CPU는 `NOT_MEASURED`다. [측정과 한계](../Playtest/ValidationSummary.md#v3-net-baseline--network-traffic-and-resilience) |
| NET-01 · 정상 방 종료와 단절 대기 | Profile C에서 종료 요청 후 Host UTP가 약 0.13초에 닫혔고 Client는 명시적 사유 없이 30.27초 후 `Connection`으로 분류했다. 별도 Client 단절 시험에서 Host의 약 30초 동안 신뢰성 재전송을 관측했다. | 정상 종료는 C/D 각 3회에서 `HostLeft`와 새 방 참가를 검증했다. 통신이 차단된 경우 기존 약 30초 타임아웃과 신뢰성 재전송이 관측됐다. 전송 큐와 와이어 손실은 미계측이다. [재현과 검증](../Playtest/ValidationSummary.md#v3-net-close-01--graceful-session-shutdown) |
| QA-11 · `HUMAN_REVIEW_REQUIRED` | 별도 두 PC의 2·3차 사람 리뷰, 실제 1440p·IME와 청취·조작. | 제출을 기다리고 실제 세션, 하드웨어, 증상 빈도를 기록한다. 기존 자동화 자료는 비교 기준이다. |
| QA-12 · `DEFERRED` | 최신 전체 월드, 전체 경기, 전체 E2E Relay와 최종 배포. | V3-INTEGRATION과 별도 사용자 시작 조건. 혼합된 역사 빌드 결과를 현재 통합 PASS로 사용하지 않는다. |
| QA-13 · `SUSPECTED` · P3 | URP 17.3.0 대조에서 PC Renderer의 7개는 이전 APV 리소스 필드였고 현재 대응 리소스는 해석됐다. 기본 볼륨 프로필의 4개 스크립트 참조는 여전히 미해결이다. | 활성 패스 오류나 비용과의 연계가 확인되지 않아 직렬화 참조를 보존한다. 대형 packed 에셋의 전이·동적 사용 여부도 열린 검토 항목이다. |

### 후속 작업과 완료 조건

모든 구현은 별도 사용자 요청 이후 시작한다. 제안한 수치 기준은 후속 작업의 검증 목표다.

| Task / 경로 | Findings와 범위 | 완료 조건·집중 검증 | 선행 작업·위험 |
| --- | --- | --- | --- |
| V3-STABILITY-01 · Complex | QA-03/04 P1. 다리 건설/복로·서버 지지와 Driver. 원인 분리부터 수행. | 8번째 운반 포함 왕로 설치와 점프 7→4의 반복 격리 재생, 양쪽 프레임·서버·Collider·입력 증거. 수정 시 정상 및 원래 실패 조건에서 각각 최소 10회 연속 성공과 승인 경로 보존. 재현되지 않으면 상태 유지. | 저장 테스트 입력과 실패 증거. 물리/서버 변경 위험 높음. |
| V3-PERF-01 · Complex | QA-01 P1, QA-13 P3 관련. PC Renderer, 가시 메쉬·그림자·SSAO·UI 중 실측 병목. | 대표 고정 시점의 양쪽 지도 상태별 600+ 안정 프레임 × 3회, 1600×900 및 실제 1440p. 프레임 제한·단일/두 피어를 분리하고 GPU·draw·P95로 전후 비교. 60fps 목표(16.67ms) 대비 제한 요인과 변경한 비용 감소를 수치로 기록. 승인 외관·충돌 보존. | 타임라인·Frame Debugger로 패스 확정. GPU/외관 변경 위험 중간. |
| V3-PERF-02 · Standard · 수행 | QA-02/10 P2. 시작과 정상 메뉴, 대기실·결과, HUD·지도·키패드, 오디오와 반복 전환을 계측했다. | 산악 경계의 델리게이트와 대기실·결과 문자열의 반복 할당을 제거했다. 대응 D3D11 표본과 10회 반복, 직접 Relay 및 일반 Player 검증을 기록했다. | 오디오 DSP·Streaming 측정. 독립 Decode CPU와 장기 수명 분석은 미계측이다. [검증 결과](../Playtest/ValidationSummary.md#v3-perf-02--cpu-gc-오디오와-자원-수명) |
| V3-NET-BASELINE · Standard | QA-10. NGO/Transport의 RTT·traffic·RPC/변수 계측, 안정 회선 및 임페어먼트 재입장/재경기. | 두 피어별 30초 이상 송수신/RTT/메시지 유형 표본, 손실·재전송 지표의 층위 기록. 기본/지연·손실 프로필별 최소 두 차례 Runtime Ready, Finished 재입장, 한 번의 재경기 및 이전 요청 거부 확인. 측정으로 확인된 문제가 있을 때만 수정 작업 분리. | 격리 2피어·정상 기준과 지원 계측. 네트워크 코드 변경 없음. |
| V3-NET-CLOSE-01 · Complex | NET-01. 종료 사유 전달과 Host·Client 세션 정리 순서. | 실제 Relay C/D 각 3회 연속 `HostLeft`와 새 방 참가, 도달 불가 타임아웃, Outbound·Finished 재입장과 재경기, 일반 D3D11 Player를 확인한다. | [계측과 결과](../Playtest/ValidationSummary.md#v3-net-close-01--graceful-session-shutdown). |
| V3-PRES-FOLLOWUP · Standard | QA-05/08 P2/P3. 막대 손 그립 잔차와 지하 캡처 시각. | 같은 두 피어의 착지/정지/역할 변경 로그의 최대 17.4cm 잔차 감소와 시각 확인, 공중 그립 1mm 로그 정밀도 유지. 배너 시작·표시·캡처 시각을 각 피어에서 기록. | 포즈/캡처 원인 분리. 서버 판정 변경 없이 표현만 변경. 위험 중간. |
| V3-FEEDBACK-FINAL · Standard | QA-06/11. 2·3차 제출, 실제 사람의 입력·시각·청취·진행·기기 피드백. | 별도 두 PC의 체크리스트와 실제 1440p 표시 기록을 수집하고, 재현 가능한 보고만 후속 범위로 확정. | 제출 대기. 빌드·코드 변경은 별도 승인. |
| V3-INTEGRATION · Complex | QA-12. 승인 주요 작업 이후 최신 전체 월드·전체 경기·전체 E2E Relay 및 최종 Windows Player 통합 검증. | 기존 ROADMAP 승인 조건과 Playtest Guide L5, 결과 및 범위 근거 기록. | 사용자 명시 시작과 주요 개발 완료. 기존 보류 유지. |
| V3-RELEASE · Workflow | 통과한 최종 통합 기준의 승인 배포. | 승인 빌드와 배포 ZIP의 구성·고지·해시 및 원격 소스 대조. | V3-INTEGRATION 통과와 배포 승인. |
