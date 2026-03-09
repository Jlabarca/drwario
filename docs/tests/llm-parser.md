# LLM Response Parser Reference

Verified by `LLMResponseParserTests`.

## Parse(string content) Behavior

### Input Handling
| Input | Result |
|-------|--------|
| `null` or `""` | Empty list |
| No `[` or `]` in string | Empty list (logged warning) |
| Valid JSON array `[{...}]` | Parsed findings |
| Markdown-wrapped `` ```json [...] ``` `` | Fences stripped, then parsed |
| Leading text `"Here are findings: [{...}]"` | Extracts array between `[` and `]` |
| Malformed JSON between `[` and `]` | Single finding with `RuleId = "AI_PARSE_ERROR"` |

### Field Defaults
| Field | Default When Missing |
|-------|---------------------|
| `ruleId` | `"AI_UNKNOWN"` |
| `category` | `"General"` |
| `severity` | `Info` |
| `title` | `"AI Finding"` |
| `confidence` | `Medium` |
| `environmentNote` | `null` (empty string → null) |
| `scriptPath` | `null` (empty string → null) |
| `assetPath` | `null` (empty string → null) |
| `FrameIndex` | Always `-1` (parser doesn't set it) |

### Severity Mapping (case-insensitive)
| Input | Result |
|-------|--------|
| `"critical"` | `Severity.Critical` |
| `"warning"` | `Severity.Warning` |
| anything else / null | `Severity.Info` |

### Confidence Mapping (case-insensitive)
| Input | Result |
|-------|--------|
| `"high"` | `Confidence.High` |
| `"low"` | `Confidence.Low` |
| anything else / null | `Confidence.Medium` |

## ParseSingle(string jsonObject) Behavior

- Returns `null` for null, empty, or non-object input (must start with `{` and end with `}`)
- Wraps input in array internally: `[{input}]` → reuses Parse logic
- Returns `DiagnosticFinding?` — nullable struct
