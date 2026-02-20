using UnityEngine;
using TMPro;
using UniRx;

public class CoinUI : MonoBehaviour
{
    [SerializeField] private TextMeshPro coinTextWorld;

    private void Start()
    {
        if (GameProgressManager.Instance != null && GameProgressManager.Instance.Coins != null)
        {
            GameProgressManager.Instance.Coins.Subscribe(UpdateCoinText).AddTo(this);
        }
    }

    private void UpdateCoinText(int count)
    {
        coinTextWorld.text = count.ToString();
    }
}
