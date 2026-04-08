# CLAUDE.md

## プロジェクト概要

DiscordBot-Noteworthy — C# / .NET で構築する Discord Bot。

## ビルド・実行

```bash
dotnet build
dotnet run --project DiscordBot-Noteworthy
dotnet test
```

## コーディング規約（Microsoft .NET 準拠）

### 命名規則

| 対象 | スタイル | 例 |
|------|----------|-----|
| クラス・構造体・レコード | PascalCase | `UserService` |
| インターフェース | `I` + PascalCase | `IUserRepository` |
| メソッド | PascalCase | `GetUserAsync` |
| public / protected プロパティ | PascalCase | `DisplayName` |
| public / protected フィールド | PascalCase | `MaxRetryCount` |
| private / internal フィールド | `_camelCase` | `_logger` |
| ローカル変数・引数 | camelCase | `userName` |
| 定数 | PascalCase | `DefaultTimeout` |
| 型パラメータ | `T` + PascalCase | `TEntity` |
| 非同期メソッド | 末尾に `Async` | `SendMessageAsync` |
| 名前空間 | PascalCase（`.`区切り） | `DiscordBot.Services` |
| enum | PascalCase（単数形） | `LogLevel` |
| enum メンバー | PascalCase | `LogLevel.Warning` |

### レイアウト・書式

- インデントは **スペース 4 つ**（タブ不可）
- Allman スタイル（`{` を新しい行に置く）
- 1 ファイル 1 型を基本とする
- `using` ディレクティブはファイル先頭、名前空間の外に置く。`System` 名前空間を先頭にソートする
- 行の長さは **120 文字以内** を目安にする
- ファイル末尾に改行を入れる

### 言語機能の使い方

- 組み込み型エイリアスを使う（`string`, `int`, `bool` — `String`, `Int32` ではなく）
- `var` は型が右辺から明らかな場合にのみ使う
- 文字列結合には文字列補間 `$"Hello, {name}"` を使う
- null 許容参照型を有効にし、`#nullable enable` を前提とする
- パターンマッチング・switch 式を積極的に使う
- コレクション初期化子やターゲット型 `new()` を活用する
- `IDisposable` 実装は `using` 宣言 / `using` ブロックで確実に解放する

### 非同期プログラミング

- I/O バウンド処理は `async` / `await` を使い、`Task.Result` や `Task.Wait()` でブロックしない
- `async void` は使用禁止（イベントハンドラを除く）
- `CancellationToken` を受け取るオーバーロードがあれば伝搬する
- `ConfigureAwait(false)` はライブラリコードで使用し、アプリケーション最上位層では省略する

### エラーハンドリング

- 例外は制御フローに使わない
- 空の `catch` ブロックは禁止。最低限ログを残す
- 独自例外は `Exception` を末尾に付けて命名する（`ConfigNotFoundException`）

### コメント・ドキュメント

- public API には `///` XML ドキュメントコメントを付ける
- 実装コメントは `//` で、コードの「なぜ」を説明する。自明な処理にはコメント不要
- TODO / HACK / FIXME は一時的な使用のみ。長期間残さない

### プロジェクト構成

```
DiscordBot-Noteworthy/
├── DiscordBot-Noteworthy.sln
├── src/
│   └── DiscordBot-Noteworthy/
│       ├── DiscordBot-Noteworthy.csproj
│       ├── Program.cs
│       ├── Commands/       # スラッシュコマンド・テキストコマンド
│       ├── Services/       # ビジネスロジック
│       ├── Models/         # データモデル・DTO
│       └── Configuration/  # 設定関連
└── tests/
    └── DiscordBot-Noteworthy.Tests/
```

### その他のルール

- 警告はエラーとして扱う（`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`）
- 未使用の `using` は削除する
- アクセス修飾子は常に明示する（`private` も省略しない）
- `this.` は冗長な場合は使わない
- マジックナンバーは定数または設定値に置き換える
- シークレット（トークン・APIキー）はコードにハードコードしない。環境変数または User Secrets を使う
