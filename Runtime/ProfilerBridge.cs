#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Profiling;

namespace DrWario.Runtime
{
    /// <summary>
    /// Reads Unity Profiler counters via ProfilerRecorder.
    /// Zero-allocation per-frame reads. Must be Disposed when done.
    /// Falls back gracefully if counters are unavailable.
    /// </summary>
    public class ProfilerBridge : IDisposable
    {
        // Timing
        private ProfilerRecorder _mainThread;
        private ProfilerRecorder _renderThread;

        // Rendering
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPassCalls;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _vertices;

        // Memory
        private ProfilerRecorder _gcAllocCount;
        private ProfilerRecorder _gcAllocBytes;
        private ProfilerRecorder _totalUsedMemory;
        private ProfilerRecorder _textureMemory;
        private ProfilerRecorder _meshMemory;

        // Extended subsystem counters
        private ProfilerRecorder _physicsActiveBodies;
        private ProfilerRecorder _physicsKinematicBodies;
        private ProfilerRecorder _physicsContacts;
        private ProfilerRecorder _audioVoiceCount;
        private ProfilerRecorder _audioDSPLoad;
        private ProfilerRecorder _animatorCount;
        private ProfilerRecorder _uiCanvasRebuilds;
        private ProfilerRecorder _uiLayoutRebuilds;

        // Latest sampled values
        public float MainThreadMs { get; private set; }
        public float RenderThreadMs { get; private set; }
        public int DrawCalls { get; private set; }
        public int Batches { get; private set; }
        public int SetPassCalls { get; private set; }
        public int Triangles { get; private set; }
        public int Vertices { get; private set; }
        public int GcAllocCount { get; private set; }
        public long GcAllocBytes { get; private set; }
        public long TotalUsedMemory { get; private set; }
        public long TextureMemory { get; private set; }
        public long MeshMemory { get; private set; }

        // Extended subsystem counters
        public int PhysicsActiveBodies { get; private set; }
        public int PhysicsKinematicBodies { get; private set; }
        public int PhysicsContacts { get; private set; }
        public int AudioVoiceCount { get; private set; }
        public float AudioDSPLoad { get; private set; }
        public int AnimatorCount { get; private set; }
        public int UICanvasRebuilds { get; private set; }
        public int UILayoutRebuilds { get; private set; }

        /// <summary>True if ProfilerRecorder is available and at least one counter is recording.</summary>
        public bool IsActive { get; private set; }

        public void Start()
        {
            try
            {
                _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                _renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);

                _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
                _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
                _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
                _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
                _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 1);

                _gcAllocCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count", 1);
                _gcAllocBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                _totalUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
                _textureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory", 1);
                _meshMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory", 1);

                // Extended subsystem counters — these may not be available on all platforms
                TryStartRecorder(ref _physicsActiveBodies, ProfilerCategory.Physics, "Physics.ActiveDynamicBodies");
                TryStartRecorder(ref _physicsKinematicBodies, ProfilerCategory.Physics, "Physics.ActiveKinematicBodies");
                TryStartRecorder(ref _physicsContacts, ProfilerCategory.Physics, "Physics.Contacts");
                TryStartRecorder(ref _audioVoiceCount, ProfilerCategory.Audio, "AudioManager.ActiveVoiceCount");
                TryStartRecorder(ref _audioDSPLoad, ProfilerCategory.Audio, "Audio.DSPLoad");
                TryStartRecorder(ref _animatorCount, ProfilerCategory.Animation, "Animators.Count");
                TryStartRecorder(ref _uiCanvasRebuilds, ProfilerCategory.Gui, "UI.CanvasRebuildCount");
                TryStartRecorder(ref _uiLayoutRebuilds, ProfilerCategory.Gui, "UI.LayoutRebuildCount");

                IsActive = _mainThread.Valid;
                if (IsActive)
                    UnityEngine.Debug.Log("[DrWario] ProfilerBridge active — reading ProfilerRecorder counters.");
                else
                    UnityEngine.Debug.LogWarning("[DrWario] ProfilerBridge: Main Thread counter not available. Falling back to legacy sampling.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[DrWario] ProfilerBridge failed to start: {e.Message}. Using legacy sampling.");
                IsActive = false;
            }
        }

        /// <summary>
        /// Read current values from all recorders. Call once per frame in Update().
        /// </summary>
        public void Sample()
        {
            if (!IsActive) return;

            MainThreadMs = ReadNsAsMs(_mainThread);
            RenderThreadMs = ReadNsAsMs(_renderThread);

            DrawCalls = ReadInt(_drawCalls);
            Batches = ReadInt(_batches);
            SetPassCalls = ReadInt(_setPassCalls);
            Triangles = ReadInt(_triangles);
            Vertices = ReadInt(_vertices);

            GcAllocCount = ReadInt(_gcAllocCount);
            GcAllocBytes = ReadLong(_gcAllocBytes);
            TotalUsedMemory = ReadLong(_totalUsedMemory);
            TextureMemory = ReadLong(_textureMemory);
            MeshMemory = ReadLong(_meshMemory);

            // Extended counters (0 if unavailable)
            PhysicsActiveBodies = ReadInt(_physicsActiveBodies);
            PhysicsKinematicBodies = ReadInt(_physicsKinematicBodies);
            PhysicsContacts = ReadInt(_physicsContacts);
            AudioVoiceCount = ReadInt(_audioVoiceCount);
            AudioDSPLoad = _audioDSPLoad.Valid ? _audioDSPLoad.CurrentValue / 100f : 0f; // percentage
            AnimatorCount = ReadInt(_animatorCount);
            UICanvasRebuilds = ReadInt(_uiCanvasRebuilds);
            UILayoutRebuilds = ReadInt(_uiLayoutRebuilds);
        }

        public void Dispose()
        {
            _mainThread.Dispose();
            _renderThread.Dispose();
            _drawCalls.Dispose();
            _batches.Dispose();
            _setPassCalls.Dispose();
            _triangles.Dispose();
            _vertices.Dispose();
            _gcAllocCount.Dispose();
            _gcAllocBytes.Dispose();
            _totalUsedMemory.Dispose();
            _textureMemory.Dispose();
            _meshMemory.Dispose();

            _physicsActiveBodies.Dispose();
            _physicsKinematicBodies.Dispose();
            _physicsContacts.Dispose();
            _audioVoiceCount.Dispose();
            _audioDSPLoad.Dispose();
            _animatorCount.Dispose();
            _uiCanvasRebuilds.Dispose();
            _uiLayoutRebuilds.Dispose();

            IsActive = false;
        }

        private static void TryStartRecorder(ref ProfilerRecorder recorder, ProfilerCategory category, string name)
        {
            try
            {
                recorder = ProfilerRecorder.StartNew(category, name, 1);
            }
            catch
            {
                // Counter not available on this platform — leave as default (invalid)
            }
        }

        private static float ReadNsAsMs(ProfilerRecorder r)
        {
            return r.Valid && r.CurrentValue > 0 ? r.CurrentValue / 1_000_000f : 0f;
        }

        private static int ReadInt(ProfilerRecorder r)
        {
            return r.Valid ? (int)r.CurrentValue : 0;
        }

        private static long ReadLong(ProfilerRecorder r)
        {
            return r.Valid ? r.CurrentValue : 0;
        }
    }
}
#endif
