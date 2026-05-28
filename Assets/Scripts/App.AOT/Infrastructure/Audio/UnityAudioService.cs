using System.Collections.Generic;
using System.Threading.Tasks;
using App.Shared.Contracts;
using UnityEngine;

namespace App.AOT.Infrastructure.Audio
{
    /// <summary>
    /// Unity 音频服务：AOT 宿主能力，只负责播放和资源加载，不承载玩法规则。
    /// </summary>
    public sealed class UnityAudioService : IAudioService
    {
        private readonly IAssetsRuntime _assetsRuntime;
        private readonly IAppLogger _logger;
        private readonly int _sfxPoolSize;
        private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
        private readonly List<SourcePlaybackHandle> _transientHandles = new List<SourcePlaybackHandle>();
        private GameObject _root;
        private AudioSource _musicSource;
        private IAssetHandle _musicAssetHandle;
        private int _musicVersion;
        private int _nextSfxIndex;
        private float _musicClipVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;

        public UnityAudioService(IAssetsRuntime assetsRuntime, IAppLogger logger = null, int sfxPoolSize = 8)
        {
            _assetsRuntime = assetsRuntime;
            _logger = logger;
            _sfxPoolSize = Mathf.Max(1, sfxPoolSize);
        }

        public AudioSource MusicSource => _musicSource;
        public IReadOnlyList<AudioSource> SfxSources => _sfxSources;

        public void Initialize()
        {
            EnsureInitialized();
            AudioServiceRegistry.Register(this);
        }

        public void Update(float deltaTime)
        {
            CleanupTransientHandles();
        }

        public void Shutdown()
        {
            StopMusic();
            StopAllSfx();
            ReleaseTransientHandles();
            AudioServiceRegistry.Clear(this);
            if (_root != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_root);
                }
                else
                {
                    Object.DestroyImmediate(_root);
                }

                _root = null;
            }
        }

        public IAudioPlaybackHandle PlayMusic(AudioClip clip, AudioPlayParameters parameters)
        {
            EnsureInitialized();
            if (clip == null)
            {
                _logger?.LogWarning("UnityAudioService: music clip is null.");
                return AudioPlaybackHandle.Empty;
            }

            ReleaseMusicHandle();
            _musicVersion++;
            ApplyMusicClipToSource(clip, parameters);
            _musicSource.Play();
            return new MusicPlaybackHandle(this, _musicVersion);
        }

        public async Task<IAudioPlaybackHandle> PlayMusicAsync(string assetLocation, AudioPlayParameters parameters)
        {
            IAssetHandle handle = await LoadAudioClipAsync(assetLocation, "music");
            if (!(handle?.AssetObject is AudioClip clip))
            {
                handle?.Release();
                return AudioPlaybackHandle.Empty;
            }

            IAudioPlaybackHandle playbackHandle = PlayMusic(clip, parameters);
            ReleaseMusicHandle();
            _musicAssetHandle = handle;
            return playbackHandle;
        }

        public IAudioPlaybackHandle PlaySfx(AudioClip clip, AudioPlayParameters parameters)
        {
            EnsureInitialized();
            if (clip == null)
            {
                _logger?.LogWarning("UnityAudioService: sfx clip is null.");
                return AudioPlaybackHandle.Empty;
            }

            CleanupTransientHandles();
            AudioSource source = AcquireSfxSource();
            ApplyClipToSource(source, clip, parameters, _sfxVolume);
            source.Play();
            var handle = new SourcePlaybackHandle(source, null);
            _transientHandles.Add(handle);
            return handle;
        }

        public async Task<IAudioPlaybackHandle> PlaySfxAsync(string assetLocation, AudioPlayParameters parameters)
        {
            IAssetHandle handle = await LoadAudioClipAsync(assetLocation, "sfx");
            if (!(handle?.AssetObject is AudioClip clip))
            {
                handle?.Release();
                return AudioPlaybackHandle.Empty;
            }

            EnsureInitialized();
            CleanupTransientHandles();
            AudioSource source = AcquireSfxSource();
            ApplyClipToSource(source, clip, parameters, _sfxVolume);
            source.Play();
            var playbackHandle = new SourcePlaybackHandle(source, handle);
            _transientHandles.Add(playbackHandle);
            return playbackHandle;
        }

        public void StopMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }

            _musicVersion++;
            ReleaseMusicHandle();
        }

        public void StopAllSfx()
        {
            foreach (AudioSource source in _sfxSources)
            {
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
            }

            ReleaseTransientHandles();
        }

        public void SetVolume(AudioChannel channel, float volume)
        {
            float clampedVolume = ClampVolume(volume);
            if (channel == AudioChannel.Music)
            {
                _musicVolume = clampedVolume;
                if (_musicSource != null)
                {
                    _musicSource.volume = _musicClipVolume * _musicVolume;
                }
            }
            else
            {
                _sfxVolume = clampedVolume;
            }
        }

        public float GetVolume(AudioChannel channel)
        {
            return channel == AudioChannel.Music ? _musicVolume : _sfxVolume;
        }

        private void StopMusicIfVersion(int version)
        {
            if (version == _musicVersion)
            {
                StopMusic();
            }
        }

        private void EnsureInitialized()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("App Audio Service");
            Object.DontDestroyOnLoad(_root);
            _musicSource = CreateSource("Music", _root.transform);
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                _sfxSources.Add(CreateSource($"Sfx {i + 1:00}", _root.transform));
            }
        }

        private static AudioSource CreateSource(string name, Transform parent)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private AudioSource AcquireSfxSource()
        {
            if (_sfxSources.Count == 0)
            {
                _sfxSources.Add(CreateSource("Sfx 01", _root.transform));
            }

            AudioSource source = _sfxSources[_nextSfxIndex];
            _nextSfxIndex = (_nextSfxIndex + 1) % _sfxSources.Count;
            return source;
        }

        private async Task<IAssetHandle> LoadAudioClipAsync(string assetLocation, string role)
        {
            if (string.IsNullOrWhiteSpace(assetLocation))
            {
                _logger?.LogWarning("UnityAudioService: {0} asset location is empty.", role);
                return null;
            }

            if (_assetsRuntime == null)
            {
                _logger?.LogWarning("UnityAudioService: IAssetsRuntime missing for {0}. location={1}", role, assetLocation);
                return null;
            }

            IAssetHandle handle = await _assetsRuntime.LoadAssetAsync(assetLocation);
            if (!(handle?.AssetObject is AudioClip))
            {
                _logger?.LogWarning("UnityAudioService: {0} asset is not an AudioClip. location={1}", role, assetLocation);
            }

            return handle;
        }

        private static void ApplyClipToSource(AudioSource source, AudioClip clip, AudioPlayParameters parameters, float channelVolume)
        {
            source.Stop();
            source.clip = clip;
            source.loop = parameters.Loop;
            source.volume = ClampVolume(parameters.Volume) * ClampVolume(channelVolume);
        }

        private void ApplyMusicClipToSource(AudioClip clip, AudioPlayParameters parameters)
        {
            _musicClipVolume = ClampVolume(parameters.Volume);
            _musicSource.Stop();
            _musicSource.clip = clip;
            _musicSource.loop = parameters.Loop;
            _musicSource.volume = _musicClipVolume * _musicVolume;
        }

        private static float ClampVolume(float volume)
        {
            return Mathf.Clamp01(volume);
        }

        private void ReleaseMusicHandle()
        {
            _musicAssetHandle?.Release();
            _musicAssetHandle = null;
        }

        private void CleanupTransientHandles()
        {
            for (int i = _transientHandles.Count - 1; i >= 0; i--)
            {
                SourcePlaybackHandle handle = _transientHandles[i];
                if (handle == null || !handle.IsPlaying)
                {
                    handle?.ReleaseOnly();
                    _transientHandles.RemoveAt(i);
                }
            }
        }

        private void ReleaseTransientHandles()
        {
            for (int i = 0; i < _transientHandles.Count; i++)
            {
                _transientHandles[i]?.ReleaseOnly();
            }

            _transientHandles.Clear();
        }

        private sealed class MusicPlaybackHandle : IAudioPlaybackHandle
        {
            private readonly UnityAudioService _service;
            private readonly int _version;

            public MusicPlaybackHandle(UnityAudioService service, int version)
            {
                _service = service;
                _version = version;
            }

            public bool IsPlaying => _service?._musicSource != null && _service._musicSource.isPlaying;

            public void Stop()
            {
                _service?.StopMusicIfVersion(_version);
            }
        }

        private sealed class SourcePlaybackHandle : IAudioPlaybackHandle
        {
            private readonly AudioSource _source;
            private IAssetHandle _assetHandle;

            public SourcePlaybackHandle(AudioSource source, IAssetHandle assetHandle)
            {
                _source = source;
                _assetHandle = assetHandle;
            }

            public bool IsPlaying => _source != null && _source.isPlaying;

            public void Stop()
            {
                if (_source != null)
                {
                    _source.Stop();
                    _source.clip = null;
                }

                ReleaseOnly();
            }

            public void ReleaseOnly()
            {
                _assetHandle?.Release();
                _assetHandle = null;
            }
        }
    }

    public sealed class AudioPlaybackHandle : IAudioPlaybackHandle
    {
        public static readonly IAudioPlaybackHandle Empty = new AudioPlaybackHandle();

        public bool IsPlaying => false;

        public void Stop()
        {
        }
    }
}
