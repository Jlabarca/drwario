using System.Collections.Generic;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.LLM;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class LLMPromptBuilderMcpTests
    {
        [SetUp]
        public void SetUp()
        {
            LLMPromptBuilder.AdditionalContext = null;
        }

        // -- BuildMcpSuspectCheckPrompt --

        [Test]
        public void McpSuspectCheck_ContainsActiveScripts()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50, cpuMs: 20f)
                .WithActiveScripts(new List<ActiveScriptEntry>
                {
                    new ActiveScriptEntry
                    {
                        TypeName = "EnemyAI",
                        InstanceCount = 50,
                        SampleGameObjectNames = new[] { "Enemy1", "Enemy2" }
                    }
                })
                .Build();

            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Warning, category: "CPU")
            );
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildMcpSuspectCheckPrompt(session, report);

            Assert.That(prompt, Does.Contain("ACTIVE SCRIPTS"));
            Assert.That(prompt, Does.Contain("EnemyAI x50"));
            Assert.That(prompt, Does.Contain("Enemy1"));
        }

        [Test]
        public void McpSuspectCheck_ContainsFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50, cpuMs: 30f)
                .Build();

            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Critical, category: "CPU", ruleId: "FRAME_DROP")
            );
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildMcpSuspectCheckPrompt(session, report);

            Assert.That(prompt, Does.Contain("DRWARIO FINDINGS"));
            Assert.That(prompt, Does.Contain("[Critical]"));
        }

        [Test]
        public void McpSuspectCheck_ContainsProfilingSummary()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, cpuMs: 20f, gcAllocBytes: 5000)
                .Build();

            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildMcpSuspectCheckPrompt(session, report);

            Assert.That(prompt, Does.Contain("PROFILING SUMMARY"));
            Assert.That(prompt, Does.Contain("Frames: 100"));
        }

        [Test]
        public void McpSuspectCheck_WithConsoleLogs_ContainsLogs()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .WithConsoleLogs(new List<ConsoleLogEntry>
                {
                    new ConsoleLogEntry
                    {
                        LogType = "Error",
                        Message = "NullRef in Update",
                        StackTraceHint = "EnemyAI.Update()"
                    }
                })
                .Build();

            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildMcpSuspectCheckPrompt(session, report);

            Assert.That(prompt, Does.Contain("CONSOLE ERRORS"));
            Assert.That(prompt, Does.Contain("NullRef in Update"));
        }

        // -- BuildReportCorrectionPrompt --

        [Test]
        public void ReportCorrection_ContainsAuditInstructions()
        {
            var session = new TestSessionBuilder().AddFrames(50, cpuMs: 15f).Build();
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Warning, category: "CPU")
            );
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildReportCorrectionPrompt(session, report);

            Assert.That(prompt, Does.Contain("AUDIT and CORRECT"));
            Assert.That(prompt, Does.Contain("False Positive"));
            Assert.That(prompt, Does.Contain("DRWARIO REPORT TO AUDIT"));
        }

        [Test]
        public void ReportCorrection_ContainsProfilingData()
        {
            var session = new TestSessionBuilder().AddFrames(100, cpuMs: 25f).Build();
            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildReportCorrectionPrompt(session, report);

            Assert.That(prompt, Does.Contain("PROFILING DATA"));
            Assert.That(prompt, Does.Contain("\"frameSummary\""));
        }

        [Test]
        public void ReportCorrection_ContainsNumberedFindings()
        {
            var session = new TestSessionBuilder().AddFrames(50).Build();
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Critical, category: "CPU", ruleId: "A"),
                TestReportFactory.MakeFinding(Severity.Warning, category: "Memory", ruleId: "B")
            );
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildReportCorrectionPrompt(session, report);

            Assert.That(prompt, Does.Contain("#1"));
            Assert.That(prompt, Does.Contain("#2"));
        }

        // -- BuildMcpReportCorrectionPrompt --

        [Test]
        public void McpReportCorrection_ContainsTwoPhases()
        {
            var session = new TestSessionBuilder().AddFrames(50).Build();
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Warning)
            );
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildMcpReportCorrectionPrompt(session, report);

            Assert.That(prompt, Does.Contain("PHASE 1"));
            Assert.That(prompt, Does.Contain("INVESTIGATE SUSPECTS"));
            Assert.That(prompt, Does.Contain("PHASE 2"));
            Assert.That(prompt, Does.Contain("CORRECT THE REPORT"));
        }

        [Test]
        public void McpReportCorrection_ContainsReportToVerify()
        {
            var session = new TestSessionBuilder().AddFrames(50).Build();
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Warning, category: "Memory", ruleId: "GC_SPIKE")
            );
            report.ComputeGrades();

            var prompt = LLMPromptBuilder.BuildMcpReportCorrectionPrompt(session, report);

            Assert.That(prompt, Does.Contain("DRWARIO REPORT TO VERIFY & CORRECT"));
            Assert.That(prompt, Does.Contain("[Warning]"));
        }

        [Test]
        public void McpReportCorrection_NullReport_NoReportSection()
        {
            var session = new TestSessionBuilder().AddFrames(50).Build();

            var prompt = LLMPromptBuilder.BuildMcpReportCorrectionPrompt(session, null);

            Assert.That(prompt, Does.Not.Contain("DRWARIO REPORT TO VERIFY"));
            Assert.That(prompt, Does.Contain("PHASE 1"));
        }
    }
}
