using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DrWario.Editor.Analysis.LLM
{
    /// <summary>
    /// Custom DownloadHandlerScript for processing Server-Sent Events (SSE) streams
    /// from Claude and OpenAI APIs. Parses content deltas in real time and runs a
    /// progressive JSON array parser to emit DiagnosticFinding objects as they complete.
    /// </summary>
    public class SseDownloadHandler : DownloadHandlerScript
    {
        private readonly StringBuilder _buffer = new();
        private readonly StringBuilder _contentAccumulator = new();
        private readonly LLMProvider _provider;

        // Progressive JSON parsing state
        private int _braceDepth;
        private int _arrayDepth;
        private bool _inString;
        private bool _escaped;
        private readonly StringBuilder _currentObject = new();
        private bool _foundArrayStart;

        /// <summary>Called with each text chunk extracted from SSE data lines.</summary>
        public Action<string> OnChunkReceived;

        /// <summary>Called when a complete JSON object within the findings array is parsed into a DiagnosticFinding.</summary>
        public Action<DiagnosticFinding> OnFindingParsed;

        /// <summary>Called when the stream ends, with the full accumulated content.</summary>
        public Action<string> OnComplete;

        /// <summary>Called when a stream-level error is detected.</summary>
        public Action<string> OnError;

        public SseDownloadHandler(LLMProvider provider) : base()
        {
            _provider = provider;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0)
                return true;

            // Decode the incoming bytes to string
            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _buffer.Append(chunk);

            // Process complete lines (SSE uses \n as line delimiter)
            string bufferStr = _buffer.ToString();
            int lastNewline = bufferStr.LastIndexOf('\n');
            if (lastNewline < 0)
                return true; // No complete line yet

            // Split into complete lines and keep remainder
            string completeSection = bufferStr.Substring(0, lastNewline + 1);
            _buffer.Clear();
            if (lastNewline + 1 < bufferStr.Length)
                _buffer.Append(bufferStr.Substring(lastNewline + 1));

            string[] lines = completeSection.Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');

                // Check for SSE event lines
                if (line.StartsWith("event:"))
                {
                    string eventType = line.Substring(6).Trim();
                    // Claude end-of-stream
                    if (eventType == "message_stop")
                    {
                        OnComplete?.Invoke(_contentAccumulator.ToString());
                        return true;
                    }
                    // Claude error event
                    if (eventType == "error")
                    {
                        OnError?.Invoke("Stream error event received from Claude API.");
                        return true;
                    }
                    continue;
                }

                // Only process data: lines
                if (!line.StartsWith("data:"))
                    continue;

                string payload = line.Substring(5).Trim();
                if (string.IsNullOrEmpty(payload))
                    continue;

                // OpenAI end-of-stream sentinel
                if (payload == "[DONE]")
                {
                    OnComplete?.Invoke(_contentAccumulator.ToString());
                    return true;
                }

                // Extract text content from the SSE payload JSON
                string textDelta = _provider switch
                {
                    LLMProvider.Claude => ExtractClaudeDelta(payload),
                    LLMProvider.OpenAI or LLMProvider.Custom => ExtractOpenAIDelta(payload),
                    _ => null
                };

                if (textDelta == null)
                    continue;

                _contentAccumulator.Append(textDelta);
                OnChunkReceived?.Invoke(textDelta);

                // Run progressive JSON array parser on accumulated content
                FeedProgressiveParser(textDelta);
            }

            return true;
        }

        /// <summary>
        /// Extracts text from Claude SSE content_block_delta events.
        /// Format: {"type":"content_block_delta","delta":{"type":"text_delta","text":"..."}}
        /// </summary>
        private static string ExtractClaudeDelta(string json)
        {
            // Only process content_block_delta events
            int typeIdx = json.IndexOf("\"content_block_delta\"", StringComparison.Ordinal);
            if (typeIdx < 0)
                return null;

            // Find "text_delta" to confirm this is a text chunk
            int textDeltaIdx = json.IndexOf("\"text_delta\"", StringComparison.Ordinal);
            if (textDeltaIdx < 0)
                return null;

            // Find the "text" field value after text_delta
            // Look for "text": after the text_delta marker
            int textKeyIdx = json.IndexOf("\"text\"", textDeltaIdx + 12, StringComparison.Ordinal);
            if (textKeyIdx < 0)
                return null;

            return ExtractStringValue(json, textKeyIdx);
        }

        /// <summary>
        /// Extracts content from OpenAI SSE delta events.
        /// Format: {"choices":[{"delta":{"content":"..."}}]}
        /// </summary>
        private static string ExtractOpenAIDelta(string json)
        {
            // Find "delta" object
            int deltaIdx = json.IndexOf("\"delta\"", StringComparison.Ordinal);
            if (deltaIdx < 0)
                return null;

            // Find "content" within the delta
            int contentIdx = json.IndexOf("\"content\"", deltaIdx, StringComparison.Ordinal);
            if (contentIdx < 0)
                return null;

            return ExtractStringValue(json, contentIdx);
        }

        /// <summary>
        /// Extracts a JSON string value given the position of its key.
        /// Handles escaped characters within the string value.
        /// </summary>
        private static string ExtractStringValue(string json, int keyStart)
        {
            int colonIdx = json.IndexOf(':', keyStart);
            if (colonIdx < 0) return null;

            int openQuote = json.IndexOf('"', colonIdx + 1);
            if (openQuote < 0) return null;

            var sb = new StringBuilder();
            for (int i = openQuote + 1; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '/': sb.Append('/'); i++; break;
                        default: sb.Append('\\'); sb.Append(next); i++; break;
                    }
                }
                else if (json[i] == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(json[i]);
                }
            }
            return null;
        }

        /// <summary>
        /// Progressive JSON array parser. Feeds new text character by character,
        /// tracking brace/bracket depth to detect complete JSON objects within a
        /// top-level array. When a complete object is detected, attempts to parse
        /// it as a DiagnosticFinding via LLMResponseParser.ParseSingle().
        /// </summary>
        private void FeedProgressiveParser(string newText)
        {
            foreach (char c in newText)
            {
                if (_inString)
                {
                    _currentObject.Append(c);
                    if (_escaped)
                    {
                        _escaped = false;
                        continue;
                    }
                    if (c == '\\')
                    {
                        _escaped = true;
                        continue;
                    }
                    if (c == '"')
                    {
                        _inString = false;
                    }
                    continue;
                }

                // Outside a string
                switch (c)
                {
                    case '[':
                        if (!_foundArrayStart && _braceDepth == 0)
                        {
                            _foundArrayStart = true;
                            _arrayDepth++;
                            // Don't add '[' to current object
                            continue;
                        }
                        if (_braceDepth > 0)
                            _currentObject.Append(c);
                        _arrayDepth++;
                        break;

                    case ']':
                        _arrayDepth--;
                        if (_braceDepth > 0)
                            _currentObject.Append(c);
                        break;

                    case '{':
                        _braceDepth++;
                        _currentObject.Append(c);
                        break;

                    case '}':
                        _braceDepth--;
                        _currentObject.Append(c);

                        // Complete object detected at top level of the array
                        if (_braceDepth == 0 && _foundArrayStart)
                        {
                            string objectJson = _currentObject.ToString().Trim();
                            _currentObject.Clear();

                            if (objectJson.Length > 2) // Not just "{}"
                            {
                                try
                                {
                                    var finding = LLMResponseParser.ParseSingle(objectJson);
                                    if (finding.HasValue)
                                    {
                                        OnFindingParsed?.Invoke(finding.Value);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"[DrWario] Progressive parse failed for object: {e.Message}");
                                }
                            }
                        }
                        break;

                    case '"':
                        _inString = true;
                        if (_braceDepth > 0)
                            _currentObject.Append(c);
                        break;

                    case ',':
                        // Comma between objects at array level — ignore
                        if (_braceDepth > 0)
                            _currentObject.Append(c);
                        break;

                    default:
                        // Whitespace and other chars
                        if (_braceDepth > 0)
                            _currentObject.Append(c);
                        break;
                }
            }
        }
    }
}
