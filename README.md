# GRIDCRYPT

Unity で開発しているグリッド型パズルゲームです。

## 開発環境

- Unity Hub
- Unity Editor **6000.3.24f1**（`ProjectSettings/ProjectVersion.txt` に記載）
- Git

## パッケージとライブラリ

Unity Package Manager が `Packages/manifest.json` と `Packages/packages-lock.json` に基づいてパッケージを復元します。通常、UniTask、UniRx、DOTween、Addressables、Localization、URP などを個別にインストールする必要はありません。初回起動時は Git URL から取得するパッケージを含め、インターネット接続が必要です。

Steamworks.NET は `Assets/com.rlabrecque.steamworks.net` に同梱されています。別途インポートする必要はありません。Steam 機能を実際に動かす場合は Steam クライアントへのログインと対象 App ID の利用権が必要です。
