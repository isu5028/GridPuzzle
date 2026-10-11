# GRIDCRYPT

Unity で開発しているグリッド型パズルゲームです。

## 開発環境

- Unity Hub
- Unity Editor **6000.3.24f1**（`ProjectSettings/ProjectVersion.txt` に記載）
- Git

Unity Hub で上記バージョンをインストールし、必要に応じて対象プラットフォームの Build Support を追加してください。リポジトリを clone した後、Unity Hub の **Add > Add project from disk** からこのフォルダを選択して開きます。

## パッケージとライブラリ

Unity Package Manager が `Packages/manifest.json` と `Packages/packages-lock.json` に基づいてパッケージを復元します。通常、UniTask、UniRx、DOTween、Addressables、Localization、URP などを個別にインストールする必要はありません。初回起動時は Git URL から取得するパッケージを含め、インターネット接続が必要です。

Steamworks.NET は `Assets/com.rlabrecque.steamworks.net` に同梱されています。別途インポートする必要はありません。Steam 機能を実際に動かす場合は Steam クライアントへのログインと対象 App ID の利用権が必要です。Discord Rich Presence は Discord デスクトップアプリが起動している場合に利用できます。

## 別 PC でのセットアップ

```powershell
git clone https://github.com/isu5028/GridPuzzle.git
```

Unity Hub から clone した `GridPuzzle` フォルダを開きます。初回は Unity がパッケージの取得と `Library` の再生成を行うため、完了まで時間がかかることがあります。`Library`、`Temp`、`UserSettings` は生成物・ユーザー別設定なのでリポジトリには含めていません。

## Steam での実行

Unity Editor での通常の起動や Steam 外からの実行では Steam API が初期化できないことがあります。Steam 機能の確認には Steam クライアントを起動し、適切な App ID と `steam_appid.txt` を実行ファイルと同じ場所に用意してください。App ID ファイルはローカル開発用です。配布ビルドには含めないでください。
