using UnityEditor;

namespace MiniTank.EditorTools
{
    public static class StoreShotMenu
    {
        // %#k = Ctrl+Shift+K
        [MenuItem("MiniTank/Ekran Görüntüsü Al (Play Store, 1920x1080) %#k")]
        static void Take() => StoreShot.Take();

        [MenuItem("MiniTank/Ekran Görüntüsü Klasörünü Aç")]
        static void Open()
        {
            System.IO.Directory.CreateDirectory(StoreShot.Folder);
            EditorUtility.RevealInFinder(StoreShot.Folder);
        }
    }
}
