using System.Linq;
using Caste.Cards;
using Caste.CardSwipe;
using Caste.Stats;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Caste.CardSwipe.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 카드 게임 테스트 씬(스탯 게이지 + 카드 스와이프)을 만든다.
    /// 메뉴: 상단 CASTE → JSW → Build Card Swipe Test Scene
    /// 만드는 것은 모두 Assets/JSW 안에만 저장한다. 기존 에셋(스탯·카드·설정)은 덮어쓰지 않는다.
    /// </summary>
    public static class CardSwipeTestSceneBuilder
    {
        private const string Root = "Assets/JSW";
        private const string ScenePath = Root + "/Scenes/JSW_CardSwipeTest.unity";
        private const string SettingsPath = Root + "/Settings/SwipeSettings.asset";
        private const string StatsDir = Root + "/Data/Stats";
        private const string CardsDir = Root + "/Data/Cards";
        private const string SpritesDir = Root + "/Sprites";
        private const string BackgroundSprite = SpritesDir + "/CardBoard_Background.png";
        private const string KoreanFontPath = Root + "/Font/NotoSansKR-Black SDF (2).asset";

        // 게임판(CardBoard.png, 1080x1920) 기준 아이콘 중심 좌표 (왼쪽 위 기준 픽셀)
        private static readonly Vector2[] IconCenters =
        {
            new(167f, 157f), new(414f, 157f), new(670f, 164f), new(908f, 162f),
        };
        private static readonly string[] IconFiles =
        {
            "게임화면_신앙스텟.png", "게임화면_치안스텟.png", "게임화면_재정스텟.png", "게임화면_불만스텟.png",
        };

        // 게임판 팔레트
        private static readonly Color BoardDark = new(29 / 255f, 19 / 255f, 5 / 255f);
        private static readonly Color BoardTan = new(144 / 255f, 124 / 255f, 91 / 255f);

        [MenuItem("CASTE/JSW/Build Card Swipe Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("테스트 씬 다시 만들기",
                    $"{ScenePath} 가 이미 있습니다. 덮어쓸까요?\n(스탯·카드·설정 에셋은 유지됩니다)", "덮어쓰기", "취소"))
                return;

            BuildScene();
        }

        /// <summary>확인 창 없이 씬을 만든다. (자동화·테스트용)</summary>
        public static void BuildScene()
        {
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Settings");
            EnsureFolder(Root, "Data");
            EnsureFolder(Root + "/Data", "Stats");
            EnsureFolder(Root + "/Data", "Cards");

            // ── 1단계: 데이터 에셋이 없으면 만들기 ──
            EnsureAssets();

            // ── 2단계: 새 씬을 먼저 만든 뒤 에셋 불러오기 ──
            // NewScene(Single)은 쓰지 않는 에셋을 메모리에서 내린다(UnloadUnusedAssets).
            // 씬을 만들기 전에 불러둔 에셋 참조는 파괴된 객체가 되어 씬에 빈 칸으로 저장되므로, 순서가 중요하다.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var settings = AssetDatabase.LoadAssetAtPath<SwipeSettings>(SettingsPath);
            StatDefinition[] stats =
            {
                Load<StatDefinition>(StatsDir, "Stat_Faith"), Load<StatDefinition>(StatsDir, "Stat_Security"),
                Load<StatDefinition>(StatsDir, "Stat_Finance"), Load<StatDefinition>(StatsDir, "Stat_Unrest"),
            };
            CardData[] cards =
            {
                Load<CardData>(CardsDir, "Card_01_Priest"), Load<CardData>(CardsDir, "Card_02_Captain"),
                Load<CardData>(CardsDir, "Card_03_Merchant"),
            };

            // ── 씬 구성 ──
            CreateEventSystem();
            RectTransform canvas = CreateCanvas();

            // 게임판 배경 (Sprite만 바꾸면 배경 교체)
            RectTransform background = CreateRect("Background", canvas);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.sprite = LoadSprite(BackgroundSprite);
            bgImage.color = bgImage.sprite != null ? Color.white : BoardTan;
            bgImage.raycastTarget = false;

            // 상단 스탯 바 (게임판 위쪽 어두운 띠, 높이 313)
            RectTransform statBar = CreateRect("StatBar", canvas);
            statBar.anchorMin = new Vector2(0f, 1f);
            statBar.anchorMax = new Vector2(1f, 1f);
            statBar.pivot = new Vector2(0.5f, 1f);
            statBar.anchoredPosition = Vector2.zero;
            statBar.sizeDelta = new Vector2(0f, 313f);

            var gauges = new StatGaugeView[stats.Length];
            for (int i = 0; i < stats.Length; i++) gauges[i] = CreateGauge(statBar, stats[i], IconCenters[i]);
            var statPanel = statBar.gameObject.AddComponent<StatPanelView>();
            SetRefArray(statPanel, "_gauges", gauges);

            // 대사 (게임판 밝은 영역 y 313~673)
            TMP_Text question = CreateText("Question", canvas, "", 46f, TextAlignmentOptions.Center, BoardDark);
            SetAnchored(question.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -333f), new Vector2(940f, 320f));

            // 카드 영역 (화면 전체 = 스와이프 기준)
            RectTransform area = CreateRect("CardArea", canvas);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;

            RectTransform card = CreateRect("Card", area);
            // 게임판 무늬 패널(x 81~1000, y 673~1578) 위에 정확히 겹치게 → 카드가 날아가면 뒤의 무늬 패널이 보인다
            card.sizeDelta = new Vector2(919f, 905f);
            card.anchoredPosition = new Vector2(0.5f, -165.5f);
            card.gameObject.AddComponent<Image>().color = new Color(0.93f, 0.85f, 0.7f);

            RectTransform portrait = CreateRect("Portrait", card);
            portrait.sizeDelta = new Vector2(620f, 620f);
            portrait.anchoredPosition = new Vector2(0f, -70f);
            var portraitImage = portrait.gameObject.AddComponent<Image>();
            portraitImage.color = new Color(0.75f, 0.65f, 0.5f);
            portraitImage.raycastTarget = false;

            TMP_Text leftLabel = CreateChoiceLabel("LeftChoice", card, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
            TMP_Text rightLabel = CreateChoiceLabel("RightChoice", card, TextAlignmentOptions.TopRight, new Vector2(1f, 1f));

            // 인물 이름 (카드 아래 밝은 띠 y 1578~1713)
            TMP_Text characterName = CreateText("CharacterName", canvas, "", 44f, TextAlignmentOptions.Center, BoardDark);
            SetAnchored(characterName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -685.5f), new Vector2(900f, 100f));

            // ── 컴포넌트 ──
            var swipeInput = card.gameObject.AddComponent<CardSwipeInput>();
            var motion = card.gameObject.AddComponent<CardMotion>();
            var choicePreview = card.gameObject.AddComponent<ChoicePreviewView>();
            var cardView = card.gameObject.AddComponent<CardView>();
            var keyboard = area.gameObject.AddComponent<KeyboardChoiceInput>();
            var swipe = area.gameObject.AddComponent<CardSwipeController>();

            var game = new GameObject("CardGame");
            var deck = game.AddComponent<CardDeck>();
            var gameController = game.AddComponent<CardGameController>();

            // ── 연결 ──
            SetRef(swipeInput, "_settings", settings);
            SetRef(motion, "_settings", settings);
            SetRef(choicePreview, "_leftChoice", leftLabel.GetComponent<CanvasGroup>());
            SetRef(choicePreview, "_rightChoice", rightLabel.GetComponent<CanvasGroup>());

            SetRef(cardView, "_question", question);
            SetRef(cardView, "_characterName", characterName);
            SetRef(cardView, "_portrait", portraitImage);
            SetRef(cardView, "_leftLabel", leftLabel);
            SetRef(cardView, "_rightLabel", rightLabel);

            SetRefArray(swipe, "_inputSources", new MonoBehaviour[] { swipeInput, keyboard });
            SetRef(swipe, "_motion", motion);
            SetRefArray(swipe, "_previewTargets", new MonoBehaviour[] { choicePreview, statPanel });

            SetRefArray(deck, "_cards", cards);
            SetRefArray(gameController, "_stats", stats);
            SetRef(gameController, "_swipe", swipe);
            SetRef(gameController, "_cardView", cardView);
            SetRef(gameController, "_statPanel", statPanel);
            SetRef(gameController, "_cardSource", deck);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[JSW] 테스트 씬 생성 완료: {ScenePath}  (Game 창 해상도를 1080x1920으로 맞춰주세요)");
        }

        // ───────── 에셋 ─────────

        /// <summary>
        /// 없는 에셋만 만든다. (사용자가 수정한 기존 에셋은 그대로)
        /// ① 빈 에셋을 모두 만들고 → ② 한 번에 저장·임포트 → ③ 새로 불러온 뒤 값을 채운다.
        /// 중간에 임포트가 끼어들면 앞서 잡아둔 참조가 끊기기 때문에 단계를 나눴다.
        /// </summary>
        private static void EnsureAssets()
        {
            string[] statFiles = { "Stat_Faith", "Stat_Security", "Stat_Finance", "Stat_Unrest" };
            string[] cardFiles = { "Card_01_Priest", "Card_02_Captain", "Card_03_Merchant" };

            CreateIfMissing<SwipeSettings>(SettingsPath);
            var newStats = new bool[statFiles.Length];
            for (int i = 0; i < statFiles.Length; i++)
                newStats[i] = CreateIfMissing<StatDefinition>($"{StatsDir}/{statFiles[i]}.asset");
            var newCards = new bool[cardFiles.Length];
            for (int i = 0; i < cardFiles.Length; i++)
                newCards[i] = CreateIfMissing<CardData>($"{CardsDir}/{cardFiles[i]}.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var faith = Load<StatDefinition>(StatsDir, statFiles[0]);
            var security = Load<StatDefinition>(StatsDir, statFiles[1]);
            var finance = Load<StatDefinition>(StatsDir, statFiles[2]);
            var unrest = Load<StatDefinition>(StatsDir, statFiles[3]);

            if (newStats[0]) InitStat(faith, "신앙", new Color(0.95f, 0.8f, 0.3f), true);
            if (newStats[1]) InitStat(security, "치안", new Color(0.35f, 0.6f, 0.95f), true);
            if (newStats[2]) InitStat(finance, "재정", new Color(0.4f, 0.8f, 0.45f), true);
            if (newStats[3]) InitStat(unrest, "불만", new Color(0.85f, 0.35f, 0.35f), false);

            // 아이콘이 비어 있는 스탯에만 Sprites 폴더의 아이콘을 넣는다 (사용자가 바꾼 아이콘은 유지)
            StatDefinition[] statAssets = { faith, security, finance, unrest };
            for (int i = 0; i < statAssets.Length; i++)
                AssignIconIfMissing(statAssets[i], $"{SpritesDir}/{IconFiles[i]}");

            if (newCards[0])
                InitCard(Load<CardData>(CardsDir, cardFiles[0]), "대사제", "신전을 보수하려면 금화가 필요합니다.",
                    "거절한다", new[] { (faith, -10), (unrest, 5) },
                    "기부한다", new[] { (faith, 15), (finance, -10) });
            if (newCards[1])
                InitCard(Load<CardData>(CardsDir, cardFiles[1]), "경비대장", "도적들이 길목을 휩쓸고 있습니다. 병사를 보낼까요?",
                    "무시한다", new[] { (security, -15), (unrest, 10) },
                    "파병한다", new[] { (security, 10), (finance, -5) });
            if (newCards[2])
                InitCard(Load<CardData>(CardsDir, cardFiles[2]), "상인", "시장 세금을 올리시겠습니까?",
                    "아니오", new[] { (finance, -5), (unrest, -5) },
                    "올린다", new[] { (finance, 20), (unrest, 15) });

            AssetDatabase.SaveAssets();
        }

        private static bool CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return false;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            return true;
        }

        private static T Load<T>(string dir, string file) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>($"{dir}/{file}.asset");

        /// <summary>Sprite Mode가 Single이든 Multiple이든 첫 번째 Sprite를 가져온다.</summary>
        private static Sprite LoadSprite(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

        private static void AssignIconIfMissing(StatDefinition stat, string iconPath)
        {
            if (stat.Icon != null) return;
            Sprite icon = LoadSprite(iconPath);
            if (icon == null) { Debug.LogWarning($"[JSW] 아이콘 없음: {iconPath}"); return; }

            var so = new SerializedObject(stat);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_color").colorValue = Color.white; // 아이콘 그림 색 그대로 사용
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stat);
        }

        private static void InitStat(StatDefinition stat, string displayName, Color color, bool higherIsBetter)
        {
            var so = new SerializedObject(stat);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_color").colorValue = color;
            so.FindProperty("_higherIsBetter").boolValue = higherIsBetter;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stat);
        }

        private static void InitCard(CardData card, string characterName, string question,
            string leftLabel, (StatDefinition stat, int amount)[] leftEffects,
            string rightLabel, (StatDefinition stat, int amount)[] rightEffects)
        {
            var so = new SerializedObject(card);
            so.FindProperty("_characterName").stringValue = characterName;
            so.FindProperty("_question").stringValue = question;
            SetChoice(so.FindProperty("_left"), leftLabel, leftEffects);
            SetChoice(so.FindProperty("_right"), rightLabel, rightEffects);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
        }

        private static void SetChoice(SerializedProperty choice, string label, (StatDefinition stat, int amount)[] effects)
        {
            choice.FindPropertyRelative("_label").stringValue = label;
            SerializedProperty list = choice.FindPropertyRelative("_effects");
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("_stat").objectReferenceValue = effects[i].stat;
                e.FindPropertyRelative("_amount").intValue = effects[i].amount;
            }
        }

        // ───────── UI ─────────

        /// <summary>
        /// 아이콘 모양 게이지. 같은 아이콘 3장을 겹친다:
        ///   Track(어둡게 칠한 빈 게이지) / PreviewFill(예상 구간) / Fill(아이콘 원래 색으로 차오름)
        /// center = 게임판 기준 아이콘 중심 (왼쪽 위 기준 픽셀)
        /// </summary>
        private static StatGaugeView CreateGauge(RectTransform parent, StatDefinition stat, Vector2 center)
        {
            RectTransform root = CreateRect($"Gauge_{stat.DisplayName}", parent);
            Sprite icon = stat.Icon != null ? stat.Icon : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Vector2 size = stat.Icon != null ? icon.rect.size : new Vector2(120f, 180f);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f); // 왼쪽 위 기준 → 게임판 픽셀 좌표 그대로
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(center.x, -center.y);
            root.sizeDelta = size;

            Image track = CreateGaugeImage("Track", root, icon, new Color(0.38f, 0.38f, 0.38f, 1f), false);
            Image preview = CreateGaugeImage("PreviewFill", root, icon, new Color(1f, 1f, 1f, 0f), true);
            Image fill = CreateGaugeImage("Fill", root, icon, stat.Color, true);

            // 변화량 숫자: 아이콘 아래 어두운 띠 안
            TMP_Text delta = CreateText("Delta", root, "", 40f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0f));
            SetAnchored(delta.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -45f), new Vector2(160f, 50f));
            delta.fontStyle = FontStyles.Bold;

            var view = root.gameObject.AddComponent<StatGaugeView>();
            SetRef(view, "_stat", stat);
            SetRef(view, "_track", track);
            SetRef(view, "_previewFill", preview);
            SetRef(view, "_fill", fill);
            SetRef(view, "_deltaText", delta);

            // 게임판 톤에 맞춘 미리보기 색 (인스펙터에서 변경 가능)
            var so = new SerializedObject(view);
            so.FindProperty("_goodColor").colorValue = new Color(1f, 0.86f, 0.55f);  // 밝은 금색
            so.FindProperty("_badColor").colorValue = new Color(0.78f, 0.22f, 0.14f); // 적갈색
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Image CreateGaugeImage(string name, RectTransform parent, Sprite sprite, Color color, bool filled)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (filled)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Vertical;
                img.fillOrigin = (int)Image.OriginVertical.Bottom;
            }
            return img;
        }

        private static TMP_Text CreateChoiceLabel(string name, RectTransform card, TextAlignmentOptions align, Vector2 corner)
        {
            TMP_Text tmp = CreateText(name, card, "", 52f, align, Color.black);
            RectTransform rect = tmp.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.sizeDelta = new Vector2(320f, 120f);
            rect.anchoredPosition = new Vector2(corner.x > 0.5f ? -30f : 30f, -30f);
            tmp.gameObject.AddComponent<CanvasGroup>();
            return tmp;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font != null) tmp.font = font; // 씬을 다시 만들어도 한글 폰트 유지
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false; // 텍스트가 드래그를 가로채지 않게
            return tmp;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // ───────── 참조 연결 (private [SerializeField]는 SerializedObject로 설정) ─────────

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty arr = so.FindProperty(field);
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
