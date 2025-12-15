using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SubAssetRemover
{
    // Projectウィンドウで複数選択し、右クリック > "Delete SubAsset (Force)" を実行
    [MenuItem("Assets/Delete SubAsset (Force)")]
    private static void DeleteSelectedSubAssets()
    {
        // 選択されているすべてのオブジェクトを取得
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        // 削除対象（サブアセット）だけをリストアップ
        List<Object> assetsToDelete = new List<Object>();

        foreach (var obj in selectedObjects)
        {
            // メインアセット（親）は除外して、サブアセットのみ対象にする
            if (!AssetDatabase.IsMainAsset(obj))
            {
                assetsToDelete.Add(obj);
            }
        }

        if (assetsToDelete.Count == 0)
        {
            Debug.LogWarning("削除可能なサブアセットが選択されていません。（親アセットは削除されません）");
            return;
        }

        // 確認ダイアログ
        if (!EditorUtility.DisplayDialog("強制削除",
            $"{assetsToDelete.Count} 個のサブアセットを本当に削除しますか？\n（親アセットは維持されますが、元に戻せません）",
            "まとめて削除", "キャンセル"))
        {
            return;
        }

        // 一括削除実行
        foreach (var obj in assetsToDelete)
        {
            Object.DestroyImmediate(obj, true);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{assetsToDelete.Count} 個のサブアセットを削除しました。");
    }
}