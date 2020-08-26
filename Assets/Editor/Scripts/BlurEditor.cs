using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Blur))]
public class BlurEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        //DrawDefaultInspector();

        Blur script = (Blur)target;
        if (GUILayout.Button("DoBlur"))
        {
            script.DoBlur();
        }
        if(GUILayout.Button("ClearBlur"))
        {
            script.ClearBlur();
        }
    }
}
