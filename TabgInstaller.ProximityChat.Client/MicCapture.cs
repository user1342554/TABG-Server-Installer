using System;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class MicCapture : IDisposable
    {
        private const int SampleRate = 48000;
        private const int FrameSizeMs = 20;
        private const int FrameSamples48k = SampleRate * FrameSizeMs / 1000; // 960 samples at 48kHz
        private const int TargetSampleRate = 16000;
        private const int FrameSamples = TargetSampleRate * FrameSizeMs / 1000; // 320 samples at 16kHz
        private const int DownsampleFactor = SampleRate / TargetSampleRate; // 3

        private AudioClip _micClip;
        private int _lastSamplePos;
        private readonly float[] _sampleBuffer = new float[FrameSamples48k];
        private readonly string _deviceName;
        private bool _recording;

        public event Action<byte[], int> OnFrameEncoded;

        public MicCapture(string deviceName)
        {
            _deviceName = string.IsNullOrEmpty(deviceName) ? null : deviceName;
        }

        public void StartRecording()
        {
            if (_recording) return;
            string device = _deviceName;
            _micClip = Microphone.Start(device, true, 2, SampleRate);
            _lastSamplePos = 0;
            _recording = true;
        }

        public void StopRecording()
        {
            if (!_recording) return;
            Microphone.End(_deviceName);
            _recording = false;
        }

        public void ProcessMicData(float sensitivity)
        {
            if (!_recording || _micClip == null) return;

            int currentPos = Microphone.GetPosition(_deviceName);
            if (currentPos == _lastSamplePos) return;

            int available;
            if (currentPos > _lastSamplePos)
                available = currentPos - _lastSamplePos;
            else
                available = (_micClip.samples - _lastSamplePos) + currentPos;

            while (available >= FrameSamples48k)
            {
                _micClip.GetData(_sampleBuffer, _lastSamplePos);
                _lastSamplePos = (_lastSamplePos + FrameSamples48k) % _micClip.samples;
                available -= FrameSamples48k;

                float rms = 0f;
                for (int i = 0; i < FrameSamples48k; i++)
                    rms += _sampleBuffer[i] * _sampleBuffer[i];
                rms = Mathf.Sqrt(rms / FrameSamples48k);

                if (rms < sensitivity) continue;

                // Downsample from 48kHz to 16kHz by taking every 3rd sample,
                // then convert to 16-bit PCM bytes (little-endian)
                byte[] pcmBytes = new byte[FrameSamples * 2];
                for (int i = 0; i < FrameSamples; i++)
                {
                    short sample = (short)(Mathf.Clamp(_sampleBuffer[i * DownsampleFactor], -1f, 1f) * 32767f);
                    pcmBytes[i * 2]     = (byte)(sample & 0xFF);
                    pcmBytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                }

                OnFrameEncoded?.Invoke(pcmBytes, pcmBytes.Length);
            }
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}
