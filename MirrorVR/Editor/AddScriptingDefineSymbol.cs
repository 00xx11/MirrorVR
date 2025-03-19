using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror.VR.Editor
{
    public static class AddScriptingDefineSymbol
    {
        /// <summary>
        /// An edited version of Mirror's PreprocesserDefine script to add the MirrorVR Scripting Define Symbol.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void AddDefineSymbols()
        {
            #region Scripting Define Symbols
#if UNITY_2021_2_OR_NEWER
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
            // Deprecated in Unity 2023.1
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            HashSet<string> defines = new HashSet<string>(currentDefines.Split(';'))
            {
                "MIRROR_VR",
                "MIRROR_VR_EA",
                "MIRROR_VR_EA_OR_LATER",
            };

            // only touch PlayerSettings if we actually modified it,
            // otherwise it shows up as changed in git each time.
            string newDefines = string.Join(";", defines);
            if (newDefines != currentDefines)
            {
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), newDefines);
#else
                // Deprecated in Unity 2023.1
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
#endif
            }
            #endregion

            #region Logging

            if (!SessionState.GetBool("MirrorVR Startup Log", false))
            {
                Debug.Log("<color=grey>[MirrorVR]</color> Thanks for using MirrorVR! | Discord: <u>https://discord.gg/6KCH9xvGUE</u> | GitHub: <u>https://github.com/MirrorVR/MirrorVR</u>");
                SessionState.SetBool("MirrorVR Startup Log", true);
            }

            #endregion
        }
    }
}
