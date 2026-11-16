using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Caste.EditorTools
{
    /// <summary>
    /// 한글 폰트를 "필요한 글자를 미리 구운 Static 메인 폰트 + 드문 글자용 Dynamic 보조 폰트"로 다시 만든다.
    /// 왜: Dynamic 폰트는 플레이할 때마다 파일 자체가 바뀌어서, 두 사람이 각자 플레이 후 커밋하면 git 충돌이 난다.
    ///     Static 메인 폰트는 이 메뉴를 실행할 때만 바뀐다.
    /// 메뉴: 상단 CASTE → JSW → Rebuild Korean Font
    /// 언제: CSV(대사·엔딩)에 새 글자가 많이 늘었을 때, 브랜치 머지 후 한 번.
    /// 보조 폰트는 매번 비운 상태로 저장되므로, 충돌이 나면 어느 쪽을 골라도 상관없다.
    /// </summary>
    public static class KoreanFontRebuilder
    {
        private const string MainPath = "Assets/JSW/Font/NotoSansKR-Black SDF (2).asset";
        private const string FallbackPath = "Assets/JSW/Font/NotoSansKR-Black SDF Fallback.asset";
        private const string TtfPath = "Assets/JSW/Font/NotoSansKR-Black (2).ttf";

        private const int SamplingPointSize = 64; // 글자를 저장하는 크기 (17pt일 때 뭉개졌던 문제 방지)
        private const int Padding = 7;
        private const int AtlasSize = 4096;

        // 대사·UI에서 쓸 만한 문장부호/기호
        private const string ExtraChars = "“”‘’…·—–※○●◎◆◇■□▲△▼▽→←↑↓★☆♡♥「」『』《》〈〉【】、。~!?%+-×÷=<>";

        [MenuItem("CASTE/JSW/Rebuild Korean Font")]
        public static void RebuildFromMenu() => Debug.Log("[KoreanFont] " + Rebuild());

        public static string Rebuild()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            var main = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainPath);
            if (ttf == null || main == null) return "폰트 파일(ttf 또는 SDF 에셋)을 찾지 못했습니다.";

            string chars = CollectCharacters();
            FontEngine.InitializeFontEngine();

            // ── 1) 메인 폰트: 잠깐 Dynamic으로 글자를 구운 뒤 Static으로 고정 (GUID 유지 → 씬 연결 그대로) ──
            var so = new SerializedObject(main);
            // 필드 이름이 TMP 버전마다 달라서 있는 것만 설정
            foreach (string name in new[] { "m_SourceFontFile_EditorRef", "m_SourceFontFile" })
            {
                var src = so.FindProperty(name);
                if (src != null) src.objectReferenceValue = ttf;
            }
            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
            so.FindProperty("m_AtlasPadding").intValue = Padding;
            so.FindProperty("m_AtlasWidth").intValue = AtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
            var multi = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
            if (multi != null) multi.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (FontEngine.LoadFontFace(ttf, SamplingPointSize) != FontEngineError.Success)
                return "ttf를 불러오지 못했습니다.";
            main.faceInfo = FontEngine.GetFaceInfo();
            main.ClearFontAssetData(true);
            main.TryAddCharacters(chars, out string missing);

            so.Update();
            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 2) 보조 폰트: 메인에 없는 글자만 실행 중에 채움 (저장은 항상 빈 상태) ──
            TMP_FontAsset fallback = GetOrCreateFallback(ttf);
            fallback.ClearFontAssetData(true);

            main.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!main.fallbackFontAssetTable.Contains(fallback)) main.fallbackFontAssetTable.Add(fallback);

            EditorUtility.SetDirty(main);
            EditorUtility.SetDirty(fallback);
            AssetDatabase.SaveAssets();

            return $"완료: 글자 {main.characterTable.Count}개, 아틀라스 {main.atlasTextures.Length}장 ({AtlasSize}px, {SamplingPointSize}pt)" +
                   (string.IsNullOrEmpty(missing) ? "" : $", 폰트에 없는 글자 {missing.Length}개");
        }

        private static TMP_FontAsset GetOrCreateFallback(Font ttf)
        {
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            if (fallback == null)
            {
                fallback = TMP_FontAsset.CreateFontAsset(ttf, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                fallback.name = Path.GetFileNameWithoutExtension(FallbackPath);
                AssetDatabase.CreateAsset(fallback, FallbackPath);
                fallback.atlasTexture.name = fallback.name + " Atlas";
                fallback.material.name = fallback.name + " Material";
                AssetDatabase.AddObjectToAsset(fallback.atlasTexture, fallback);
                AssetDatabase.AddObjectToAsset(fallback.material, fallback);
            }

            var so = new SerializedObject(fallback);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) clear.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return fallback;
        }

        /// <summary>
        /// 미리 구울 글자: 영문·숫자 + 자주 쓰는 한글 2,350자(KS X 1001) + 한글 자모 + 기호
        /// + 프로젝트의 모든 CSV/TXT/JSON 글자 + JSW 씬·에셋·스크립트에 쓰인 글자.
        /// (팀원 파일은 읽기만 한다)
        /// </summary>
        private static string CollectCharacters()
        {
            var set = new HashSet<char>();
            for (int c = 0x20; c < 0x7F; c++) set.Add((char)c);
            for (int c = 0x3131; c <= 0x3163; c++) set.Add((char)c);
            foreach (char c in ExtraChars) set.Add(c);

            try
            {
                var cp949 = Encoding.GetEncoding(949);
                for (int hi = 0xB0; hi <= 0xC8; hi++)
                for (int lo = 0xA1; lo <= 0xFE; lo++)
                {
                    string s = cp949.GetString(new[] { (byte)hi, (byte)lo });
                    if (s.Length == 1 && s[0] >= 0xAC00 && s[0] <= 0xD7A3) set.Add(s[0]);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KoreanFont] KS X 1001 글자표를 만들지 못했습니다(파일 속 글자만 사용): " + e.Message);
            }

            var unicodeEscape = new Regex(@"\\u([0-9A-Fa-f]{4})");
            foreach (string raw in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                string path = raw.Replace('\\', '/');
                string ext = Path.GetExtension(path).ToLowerInvariant();
                bool data = ext == ".csv" || ext == ".txt" || ext == ".json";
                bool jsw = path.StartsWith("Assets/JSW/") && !path.Contains("/Font/") &&
                           (ext == ".unity" || ext == ".prefab" || ext == ".asset" || ext == ".cs");
                if (!data && !jsw) continue;
                if (path.Contains("TextMesh Pro")) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }

                // 씬/에셋(YAML)은 한글을 신 같은 형태로 저장한다
                if (ext == ".unity" || ext == ".prefab" || ext == ".asset")
                    text = unicodeEscape.Replace(text, m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

                foreach (char c in text)
                    if (c > 0x7F && !char.IsControl(c) && !char.IsSurrogate(c)) set.Add(c);
            }

            var sb = new StringBuilder(set.Count);
            foreach (char c in set) sb.Append(c);
            return sb.ToString();
        }
    }
}
