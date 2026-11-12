using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class Test : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private GameManager gameManager;
    [Tooltip("수동 테스트에 사용할 CSV variant_id입니다. 실제 게임의 첫 카드 선택 규칙과는 별개입니다.")]
    [SerializeField] private string demoVariantId = "B-M04";

    private CardDatabase cardDatabase;
    private GameSession testSession;

    private int passed;
    private int failed;
    private readonly List<(string name, string error)> results = new();
    private Font dashboardFont;
    private CardData demoCard;
    private string demoSavePath;
    private string saveBeforeAction;
    private GameState testState;
    private UnityEngine.UI.Text demoStatus;
    private readonly UnityEngine.UI.Text[] previousStatValues = new UnityEngine.UI.Text[4];
    private readonly UnityEngine.UI.Text[] currentStatValues = new UnityEngine.UI.Text[4];
    private UnityEngine.UI.Button leftButton;
    private UnityEngine.UI.Button rightButton;
    private UnityEngine.UI.Button restoreButton;
    private UnityEngine.UI.Text reportSummary;
    private UnityEngine.UI.Text reportDetails;
    private UnityEngine.UI.Text saveBeforeText;
    private UnityEngine.UI.Text saveCurrentText;
    private GameState observedState;
    private int cardChangedCount;

    private const string PendingFeatures =
        "미구현 · 자동 검증 통과에 포함하지 않음\n" +
        "• 새 게임의 첫 카드 자동 지정\n" +
        "• 조건에 맞는 다음 카드 선택\n" +
        "• 계층 전환과 체크포인트 자동 생성\n" +
        "• 게임 오버 판정과 자동 복원\n\n" +
        "이 화면은 GameSession을 직접 호출합니다.\n" +
        "GameManager 초기화·스와이프 입력은 검증 대상에서 제외합니다.";

    private void Start()
    {
        RunAllChecks();
        ShowDashboard();
    }

    private void RunAllChecks()
    {
        GameSession demoSession = testSession;
        GameState demoState = testState;
        passed = failed = 0;
        results.Clear();
        try
        {
            RunCase("테스트 씬과 CSV 연결", () =>
            {
                Require(csvFile != null, "Test의 CSV 파일을 연결해 주세요.");
                Require(gameManager != null && !gameManager.enabled,
                    "실제 세이브 보호를 위해 이 씬의 GameManager는 비활성화해야 합니다.");
                cardDatabase = new CardDatabase(csvFile.text);
                demoCard = cardDatabase.FindByVariantId(demoVariantId);
                Require(demoCard != null, $"테스트 카드 ID를 찾을 수 없습니다: {demoVariantId}");
            });
            RunCase("CSV 전체 카드 / 사건별 변형 조회", CheckCards);
            RunCase("기본 스탯 / 합산 / 0~100 제한", CheckStats);
            RunCase("새 게임 / 저장 데이터와 상태 분리", CheckNewGame);
            RunCase("플래그 / 계층 기록 / 복사본 보호", CheckFlagsAndHistory);
            RunCase("현재 카드 변경 알림 / 구독 해제", CheckCardNotifications);
            RunCase("왼쪽 선택 → 효과 / 기록 / 자동 저장", () =>
                CheckSessionChoice(false, Stat.Default, new Stat(57, 52, 53, 58), "zeal"));
            RunCase("오른쪽 선택 → 효과 / 기록 / 자동 저장", () =>
                CheckSessionChoice(true, Stat.Default, new Stat(46, 48, 44, 42), "mercy"));
            RunCase("실제 선택에서 스탯 상한 / 하한", CheckChoices);
            RunCase("현재 카드가 없을 때 선택 무시", CheckEmptyChoice);
            RunCase("JSON 덮어쓰기 / 정확한 카드 이어하기 / 복원", CheckSaveAndRestore);
            RunCase("progress 재사용 / 저장 스냅샷 분리", CheckProgressSnapshot);
            RunCase("체크포인트가 없을 때 복원 거부", CheckMissingCheckpoint);
            RunCase("손상 JSON / 잘못된 카드 ID 거부", CheckInvalidSaves);
            RunCase("필수 progress가 없는 JSON 거부", CheckIncompleteSave);
        }
        finally
        {
            testSession = demoSession;
            testState = demoState;
            RefreshReport();
        }

        string summary = $"[Integration Test] {passed} passed, {failed} failed. 4 pending features excluded.";
        if (failed == 0)
            Debug.Log(summary, this);
        else
            Debug.LogError(summary, this);
    }

    private void RefreshReport()
    {
        if (reportSummary != null)
        {
            reportSummary.text = $"자동 검증  {passed} 통과 · {failed} 실패";
            reportSummary.color = failed == 0
                ? new Color32(108, 224, 164, 255) : new Color32(255, 135, 135, 255);
        }
        if (reportDetails != null)
        {
            reportDetails.text = string.Join("\n\n", results.OrderBy(result => result.error == null).Select(result =>
                result.error == null ? $"PASS  {result.name}" : $"FAIL  {result.name}\n{result.error}"))
                + "\n\n────────────\n" + PendingFeatures;
        }
    }

    private void OnDestroy()
    {
        if (observedState != null)
            observedState.CurrentCardChanged -= OnDemoCardChanged;
        if (demoSavePath != null)
            RemoveTemporarySave(demoSavePath);
        if (dashboardFont != null)
            Destroy(dashboardFont);
    }

    private void ShowDashboard()
    {
        dashboardFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
        var canvasObject = new GameObject("Test Dashboard", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        scaler.matchWidthOrHeight = 0.5f;

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var input = new GameObject("Test Input", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            input.transform.SetParent(transform, false);
        }

        RectTransform background = MakeRect(canvas.transform, "Background", 0, 0, 1, 1);
        background.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(16, 23, 34, 255);
        Label(background, "CASTE / 기능 테스트", 30, 0.04f, 0.90f, 0.49f, 0.98f);
        MakeButton(background, "자동 검사 다시 실행", 0.52f, 0.91f, 0.74f, 0.97f, RunAllChecks);

        var report = Panel(background, "자동 검증", 0.04f, 0.05f, 0.38f, 0.875f);
        reportSummary = Label(report, string.Empty, 23, 0.05f, 0.89f, 0.95f, 0.98f);
        Label(report, "미구현 4 항목 별도 표시 · 아래로 스크롤", 15, 0.05f, 0.80f, 0.95f, 0.89f)
            .color = new Color32(247, 197, 109, 255);
        var (_, details) = MakeSavePane(report, "검사 결과 / 실패 원인", 0.04f, 0.96f);
        reportDetails = details;
        RefreshReport();

        var demo = Panel(background, "수동 확인", 0.40f, 0.05f, 0.96f, 0.875f);
        Label(demo, "실제 진행 코드 · 수동 확인", 24, 0.04f, 0.93f, 0.96f, 0.995f);
        Label(demo, $"테스트 카드  {demoCard?.variantId ?? demoVariantId} / {demoCard?.title}",
            20, 0.04f, 0.86f, 0.96f, 0.93f);
        Label(demo, $"화자  {demoCard?.speaker}", 17, 0.04f, 0.81f, 0.96f, 0.86f)
            .color = new Color32(173, 201, 229, 255);
        Label(demo, demoCard?.dialogue ?? "CSV 연결과 테스트 카드 ID를 확인해 주세요.",
            17, 0.04f, 0.65f, 0.96f, 0.81f);
        Label(demo, $"왼쪽  {demoCard?.leftChoice?.text}\n오른쪽  {demoCard?.rightChoice?.text}",
            15, 0.04f, 0.56f, 0.96f, 0.65f);

        string[] names = { "신앙", "치안", "경제", "불만" };
        for (int i = 0; i < names.Length; i++)
        {
            float left = 0.04f + i * 0.235f;
            Label(demo, names[i] + " · 현재", 16, left, 0.51f, left + 0.215f, 0.56f);
            currentStatValues[i] = Label(demo, "—", 36, left, 0.44f, left + 0.215f, 0.51f);
            currentStatValues[i].color = new Color32(108, 224, 164, 255);
            previousStatValues[i] = Label(demo, "이전 —", 14, left, 0.40f, left + 0.215f, 0.44f);
        }

        demoStatus = Label(demo, string.Empty, 15, 0.04f, 0.20f, 0.96f, 0.39f);
        leftButton = MakeButton(demo, "왼쪽 선택", 0.04f, 0.115f, 0.33f, 0.185f, () => ChooseDemo(false));
        rightButton = MakeButton(demo, "오른쪽 선택", 0.355f, 0.115f, 0.645f, 0.185f, () => ChooseDemo(true));
        restoreButton = MakeButton(demo, "체크포인트 복원", 0.67f, 0.115f, 0.96f, 0.185f, RestoreDemo);
        MakeButton(demo, "테스트 초기화", 0.04f, 0.025f, 0.33f, 0.095f, ResetDemo);
        MakeButton(demo, "저장 파일로 이어하기", 0.355f, 0.025f, 0.645f, 0.095f, ResumeDemo);
        MakeButton(demo, "테스트 카드만 지정", 0.67f, 0.025f, 0.96f, 0.095f, AssignDemoCard);

        demoSavePath = CreateTemporarySavePath();
        AddSaveInspector(background, report, demo);
        if (demoCard != null && gameManager != null && !gameManager.enabled)
            ResetDemo();
        else
        {
            leftButton.interactable = rightButton.interactable = restoreButton.interactable = false;
            demoStatus.text = "테스트 초기화 실패: 왼쪽 검사 결과를 확인해 주세요.";
        }
    }

    private void AddSaveInspector(RectTransform background, RectTransform report, RectTransform demo)
    {
        var inspector = Panel(background, "저장 파일", 0.04f, 0.05f, 0.96f, 0.875f);
        Label(inspector, "임시 세이브 경로", 20, 0.025f, 0.90f, 0.97f, 0.98f);
        Label(inspector, demoSavePath.Replace('\\', '/'), 17, 0.025f, 0.83f, 0.97f, 0.90f);
        Label(inspector, "스크롤로 save.json 전체 보기 · Play 모드를 종료하면 임시 파일이 삭제됩니다.",
            15, 0.025f, 0.77f, 0.97f, 0.83f).color = new Color32(168, 187, 208, 255);

        var (beforeScroll, beforeText) = MakeSavePane(inspector, "최근 동작 직전", 0.025f, 0.49f);
        var (currentScroll, currentText) = MakeSavePane(inspector, "현재 파일", 0.51f, 0.975f);
        saveBeforeText = beforeText;
        saveCurrentText = currentText;
        inspector.gameObject.SetActive(false);

        UnityEngine.UI.Text toggleLabel = null;
        var toggle = MakeButton(background, "저장 파일 보기", 0.76f, 0.91f, 0.96f, 0.97f, () =>
        {
            bool open = !inspector.gameObject.activeSelf;
            if (open)
            {
                RefreshSaveInspector();
            }

            report.gameObject.SetActive(!open);
            demo.gameObject.SetActive(!open);
            inspector.gameObject.SetActive(open);
            toggleLabel.text = open ? "테스트 화면" : "저장 파일 보기";
            if (open)
            {
                Canvas.ForceUpdateCanvases();
                beforeScroll.verticalNormalizedPosition = 1;
                currentScroll.verticalNormalizedPosition = 1;
            }
        });
        toggleLabel = toggle.GetComponentInChildren<UnityEngine.UI.Text>();
    }

    private void RefreshSaveInspector()
    {
        if (saveBeforeText != null)
            saveBeforeText.text = saveBeforeAction ?? "아직 이전 저장 내용이 없습니다.";
        if (saveCurrentText != null)
            saveCurrentText.text = File.Exists(demoSavePath) ? File.ReadAllText(demoSavePath) : "저장 파일이 없습니다.";
    }

    private (UnityEngine.UI.ScrollRect scroll, UnityEngine.UI.Text text) MakeSavePane(
        RectTransform parent, string heading, float left, float right)
    {
        var pane = Panel(parent, heading, left, 0.04f, right, 0.75f);
        Label(pane, heading, 19, 0.025f, 0.90f, 0.96f, 1);

        var scroll = MakeRect(pane, "JSON Scroll", 0, 0, 1, 0.89f)
            .gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = Panel(scroll.transform, "Viewport", 0, 0, 1, 1);
        var viewportImage = viewport.GetComponent<UnityEngine.UI.Image>();
        viewportImage.color = new Color32(16, 23, 34, 255);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();

        var content = MakeRect(viewport, "Content", 0, 1, 1, 1);
        content.pivot = new Vector2(0.5f, 1);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        var text = Label(content, string.Empty, 16, 0, 0, 1, 1);
        text.alignment = TextAnchor.UpperLeft;
        text.resizeTextForBestFit = false;
        text.supportRichText = false;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24;
        return (scroll, text);
    }

    private void ResetDemo()
    {
        Require(demoCard != null, "테스트 카드 설정을 확인해 주세요.");
        Stat before = testState?.GetStats() ?? Stat.Default;
        CaptureSaveBeforeAction();
        WriteTemporarySave(CreateSaveFixture(demoCard, Stat.Default), demoSavePath);
        cardChangedCount = 0;
        InitializeGame(demoSavePath, observe: true);
        ShowDemoState(before, "테스트 초기화: 스탯 50 / 계층 체크포인트 / 테스트 카드 지정");
    }

    private void ChooseDemo(bool right)
    {
        Require(testState?.GetCurrentCard() != null, "먼저 테스트 카드를 지정해 주세요.");
        Stat before = testState.GetStats();
        CaptureSaveBeforeAction();
        testSession.Choose(right);
        ShowDemoState(before, right ? "오른쪽 선택 → GameSession 자동 저장 완료" : "왼쪽 선택 → GameSession 자동 저장 완료");
    }

    private void RestoreDemo()
    {
        Require(testSession != null, "먼저 테스트를 초기화해 주세요.");
        Stat before = testState.GetStats();
        CaptureSaveBeforeAction();
        RestoreTestCheckpoint();
        ShowDemoState(before, "체크포인트 복원 → 저장 완료 (카드는 자동 지정하지 않음)");
    }

    private void ResumeDemo()
    {
        Require(File.Exists(demoSavePath), "이어할 임시 저장 파일이 없습니다.");
        Stat before = testState?.GetStats() ?? Stat.Default;
        CaptureSaveBeforeAction();
        InitializeGame(demoSavePath, observe: true);
        ShowDemoState(before, "새 GameSession이 저장 파일에서 이어하기 완료");
    }

    private void AssignDemoCard()
    {
        Require(testSession != null && demoCard != null, "먼저 테스트를 초기화해 주세요.");
        Stat before = testState.GetStats();
        CaptureSaveBeforeAction();
        SetCurrentCard(demoCard);
        new SaveSystem(demoSavePath).SaveProgress(ReadTemporarySave(demoSavePath), testState);
        ShowDemoState(before, "수동 테스트용 카드 지정 / 스탯·플래그·기록 유지");
    }

    private void CaptureSaveBeforeAction()
    {
        saveBeforeAction = File.Exists(demoSavePath) ? File.ReadAllText(demoSavePath) : null;
    }

    private void OnDemoCardChanged()
    {
        cardChangedCount++;
    }

    private void ShowDemoState(Stat before, string status)
    {
        Stat after = testState.GetStats();
        ShowStat(0, before.faith, after.faith);
        ShowStat(1, before.order, after.order);
        ShowStat(2, before.economy, after.economy);
        ShowStat(3, before.unrest, after.unrest);

        string flags = string.Join(", ", testState.CopyFlags());
        string seen = string.Join(", ", testState.CopySeenEventIds());
        CardData current = testState.GetCurrentCard();
        string currentId = current == null ? "없음 (자동 지정 미구현)" : current.variantId;
        demoStatus.color = new Color32(232, 239, 248, 255);
        demoStatus.text = $"{status}\n현재 카드  {currentId} · 변경 알림 {cardChangedCount}회\n" +
            $"현재 플래그  {(flags.Length == 0 ? "없음" : flags)}\n" +
            $"현재 계층 기록  {(seen.Length == 0 ? "없음" : seen)}";
        leftButton.interactable = rightButton.interactable = current != null;
        restoreButton.interactable = true;
        RefreshSaveInspector();
    }

    private void ShowStat(int index, int previous, int current)
    {
        previousStatValues[index].text = $"이전 {previous}";
        currentStatValues[index].text = current.ToString();
    }

    private UnityEngine.UI.Button MakeButton(Transform parent, string text, float left, float bottom, float right,
        float top, UnityEngine.Events.UnityAction click)
    {
        RectTransform rect = MakeRect(parent, text, left, bottom, right, top);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color32(44, 89, 120, 255);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            try
            {
                click();
            }
            catch (Exception exception)
            {
                if (demoStatus != null)
                {
                    demoStatus.color = new Color32(255, 135, 135, 255);
                    demoStatus.text = $"동작 실패: {exception.Message}";
                }
                Debug.LogError($"[Integration Test] Manual action failed: {text}\n{exception}", this);
            }
        });
        Label(rect, text, 17, 0.06f, 0, 0.94f, 1).alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private static RectTransform Panel(Transform parent, string name, float left, float bottom, float right, float top)
    {
        RectTransform rect = MakeRect(parent, name, left, bottom, right, top);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color32(29, 42, 58, 255);
        image.raycastTarget = false;
        return rect;
    }

    private UnityEngine.UI.Text Label(Transform parent, string text, int size, float left, float bottom, float right, float top)
    {
        var label = MakeRect(parent, "Text", left, bottom, right, top).gameObject.AddComponent<UnityEngine.UI.Text>();
        label.font = dashboardFont;
        label.fontSize = size;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = size;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color32(232, 239, 248, 255);
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private static RectTransform MakeRect(Transform parent, string name, float left, float bottom, float right, float top)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void CheckCards()
    {
        List<CardData> cards = cardDatabase.GetAllCards();
        Require(cards.Count > 0, "The event CSV should contain cards.");
        Require(cards.All(card => card.leftChoice != null && card.rightChoice != null),
            "Every card should have both choices.");
        Require(cards.Select(card => card.variantId).Distinct().Count() == cards.Count,
            "Card variant Ids should be unique.");
        foreach (var group in cards.GroupBy(card => card.eventId))
        {
            Require(cardDatabase.GetEventVariants(group.Key).Count == group.Count(),
                $"사건별 카드 수가 다릅니다: {group.Key}");
        }
        foreach (CardData card in cards)
        {
            Require(!string.IsNullOrWhiteSpace(card.eventId) && !string.IsNullOrWhiteSpace(card.variantId),
                "모든 카드에 사건 ID와 카드 변형 ID가 필요합니다.");
            Require(ReferenceEquals(cardDatabase.FindByVariantId(card.variantId), card),
                $"정확한 카드 ID로 조회하지 못했습니다: {card.variantId}");
        }
        Require(cardDatabase.FindByVariantId("missing-card-for-test") == null, "없는 카드 ID는 null이어야 합니다.");

        List<CardData> variants = cardDatabase.GetEventVariants("K-M01");
        Require(variants.Any(card => card.variantId == "K-M01__MERCY" && card.requiredLegacyFlag == "mercy"),
            "The mercy variant should be indexed under K-M01.");
        Require(variants.Any(card => card.variantId == "K-M01__ZEAL" && card.requiredLegacyFlag == "zeal"),
            "The zeal variant should be indexed under K-M01.");
        Require(cardDatabase.GetEventVariants("missing-event").Count == 0,
            "An unknown event should have no variants.");
    }

    private void CheckNewGame()
    {
        GameState state = InitializeGame(CreateTemporarySavePath());
        ExpectStats(Stat.Default);
        Require(!state.HasFlag("mercy"), "A new game should start without legacy flags.");

        var savedFlags = new List<string> { "mercy" };
        var savedHistory = new List<string> { "B-M01" };
        var copy = new GameState(flags: savedFlags, cardHistory: savedHistory);
        copy.AddFlag("live_only");
        copy.MarkEventSeen("B-M02");
        Require(savedFlags.SequenceEqual(new[] { "mercy" }), "State changes must not mutate saved flags.");
        Require(savedHistory.SequenceEqual(new[] { "B-M01" }), "State changes must not mutate saved history.");
        Require(copy.RemoveFlag("mercy") && !copy.HasFlag("mercy"), "Removing an existing flag should work.");

        state.AddFlag("mercy");
        state.SetFlags(null);
        Require(!state.HasFlag("mercy"), "Replacing flags with null should clear them.");
    }

    private void CheckStats()
    {
        Require(Stat.Default.Equals(new Stat(50, 50, 50, 50)), "기본 스탯은 모두 50이어야 합니다.");
        Stat sum = new Stat(10, 20, 30, 40) + new Stat(-20, 10, 90, -90);
        Require(sum.Equals(new Stat(-10, 30, 120, -50)), "스탯 합산 결과가 다릅니다.");
        Require(sum.Clamp().Equals(new Stat(0, 30, 100, 0)), "스탯 제한 결과가 다릅니다.");
        sum.Clear();
        Require(sum.Equals(Stat.Default), "Clear는 기본 스탯으로 복원해야 합니다.");
        var state = new GameState();
        Stat copy = state.GetStats();
        copy.faith = 0;
        Require(state.GetStats().faith == 50, "반환한 Stat 구조체는 복사본이어야 합니다.");
    }

    private void CheckFlagsAndHistory()
    {
        var state = new GameState();
        state.AddFlag(null);
        state.AddFlag(" ");
        state.AddFlag("trial");
        state.AddFlag("trial");
        Require(state.CopyFlags().SequenceEqual(new[] { "trial" }), "플래그는 빈 값과 중복을 제외해야 합니다.");
        Require(!state.RemoveFlag("absent") && state.RemoveFlag("trial"), "플래그 제거 결과가 다릅니다.");
        state.MarkEventSeen(null);
        state.MarkEventSeen(" ");
        state.MarkEventSeen("B-M01");
        state.MarkEventSeen("B-M01");
        Require(state.CopySeenEventIds().SequenceEqual(new[] { "B-M01" }), "본 사건은 한 번만 기록해야 합니다.");
        state.CopySeenEventIds().Clear();
        Require(state.HasSeenEvent("B-M01"), "반환한 기록 복사본이 원본을 변경하면 안 됩니다.");
        var flags = new List<string> { "saved" };
        var history = new List<string> { "K-M01" };
        state.SetFlags(flags);
        state.SetSeenEventIds(history);
        flags.Clear();
        history.Clear();
        state.CopyFlags().Clear();
        Require(state.HasFlag("saved") && state.HasSeenEvent("K-M01"), "입력 목록과 상태는 분리되어야 합니다.");
        state.InitEventHist();
        Require(state.CopySeenEventIds().Count == 0, "계층 기록 초기화가 필요합니다.");
        state.MarkEventSeen("K-M02");
        state.SetSeenEventIds(null);
        Require(state.CopySeenEventIds().Count == 0, "null 기록으로 교체하면 비워야 합니다.");
    }

    private void CheckCardNotifications()
    {
        var state = new GameState();
        CardData card = cardDatabase.GetAllCards().First();
        int notifications = 0;
        CardData observed = null;
        Action listener = () => { notifications++; observed = state.GetCurrentCard(); };
        state.CurrentCardChanged += listener;
        try
        {
            state.UpdateCurrentCard(card);
            Require(notifications == 1 && ReferenceEquals(observed, card),
                "카드 변경 이후 동일한 카드 참조로 한 번 알려야 합니다.");
            state.UpdateCurrentCard(null);
            Require(notifications == 2 && observed == null, "카드를 비울 때도 변경 알림이 필요합니다.");
        }
        finally
        {
            state.CurrentCardChanged -= listener;
        }
        state.UpdateCurrentCard(card);
        Require(notifications == 2, "구독 해제 이후 호출되면 안 됩니다.");
    }

    private static SaveData CreateSaveFixture(CardData card, Stat stats)
    {
        return new SaveData
        {
            checkpoints = new List<CheckpointData>
            {
                new CheckpointData { casteCode = card?.casteCode, stats = Stat.Default }
            },
            progress = new ProgressData { stats = stats, currentVariantId = card?.variantId }
        };
    }

    private void CheckSessionChoice(bool right, Stat initial, Stat expected, string expectedFlag)
    {
        WithTemporarySave(path =>
        {
            // 고정 기대값을 검증하는 CSV 테스트 케이스입니다. 실제 카드 선택 규칙이 아닙니다.
            CardData card = cardDatabase.FindByVariantId("B-M04");
            Require(card != null, "선택 검사용 CSV 카드 B-M04가 필요합니다.");
            WriteTemporarySave(CreateSaveFixture(card, initial), path);
            GameState state = InitializeGame(path);
            int notifications = 0;
            Action listener = () => notifications++;
            state.CurrentCardChanged += listener;
            try { testSession.Choose(right); }
            finally { state.CurrentCardChanged -= listener; }

            ExpectStats(expected);
            Require(state.CopyFlags().SequenceEqual(new[] { expectedFlag }), "선택 플래그가 다릅니다.");
            Require(state.CopySeenEventIds().SequenceEqual(new[] { card.eventId }), "선택한 사건 ID를 기록해야 합니다.");
            Require(notifications == 1, "선택 완료 시 카드 변경 알림이 한 번 필요합니다.");
            SaveData saved = ReadTemporarySave(path);
            Require(saved.progress.stats.Equals(expected), "GameSession이 변경된 스탯을 자동 저장해야 합니다.");
            Require(saved.progress.flags.SequenceEqual(state.CopyFlags())
                && saved.progress.seenEventIds.SequenceEqual(state.CopySeenEventIds()), "플래그와 기록을 자동 저장해야 합니다.");
            Require((saved.progress.currentVariantId ?? string.Empty) == (state.GetCurrentCard()?.variantId ?? string.Empty),
                "현재 카드 ID와 저장된 카드 ID가 일치해야 합니다.");
            Require(saved.GetLastCheckpoint().stats.Equals(Stat.Default), "선택이 체크포인트 스탯을 덮어쓰면 안 됩니다.");
            Require(!File.Exists(path + ".tmp"), "저장 완료 후 임시 쓰기 파일이 남으면 안 됩니다.");
        });
    }

    private void CheckChoices()
    {
        CheckSessionChoice(false, new Stat(98, 99, 99, 98), new Stat(100, 100, 100, 100), "zeal");
        CheckSessionChoice(true, new Stat(1, 1, 1, 1), default, "mercy");
    }

    private void CheckEmptyChoice()
    {
        WithTemporarySave(path =>
        {
            WriteTemporarySave(CreateSaveFixture(null, Stat.Default), path);
            GameState state = InitializeGame(path);
            string original = File.ReadAllText(path);
            testSession.Choose(false);
            testSession.Choose(true);
            ExpectStats(Stat.Default);
            Require(state.CopyFlags().Count == 0 && state.CopySeenEventIds().Count == 0,
                "현재 카드가 없으면 상태를 바꾸면 안 됩니다.");
            Require(File.ReadAllText(path) == original, "무효 선택은 저장 파일을 바꾸면 안 됩니다.");
        });
    }

    private void CheckProgressSnapshot()
    {
        WithTemporarySave(path =>
        {
            var state = new GameState(flags: new List<string> { "seed" }, cardHistory: new List<string> { "B-M01" });
            state.UpdateCurrentCard(cardDatabase.FindByVariantId("B-M04"));
            SaveData data = CreateSaveFixture(state.GetCurrentCard(), Stat.Default);
            ProgressData original = data.progress;
            var store = new SaveSystem(path);
            store.SaveProgress(data, state);
            state.AddFlag("later");
            state.MarkEventSeen("B-M02");
            state.SetStats(new Stat(61, 62, 63, 64));
            Require(data.progress.flags.SequenceEqual(new[] { "seed" })
                && data.progress.seenEventIds.SequenceEqual(new[] { "B-M01" })
                && data.progress.stats.Equals(Stat.Default), "저장 스냅샷은 이후 상태 변경과 분리되어야 합니다.");
            store.SaveProgress(data, state);
            Require(ReferenceEquals(original, data.progress), "선택마다 ProgressData가 늘어나면 안 됩니다.");
            SaveData loaded = store.LoadOrNull();
            Require(loaded.progress.stats.Equals(state.GetStats()) && loaded.progress.currentVariantId == "B-M04"
                && loaded.progress.flags.SequenceEqual(new[] { "seed", "later" }), "최근 진행 상태로 덮어써야 합니다.");
            data.progress.Clear();
            Require(data.progress.stats.Equals(Stat.Default) && data.progress.flags.Count == 0
                && data.progress.seenEventIds.Count == 0 && data.progress.currentVariantId == null,
                "진행 데이터 초기화 결과가 다릅니다.");
            var added = new CheckpointData { casteCode = "K", stats = Stat.Default };
            data.AddCheckpoint(added);
            Require(ReferenceEquals(data.GetLastCheckpoint(), added), "마지막 체크포인트를 반환해야 합니다.");
            data.RemoveCheckpoint(added);
            Require(data.checkpoints.Count == 1, "지정한 체크포인트만 제거해야 합니다.");
            data.Clear();
            Require(data.GetLastCheckpoint() == null && data.progress == null, "저장 데이터 초기화가 필요합니다.");
        });
    }

    private void CheckMissingCheckpoint()
    {
        WithTemporarySave(path =>
        {
            SaveData data = CreateSaveFixture(demoCard, Stat.Default);
            data.checkpoints.Clear();
            WriteTemporarySave(data, path);
            GameState state = InitializeGame(path);
            CardData before = state.GetCurrentCard();
            string original = File.ReadAllText(path);
            Require(!testSession.RestoreCheckpoint(), "체크포인트가 없으면 false를 반환해야 합니다.");
            Require(ReferenceEquals(before, state.GetCurrentCard()) && File.ReadAllText(path) == original,
                "복원할 수 없으면 상태와 파일을 유지해야 합니다.");
        });
    }

    private void CheckInvalidSaves()
    {
        WithTemporarySave(path =>
        {
            var store = new SaveSystem(path);
            ExpectThrows<InvalidDataException>(() => store.Save(null));
            ExpectThrows<InvalidDataException>(() => store.Save(new SaveData()));
            store.Save(CreateSaveFixture(demoCard, Stat.Default));
            File.WriteAllText(path, "{\"progress\":");
            ExpectThrows<InvalidDataException>(() => store.LoadOrNull());
            SaveData unknown = CreateSaveFixture(demoCard, Stat.Default);
            unknown.progress.currentVariantId = "missing-card-for-test";
            store.Save(unknown);
            ExpectThrows<InvalidDataException>(() => InitializeGame(path));
            File.WriteAllText(path, "{\"checkpoints\":[],\"progress\":{\"stats\":{\"faith\":50,\"order\":50,\"economy\":50,\"unrest\":50},\"flags\":null,\"seenEventIds\":null}}");
            SaveData normalized = store.LoadOrNull();
            Require(normalized.progress.flags != null && normalized.progress.seenEventIds != null,
                "비어 있는 플래그와 기록은 빈 목록으로 읽어야 합니다.");
        });
    }

    private void CheckIncompleteSave()
    {
        WithTemporarySave(path =>
        {
            var store = new SaveSystem(path);
            store.Save(CreateSaveFixture(demoCard, Stat.Default));
            string[] invalidFiles =
            {
                "{}", "null", "[]", "{\"checkpoints\":[]}",
                "{\"checkpoints\":[],\"progress\":null}",
                "{\"checkpoints\":[],\"progress\":{}}",
                "{\"checkpoints\":null,\"progress\":{\"stats\":{}}}"
            };
            foreach (string json in invalidFiles)
            {
                File.WriteAllText(path, json);
                ExpectThrows<InvalidDataException>(() => store.LoadOrNull());
            }
        });
    }

    private static void ExpectThrows<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name} was not thrown.");
    }

    private void CheckSaveAndRestore()
    {
        WithTemporarySave(path =>
        {
            Require(ReadTemporarySave(path) == null, "A missing test save should return null.");
            var checkpointStats = new Stat(37, 48, 35, 27);
            var progressStats = new Stat(53, 50, 43, 41);
            var save = new SaveData
            {
                checkpoints = new List<CheckpointData>
                {
                    new CheckpointData { casteCode = "B" },
                    new CheckpointData
                    {
                        casteCode = "K",
                        stats = checkpointStats,
                        flags = new List<string> { "mercy" }
                    }
                },
                progress = new ProgressData
                {
                    stats = progressStats,
                    flags = new List<string> { "mercy", "trial_flag" },
                    seenEventIds = new List<string> { "K-M01" },
                    currentVariantId = "K-M03__MERCY"
                }
            };

            WriteTemporarySave(save, path);
            save.progress.seenEventIds.Add("K-M02");
            WriteTemporarySave(save, path);

            SaveData loaded = ReadTemporarySave(path);
            Require(loaded != null, "The save should load from disk.");
            Require(loaded.checkpoints.Select(item => item.casteCode).SequenceEqual(new[] { "B", "K" }),
                "Checkpoints should round-trip in caste order.");
            Require(loaded.progress.seenEventIds.SequenceEqual(new[] { "K-M01", "K-M02" }),
                "The overwritten save should contain the current caste's event history.");

            GameState state = InitializeGame(path);
            ExpectStats(progressStats);
            Require(state.GetCurrentCard()?.variantId == "K-M03__MERCY",
                "Continuing should restore the exact pending card variant.");
            Require(state.HasSeenEvent("K-M02"), "Continuing should restore the saved event history.");
            ProgressData existingProgress = loaded.progress;
            new SaveSystem(path).SaveProgress(loaded, state);
            Require(ReferenceEquals(existingProgress, loaded.progress), "Saving should reuse ProgressData.");
            Require(!File.ReadAllText(path).Contains("\"currentCard\""), "Save only the current card ID.");
            Require(testState.HasFlag("mercy") && testState.HasFlag("trial_flag"),
                "Continuing should restore the progress flags.");
            state.AddFlag("live_only");

            CheckpointData checkpoint = loaded.GetLastCheckpoint();
            Require(checkpoint != null && checkpoint.casteCode == "K", "The newest checkpoint should be for K.");
            RestoreTestCheckpoint();
            Require(ReferenceEquals(state, testSession.State), "Checkpoint restore should reuse the current GameState.");
            Require(state.GetCurrentCard() == null, "현재 복원 구현은 카드를 비우며 첫 카드 지정은 아직 미구현입니다.");
            ExpectStats(checkpointStats);
            Require(testState.HasFlag("mercy") && !testState.HasFlag("trial_flag")
                && !testState.HasFlag("live_only"), "Restoring a checkpoint should replace the live flags.");
            state.SetFlags(null);

            SaveData restarted = ReadTemporarySave(path);
            Require(restarted.progress.stats.Equals(checkpointStats), "Restarted stats should be saved to disk.");
            Require(restarted.progress.flags.SequenceEqual(new[] { "mercy" }),
                "Only checkpoint flags should remain in the restarted save.");
            Require(restarted.progress.seenEventIds.Count == 0, "Restarting should clear the current event history.");
            Require(restarted.GetLastCheckpoint().flags.SequenceEqual(new[] { "mercy" }),
                "Restarting should preserve the last checkpoint's flags.");

            InitializeGame(path);
            ExpectStats(checkpointStats);
            Require(testState.HasFlag("mercy") && !testState.HasFlag("trial_flag"),
                "Continuing after a restart should load the checkpoint's flags.");
        });
    }

    private static SaveData ReadTemporarySave(string path)
    {
        ISaveStore store = new SaveSystem(path);
        return store.LoadOrNull();
    }

    private static void WriteTemporarySave(SaveData data, string path)
    {
        ISaveStore store = new SaveSystem(path);
        store.Save(data);
    }

    private void RestoreTestCheckpoint()
    {
        Require(testSession.RestoreCheckpoint(), "The temporary save must have a checkpoint.");
    }

    private GameState InitializeGame(string path, bool observe = false)
    {
        Require(!gameManager.enabled, "Keep GameManager disabled while the test uses its own save path.");

        testSession = new GameSession(cardDatabase, new CardSelector(), new SaveSystem(path));
        testState = testSession.State;
        if (observe)
        {
            if (observedState != null)
                observedState.CurrentCardChanged -= OnDemoCardChanged;
            observedState = testState;
            observedState.CurrentCardChanged += OnDemoCardChanged;
        }
        testSession.StartOrResume();
        return testState;
    }

    private void SetCurrentCard(CardData card)
    {
        testState.UpdateCurrentCard(card);
    }

    private void ExpectStats(Stat expected)
    {
        Stat actual = testState.GetStats();
        Require(actual.faith == expected.faith && actual.order == expected.order
            && actual.economy == expected.economy && actual.unrest == expected.unrest,
            $"Expected stats ({expected.faith}, {expected.order}, {expected.economy}, {expected.unrest}), " +
            $"got ({actual.faith}, {actual.order}, {actual.economy}, {actual.unrest}).");
    }

    private static void WithTemporarySave(Action<string> check)
    {
        string path = CreateTemporarySavePath();
        try
        {
            check(path);
        }
        finally
        {
            RemoveTemporarySave(path);
        }
    }

    private static void RemoveTemporarySave(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
            return;

        File.Delete(path + ".tmp");
        File.Delete(path);
        Directory.Delete(directory);
    }

    private static string CreateTemporarySavePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CASTE-Test-" + Guid.NewGuid().ToString("N"));
        return Path.Combine(directory, "save.json");
    }

    private void RunCase(string name, Action check)
    {
        try
        {
            check();
            passed++;
            results.Add((name, null));
            Debug.Log($"[Integration Test] PASS {name}", this);
        }
        catch (Exception exception)
        {
            failed++;
            results.Add((name, exception.Message));
            Debug.LogError($"[Integration Test] FAIL {name}\n{exception}", this);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
