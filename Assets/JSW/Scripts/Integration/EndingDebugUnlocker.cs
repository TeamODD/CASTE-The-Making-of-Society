namespace Caste.Integration
{
    // using을 namespace 안에 둔다: 전역 타입(JMS)과 이름이 겹쳐도 Caste 쪽을 먼저 찾게 하기 위함.
    using System;
    using System.Collections.Generic;
    using UnityEngine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    /// <summary>
    /// 개발자용: 단축키(기본 F5)를 누르면 모든 엔딩을 해금한다.
    /// 해금 기록은 백엔드(JMS)의 EndingArchiveStore로 저장하므로, 실제로 엔딩을 본 것과 똑같이 취급된다.
    /// 백엔드 코드는 고치지 않는다. (팀원 작업 공간 보호, OCP)
    /// 붙이는 곳: 아무 오브젝트 (타이틀·게임 씬 어디서 눌러도 같은 파일에 저장됨)
    /// </summary>
    [AddComponentMenu("Caste/Integration/Ending Debug Unlocker")]
    public class EndingDebugUnlocker : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        [Tooltip("모든 엔딩을 해금할 키")]
        [SerializeField] private Key _key = Key.F5;
#else
        [Tooltip("모든 엔딩을 해금할 키")]
        [SerializeField] private KeyCode _key = KeyCode.F5;
#endif

        [Tooltip("켜면 에디터·Development Build에서만 동작 (정식 빌드에서 실수로 해금되는 것 방지)")]
        [SerializeField] private bool _debugBuildOnly = true;

        [Tooltip("(선택) 해금 직후 도감을 새로고침할 바인더. 도감이 열려 있으면 바로 반영된다")]
        [SerializeField] private BackendEndingCollectionBinder _collectionBinder;

        private void Awake()
        {
            if (_debugBuildOnly && !Debug.isDebugBuild) enabled = false;
        }

        private void Update()
        {
            if (Pressed()) UnlockAll();
        }

        [ContextMenu("모든 엔딩 해금")]
        public void UnlockAll()
        {
            try
            {
                var ids = CollectAllEndingIds();
                var archive = new EndingArchiveStore(); // GameManager와 같은 기본 경로(archive.json)
                foreach (string id in ids) archive.RecordSeen(id);

                if (_collectionBinder != null) _collectionBinder.Reload();
                Debug.Log($"[EndingDebug] 엔딩 {ids.Count}개를 모두 해금했습니다.", this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        // 백엔드에는 "전체 엔딩 ID 목록"을 주는 공개 기능이 없다.
        // 대신 도감 목록을 만들 때 백엔드가 엔딩마다 "이 ID 봤어?"라고 묻는 것을 받아 적는다.
        private static List<string> CollectAllEndingIds()
        {
            var recorder = new IdRecordingSet();
            EndingDatabase.LoadDefault().GetCollection(recorder);
            return recorder.Asked;
        }

        /// <summary>질문받은 ID를 순서대로 기록하고, 모두 "봤다"고 답하는 집합.</summary>
        private sealed class IdRecordingSet : HashSet<string>, ISet<string>
        {
            public readonly List<string> Asked = new List<string>();

            // ISet을 다시 구현해서, 백엔드가 ISet으로 호출하는 Contains만 가로챈다.
            bool ICollection<string>.Contains(string item)
            {
                if (!string.IsNullOrEmpty(item) && Add(item)) Asked.Add(item);
                return true;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private bool Pressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb[_key].wasPressedThisFrame;
        }
#else
        private bool Pressed() => Input.GetKeyDown(_key);
#endif
    }
}
