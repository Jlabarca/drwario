# LLM Response Contract: Extended Finding Schema

**Feature**: 001-rich-diagnostics-ui | **Version**: 2.0

## Response Format

The LLM must return a JSON array of findings. Each finding object follows this schema:

```json
[
  {
    "ruleId": "AI_GC_SPIKE",
    "category": "Memory",
    "severity": "Warning",
    "title": "Frequent GC Allocations in PlayerController.Update",
    "description": "PlayerController.cs allocates ~2KB per frame via string concatenation in the Update loop.",
    "recommendation": "Use StringBuilder or cached strings. Move allocations out of the hot path.",
    "metric": 2048.0,
    "threshold": 1024.0,
    "scriptPath": "Assets/Scripts/PlayerController.cs",
    "scriptLine": 47,
    "assetPath": null
  },
  {
    "ruleId": "AI_TEXTURE_SIZE",
    "category": "Memory",
    "severity": "Info",
    "title": "Large uncompressed texture detected",
    "description": "Background.png is 4096x4096 uncompressed, consuming 64MB of GPU memory.",
    "recommendation": "Compress with ASTC/ETC2 or reduce resolution to 2048x2048.",
    "metric": 67108864,
    "threshold": 16777216,
    "scriptPath": null,
    "scriptLine": 0,
    "assetPath": "Assets/Textures/Background.png"
  }
]
```

## Field Specification

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| ruleId | string | required | Unique identifier prefixed with "AI_" |
| category | string | required | One of: "CPU", "Memory", "Boot", "Assets", "Network", "Rendering", "Physics", "UI", "Audio" |
| severity | string | required | One of: "Critical", "Warning", "Info" |
| title | string | required | Short finding summary (under 80 chars) |
| description | string | required | Detailed explanation with metrics |
| recommendation | string | required | Actionable fix with code patterns |
| metric | float | required | The measured value |
| threshold | float | required | The reference threshold |
| scriptPath | string | optional | Relative Unity asset path to script (e.g., "Assets/Scripts/Foo.cs"). Null if unknown. |
| scriptLine | int | optional | Line number in the script. 0 if unknown. |
| assetPath | string | optional | Relative Unity asset path to asset (e.g., "Assets/Textures/Bar.png"). Null if unknown. |

## System Prompt Addition

The following instruction is appended to the system prompt to guide LLM responses:

> When you can identify a specific script file causing a performance issue, include `scriptPath` with the relative Unity asset path (starting with "Assets/") and `scriptLine` with the approximate line number. When you can identify a specific asset (texture, mesh, material, prefab), include `assetPath`. Only include references you are confident about — omit rather than guess.

## Backward Compatibility

- All three new fields are optional — responses without them parse correctly
- Existing providers (Claude, OpenAI, Ollama) will include references only when the system prompt instructs them to
- `JsonUtility.FromJson` silently ignores missing fields (defaults: null for strings, 0 for int)
