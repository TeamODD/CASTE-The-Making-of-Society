using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class Test : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private GameManager gameManager;
    [Tooltip("수동 테스트에 사용할 CSV variant_id입니다. 실제 게임의 첫 카드 선택 규칙과는 별개입니다.")]
    [SerializeField] private string demoVariantId = "B-M04";

    private TestDemoSession demoSession;
    private IReadOnlyList<(string name, string error)> results = Array.Empty<(string, string)>();
    private Font dashboardFont;
    private UnityEngine.UI.Text demoCardTitle;
    private UnityEngine.UI.Text demoCardSpeaker;
    private UnityEngine.UI.Text demoCardDialogue;
    private UnityEngine.UI.Text demoCardChoices;
    private UnityEngine.UI.Text demoStatus;
    private readonly UnityEngine.UI.Text[] previousStatValues = new UnityEngine.UI.Text[4];
    private readonly UnityEngine.UI.Text[] currentStatValues = new UnityEngine.UI.Text[4];
    private UnityEngine.UI.Button leftButton;
    private UnityEngine.UI.Button rightButton;
    private UnityEngine.UI.Button restoreButton;
    private UnityEngine.UI.Button assignDemoCardButton;
    private UnityEngine.UI.Text reportSummary;
    private UnityEngine.UI.Text reportDetails;
    private UnityEngine.UI.Text saveBeforeText;
    private UnityEngine.UI.Text saveCurrentText;
    private int cardChangedCount;

    private const string PendingFeatures =
        "수동 확인은 테스트 전용 세이브를 사용합니다. 자동 검사는 실행마다 별도의 임시 저장을 사용합니다.";

    private void Start()
    {
        ShowDashboard(RunAndDisplayChecks());
    }

    private TestSessionData RunAndDisplayChecks()
    {
        var report = GameSessionChecks.Run(
            csvFile != null ? csvFile.text : null, gameManager != null && !gameManager.enabled, demoVariantId);
        results = report.results;
        RefreshReport();
        return report.data;
    }

    private void RefreshReport()
    {
        if (reportSummary != null)
        {
            int failed = results.Count(result => result.error != null);
            reportSummary.text = $"자동 검증  {results.Count - failed} 통과 · {failed} 실패";
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
        if (demoSession != null)
        {
            demoSession.CurrentCardChanged -= OnDemoCardChanged;
            demoSession.Dispose();
        }
        if (dashboardFont != null)
            Destroy(dashboardFont);
    }

    private void ShowDashboard(TestSessionData data)
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
        MakeButton(background, "자동 검사 다시 실행", 0.52f, 0.91f, 0.74f, 0.97f, () => RunAndDisplayChecks());

        RectTransform content = MakeRect(background, "Test Content", 0, 0, 1, 1);
        var report = Panel(content, "자동 검증", 0.04f, 0.05f, 0.38f, 0.875f);
        reportSummary = Label(report, string.Empty, 23, 0.05f, 0.89f, 0.95f, 0.98f);
        Label(report, "검사 결과 · 아래로 스크롤", 15, 0.05f, 0.80f, 0.95f, 0.89f)
            .color = new Color32(247, 197, 109, 255);
        var (_, details) = MakeSavePane(report, "검사 결과 / 실패 원인", 0.04f, 0.96f);
        reportDetails = details;
        RefreshReport();

        var demo = Panel(content, "수동 확인", 0.40f, 0.05f, 0.96f, 0.875f);
        Label(demo, "실제 진행 코드 · 수동 확인", 24, 0.04f, 0.93f, 0.96f, 0.995f);
        demoCardTitle = Label(demo, string.Empty, 20, 0.04f, 0.86f, 0.96f, 0.93f);
        demoCardSpeaker = Label(demo, string.Empty, 17, 0.04f, 0.81f, 0.96f, 0.86f);
        demoCardSpeaker.color = new Color32(173, 201, 229, 255);
        demoCardDialogue = Label(demo, string.Empty, 17, 0.04f, 0.65f, 0.96f, 0.81f);
        demoCardChoices = Label(demo, string.Empty, 15, 0.04f, 0.56f, 0.96f, 0.65f);

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
        leftButton = MakeButton(demo, "왼쪽 선택", 0.04f, 0.115f, 0.33f, 0.185f,
            () => RunDemoAction(session => session.Choose(false), "왼쪽 선택 → GameSession 자동 저장 완료"));
        rightButton = MakeButton(demo, "오른쪽 선택", 0.355f, 0.115f, 0.645f, 0.185f,
            () => RunDemoAction(session => session.Choose(true), "오른쪽 선택 → GameSession 자동 저장 완료"));
        restoreButton = MakeButton(demo, "체크포인트 복원", 0.67f, 0.115f, 0.96f, 0.185f,
            () => RunDemoAction(session => session.Restore(), "체크포인트 복원 → 첫 카드 및 저장 복구"));
        assignDemoCardButton = MakeButton(demo, "테스트 카드만 지정", 0.67f, 0.025f, 0.96f, 0.095f,
            () => RunDemoAction(session => session.AssignDemoCard(), "수동 테스트용 카드 지정 / 스탯·플래그·기록 유지"));

        if (data != null)
        {
            demoSession = new TestDemoSession(data, () => gameManager.TurnsPerCaste, Application.persistentDataPath);
            demoSession.CurrentCardChanged += OnDemoCardChanged;
        }

        AddStartMenu(content, demo);
        AddSaveInspector(background, content);
    }

    private void AddStartMenu(RectTransform content, RectTransform demo)
    {
        var menu = Panel(content, "시작 메뉴", 0.40f, 0.05f, 0.96f, 0.875f);
        Label(menu, "테스트 시작", 32, 0.10f, 0.71f, 0.90f, 0.83f).alignment = TextAnchor.MiddleCenter;
        var menuStatus = Label(menu, string.Empty, 18, 0.10f, 0.22f, 0.90f, 0.34f);
        menuStatus.alignment = TextAnchor.MiddleCenter;

        void OpenDemo(Action<TestDemoSession> open, string status)
        {
            RunDemoAction(open, status);
            menu.gameObject.SetActive(false);
            demo.gameObject.SetActive(true);
        }

        var newGame = MakeButton(menu, "새로하기", 0.22f, 0.55f, 0.78f, 0.65f,
            () => OpenDemo(session => session.StartNewGame(), "새 테스트 진행과 첫 체크포인트 저장 완료"), menuStatus);
        var resume = MakeButton(menu, "이어하기", 0.22f, 0.40f, 0.78f, 0.50f,
            () => OpenDemo(session => session.ResumeGame(), "테스트 저장 파일에서 이어하기 완료"), menuStatus);
        var savedCardTitle = Label(menu, string.Empty, 18, 0.10f, 0.35f, 0.90f, 0.395f);
        savedCardTitle.gameObject.name = "Saved Card Title";
        savedCardTitle.alignment = TextAnchor.MiddleCenter;

        void ShowStartMenu()
        {
            newGame.interactable = demoSession != null;
            resume.interactable = demoSession?.HasSave == true;
            savedCardTitle.text = resume.interactable ? demoSession.GetSavedCardTitle() ?? string.Empty : string.Empty;
            menuStatus.color = new Color32(232, 239, 248, 255);
            menuStatus.text = demoSession == null
                ? "CSV 연결과 테스트 카드 ID를 확인해 주세요."
                : resume.interactable
                    ? "새로하기를 누르면 기존 테스트 세이브를 덮어씁니다."
                    : "이어할 테스트 세이브가 없습니다.";
            demo.gameObject.SetActive(false);
            menu.gameObject.SetActive(true);
        }

        MakeButton(demo, "시작 메뉴", 0.04f, 0.025f, 0.33f, 0.095f, ShowStartMenu);
        ShowStartMenu();
    }

    private void AddSaveInspector(RectTransform background, RectTransform content)
    {
        var inspector = Panel(background, "저장 파일", 0.04f, 0.05f, 0.96f, 0.875f);
        Label(inspector, "테스트 세이브 경로", 20, 0.025f, 0.90f, 0.97f, 0.98f);
        Label(inspector, demoSession?.SavePath.Replace('\\', '/') ?? "테스트 저장 경로가 없습니다.", 17, 0.025f, 0.83f, 0.97f, 0.90f);
        Label(inspector, "스크롤로 save.json 전체 보기 · Play를 종료해도 테스트 세이브가 남습니다.",
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

            content.gameObject.SetActive(!open);
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
            saveBeforeText.text = demoSession?.SaveBeforeAction ?? "아직 이전 저장 내용이 없습니다.";
        if (saveCurrentText != null)
            saveCurrentText.text = demoSession?.CurrentSave ?? "저장 파일이 없습니다.";
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

    private void RunDemoAction(Action<TestDemoSession> action, string status)
    {
        if (demoSession == null)
            throw new InvalidOperationException("먼저 테스트를 초기화해 주세요.");

        Stat before = demoSession.State?.GetStats() ?? Stat.Default;
        action(demoSession);
        ShowDemoState(before, status);
    }

    private void OnDemoCardChanged()
    {
        cardChangedCount++;
        RefreshDemoCard(demoSession.State.GetCurrentCard());
    }

    private void RefreshDemoCard(CardData card)
    {
        demoCardTitle.text = card == null ? "현재 카드 없음" : $"현재 카드  {card.variantId} / {card.title}";
        demoCardSpeaker.text = card == null ? string.Empty : $"화자  {card.speaker}";
        demoCardDialogue.text = card?.dialogue ?? string.Empty;
        demoCardChoices.text = card == null
            ? string.Empty
            : $"왼쪽  {card.leftChoice?.text}\n오른쪽  {card.rightChoice?.text}";
        leftButton.interactable = card?.leftChoice != null;
        rightButton.interactable = card?.rightChoice != null;
    }

    private void ShowDemoState(Stat before, string status)
    {
        GameState state = demoSession.State;
        Stat after = state.GetStats();
        ShowStat(0, before.faith, after.faith);
        ShowStat(1, before.order, after.order);
        ShowStat(2, before.economy, after.economy);
        ShowStat(3, before.unrest, after.unrest);

        string flags = string.Join(", ", state.CopyFlags());
        string seen = string.Join(", ", state.CopySeenEventIds());
        CardData current = state.GetCurrentCard();
        string currentId = current == null ? "없음" : current.variantId;
        demoStatus.color = new Color32(232, 239, 248, 255);
        demoStatus.text = $"{status}\n현재 카드  {currentId} · 변경 알림 {cardChangedCount}회\n" +
            $"현재 플래그  {(flags.Length == 0 ? "없음" : flags)}\n" +
            $"현재 계층 기록  {(seen.Length == 0 ? "없음" : seen)}";
        restoreButton.interactable = assignDemoCardButton.interactable = true;
        RefreshSaveInspector();
    }

    private void ShowStat(int index, int previous, int current)
    {
        previousStatValues[index].text = $"이전 {previous}";
        currentStatValues[index].text = current.ToString();
    }

    private UnityEngine.UI.Button MakeButton(Transform parent, string text, float left, float bottom, float right,
        float top, UnityEngine.Events.UnityAction click, UnityEngine.UI.Text errorLabel = null)
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
                var status = errorLabel != null ? errorLabel : demoStatus;
                if (status != null)
                {
                    status.color = new Color32(255, 135, 135, 255);
                    status.text = $"동작 실패: {exception.Message}";
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

}
