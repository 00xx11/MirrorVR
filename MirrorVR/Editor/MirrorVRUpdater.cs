using System;
using System.IO;
using System.Net;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using Newtonsoft.Json.Linq;


namespace Mirror.VR.Editor.Updater
{
    public class MirrorVRUpdater : EditorWindow
    {
        public static MirrorVRUpdater window;

        private const string githuburl = "https://api.github.com/repos/Glitched-Cat-Studios/MirrorVR/releases/latest";
        private const string onlinejsonurl = "https://raw.githubusercontent.com/Glitched-Cat-Studios/MirrorVR/refs/heads/main/package.json";

        private static PackageJson localpackagejson;
        private static PackageJson onlinepackagejson;

        private bool fetchedFiles => localpackagejson != null && onlinepackagejson != null;


        [MenuItem("Window/MirrorVR/MirrorVR Updater _F10")]
        public static void ShowWindow()
        {
            window = GetWindow<MirrorVRUpdater>();
            window.titleContent = new GUIContent("MirrorVR Updater");
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("MirrorVR Updater", EditorStyles.boldLabel);


            if (!fetchedFiles)
            {
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUI.backgroundColor = new Color(0.01176470588f, 0.51764705882f, 0.98823529411f);
                if (GUILayout.Button("Fetch Updates", GUILayout.Width(140)))
                {
                    FetchUpdate();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUI.backgroundColor = Color.white;

                GUILayout.Space(10);
                EditorGUILayout.HelpBox("TIP: Try pressing F10 to get into this window!", MessageType.Info);
            }
            else
            {
                GUILayout.Space(20);

                if (localpackagejson.version != onlinepackagejson.version)
                {
                    EditorGUILayout.HelpBox("New update available!", MessageType.Warning);
                    GUILayout.Space(7.5f);
                    GUILayout.Label($"Current Version: {localpackagejson.version}");
                    GUILayout.Label($"New Version: {onlinepackagejson.version}");
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    GUI.backgroundColor = new Color(1f, 0f, 0f);
                    if (GUILayout.Button("Download Update", GUILayout.Width(140)))
                    {
                        DownloadLatestUpdate();
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    EditorGUILayout.HelpBox($"You are on the latest version: {localpackagejson.version}.", MessageType.Info);
                }
            }

            GUILayout.Space(20);
            GUILayout.Label("MirrorVR Updater v1.0.0", EditorStyles.centeredGreyMiniLabel);
        }

        private void FetchUpdate()
        {
            localpackagejson = JsonUtility.FromJson<PackageJson>(File.ReadAllText($"{Application.dataPath}/MirrorVR/package.json"));

            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Unity Editor");
                onlinepackagejson = JsonUtility.FromJson<PackageJson>(webClient.DownloadString(onlinejsonurl));
            }
        }

        private void DownloadLatestUpdate()
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Unity Editor");
                string json = webClient.DownloadString(githuburl);
                JObject releaseData = JObject.Parse(json);

                string downloadUrl = string.Empty;

                foreach (var asset in releaseData["assets"])
                {
                    string assetName = asset["name"].ToString();
                    if (assetName.EndsWith(".unitypackage")) //assetName.Contains("MirrorVR") && 
                    {
                        downloadUrl = asset["browser_download_url"].ToString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Debug.LogError("No .unitypackage matching 'MirrorVR' found in the latest release.");
                    return;
                }

                string tempFilePath = Path.Combine(Application.temporaryCachePath, $"MirrorVR (v{onlinepackagejson.version}).unitypackage");
                webClient.DownloadFile(downloadUrl, tempFilePath);
                AssetDatabase.ImportPackage(tempFilePath, true);

                Debug.Log($"Successfully downloaded and imported version {onlinepackagejson.version}.");
            }
        }

        internal static PackageJson GetLocalPackageJson() => JsonUtility.FromJson<PackageJson>(File.ReadAllText($"{Application.dataPath}/MirrorVR/package.json"));

        internal static PackageJson GetOnlinePackageJson()
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Unity Editor");
                return JsonUtility.FromJson<PackageJson>(webClient.DownloadString(onlinejsonurl));

            }
        }


        [Serializable]
        public class PackageJson
        {
            public string name;
            public string version;
            public string displayName;
            public string description;
            public string unity;
            public string unityRelease;
            public string documentationUrl;
            public string changelogUrl;
            public string licensesUrl;
            public Dependencies dependencies;
            public List<string> keywords;
            public Author author;

            [Serializable]
            public class Dependencies
            {
                public string com_unity_nuget_newtonsoft_json;
            }

            [Serializable]
            public class Author
            {
                public string name;
                public string email;
            }
        }
    }
}
