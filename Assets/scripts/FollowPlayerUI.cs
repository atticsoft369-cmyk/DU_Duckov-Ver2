using UnityEngine;

public class FollowPlayerUI : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera")]
    public Camera mainCamera;

    [Header("Follow Offset")]
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("UI Offset")]
    public Vector2 screenOffset = new Vector2(0f, 0f);

    [Header("Visibility")]
    public bool hideWhenBehindCamera = false;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            return;
        }

        if (rectTransform == null)
        {
            return;
        }

        Vector3 worldPosition = target.position + worldOffset;
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

        if (hideWhenBehindCamera && screenPosition.z < 0f)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        canvasGroup.alpha = 1f;

        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            rectTransform.position =
                screenPosition + new Vector3(screenOffset.x, screenOffset.y, 0f);
        }
        else
        {
            RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

            Vector2 localPoint;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                parentCanvas.worldCamera,
                out localPoint
            );

            rectTransform.anchoredPosition = localPoint + screenOffset;
        }
    }
}