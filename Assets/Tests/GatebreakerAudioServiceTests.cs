using System.Collections.Generic;
using System.Threading.Tasks;
using App.AOT.Infrastructure.Audio;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerAudioServiceTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioServiceRegistry.Clear(AudioServiceRegistry.Current);
        }

        [Test]
        public void AudioClipPlayer_UsesDraggedClipBeforeAssetLocation()
        {
            var fakeAudio = new RecordingAudioService();
            AudioServiceRegistry.Register(fakeAudio);
            AudioClip clip = CreateClip("dragged");
            var playerObject = new GameObject("Audio Player");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<GatebreakerAudioClipPlayer>();
            player.AssignForEditor(clip, "Assets/Audio/ignored.wav", AudioChannel.Music, false, true, 0.5f, true);
            playerObject.SetActive(true);

            player.PlayAsync().GetAwaiter().GetResult();

            Assert.AreSame(clip, fakeAudio.MusicClip);
            Assert.IsNull(fakeAudio.MusicLocation);
            Assert.IsTrue(fakeAudio.LastParameters.Loop);
            Assert.AreEqual(0.5f, fakeAudio.LastParameters.Volume);

            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void AudioClipPlayer_UsesAssetLocationWhenClipIsMissing()
        {
            var fakeAudio = new RecordingAudioService();
            AudioServiceRegistry.Register(fakeAudio);
            var playerObject = new GameObject("Audio Player");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<GatebreakerAudioClipPlayer>();
            player.AssignForEditor(null, "Assets/Audio/click.wav", AudioChannel.Sfx, false, false, 0.75f, true);
            playerObject.SetActive(true);

            player.PlayAsync().GetAwaiter().GetResult();

            Assert.AreEqual("Assets/Audio/click.wav", fakeAudio.SfxLocation);
            Assert.IsNull(fakeAudio.SfxClip);
            Assert.IsFalse(fakeAudio.LastParameters.Loop);
            Assert.AreEqual(0.75f, fakeAudio.LastParameters.Volume);

            Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void AudioClipPlayer_FallsBackToLocalAudioSourceWithoutAudioService()
        {
            AudioClip clip = CreateClip("local");
            var playerObject = new GameObject("Audio Player");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<GatebreakerAudioClipPlayer>();
            player.AssignForEditor(clip, string.Empty, AudioChannel.Sfx, false, true, 0.25f, true);
            playerObject.SetActive(true);

            player.PlayAsync().GetAwaiter().GetResult();

            Assert.IsNotNull(player.LocalAudioSource);
            Assert.AreSame(clip, player.LocalAudioSource.clip);
            Assert.IsTrue(player.LocalAudioSource.loop);
            Assert.AreEqual(0.25f, player.LocalAudioSource.volume);

            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void UnityAudioService_LoadsLocationThroughAssetsRuntime()
        {
            AudioClip clip = CreateClip("loaded");
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("Assets/Audio/bgm.wav", clip);
            var service = new UnityAudioService(assetsRuntime, null, 2);
            service.Initialize();

            service.PlayMusicAsync("Assets/Audio/bgm.wav", new AudioPlayParameters(true, 0.4f))
                .GetAwaiter()
                .GetResult();

            CollectionAssert.AreEqual(new[] { "Assets/Audio/bgm.wav" }, assetsRuntime.LoadedLocations);
            Assert.AreSame(clip, service.MusicSource.clip);
            Assert.IsTrue(service.MusicSource.loop);
            Assert.AreEqual(0.4f, service.MusicSource.volume);
            Assert.IsFalse(assetsRuntime.Handles["Assets/Audio/bgm.wav"].Released);

            service.Shutdown();
            Assert.IsTrue(assetsRuntime.Handles["Assets/Audio/bgm.wav"].Released);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void UnityAudioService_ReusesSingleMusicSourceForRepeatedBgm()
        {
            AudioClip firstClip = CreateClip("first");
            AudioClip secondClip = CreateClip("second");
            var service = new UnityAudioService(null, null, 2);
            service.Initialize();

            service.PlayMusic(firstClip, new AudioPlayParameters(true, 0.4f));
            AudioSource musicSource = service.MusicSource;
            service.PlayMusic(secondClip, new AudioPlayParameters(false, 0.7f));

            Assert.AreSame(musicSource, service.MusicSource);
            Assert.AreSame(secondClip, service.MusicSource.clip);
            Assert.IsFalse(service.MusicSource.loop);
            Assert.AreEqual(0.7f, service.MusicSource.volume);

            service.SetVolume(AudioChannel.Music, 0.5f);
            Assert.AreEqual(0.35f, service.MusicSource.volume, 0.0001f);

            service.Shutdown();
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
        }

        [Test]
        public void UnityAudioService_UsesSfxPoolWithoutTouchingMusicSource()
        {
            AudioClip firstClip = CreateClip("sfx-a");
            AudioClip secondClip = CreateClip("sfx-b");
            var service = new UnityAudioService(null, null, 2);
            service.Initialize();

            service.PlaySfx(firstClip, new AudioPlayParameters(false, 0.8f));
            service.PlaySfx(secondClip, new AudioPlayParameters(false, 0.6f));

            Assert.AreEqual(2, service.SfxSources.Count);
            Assert.AreSame(firstClip, service.SfxSources[0].clip);
            Assert.AreSame(secondClip, service.SfxSources[1].clip);
            Assert.IsNull(service.MusicSource.clip);

            service.Shutdown();
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
        }

        private static AudioClip CreateClip(string name)
        {
            return AudioClip.Create(name, 64, 1, 44100, false);
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public AudioClip MusicClip { get; private set; }
            public AudioClip SfxClip { get; private set; }
            public string MusicLocation { get; private set; }
            public string SfxLocation { get; private set; }
            public AudioPlayParameters LastParameters { get; private set; }

            public void Initialize()
            {
            }

            public void Update(float deltaTime)
            {
            }

            public void Shutdown()
            {
            }

            public IAudioPlaybackHandle PlayMusic(AudioClip clip, AudioPlayParameters parameters)
            {
                MusicClip = clip;
                LastParameters = parameters;
                return AudioPlaybackHandle.Empty;
            }

            public Task<IAudioPlaybackHandle> PlayMusicAsync(string assetLocation, AudioPlayParameters parameters)
            {
                MusicLocation = assetLocation;
                LastParameters = parameters;
                return Task.FromResult(AudioPlaybackHandle.Empty);
            }

            public IAudioPlaybackHandle PlaySfx(AudioClip clip, AudioPlayParameters parameters)
            {
                SfxClip = clip;
                LastParameters = parameters;
                return AudioPlaybackHandle.Empty;
            }

            public Task<IAudioPlaybackHandle> PlaySfxAsync(string assetLocation, AudioPlayParameters parameters)
            {
                SfxLocation = assetLocation;
                LastParameters = parameters;
                return Task.FromResult(AudioPlaybackHandle.Empty);
            }

            public void StopMusic()
            {
            }

            public void StopAllSfx()
            {
            }

            public void SetVolume(AudioChannel channel, float volume)
            {
            }

            public float GetVolume(AudioChannel channel)
            {
                return 1f;
            }
        }

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly Dictionary<string, FakeAssetHandle> _handles = new Dictionary<string, FakeAssetHandle>();
            private readonly List<string> _loadedLocations = new List<string>();

            public IReadOnlyDictionary<string, FakeAssetHandle> Handles => _handles;
            public IReadOnlyList<string> LoadedLocations => _loadedLocations;

            public void Add(string location, Object asset)
            {
                _handles[location] = new FakeAssetHandle(asset);
            }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public Task<bool> RunPatchFlowAsync(string packageVersion = null)
            {
                return Task.FromResult(true);
            }

            public Task<IAssetHandle> LoadAssetAsync(string location)
            {
                _loadedLocations.Add(location);
                _handles.TryGetValue(location, out FakeAssetHandle handle);
                return Task.FromResult<IAssetHandle>(handle);
            }

            public void Shutdown()
            {
            }
        }

        private sealed class FakeAssetHandle : IAssetHandle
        {
            public FakeAssetHandle(Object assetObject)
            {
                AssetObject = assetObject;
            }

            public Object AssetObject { get; }
            public bool Released { get; private set; }

            public void Release()
            {
                Released = true;
            }
        }
    }
}
