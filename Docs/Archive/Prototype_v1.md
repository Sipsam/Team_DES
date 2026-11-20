# 프로토타입과 V1/V2 복원

프로토타입 전체 트리는 기존 `prototype` 태그(`327c6a2aa730a2c0dd3e5cb3f69342014778e31e`)로 유지된다. 정리 직전의 MainPrototype과 V1·V2 블록아웃, 생성 에셋, 구버전 도구는 `c4b6e942d7102dc532dab0c3d87c2ebd5bccf61a` 트리에서 확인할 수 있다.

```powershell
git fetch origin --tags
git worktree add --detach ../Team_DES-prototype prototype
git worktree add --detach ../Team_DES-pre-cleanup c4b6e942d7102dc532dab0c3d87c2ebd5bccf61a
```

Unity 6000.3.27f1에서 별도 작업 폴더를 열고 필요한 씬을 선택한다. 프로토타입에는 첫 도착 방식의 승리, 구형 뗏목·다리·문·점프 기믹이 있으며, 현재 V3의 승리 조건과 이동 경로는 [게임플레이 시스템](../Architecture/GameplaySystems.md)에 기록되어 있다. 오래된 로컬 검증 원본은 저장소 커밋과 별개이며 현재 실행과 빌드에는 필요하지 않다.
