using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class SmoothVideoLoader : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Target RawImage for video output")]
    public RawImage videoDisplay;

    private VideoPlayer _videoPlayer;

    void Start()
    {
        _videoPlayer = GetComponent<VideoPlayer>();

        UpdateAudioState();

        if (videoDisplay != null) videoDisplay.color = new Color(1f, 1f, 1f, 0f);
        _videoPlayer.prepareCompleted += OnVideoPrepared;
        _videoPlayer.Prepare();
    }

    public void UpdateAudioState()
    {
        if (_videoPlayer != null)
        {
            bool soundEnabled = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
            // Глушим аудио дорожку видео, если звуки выключены
            ushort trackCount = _videoPlayer.audioTrackCount;
            for (ushort i = 0; i < trackCount; i++)
            {
                _videoPlayer.SetDirectAudioMute(i, !soundEnabled);
                _videoPlayer.SetDirectAudioVolume(i, soundEnabled ? 1f : 0f);
            }
        }
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (videoDisplay != null) videoDisplay.color = new Color(1f, 1f, 1f, 1f);
        vp.Play();
    }

    public void ChangeVideo(VideoClip newClip)
    {
        if (_videoPlayer == null) _videoPlayer = GetComponent<VideoPlayer>();
        if (newClip == null || _videoPlayer.clip == newClip) return;

        // Hide screen while loading new video
        if (videoDisplay != null) 
            videoDisplay.color = new Color(1f, 1f, 1f, 0f);
        
        _videoPlayer.Stop();
        _videoPlayer.clip = newClip;
        _videoPlayer.Prepare(); // OnVideoPrepared will fire automatically
    }
}
