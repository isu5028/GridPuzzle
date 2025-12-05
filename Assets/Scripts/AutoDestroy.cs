using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float delay = 2.0f; // 寿命（秒）

    void Start()
    {
        // 第2引数に時間を入れるだけで遅延破壊できる
        Destroy(gameObject, delay);
    }
}