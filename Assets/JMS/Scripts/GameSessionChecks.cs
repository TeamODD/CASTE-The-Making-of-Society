using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using static TestSessionData;

internal sealed class GameSessionChecks
{
    private readonly List<(string name, string error)> results = new();
    private int passed;
    private int failed;
    private TestSessionData fixtures;
    private GameSession testSession;
    private GameState testState;

    private CardDatabase cardDatabase => fixtures.Database;
    private CardData demoCard => fixtures.DemoCard;

    public static (TestSessionData data, IReadOnlyList<(string name, string error)> results) Run(
        string csvText, bool managerDisabled, string demoVariantId)
    {
        var checks = new GameSessionChecks();
        checks.RunAll(csvText, managerDisabled, demoVariantId);
        return (checks.fixtures, checks.results);
    }

    private void RunAll(string csvText, bool managerDisabled, string demoVariantId)
    {
        RunCase("테스트 씬과 CSV 연결", () =>
            fixtures = new TestSessionData(csvText, managerDisabled, demoVariantId));

        if (fixtures != null)
        {
            RunCase("CSV 전체 카드 / 사건별 변형 조회", CheckCards);
            RunCase("기본 스탯 / 합산 / 0~100 제한", CheckStats);
            RunCase("새 게임 / 저장 데이터와 상태 분리", CheckNewGame);
            RunCase("새 게임 버튼 경로 / 저장 교체와 실패 복구", CheckStartNewGame);
            RunCase("테스트 저장 유지 / 실제 저장 경로 분리", CheckTestDemoSavePersistence);
            RunCase("플래그 / 계층 기록 / 복사본 보호", CheckFlagsAndHistory);
            RunCase("현재 카드 변경 알림 / 구독 해제", CheckCardNotifications);
            RunCase("왼쪽 선택 → 효과 / 기록 / 자동 저장", () =>
                CheckSessionChoice(false, Stat.Default, new Stat(57, 52, 53, 58), "zeal"));
            RunCase("오른쪽 선택 → 효과 / 기록 / 자동 저장", () =>
                CheckSessionChoice(true, Stat.Default, new Stat(46, 48, 44, 42), "mercy"));
            RunCase("스탯 한계 도달 → 계층 첫 카드 복귀", CheckGameOver);
            RunCase("메인 사건 순서 / 턴 예약 / 0·2·12턴", CheckTurnRules);
            RunCase("랜덤 후보 / 현재 플래그 / 사건 중복 제외", CheckRandomCandidates);
            RunCase("유산 분기 / 저장 후 계층 전환 알림", CheckCasteTransition);
            RunCase("고를 수 없는 계층 건너뛰기", CheckSkipCaste);
            RunCase("저장 실패 시 진행 보존 / 재시도", CheckSaveFailure);
            RunCase("현재 카드가 없을 때 선택 무시", CheckEmptyChoice);
            RunCase("JSON 덮어쓰기 / 정확한 카드 이어하기 / 복원", CheckSaveAndRestore);
            RunCase("저장 스냅샷 분리", CheckProgressSnapshot);
            RunCase("빈 체크포인트 파일을 새 게임으로 교체", CheckMissingCheckpoint);
            RunCase("손상 JSON / 잘못된 카드 ID 교체", CheckInvalidSaves);
            RunCase("구형 카드 객체 / 빈 카드 이어하기", CheckLegacySave);
            RunCase("필수 progress가 없는 JSON 거부", CheckIncompleteSave);
        }

        string summary = $"[Integration Test] {passed} passed, {failed} failed.";
        if (failed == 0)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
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
        WithTemporarySave(path =>
        {
            GameState state = InitializeGame(path);
            ExpectStats(Stat.Default);
            Require(!state.HasFlag("mercy"), "A new game should start without legacy flags.");
            SaveData save = ReadTemporarySave(path);
            Require(state.GetCurrentCard()?.variantId == "B-M01" && save.checkpoints.Count == 1
                && save.GetLastCheckpoint().firstVariantId == "B-M01", "새 게임은 첫 계층의 체크포인트를 저장해야 합니다.");

            var savedFlags = new List<string> { "mercy" };
            var savedHistory = new List<string> { "B-M01" };
            var copy = new GameState(flags: savedFlags, cardHist: savedHistory);
            copy.AddFlag("live_only");
            copy.MarkEventSeen("B-M02");
            Require(savedFlags.SequenceEqual(new[] { "mercy" }), "State changes must not mutate saved flags.");
            Require(savedHistory.SequenceEqual(new[] { "B-M01" }), "State changes must not mutate saved history.");
            Require(copy.RemoveFlag("mercy") && !copy.HasFlag("mercy"), "Removing an existing flag should work.");

            state.AddFlag("mercy");
            state.SetFlags(null);
            Require(!state.HasFlag("mercy"), "Replacing flags with null should clear them.");
        });
    }

    private void CheckStartNewGame()
    {
        WithTemporarySave(path =>
        {
            var store = new SaveSystem(path);
            var session = fixtures.CreateSession(store);
            int notifications = 0;
            session.State.CurrentCardChanged += () => notifications++;
            Require(!store.HasSave && !session.TryResume(2) && !store.HasSave && notifications == 0
                && session.State.GetCurrentCard() == null, "저장이 없으면 이어하기가 진행이나 새 파일을 만들면 안 됩니다.");
            session.StartNewGame(12);
            session.Choose(false);
            Require(session.State.GetCurrentCard()?.variantId == "B-M02", "새로 시작하기 전에 진행된 저장이 필요합니다.");

            GameState state = session.State;
            Require(session.StartNewGame(2), "새 게임을 시작해야 합니다.");
            SaveData saved = store.LoadOrNull();
            Require(ReferenceEquals(state, session.State) && state.GetCurrentCard()?.variantId == "B-M01"
                && state.GetStats().Equals(Stat.Default), "같은 상태 객체에 새 게임의 첫 카드와 스탯을 적용해야 합니다.");
            Require(saved.checkpoints.Count == 1 && saved.GetLastCheckpoint().firstVariantId == "B-M01"
                && saved.progress.turnsPerCaste == 2 && saved.progress.turnsUsedInCaste == 0
                && saved.progress.seenEventIds.Count == 0, "새 게임 저장은 기존 진행을 첫 체크포인트로 교체해야 합니다.");

            session.Choose(true);
            string savedJson = File.ReadAllText(path);
            Require(store.HasSave && session.TryResume(12) && File.ReadAllText(path) == savedJson
                && state.GetCurrentCard()?.variantId == "B-M02" && store.LoadOrNull().progress.turnsPerCaste == 2,
                "이어하기는 저장된 카드와 턴 설정을 사용해야 합니다.");
        });

        var failingStore = new MemorySaveStore();
        var failingSession = fixtures.CreateSession(failingStore);
        failingSession.StartOrResume();
        failingSession.Choose(false);
        SaveData previous = failingStore.Saved;
        int notifications = 0;
        failingSession.State.CurrentCardChanged += () => notifications++;
        failingStore.FailWrites = true;
        ExpectThrows<IOException>(() => failingSession.StartNewGame(2));
        Require(ReferenceEquals(failingStore.Saved, previous) && notifications == 0
            && failingSession.State.GetCurrentCard()?.variantId == "B-M02",
            "새 게임 저장 실패 시 기존 진행과 화면 상태를 유지해야 합니다.");
    }

    private void CheckTestDemoSavePersistence()
    {
        WithTemporarySave(gameSavePath =>
        {
            const string gameSave = "actual game save";
            File.WriteAllText(gameSavePath, gameSave);
            string root = Path.GetDirectoryName(gameSavePath);
            string demoSavePath = Path.Combine(root, "TestDemo", "save.json");
            try
            {
                Stat savedStats;
                using (var first = new TestDemoSession(fixtures, () => 12, root))
                {
                    Require(first.SavePath == demoSavePath && !first.HasSave, "별도의 테스트 저장 경로를 사용해야 합니다.");
                    Require(first.GetSavedCardTitle() == null, "저장이 없으면 표시할 카드 제목도 없어야 합니다.");
                    first.StartNewGame();
                    first.Choose(false);
                    savedStats = first.State.GetStats();
                }

                Require(File.Exists(demoSavePath), "수동 확인 세션을 종료해도 저장이 남아야 합니다.");
                string originalSave = File.ReadAllText(demoSavePath);
                using (var reopened = new TestDemoSession(fixtures, () => 12, root))
                {
                    Require(reopened.HasSave && reopened.State == null && reopened.CurrentSave == originalSave,
                        "다음 테스트 세션에서 불러오기 전까지 이전 저장을 유지해야 합니다.");
                    Require(reopened.GetSavedCardTitle() == cardDatabase.FindByVariantId("B-M02").title
                        && reopened.State == null && reopened.CurrentSave == originalSave,
                        "이어하기 전에도 저장된 카드 제목을 읽을 수 있어야 합니다.");
                    reopened.ResumeGame();
                    Require(reopened.State.GetCurrentCard()?.variantId == "B-M02"
                        && reopened.State.GetStats().Equals(savedStats),
                        "이어하기는 이전 카드와 스탯을 복원해야 합니다.");
                }
                Require(File.ReadAllText(gameSavePath) == gameSave, "테스트 저장은 실제 게임 경로에 영향을 주면 안 됩니다.");
            }
            finally
            {
                RemoveTemporarySave(demoSavePath);
            }
        });
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
        Require(state.HasSeenCard("B-M01"), "반환한 기록 복사본이 원본을 변경하면 안 됩니다.");
        var flags = new List<string> { "saved" };
        var history = new List<string> { "K-M01" };
        state.SetFlags(flags);
        state.SetSeenEventIds(history);
        flags.Clear();
        history.Clear();
        state.CopyFlags().Clear();
        Require(state.HasFlag("saved") && state.HasSeenCard("K-M01"), "입력 목록과 상태는 분리되어야 합니다.");
        state.InitCardHist();
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

    private void CheckSessionChoice(bool right, Stat initial, Stat expected, string expectedFlag)
    {
        WithTemporarySave(path =>
        {
            // 고정 기대값을 검증하는 CSV 테스트 케이스입니다. 실제 카드 선택 규칙이 아닙니다.
            CardData card = cardDatabase.FindByVariantId("B-M04");
            Require(card != null, "선택 검사용 CSV 카드 B-M04가 필요합니다.");
            WriteTemporarySave(fixtures.CreateSaveFixture(card, initial), path);
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
            Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length == 0,
                "저장 완료 후 임시 쓰기 파일이 남으면 안 됩니다.");
        });
    }

    private void CheckGameOver()
    {
        foreach (bool right in new[] { false, true })
        {
            WithTemporarySave(path =>
            {
                CardData last = cardDatabase.FindByVariantId("B-M04");
                Stat atRisk = right ? new Stat(1, 1, 1, 1) : new Stat(99, 99, 99, 99);
                SaveData fixture = fixtures.CreateSaveFixture(last, atRisk);
                fixture.progress.seenEventIds.AddRange(new[] { "B-M01", "B-M02", "B-M03" });
                fixture.progress.turnsUsedInCaste = 3;
                WriteTemporarySave(fixture, path);
                GameState state = InitializeGame(path);
                int transitions = 0;
                testSession.CasteChanged += (_, _) => transitions++;
                testSession.Choose(right);

                SaveData recovered = ReadTemporarySave(path);
                ExpectStats(Stat.Default);
                Require(state.GetCurrentCard()?.variantId == "B-M01", "게임 오버 후 계층의 첫 카드로 돌아가야 합니다.");
                Require(recovered.progress.turnsUsedInCaste == 0 && recovered.progress.flags.Count == 0
                    && recovered.progress.seenEventIds.Count == 0, "게임 오버를 낸 선택은 세이브에 남으면 안 됩니다.");
                Require(transitions == 0 && recovered.checkpoints.Count == 1,
                    "계층 마지막 카드의 게임 오버는 다음 계층 전환보다 먼저 처리해야 합니다.");
            });
        }
    }

    private void CheckTurnRules()
    {
        string header = fixtures.CsvHeader;
        var rows = new List<string> { header };
        for (int i = 1; i <= 9; i++)
            rows.Add(CreateCsvRow($"B-S{i:00}", "side", "B", 1));
        foreach (int i in new[] { 4, 2, 3, 1 })
            rows.Add(CreateCsvRow($"B-M{i:00}", "main", "B", 1));
        rows.Add(CreateCsvRow("B-M04", "main", "B", 1, variantId: "B-M04__ALT"));

        var database = new CardDatabase(string.Join("\n", rows));
        foreach (int limit in new[] { 0, 2, 12 })
        {
            var store = new MemorySaveStore();
            var session = new GameSession(database, new CardSelector(new System.Random(31)), store, limit);
            session.StartOrResume();

            var seen = new HashSet<string>();
            int nextMainNumber = 1;
            while (session.State.GetCurrentCard() is CardData card)
            {
                Require(seen.Add(card.eventId), "같은 사건을 두 번 고르면 안 됩니다.");
                int mainRemaining = 5 - nextMainNumber;
                bool isMain = card.eventType == "main";
                Require(limit - session.TurnsUsedInCaste > mainRemaining || isMain,
                    "메인 카드에 필요한 턴은 일반 카드에 쓸 수 없습니다.");
                if (isMain)
                {
                    Require(card.variantId == $"B-M{nextMainNumber:00}",
                        "메인 사건의 번호와 변형의 CSV 순서를 지켜야 합니다.");
                    nextMainNumber++;
                }

                session.Choose(false);
                Require(session.TurnsUsedInCaste == Math.Min(limit, seen.Count),
                    "설정 턴까지만 카운트해야 합니다.");
            }

            Require(nextMainNumber == 5 && seen.Count == Math.Max(limit, 4),
                "메인 카드가 모두 나오고 일반 카드는 여유 턴만 써야 합니다.");
            Require(store.Saved.progress.isFinished, "마지막 계층의 종료를 저장해야 합니다.");
            var resumed = new GameSession(database, new CardSelector(), store, 99);
            resumed.StartOrResume();
            Require(resumed.State.GetCurrentCard() == null && resumed.TurnsUsedInCaste == limit,
                "완료된 게임은 기존 턴 설정과 종료 상태로 이어야 합니다.");
        }
    }

    private void CheckRandomCandidates()
    {
        string header = fixtures.CsvHeader;
        var database = new CardDatabase(string.Join("\n", header,
            CreateCsvRow("B-S01", "random", "B", 1),
            CreateCsvRow("B-S02", "random", "B", 1, "zeal"),
            CreateCsvRow("B-S02", "random", "B", 1, "zeal", "B-S02__ALT"),
            CreateCsvRow("B-S03", "random", "B", 1, "mercy"),
            CreateCsvRow("B-S04", "random", "B", 1),
            CreateCsvRow("B-S05", "random", "B", 1, ""),
            CreateCsvRow("K-S01", "random", "K", 2),
            CreateCsvRow("B-M02", "main", "B", 1),
            CreateCsvRow("B-M01", "main", "B", 1)));
        List<CardData> cards = database.GetAllCards();
        var state = new GameState(cardHist: new List<string> { "B-S04" });

        CheckCandidates("B-S01", "B-S05", "B-M01");
        state.AddFlag("zeal");
        CheckCandidates("B-S01", "B-S02", "B-S05", "B-M01");

        void CheckCandidates(params string[] expected)
        {
            var actual = new HashSet<string>();
            for (int i = 0; i < expected.Length; i++)
            {
                var selector = new CardSelector(new IndexedCardRandom(i, expected.Length));
                CardData card = selector.PickNextCard(cards, 1, Array.Empty<string>(), state, 12);
                Require(card != null, "여유 턴에 고를 수 있는 카드가 있어야 합니다.");
                actual.Add(card.variantId);
            }
            Require(actual.SetEquals(expected), "현재 계층과 플래그에 맞는 미사용 사건만 추첨해야 합니다.");
        }
    }

    private void CheckCasteTransition()
    {
        foreach (bool right in new[] { false, true })
        {
            WithTemporarySave(path =>
            {
                GameState state = InitializeGame(path);
                string expectedVariant = right ? "K-M01__MERCY" : "K-M01__ZEAL";
                var notificationOrder = new List<string>();
                state.CurrentCardChanged += () => notificationOrder.Add("card");
                testSession.CasteChanged += (previous, next) =>
                {
                    notificationOrder.Add("caste");
                    SaveData saved = ReadTemporarySave(path);
                    Require(previous == 1 && next == 2, "전환 이벤트의 계층 번호가 다릅니다.");
                    Require(saved.checkpoints.Count == 2 && saved.GetLastCheckpoint().firstVariantId == expectedVariant,
                        "전환 이벤트 전에 새 체크포인트가 디스크에 기록되어야 합니다.");
                    Require(saved.progress.currentVariantId == expectedVariant && saved.progress.turnsUsedInCaste == 0,
                        "새 계층의 첫 카드와 턴을 함께 저장해야 합니다.");
                };

                for (int i = 0; i < 4; i++)
                    testSession.Choose(right);

                Require(notificationOrder.SequenceEqual(new[] { "card", "card", "card", "card", "caste" }),
                    "같은 계층에서는 카드만 갱신하고 계층 전환 때는 카드 이후에 계층을 알려야 합니다.");
                Require(state.GetCurrentCard()?.variantId == expectedVariant, "유산 조건에 맞는 첫 카드가 필요합니다.");
            });
        }
    }

    private void CheckSkipCaste()
    {
        string header = fixtures.CsvHeader;
        var database = new CardDatabase(string.Join("\n", header,
            CreateCsvRow("B-M01", "main", "B", 1),
            CreateCsvRow("K-M01", "main", "K", 2, "unavailable"),
            CreateCsvRow("V-M01", "main", "V", 3)));
        var store = new MemorySaveStore();
        var session = new GameSession(database, new CardSelector(), store);
        session.StartOrResume();
        var transitions = new List<(int previous, int next)>();
        session.CasteChanged += (previous, next) => transitions.Add((previous, next));
        session.Choose(false);
        Require(transitions.SequenceEqual(new[] { (1, 3) }), "빈 계층을 건너뛰고 실제 진입한 계층만 알려야 합니다.");
        Require(store.Saved.checkpoints.Select(item => item.casteOrder).SequenceEqual(new[] { 1, 3 }),
            "실제 진입한 계층만 체크포인트를 남겨야 합니다.");
    }

    private void CheckSaveFailure()
    {
        var store = new MemorySaveStore();
        var session = new GameSession(cardDatabase, new CardSelector(), store);
        session.StartOrResume();
        for (int i = 0; i < 3; i++)
            session.Choose(false);

        GameState state = session.State;
        CardData before = state.GetCurrentCard();
        Stat stats = state.GetStats();
        SaveData previousSave = store.Saved;
        int cardEvents = 0;
        int casteEvents = 0;
        state.CurrentCardChanged += () =>
        {
            cardEvents++;
            session.Choose(false);
        };
        session.CasteChanged += (_, _) => casteEvents++;
        store.FailWrites = true;
        ExpectThrows<IOException>(() => session.Choose(false));
        Require(ReferenceEquals(state.GetCurrentCard(), before) && state.GetStats().Equals(stats)
            && session.TurnsUsedInCaste == 3 && ReferenceEquals(store.Saved, previousSave),
            "쓰기 실패 시 진행과 카드를 그대로 유지해야 합니다.");
        Require(cardEvents == 0 && casteEvents == 0, "쓰기 실패 시 이벤트를 발행하면 안 됩니다.");

        store.FailWrites = false;
        session.Choose(false);
        Require(cardEvents == 1 && casteEvents == 1 && session.TurnsUsedInCaste == 0
            && state.GetCurrentCard()?.variantId == "K-M01__ZEAL", "재시도와 동기 재진입을 한 번의 선택으로 처리해야 합니다.");

        var inaccessibleStore = new MemorySaveStore { FailReads = true };
        var start = new GameSession(cardDatabase, new CardSelector(), inaccessibleStore);
        ExpectThrows<IOException>(start.StartOrResume);
        Require(inaccessibleStore.Saved == null && start.State.GetCurrentCard() == null,
            "읽기 접근 오류는 새 저장으로 덮어쓰면 안 됩니다.");

        var corruptStore = new MemorySaveStore { InvalidOnRead = true, FailWrites = true };
        var recovery = new GameSession(cardDatabase, new CardSelector(), corruptStore);
        int recoveryCardEvents = 0;
        recovery.State.CurrentCardChanged += () => recoveryCardEvents++;
        ExpectThrows<IOException>(recovery.StartOrResume);
        Require(recovery.State.GetCurrentCard() == null && recoveryCardEvents == 0,
            "손상 저장 교체가 실패하면 초기 카드를 공개하면 안 됩니다.");
    }

    private void CheckEmptyChoice()
    {
        WithTemporarySave(path =>
        {
            WriteTemporarySave(fixtures.CreateSaveFixture(null, Stat.Default), path);
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
        var store = new MemorySaveStore();
        var session = new GameSession(cardDatabase, new CardSelector(), store);
        session.StartOrResume();
        session.Choose(false);
        SaveData saved = store.Saved;
        Stat expectedStats = session.State.GetStats();

        session.State.AddFlag("later");
        session.State.MarkEventSeen("B-M02");
        session.State.SetStats(new Stat(61, 62, 63, 64));
        Require(saved.progress.flags.Count == 0 && saved.progress.seenEventIds.SequenceEqual(new[] { "B-M01" })
            && saved.progress.stats.Equals(expectedStats), "저장 스냅샷은 이후 상태 변경과 분리되어야 합니다.");
        Require(saved.GetLastCheckpoint().stats.Equals(Stat.Default), "진입 스탯은 첫 체크포인트에 보존되어야 합니다.");
    }

    private void CheckMissingCheckpoint()
    {
        WithTemporarySave(path =>
        {
            SaveData data = fixtures.CreateSaveFixture(demoCard, Stat.Default);
            data.checkpoints.Clear();
            string original = JsonConvert.SerializeObject(data);
            File.WriteAllText(path, original);
            GameState state = InitializeGame(path);
            SaveData replaced = ReadTemporarySave(path);
            Require(File.ReadAllText(path) != original && replaced.checkpoints.Count == 1,
                "빈 체크포인트가 있는 파일은 새 게임 저장으로 덮어써야 합니다.");
            Require(state.GetCurrentCard()?.variantId == "B-M01" && replaced.GetLastCheckpoint().firstVariantId == "B-M01",
                "첫 카드와 첫 체크포인트를 같이 복구해야 합니다.");
        });
    }

    private void CheckInvalidSaves()
    {
        WithTemporarySave(path =>
        {
            var store = new SaveSystem(path);
            ExpectThrows<InvalidDataException>(() => store.Save(null));
            ExpectThrows<InvalidDataException>(() => store.Save(new SaveData()));
            File.WriteAllText(path, "{\"progress\":");
            ExpectThrows<InvalidDataException>(() => store.LoadOrNull());
            Require(InitializeGame(path).GetCurrentCard()?.variantId == "B-M01", "손상 JSON은 새 게임으로 교체해야 합니다.");

            SaveData unknown = fixtures.CreateSaveFixture(demoCard, Stat.Default);
            unknown.progress.currentVariantId = "missing-card-for-test";
            store.Save(unknown);
            Require(InitializeGame(path).GetCurrentCard()?.variantId == "B-M01"
                && store.LoadOrNull().progress.currentVariantId == "B-M01",
                "존재하지 않는 카드 변형이 저장된 파일은 새 게임으로 교체해야 합니다.");
        });
    }

    private void CheckLegacySave()
    {
        WithTemporarySave(path =>
        {
            const string stats = "{\"faith\":50,\"order\":50,\"economy\":50,\"unrest\":50}";
            string legacy = "{\"checkpoints\":[{\"casteCode\":\"B\",\"stats\":" + stats
                + ",\"flags\":null}],\"progress\":{\"stats\":" + stats
                + ",\"flags\":null,\"seenEventIds\":[\"B-M01\"],\"currentCard\":{\"variantId\":\"B-M02\"}}}";
            File.WriteAllText(path, legacy);
            Require(InitializeGame(path).GetCurrentCard()?.variantId == "B-M02" && testSession.TurnsUsedInCaste == 1,
                "구형 저장의 카드 객체와 사건 기록을 복구해야 합니다.");
            Require(File.ReadAllText(path) == legacy, "기존 계층에서 이어할 때는 선택 전 파일을 유지해야 합니다.");

            File.WriteAllText(path, legacy.Replace("{\"variantId\":\"B-M02\"}", "null"));
            Require(InitializeGame(path).GetCurrentCard()?.variantId == "B-M02",
                "구형 파일의 빈 카드는 사건 기록을 바탕으로 다음 카드를 복구해야 합니다.");
            testSession.Choose(false);
            SaveData saved = ReadTemporarySave(path);
            Require(saved.schemaVersion == 1 && saved.progress.currentVariantId == "B-M03",
                "구형 저장에서 이어간 뒤에는 변형 ID가 포함된 새 형식을 저장해야 합니다.");
        });
    }

    private void CheckIncompleteSave()
    {
        WithTemporarySave(path =>
        {
            var store = new SaveSystem(path);
            store.Save(fixtures.CreateSaveFixture(demoCard, Stat.Default));
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
            var save = fixtures.CreateSaveFixture(cardDatabase.FindByVariantId("K-M03__MERCY"), progressStats, turnLimit: 2);
            save.checkpoints.Insert(0, fixtures.CreateSaveFixture(cardDatabase.FindByVariantId("B-M01"), Stat.Default).GetLastCheckpoint());
            save.GetLastCheckpoint().stats = checkpointStats;
            save.progress.flags.Add("trial_flag");
            save.progress.flags.Add("2026-11-12T00:00:00Z");
            save.progress.seenEventIds.Add("K-M01");
            save.progress.turnsUsedInCaste = 1;

            WriteTemporarySave(save, path);
            save.progress.seenEventIds.Add("K-M02");
            save.progress.turnsUsedInCaste = 2;
            WriteTemporarySave(save, path);

            SaveData loaded = ReadTemporarySave(path);
            Require(loaded.checkpoints.Select(item => item.casteCode).SequenceEqual(new[] { "B", "K" }),
                "Checkpoints should round-trip in caste order.");
            Require(loaded.progress.seenEventIds.SequenceEqual(new[] { "K-M01", "K-M02" }),
                "The overwritten save should contain the current caste's event history.");

            GameState state = InitializeGame(path);
            ExpectStats(progressStats);
            Require(state.GetCurrentCard()?.variantId == "K-M03__MERCY" && testSession.TurnsUsedInCaste == 2,
                "이어하기는 카드 변형과 사용 턴을 복원해야 합니다.");
            Require(state.HasSeenCard("K-M02") && state.HasFlag("trial_flag")
                && state.HasFlag("2026-11-12T00:00:00Z"), "이전 사건과 날짜 모양의 플래그도 문자열로 복원해야 합니다.");
            Require(!File.ReadAllText(path).Contains("\"currentCard\""), "Save only the current card ID.");
            state.AddFlag("live_only");

            RestoreTestCheckpoint();
            Require(ReferenceEquals(state, testSession.State), "Checkpoint restore should reuse the current GameState.");
            Require(state.GetCurrentCard()?.variantId == "K-M01__MERCY", "체크포인트의 첫 카드로 돌아가야 합니다.");
            ExpectStats(checkpointStats);
            Require(state.HasFlag("mercy") && !state.HasFlag("trial_flag") && !state.HasFlag("live_only"),
                "Restoring a checkpoint should replace the live flags.");

            SaveData restarted = ReadTemporarySave(path);
            Require(restarted.progress.stats.Equals(checkpointStats) && restarted.progress.turnsUsedInCaste == 0
                && restarted.progress.turnsPerCaste == 2, "진입 스탯과 설정값을 복원하여 저장해야 합니다.");
            Require(restarted.progress.flags.SequenceEqual(new[] { "mercy" }) && restarted.progress.seenEventIds.Count == 0,
                "재시작한 계층은 체크포인트 플래그와 빈 사건 기록으로 시작해야 합니다.");

            InitializeGame(path);
            ExpectStats(checkpointStats);
            Require(testState.GetCurrentCard()?.variantId == "K-M01__MERCY", "복원 저장에서도 첫 카드를 이어야 합니다.");
        });
    }

    private void RestoreTestCheckpoint()
    {
        Require(testSession.RestoreCheckpoint(), "The temporary save must have a checkpoint.");
    }

    private GameState InitializeGame(string path)
    {
        testSession = fixtures.CreateSession(new SaveSystem(path));
        testSession.StartOrResume();
        testState = testSession.State;
        return testState;
    }

    private void ExpectStats(Stat expected)
    {
        Stat actual = testState.GetStats();
        Require(actual.faith == expected.faith && actual.order == expected.order
            && actual.economy == expected.economy && actual.unrest == expected.unrest,
            $"Expected stats ({expected.faith}, {expected.order}, {expected.economy}, {expected.unrest}), " +
            $"got ({actual.faith}, {actual.order}, {actual.economy}, {actual.unrest}).");
    }

    private void RunCase(string name, Action check)
    {
        try
        {
            check();
            passed++;
            results.Add((name, null));
            Debug.Log($"[Integration Test] PASS {name}");
        }
        catch (Exception exception)
        {
            failed++;
            results.Add((name, exception.Message));
            Debug.LogError($"[Integration Test] FAIL {name}\n{exception}");
        }
    }

}
