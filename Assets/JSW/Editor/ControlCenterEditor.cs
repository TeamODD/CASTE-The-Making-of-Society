using System.Collections.Generic;
using System.Linq;
using Caste.Tools;
using UnityEditor;
using UnityEngine;

namespace Caste.EditorTools
{
    /// <summary>
    /// ControlCenter를 선택하면 씬의 Caste 컴포넌트와 설정 에셋의 인스펙터를
    /// 분류별 접이식 묶음으로 한 화면에 그린다. 여기서 고치면 원래 컴포넌트의 값이 바뀐다. (Undo 지원)
    /// </summary>
    [CustomEditor(typeof(ControlCenter))]
    public class ControlCenterEditor : Editor
    {
        private const string Root = "Caste";
        private const string AssetFolder = "Assets/JSW";

        // 네임스페이스 → 보기 좋은 분류 이름 / 순서
        private static readonly (string ns, string title)[] SectionMap =
        {
            ("Caste.Settings", "게임 설정"),
            ("Caste.UI", "UI (메뉴 / 설정창 / 스킨)"),
            ("Caste.CardSwipe", "카드 스와이프"),
            ("Caste.Cards", "카드"),
            ("Caste.Stats", "스탯"),
            ("Caste.Effects", "효과"),
            ("Caste.Turn", "턴 카운터"),
        };

        private readonly Dictionary<Object, Editor> _editors = new Dictionary<Object, Editor>();
        private string _search = "";

        private ControlCenter Hub => (ControlCenter)target;

        private void OnEnable()
        {
            if (Hub.sections == null || Hub.sections.Count == 0) Collect();
        }

        private void OnDisable()
        {
            foreach (var e in _editors.Values) if (e != null) DestroyImmediate(e);
            _editors.Clear();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "게임의 조절 가능한 항목을 한곳에 모았습니다. 여기서 고치면 원래 오브젝트의 값이 바뀝니다.\n" +
                "새 스크립트나 에셋을 추가했다면 '자동으로 채우기'를 눌러주세요.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("자동으로 채우기")) Collect();
                if (GUILayout.Button("모두 펼치기")) SetAllExpanded(true);
                if (GUILayout.Button("모두 접기")) SetAllExpanded(false);
            }

            _search = EditorGUILayout.TextField("검색 (이름)", _search);
            EditorGUILayout.Space(6);

            foreach (var section in Hub.sections)
            {
                var targets = section.targets.Where(t => t != null && MatchesSearch(t)).ToList();
                if (targets.Count == 0) continue;
                DrawSection(section.title, targets);
            }

            EditorGUILayout.Space(10);
            DrawRawList();
        }

        private void DrawSection(string title, List<Object> targets)
        {
            string key = "Caste.CC.S." + title;
            bool open = SessionState.GetBool(key, false);

            var style = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, $"{title}  ({targets.Count})", style);
            SessionState.SetBool(key, open);
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!open) return;

            EditorGUI.indentLevel++;
            foreach (var t in targets) DrawTarget(t);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void DrawTarget(Object t)
        {
            string key = "Caste.CC.T." + t.GetInstanceID();
            bool open = SessionState.GetBool(key, false);

            using (new EditorGUILayout.HorizontalScope())
            {
                open = EditorGUILayout.Foldout(open, Label(t), true);
                if (GUILayout.Button("선택", GUILayout.Width(44)))
                {
                    Selection.activeObject = t is Component c ? c.gameObject : t;
                    EditorGUIUtility.PingObject(t);
                }
            }
            SessionState.SetBool(key, open);
            if (!open) return;

            if (!_editors.TryGetValue(t, out var editor) || editor == null)
            {
                editor = CreateEditor(t);
                _editors[t] = editor;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                editor.OnInspectorGUI();
            }
        }

        private void DrawRawList()
        {
            bool open = SessionState.GetBool("Caste.CC.Raw", false);
            open = EditorGUILayout.Foldout(open, "목록 직접 편집 (고급)", true);
            SessionState.SetBool("Caste.CC.Raw", open);
            if (!open) return;

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sections"), true);
            serializedObject.ApplyModifiedProperties();
        }

        private bool MatchesSearch(Object t) =>
            string.IsNullOrEmpty(_search) ||
            Label(t).IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Label(Object t)
        {
            string typeName = ObjectNames.NicifyVariableName(t.GetType().Name);
            return t is Component c ? $"{c.gameObject.name}  —  {typeName}" : $"{t.name}  —  {typeName} (에셋)";
        }

        private void SetAllExpanded(bool value)
        {
            foreach (var s in Hub.sections)
            {
                SessionState.SetBool("Caste.CC.S." + s.title, value);
                foreach (var t in s.targets)
                    if (t != null) SessionState.SetBool("Caste.CC.T." + t.GetInstanceID(), value);
            }
        }

        /// <summary>씬의 Caste 컴포넌트 + Assets/JSW의 Caste ScriptableObject를 분류해서 채운다.</summary>
        private void Collect()
        {
            var found = new List<Object>();

            // 1) 씬 오브젝트 (계층 순서대로)
            foreach (var rootGo in Hub.gameObject.scene.GetRootGameObjects())
                foreach (var mb in rootGo.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && IsCaste(mb.GetType()) && !(mb is ControlCenter))
                        found.Add(mb);

            // 2) 설정 에셋
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { AssetFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && IsCaste(asset.GetType())) found.Add(asset);
            }

            var sections = new List<ControlCenter.Section>();
            foreach (var (ns, title) in SectionMap)
            {
                var list = found.Where(o => o.GetType().Namespace == ns).ToList();
                if (list.Count > 0) sections.Add(new ControlCenter.Section { title = title, targets = list });
            }

            // 분류표에 없는 새 네임스페이스는 자동으로 별도 묶음
            var known = new HashSet<string>(SectionMap.Select(m => m.ns));
            foreach (var group in found.Where(o => !known.Contains(o.GetType().Namespace))
                                       .GroupBy(o => o.GetType().Namespace))
            {
                sections.Add(new ControlCenter.Section { title = group.Key ?? "기타", targets = group.ToList() });
            }

            Undo.RecordObject(Hub, "Control Center 자동 채우기");
            Hub.sections = sections;
            EditorUtility.SetDirty(Hub);
        }

        private static bool IsCaste(System.Type type)
        {
            string ns = type.Namespace;
            return ns != null && (ns == Root || ns.StartsWith(Root + ".")) && ns != "Caste.Tools";
        }
    }
}
