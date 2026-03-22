using System;
using Concentus.Enums;
using Concentus.Structs;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class MicCapture : IDisposable
    {
        private const int SampleRate = 48000;
        private const int FrameSizeMs = 20;
        private const int FrameSamples = SampleRate * FrameSizeMs / 1000;
        private const int Channels = 1;
        private const int BitRate = 24000;

        private AudioClip _micClip;
        private int _lastSamplePos;
        private readonly float[] _sampleBuffer = new float[FrameSamples];
        private readonly short[] _pcmBuffer = new short[FrameSamples];
        private readonly byte[] _opusBuffer = new byte[4000];
        private readonly OpusEncoder _encoder;
        private readonly string _deviceName;
        private bool _recording;

        public event Action<byte[], int> OnFrameEncoded;

        public MicCapture(string deviceName)
        {
            _deviceName = string.IsNullOrEmpty(deviceName) ? null : deviceName;
            _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.Bitrate = BitRate;
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

                for (int i = 0; i < FrameSamples; i++)
                    _pcmBuffer[i] = (short)(Mathf.Clamp(_sampleBuffer[i], -1f, 1f) * 32767f);

                try
                {
                    int encodedLength = _encoder.Encode(_pcmBuffer, 0, FrameSamples, _opusBuffer, 0, _opusBuffer.Length);
                    if (encodedLength > 0)
                    {
                        byte[] encoded = new byte[encodedLength];
                        Buffer.BlockCopy(_opusBuffer, 0, encoded, 0, encodedLength);
                        OnFrameEncoded?.Invoke(encoded, encodedLength);
                    }
                }
                catch { }
            }
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}
