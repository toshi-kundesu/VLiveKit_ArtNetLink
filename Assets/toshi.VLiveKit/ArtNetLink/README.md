# VLiveKit ArtNetLink

Art-Net / DMX の受信と照明制御を VLiveKit から扱うための Unity package です。

## Package

- Package name: `com.toshi.vlivekit.artnetlink`
- Version: `0.1.17`
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
    "com.toshi.vlivekit.artnetlink": "https://github.com/toshi-kundesu/VLiveKit_ArtNetLink.git?path=/Assets/toshi.VLiveKit/ArtNetLink#v0.1.17"
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

- `LIVE` / `STALE` / `LOST`: 受信状態の色付き表示
- `Packets`: 受信した packet 数
- `Last Received`: 最後に受信してからの経過時間
- `Length / Non Zero`: DMX payload 長と、0 以外の channel 数
- `Sequence / Physical`: Art-Net packet の metadata
- Channel preview: 512 channel の値

複数 universe を受けている場合は `Universe Packet State` で一覧できます。Universe 番号を押すと、その universe を `Selected Universe` に切り替えて channel bar を確認できます。

`Standalone Monitor` は Play Mode に入らなくても使えます。シーン上に `VLiveArtNetReceiver` がなくても、この window が一時的な UDP receiver を作って受信します。`Stop Receiver` を押すか window を閉じると、monitor が作った UDP receiver は破棄されます。UDP port が既に別の receiver に使われている場合は、そちらを止めるか、下の `Scene Receivers` 側で確認してください。

受信 signal が来ない状態でも、内部 receiver は短い timeout で停止要求を確認します。window を閉じたときも receiver thread の終了待ちは上限付きなので、無信号状態で editor が閉じ待ちし続けないようにしています。

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

## Samples

`ArtNetLink Simple Test` is available from the Unity Package Manager Samples
tab. Import it to open `ArtNetLink_SimpleTest.unity`, a simple Art-Net receiver
and moving light test scene for checking DMX output with `SimpleLightConsole`
and `ArtNet Monitor`.

## SimpleLightConsole

Open the lightweight Art-Net sender from:

`toshi > VLiveKit > Lighting > SimpleLightConsole`

Use it for quick fixture checks and simple channel output while setting up Art-Net lighting. Older duplicated test-signal menu entries were consolidated into this single window name.

The top status banner shows whether the window is `SENDING LIVE DESK`,
`SENDING TEST SIGNAL`, `READY TO SEND ONCE`, or `STOPPED`, including the target
endpoint and send rate. The automatic fixture pattern is inside the `Test Signal`
section of the same window.

## SimpleDimmerFixture

Add `SimpleDimmerFixture` to a GameObject when you only need a one-channel
Art-Net dimmer test. The component automatically keeps a `VLiveArtNetReceiver`
on the same GameObject and, by default, clears the receiver connection so it
listens on `127.0.0.1:6454`.

Set `Universe` and `Dimmer Address` on `SimpleDimmerFixture`. The DMX channel is
1-based, so address `1` reads channel 1 from the selected Art-Net universe. Assign
a `Light`, `HDAdditionalLightData`, or target renderers to drive light intensity
and emissive preview surfaces.

## Unity Version Compatibility

Art-Net light helpers use Unity/HDRP version gates for APIs that changed around Unity 2023.2, 2023.3, and 6000.3. Newer editors use the current `Light` and `LightUnitUtils` APIs, while older editors keep the HDRP fallback calls.

## License

この package 独自のコードと asset は repository の `LICENSE` に従います。third-party asset を含む場合は、それぞれの license / README を確認してください。
