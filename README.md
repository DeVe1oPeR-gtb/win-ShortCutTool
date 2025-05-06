# 登録ショートカット実行ツール

## 概要
このアプリケーションは、登録されたショートカットキーを使用して様々なアクションを実行するためのツールです。主にプロンプト生成やブラウザ操作などの機能を提供します。

## 機能
### プロンプト生成機能
- 要約 (Ctrl+Alt+F1)
- 文章説明 (Ctrl+Alt+F2)
- 英訳 (Ctrl+Alt+F3)
- 日本語訳 (Ctrl+Alt+F4)
- 議事録作成 (Ctrl+Alt+F5)
- 調査 (Ctrl+Alt+F6)
- コード説明依頼プロンプト作成 (Ctrl+Alt+F11)
- デバッグ依頼プロンプト作成 (Ctrl+Alt+F12)

### ソースコード生成機能
- Pythonソース製作依頼 (Ctrl+Alt+1)
- C言語ソース製作依頼 (Ctrl+Alt+2)
- C++ソース製作依頼 (Ctrl+Alt+3)
- bashソース製作依頼 (Ctrl+Alt+4)
- バッチソース製作依頼 (Ctrl+Alt+5)
- VBAソース製作依頼 (Ctrl+Alt+6)

### その他の機能
- プロンプト作成 (Ctrl+Alt+P)
- Google HP (Ctrl+Alt+G)

## 使用方法
1. アプリケーションを起動すると、タスクトレイに常駐します
2. Ctrl+Alt+L を押すと実行待ちモードが開始されます
3. 表示されたリストから実行したいアクションを選択します
   - ダブルクリックまたはEnterキーで実行
   - Ctrl+Q で実行待ちモードを終了

## 設定
### ウィンドウ設定
- 幅: 600px
- 高さ: 450px
- 位置: (100, 100)

### UI設定
- フォント: BIZ UDGothic
- フォントサイズ: 12pt
- 区切り線: 6, 8, 14, 16行目に配置

### 動作設定
- アクション実行後自動終了: 有効
- バルーン通知: 無効
- 起動時バルーン通知: 有効
- バルーン表示時間: 1000ms

### ログ設定
- ログレベル: Debug
- 最大ファイルサイズ: 512KB
- ログディレクトリ: logs
- ログファイル名: app.log

## テンプレート
各プロンプト生成機能は、`templates`ディレクトリ内の対応するテンプレートファイルを使用します：

### プロンプト生成用テンプレート
- summary.txt - 要約機能用
- text_explanation.txt - 文章説明機能用
- to_english.txt - 英訳機能用
- to_japanese.txt - 日本語訳機能用
- minutes.txt - 議事録作成機能用
- research.txt - 調査機能用
- code_explanation.txt - コード説明機能用
- debug.txt - デバッグ機能用
- prompt_generation.txt - プロンプト作成機能用

### ソースコード生成用テンプレート
- code_request_python.txt - Pythonコード生成用
- code_request_c.txt - C言語コード生成用
- code_request_cpp.txt - C++コード生成用
- code_request_bash.txt - bashスクリプト生成用
- code_request_batch.txt - バッチスクリプト生成用
- code_request_vba.txt - VBAコード生成用

## 注意事項
- プロンプト生成機能を使用する際は、クリップボードにテキストが存在する必要があります
- ブラウザ機能はEdgeまたはChromeに対応しています
- 設定ファイル（Settings.json）を編集することで、ショートカットキーや動作をカスタマイズできます

## 開発者向け情報

### プロジェクト構成

```txt
.
├── src/                    # ソースコード
│   ├── Program.cs         # メインプログラム
│   ├── MainForm.cs        # メインフォーム
│   ├── Settings.cs        # 設定管理
│   ├── ShortcutParser.cs  # ショートカット解析
│   └── Logger.cs          # ログ管理
├── Settings.json          # 設定ファイル
├── templates/             # プロンプトテンプレート
│   ├── summary.txt
│   ├── debug.txt
│   └── ...
└── build.bat              # ビルドスクリプト
```

### ビルド方法

1. .NET Framework 4.0以上がインストールされていることを確認
2. `build.bat`を実行
3. 生成された`ShortcutTool.exe`を実行

### 主要なクラス

- `MainForm`: メインウィンドウとUIの管理
- `Settings`: 設定ファイルの読み込みと管理
- `ShortcutParser`: ショートカットキーの解析
- `Logger`: ログ出力の管理

### カスタマイズ

#### 新しいショートカットの追加

1. `templates`ディレクトリに新しいテンプレートファイルを作成
2. `Settings.json`の`actions`セクションに新しいショートカットを追加

#### テンプレートの修正

`templates`ディレクトリ内のテンプレートファイルを編集することで、プロンプトの内容をカスタマイズできます。

## 参考資料

- [Windows Forms アプリケーション開発](https://learn.microsoft.com/ja-jp/dotnet/desktop/winforms/?view=netdesktop-7.0)
- [C# プログラミング ガイド](https://learn.microsoft.com/ja-jp/dotnet/csharp/programming-guide/)
- [Windows API Code Pack](https://github.com/aybe/Windows-API-Code-Pack-1.1)
- [Qiita: Windows 10でフォーカスを確実に取得する方法](https://qiita.com/kenichiuda/items/0d4f8f7e6c7c1c0c0c0c)
