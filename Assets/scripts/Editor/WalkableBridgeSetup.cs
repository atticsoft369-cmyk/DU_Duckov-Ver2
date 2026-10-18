#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Map > Make Selected Bridge Walkable
/// 선택한 다리 오브젝트를 플레이어가 위로 지나갈 수 있게 만듭니다.
/// - 다리 BoxCollider -> Trigger + WalkableSurface (밟을 수 있는 면)
/// - 다리 긴 방향 양옆에 난간 벽 콜라이더 생성 (물로 떨어지지 않게)
/// 여러 번 실행해도 안전합니다.
/// </summary>
public static class WalkableBridgeSetup
{
    const float WallThickness = 0.3f;
    const float WallHeight = 2.5f;
    const string WallSuffix = "_SideWall";

    [MenuItem("Tools/Map/Make Selected Bridge Walkable")]
    static void MakeWalkable()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Walkable Bridge", "Play 모드를 끈 뒤 실행하세요.", "OK");
            return;
        }

        GameObject[] selected = Selection.gameObjects;

        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Walkable Bridge", "Hierarchy에서 다리 오브젝트를 먼저 선택하세요.", "OK");
            return;
        }

        foreach (GameObject bridge in selected)
        {
            Setup(bridge);
        }
    }

    static void Setup(GameObject bridge)
    {
        BoxCollider box = bridge.GetComponent<BoxCollider>();

        if (box == null)
        {
            Debug.LogWarning("[WalkableBridge] BoxCollider가 없습니다: " + bridge.name, bridge);
            return;
        }

        Undo.RecordObject(box, "Walkable Bridge");
        box.enabled = true;
        box.isTrigger = true;

        if (bridge.GetComponent<WalkableSurface>() == null)
        {
            Undo.AddComponent<WalkableSurface>(bridge);
        }

        Bounds b = box.bounds;
        bool longAlongZ = b.size.z >= b.size.x;
        Transform parent = bridge.transform.parent;

        for (int side = 0; side < 2; side++)
        {
            string wallName = bridge.name + WallSuffix + (side == 0 ? "_L" : "_R");
            Transform existing = parent != null ? parent.Find(wallName) : null;

            GameObject wall;

            if (existing != null)
            {
                wall = existing.gameObject;
            }
            else
            {
                wall = new GameObject(wallName);
                Undo.RegisterCreatedObjectUndo(wall, "Bridge Side Wall");
                wall.transform.SetParent(parent, true);
            }

            wall.layer = bridge.layer;

            Vector3 center;
            Vector3 size;

            if (longAlongZ)
            {
                float x = side == 0 ? b.min.x + WallThickness * 0.5f : b.max.x - WallThickness * 0.5f;
                center = new Vector3(x, b.max.y + WallHeight * 0.5f - 0.5f, b.center.z);
                size = new Vector3(WallThickness, WallHeight, b.size.z);
            }
            else
            {
                float z = side == 0 ? b.min.z + WallThickness * 0.5f : b.max.z - WallThickness * 0.5f;
                center = new Vector3(b.center.x, b.max.y + WallHeight * 0.5f - 0.5f, z);
                size = new Vector3(b.size.x, WallHeight, WallThickness);
            }

            wall.transform.position = center;
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = Vector3.one;

            if (parent != null)
            {
                // 부모 스케일 영향 제거
                Vector3 ls = parent.lossyScale;
                wall.transform.localScale = new Vector3(
                    ls.x != 0f ? 1f / ls.x : 1f,
                    ls.y != 0f ? 1f / ls.y : 1f,
                    ls.z != 0f ? 1f / ls.z : 1f
                );
            }

            BoxCollider wc = wall.GetComponent<BoxCollider>();

            if (wc == null)
            {
                wc = Undo.AddComponent<BoxCollider>(wall);
            }

            wc.center = Vector3.zero;
            wc.size = size;
            wc.isTrigger = false;
        }

        EditorSceneManager.MarkSceneDirty(bridge.scene);

        Debug.Log(
            "[WalkableBridge] 완료: " + bridge.name +
            " / 긴 방향=" + (longAlongZ ? "Z" : "X") +
            " / 다리 윗면 높이=" + b.max.y.ToString("F2") +
            ". 씬을 저장하세요(Ctrl+S).",
            bridge
        );
    }
}
#endif
