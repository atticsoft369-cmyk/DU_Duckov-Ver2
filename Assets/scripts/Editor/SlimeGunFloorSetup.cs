#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Slime Gun > Setup Projectile & Floor
/// - Slime gun_floor 텍스처로 머티리얼 생성 → FBX에 적용
/// - 씬의 SlimeGunController에 예고 이미지 / 발사체 머티리얼 / 장판 모델 연결
/// </summary>
public static class SlimeGunFloorSetup
{
    const string Folder = "Assets/(1013) New Data/Character/Weapon/Slime gun/Slime gun_floor/";
    const string FbxPath = Folder + "Slime gun_floor.fbx";
    const string TexFolder = Folder + "text/";
    const string MatPath = TexFolder + "SlimeGunFloor_Mat.mat";
    const string AimSpritePath = "Assets/(1013) New Data/UI/Slime gun Floor.png";

    [MenuItem("Tools/Slime Gun/Setup Projectile & Floor")]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Slime Gun", "Play 모드를 끈 뒤 실행하세요.", "OK");
            return;
        }

        string normalPath = TexFolder + "Slime gun_floor_DefaultMaterial_Normal.png";
        TextureImporter ni = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (ni != null && ni.textureType != TextureImporterType.NormalMap)
        {
            ni.textureType = TextureImporterType.NormalMap;
            ni.SaveAndReimport();
        }

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "Slime gun_floor_DefaultMaterial_BaseColor.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        if (baseColor != null) mat.SetTexture("_BaseMap", baseColor);
        mat.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.75f); // 젤리 광택

        // 양면 렌더링 (Render Face = Both) : 장판 뒷면/안쪽도 보이게
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            bool changed = false;
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Material>())
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        SlimeGunController gun = Object.FindFirstObjectByType<SlimeGunController>(FindObjectsInactive.Include);
        if (gun == null)
        {
            Debug.LogError("[SlimeGun] 씬에서 SlimeGunController를 찾지 못했습니다.");
            return;
        }

        Undo.RecordObject(gun, "Setup Slime Gun Floor");
        gun.slimeMaterial = mat;
        gun.floorModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        gun.aimIndicatorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AimSpritePath);
        EditorUtility.SetDirty(gun);
        EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);

        Debug.Log("[SlimeGun] 설정 완료: 예고 이미지 / 파란 구 발사체 / 장판 모델 + 텍스처. 씬을 저장하세요(Ctrl+S).");
    }
}
#endif
