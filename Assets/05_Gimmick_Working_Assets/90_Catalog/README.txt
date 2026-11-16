# 지금 사용하는 파일 안내

현재 작업 폴더: Assets/05_Gimmick_Working_Assets
00_Shared_Characters → 01_Bridge_Building → 02_Raft_Traversal → 03_Information_Sharing_Doors → 04_Jump_Map_Treasure → 05_Shared_Environment.
90_Catalog은 설명서, 91_Materials은 색/표면 설정입니다. 66개 배치용 프리팹을 기믹별로 합쳤습니다.
이전 01~04 폴더에는 참조하는 원본 메시·재질이 있으므로 아직 삭제하지 마세요.
MedievalKitBuild는 자동 검증용 복사본입니다. 평소에는 UnityAssetPreview만 사용하세요.
이전 날짜/회차별 ZIP은 이전 전달본입니다. 현재 전달 ZIP은 양날의검_기믹별_동작에셋_최신.zip입니다.

## Unity에서 확인
현재 Play 중이면 종료하고 새 폴더의 GimmickBehaviorReview.unity를 다시 여세요.
Play → Game → Next / Previous로 개별 확인. 버튼이 안 보이면 Game 해상도를 Free Aspect로 설정하세요.
씬의 E/S 번호는 이전 검토 배치 번호입니다. 현재 폴더의 필수/선택 구분을 뜻하지 않습니다.
검토 씬은 배열을 그대로 보존했고 이번 변경은 파일 폴더 분류입니다.

## 파일 확장자
.fbx: 모델 메시, 경우에 따라 뼈대와 애니메이션. FBX라고 반드시 애니메이션이 있는 것은 아닙니다.
.glb: 메시·재질·텍스처 등을 묶는 바이너리 glTF. Unity에서는 이 프로젝트의 glTFast 임포터를 사용합니다.
.gltf: glTF의 JSON 설명 파일. 별도 .bin과 이미지 파일을 함께 요구할 수 있습니다.
.obj: 주로 정적 메시 형태. 이번 직접 제작 OBJ에는 뼈대/동작 스크립트가 없습니다.
.mtl: OBJ의 재질 설명. OBJ 옆에 유지해야 합니다. Unity .mat과 다른 파일입니다.
.png/.jpg: 텍스처 또는 미리보기 이미지. 어느 용도인지는 폴더와 참조를 확인하세요.
.mat: Unity 재질. 모델의 색·텍스처·광택·셰이더 설정을 저장합니다.
.prefab: 메시·재질·자식 부품·컴포넌트를 조립한 Unity 배치용 파일. 다른 파일을 참조하므로 이것만 전달하면 불완전할 수 있습니다.
.unity: 오브젝트 위치·카메라·조명 등을 저장한 씬.
.cs: C# 코드. 움직임·화면 버튼·기믹 동작을 구현합니다.
.shader: GPU 표면 표현 코드. 현재 물·용암의 색 흐름 효과에 사용합니다.
.meta: Unity 식별자(GUID)와 임포트 설정. 원본 파일과 함께 보존해야 합니다.
.asset: Unity가 저장하는 데이터. 이번에는 문양에 쓰는 메시 등의 데이터도 있습니다.
.anim/.controller: 애니메이션 클립/Animator 상태 연결 파일. 이번 18개 효과는 새 .anim을 만든 방식이 아닙니다.
.unitypackage: 참조 파일과 메타데이터를 함께 묶은 Unity 가져오기 패키지.
.zip: 패키지·설명서·출처·미리보기를 한 번에 전달하는 일반 압축 파일.
.md/.txt/.json: 사람이 읽는 설명서 또는 프로그램이 읽는 목록 데이터. 모델 자체가 아닙니다.
.gif: 동작을 녹화한 참고 이미지. Unity 안의 동작을 만들어 주는 파일이 아닙니다.

## 현재 애니메이션 구현
MedievalMotionPreview.cs가 시간에 따라 Transform의 위치·회전을 바꾸거나 Renderer를 껐다 켭니다. 18개 시각 미리보기입니다.
문은 좌우 문짝 축 회전, 이동발판은 좌우 왕복, 소멸발판은 흔들림 후 Renderer 숨김/복원입니다.
뗏목은 RaftPair.cs의 SplitForReturn/JoinForOutbound를 호출해 두 뗏목을 벌리고 결속 밧줄을 숨깁니다.
물고기/악어는 전체 모델의 이동이며 뼈대 수영 애니메이션이 아닙니다.
물·용암은 MedievalFlow.shader, 불꽃은 ParticleSystem을 사용합니다.
배치용 prefab에서는 MedievalMotionPreview가 꺼져 있고 검토 씬에서만 켜져 있습니다.
콜라이더와 피해·탑승·승객 이동·퍼즐 정답·네트워크 동기화는 이 시각 미리보기에 연결되어 있지 않습니다.
특히 소멸 시 Renderer만 숨기므로 게임용 발판 소멸에는 Collider와 리스폰 처리도 별도로 필요합니다.
캐릭터 걷기·점프·운반·노젓기, 동물 관절 수영/공격은 아직 적용하지 않았습니다.

## 팀 전달
최신 ZIP 안의 기믹별_동작에셋.unitypackage를 Import하고 출처와라이선스를 함께 보관하세요.
Unity 6000.5.4f1 / Built-in 기준. glTFast 6.12.1과 physics, particlesystem, animation, imgui, audio 기본 모듈이 필요합니다.
URP/HDRP 변환은 별도입니다. 이 패키지는 완성 게임이 아니라 외형과 동작 검토용입니다.
