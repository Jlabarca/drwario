using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class ProfilingSessionTests
    {
        [Test]
        public void RecordFrame_UnderCapacity_FrameCountIncreases()
        {
            var session = new TestSessionBuilder()
                .WithCapacity(100)
                .AddFrames(10)
                .Build();

            Assert.AreEqual(10, session.FrameCount);
        }

        [Test]
        public void RecordFrame_AtCapacity_FrameCountCapped()
        {
            var session = new TestSessionBuilder()
                .WithCapacity(100)
                .AddFrames(200)
                .Build();

            Assert.AreEqual(100, session.FrameCount);
        }

        [Test]
        public void GetFrames_UnderCapacity_ReturnsChronologicalOrder()
        {
            var session = new ProfilingSession(100);
            session.Start();
            session.Metadata = new SessionMetadata { TargetFrameRate = 60 };

            for (int i = 0; i < 5; i++)
                session.RecordFrame(new FrameSample { Timestamp = i * 0.1f, CpuFrameTimeMs = i });

            var frames = session.GetFrames();
            Assert.AreEqual(5, frames.Length);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(i, frames[i].CpuFrameTimeMs, $"Frame {i} out of order");
        }

        [Test]
        public void GetFrames_WrapAround_ReturnsChronologicalOrder()
        {
            var session = new ProfilingSession(100);
            session.Start();
            session.Metadata = new SessionMetadata { TargetFrameRate = 60 };

            // Record 150 frames into capacity-100 buffer
            for (int i = 0; i < 150; i++)
                session.RecordFrame(new FrameSample { CpuFrameTimeMs = i, FrameNumber = i });

            var frames = session.GetFrames();
            Assert.AreEqual(100, frames.Length);

            // Should contain frames 50-149 in order
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(50 + i, frames[i].CpuFrameTimeMs,
                    $"Frame at index {i} should be frame {50 + i}");
        }

        [Test]
        public void GetFrames_Empty_ReturnsEmptyArray()
        {
            var session = new ProfilingSession(100);
            session.Start();

            var frames = session.GetFrames();
            Assert.AreEqual(0, frames.Length);
        }

        [Test]
        public void RecordFrame_WhenNotRecording_IsIgnored()
        {
            var session = new ProfilingSession(100);
            // Do NOT call Start()
            session.RecordFrame(new FrameSample { CpuFrameTimeMs = 10f });

            Assert.AreEqual(0, session.FrameCount);
        }

        [Test]
        public void Start_ResetsState()
        {
            var session = new ProfilingSession(100);
            session.Start();
            for (int i = 0; i < 10; i++)
                session.RecordFrame(new FrameSample());
            Assert.AreEqual(10, session.FrameCount);

            session.Start();
            Assert.AreEqual(0, session.FrameCount);
        }

        [Test]
        public void SetActiveScripts_StoresAndReturns()
        {
            var scripts = new List<ActiveScriptEntry>
            {
                new ActiveScriptEntry { TypeName = "PlayerController", InstanceCount = 1 },
                new ActiveScriptEntry { TypeName = "EnemyAI", InstanceCount = 5 }
            };

            var session = new TestSessionBuilder()
                .WithActiveScripts(scripts)
                .Build();

            Assert.AreEqual(2, session.ActiveScripts.Count);
            Assert.AreEqual("PlayerController", session.ActiveScripts[0].TypeName);
            Assert.AreEqual(5, session.ActiveScripts[1].InstanceCount);
        }

        [Test]
        public void RecordConsoleLog_UnderLimit_StoresAll()
        {
            var session = new ProfilingSession();
            session.Start();

            for (int i = 0; i < 10; i++)
                session.RecordConsoleLog(new ConsoleLogEntry { Message = $"Log {i}", LogType = "Error" });

            Assert.AreEqual(10, session.ConsoleLogs.Count);
        }

        [Test]
        public void RecordConsoleLog_OverLimit_CapsAt50()
        {
            var session = new ProfilingSession();
            session.Start();

            for (int i = 0; i < 60; i++)
                session.RecordConsoleLog(new ConsoleLogEntry { Message = $"Log {i}", LogType = "Warning" });

            Assert.AreEqual(50, session.ConsoleLogs.Count);
        }

        [Test]
        public void SetProfilerMarkers_ClearsAndReplaces()
        {
            var session = new ProfilingSession();
            session.Start();

            session.SetProfilerMarkers(new List<ProfilerMarkerSample>
            {
                new ProfilerMarkerSample { MarkerName = "Old" }
            });
            Assert.AreEqual(1, session.ProfilerMarkers.Count);

            session.SetProfilerMarkers(new List<ProfilerMarkerSample>
            {
                new ProfilerMarkerSample { MarkerName = "New1" },
                new ProfilerMarkerSample { MarkerName = "New2" }
            });
            Assert.AreEqual(2, session.ProfilerMarkers.Count);
            Assert.AreEqual("New1", session.ProfilerMarkers[0].MarkerName);
        }

        [Test]
        public void RecordSceneSnapshot_CapsAt100()
        {
            var session = new ProfilingSession();
            session.Start();

            for (int i = 0; i < 110; i++)
                session.RecordSceneSnapshot(new SceneSnapshotDiff { FrameIndex = i });

            Assert.AreEqual(100, session.SceneSnapshots.Count);
        }

        [Test]
        public void MarkCaptureFrame_TracksFrameNumbers()
        {
            var session = new ProfilingSession();
            session.Start();

            session.MarkCaptureFrame(10);
            session.MarkCaptureFrame(20);
            session.MarkCaptureFrame(30);

            Assert.IsTrue(session.DrWarioCaptureFrames.Contains(10));
            Assert.IsTrue(session.DrWarioCaptureFrames.Contains(20));
            Assert.IsTrue(session.DrWarioCaptureFrames.Contains(30));
            Assert.IsFalse(session.DrWarioCaptureFrames.Contains(15));
        }

        [Test]
        public void Stop_SetsIsRecordingFalse()
        {
            var session = new ProfilingSession();
            session.Start();
            Assert.IsTrue(session.IsRecording);

            session.Stop();
            Assert.IsFalse(session.IsRecording);
        }

        [Test]
        public void Capacity_ReturnsConfiguredValue()
        {
            var session = new ProfilingSession(500);
            Assert.AreEqual(500, session.Capacity);
        }

        [Test]
        public void RecordBootStage_StoresStages()
        {
            var session = new TestSessionBuilder()
                .AddBootStage("Init", 100)
                .AddBootStage("Load", 500, false)
                .Build();

            Assert.AreEqual(2, session.BootStages.Count);
            Assert.AreEqual("Init", session.BootStages[0].StageName);
            Assert.AreEqual(100, session.BootStages[0].DurationMs);
            Assert.IsTrue(session.BootStages[0].Success);
            Assert.IsFalse(session.BootStages[1].Success);
        }

        [Test]
        public void RecordAssetLoad_StoresLoads()
        {
            var session = new TestSessionBuilder()
                .AddAssetLoad("texture.png", 200, 1024)
                .Build();

            Assert.AreEqual(1, session.AssetLoads.Count);
            Assert.AreEqual("texture.png", session.AssetLoads[0].AssetKey);
            Assert.AreEqual(200, session.AssetLoads[0].DurationMs);
            Assert.AreEqual(1024, session.AssetLoads[0].SizeBytes);
        }
    }
}
