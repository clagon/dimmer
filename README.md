# Dimmer

タスクバーに常駐し、モニターの明るさ・入力ソースなどを操作する Windows アプリ。用語は [CONTEXT.md](CONTEXT.md) を参照。

## インストール

自己署名の証明書で署名しているため、最初に証明書を信頼済みとして登録する。

1. [Releases](https://github.com/clagon/dimmer/releases) から `Dimmer_x64.msix` と `Dimmer.cer` をダウンロードする
2. 管理者の PowerShell で、証明書を「信頼されたユーザー」に登録する（最初の 1 回だけ）

   ```powershell
   Import-Certificate -FilePath .\Dimmer.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

3. `Dimmer_x64.msix` をダブルクリックしてインストールする（または `Add-AppxPackage .\Dimmer_x64.msix`）

インストール直後からサインイン時に自動で起動する。自動起動は設定画面の「全般」か、Windows の「設定 → アプリ → スタートアップ」で切り替えられる。

## 使い方

- トレイアイコンの左クリック（または右クリックメニューの「設定」）で設定画面を開く
- 右クリックメニューから Profile を Apply する
- Profile・Profile Cycle・Adjustment に Shortcut を割り当てると、どこからでも実行できる
- 設定は `%LOCALAPPDATA%\Packages\Dimmer_qwz61hn511ep6\LocalState\settings.json` に保存する。手で書き換えた場合は再起動で反映する

## 開発

### 必要なもの

- Windows 10 1809 以降 / Windows 11
- .NET 10 SDK（`winget install Microsoft.DotNet.SDK.10`）
- 開発者モード（設定 → システム → 開発者向け）。署名なしのパッケージを登録するために必要

.NET ランタイムと Windows App SDK ランタイムはアプリに同梱する（`SelfContained` / `WindowsAppSDKSelfContained`）ため、実行する PC に別途入れる必要はない。

### WSL からのビルドと実行

リポジトリは Windows 側のパス（例: `C:\src\display-ctrler` = `/mnt/c/src/display-ctrler`）に置く。WSL 側のパス（`\\wsl.localhost\...`）にあるビルド成果物は `Add-AppxPackage -Register` で登録できない。

```bash
# ビルド
dotnet.exe build Dimmer.slnx

# テスト
dotnet.exe test Dimmer.slnx

# パッケージを登録して起動（ビルドのたびに登録し直す）
powershell.exe -NoProfile -Command "Add-AppxPackage -Register 'C:\src\display-ctrler\src\Dimmer\bin\Debug\net10.0-windows10.0.19041.0\win-x64\AppxManifest.xml' -ForceApplicationShutdown"
explorer.exe 'shell:AppsFolder\Dimmer_qwz61hn511ep6!App'

# 登録を解除
powershell.exe -NoProfile -Command "Get-AppxPackage Dimmer | Remove-AppxPackage"
```

ARM64 向けには `-p:Platform=ARM64` を付けてビルドし、`win-arm64` 側の `AppxManifest.xml` を登録する。

### ログ

不具合調査用のログを `%LOCALAPPDATA%\Packages\Dimmer_qwz61hn511ep6\LocalState\dimmer.log` に書き出す。

## リリース

`v` で始まるタグ（例: `v0.1.0`）を push すると、GitHub Actions（`.github/workflows/release.yml`）が MSIX をビルドして署名し、GitHub Releases に上げる。パッケージのバージョンはタグから決める。

署名用の証明書は Secrets に置く。

- `SIGNING_CERT_BASE64`: 証明書（.pfx）を Base64 にしたもの。Subject は `Package.appxmanifest` の Publisher（`CN=clagon`）と同じにする
- `SIGNING_CERT_PASSWORD`: .pfx のパスワード

## 既知の制限

- シリアル番号を EDID に持たない同じ型番の Monitor が 2 台あると、ケーブルを差し替えたときに取り違える（シリアル番号が無い Monitor は接続パスで特定しているため）。検知はしない
- 排他フルスクリーンのゲームの上には Overlay を表示できない
- キーボードのユーティリティ（Logicool G HUB など）が横取りするキーは、Shortcut に割り当てても届かないことがある
