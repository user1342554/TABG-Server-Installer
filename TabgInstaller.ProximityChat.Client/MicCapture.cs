using System;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class MicCapture : IDisposable
    {
        private const int SampleRate = 48000;         // capture at 48kHz
        private const int TargetRate = 16000;         // output at 16kHz
        private const int DownsampleFactor = 3;       // 48000 / 16000
        private const int FrameSizeMs = 20;
        private const int FrameSamples48k = SampleRate * FrameSizeMs / 1000;    // 960 samples at 48kHz
        private const int FrameSamplesTarget = TargetRate * FrameSizeMs / 1000; // 320 samples at 16kHz

        private AudioClip _micClip;
        private int _lastSamplePos;
        private readonly float[] _sampleBuffer = new float[FrameSamples48k];
        private readonly byte[] _pcmFrameBuffer = new byte[FrameSamplesTarget];
        private readonly string _deviceName;
        private bool _recording;

        public event Action<byte[], int> OnPcmFrameReady;

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

                // Very low threshold to filter silence/noise but let voice through
                if (rms < 0.002f) continue;

                // Downsample 48kHz -> 16kHz (take every 3rd sample)
                // Convert float[-1,1] to 8-bit unsigned PCM (0-255, 128=silence)
                // 320 samples * 1 byte = 320 bytes per frame — well under MTU
                for (int i = 0; i < FrameSamplesTarget; i++)
                {
                    float s = Mathf.Clamp(_sampleBuffer[i * DownsampleFactor], -1f, 1f);
                    _pcmFrameBuffer[i] = (byte)((s * 0.5f + 0.5f) * 255f);
                }

                OnPcmFrameReady?.Invoke(_pcmFrameBuffer, _pcmFrameBuffer.Length);
            }
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}
