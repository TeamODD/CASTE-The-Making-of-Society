using UnityEngine;

namespace Caste.Audio
{
    /// <summary>
    /// "여기서는 이 곡을 틀어라"라는 요청 한 개.
    /// 씬 시작 시 자동으로 틀거나, UnityEvent(예: 엔딩 시작)에 Play()를 연결해서 쓴다.
    /// 곡을 바꾸고 싶으면 코드 수정 없이 인스펙터에서 Clip만 바꾸면 된다. (OCP)
    /// 붙이는 곳: 아무 오브젝트 (씬 하나에 여러 개 가능)
    /// </summary>
    [AddComponentMenu("Caste/Audio/Bgm Cue")]
    public class BgmCue : MonoBehaviour
    {
        [Tooltip("틀 곡. 비워두고 Play()하면 음악이 서서히 꺼진다")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("켜면 씬이 시작될 때 자동으로 재생")]
        [SerializeField] private bool _playOnStart = true;

        [Tooltip("페이드 시간(초). -1 = BgmPlayer의 기본값 사용")]
        [SerializeField] private float _fadeDuration = -1f;

        private void Start()
        {
            if (_playOnStart) Play();
        }

        /// <summary>지정한 곡 재생 (UnityEvent에 연결 가능)</summary>
        public void Play()
        {
            var player = BgmPlayer.Instance;
            if (player == null)
            {
                Debug.LogWarning($"[BgmCue] {name}: 씬에 BgmPlayer가 없습니다.", this);
                return;
            }
            player.Play(_clip, _fadeDuration);
        }

        /// <summary>배경음 끄기 (UnityEvent에 연결 가능)</summary>
        public void Stop()
        {
            BgmPlayer.Instance?.Stop(_fadeDuration);
        }
    }
}
