using UnityEngine;
using UnityEngine.UI;

public class MonsterHpBarUI : MonoBehaviour
{
    [Header("HP Bar")]
    [Tooltip("실제로 HP가 줄어드는 Fill 이미지")]
    public Image hpFillImage;

    private RectTransform rectTransform;
    private Transform target;
    private Camera targetCamera;
    private Canvas parentCanvas;

    private Vector3 worldOffset;
    private bool initialized;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (rectTransform == null)
        {
            Debug.LogError(
                name + ": RectTransform을 찾지 못했습니다.",
                this
            );
        }
    }

    public void Initialize(
        Transform targetTransform,
        Camera cameraToUse,
        Vector3 offset)
    {
        target = targetTransform;
        targetCamera = cameraToUse;
        worldOffset = offset;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        initialized = true;

        SetHp(1f);
        UpdateScreenPosition();
    }

    public void SetHp(float normalizedHp)
    {
        if (hpFillImage == null)
        {
            Debug.LogWarning(
                name + ": HP Fill Image가 연결되지 않았습니다.",
                this
            );

            return;
        }

        hpFillImage.fillAmount =
            Mathf.Clamp01(normalizedHp);
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateScreenPosition();
    }

    private void UpdateScreenPosition()
    {
        if (target == null ||
            rectTransform == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 worldPosition =
            target.position + worldOffset;

        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(
                worldPosition
            );

        if (screenPosition.z <= 0f)
        {
            return;
        }

        /*
         * Screen Space Overlay Canvas라면
         * 화면 좌표를 그대로 사용합니다.
         */
        if (parentCanvas == null ||
            parentCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            rectTransform.position =
                screenPosition;

            return;
        }

        /*
         * Screen Space Camera Canvas 대응
         */
        RectTransform canvasRect =
            parentCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            rectTransform.position =
                screenPosition;

            return;
        }

        Camera canvasCamera =
            parentCanvas.renderMode ==
            RenderMode.ScreenSpaceCamera
                ? parentCanvas.worldCamera
                : targetCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvasCamera,
                out Vector2 localPosition))
        {
            rectTransform.anchoredPosition =
                localPosition;
        }
    }
}