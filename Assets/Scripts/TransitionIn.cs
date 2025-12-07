using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;

public class TransitionIn : MonoBehaviour
{
    [SerializeField] private Transform[] squareTransforms = null!;
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private float elementDelay = 0.1f;
    float sizeAfter = 0;
    float sizeBefore = 64;
    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    void Start()
    {
        isAnimating = true;
        AnimateAsync().Forget();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (isAnimating) return;
            isAnimating = true;
            AnimateAsync().Forget();
        }
#endif
    }

    async UniTaskVoid AnimateAsync()
    {
        for (int i = 0; i < squareTransforms.Length; i++)
        {
            squareTransforms[i].localScale = new Vector3(sizeBefore, sizeBefore, sizeBefore);
        }
        for (int i = 0; i < squareTransforms.Length; i++)
        {
            squareTransforms[i].DOScaleX(sizeAfter, slideDuration).SetEase(Ease.InCubic);
            await UniTask.Delay(TimeSpan.FromSeconds(elementDelay));
        }
        isAnimating = false;
    }
}
