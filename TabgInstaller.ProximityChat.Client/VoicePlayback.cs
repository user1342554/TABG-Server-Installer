using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Concentus.Enums;
using Concentus.Structs;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class VoicePlayback : IDisposable
    {
        private const int SampleRate = 48000;
        private const int FrameSamples = 960;
        private const int RingBufferSize = SampleRate * 2;
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
            public byte[] OpusData;
            public int OpusLength;
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
                OpusData = opusData,
                OpusLength = opusLength
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
                ProcessAudioOnMainThread(queued.SenderId, queued.Sequence, queued.OpusData, queued.OpusLength);
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

        private void ProcessAudioOnMainThread(int senderId, ushort sequence, byte[] opusData, int opusLength)
        {
            if (!_sources.TryGetValue(senderId, out var source))
            {
                if (_sources.Count >= MaxSources) return;
                source = new PlayerVoiceSource(senderId, SampleRate, _minRange, _maxRange, _rolloffMode, _masterVolume);
                _sources[senderId] = source;

                if (_playerTransformCache.TryGetValue(senderId, out var transform))
                    source.AttachToPlayer(transform);
            }

            source.DecodeAndFeed(opusData, opusLength);
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
            private readonly AudioSource _audioSource;
            private readonly AudioClip _clip;
            private readonly float[] _ringBuffer;
            private readonly OpusDecoder _decoder;
            private readonly short[] _pcmBuffer = new short[FrameSamples];
            private int _writePos;
            private readonly object _lock = new object();

            public float LastReceiveTime;
            public bool IsAttached { get; private set; }

            public PlayerVoiceSource(int playerId, int sampleRate, float minDist, float maxDist, AudioRolloffMode rolloff, float masterVol)
            {
                _ringBuffer = new float[RingBufferSize];
                _decoder = new OpusDecoder(sampleRate, 1);

                _go = new GameObject($"VoiceSource_{playerId}");
                UnityEngine.Object.DontDestroyOnLoad(_go);

                _audioSource = _go.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1.0f;
                _audioSource.minDistance = minDist;
                _audioSource.maxDistance = maxDist;
                _audioSource.rolloffMode = rolloff;
                _audioSource.volume = masterVol;
                _audioSource.loop = true;
                _audioSource.dopplerLevel = 0f;
                _audioSource.spread = 0f;

                _clip = AudioClip.Create($"Voice_{playerId}", RingBufferSize, 1, sampleRate, true, OnPcmRead);
                _audioSource.clip = _clip;
                _audioSource.Play();

                LastReceiveTime = Time.unscaledTime;
            }

            public void DecodeAndFeed(byte[] opusData, int opusLength)
            {
                int decoded;
                try
                {
                    decoded = _decoder.Decode(opusData, 0, opusLength, _pcmBuffer, 0, FrameSamples, false);
                }
                catch { return; }
                if (decoded <= 0) return;

                lock (_lock)
                {
                    for (int i = 0; i < decoded; i++)
                    {
                        _ringBuffer[_writePos] = _pcmBuffer[i] / 32768f;
                        _writePos = (_writePos + 1) % RingBufferSize;
                    }
                }
            }

            private int _readPos;

            private void OnPcmRead(float[] data)
            {
                lock (_lock)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = _ringBuffer[_readPos];
                        _ringBuffer[_readPos] = 0f;
                        _readPos = (_readPos + 1) % RingBufferSize;
                    }
                }
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
                if (_audioSource != null)
                {
                    _audioSource.Stop();
                    _audioSource.clip = null;
                }
                if (_clip != null) UnityEngine.Object.Destroy(_clip);
                if (_go != null) UnityEngine.Object.Destroy(_go);
            }
        }
    }
}
