using System.Collections.Generic;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.LLM;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class LLMResponseParserTests
    {
        [Test]
        public void Parse_EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual(0, LLMResponseParser.Parse(null).Count);
            Assert.AreEqual(0, LLMResponseParser.Parse("").Count);
        }

        [Test]
        public void Parse_ValidJsonArray_ReturnsFindings()
        {
            string json = @"[{
                ""ruleId"": ""AI_GC_PATTERN"",
                ""category"": ""Memory"",
                ""severity"": ""Warning"",
                ""title"": ""String concat in Update"",
                ""description"": ""desc"",
                ""recommendation"": ""rec"",
                ""metric"": 2048,
                ""threshold"": 1024
            }]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("AI_GC_PATTERN", findings[0].RuleId);
            Assert.AreEqual("Memory", findings[0].Category);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
            Assert.AreEqual("String concat in Update", findings[0].Title);
            Assert.AreEqual(2048f, findings[0].Metric, 0.01f);
            Assert.AreEqual(1024f, findings[0].Threshold, 0.01f);
        }

        [Test]
        public void Parse_MarkdownWrapped_StripsFences()
        {
            string json = "```json\n[{\"ruleId\":\"AI_1\",\"category\":\"CPU\",\"severity\":\"Info\",\"title\":\"T\",\"description\":\"D\",\"recommendation\":\"R\",\"metric\":0,\"threshold\":0}]\n```";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("AI_1", findings[0].RuleId);
        }

        [Test]
        public void Parse_LeadingText_FindsArrayBrackets()
        {
            string json = "Here are the findings: [{\"ruleId\":\"AI_2\",\"category\":\"CPU\",\"severity\":\"Critical\",\"title\":\"T\",\"description\":\"D\",\"recommendation\":\"R\",\"metric\":0,\"threshold\":0}]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("AI_2", findings[0].RuleId);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Parse_MissingOptionalFields_UsesDefaults()
        {
            string json = @"[{
                ""ruleId"": ""AI_MIN"",
                ""category"": ""CPU"",
                ""severity"": ""Info"",
                ""title"": ""Minimal"",
                ""description"": ""desc"",
                ""recommendation"": ""rec"",
                ""metric"": 5,
                ""threshold"": 10
            }]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(1, findings.Count);
            Assert.IsNull(findings[0].ScriptPath);
            Assert.IsNull(findings[0].AssetPath);
            Assert.AreEqual(Confidence.Medium, findings[0].Confidence); // default when not specified
        }

        [Test]
        public void Parse_SeverityCaseInsensitive()
        {
            string json = @"[{""ruleId"":""A"",""category"":""C"",""severity"":""critical"",""title"":""T"",""description"":""D"",""recommendation"":""R"",""metric"":0,""threshold"":0}]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Parse_ConfidenceMapping()
        {
            string MakeJson(string conf) =>
                $@"[{{""ruleId"":""A"",""category"":""C"",""severity"":""Info"",""title"":""T"",""description"":""D"",""recommendation"":""R"",""metric"":0,""threshold"":0,""confidence"":""{conf}""}}]";

            Assert.AreEqual(Confidence.High, LLMResponseParser.Parse(MakeJson("high"))[0].Confidence);
            Assert.AreEqual(Confidence.Low, LLMResponseParser.Parse(MakeJson("low"))[0].Confidence);
            Assert.AreEqual(Confidence.Medium, LLMResponseParser.Parse(MakeJson("medium"))[0].Confidence);
            Assert.AreEqual(Confidence.Medium, LLMResponseParser.Parse(MakeJson("unknown"))[0].Confidence);
        }

        [Test]
        public void Parse_MalformedJson_ReturnsFallbackFinding()
        {
            // Has [ and ] but content between them is not valid JSON
            string json = "[{ this is not valid json }]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("AI_PARSE_ERROR", findings[0].RuleId);
            Assert.AreEqual(Severity.Info, findings[0].Severity);
        }

        [Test]
        public void Parse_NoArrayBrackets_ReturnsEmpty()
        {
            string json = "This is just plain text with no brackets";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void ParseSingle_ValidObject_ReturnsFinding()
        {
            string json = @"{""ruleId"":""AI_SINGLE"",""category"":""Memory"",""severity"":""Warning"",""title"":""Single"",""description"":""D"",""recommendation"":""R"",""metric"":100,""threshold"":50,""scriptPath"":""Assets/Scripts/Foo.cs"",""scriptLine"":42}";

            var finding = LLMResponseParser.ParseSingle(json);

            Assert.IsNotNull(finding);
            Assert.AreEqual("AI_SINGLE", finding.Value.RuleId);
            Assert.AreEqual("Assets/Scripts/Foo.cs", finding.Value.ScriptPath);
            Assert.AreEqual(42, finding.Value.ScriptLine);
        }

        [Test]
        public void ParseSingle_InvalidObject_ReturnsNull()
        {
            Assert.IsNull(LLMResponseParser.ParseSingle(null));
            Assert.IsNull(LLMResponseParser.ParseSingle(""));
            Assert.IsNull(LLMResponseParser.ParseSingle("not json"));
            Assert.IsNull(LLMResponseParser.ParseSingle("[array]"));
        }

        [Test]
        public void Parse_MultipleFindings_AllParsed()
        {
            string json = @"[
                {""ruleId"":""A1"",""category"":""CPU"",""severity"":""Info"",""title"":""T1"",""description"":""D"",""recommendation"":""R"",""metric"":0,""threshold"":0},
                {""ruleId"":""A2"",""category"":""Memory"",""severity"":""Warning"",""title"":""T2"",""description"":""D"",""recommendation"":""R"",""metric"":0,""threshold"":0},
                {""ruleId"":""A3"",""category"":""Rendering"",""severity"":""Critical"",""title"":""T3"",""description"":""D"",""recommendation"":""R"",""metric"":0,""threshold"":0}
            ]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual(3, findings.Count);
            Assert.AreEqual("A1", findings[0].RuleId);
            Assert.AreEqual("A2", findings[1].RuleId);
            Assert.AreEqual("A3", findings[2].RuleId);
        }

        [Test]
        public void Parse_WithEnvironmentNote_Preserved()
        {
            string json = @"[{""ruleId"":""A"",""category"":""C"",""severity"":""Info"",""title"":""T"",""description"":""D"",""recommendation"":""R"",""metric"":0,""threshold"":0,""environmentNote"":""Editor overhead detected""}]";

            var findings = LLMResponseParser.Parse(json);

            Assert.AreEqual("Editor overhead detected", findings[0].EnvironmentNote);
        }
    }
}
