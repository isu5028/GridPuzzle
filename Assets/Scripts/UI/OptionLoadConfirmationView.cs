using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.Localization.Components;

public class OptionLoadConfirmationView : MonoBehaviour
{
    [SerializeField] private Transform[] itemPositions = null!;
    [SerializeField] private Transform cursorObj = null!;

    [SerializeField] private Transform optionLoadConfirmationPanel = null!;
    [SerializeField] private float slideDuration = 0.3f;

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    public void UpdateCursorPosition(int index)
    {
        if (index >= 0 && index < itemPositions.Length)
        {
            cursorObj.position = itemPositions[index].position;
        }
    }

    void Start()
    {
        if (optionLoadConfirmationPanel != null)
        {
            visiblePosition = new Vector3(0, 0, optionLoadConfirmationPanel.localPosition.z);
            hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
            optionLoadConfirmationPanel.localPosition = hiddenPosition;
        }
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionLoadConfirmationPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionLoadConfirmationPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }
}
