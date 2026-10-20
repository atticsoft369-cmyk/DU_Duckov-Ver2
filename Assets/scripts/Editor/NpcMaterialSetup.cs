#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Npc > Apply Texture
/// Npc_text 텍스처로 URP Lit 머티리얼을 만들어 Npc.fbx(모든 사용처)와 씬의 Npc에 적용합니다.
/// </summary>
public static class NpcMaterialSetup
{
    const string Root = "Assets/(1013) New Data/Character/Npc/";
    const string TexFolder = Root + "Npc_text/";
    const string Prefix = "tripo_2a7f9207_68e1_46f7_936b_6f5e8517a233_";
    const string FbxPath = Root + "Npc.fbx";
    const string MatPath = TexFolder + "Npc_Mat.mat";

    [MenuItem("Tools/Npc/Apply Texture")]
    public static void Apply()
    {
        string normalPath = TexFolder + Prefix + "Normal.png";
        TextureImporter ni = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (ni != null && ni.textureType != TextureImporterType.NormalMap)
        {
            ni.textureType = TextureImporterType.NormalMap;
            ni.SaveAndReimport();
        }

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + Prefix + "BaseColor.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        if (baseColor == null)
        {
            Debug.LogError("[Npc] BaseColor 텍스처를 찾지 못했습니다: " + TexFolder);
            return;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        mat.SetTexture("_BaseMap", baseColor);
        mat.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 1) FBX 임포트 머티리얼을 새 머티리얼로 교체 (Npc를 쓰는 모든 곳에 적용)
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        int remapped = 0;
        if (importer != null)
        {
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Material>())
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
                remapped++;
            }

            if (remapped > 0)
            {
                importer.SaveAndReimport();
            }
        }

        // 2) 씬의 Npc 렌더러에도 직접 적용 (임포트 머티리얼이 없거나 씬에서 바꾼 경우 대비)
        int count = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "Npc" || !t.gameObject.scene.IsValid()) continue;

            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                // 말풍선(TextMeshPro) 등은 제외
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.GetComponent<TMPro.TMP_Text>() != null) continue;

                Undo.RecordObject(r, "Apply Npc Material");
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                count++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[Npc] 머티리얼 적용 완료: {MatPath} / FBX 교체 {remapped}개, 씬 Renderer {count}개. 씬을 저장하세요(Ctrl+S).");
    }
}
#endif
