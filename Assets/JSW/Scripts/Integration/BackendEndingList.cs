namespace Caste.Integration
{
    // using을 namespace 안에 둔다: 전역 타입(JMS)과 이름이 겹쳐도 Caste 쪽을 먼저 찾게 하기 위함.
    using System.Collections.Generic;
    using Caste.Endings;
    using BackendGroup = global::EndingCollectionGroup;

    /// <summary>
    /// 백엔드(JMS) 엔딩 도감 데이터 → 도감 화면(JSW)용 목록으로 바꾼다. (SRP: 변환만)
    /// 해금 기록은 백엔드가 엔딩 발생 시 직접 남기므로 Unlock은 아무것도 하지 않는다.
    /// </summary>
    public sealed class BackendEndingList : IEndingList, IEndingUnlockStore
    {
        private readonly List<EndingEntry> _entries = new();
        private readonly HashSet<string> _unlocked = new();

        public BackendEndingList(IReadOnlyList<BackendGroup> groups, bool showCasteName, int titleLength)
        {
            if (groups == null) return;

            foreach (var group in groups)
            {
                if (group?.Entries == null) continue;
                foreach (var e in group.Entries)
                {
                    if (e == null) continue;
                    string title = MakeTitle(e.Text, titleLength);
                    if (showCasteName && !string.IsNullOrEmpty(group.CasteName)) title = $"[{group.CasteName}] {title}";

                    _entries.Add(new EndingEntry
                    {
                        id = e.Id,
                        title = title,
                        description = e.Text,
                        illustration = e.Illustration,
                    });
                    if (e.IsUnlocked) _unlocked.Add(e.Id);
                }
            }
        }

        public int Count => _entries.Count;

        public string GetId(int index) => index >= 0 && index < _entries.Count ? _entries[index].id : null;

        public bool TryGet(int index, out EndingEntry entry)
        {
            bool ok = index >= 0 && index < _entries.Count;
            entry = ok ? _entries[index] : null;
            return ok;
        }

        public bool IsUnlocked(string endingId) => endingId != null && _unlocked.Contains(endingId);

        public void Unlock(string endingId) { } // 백엔드가 기록함

        // 엔딩 문장 첫 줄을 칸 제목으로 사용 (길면 자르고 … 붙임)
        private static string MakeTitle(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string first = text.Replace("\r", "").Split('\n')[0].Trim();
            return first.Length <= maxLength ? first : first.Substring(0, maxLength) + "…";
        }
    }
}
