using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Caste.Integration.EditorTools
{
    /// <summary>
    /// EventCsvMergeConfig에 등록된 CSV들을 하나로 합친다.
    /// - 소스 CSV나 설정이 바뀌면 자동으로 다시 만든다 (AssetPostprocessor).
    /// - 합치는 일만 한다. 카드 규칙·파싱은 백엔드(JMS CardDatabase)가 그대로 담당한다. (SRP)
    /// </summary>
    public class EventCsvMerger : AssetPostprocessor
    {
        private const string MenuPath = "CASTE/이벤트 CSV 다시 합치기";
        private static bool _scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool relevant = imported.Concat(moved)
                .Any(p => p.EndsWith(".csv") || p.EndsWith(".asset"));
            if (!relevant || _scheduled) return;

            // 임포트 도중에 파일을 쓰면 꼬일 수 있어서 한 프레임 뒤에 실행한다.
            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                foreach (var config in LoadConfigs())
                    if (config.autoRebuild) Rebuild(config, logUnchanged: false);
            };
        }

        [MenuItem(MenuPath)]
        private static void RebuildAll()
        {
            var configs = LoadConfigs();
            if (configs.Length == 0)
                Debug.LogWarning("[EventCsvMerger] EventCsvMergeConfig 에셋이 없습니다. Create > CASTE > Event CSV Merge Config로 만드세요.");
            foreach (var config in configs) Rebuild(config, logUnchanged: true);
        }

        public static bool Rebuild(EventCsvMergeConfig config, bool logUnchanged)
        {
            var sources = config.sources?.Where(s => s != null).ToArray();
            if (sources == null || sources.Length == 0)
            {
                Debug.LogWarning("[EventCsvMerger] 합칠 CSV가 비어 있습니다.", config);
                return false;
            }

            string header = null;
            int rows = 0;
            var sb = new StringBuilder();

            foreach (var src in sources)
            {
                string path = AssetDatabase.GetAssetPath(src);
                if (path == config.outputPath) continue; // 결과 파일을 자기 자신에 다시 합치지 않도록

                // TextAsset.text는 임포트 직후 옛 내용일 수 있어서 디스크에서 직접 읽는다.
                string[] lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
                string fileHeader = lines[0].Trim('﻿', '\r', ' ');

                if (header == null)
                {
                    header = fileHeader;
                    sb.Append(header).Append("\r\n");
                }
                else if (NormalizeHeader(fileHeader, config) != NormalizeHeader(header, config))
                {
                    Debug.LogError($"[EventCsvMerger] {src.name}의 헤더(열 구성)가 첫 번째 CSV와 달라서 합치지 않았습니다.", src);
                    return false;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    sb.Append(lines[i].TrimEnd('\r')).Append("\r\n");
                    rows++;
                }
            }

            string merged = sb.ToString();
            string outPath = config.outputPath;

            // 내용이 같으면 쓰지 않는다 → 불필요한 재임포트와 무한 반복 방지
            if (File.Exists(outPath) && File.ReadAllText(outPath) == merged)
            {
                if (logUnchanged) Debug.Log($"[EventCsvMerger] 변경 없음 ({rows}줄): {outPath}");
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, merged, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(outPath);
            Debug.Log($"[EventCsvMerger] {sources.Length}개 CSV → {rows}줄로 합침: {outPath}");
            return true;
        }

        // 같은 뜻의 열 이름(예: unrest ↔ publicOp)을 하나로 맞춘 뒤 비교 → 열 이름만 바뀐 경우 팀원 파일을 고치지 않고 합칠 수 있다
        private static string NormalizeHeader(string header, EventCsvMergeConfig config)
        {
            if (config.headerAliases == null) return header;
            foreach (var alias in config.headerAliases)
                if (!string.IsNullOrEmpty(alias.from) && alias.to != null)
                    header = header.Replace(alias.from, alias.to);
            return header;
        }

        private static EventCsvMergeConfig[] LoadConfigs() =>
            AssetDatabase.FindAssets("t:" + nameof(EventCsvMergeConfig))
                .Select(guid => AssetDatabase.LoadAssetAtPath<EventCsvMergeConfig>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null)
                .ToArray();
    }
}
