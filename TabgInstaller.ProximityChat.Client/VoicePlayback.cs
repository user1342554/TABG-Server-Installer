using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class VoicePlayback : IDisposable
    {
        private const int SampleRate = 48000;
        private const int FrameSamples = 960; // 20ms at 48kHz
        private const int RingBufferSize = SampleRate * 2; // 2 seconds at 48kHz
        private const int MaxSources = 32;

        private readonly Dictionary<int, PlayerVoiceSource> _sources = new Dictionary<int, PlayerVoiceSource>();
        private readonly float _masterVolume;
        private float _minRange;
        private float _maxRange;
        private AudioRolloffMode _rolloffMode;

        private readonly ConcurrentQueue<QueuedAudio> _audioQueue = new ConcurrentQueue<QueuedAudio>();

        private struct QueuedAudio
        {
            public int SenderId;
            public ushort Sequence;
            public byte[] PcmData;
            public int PcmLength;
        }

        private readonly Dictionary<int, Transform> _playerTransformCache = new Dictionary<int, Transform>();
        private float _nextPlayerCacheRefresh;

        public VoicePlayback(float masterVolume)
        {
            _masterVolume = masterVolume;
        }

        public void UpdateConfig(float minRange, float maxRange, byte falloffCurve)
        {
            _minRange = minRange;
            _maxRange = maxRange;
            _rolloffMode = falloffCurve == 0 ? AudioRolloffMode.Linear : AudioRolloffMode.Logarithmic;
        }

        public void EnqueueAudio(int senderId, ushort sequence, byte[] opusData, int opusLength)
        {
            _audioQueue.Enqueue(new QueuedAudio
            {
                SenderId = senderId,
                Sequence = sequence,
                PcmData = opusData,
                PcmLength = opusLength
            });
        }

        public void Tick()
        {
            if (Time.unscaledTime >= _nextPlayerCacheRefresh)
            {
                RefreshPlayerCache();
                _nextPlayerCacheRefresh = Time.unscaledTime + 0.5f;
            }

            while (_audioQueue.TryDequeue(out var queued))
            {
                ProcessAudioOnMainThread(queued.SenderId, queued.Sequence, queued.PcmData, queued.PcmLength);
            }

            foreach (var kvp in _sources)
            {
                if (!kvp.Value.IsAttached && _playerTransformCache.TryGetValue(kvp.Key, out var transform))
                    kvp.Value.AttachToPlayer(transform);
            }

            var toRemove = new List<int>();
            foreach (var kvp in _sources)
            {
                if (Time.unscaledTime - kvp.Value.LastReceiveTime > 0.5f)
                    toRemove.Add(kvp.Key);
            }
            foreach (int id in toRemove)
            {
                _sources[id].Dispose();
                _sources.Remove(id);
            }
        }

        private void ProcessAudioOnMainThread(int senderId, ushort sequence, byte[] pcmData, int pcmLength)
        {
            if (!_sources.TryGetValue(senderId, out var source))
            {
                if (_sources.Count >= MaxSources) return;
                source = new PlayerVoiceSource(senderId, _minRange, _maxRange, _rolloffMode, _masterVolume);
                _sources[senderId] = source;

                if (_playerTransformCache.TryGetValue(senderId, out var transform))
                    source.AttachToPlayer(transform);
            }

            source.DecodeAndFeed(pcmData, pcmLength);
            source.LastReceiveTime = Time.unscaledTime;
        }

        private void RefreshPlayerCache()
        {
            _playerTransformCache.Clear();
            try
            {
                var handler = Landfall.Network.PhotonServerHandler.instance;
                if (handler == null) return;
                var players = handler.AllPlayers;
                if (players == null) return;
                foreach (var player in players)
                {
                    if (player != null && player.PlayerObject != null)
                        _playerTransformCache[(int)player.PlayerIndex] = player.PlayerObject.transform;
                }
            }
            catch { }
        }

        public Dictionary<int, Transform> GetPlayerTransformCache()
        {
            return _playerTransformCache;
        }

        public bool IsPlayerTalking(int playerId)
        {
            return _sources.ContainsKey(playerId);
        }

        public IEnumerable<int> GetTalkingPlayerIds()
        {
            return _sources.Keys;
        }

        public void Dispose()
        {
            foreach (var kvp in _sources)
                kvp.Value.Dispose();
            _sources.Clear();
        }

        private class PlayerVoiceSource : IDisposable
        {
            private readonly GameObject _go;
            private readonly VoiceAudioFilter _filter;

            public float LastReceiveTime;
            public bool IsAttached { get; private set; }

            public PlayerVoiceSource(int playerId, float minDist, float maxDist, AudioRolloffMode rolloff, float masterVol)
            {
                _go = new GameObject($"VoiceSource_{playerId}");
                UnityEngine.Object.DontDestroyOnLoad(_go);

                // AudioSource needed for OnAudioFilterRead to fire
                var audioSource = _go.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
                audioSource.volume = masterVol;
                audioSource.loop = true;
                audioSource.dopplerLevel = 0f;
                audioSource.bypassEffects = true;
                audioSource.bypassListenerEffects = true;
                audioSource.bypassReverbZones = true;

                // Play a 1-second silent clip so OnAudioFilterRead gets called continuously
                audioSource.clip = AudioClip.Create($"Silent_{playerId}", SampleRate, 1, SampleRate, false);
                audioSource.Play();

                // VoiceAudioFilter reads from its ring buffer and writes into the audio pipeline
                _filter = _go.AddComponent<VoiceAudioFilter>();

                LastReceiveTime = Time.unscaledTime;
            }

            public void DecodeAndFeed(byte[] pcmData, int pcmLength)
            {
                if (_filter == null) return;
                int sampleCount = pcmLength / 2;
                if (sampleCount <= 0) return;

                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
                    samples[i] = sample / 32768f;
                }
                _filter.Feed(samples);
            }

            public void AttachToPlayer(Transform playerTransform)
            {
                if (_go != null && playerTransform != null)
                {
                    _go.transform.SetParent(playerTransform, false);
                    IsAttached = true;
                }
            }

            public void Dispose()
            {
                if (_go != null) UnityEngine.Object.Destroy(_go);
            }
        }
    }

    // MonoBehaviour that streams audio via OnAudioFilterRead.
    // Must be a public non-nested class so Unity's AddComponent can find it.
    public class VoiceAudioFilter : MonoBehaviour
    {
        private float[] _buffer = new float[96000]; // 2 sec at 48kHz
        private int _writePos;
        private int _readPos;
        private int _available; // how many samples are ready to play

        public void Feed(float[] samples)
        {
            lock (this)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _buffer[_writePos] = samples[i];
                    _writePos = (_writePos + 1) % _buffer.Length;
                    _available = Math.Min(_available + 1, _buffer.Length);
                }
            }
        }

        // Called by Unity's audio thread — output voice data when available, silence otherwise
        private void OnAudioFilterRead(float[] data, int channels)
        {
            lock (this)
            {
                for (int i = 0; i < data.Length; i += channels)
                {
                    float sample = 0f;
                    if (_available > 0)
                    {
                        sample = _buffer[_readPos];
                        _buffer[_readPos] = 0f;
                        _readPos = (_readPos + 1) % _buffer.Length;
                        _available--;
                    }

                    for (int ch = 0; ch < channels; ch++)
                        data[i + ch] = sample;
                }
            }
        }
    }
}
