#if UNITY_EDITOR
using UnityEditor;

namespace DrWario.Editor.Analysis
{
    public static class RuleConfig
    {
        public static bool IsEnabled(string ruleId)
        {
            return EditorPrefs.GetBool($"DrWario_Rule_{ruleId}_Enabled", true);
        }

        public static void SetEnabled(string ruleId, bool enabled)
        {
            EditorPrefs.SetBool($"DrWario_Rule_{ruleId}_Enabled", enabled);
        }

        public static float GetThreshold(string ruleId, float defaultValue)
        {
            return EditorPrefs.GetFloat($"DrWario_Rule_{ruleId}_Threshold", defaultValue);
        }

        public static void SetThreshold(string ruleId, float value)
        {
            EditorPrefs.SetFloat($"DrWario_Rule_{ruleId}_Threshold", value);
        }

        public static void ResetRule(string ruleId)
        {
            EditorPrefs.DeleteKey($"DrWario_Rule_{ruleId}_Enabled");
            EditorPrefs.DeleteKey($"DrWario_Rule_{ruleId}_Threshold");
        }
    }
}
#endif
