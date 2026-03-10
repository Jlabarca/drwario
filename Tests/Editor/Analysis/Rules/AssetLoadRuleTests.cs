using DrWario.Editor.Analysis.Rules;
using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class AssetLoadRuleTests
    {
        private AssetLoadRule _rule;

        [SetUp]
        public void SetUp() => _rule = new AssetLoadRule();

        [Test]
        public void Analyze_NoLoads_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_AllFastLoads_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddAssetLoad("texture.png", 200)
                .AddAssetLoad("mesh.fbx", 400)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_OneSlowLoad_InfoSeverity()
        {
            // 1 load > 500ms, none > 2000ms, count <= 3
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddAssetLoad("small.png", 100)
                .AddAssetLoad("big_texture.png", 600)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Info, findings[0].Severity);
            Assert.AreEqual("SLOW_ASSET_LOAD", findings[0].RuleId);
            Assert.AreEqual("Assets", findings[0].Category);
            Assert.AreEqual("big_texture.png", findings[0].AssetPath);
        }

        [Test]
        public void Analyze_ManySlowLoads_WarningSeverity()
        {
            // 4 slow loads > 500ms, none > 2000ms
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddAssetLoad("a.png", 600)
                .AddAssetLoad("b.png", 700)
                .AddAssetLoad("c.png", 800)
                .AddAssetLoad("d.png", 900)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
        }

        [Test]
        public void Analyze_CriticalLoad_CriticalSeverity()
        {
            // 1 load > 2000ms
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddAssetLoad("huge_world.fbx", 2500)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Analyze_SlowestAssetTracked()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddAssetLoad("slow.png", 800)
                .AddAssetLoad("slower.fbx", 1500)
                .AddAssetLoad("fast.wav", 100)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("slower.fbx", findings[0].AssetPath);
            Assert.AreEqual(1500, findings[0].Metric);
        }
    }
}
