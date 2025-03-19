using UnityEditor;
using UnityEngine;

namespace Mirror.VR.Editor.Updater
{
    [InitializeOnLoad]
    public class MirrorVRUpdatePrompt
    {
        static MirrorVRUpdatePrompt()
        {
            MirrorVRUpdater.PackageJson localpackagejson = MirrorVRUpdater.GetLocalPackageJson();
            MirrorVRUpdater.PackageJson onlinepackagejson = MirrorVRUpdater.GetOnlinePackageJson();

            if (localpackagejson.version != onlinepackagejson.version)
            {
                if (!EditorPrefs.GetBool("HideMirrorVRUpdatePrompt", false))
                {
                    if (!SessionState.GetBool("MirrorVRUpdatePrompt", false))
                    {
                        int option = EditorUtility.DisplayDialogComplex(
                            "MirrorVR Updater",
                            "A new update for MirrorVR is available. Would you like to update?",
                            "Yes",
                            "Nope",
                            "Don't show again"
                        );

                        switch (option)
                        {
                            case 0:
                                EditorApplication.ExecuteMenuItem("Window/MirrorVR/MirrorVR Updater");
                                break;
                            case 2:
                                EditorPrefs.SetBool("HideMirrorVRUpdatePrompt", true);
                                break;

                        }

                        SessionState.SetBool("MirrorVRUpdatePrompt", true);
                    }
                }
            }
        }
    }
}
