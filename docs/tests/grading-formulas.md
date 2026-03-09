# Grading Formula Reference

Quick reference for the scoring/grading math verified by `DiagnosticReportGradingTests`.

## Health Score Calculation

```
score = 100

for each finding:
    base_penalty = Critical ? 15 : Warning ? 5 : 1

    confidence_multiplier = finding.Confidence switch:
        High   → 1.0
        Medium → 0.6
        Low    → 0.25

    score -= base_penalty × confidence_multiplier

score = clamp(score, 0, 100)
```

## Grade Mapping

| Score Range | Grade |
|------------|-------|
| ≥ 90       | A     |
| ≥ 80       | B     |
| ≥ 70       | C     |
| ≥ 60       | D     |
| < 60       | F     |

Boundary is inclusive: exactly 90 = A, exactly 80 = B, etc.

## Category Grades

Same formula applied independently per category. Each category starts at 100 and only deducts penalties from findings in that category.

## Verified Examples

| Findings | Score | Grade |
|----------|-------|-------|
| None | 100 | A |
| 1× Critical (High) | 85 | B |
| 1× Warning (High) | 95 | A |
| 1× Info (High) | 99 | A |
| 1× Critical (Low) | 96.25 | A |
| 1× Critical (Medium) | 91 | A |
| 5× Critical (Low) | 81.25 | B |
| 10× Critical (High) | 0 | F |
| 2× Warning (High) | 90 | A |
| 1× Critical + 1× Warning | 80 | B |
| 1× Critical + 1× Warning + 1× Info | 79 | C |

## Report Comparison Grade-to-Score

Used by `ReportComparison` for `GradeDelta.ScoreDelta` calculations:

| Grade | Score |
|-------|-------|
| A | 95 |
| B | 85 |
| C | 75 |
| D | 65 |
| F | 50 |
| - (missing) | 0 |
