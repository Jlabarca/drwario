# Report Comparison Reference

Verified by `ReportComparisonTests`.

## Finding Matching

Findings are matched between reports using the key: `"{RuleId}|{Category}"`.

When duplicate keys exist in a single report, the **first one wins** — only one match per key.

## FindingDiffStatus

| Status | Meaning |
|--------|---------|
| `Fixed` | Finding exists in Report A but not in Report B |
| `New` | Finding exists in Report B but not in Report A |
| `Persists` | Finding exists in both reports (same RuleId + Category) |

### Persists Details
- `SeverityChange` = `(int)findingB.Severity - (int)findingA.Severity`
  - Positive = severity worsened (e.g., Warning→Critical = +1)
  - Negative = severity improved
  - Zero = same severity
- `MetricDelta` = `findingB.Metric - findingA.Metric`
  - Positive = metric worsened
  - Negative = metric improved

## MetricDelta Fields

All computed as `reportB.value - reportA.value`:

| Field | Positive Means |
|-------|---------------|
| `AvgCpuTimeDelta` | CPU regression |
| `P95CpuTimeDelta` | Tail latency regression |
| `GcRateDelta` | More GC allocations |
| `MemorySlopeDelta` | Faster heap growth |
| `DrawCallsDelta` | More draw calls |
| `HealthScoreDelta` | Better overall health |

Note: `HealthScoreDelta` and `OverallGradeDelta` are both `reportB.HealthScore - reportA.HealthScore`. Positive = improvement.

## Category Deltas

- All categories from both reports are included
- Missing category in A → `GradeA = '-'`
- Missing category in B → `GradeB = '-'`
- `ScoreDelta` uses `GradeToScore()`: A=95, B=85, C=75, D=65, F=50, '-'=0
