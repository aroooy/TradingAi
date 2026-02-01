namespace TradingAi.Core.Models;

/// <summary>
/// AIモデルの構造を定義する共通の定数クラスです。
/// トレーニング時と推論時で同じパラメータを使用することを保証します。
/// </summary>
public static class ModelDefinition
{
    /// <summary>
    /// モデルに入力する特徴量の数。
    /// </summary>
    public const long InputSize = 4;

    /// <summary>
    /// LSTMモデルの隠れ層のユニット数。
    /// </summary>
    public const long HiddenSize = 128;
}
