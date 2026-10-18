#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Monster > Setup Armor & Spin Slime
/// - Slime_Armor 프리팹 -> Armor slime 모델 (move / attack / hit / drop / die)
///   1차 사망(Drop) 후 방패 숨김, HP 회복 -> 2차 사망(Die)
/// - Slime_Dash 프리팹  -> Spin slime 모델 (move / attack=move / hit / die)
/// - Slime_Angel 프리팹 -> Angel slime 모델 (move / attack / hit / die)
///   프리팹이 없으면 Slime_Normal을 복제해서 만듭니다.
/// 기존 모델은 렌더러를 끄고, 새 모델을 자식(Model)으로 넣어 전용 애니메이터를 연결합니다.
/// 여러 번 실행해도 안전합니다.
/// </summary>
public static class MonsterModelSetup
{
    const string MonsterRoot = "Assets/(1013) New Data/Monster/";
    const string PrefabRoot = "Assets/Resources/prefabs/";
    const string ModelChildName = "Model";

    class MonsterDef
    {
        public string prefab;      // Resources/prefabs/{prefab}.prefab
        public string folder;      // Monster/{folder}
        public string prefix;      // {prefix}_move.fbx ...
        public string moveClip;
        public string attackClip;
        public string hitClip;
        public string dieClip;
        public string dropClip;    // null 이면 없음
        public string copyFrom;    // 프리팹이 없을 때 복제할 원본 프리팹
        public float hitDelayRatio; // 공격 클립 길이 대비 타격 시점 (0 = 즉시)
    }

    static readonly MonsterDef[] Defs =
    {
        new MonsterDef
        {
            prefab = "Slime_Armor", folder = "Armor", prefix = "Armor",
            moveClip = "move", attackClip = "attack", hitClip = "hit", dieClip = "die", dropClip = "drop"
        },
        new MonsterDef
        {
            prefab = "Slime_Dash", folder = "Spin", prefix = "Spin",
            moveClip = "move", attackClip = "move", hitClip = "hit", dieClip = "die", dropClip = null
        },
        // Angel: 날개짓 비행(move 0.8s 루프), attack 1.0s = 뒤로 0.47s 힘을 모았다가 0.6s에 앞으로 돌진
        new MonsterDef
        {
            prefab = "Slime_Angel", folder = "Angel", prefix = "Angel",
            moveClip = "move", attackClip = "attack", hitClip = "hit", dieClip = "die", dropClip = null,
            copyFrom = "Slime_Normal", hitDelayRatio = 0.58f
        },
    };

    [MenuItem("Tools/Monster/Setup Armor & Spin Slime")]
    public static void SetupAll()
    {
        foreach (MonsterDef def in Defs)
        {
            Setup(def);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[MonsterSetup] 완료: Slime_Armor(Armor slime), Slime_Dash(Spin slime), Slime_Angel(Angel slime)");
    }

    static string AnimFbx(MonsterDef d, string clip) =>
        MonsterRoot + d.folder + "/" + d.folder + "_ani/" + d.prefix + "_" + clip + ".fbx";

    static void Setup(MonsterDef d)
    {
        // ---------- 1. 클립 루프 설정 ----------
        var clipNames = new List<string> { d.moveClip, d.attackClip, d.hitClip, d.dieClip };
        if (d.dropClip != null) clipNames.Add(d.dropClip);

        foreach (string c in clipNames.Distinct())
        {
            SetLoop(AnimFbx(d, c), c == d.moveClip);
        }

        // ---------- 2. 머티리얼 ----------
        Material mat = CreateMaterial(d);

        // ---------- 3. 애니메이터 컨트롤러 ----------
        AnimatorController controller = BuildController(d);

        // ---------- 4. 프리팹 ----------
        string prefabPath = PrefabRoot + d.prefab + ".prefab";

        if (!string.IsNullOrEmpty(d.copyFrom) &&
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(PrefabRoot + d.copyFrom + ".prefab", prefabPath))
            {
                Debug.LogError("[MonsterSetup] 프리팹 복제 실패: " + d.copyFrom + " -> " + d.prefab);
                return;
            }

            AssetDatabase.ImportAsset(prefabPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            Debug.LogError("[MonsterSetup] 프리팹 없음: " + prefabPath);
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

            // 기존 모델 높이 측정 후 렌더러 끄기 (미니맵 점은 유지)
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

            // 새 모델 (애니메이션 경로가 맞도록 move FBX의 모델을 사용)
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AnimFbx(d, d.moveClip));
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

            int layer = root.layer;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            MonsterChaser chaser = root.GetComponent<MonsterChaser>();
            if (chaser != null)
            {
                chaser.modelAnimator = animator;
                chaser.hasArmorPhase = d.dropClip != null;
                chaser.dropParameterName = "Drop";

                if (d.dropClip != null)
                {
                    AnimationClip drop = LoadClip(AnimFbx(d, d.dropClip));
                    if (drop != null)
                    {
                        chaser.dropAnimationDuration = drop.length;
                    }
                }

                AnimationClip attackClip = LoadClip(AnimFbx(d, d.attackClip));
                if (attackClip != null && d.hitDelayRatio > 0f)
                {
                    chaser.attackHitDelay = attackClip.length * d.hitDelayRatio;
                    chaser.attackInterval = Mathf.Max(chaser.attackInterval, attackClip.length + 0.2f);
                }

                AnimationClip die = LoadClip(AnimFbx(d, d.dieClip));
                if (die != null)
                {
                    chaser.deathAnimationDuration = Mathf.Max(0.3f, die.length);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetLoop(string fbxPath, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[MonsterSetup] FBX 없음: " + fbxPath);
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

    static AnimatorController BuildController(MonsterDef d)
    {
        string path = MonsterRoot + d.folder + "/" + d.folder + "_ani/" + d.prefix + "_Slime.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        if (d.dropClip != null) ac.AddParameter("Drop", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        AnimatorState move = sm.AddState("Move", new Vector3(300, 0, 0));
        move.motion = LoadClip(AnimFbx(d, d.moveClip));
        sm.defaultState = move;

        AnimatorState attack = sm.AddState("Attack", new Vector3(300, 150, 0));
        attack.motion = LoadClip(AnimFbx(d, d.attackClip));

        AnimatorState hit = sm.AddState("Hit", new Vector3(550, 150, 0));
        hit.motion = LoadClip(AnimFbx(d, d.hitClip));

        AnimatorState die = sm.AddState("Die", new Vector3(550, -150, 0));
        die.motion = LoadClip(AnimFbx(d, d.dieClip));

        // 공격 / 피격: 재생 후 Move로 복귀
        AddAny(sm, attack, "Attack", 0.05f);
        AddAny(sm, hit, "Hit", 0.05f);
        BackToMove(attack, move);
        BackToMove(hit, move);

        if (d.dropClip != null)
        {
            AnimatorState drop = sm.AddState("Drop", new Vector3(800, 0, 0));
            drop.motion = LoadClip(AnimFbx(d, d.dropClip));
            AddAny(sm, drop, "Drop", 0.05f);
            BackToMove(drop, move);
        }

        // 사망: 마지막 프레임 유지
        AnimatorStateTransition anyDie = AddAny(sm, die, "Die", 0.05f);
        anyDie.canTransitionToSelf = false;

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static AnimatorStateTransition AddAny(AnimatorStateMachine sm, AnimatorState to, string trigger, float duration)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = false;
        t.duration = duration;
        t.canTransitionToSelf = true;
        return t;
    }

    static void BackToMove(AnimatorState from, AnimatorState move)
    {
        AnimatorStateTransition t = from.AddTransition(move);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0.1f;
    }

    static Material CreateMaterial(MonsterDef d)
    {
        string texFolder = MonsterRoot + d.folder + "/" + d.folder + "_text";
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
            Debug.LogWarning("[MonsterSetup] BaseColor 텍스처 없음: " + texFolder);
            return null;
        }

        string matPath = texFolder + "/" + d.prefix + "_Slime_Mat.mat";
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
        mat.SetFloat("_Smoothness", 0.4f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif
