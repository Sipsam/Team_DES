# 캐릭터 모션 테스트

현재 캐릭터 FBX 안에 들어 있던 모션을 재사용했습니다. 새로 생성한 모션이 아닙니다.

원본: Assets/02_Medieval_Fantasy_Models/00_Characters/01_Characters/CHAR_PLAYER_BASE_01.fbx

확인한 FBX 모션: CharacterArmature|Roll, CharacterArmature|Walk, CharacterArmature|Shoot_OneHanded, CharacterArmature|RecieveHit, CharacterArmature|Punch, CharacterArmature|StandUp, CharacterArmature|Walk_Carry, CharacterArmature|Jump, CharacterArmature|SitDown, CharacterArmature|Defeat, CharacterArmature|Run, CharacterArmature|SwordSlash, CharacterArmature|Death, CharacterArmature|Victory, CharacterArmature|PickUp, CharacterArmature|Run_Carry, CharacterArmature|Idle

- Idle: 원본 CharacterArmature|Idle, 4.17초. 테스트용 반복 재생.
- Walk: 원본 CharacterArmature|Walk, 1.25초. 테스트용 반복 재생.
- Jump: 원본 CharacterArmature|Jump, 1.04초. 테스트용 반복 재생.
- Run: 원본 CharacterArmature|Run, 0.88초. 테스트용 반복 재생.
- Walk_Carry: 원본 CharacterArmature|Walk_Carry, 1.25초. 테스트용 반복 재생.

검증 Idle: 스킨 메시 변화량 7.2518

검증 Walk: 스킨 메시 변화량 616.22

검증 Jump: 스킨 메시 변화량 2220.4

검증 Run: 스킨 메시 변화량 1163.8

검증 Walk_Carry: 스킨 메시 변화량 149.17

## 사용법
CharacterAnimationPreview 씬 → Play → Game → Idle/Walk/Jump/Run/Walk_Carry 버튼. Game 해상도 Free Aspect 권장. Turn 45로 방향, Pause로 일시 정지, Speed로 속도 조절.

.anim은 FBX에서 복사한 독립 모션, .controller는 5개 상태, CharacterWithAnimation.prefab은 Animator를 연결한 캐릭터입니다. CharacterMotionDemo.cs는 버튼 선택을 담당합니다.

이번에는 제자리 모션 확인입니다. WASD 이동·실제 점프 물리·목재 잡기·노젓기·네트워크 동기화는 포함하지 않습니다. Jump도 확인을 위해 반복합니다. 망토와 벨트는 관절에 붙인 단단한 장식이며 천 시뮬레이션이 아닙니다. 원본 캐릭터와 기존 에셋은 유지했습니다.
