using System;
using System.Collections.Generic;

namespace DrWario.Runtime
{
    public class ProfilingSession
    {
        public const int DefaultCapacity = 3600; // ~60s at 60fps

        private readonly FrameSample[] _frameBuffer;
        private int _frameWriteIndex;
        private int _frameCount;

        private readonly List<BootStageTiming> _bootStages = new();
        private readonly List<AssetLoadTiming> _assetLoads = new();
        private readonly List<NetworkEvent> _networkEvents = new();

        public SessionMetadata Metadata;
        public bool IsRecording { get; private set; }

        public int FrameCount => _frameCount;
        public int Capacity => _frameBuffer.Length;

        public ProfilingSession(int capacity = DefaultCapacity)
        {
            _frameBuffer = new FrameSample[capacity];
        }

        public void Start()
        {
            _frameWriteIndex = 0;
            _frameCount = 0;
            _bootStages.Clear();
            _assetLoads.Clear();
            _networkEvents.Clear();

            Metadata = new SessionMetadata
            {
                StartTime = DateTime.UtcNow,
                UnityVersion = UnityEngine.Application.unityVersion,
                Platform = UnityEngine.Application.platform.ToString(),
                TargetFrameRate = UnityEngine.Application.targetFrameRate,
                ScreenWidth = UnityEngine.Screen.width,
                ScreenHeight = UnityEngine.Screen.height
            };

            IsRecording = true;
        }

        public void Stop()
        {
            IsRecording = false;
            Metadata.EndTime = DateTime.UtcNow;
        }

        public void RecordFrame(FrameSample sample)
        {
            if (!IsRecording) return;

            _frameBuffer[_frameWriteIndex] = sample;
            _frameWriteIndex = (_frameWriteIndex + 1) % _frameBuffer.Length;
            if (_frameCount < _frameBuffer.Length)
                _frameCount++;
        }

        public void RecordBootStage(string stageName, long durationMs, bool success)
        {
            _bootStages.Add(new BootStageTiming
            {
                StageName = stageName,
                DurationMs = durationMs,
                Success = success
            });
        }

        public void RecordAssetLoad(string assetKey, long durationMs, long sizeBytes = 0)
        {
            _assetLoads.Add(new AssetLoadTiming
            {
                AssetKey = assetKey,
                DurationMs = durationMs,
                SizeBytes = sizeBytes
            });
        }

        /// <summary>
        /// Returns frames in chronological order from the ring buffer.
        /// </summary>
        public FrameSample[] GetFrames()
        {
            var result = new FrameSample[_frameCount];
            if (_frameCount < _frameBuffer.Length)
            {
                Array.Copy(_frameBuffer, 0, result, 0, _frameCount);
            }
            else
            {
                int oldest = _frameWriteIndex; // oldest entry in ring
                int tailLen = _frameBuffer.Length - oldest;
                Array.Copy(_frameBuffer, oldest, result, 0, tailLen);
                Array.Copy(_frameBuffer, 0, result, tailLen, oldest);
            }
            return result;
        }

        public void RecordNetworkEvent(NetworkEventType type, int bytes, float latencyMs = 0)
        {
            _networkEvents.Add(new NetworkEvent
            {
                Timestamp = UnityEngine.Time.realtimeSinceStartup,
                Type = type,
                Bytes = bytes,
                LatencyMs = latencyMs
            });
        }

        public IReadOnlyList<BootStageTiming> BootStages => _bootStages;
        public IReadOnlyList<AssetLoadTiming> AssetLoads => _assetLoads;
        public IReadOnlyList<NetworkEvent> NetworkEvents => _networkEvents;
    }
}
