#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Dam > Setup Dam Parts
/// 이름이 Dam_ 으로 시작하는 오브젝트(Dam_01~04)에 DamPart를 붙이고
/// 씬의 DamHealth와 연결합니다. 몬스터 판정용으로 BoxCollider를 사용합니다.
/// 여러 번 실행해도 안전합니다.
/// </summary>
public static class SetupDamParts
{
    static readonly string[] DamNamePrefixes = { "Dam_" };

    [MenuItem("Tools/Dam/Setup Dam Parts")]
    public static void Setup()
    {
        DamHealth health = Object.FindFirstObjectByType<DamHealth>(FindObjectsInactive.Include);

        if (health == null)
        {
            Debug.LogError("[DamSetup] 씬에서 DamHealth를 찾지 못했습니다.");
            return;
        }

        int count = 0;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.gameObject.scene.IsValid() || !DamNamePrefixes.Any(p => t.name.StartsWith(p)))
            {
                continue;
            }

            // 같은 이름 계열의 자식은 건너뛰고 최상위 조각에만 붙입니다.
            if (t.parent != null && DamNamePrefixes.Any(p => t.parent.name.StartsWith(p)))
            {
                continue;
            }

            GameObject go = t.gameObject;

            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(go);
                FitBoxToRenderers(box);
            }
            else
            {
                // 음수 크기 보정 (BoxCollider는 음수 크기를 지원하지 않음)
                Undo.RecordObject(box, "Fix Box Size");
                box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
            }

            DamPart part = go.GetComponent<DamPart>();
            if (part == null)
            {
                part = Undo.AddComponent<DamPart>(go);
            }

            Undo.RecordObject(part, "Setup DamPart");
            part.sharedHealth = health;
            part.targetCollider = box;
            EditorUtility.SetDirty(part);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(health.gameObject.scene);
        Debug.Log("[DamSetup] DamPart 설정 완료: " + count + "개. 씬을 저장하세요.");
    }

    static void FitBoxToRenderers(BoxCollider box)
    {
        Renderer[] renderers = box.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds world = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            world.Encapsulate(r.bounds);
        }

        Transform t = box.transform;
        Vector3 center = t.InverseTransformPoint(world.center);
        Vector3 size = t.InverseTransformVector(world.size);
        box.center = center;
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }
}
#endif
