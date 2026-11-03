# Dimmer

タスクバーに常駐し、モニターの明るさ・入力ソースなどを操作する Windows アプリ。用語は [CONTEXT.md](CONTEXT.md) を参照。

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

## 既知の制限

- シリアル番号を EDID に持たない同じ型番の Monitor が 2 台あると、ケーブルを差し替えたときに取り違える（シリアル番号が無い Monitor は接続パスで特定しているため）。検知はしない
- 排他フルスクリーンのゲームの上には Overlay を表示できない
