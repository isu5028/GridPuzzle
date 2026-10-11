using UnityEngine;

public class LofiEffect : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [Header("揺れの強さ")]
    [SerializeField] private float wobbleAmount = 0.005f; // 0.002 ~ 0.01 くらいがおすすめ
    [Header("揺れの速さ")]
    [SerializeField] private float wobbleSpeed = 1.0f;    // 0.5 ~ 2.0 くらい

    private void Start()
    {

    }

    private void Update()
    {
        // PerlinNoiseを使ってランダムかつ滑らかに揺らす
        float noise = Mathf.PerlinNoise(Time.time * wobbleSpeed, 0.0f);

        // 1.0（通常速度）を中心に、微妙にピッチをずらす
        // noiseは0~1なので、0.5引いて -0.5~0.5 にし、強さを掛ける
        audioSource.pitch = 1.0f + (noise - 0.5f) * wobbleAmount;
    }
}