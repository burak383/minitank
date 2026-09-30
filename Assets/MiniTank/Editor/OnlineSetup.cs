using Fusion;
using UnityEditor;
using UnityEngine;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Online maç için gereken ağ nesnesini (NetMatch prefab'ı) oluşturur.
    /// Fusion, NetworkObject içeren prefab'ları kendi prefab tablosuna otomatik ekler.
    /// </summary>
    public static class OnlineSetup
    {
        public const string Folder = "Assets/MiniTank/Resources";
        public const string PrefabPath = Folder + "/" + OnlineSession.NetMatchResource + ".prefab";

        [MenuItem("MiniTank/Online Kurulumu (Photon)", priority = 5)]
        public static void Menu()
        {
            EnsureNetMatchPrefab(true);
        }

        public static void EnsureNetMatchPrefab(bool showDialog)
        {
            if (!AssetDatabase.IsValidFolder("Assets/MiniTank")) AssetDatabase.CreateFolder("Assets", "MiniTank");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/MiniTank", "Resources");

            var go = new GameObject(OnlineSession.NetMatchResource);
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetMatch>();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            string appId = "";
            var settings = Resources.Load<ScriptableObject>("PhotonAppSettings");
            if (settings == null) appId = "\n\nUYARI: PhotonAppSettings bulunamadı. Fusion Hub'da App ID'yi girdiğinden emin ol.";

            if (showDialog)
                EditorUtility.DisplayDialog("MiniTank Online",
                    "NetMatch ağ nesnesi hazır:\n" + PrefabPath +
                    "\n\nTest: Build al, telefonda ve editörde aynı mod + arenayı seçip 'Çevrimiçi' ile başla." + appId, "Tamam");
        }
    }
}
