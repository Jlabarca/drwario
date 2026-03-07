using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DrWario.Editor.Analysis.LLM
{
    public class LLMClient
    {
        private readonly LLMConfig _config;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private const int RateLimitSeconds = 10;

        public LLMClient(LLMConfig config)
        {
            _config = config;
        }

        public async Task<LLMResponse> SendAsync(string systemPrompt, string userPrompt)
        {
            // Rate limiting
            var elapsed = (DateTime.UtcNow - _lastRequestTime).TotalSeconds;
            if (elapsed < RateLimitSeconds)
                return LLMResponse.Error($"Rate limited. Wait {RateLimitSeconds - (int)elapsed}s before retrying.");

            _lastRequestTime = DateTime.UtcNow;

            string endpoint = _config.Endpoint;
            string body = BuildRequestBody(systemPrompt, userPrompt);
            if (body == null)
                return LLMResponse.Error($"Unsupported provider: {_config.Provider}");

            Debug.Log($"[DrWario] Sending {body.Length} chars to {_config.Provider} ({_config.ModelId})...");

            try
            {
                var request = new UnityWebRequest(endpoint, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = _config.TimeoutSeconds;

                request.SetRequestHeader("Content-Type", "application/json");
                SetAuthHeaders(request);

                var operation = request.SendWebRequest();

                // Async wait for UnityWebRequest
                while (!operation.isDone)
                    await Task.Delay(100);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorDetail = request.downloadHandler?.text ?? request.error;
                    Debug.LogWarning($"[DrWario] LLM request failed ({request.responseCode}): {errorDetail}");

                    return request.responseCode switch
                    {
                        401 => LLMResponse.Error("Invalid API key. Check DrWario settings."),
                        429 => LLMResponse.Error("Rate limited by provider. Try again in 60s."),
                        _ => LLMResponse.Error($"HTTP {request.responseCode}: {request.error}")
                    };
                }

                string responseText = request.downloadHandler.text;
                string content = ExtractContent(responseText);

                if (content == null)
                    return LLMResponse.Error($"Failed to parse response from {_config.Provider}.");

                Debug.Log($"[DrWario] Received {content.Length} chars from {_config.Provider}.");
                return LLMResponse.Success(content);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DrWario] LLM exception: {e.Message}");
                return LLMResponse.Error($"Exception: {e.Message}");
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            var result = await SendAsync(
                "You are a test endpoint. Respond with exactly: {\"status\":\"ok\"}",
                "Ping"
            );
            return result.IsSuccess;
        }

        private string BuildRequestBody(string systemPrompt, string userPrompt)
        {
            string escapedSystem = EscapeJsonString(systemPrompt);
            string escapedUser = EscapeJsonString(userPrompt);
            string model = _config.ModelId;

            return _config.Provider switch
            {
                LLMProvider.Claude => BuildClaudeBody(escapedSystem, escapedUser, model),
                LLMProvider.OpenAI => BuildOpenAIBody(escapedSystem, escapedUser, model),
                LLMProvider.Ollama => BuildOllamaBody(escapedSystem, escapedUser, model),
                LLMProvider.Custom => BuildOpenAIBody(escapedSystem, escapedUser, model),
                _ => null
            };
        }

        private string BuildClaudeBody(string system, string user, string model)
        {
            return $@"{{
  ""model"": ""{model}"",
  ""max_tokens"": 4096,
  ""system"": ""{system}"",
  ""messages"": [
    {{ ""role"": ""user"", ""content"": ""{user}"" }}
  ]
}}";
        }

        private string BuildOpenAIBody(string system, string user, string model)
        {
            return $@"{{
  ""model"": ""{model}"",
  ""max_tokens"": 4096,
  ""messages"": [
    {{ ""role"": ""system"", ""content"": ""{system}"" }},
    {{ ""role"": ""user"", ""content"": ""{user}"" }}
  ],
  ""temperature"": 0.3
}}";
        }

        private string BuildOllamaBody(string system, string user, string model)
        {
            return $@"{{
  ""model"": ""{model}"",
  ""stream"": false,
  ""messages"": [
    {{ ""role"": ""system"", ""content"": ""{system}"" }},
    {{ ""role"": ""user"", ""content"": ""{user}"" }}
  ]
}}";
        }

        private void SetAuthHeaders(UnityWebRequest request)
        {
            switch (_config.Provider)
            {
                case LLMProvider.Claude:
                    request.SetRequestHeader("x-api-key", _config.ApiKey);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                    break;
                case LLMProvider.OpenAI:
                case LLMProvider.Custom:
                    request.SetRequestHeader("Authorization", $"Bearer {_config.ApiKey}");
                    break;
                // Ollama: no auth by default
            }
        }

        private string ExtractContent(string responseJson)
        {
            try
            {
                return _config.Provider switch
                {
                    LLMProvider.Claude => ExtractClaudeContent(responseJson),
                    LLMProvider.OpenAI or LLMProvider.Custom => ExtractOpenAIContent(responseJson),
                    LLMProvider.Ollama => ExtractOllamaContent(responseJson),
                    _ => null
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DrWario] Content extraction failed: {e.Message}");
                return null;
            }
        }

        // Lightweight JSON extraction without full parser dependency

        private static string ExtractClaudeContent(string json)
        {
            // Claude: {"content":[{"type":"text","text":"..."}]}
            int textIdx = json.IndexOf("\"text\"", StringComparison.Ordinal);
            if (textIdx < 0) return null;
            return ExtractStringValue(json, textIdx);
        }

        private static string ExtractOpenAIContent(string json)
        {
            // OpenAI: {"choices":[{"message":{"content":"..."}}]}
            int contentIdx = json.IndexOf("\"content\"", json.IndexOf("\"message\"", StringComparison.Ordinal), StringComparison.Ordinal);
            if (contentIdx < 0) return null;
            return ExtractStringValue(json, contentIdx);
        }

        private static string ExtractOllamaContent(string json)
        {
            // Ollama: {"message":{"content":"..."}}
            int msgIdx = json.IndexOf("\"message\"", StringComparison.Ordinal);
            if (msgIdx < 0) return null;
            int contentIdx = json.IndexOf("\"content\"", msgIdx, StringComparison.Ordinal);
            if (contentIdx < 0) return null;
            return ExtractStringValue(json, contentIdx);
        }

        private static string ExtractStringValue(string json, int keyStart)
        {
            // Find the colon after the key
            int colonIdx = json.IndexOf(':', keyStart);
            if (colonIdx < 0) return null;

            // Find the opening quote of the value
            int openQuote = json.IndexOf('"', colonIdx + 1);
            if (openQuote < 0) return null;

            // Find the closing quote, handling escaped quotes
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

        private static string EscapeJsonString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }

    public struct LLMResponse
    {
        public bool IsSuccess;
        public string Content;
        public string ErrorMessage;

        public static LLMResponse Success(string content) => new()
            { IsSuccess = true, Content = content };

        public static LLMResponse Error(string message) => new()
            { IsSuccess = false, ErrorMessage = message };
    }
}
