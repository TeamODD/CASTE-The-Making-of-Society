using UnityEngine;

namespace Caste.Audio
{
    /// <summary>
    /// BGM을 "틀어달라 / 꺼달라"고 요청하는 쪽이 아는 최소한의 약속.
    /// 요청하는 쪽(BgmCue 등)은 실제 재생 방식(크로스페이드, 음량 계산)을 모른다. (ISP, DIP)
    /// </summary>
    public interface IBgmPlayer
    {
        AudioClip CurrentClip { get; }

        /// <summary>곡을 바꾼다. 같은 곡이 이미 나오고 있으면 처음부터 다시 틀지 않고 이어서 재생.</summary>
        /// <param name="fadeDuration">음수면 재생기의 기본 페이드 시간 사용</param>
        void Play(AudioClip clip, float fadeDuration = -1f);

        /// <summary>현재 곡을 서서히 끈다.</summary>
        void Stop(float fadeDuration = -1f);
    }
}
