using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause / 게임 클리어 / 게임 오버 화면이 켜지면 뒤 배경을 반투명 검정으로 어둡게 합니다.
/// 씬이 로드될 때 아래 이름의 오브젝트를 찾아 자동으로 붙습니다.
/// </summary>
public class UIDimBackground : MonoBehaviour
{
    static readonly string[] TargetNames = { "Pause Popup", "GameOver Canvas", "GameClear Canvas" };

    public Color dimColor = new Color(0f, 0f, 0f, 0.5f);

    GameObject dim;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachAll();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachAll();
    }

    static void AttachAll()
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || !go.scene.IsValid())
            {
                continue;
            }

            foreach (string n in TargetNames)
            {
                if (go.name == n && go.GetComponent<UIDimBackground>() == null)
                {
                    UIDimBackground d = go.AddComponent<UIDimBackground>();

                    // 이미 켜져 있는 상태에서 붙은 경우 바로 적용
                    if (go.activeInHierarchy)
                    {
                        d.ShowDim(true);
                    }
                }
            }
        }
    }

    void OnEnable()
    {
        ShowDim(true);
    }

    void OnDisable()
    {
        ShowDim(false);
    }

    void OnDestroy()
    {
        if (dim != null)
        {
            Destroy(dim);
        }
    }

    void ShowDim(bool show)
    {
        if (show && dim == null)
        {
            CreateDim();
        }

        if (dim != null && dim.activeSelf != show)
        {
            dim.SetActive(show);
        }

        if (show && dim != null && transform.parent != null && dim.transform.parent == transform.parent)
        {
            // 형제로 둔 경우: 항상 대상 바로 뒤(아래)에 그려지도록 순서 유지
            int targetIndex = transform.GetSiblingIndex();
            int dimIndex = dim.transform.GetSiblingIndex();

            if (dimIndex != targetIndex - 1)
            {
                dim.transform.SetSiblingIndex(Mathf.Max(0, dimIndex < targetIndex ? targetIndex - 1 : targetIndex));
            }
        }
    }

    void CreateDim()
    {
        dim = new GameObject(name + " Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.layer = gameObject.layer;

        RectTransform rt = dim.GetComponent<RectTransform>();
        Canvas ownCanvas = GetComponent<Canvas>();

        if (ownCanvas != null && ownCanvas.isRootCanvas)
        {
            // 루트 Canvas(게임오버/클리어): Canvas 안 맨 뒤에 깔기
            rt.SetParent(transform, false);
            rt.SetAsFirstSibling();
        }
        else
        {
            // 일반 패널(Pause): 같은 부모 안에서 패널 바로 뒤에 깔기
            rt.SetParent(transform.parent, false);
            rt.SetSiblingIndex(transform.GetSiblingIndex());
        }

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Image img = dim.GetComponent<Image>();
        img.color = dimColor;
        img.raycastTarget = true; // 뒤쪽 UI 클릭 차단
    }
}
