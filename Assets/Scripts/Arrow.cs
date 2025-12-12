using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;

public class Arrow : MonoBehaviour
{
    [SerializeField] private float defaultSpeed = 20f; // Blocks per second (approx)

    public void Launch(Vector3 startPos, Vector3 targetPos, Action onHit)
    {
        LaunchAsync(startPos, targetPos, onHit).Forget();
    }

    private async UniTask LaunchAsync(Vector3 startPos, Vector3 targetPos, Action onHit)
    {
        transform.position = startPos;

        // Face target
        Vector3 direction = (targetPos - startPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / defaultSpeed;

        // Use Linear ease for projectile
        await transform.DOMove(targetPos, duration).SetEase(Ease.Linear).AsyncWaitForCompletion();

        onHit?.Invoke();
        Destroy(gameObject);
    }
}
