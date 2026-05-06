# VLiveKit ArtNetLink

VLiveKit の一部として開発している、Art-Net / DMX 受信用の Unity package です。

## Package

- Package name: `com.toshi.vlivekit.artnetlink`
- Version: `0.1.2`
- Unity: 2022.3
- Package root: `Assets/toshi.VLiveKit/ArtNetLink`

## ArtNet Monitor

Unity メニューから開きます。

`toshi > VLiveKit > Lighting > ArtNet Monitor`

### 受信チェック

`Standalone Monitor` は、Play Mode に入らずに Art-Net の受信だけを確認するための monitor です。

1. `Host` に受信に使うローカル IP アドレスを入れます。
2. `Port` に Art-Net の UDP port を入れます。通常は `6454` です。
3. `Start` を押します。
4. 外部ツールや照明卓から Art-Net DMX を送ります。

受信すると universe ごとに packet 数、最後に受信してからの経過時間、DMX payload 長、0 以外の channel 数、先頭 32 channel の値が表示されます。

シーン上で `VLiveArtNetReceiver` が有効になっている場合は、同じ window の `Scene Receivers` に表示されます。Play Mode 中の fixture 確認では、receiver の endpoint、selected universe、受信 channel 値をここで確認できます。

詳細は `Assets/toshi.VLiveKit/ArtNetLink/README.md` を参照してください。
