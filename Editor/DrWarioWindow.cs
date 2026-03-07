using UnityEditor;
using UnityEngine;

namespace DrWario.Editor
{
    public class DrWarioWindow : EditorWindow
    {
        [MenuItem("Window/DrWario/Diagnostics")]
        public static void ShowWindow()
        {
            var window = GetWindow<DrWarioWindow>("DrWario");
            window.minSize = new Vector2(700, 500);
        }

        public void CreateGUI()
        {
            rootVisualElement.Add(new DrWarioView());
        }
    }
}
