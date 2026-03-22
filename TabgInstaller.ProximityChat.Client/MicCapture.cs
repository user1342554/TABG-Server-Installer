using System;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class MicCapture : IDisposable
    {
        private const int SampleRate = 48000;
        private const int FrameSizeMs = 20;
        private const int FrameSamples = SampleRate * FrameSizeMs / 1000; // 960 samples at 48kHz

        private AudioClip _micClip;
        private int _lastSamplePos;
        private readonly float[] _sampleBuffer = new float[FrameSamples];
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

            while (available >= FrameSamples)
            {
                _micClip.GetData(_sampleBuffer, _lastSamplePos);
                _lastSamplePos = (_lastSamplePos + FrameSamples) % _micClip.samples;
                available -= FrameSamples;

                float rms = 0f;
                for (int i = 0; i < FrameSamples; i++)
                    rms += _sampleBuffer[i] * _sampleBuffer[i];
                rms = Mathf.Sqrt(rms / FrameSamples);

                if (rms < sensitivity) continue;

                // Convert float[-1,1] to 16-bit PCM bytes (little-endian)
                // 960 samples * 2 bytes = 1920 bytes per frame
                byte[] pcmBytes = new byte[FrameSamples * 2];
                for (int i = 0; i < FrameSamples; i++)
                {
                    short sample = (short)(Mathf.Clamp(_sampleBuffer[i], -1f, 1f) * 32767f);
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
