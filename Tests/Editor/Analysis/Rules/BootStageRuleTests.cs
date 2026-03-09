using DrWario.Editor.Analysis.Rules;
using DrWario.Runtime;
using NUnit.Framework;
using System.Linq;

namespace DrWario.Tests
{
    [TestFixture]
    public class BootStageRuleTests
    {
        private BootStageRule _rule;

        [SetUp]
        public void SetUp() => _rule = new BootStageRule();

        [Test]
        public void Analyze_NoStages_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, cpuMs: 10f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_FastStages_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("Init", 500)
                .AddBootStage("Load", 1000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_SlowStage_Warning()
        {
            // 3000ms > 2000ms threshold, < 4000ms (2x threshold)
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("Networking", 3000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("SLOW_BOOT", findings[0].RuleId);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
            Assert.AreEqual("Boot", findings[0].Category);
            Assert.AreEqual("Runtime/BootTimingHook.cs", findings[0].ScriptPath);
        }

        [Test]
        public void Analyze_VerySlowStage_Critical()
        {
            // 5000ms > 4000ms (2x threshold)
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("Networking", 5000)
                .Build();

            var slowBoot = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "SLOW_BOOT");
            Assert.AreEqual(Severity.Critical, slowBoot.Severity);
        }

        [Test]
        public void Analyze_FailedStage_CriticalBootFailure()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("Patching", 500, success: false)
                .Build();

            var findings = _rule.Analyze(session);
            var failure = findings.FirstOrDefault(f => f.RuleId == "BOOT_FAILURE");
            Assert.IsNotNull(failure.Title);
            Assert.AreEqual(Severity.Critical, failure.Severity);
            Assert.That(failure.Title, Does.Contain("Patching"));
        }

        [Test]
        public void Analyze_TotalBootOverWarningThreshold_Warning()
        {
            // Total = 3000 + 3000 + 3000 = 9000ms > 8000ms, < 16000ms
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("Init", 3000)
                .AddBootStage("Load", 3000)
                .AddBootStage("Network", 3000)
                .Build();

            var total = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "TOTAL_BOOT_TIME");
            Assert.IsNotNull(total.Title);
            Assert.AreEqual(Severity.Warning, total.Severity);
            Assert.AreEqual(9000, total.Metric);
        }

        [Test]
        public void Analyze_TotalBootOverCriticalThreshold_Critical()
        {
            // Total = 6000 + 6000 + 6000 = 18000ms > 16000ms
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("Init", 6000)
                .AddBootStage("Load", 6000)
                .AddBootStage("Network", 6000)
                .Build();

            var total = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "TOTAL_BOOT_TIME");
            Assert.AreEqual(Severity.Critical, total.Severity);
        }

        [Test]
        public void Analyze_SlowAndFailedStage_ProducesBothFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100)
                .AddBootStage("SlowOne", 3000) // slow + successful
                .AddBootStage("FailOne", 100, success: false) // fast but failed
                .Build();

            var findings = _rule.Analyze(session);
            Assert.IsTrue(findings.Any(f => f.RuleId == "SLOW_BOOT"));
            Assert.IsTrue(findings.Any(f => f.RuleId == "BOOT_FAILURE"));
        }
    }
}
