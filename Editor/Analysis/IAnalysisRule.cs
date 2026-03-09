using System.Collections.Generic;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis
{
    /// <summary>
    /// Extensible analysis rule interface.
    /// Implement this to add custom diagnostics — including future AI-powered rules.
    /// </summary>
    public interface IAnalysisRule
    {
        string Category { get; }
        string RuleId { get; }
        List<DiagnosticFinding> Analyze(ProfilingSession session);
    }

    /// <summary>
    /// Optional interface for rules that expose a configurable threshold.
    /// Rules implementing this appear with an adjustable slider in the Rule Management UI.
    /// </summary>
    public interface IConfigurableRule : IAnalysisRule
    {
        string ThresholdLabel { get; }
        float DefaultThreshold { get; }
        float MinThreshold { get; }
        float MaxThreshold { get; }
    }
}
