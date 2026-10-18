#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 빌드할 때 프로젝트 폴더의 GameControl.csv 를 실행 파일 옆으로 복사합니다.
/// (빌드 후 그 파일을 엑셀로 수정하면 게임에 반영됩니다)
/// </summary>
public class GameControlBuildCopy : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string source = Path.Combine(Path.GetDirectoryName(Application.dataPath), GameControl.FileName);

        if (!File.Exists(source))
        {
            Debug.LogWarning("[GameControl] 복사할 파일이 없습니다: " + source);
            return;
        }

        string output = report.summary.outputPath;
        string folder = Directory.Exists(output) ? output : Path.GetDirectoryName(output);

        // macOS .app 은 번들 옆에 둡니다.
        if (folder != null && folder.EndsWith(".app"))
        {
            folder = Path.GetDirectoryName(folder);
        }

        string dest = Path.Combine(folder ?? "", GameControl.FileName);
        File.Copy(source, dest, true);
        Debug.Log("[GameControl] 빌드 폴더로 복사: " + dest);
    }

    [MenuItem("Tools/Game Control/Open GameControl.csv Folder")]
    static void OpenFolder()
    {
        EditorUtility.RevealInFinder(Path.Combine(Path.GetDirectoryName(Application.dataPath), GameControl.FileName));
    }
}
#endif
