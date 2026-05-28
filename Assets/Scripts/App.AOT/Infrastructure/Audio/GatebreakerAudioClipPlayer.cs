using System.Threading.Tasks;
using App.Shared.Contracts;
using UnityEngine;

namespace App.AOT.Infrastructure.Audio
{
    /// <summary>
    /// prefab 音频挂件：支持直接拖 AudioClip，也支持 YooAssets location。
    /// </summary>
    public sealed class GatebreakerAudioClipPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField] private string _assetLocation;
        [SerializeField] private AudioChannel _channel = AudioChannel.Sfx;
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _loop;
        [SerializeField] [Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] private bool _stopOnDisable = true;

        private IAudioPlaybackHandle _playbackHandle;
        private AudioSource _localSource;
        private int _playVersion;

        public IAudioPlaybackHandle LastPlaybackHandle => _playbackHandle;
        public AudioSource LocalAudioSource => _localSource;

        private async void OnEnable()
        {
            if (_playOnEnable)
            {
                await PlayAsync();
            }
        }

        private void OnDisable()
        {
            _playVersion++;
            if (_stopOnDisable)
            {
                _playbackHandle?.Stop();
                _playbackHandle = null;
                if (_localSource != null)
                {
                    _localSource.Stop();
                }
            }
        }

        public async Task<IAudioPlaybackHandle> PlayAsync()
        {
            int version = ++_playVersion;
            AudioPlayParameters parameters = new AudioPlayParameters(_loop, _volume);
            IAudioService audioService = AudioServiceRegistry.Current;
            IAudioPlaybackHandle handle;
            if (audioService != null)
            {
                handle = await PlayWithServiceAsync(audioService, parameters);
            }
            else
            {
                handle = PlayWithLocalSource(parameters);
            }

            if (version != _playVersion || !isActiveAndEnabled)
            {
                handle?.Stop();
                return AudioPlaybackHandle.Empty;
            }

            _playbackHandle = handle;
            return _playbackHandle;
        }

        private Task<IAudioPlaybackHandle> PlayWithServiceAsync(IAudioService audioService, AudioPlayParameters parameters)
        {
            if (_clip != null)
            {
                IAudioPlaybackHandle handle = _channel == AudioChannel.Music
                    ? audioService.PlayMusic(_clip, parameters)
                    : audioService.PlaySfx(_clip, parameters);
                return Task.FromResult(handle);
            }

            return _channel == AudioChannel.Music
                ? audioService.PlayMusicAsync(_assetLocation, parameters)
                : audioService.PlaySfxAsync(_assetLocation, parameters);
        }

        private IAudioPlaybackHandle PlayWithLocalSource(AudioPlayParameters parameters)
        {
            if (_clip == null)
            {
                Debug.LogWarning($"GatebreakerAudioClipPlayer: no AudioClip and no audio service available. location={_assetLocation}");
                return AudioPlaybackHandle.Empty;
            }

            if (_localSource == null)
            {
                _localSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                _localSource.playOnAwake = false;
            }

            _localSource.Stop();
            _localSource.clip = _clip;
            _localSource.loop = parameters.Loop;
            _localSource.volume = Mathf.Clamp01(parameters.Volume);
            _localSource.Play();
            return new LocalPlaybackHandle(_localSource);
        }

#if UNITY_EDITOR
        public void AssignForEditor(
            AudioClip clip,
            string assetLocation,
            AudioChannel channel,
            bool playOnEnable,
            bool loop,
            float volume,
            bool stopOnDisable)
        {
            _clip = clip;
            _assetLocation = assetLocation;
            _channel = channel;
            _playOnEnable = playOnEnable;
            _loop = loop;
            _volume = volume;
            _stopOnDisable = stopOnDisable;
        }
#endif

        private sealed class LocalPlaybackHandle : IAudioPlaybackHandle
        {
            private readonly AudioSource _source;

            public LocalPlaybackHandle(AudioSource source)
            {
                _source = source;
            }

            public bool IsPlaying => _source != null && _source.isPlaying;

            public void Stop()
            {
                _source?.Stop();
            }
        }
    }
}
