## 概要

VLiveKitの一部として開発している、  
Art-Netベースの照明制御を行うためのUnity向けパッケージです。

Unity上から照明の制御・記録を行うことを目的とし、  
ライブ制作での使用を前提に設計しています。

現在は構成の整理および再実装を進めている段階です。

---

## 機能（予定 / 一部実装）

- Art-NetによるDMXデータの受信
- Unity上での照明制御
- 照明状態の記録（レコーディング）

今後追加予定：

- Art-Net受信確認用モニタ
- デバッグ・可視化ツール
- 現場運用向け機能の拡張

---

## 開発方針

本パッケージは、実際のライブ制作で使用しながら  
継続的に改善・拡張していくことを前提としています。

---

## 依存・参考実装

### OSCJack

- Repository  
  https://github.com/keijiro/OscJack

- License  
  Unlicense

※ 本パッケージでは、主に受信処理の実装を参考にしています。

---

## ライセンス

本パッケージは **Unlicense** で公開されています。

- https://unlicense.org/

商用利用・改変・再配布など、用途に制限はありません。

```csharp
// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 20##/##/##
