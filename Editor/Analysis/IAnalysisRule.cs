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
}
