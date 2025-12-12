using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;

public class TransitionOut : MonoBehaviour
{
    [SerializeField] private Transform[] squareTransforms = null!;
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private float elementDelay = 0.1f;
    float sizeAfter = 64;
    float sizeBefore = 0;
    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    void Start()
    {

    }

    void Update()
    {

    }

    public async UniTask AnimateAsync()
    {
        var delay = slideDuration;
        for (int i = 0; i < squareTransforms.Length; i++)
        {
            squareTransforms[i].DOScaleX(sizeAfter, slideDuration).SetEase(Ease.OutCubic);
            delay -= elementDelay;
            await UniTask.Delay(TimeSpan.FromSeconds(elementDelay));
        }
        await UniTask.Delay(TimeSpan.FromSeconds(delay));
    }
}
