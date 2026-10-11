using UnityEngine;
using TMPro;
using UniRx;

public class CoinUI : MonoBehaviour
{
    [SerializeField] private TextMeshPro coinTextWorld;
    [SerializeField] private TextMeshPro totalCoinTextWorld; // 累計コイン用
    [SerializeField] private HUDFeedback coinFeedback;

    private int previousCoinCount = -1;

    private void Start()
    {
        if (GameProgressManager.Instance != null)
        {
            if (GameProgressManager.Instance.Coins != null)
            {
                // 初期値を記録（ロード時のフィードバック防止）
                previousCoinCount = GameProgressManager.Instance.Coins.Value;

                GameProgressManager.Instance.Coins.Subscribe(UpdateCoinText).AddTo(this);
            }

            if (GameProgressManager.Instance.TotalCoins != null)
            {
                GameProgressManager.Instance.TotalCoins.Subscribe(UpdateTotalCoinText).AddTo(this);
            }
        }
    }

    private void UpdateCoinText(int count)
    {
        if (coinTextWorld != null)
        {
            coinTextWorld.text = count.ToString();
        }

        // コインが増えたときだけフィードバックを再生
        if (previousCoinCount >= 0 && count > previousCoinCount && coinFeedback != null)
        {
            coinFeedback.PlayFeedback();
        }
        previousCoinCount = count;
    }

    private void UpdateTotalCoinText(int count)
    {
        if (totalCoinTextWorld != null)
        {
            totalCoinTextWorld.text = count.ToString();
        }
    }
}
