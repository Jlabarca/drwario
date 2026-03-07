using UnityEditor;
using UnityEngine;

namespace DrWario.Editor.Analysis.LLM
{
    public enum LLMProvider
    {
        Claude,
        OpenAI,
        Ollama,
        Custom
    }

    public class LLMConfig
    {
        private const string PrefsPrefix = "DrWario_";

        public LLMProvider Provider
        {
            get => (LLMProvider)EditorPrefs.GetInt(PrefsPrefix + "Provider", 0);
            set => EditorPrefs.SetInt(PrefsPrefix + "Provider", (int)value);
        }

        public string ApiKey
        {
            get => Deobfuscate(EditorPrefs.GetString(PrefsPrefix + "ApiKey_" + Provider, ""));
            set => EditorPrefs.SetString(PrefsPrefix + "ApiKey_" + Provider, Obfuscate(value));
        }

        public string ModelId
        {
            get => EditorPrefs.GetString(PrefsPrefix + "ModelId", DefaultModelId);
            set => EditorPrefs.SetString(PrefsPrefix + "ModelId", value);
        }

        public string Endpoint
        {
            get => EditorPrefs.GetString(PrefsPrefix + "Endpoint", DefaultEndpoint);
            set => EditorPrefs.SetString(PrefsPrefix + "Endpoint", value);
        }

        public int TimeoutSeconds
        {
            get => EditorPrefs.GetInt(PrefsPrefix + "Timeout", 30);
            set => EditorPrefs.SetInt(PrefsPrefix + "Timeout", value);
        }

        public bool Enabled
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "Enabled", false);
            set => EditorPrefs.SetBool(PrefsPrefix + "Enabled", value);
        }

        public string DefaultModelId => Provider switch
        {
            LLMProvider.Claude => "claude-sonnet-4-6",
            LLMProvider.OpenAI => "gpt-4o",
            LLMProvider.Ollama => "llama3:70b",
            _ => "gpt-4o"
        };

        public string DefaultEndpoint => Provider switch
        {
            LLMProvider.Claude => "https://api.anthropic.com/v1/messages",
            LLMProvider.OpenAI => "https://api.openai.com/v1/chat/completions",
            LLMProvider.Ollama => "http://localhost:11434/api/chat",
            _ => ""
        };

        public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

        public bool IsConfigured => Enabled && (Provider == LLMProvider.Ollama || HasApiKey);

        public void ResetToDefaults()
        {
            ModelId = DefaultModelId;
            Endpoint = DefaultEndpoint;
            TimeoutSeconds = 30;
        }

        // Simple XOR obfuscation — not encryption, just prevents plaintext in registry
        private static string Obfuscate(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var key = SystemInfo.deviceUniqueIdentifier;
            var chars = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
                chars[i] = (char)(input[i] ^ key[i % key.Length]);
            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string(chars)));
        }

        private static string Deobfuscate(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(input));
                var key = SystemInfo.deviceUniqueIdentifier;
                var chars = new char[decoded.Length];
                for (int i = 0; i < decoded.Length; i++)
                    chars[i] = (char)(decoded[i] ^ key[i % key.Length]);
                return new string(chars);
            }
            catch
            {
                return "";
            }
        }
    }
}
