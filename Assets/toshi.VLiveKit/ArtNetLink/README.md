# VLiveKit ArtNetLink

Art-Net / DMX の受信と照明制御を VLiveKit から扱うための Unity package です。

## Package

- Package name: `com.toshi.vlivekit.artnetlink`
- Version: `0.0.10`
- Unity: 2022.3
- Repository: https://github.com/toshi-kundesu/VLiveKit_ArtNetLink
- Package root: `Assets/toshi.VLiveKit/ArtNetLink`

## 主な内容

- Art-Net 経由の DMX データ受信
- 照明パラメータを Unity 上で確認・制御するための基盤
- ライブ制作での照明連携を想定した拡張用 package

## 依存・同梱 asset

- HDRP 14.0.8

## インストール

Unity の `Packages/manifest.json` の `dependencies` に追加します。

```json
{
  "dependencies": {
    "com.toshi.vlivekit.artnetlink": "https://github.com/toshi-kundesu/VLiveKit_ArtNetLink.git?path=/Assets/toshi.VLiveKit/ArtNetLink#main"
  }
}
```

VLiveKit sandbox では submodule として `Packages/VLiveKit_ArtNetLink` に配置し、`file:` 参照で読み込んでいます。

## 注意

- 現場ごとの DMX 配線やチャンネル設計に合わせて拡張する前提の package です。

## License

この package 独自のコードと asset は repository の `LICENSE` に従います。third-party asset を含む場合は、それぞれの license / README を確認してください。
