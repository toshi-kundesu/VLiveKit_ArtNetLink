# VLiveKit ArtNetLink

Art-Net / DMX の受信と照明制御を VLiveKit から扱うための Unity package です。

## Package

- Package name: `com.toshi.vlivekit.artnetlink`
- Version: `0.1.4`
- Unity: 2022.3
- Repository: https://github.com/toshi-kundesu/VLiveKit_ArtNetLink
- Package root: `Assets/toshi.VLiveKit/ArtNetLink`

## 主な内容

- Art-Net 経由の DMX データ受信
- 照明パラメータを Unity 上で確認・制御するための基盤
- DMX チャンネル変化の記録
- 受信確認用の Editor window `ArtNet Monitor`

## インストール

Unity の `Packages/manifest.json` の `dependencies` に追加します。

```json
{
  "dependencies": {
    "com.toshi.vlivekit.artnetlink": "https://github.com/toshi-kundesu/VLiveKit_ArtNetLink.git?path=/Assets/toshi.VLiveKit/ArtNetLink#v0.1.4"
  }
}
```

VLiveKit sandbox では submodule として `Packages/VLiveKit_ArtNetLink` に配置し、`file:` 参照で読み込んでいます。

## ArtNet Monitor の使い方

Unity メニューから開きます。

`toshi > VLiveKit > Lighting > ArtNet Monitor`

### Standalone Monitor で受信チェックする

シーン上の receiver や Play Mode に依存せず、Unity Editor が Art-Net を受け取れているかだけを確認したいときに使います。

1. `ArtNet Monitor` を開きます。
2. `Host` に受信に使うローカル IP アドレスを入れます。
   - 同じ PC から送る場合は `127.0.0.1`。
   - 別 PC や照明卓から送る場合は、受信 PC の LAN アダプタの IP アドレス。
3. `Port` に Art-Net の UDP port を入れます。通常は `6454` です。
4. `Start Receiver` を押します。
5. 外部ツールや照明卓から Art-Net DMX を送ります。

受信すると universe ごとに以下が表示されます。

- `Packets`: 受信した packet 数
- `Last Received`: 最後に受信してからの経過時間
- `Length / Non Zero`: DMX payload 長と、0 以外の channel 数
- `Sequence / Physical`: Art-Net packet の metadata
- Channel preview: 先頭 32 channel の値

`Standalone Monitor` は Play Mode に入らなくても使えます。`Stop Receiver` を押すか window を閉じると、monitor が作った UDP receiver は破棄されます。UDP port が既に別の receiver に使われている場合は、そちらを止めるか、下の `Scene Receivers` 側で確認してください。

### Scene Receivers で確認する

シーン上で `VLiveArtNetReceiver` が有効になっている場合、同じ window の `Scene Receivers` に表示されます。

Play Mode 中に灯体や fixture の動作確認をしながら、以下を確認できます。

- receiver が使っている endpoint
- receiver の selected universe
- universe ごとの packet 受信状況
- channel 値が変化しているか

## 注意

- 現場ごとの DMX 配線やチャンネル設計に合わせて拡張する前提の package です。
- Editor 専用ツールは `Editor` folder に置きます。
- VLiveKit の custom editor window は Unity menu の `toshi/...` 配下に置きます。

## 依存・同梱 asset

- HDRP 14.0.8

## License

この package 独自のコードと asset は repository の `LICENSE` に従います。third-party asset を含む場合は、それぞれの license / README を確認してください。
