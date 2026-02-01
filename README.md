# TradingAi

## 概要

このプロジェクトは、JPX（日本取引所グループ）が提供する「J-Quants API」から取得した金融データ（先物・オプションの分足OHLC）のCSVファイルをMariaDBデータベースにインポートし、さらにそのデータを基に機械学習モデル（AI）をトレーニングするためのC#アプリケーション群です。共通ロジックは`TradingAi.Core`プロジェクトに集約され、よりモジュール化された構造になっています。

## データソース

本プログラムが処理対象とするデータの仕様は、以下のJ-Quants公式ドキュメントに基づいています。

- **先物（分足）データ仕様:**
  [https://jpx.gitbook.io/j-quants-ja/dataspec/derivatives/ohlc_minute_1](https://jpx.gitbook.io/j-quants-ja/dataspec/derivatives/ohlc_minute_1)
- **オプション（分足）データ仕様:**
  [https://jpx.gitbook.io/j-quants-ja/dataspec/derivatives/option-ohlc_minute_1](https://jpx.gitbook.io/j-quants-ja/dataspec/derivatives/option-ohlc_minute_1)

## 主な機能

### TradingAi.Core プロジェクト (共通ライブラリ)
- **高信頼なデータベース設計:** 時系列データに最適化された複合主キー（銘柄コード + 日時）を採用。これにより、`Id`カラムを撤廃し、検索パフォーマンスとストレージ効率を向上させています。データモデルおよび`DbContext`を定義します。
- **データパイプラインロジック:** 複数のアクティブなオプション銘柄とその対になる先物データを一括で読み込み、対数収益率などの特徴量エンジニアリングを適用。**汎用モデル**を学習させるための、統一された形式の巨大なテンソルデータセットに結合するロジックを提供します。
- **共通AIモデル定義:** `LstmModel`や`ModelDefinition`といったAIモデルの構造定義をこのプロジェクトに集約し、`Trainer`（学習時）と`Bot`（推論時）の両方から一貫して利用できる設計になっています。

### TradingAi.DataImporter プロジェクト (コンソールアプリケーション: データインポート)
- `TradingAi.Core`を参照し、J-Quants APIから取得した金融データのCSVファイルをMariaDBデータベースにインポートする機能を提供します。
- **EFCore.BulkExtensionsによる高速インポート:** 大量のデータを効率的にデータベースに登録するため、`EFCore.BulkExtensions`を利用したバルク処理（UPSERT）を導入しています。
- **重複インポートの防止:** データベースの複合主キー制約とバルクUPSERT処理により、重複データは更新、新規データは挿入されるため、データの整合性が保証されます。さらに、インポート開始前に完了フォルダをチェックし、処理済みのファイルはスキップするロジックも実装しています。
- **設定ファイルによる柔軟な構成:** データベースの接続情報や、インポート対象のフォルダパスなどを`appsettings.json`で一元管理できます。
- **フォルダ単位での一括インポート:** 指定されたフォルダ配下にあるすべてのCSVファイルを再帰的に検索し、一括でインポートします。
- **処理済みファイルの自動移動:** インポートが正常に完了したCSVファイルは、自動的に完了用フォルダに移動されます。移動先に同名ファイルが存在する場合は、ファイル名の末尾に日時を付与してリネームし、データ損失を防ぎます。
- **タイムスタンプの自動記録:** すべての新規レコードに、データの取り込み日時を示す `created_at` がUTCで自動的に記録されます。

### TradingAi.Trainer プロジェクト (コンソールアプリケーション: AIトレーニング)
- `TradingAi.Core`を参照し、インポートされたデータベース上のデータを基に機械学習モデル（AI）をトレーニングする機能を提供します。
- **学習プロセスの自動化:**
    1. **アクティブ銘柄の自動選定:** 指定期間内で取引が活発だったオプション銘柄をDBから自動でピックアップします。
    2. **汎用データセットの構築:** `TradingAi.Core`のパイプラインを使い、選定された複数銘柄のデータを結合して単一の巨大な学習データセットを作成します。
    3. **LSTMモデルによる学習:** 準備されたデータセットを基にLSTM (Long Short-Term Memory) ニューラルネットワークをトレーニングし、時系列データから未来の価格変動（上昇確率）を予測するモデルを構築します。
- **GPU高速化対応:** TorchSharpを利用し、RTX 5090などのCUDA対応GPUを活用して高速な学習が可能です。
- **学習済みモデルの自動配置:** トレーニング完了後、モデルは`TradingAi.Bot`プロジェクトが直接参照できるパス (`TradingAi.Bot/Models/`) に`.dat`形式で保存されます。

### TradingAi.Bot プロジェクト (コンソールアプリケーション: AIモデルを利用した売買ロジック)
- `TradingAi.Core`を参照し、`Trainer`によって生成された**学習済みモデルを読み込んで、推論を実行する機能的なサンプル**を提供します。
- **推論プロセスの実例:**
    1. `Trainer`が出力した`UniversalModel.dat`をロードします。
    2. モデルを学習モードから推論（評価）モード (`.eval()`) に切り替えます。
    3. `torch.no_grad()`ブロック内でメモリ効率よく推論を実行します。
    4. ダミーの時系列データを入力し、モデルが出力する「価格上昇確率」を取得します。
    5. 取得した確率に基づき、「買い」「売り」「待機」といった判断を行うシンプルなサンプルロジックを提示します。

## フォルダ構成

`TradingAi` ソリューションは、以下のプロジェクトで構成されています。

```
プロジェクトルート/
├── Data/
│   ├── Import/
│   │   ├── future/     (ここに先物のCSVファイルを配置)
│   │   └── option/     (ここにオプションのCSVファイルを配置)
│   └── Complete/
│       ├── future/     (処理済みの先物CSVがここに移動される)
│       └── option/     (処理済みのオプションCSVがここに移動される)
├── TradingAi.Core/         (C#クラスライブラリ: 共通データモデル、DBコンテキスト、データパイプライン、AIモデル定義)
│   ├── Data/
│   │   └── TradingDbContext.cs
│   ├── Models/
│   │   ├── FutureBarFull.cs
│   │   ├── OptionBarFull.cs
│   │   └── LstmModel.cs
│   └── Pipelines/
│       └── TradingDataPipeline.cs
├── DataImporter/           (C#コンソールアプリケーション: データインポート機能)
│   └── appsettings.example.json
├── Trainer/                (C#コンソールアプリケーション: AIトレーニング機能)
│   └── appsettings.example.json
├── TradingAi.Bot/          (C#コンソールアプリケーション: リアルタイム売買ロジックサンプル)
│   └── Models/
│       └── UniversalModel.dat  (Trainerプロジェクト実行後にここに生成される学習済みモデル)
└── TradingAi.sln
```

## 必要なもの

- .NET 9.0 SDK (またはそれ以降)
- MariaDB データベース (初回実行時にEF Coreのマイグレーションにより必要なテーブルが自動作成されます)
- `dotnet-ef` グローバルツール (DataImporterのデータベースマイグレーション用)
  ```sh
  dotnet tool install --global dotnet-ef
  ```
- CUDA対応GPU (NVIDIA RTX 4000、5000シリーズなど) および対応するドライバ (Trainerプロジェクトの高速学習用、必須ではないが推奨)

## セットアップ

1.  このリポジトリをクローンします。
2.  `DataImporter` プロジェクトと `Trainer` プロジェクトの両方で、`appsettings.example.json` を `appsettings.json` という名前でコピーします。
3.  それぞれの `appsettings.json` を開き、ご自身の環境に合わせて `ConnectionStrings` や `DirectorySettings`, `ModelSettings`, `TrainingSettings` の値を設定します。
4.  上記の「フォルダ構成」に従い、インポートしたいCSVファイルを`Data/Import/`配下に配置します。

## 実行方法

プロジェクトのルートディレクトリで、以下のコマンドを実行します。

### 1. データのインポート (TradingAi.DataImporter)

```sh
dotnet run --project DataImporter/DataImporter.csproj
```
- **初回実行時:**
  EF Coreのマイグレーション機能により、データベースとテーブルが自動的に作成されます。その後、データのインポートが開始されます。
- **2回目以降の実行:**
  新しいCSVファイルを`Data/Import/`配下に配置してコマンドを実行します。データベースに既に存在するデータや、`Data/Complete/`に同名ファイルが存在する処理済みのデータはスキップされ、新しいデータのみがインポートされます。

### 2. AIのトレーニング (TradingAi.Trainer)

```sh
dotnet run --project Trainer/Trainer.csproj
```
- `DataImporter`でインポートされたデータベース上のデータを読み込み、AIモデルのトレーニングを開始します。
- 実行中にEpochごとの損失（Loss）がコンソールに表示され、学習の進捗を確認できます。
- 学習完了後、`Trainer/appsettings.json` の `ModelSettings:OutputPath` で指定されたパス（デフォルトは `TradingAi.Bot/Models/UniversalModel.dat`）に学習済みモデルが保存されます。

### 3. AIモデルによる推論実行 (TradingAi.Bot)

```sh
dotnet run --project TradingAi.Bot/TradingAi.Bot.csproj
```
- `Trainer`が生成した学習済みモデルファイルを読み込みます。
- ダミーデータを使って推論を実行し、予測結果とそれに基づいた売買判断のサンプルをコンソールに表示します。
- 実際の取引に利用する際は、このプロジェクトをベースに、リアルタイムデータ取得や発注APIとの連携などの実装が必要になります。