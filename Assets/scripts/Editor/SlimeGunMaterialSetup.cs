#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Slime Gun > Apply Texture
/// Slime gun_text 텍스처로 URP Lit 머티리얼을 만들고 씬의 "Slime gun" 모델에 적용합니다.
/// </summary>
public static class SlimeGunMaterialSetup
{
    const string Folder = "Assets/(1013) New Data/Character/Weapon/Slime gun/Slime gun_text/";
    const string MatPath = Folder + "SlimeGun_Mat.mat";

    [MenuItem("Tools/Slime Gun/Apply Texture")]
    public static void Apply()
    {
        // 노멀맵 임포트 타입 보정
        TextureImporter normalImporter = AssetImporter.GetAtPath(Folder + "DefaultMaterial_Normal.png") as TextureImporter;
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "DefaultMaterial_BaseColor.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "DefaultMaterial_Normal.png");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
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
        mat.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        int count = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "Slime gun" || !t.gameObject.scene.IsValid()) continue;

            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(r, "Apply Slime Gun Material");
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                count++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[SlimeGun] 머티리얼 적용 완료: " + MatPath + " / Renderer " + count + "개. 씬을 저장하세요.");
    }
}
#endif
