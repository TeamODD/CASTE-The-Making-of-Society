# CASTE - The Making of Society

동아리에서 만드는 소규모 Unity 2D 카드 게임이다. 플레이어는 계층별 사건에서 좌우 선택을 하고, 그 결과로 달라진 사회 스탯과 유산을 다음 계층에 가져간다. 게임은 오프라인으로 운영한다.

백엔드 담당 범위는 카드 진행, 스탯과 유산, 로컬 저장·복원이다. 프론트 담당자가 만든 UI에서 이 기능을 호출하고 결과를 표시할 수 있도록 Unity 쪽 연결 지점을 제공한다.

## 저장소 안내

```text
.
├── AGENTS.md                                 → 프로젝트 작업 지침
├── CLAUDE.md                                 → 프로젝트 작업 지침
├── Assets/
│   └── JMS/
│       └── AGENTS.md                         → 카드 진행과 저장 코드 작업 지침
└── docs/
    ├── architecture.md                       → 씬, 카드 진행, 저장 사이의 흐름
    ├── business-rules.md                     → 계층, 턴, 사건 순서와 복귀 규칙
    ├── security.md                           → 오프라인 운영과 저장 데이터 범위
    ├── standards.md                          → 빌드, 검증, 파일 관리 기준
    ├── engineering-notes.md                  → 테스트 씬과 저장에서 주의할 점
    ├── operations.md                         → 에디터 실행과 검증 절차
    ├── contracts.md                          → 카드 CSV, 씬 연결과 저장 형식
    └── tracking/
        ├── status.md                         → 현재 구현과 남은 작업
        ├── findings.md                       → 확인된 문제와 영향
        └── decisions/
            ├── index.md                      → 설계 결정 목록
            ├── 0001-caste-turns.md            → 계층 턴과 메인 사건
            ├── 0002-checkpoint-transition.md  → 체크포인트와 전환 알림
            ├── 0003-offline-play.md           → 오프라인 플레이
            └── 0004-game-ui-boundary.md       → 게임 로직과 UI의 연결 경계
```

## 반드시 지킬 사항

- 빌드는 명시적으로 요청받았을 때만 실행한다.
- 한글 인코딩이 넓은 범위에서 깨져 보여도 일괄 변환하지 않는다.
- 실패한 검사는 원인을 해결한 뒤 다시 확인하고, 직접 실행한 테스트 프로세스는 턴 종료 전에 닫는다.
- 게임은 오프라인으로 운영하고 플레이 진행을 기기에 저장한다.
- 릴리스에 배포했다면 이전 빌드본을 삭제한다.

## 작업 전 확인

`docs/standards.md`, `docs/engineering-notes.md`와 수정할 모듈의 `AGENTS.md`를 먼저 확인한다. 카드 진행을 바꿀 때는 `docs/business-rules.md`의 메인 사건 순서와 턴 예약 조건을 확인한다. 저장이나 화면 이벤트를 바꿀 때는 `docs/contracts.md`에서 저장 성공과 알림 순서를 확인한다. 씬 실행과 패키지 복원은 `docs/operations.md`의 절차를 따른다.

진행 세이브 유실이나 게임 스크립트 전체의 컴파일 차단을 발견하면 바로 사용자에게 알린다. 해결되지 않은 다른 문제는 재현 조건과 영향 범위를 `docs/tracking/findings.md`에 남긴다.
