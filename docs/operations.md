# Windows에서 열고 확인하기

## 준비

Unity Hub에서 `6000.3.15f1`을 설치한다. 저장소 루트에서 아래 PowerShell 명령으로 에디터를 연다. Hub의 설치 경로를 바꿨다면 첫 줄을 해당 경로로 바꾼다.

```powershell
$CasteUnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe'
& $CasteUnityEditor -projectPath (Get-Location).Path
```

첫 실행 시 Unity 패키지와 CsvHelper를 복원한다. 개발 환경의 패키지 복원에는 Unity 패키지 레지스트리, GitHub의 NuGetForUnity 소스, nuget.org 접속이 필요할 수 있다. 패키지가 빠졌다면 `Assets/packages.config`에서 CsvHelper `33.1.0`을 확인하고, 복원 후 `Assets/Packages/CsvHelper.33.1.0/lib/netstandard2.1/CsvHelper.dll`이 있는지 확인한다.

## 카드와 저장 검증

1. Unity 콘솔에서 스크립트 컴파일 오류를 먼저 확인한다.
2. `Assets/JMS/Scenes/Test.unity`를 열고 `Test Environment`의 `Test`가 `events.csv`를 참조하는지, `GameManager`가 비활성화되어 있는지 확인한다.
3. 플레이 모드에서 검사별 결과와 임시 JSON을 확인한다. 기존 파일과 충돌하지 않는 임시 저장 경로를 사용한다.
4. 플레이 모드를 끄고, 검증을 위해 별도로 실행한 테스트 프로세스를 종료한다.

## 빌드와 배포

명시적으로 빌드를 요청받은 경우 Unity의 빌드 설정에서 대상 플랫폼과 씬 목록을 확인한 뒤 실행한다. 시작 씬 `SampleScene.unity`에 연결된 `GameManager`가 카드 CSV를 읽으며, 플레이 화면 검증에는 프론트 UI 연결이 필요하다. 확인된 배포 채널과 배포 절차는 없다. 릴리스 배포를 했다면 이전 빌드본을 삭제한다.
