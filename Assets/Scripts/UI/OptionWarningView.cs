using System;
using UnityEngine;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Components;

public class OptionWarningView : MonoBehaviour
{
    [SerializeField] private TextMeshPro warningText = null!;
    [SerializeField] private TextMeshPro maskedWarningText = null!;
    [SerializeField] private Transform[] itemPositions = null!;
    [SerializeField] private Transform cursorObj = null!;
    [SerializeField] private Transform warningPanel = null!;
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private TextMeshPro yesText = null!;
    [SerializeField] private TextMeshPro noText = null!;
    [SerializeField] private TextMeshPro maskedYesText = null!;
    [SerializeField] private TextMeshPro maskedNoText = null!;

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;
    private bool isInitialized = false;

    private void Initialize()
    {
        if (isInitialized) return;
        if (warningPanel != null)
        {
            visiblePosition = new Vector3(0, 0, warningPanel.localPosition.z);
            hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
            warningPanel.localPosition = hiddenPosition;
        }
        isInitialized = true;
    }

    public void SetText(string key)
    {
        SetLocalizedText(warningText, "StringTable", key);
        SetLocalizedText(maskedWarningText, "StringTable", key);
        SetLocalizedText(yesText, "StringTable", "YES");
        SetLocalizedText(maskedYesText, "StringTable", "YES");
        SetLocalizedText(noText, "StringTable", "NO");
        SetLocalizedText(maskedNoText, "StringTable", "NO");
    }

    public void UpdateCursorPosition(int index)
    {
        if (index >= 0 && index < itemPositions.Length)
        {
            cursorObj.position = itemPositions[index].position;
        }
    }

    public async UniTask ShowAsync()
    {
        Initialize();
        if (isAnimating) return;

        isAnimating = true;
        warningPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        Initialize();
        if (isAnimating) return;

        isAnimating = true;
        warningPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    private void SetLocalizedText(TextMeshPro textComp, string table, string key)
    {
        if (textComp != null)
        {
            if (textComp.TryGetComponent<LocalizeStringEvent>(out var locEvent))
            {
                locEvent.StringReference.SetReference(table, key);
            }
            else
            {
                var text = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
                textComp.text = text;
            }
        }

    }
}
