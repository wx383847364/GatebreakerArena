using System.Collections;
using System.Threading.Tasks;
using App.AOT.Infrastructure.Audio;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gatebreaker.Tests.PlayMode
{
    public sealed class GatebreakerAudioClipPlayerSmokeTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioServiceRegistry.Clear(AudioServiceRegistry.Current);
        }

        [UnityTest]
        public IEnumerator AudioClipPlayer_PlayOnEnable_ForwardsLoopingClipToAudioService()
        {
            var fakeAudio = new RecordingAudioService();
            AudioServiceRegistry.Register(fakeAudio);
            AudioClip clip = AudioClip.Create("playmode-sfx", 64, 1, 44100, false);
            var playerObject = new GameObject("Audio Player Smoke");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<GatebreakerAudioClipPlayer>();
            player.AssignForEditor(clip, "Assets/Audio/ignored.wav", AudioChannel.Sfx, true, true, 0.3f, true);

            playerObject.SetActive(true);
            yield return null;

            Assert.AreSame(clip, fakeAudio.SfxClip);
            Assert.IsTrue(fakeAudio.LastParameters.Loop);
            Assert.AreEqual(0.3f, fakeAudio.LastParameters.Volume);

            Object.Destroy(playerObject);
            Object.Destroy(clip);
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public AudioClip SfxClip { get; private set; }
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
                return AudioPlaybackHandle.Empty;
            }

            public Task<IAudioPlaybackHandle> PlayMusicAsync(string assetLocation, AudioPlayParameters parameters)
            {
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
    }
}
