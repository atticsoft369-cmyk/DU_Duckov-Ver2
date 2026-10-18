using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    private float deltaTime = 0.0f;

    [Header("Display Settings")]
    public bool showFPS = true;
    public int fontSize = 24;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        if (!showFPS)
        {
            return;
        }

        float fps = 1.0f / deltaTime;

        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;

        Rect rect = new Rect(10, 10, 200, 50);

        GUI.Label(rect, $"FPS: {fps:0}", style);
    }
}