#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#endif

/// Adds Mirror define.
private static void AddDefine(string value)
{
    BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone;
#pragma warning disable CS0618 // Type or member is obsolete
    PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out string defines);
#pragma warning restore CS0618 // Type or member is obsolete

    string[] defineArr = defines.Split(';');
    if (!defineArr.Contains(value))
    {
        defineArr = defineArr.Concat(new[] { value }).ToArray();
        defines = string.Join(";", defineArr);
#pragma warning disable CS0618 // Type or member is obsolete
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defines);
#pragma warning restore CS0618 // Type or member is obsolete
    }
} 