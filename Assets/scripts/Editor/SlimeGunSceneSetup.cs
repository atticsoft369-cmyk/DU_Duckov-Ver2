#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴: Tools > Slime Gun > Setup Scene
/// 현재 열린 씬에 슬라임 건 UI/컴포넌트를 세팅합니다. 여러 번 실행해도 안전합니다.
/// - 3번 슬롯: Slime Gun (새 아이콘)
/// - 4번 슬롯: Grenade (기존 Weapon 3 아이콘을 Slot 4 위치로 이동)
/// - F 슬롯: Heal
/// - 슬라임 건 게이지 UI 생성
/// - Player에 SlimeGunController 추가 및 연결
/// </summary>
public static class SlimeGunSceneSetup
{
    const string UiFolder = "Assets/(1013) New Data/UI/";
    const string IconSpritePath = UiFolder + "slimegun.png";
    const string FloorSpritePath = UiFolder + "Slime gun Floor.png";
    const string GaugeBgSpritePath = UiFolder + "Slime gun_bg.png";
    const string GaugeFillSpritePath = UiFolder + "Slime gun_base.png";

    const string SlimeIconName = "Weapon SlimeGun";
    const string GaugeRootName = "SlimeGun Gauge";

    [MenuItem("Tools/Slime Gun/Setup Scene")]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Slime Gun Setup", "Play 모드를 끈 뒤 실행하세요.", "OK");
            return;
        }

        WeaponIconSelector selector = Object.FindFirstObjectByType<WeaponIconSelector>(FindObjectsInactive.Include);
        TopDownPlayerController player = Object.FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);

        if (selector == null || player == null)
        {
            Debug.LogError("[SlimeGunSetup] WeaponIconSelector 또는 TopDownPlayerController를 찾지 못했습니다.");
            return;
        }

        Transform weaponController = FindInScene("Weapon Controller");
        Transform weaponSocket = FindInScene("Weapon Socket");

        if (weaponController == null || weaponSocket == null)
        {
            Debug.LogError("[SlimeGunSetup] Canvas/Weapon Controller 또는 Weapon Socket을 찾지 못했습니다.");
            return;
        }

        Transform slot3 = weaponSocket.Find("Slot 3");
        Transform slot4 = weaponSocket.Find("Slot 4");
        Transform slot5 = weaponSocket.Find("Slot 5");
        Transform grenadeIcon = weaponController.Find("Weapon 3");

        if (slot3 == null || slot4 == null || slot5 == null || grenadeIcon == null)
        {
            Debug.LogError("[SlimeGunSetup] Slot 3/4/5 또는 Weapon 3(수류탄 아이콘)을 찾지 못했습니다.");
            return;
        }

        Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath);
        Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorSpritePath);
        Sprite gaugeBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GaugeBgSpritePath);
        Sprite gaugeFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GaugeFillSpritePath);

        if (iconSprite == null) Debug.LogWarning("[SlimeGunSetup] 아이콘 스프라이트 없음: " + IconSpritePath);
        if (floorSprite == null) Debug.LogWarning("[SlimeGunSetup] 장판 스프라이트 없음: " + FloorSpritePath);

        // ---------- 1. 수류탄 아이콘을 4번 슬롯 위치로 이동 ----------
        RectTransform grenadeRect = grenadeIcon as RectTransform;
        RectTransform slot3Rect = slot3 as RectTransform;
        RectTransform slot4Rect = slot4 as RectTransform;

        Undo.RecordObject(grenadeRect, "Move Grenade Icon");
        Vector2 slot3IconPos = new Vector2(slot3Rect.anchoredPosition.x, grenadeRect.anchoredPosition.y);
        grenadeRect.anchoredPosition = new Vector2(slot4Rect.anchoredPosition.x, grenadeRect.anchoredPosition.y);

        // ---------- 2. 3번 슬롯에 슬라임 건 아이콘 생성 ----------
        Transform slimeIconTr = weaponController.Find(SlimeIconName);
        Image slimeIcon;

        if (slimeIconTr == null)
        {
            GameObject go = new GameObject(SlimeIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create Slime Gun Icon");
            go.layer = grenadeIcon.gameObject.layer;
            go.transform.SetParent(weaponController, false);
            // Weapon 3 바로 앞 순서에 둡니다.
            go.transform.SetSiblingIndex(grenadeIcon.GetSiblingIndex());
            slimeIconTr = go.transform;
        }

        slimeIcon = slimeIconTr.GetComponent<Image>();
        RectTransform slimeIconRect = slimeIconTr as RectTransform;
        slimeIconRect.anchorMin = slimeIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        slimeIconRect.pivot = new Vector2(0.5f, 0.5f);
        slimeIconRect.anchoredPosition = slot3IconPos;
        slimeIconRect.sizeDelta = new Vector2(100f, 67f);
        slimeIconRect.localScale = Vector3.one;
        slimeIcon.sprite = iconSprite;
        slimeIcon.preserveAspect = true;
        slimeIcon.raycastTarget = false;

        // ---------- 3. 슬롯 라벨 확인 (3 / 4 / F) ----------
        SetSlotLabel(slot3, "3");
        SetSlotLabel(slot4, "4");
        SetSlotLabel(slot5, "F");

        // ---------- 4. WeaponIconSelector 슬롯 배열 재구성 ----------
        Undo.RecordObject(selector, "Setup Weapon Icons");

        WeaponIconSelector.WeaponIcon[] old = selector.weaponIcons;
        WeaponIconSelector.WeaponIcon pistol = FindIcon(old, "Pistol", 0);
        WeaponIconSelector.WeaponIcon rifle = FindIcon(old, "Rifle", 1);
        WeaponIconSelector.WeaponIcon heal = FindIcon(old, "Heal", old.Length - 1);

        WeaponIconSelector.WeaponIcon slime = new WeaponIconSelector.WeaponIcon
        {
            weaponName = "Slime Gun",
            iconImage = slimeIcon,
            backgroundImage = slot3.GetComponent<Image>()
        };

        WeaponIconSelector.WeaponIcon grenade = new WeaponIconSelector.WeaponIcon
        {
            weaponName = "Grenade",
            iconImage = grenadeIcon.GetComponent<Image>(),
            backgroundImage = slot4.GetComponent<Image>()
        };

        selector.weaponIcons = new[] { pistol, rifle, slime, grenade, heal };
        selector.healSlotIndex = 4;
        selector.startUnlockedSlotIndices = new[] { 0 };
        EditorUtility.SetDirty(selector);

        // ---------- 5. 게이지 UI ----------
        Image gaugeFill = CreateGauge(weaponSocket, slot3Rect, gaugeBgSprite, gaugeFillSprite);

        // ---------- 6. SlimeGunController ----------
        SlimeGunController slimeGun = player.GetComponent<SlimeGunController>();

        if (slimeGun == null)
        {
            slimeGun = Undo.AddComponent<SlimeGunController>(player.gameObject);
        }

        Undo.RecordObject(slimeGun, "Setup Slime Gun");
        slimeGun.gaugeFillImage = gaugeFill;
        slimeGun.floorSprite = floorSprite;
        EditorUtility.SetDirty(slimeGun);

        Undo.RecordObject(player, "Link Slime Gun");
        player.slimeGun = slimeGun;
        EditorUtility.SetDirty(player);

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

        Debug.Log("[SlimeGunSetup] 완료: 3번 Slime Gun / 4번 Grenade / F Heal, 게이지 UI 생성. 씬을 저장하세요(Ctrl+S).");
    }

    static WeaponIconSelector.WeaponIcon FindIcon(WeaponIconSelector.WeaponIcon[] icons, string name, int fallbackIndex)
    {
        if (icons != null)
        {
            foreach (var icon in icons)
            {
                if (icon != null && icon.weaponName == name)
                {
                    return icon;
                }
            }

            if (fallbackIndex >= 0 && fallbackIndex < icons.Length)
            {
                return icons[fallbackIndex];
            }
        }

        return new WeaponIconSelector.WeaponIcon { weaponName = name };
    }

    static void SetSlotLabel(Transform slot, string label)
    {
        TMP_Text text = slot.GetComponentInChildren<TMP_Text>(true);

        if (text != null && text.text != label)
        {
            Undo.RecordObject(text, "Slot Label");
            text.text = label;
            EditorUtility.SetDirty(text);
        }
    }

    static Image CreateGauge(Transform parent, RectTransform slot3Rect, Sprite bgSprite, Sprite fillSprite)
    {
        Transform root = parent.Find(GaugeRootName);

        if (root == null)
        {
            GameObject go = new GameObject(GaugeRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create Slime Gun Gauge");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            root = go.transform;

            RectTransform rootRect = root as RectTransform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            // 3번 슬롯 위, 댐 HP바 아래
            rootRect.anchoredPosition = new Vector2(slot3Rect.anchoredPosition.x, -362f);
            rootRect.sizeDelta = new Vector2(117f, 15f);
        }

        Image bg = root.GetComponent<Image>();
        bg.sprite = bgSprite;
        bg.type = Image.Type.Simple;
        bg.raycastTarget = false;

        Transform fillTr = root.Find("Fill");

        if (fillTr == null)
        {
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(fillGo, "Create Slime Gun Gauge Fill");
            fillGo.layer = root.gameObject.layer;
            fillGo.transform.SetParent(root, false);
            fillTr = fillGo.transform;

            RectTransform fillRect = fillTr as RectTransform;
            // 배경(106x14) 안쪽에 채움(102x10)이 들어가도록 비율 유지
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2.2f, 2.1f);
            fillRect.offsetMax = new Vector2(-2.2f, -2.1f);
        }

        Image fill = fillTr.GetComponent<Image>();
        fill.sprite = fillSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;

        return fill;
    }

    static Transform FindInScene(string objectName)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == objectName && t.gameObject.scene.IsValid())
            {
                return t;
            }
        }

        return null;
    }
}
#endif
