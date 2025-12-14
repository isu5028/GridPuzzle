using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class GlitchEffect : MonoBehaviour
{
    // Assign the same Material that is used in GlitchRenderFeature
    [SerializeField] private Material glitchMaterial;

    [SerializeField] private float defaultDuration = 0.3f;
    [SerializeField] private float defaultIntensity = 1.0f;

    private CancellationTokenSource cts;

    private void Awake()
    {
        // Ensure initially zero
        ResetEffect();
    }

    private void ResetEffect()
    {
        if(glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_Intensity", 0f);
        }
    }

    public void PlayGlitch(float duration = -1f, float intensity = -1f)
    {
        float d = duration < 0 ? defaultDuration : duration;
        float i = intensity < 0 ? defaultIntensity : intensity;
        PlayGlitchAsync(d, i).Forget();
    }

    public async UniTask PlayGlitchAsync(float duration, float intensity)
    {
        if(glitchMaterial == null) return;

        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        var token = cts.Token;

        try
        {
            float elapsed = 0f;
            while(elapsed < duration)
            {
                if(token.IsCancellationRequested) break;

                // Random fluctuation
                float currentIntensity = intensity * UnityEngine.Random.Range(0.8f, 1.2f);

                glitchMaterial.SetFloat("_Intensity", currentIntensity);
                glitchMaterial.SetFloat("_TimeX", Time.time * 20f);

                await UniTask.Delay(30, cancellationToken: token);
                elapsed += 0.03f;
            }

            ResetEffect();
        }
        catch (OperationCanceledException)
        {
            ResetEffect();
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        ResetEffect();
    }
}
