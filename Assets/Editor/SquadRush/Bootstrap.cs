using System.IO;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace SquadRush.EditorTools
{
    /// <summary>
    /// Headless project bootstrap helpers. Both also live under the "SquadRush" menu.
    /// ImportTmpEssentials must be run in an Editor started WITHOUT -quit (it exits itself).
    /// </summary>
    public static class Bootstrap
    {
        static bool importDone;
        static double deadline;

        [MenuItem("SquadRush/Import TMP Essentials")]
        public static void ImportTmpEssentialsMenu()
        {
            string path = TMP_EditorUtility.packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(path, true);
        }

        // -executeMethod SquadRush.EditorTools.Bootstrap.ImportTmpEssentials   (no -quit)
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                Debug.Log("[Bootstrap] TMP essentials already present.");
                EditorApplication.Exit(0);
                return;
            }

            string path = TMP_EditorUtility.packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";
            if (!File.Exists(path))
            {
                Debug.LogError("[Bootstrap] TMP essentials package not found at " + path);
                EditorApplication.Exit(1);
                return;
            }

            AssetDatabase.importPackageCompleted += _ => importDone = true;
            AssetDatabase.importPackageFailed += (_, err) =>
            {
                Debug.LogError("[Bootstrap] TMP import failed: " + err);
                EditorApplication.Exit(1);
            };

            Debug.Log("[Bootstrap] Importing TMP essentials from " + path);
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += PollImport;
            AssetDatabase.ImportPackage(path, false);
        }

        static void PollImport()
        {
            if (importDone)
            {
                EditorApplication.update -= PollImport;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                bool ok = AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;
                Debug.Log("[Bootstrap] TMP import complete. Settings found: " + ok);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            else if (EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= PollImport;
                Debug.LogError("[Bootstrap] Timed out waiting for TMP import.");
                EditorApplication.Exit(2);
            }
        }
    }
}
