
using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MicRouteSwitch
{
    public sealed class AudioRouter : IDisposable
    {
        private readonly MMDevice _micDevice;
        private readonly MMDevice _cableADevice;
        private readonly MMDevice _cableBDevice;

        private WasapiCapture? _capture;
        private WasapiOut? _outA;
        private WasapiOut? _outB;
        private BufferedWaveProvider? _bufA;
        private BufferedWaveProvider? _bufB;

        private volatile bool _routeToB;

        public bool RouteToB
        {
            get => _routeToB;
            set => _routeToB = value;
        }

        public AudioRouter(MMDevice mic, MMDevice cableAInput, MMDevice cableBInput)
        {
            _micDevice = mic;
            _cableADevice = cableAInput;
            _cableBDevice = cableBInput;
        }

        public void Start()
        {
            Stop();

            _capture = new WasapiCapture(_micDevice);
            var wf = _capture.WaveFormat;

            _bufA = new BufferedWaveProvider(wf) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromMilliseconds(200) };
            _bufB = new BufferedWaveProvider(wf) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromMilliseconds(200) };

            _outA = new WasapiOut(_cableADevice, AudioClientShareMode.Shared, true, 20);
            _outA.Init(_bufA);
            _outA.Play();

            _outB = new WasapiOut(_cableBDevice, AudioClientShareMode.Shared, true, 20);
            _outB.Init(_bufB);
            _outB.Play();

            _capture.DataAvailable += OnData;
            _capture.StartRecording();
        }

        private void OnData(object? sender, WaveInEventArgs e)
        {
            if (_bufA == null || _bufB == null) return;
            if (_routeToB) _bufB.AddSamples(e.Buffer, 0, e.BytesRecorded);
            else _bufA.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        public void Stop()
        {
            _capture?.StopRecording();
            _capture?.Dispose(); _capture = null;

            _outA?.Stop();
            _outA?.Dispose(); _outA = null;

            _outB?.Stop();
            _outB?.Dispose(); _outB = null;

            _bufA = null;
            _bufB = null;
        }

        public void Dispose() => Stop();
    }
}
