using System;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TradingAi.Core.Models // Namespace updated to TradingAi.Core.Models
{
    /// <summary>
    /// 時系列データ(LSTM)を受け取り、上昇確率(0.0~1.0)を出力するモデル
    /// </summary>
    public class LstmModel : Module<Tensor, Tensor>
    {
        private readonly LSTM lstm;
        private readonly Linear fc;
        private readonly Sigmoid sigmoid;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inputSize">入力の特徴量数 (今回は 4)</param>
        /// <param name="hiddenSize">LSTMの隠れ層のニューロン数 (例: 128)</param>
        /// <param name="numLayers">LSTMの層数 (例: 2)</param>
        public LstmModel(string name, long inputSize, long hiddenSize, long numLayers = 2) : base(name)
        {
            // 1. LSTM層: 時系列の特徴を抽出する
            // batchFirst: true にすると [Batch, Time, Feature] の順で入力を受け取れる
            this.lstm = LSTM(inputSize, hiddenSize, numLayers, batchFirst: true);

            // 2. 全結合層 (Fully Connected): LSTMの結果を1つの数値に変換する
            this.fc = Linear(hiddenSize, 1);

            // 3. シグモイド関数: 結果を 0.0 ～ 1.0 の確率に押し込める
            this.sigmoid = Sigmoid();

            // コンポーネントを登録 (これを忘れるとGPUにパラメータが転送されない)
            RegisterComponents();
        }

        /// <summary>
        /// 順伝播 (Forward Propagation)
        /// データを受け取って予測値を返す処理
        /// </summary>
        public override Tensor forward(Tensor input)
        {
            // LSTMの実行
            // output: 全時刻の出力, (h_n, c_n): 最終状態
            var (output, _, _) = lstm.forward(input);

            // 最終時刻(t=30)の出力だけを取り出す
            // output shape: [BatchSize, SequenceLength(30), HiddenSize]
            // -> [BatchSize, HiddenSize] にする
            var lastTimestep = output[.., -1, ..];

            // 全結合層に通す
            var x = fc.forward(lastTimestep);

            // 確率(0~1)に変換して返す
            return sigmoid.forward(x);
        }
    }
}
