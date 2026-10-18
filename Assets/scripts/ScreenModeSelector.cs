using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 빌드 실행 시 맨 처음 "전체 화면 / 창 모드" 선택 화면을 띄웁니다.
/// 어느 쪽을 골라도 해상도는 1920 x 1080 으로 고정됩니다.
/// (씬에 따로 배치할 필요 없이 자동 생성됩니다)
/// </summary>
public class ScreenModeSelector : MonoBehaviour
{
    public const int Width = 1920;
    public const int Height = 1080;

    const string PrefKey = "ScreenMode_Fullscreen";

    // 에디터에서도 선택 화면을 보고 싶으면 true
    static readonly bool ShowInEditor = false;

    static bool shownThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (shownThisSession)
        {
            return;
        }

        shownThisSession = true;

        if (Application.isEditor && !ShowInEditor)
        {
            return;
        }

        // 선택 전에도 1920x1080 유지 (이전에 고른 모드로)
        bool lastFull = PlayerPrefs.GetInt(PrefKey, 1) == 1;
        ApplyMode(lastFull);

        GameObject go = new GameObject("ScreenModeSelector");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenModeSelector>();
    }

    public static void ApplyMode(bool fullscreen)
    {
        Screen.SetResolution(
            Width,
            Height,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed
        );
    }

    GameObject root;
    float previousTimeScale = 1f;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem (ScreenMode)", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        root = new GameObject("ScreenMode Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width, Height);
        scaler.matchWidthOrHeight = 0.5f;

        // 화면 전체를 덮는 어두운 배경 (뒤쪽 타이틀 입력 차단)
        Image dim = CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.82f));
        Stretch(dim.rectTransform);

        // 패널
        Image panel = CreateImage("Panel", root.transform, new Color(0.30f, 0.19f, 0.10f, 0.97f));
        panel.rectTransform.sizeDelta = new Vector2(760f, 420f);

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.75f, 0.35f, 1f);
        outline.effectDistance = new Vector2(6f, -6f);

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Lilita_One/LilitaOne-Regular SDF");

        CreateText("Title", panel.transform, "SCREEN MODE", 72f, new Vector2(0f, 120f), font, new Color(1f, 0.85f, 0.4f));
        CreateText("Sub", panel.transform, "1920 x 1080", 40f, new Vector2(0f, 45f), font, Color.white);

        CreateButton(panel.transform, "FULLSCREEN", new Vector2(-180f, -95f), font, new Color(0.95f, 0.55f, 0.15f), () => Choose(true));
        CreateButton(panel.transform, "WINDOW", new Vector2(180f, -95f), font, new Color(0.25f, 0.6f, 0.9f), () => Choose(false));

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    void Choose(bool fullscreen)
    {
        PlayerPrefs.SetInt(PrefKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();

        ApplyMode(fullscreen);

        Time.timeScale = previousTimeScale;
        Destroy(gameObject);
    }

    // ---------------- UI helpers ----------------

    static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Vector2 pos, TMP_FontAsset font, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(700f, 100f);
        rt.anchoredPosition = pos;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static void CreateButton(Transform parent, string label, Vector2 pos, TMP_FontAsset font, Color color, UnityEngine.Events.UnityAction onClick)
    {
        Image img = CreateImage(label + " Button", parent, color);
        img.rectTransform.sizeDelta = new Vector2(320f, 110f);
        img.rectTransform.anchoredPosition = pos;

        Outline o = img.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.6f);
        o.effectDistance = new Vector2(4f, -4f);

        Button b = img.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);

        TextMeshProUGUI t = CreateText("Label", img.transform, label, 44f, Vector2.zero, font, Color.white);
        t.rectTransform.sizeDelta = new Vector2(320f, 110f);
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 200);
    }
}
