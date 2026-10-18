#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Monster > Setup King Slime
/// - Slime_King 프리팹에 새 King slime 모델 + 애니메이터(Idle / Floor / Shot) 연결
/// - 떨어지는 구체 프리팹(King_FloorSphere) 생성
/// - 보스 스킬 이미지(Boss Floor / Attack effect) 연결
/// - 플레이어 탄환 표시 배경(BULLET) 연결 (열린 씬)
/// 여러 번 실행해도 안전합니다.
/// </summary>
public static class KingSlimeSetup
{
    const string KingRoot = "Assets/(1013) New Data/Monster/King/";
    const string IdleFbx = KingRoot + "King_ani/King_idle.fbx";
    const string ModelFbx = KingRoot + "king slime.fbx";   // 보스 모델
    const string AttackFbx = KingRoot + "King_ani/King_attack_floor.fbx";
    const string FloorDropFbx = KingRoot + "King_floor/King floor_ani/King floor_drop.fbx";
    const string KingTexFolder = KingRoot + "King_text";
    const string FloorTexFolder = KingRoot + "King_floor/King floor_text";

    const string UiFolder = "Assets/(1013) New Data/UI/";
    const string BossFloorSprite = UiFolder + "Boss Floor.png";
    const string AttackEffectSprite = UiFolder + "Attack effect.png";
    const string BulletSprite = UiFolder + "BULLET.png";

    const string KingPrefab = "Assets/Resources/prefabs/Slime_King.prefab";
    const string SpherePrefab = "Assets/Resources/prefabs/King_FloorSphere.prefab";
    const string ModelChildName = "Model";

    [MenuItem("Tools/Monster/Setup King Slime")]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("King Slime Setup", "Play 모드를 끈 뒤 실행하세요.", "OK");
            return;
        }

        SetLoop(IdleFbx, true);
        SetLoop(AttackFbx, false);
        SetLoop(FloorDropFbx, false);

        Material kingMat = CreateMaterial(KingTexFolder, "King_Slime_Mat");
        Material floorMat = CreateMaterial(FloorTexFolder, "King_Floor_Mat");

        AnimatorController kingController = BuildKingController();
        AnimatorController floorController = BuildFloorController();

        GameObject sphere = BuildSpherePrefab(floorMat, floorController);
        SetupKingPrefab(kingMat, kingController, sphere);
        ApplyAmmoBackground();

        AssetDatabase.SaveAssets();
        Debug.Log("[KingSetup] 완료: Slime_King 모델/애니메이터, King_FloorSphere, 스킬 이미지, 탄환 배경. 씬을 저장하세요(Ctrl+S).");
    }

    // ---------------- Import ----------------

    static void SetLoop(string fbxPath, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[KingSetup] FBX 없음: " + fbxPath);
            return;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        foreach (var c in clips)
        {
            if (c.loopTime != loop)
            {
                c.loopTime = loop;
                changed = true;
            }
        }

        if (changed || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    static AnimationClip LoadClip(string fbxPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static Material CreateMaterial(string texFolder, string matName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder });
        Texture2D baseColor = null;
        Texture2D normal = null;

        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string n = Path.GetFileNameWithoutExtension(p);

            if (n.EndsWith("_BaseColor"))
            {
                baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            else if (n.EndsWith("_Normal"))
            {
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }

                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
        }

        if (baseColor == null)
        {
            Debug.LogWarning("[KingSetup] BaseColor 텍스처 없음: " + texFolder);
            return null;
        }

        string matPath = texFolder + "/" + matName + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.SetTexture("_BaseMap", baseColor);
        mat.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.45f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------- Animator ----------------

    static AnimatorController BuildKingController()
    {
        string path = KingRoot + "King_ani/King_Slime.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        foreach (string trig in new[] { "Attack", "Hit", "Die", "Floor", "Shot", "Dash", "JumpStart", "JumpEnd", "Poison" })
        {
            ac.AddParameter(trig, AnimatorControllerParameterType.Trigger);
        }

        AnimationClip idleClip = LoadClip(IdleFbx);
        AnimationClip attackClip = LoadClip(AttackFbx);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle", new Vector3(300, 0, 0));
        idle.motion = idleClip;
        sm.defaultState = idle;

        AnimatorState attack = sm.AddState("Attack", new Vector3(300, 150, 0));
        attack.motion = attackClip;

        AnimatorState floor = sm.AddState("Floor", new Vector3(550, 150, 0));
        floor.motion = attackClip;

        AnimatorState shot = sm.AddState("Shot", new Vector3(550, -150, 0));
        shot.motion = attackClip;

        AnimatorState die = sm.AddState("Die", new Vector3(800, 0, 0));
        die.motion = idleClip;
        die.speed = 0f; // 사망 시 정지

        AddAny(sm, attack, "Attack");
        AddAny(sm, floor, "Floor");
        AddAny(sm, shot, "Shot");
        AddAny(sm, die, "Die").canTransitionToSelf = false;

        Back(attack, idle);
        Back(floor, idle);
        Back(shot, idle);

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static AnimatorController BuildFloorController()
    {
        string path = KingRoot + "King_floor/King floor_ani/King_Floor.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;
        AnimatorState drop = sm.AddState("Drop", new Vector3(300, 0, 0));
        drop.motion = LoadClip(FloorDropFbx);
        sm.defaultState = drop;

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static AnimatorStateTransition AddAny(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.canTransitionToSelf = true;
        return t;
    }

    static void Back(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0.1f;
    }

    // ---------------- Prefabs ----------------

    static GameObject BuildSpherePrefab(Material mat, AnimatorController controller)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(FloorDropFbx);
        if (asset == null)
        {
            Debug.LogWarning("[KingSetup] 구체 FBX 없음: " + FloorDropFbx);
            return null;
        }

        GameObject root = new GameObject("King_FloorSphere");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.transform.SetParent(root.transform, false);

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (mat != null)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c);
        }

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 구체 지름을 약 1.6m 로 맞춤
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        float size = has ? Mathf.Max(b.size.x, b.size.y, b.size.z) : 1f;
        if (size > 0.001f)
        {
            model.transform.localScale = Vector3.one * (1.6f / size);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SpherePrefab);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void SetupKingPrefab(Material mat, AnimatorController controller, GameObject spherePrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(KingPrefab);
        if (root == null)
        {
            Debug.LogError("[KingSetup] 프리팹 없음: " + KingPrefab);
            return;
        }

        try
        {
            Transform oldModel = root.transform.Find(ModelChildName);
            float previousScale = oldModel != null ? oldModel.localScale.x : -1f;

            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            float oldHeight = 0f;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Minimap"))
                {
                    continue;
                }

                if (r.enabled)
                {
                    oldHeight = Mathf.Max(oldHeight, r.bounds.size.y);
                }

                r.enabled = false;
            }

            Animator oldAnimator = root.GetComponent<Animator>();
            if (oldAnimator != null)
            {
                oldAnimator.enabled = false;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);

            if (modelAsset == null)
            {
                Debug.LogWarning("[KingSetup] 모델 없음: " + ModelFbx + " → King_idle 모델로 대체");
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbx);
            }
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = ModelChildName;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            float newHeight = 0f;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                newHeight = Mathf.Max(newHeight, r.bounds.size.y);

                if (mat != null)
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }

            if (previousScale > 0f)
            {
                model.transform.localScale = Vector3.one * previousScale;
            }
            else if (oldHeight > 0.01f && newHeight > 0.01f)
            {
                model.transform.localScale = Vector3.one * (oldHeight / newHeight);
            }

            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = root.layer;
            }

            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            BossMonsterChaser boss = root.GetComponent<BossMonsterChaser>();
            if (boss != null)
            {
                boss.modelAnimator = animator;
                boss.useKingSkills = true;
                boss.startTargetPlayer = true;
                boss.kingSpherePrefab = spherePrefab;
                boss.floorZoneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossFloorSprite);
                boss.shotWarningSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackEffectSprite);
                boss.deathAnimationDuration = 1.5f;

                AnimationClip attack = LoadClip(AttackFbx);
                if (attack != null)
                {
                    // 공격 모션 중간쯤 구체가 떨어지기 시작
                    boss.floorCastTime = Mathf.Clamp(attack.length * 0.5f, 0.2f, 1.5f);
                }

                if (boss.floorZoneSprite == null) Debug.LogWarning("[KingSetup] Boss Floor 스프라이트 없음: " + BossFloorSprite);
                if (boss.shotWarningSprite == null) Debug.LogWarning("[KingSetup] Attack effect 스프라이트 없음: " + AttackEffectSprite);
            }
            else
            {
                Debug.LogWarning("[KingSetup] Slime_King에 BossMonsterChaser가 없습니다.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, KingPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyAmmoBackground()
    {
        Sprite bullet = AssetDatabase.LoadAssetAtPath<Sprite>(BulletSprite);
        TopDownPlayerController pc = Object.FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);

        if (bullet == null || pc == null)
        {
            Debug.LogWarning("[KingSetup] 탄환 배경 적용 실패 (BULLET 스프라이트 또는 플레이어 없음)");
            return;
        }

        Undo.RecordObject(pc, "Ammo Background");
        pc.ammoBackgroundSprite = bullet;
        EditorUtility.SetDirty(pc);
        EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
    }
}
#endif
