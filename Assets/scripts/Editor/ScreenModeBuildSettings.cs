#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 빌드 직전에 플레이어 설정을 1920x1080 고정으로 맞춥니다.
/// (창 크기 조절 불가, 기본 해상도 1920x1080)
/// </summary>
public class ScreenModeBuildSettings : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Apply();
    }

    [MenuItem("Tools/Game Control/Apply 1920x1080 Player Settings")]
    public static void Apply()
    {
        PlayerSettings.defaultScreenWidth = ScreenModeSelector.Width;
        PlayerSettings.defaultScreenHeight = ScreenModeSelector.Height;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.allowFullscreenSwitch = true;

        Debug.Log("[ScreenMode] 플레이어 설정: 1920x1080 / 창 크기 조절 불가");
    }
}
#endif
