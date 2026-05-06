# VLiveKit ArtNetLink

VLiveKit の一部として開発している、Art-Net / DMX 受信用の Unity package です。

## Package

- Package name: `com.toshi.vlivekit.artnetlink`
- Version: `0.1.8`
- Unity: 2022.3
- Package root: `Assets/toshi.VLiveKit/ArtNetLink`

## ArtNet Monitor

Unity メニューから開きます。

`toshi > VLiveKit > Lighting > ArtNet Monitor`

### 受信チェック

`Standalone Monitor` は、Play Mode に入らずに Art-Net の受信だけを確認するための monitor です。

1. `Host` に受信に使うローカル IP アドレスを入れます。
2. `Port` に Art-Net の UDP port を入れます。通常は `6454` です。
3. `Start Receiver` を押します。
4. 外部ツールや照明卓から Art-Net DMX を送ります。

受信すると universe ごとに `LIVE` / `STALE` / `LOST` の状態、packet 数、最後に受信してからの経過時間、DMX payload 長、0 以外の channel 数、512 channel のプレビューが表示されます。複数 universe は `Universe Packet State` で一覧できます。シーン上に `VLiveArtNetReceiver` がなくても、この window が一時的な UDP receiver を作って受信します。`Stop Receiver` を押すか window を閉じると、monitor が作った UDP receiver は破棄されます。

シーン上で `VLiveArtNetReceiver` が有効になっている場合は、同じ window の `Scene Receivers` に表示されます。Play Mode 中の fixture 確認では、receiver の endpoint、selected universe、受信 channel 値をここで確認できます。

詳細は `Assets/toshi.VLiveKit/ArtNetLink/README.md` を参照してください。
