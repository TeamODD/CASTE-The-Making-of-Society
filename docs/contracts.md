# 콘텐츠와 게임 화면의 연결 계약

## 카드 CSV

입력은 `GameManager`에 연결된 CSV 텍스트다. 각 행은 `event_id`와 고유한 `variant_id`, 계층 코드·이름·순서, 사건 유형, 유산 조건, 제목·화자·대사, 좌우 선택 텍스트와 각 스탯의 변화량 및 선택 플래그를 담는다. 좌우 변화량의 빈 칸은 0으로 읽는다. 정수가 아닌 숫자 필드와 필수 열이 빠진 파일은 파싱 오류가 날 수 있다.

사건 ID 조회는 해당 사건의 변형 목록을 반환하고, 변형 ID 조회는 해당 카드 하나를 반환한다. 등록되지 않은 사건의 변형 목록은 비어 있고, 없는 변형 ID의 결과는 `null`이다.

## 씬에서 호출하는 메서드와 이벤트

UI는 확정한 입력을 `GameManager`에 전달한다. 화면이 활성화되면 `GameManager.State`에서 현재 상태를 읽고 `GameStateChanged`를 구독해 카드와 게이지를 갱신할 수 있다. `GetCurrentCard()`와 `GetStats()`에서 표시할 값을 읽는다. 입력 미리보기는 카드의 선택지 정보를 읽을 수 있다. 실제 스탯 변경과 다음 카드 선택은 확정 입력을 받은 세션에서 수행한다.

시작 화면은 `CanResume`을 읽어 이어하기 버튼을 활성화하고, 버튼에 `StartNewGame()`과 `ResumeGame()`을 연결한다. `GameManager.State`는 첫 시작 또는 이어하기가 성공하면 제공된다.

| 연결 지점 | 입력 | 결과와 실패 |
| --- | --- | --- |
| `GameManager.GameStateReady` | 시작에 사용할 CSV와 로컬 저장 상태 | 시작이나 이어하기가 끝나면 `GameState`를 전달한다. 파싱 또는 저장 로드가 실패하면 초기화 예외가 먼저 발생한다. |
| `GameManager.GameStateChanged` | 콜백 구독 | 새 게임, 이어하기, 선택, 체크포인트 복원 후 갱신된 상태를 한 번 전달한다. 현재 카드와 스탯은 같은 상태에서 읽는다. |
| `StartNewGame()` | Inspector의 턴 설정과 연결된 CSV | 첫 카드와 체크포인트를 저장해 기존 진행을 교체한다. 저장 실패 시 이전 상태가 유지된다. |
| `CanResume` | 로컬 저장 경로 | 세이브 파일이 있으면 `true`를 반환한다. 파일 검증은 이어하기를 요청할 때 수행한다. |
| `ResumeGame()` | 연결된 CSV와 로컬 저장 | 저장된 진행을 읽어 표시한다. 파일이 없으면 상태와 파일을 그대로 두며, 손상된 파일은 새 진행으로 교체한다. |
| `ChooseLeft()`, `ChooseRight()` | 초기화된 매니저에 좌우 입력 한 번 | 현재 카드의 해당 선택을 처리한다. 유효한 선택지가 없으면 세션은 선택을 건너뛴다. 저장 오류는 호출 경로로 전달된다. |
| `GameState.CurrentCardChanged` | 콜백 구독 | 카드 참조를 갱신하면 인자 없이 호출된다. 구독자는 상태에서 현재 카드를 읽으며 카드가 비어 있으면 `null`을 받는다. |
| `BackToStart()` | 초기화된 매니저 | 가장 최근 체크포인트 복원을 요청한다. 체크포인트가 없으면 세션은 `false`를 반환하고 매니저는 결과를 반환하지 않는다. |
| `GameManager.CasteChanged(previousCasteOrder, nextCasteOrder)` | 유효한 선택에 따른 계층 진입 | 새 계층의 첫 카드와 체크포인트 저장이 성공하면 `CurrentCardChanged`로 카드를 먼저 알리고, 이어서 이전·새 계층 순서를 한 번 전달한다. |

저장에 성공한 뒤 카드와 상태를 갱신한다. 저장에 실패하면 기존 진행 상태를 유지해 같은 선택을 다시 시도할 수 있게 한다. 같은 계층에서의 선택과 체크포인트 복원은 `CurrentCardChanged`로 반영한다. 새 게임과 이어하기는 `GameStateReady` 이후 `GameStateChanged`를 보낸다. 마지막 계층을 마치면 `GetCurrentCard()`는 `null`을 반환한다.

## 저장 파일

기본 경로는 `Application.persistentDataPath/save.json`이다. 버전 1 JSON은 `schemaVersion`, `checkpoints`, `progress`를 포함한다. 진행에는 계층 순서, 사용 턴, 적용된 턴 설정, 현재 카드 변형 ID, 완료 여부, 스탯과 플래그, 본 사건 ID를 담는다. 체크포인트에는 진입 당시 스탯과 플래그, 계층 정보, 첫 카드 변형 ID가 있다.

구형 저장의 `currentCard` 객체는 변형 ID로 읽는다. 로더는 파일이 없으면 `null`을 반환하고 잘못된 형식은 `InvalidDataException`으로 보고한다. 손상 파일은 이어하기 중 같은 경로의 새 저장으로 교체하고, 경로 접근이나 교체 실패는 호출자가 받는다.
