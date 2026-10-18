#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Player > Rebuild Model (Pc_ani)
/// 1. Pc_ani.fbx 임포트 설정: 루프 클립 설정, 머티리얼(텍스처) 연결
/// 2. 새 애니메이터 컨트롤러 Player_Pc.controller 생성
///    WeaponMode 0 = Pistol, 1 = Rifle, 2 = Slime Gun
/// 3. 씬의 Player 아래에 새 모델 배치, 기존 모델/Animator 비활성화, 컨트롤러 연결
/// 여러 번 실행해도 안전합니다.
/// </summary>
public static class PlayerModelSetup
{
    const string PcFolder = "Assets/(1013) New Data/Character/Pc/";
    const string FbxPath = PcFolder + "Pc_ani/Pc_ani.fbx";
    const string TexFolder = PcFolder + "Pc_text/";
    const string ControllerPath = PcFolder + "Pc_ani/Player_Pc.controller";
    const string ModelName = "PC_Model";

    static readonly string[] OldVisualNames = { "Armature", "base_gin", "ear", "eye", "hat", "tail" };

    // FBX 머티리얼 이름 -> 텍스처 접두어
    static readonly (string material, string texPrefix, string matAsset)[] MaterialMap =
    {
        ("d40734b2_4f2a_4252_90ed_3e52c90c40b2", "d40734b2_4f2a_4252_90ed_3e52c90c40b2", "Pc_Body_Mat"),
        ("uv t", "uv t", "Pc_Detail_Mat"),
    };

    [MenuItem("Tools/Player/Rebuild Model (Pc_ani)")]
    public static void Rebuild()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Player Model", "Play 모드를 끈 뒤 실행하세요.", "OK");
            return;
        }

        SetupImporter();
        EnsureAllTakesImported();
        AnimatorController controller = BuildController();
        SetupScene(controller);
    }

    [MenuItem("Tools/Player/Rebuild Animator Only")]
    public static void RebuildAnimatorOnly()
    {
        EnsureAllTakesImported();
        AnimatorController controller = BuildController();

        TopDownPlayerController player = Object.FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);
        if (player != null && player.characterAnimator != null)
        {
            Undo.RecordObject(player.characterAnimator, "Assign Controller");
            player.characterAnimator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(player.characterAnimator);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        Debug.Log("[PlayerModel] 애니메이터 재구성 완료: " + ControllerPath);
    }

    /// <summary>
    /// FBX 안의 모든 애니메이션(Take)이 클립으로 임포트되도록 보장합니다.
    /// 클립 목록에서 빠진 Take(예: Die)는 추가하고, 같은 Take를 중복으로 쓰는 "(1)" 복제 클립은 제거합니다.
    /// </summary>
    static void EnsureAllTakesImported()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) return;

        var clips = (importer.clipAnimations != null && importer.clipAnimations.Length > 0
            ? importer.clipAnimations
            : importer.defaultClipAnimations).ToList();

        // 중복 복제 클립 제거 (이름이 " (1)"로 끝나고 같은 Take를 쓰는 클립이 이미 있는 경우)
        clips = clips.Where(c => !(c.name.EndsWith(" (1)") &&
                                   clips.Any(o => o != c && o.takeName == c.takeName && !o.name.EndsWith(" (1)"))))
                     .ToList();

        bool changed = false;

        foreach (ModelImporterClipAnimation def in importer.defaultClipAnimations)
        {
            if (clips.Any(c => c.takeName == def.takeName))
            {
                continue;
            }

            def.loopTime = !def.name.ToLower().Contains("die");
            clips.Add(def);
            changed = true;
            Debug.Log("[PlayerModel] 누락된 클립 추가: " + def.name);
        }

        foreach (var c in clips)
        {
            if (c.name.ToLower().Contains("die") && c.loopTime)
            {
                c.loopTime = false;
                changed = true;
            }
        }

        if (changed || clips.Count != importer.clipAnimations.Length)
        {
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }
    }

    // ---------------------------------------------------------------- 1. 임포트
    static void SetupImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;

        if (importer == null)
        {
            Debug.LogError("[PlayerModel] FBX를 찾지 못했습니다: " + FbxPath);
            return;
        }

        // 머티리얼: 텍스처를 입힌 URP Lit 머티리얼로 교체
        foreach (var map in MaterialMap)
        {
            Material mat = CreateOrUpdateMaterial(map.texPrefix, map.matAsset);
            if (mat != null)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), map.material), mat);
            }
        }

        // 클립 루프 설정 (Die만 1회 재생)
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        foreach (var clip in clips)
        {
            bool isDie = clip.name.ToLower().Contains("die");
            clip.loopTime = !isDie;
            clip.loopPose = false;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static Material CreateOrUpdateMaterial(string texPrefix, string matAsset)
    {
        string matPath = PcFolder + "Pc_text/" + matAsset + ".mat";
        string normalPath = TexFolder + texPrefix + "_Normal.png";

        TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + texPrefix + "_BaseColor.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        if (baseColor == null)
        {
            Debug.LogWarning("[PlayerModel] BaseColor 텍스처 없음: " + texPrefix);
            return null;
        }

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

        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ---------------------------------------------------------------- 2. 컨트롤러
    static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string shortName)
    {
        // "Armature|Rifle" 처럼 앞에 접두어가 붙어 있으므로 '|' 뒤 이름으로 찾습니다.
        foreach (var kv in clips)
        {
            string n = kv.Key.Contains("|") ? kv.Key.Substring(kv.Key.LastIndexOf('|') + 1) : kv.Key;
            if (n == shortName) return kv.Value;
        }

        Debug.LogWarning("[PlayerModel] 클립 없음: " + shortName);
        return null;
    }

    static AnimatorController BuildController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name, c => c);

        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Shoot", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("WeaponMode", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        AnimatorState die = sm.AddState("Die", new Vector3(650, -150, 0));
        die.motion = Clip(clips, "Die");

        // 서 있을 때 = 현재 무기 자세 (Pistol / Rifle / Slime_Luncher)
        // 이동할 때 = 무기별 이동 (Pisto_Move / Rifle_Move / Luncher_Move)
        string[] idleClips = { "Pistol", "Rifle", "Slime_Luncher" };
        string[] moveClips = { "Pisto_Move", "Rifle_Move", "Luncher_Move" };
        string[] modeNames = { "Pistol", "Rifle", "Slime" };

        var idles = new AnimatorState[3];
        var moves = new AnimatorState[3];

        for (int m = 0; m < 3; m++)
        {
            idles[m] = sm.AddState("Idle_" + modeNames[m], new Vector3(50 + m * 250, 0, 0));
            idles[m].motion = Clip(clips, idleClips[m]);

            moves[m] = sm.AddState("Move_" + modeNames[m], new Vector3(50 + m * 250, 200, 0));
            moves[m].motion = Clip(clips, moveClips[m]);
        }

        sm.defaultState = idles[0];

        for (int m = 0; m < 3; m++)
        {
            var t = Trans(idles[m], moves[m]);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            t = Trans(moves[m], idles[m]);
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // 무기 교체
            for (int k = 0; k < 3; k++)
            {
                if (k == m) continue;

                t = Trans(idles[m], idles[k]);
                t.AddCondition(AnimatorConditionMode.Equals, k, "WeaponMode");

                t = Trans(moves[m], moves[k]);
                t.AddCondition(AnimatorConditionMode.Equals, k, "WeaponMode");
            }
        }

        // Any State -> Die
        AnimatorStateTransition anyDie = sm.AddAnyStateTransition(die);
        anyDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        anyDie.hasExitTime = false;
        anyDie.duration = 0.1f;
        anyDie.canTransitionToSelf = false;

        // Die 상태는 마지막 프레임을 유지합니다. (Exit 전환 없음)
        // 부활 시 TopDownPlayerController.ResetAfterRespawn()에서 Animator를 초기화합니다.

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        return ac;
    }

    static AnimatorStateTransition Trans(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        return t;
    }

    // ---------------------------------------------------------------- 3. 씬
    static void SetupScene(AnimatorController controller)
    {
        TopDownPlayerController player = Object.FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            Debug.LogError("[PlayerModel] TopDownPlayerController를 찾지 못했습니다.");
            return;
        }

        Transform root = player.transform;

        // 기존 모델 크기(높이) 측정 후 비활성화
        float oldHeight = 0f;
        foreach (string n in OldVisualNames)
        {
            Transform old = root.Find(n);
            if (old == null) continue;

            foreach (Renderer r in old.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled && r.gameObject.activeInHierarchy)
                    oldHeight = Mathf.Max(oldHeight, r.bounds.size.y);
            }

            Undo.RecordObject(old.gameObject, "Hide Old Model");
            old.gameObject.SetActive(false);
        }

        Animator oldAnimator = root.GetComponent<Animator>();
        if (oldAnimator != null)
        {
            Undo.RecordObject(oldAnimator, "Disable Old Animator");
            oldAnimator.enabled = false;
        }

        // 새 모델 배치
        Transform existing = root.Find(ModelName);
        float previousScale = existing != null ? existing.localScale.x : -1f;
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(model, "Create Player Model");
        model.name = ModelName;
        model.transform.SetParent(root, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // 기존 모델과 키를 맞춥니다.
        float newHeight = 0f;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            newHeight = Mathf.Max(newHeight, r.bounds.size.y);

        if (previousScale > 0f)
        {
            // 다시 실행한 경우 이전 크기 유지 (기존 모델은 이미 꺼져 있어 측정 불가)
            model.transform.localScale = Vector3.one * previousScale;
        }
        else if (oldHeight > 0.01f && newHeight > 0.01f)
        {
            model.transform.localScale = Vector3.one * (oldHeight / newHeight);
        }

        // 레이어를 Player와 맞춤 (조명/미니맵 레이어 설정 유지)
        int layer = root.gameObject.layer;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;

        // Animator
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Undo.RecordObject(player, "Link Character Animator");
        player.characterAnimator = animator;
        EditorUtility.SetDirty(player);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log("[PlayerModel] 완료: " + ModelName + " 배치 (scale " + model.transform.localScale.x.ToString("F3") +
                  "), 컨트롤러 " + ControllerPath + ". 기존 모델은 비활성화했습니다. 씬을 저장하세요.");
    }
}
#endif
