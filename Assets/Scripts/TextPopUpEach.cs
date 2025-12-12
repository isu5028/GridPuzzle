using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class TextPopUpEach : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] float jumpStrength = 2f;
    [SerializeField] float duration = 0.4f;
    [SerializeField] float delayBetweenChars = 0.05f;
    Vector3 originalPosition;
    Vector3 hiddenPosition;
    System.Threading.CancellationTokenSource _cts;

    void Start()
    {
        originalPosition = text.transform.localPosition;
        hiddenPosition = originalPosition + new Vector3(-100, 0, 0);
        transform.localPosition = hiddenPosition;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P))
        {
            Show();
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            Hide();
        }
#endif
    }

    public void Show()
    {
        _cts?.Cancel();
        _cts = new System.Threading.CancellationTokenSource();
        ShowAsync(_cts.Token).Forget();
    }

    async UniTaskVoid ShowAsync(System.Threading.CancellationToken token)
    {
        text.transform.localPosition = hiddenPosition;
        DOTween.To(() => text.transform.localPosition, x => text.transform.localPosition = x, originalPosition, duration).SetEase(Ease.OutQuad);
        await UniTask.Delay((int)(duration * 500), cancellationToken: token);

        try
        {
            while (!token.IsCancellationRequested)
            {
                await AnimateWave(token);
                await UniTask.Delay(500, cancellationToken: token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    async UniTask AnimateWave(System.Threading.CancellationToken token)
    {
        // Ensure the text mesh is generated and up to date
        text.ForceMeshUpdate();

        var textInfo = text.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0) return;

        // Array to store the animated Y offsets for each character
        float[] yOffsets = new float[charCount];

        // Create a Sequence to manage the animation
        Sequence seq = DOTween.Sequence();

        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int index = i;
            float startTime = i * delayBetweenChars;

            // Animate offset: 0 -> jumpStrength -> 0
            seq.Insert(startTime, DOTween.To(() => yOffsets[index], x => yOffsets[index] = x, jumpStrength, duration * 0.5f).SetEase(Ease.OutQuad));
            seq.Insert(startTime + duration * 0.5f, DOTween.To(() => yOffsets[index], x => yOffsets[index] = x, 0f, duration * 0.5f).SetEase(Ease.InQuad));
        }

        // Apply offsets to the mesh every frame
        seq.OnUpdate(() =>
        {
            // Reset mesh to base state to prevent cumulative offsets
            text.ForceMeshUpdate();

            var meshInfo = text.textInfo.meshInfo;

            for (int i = 0; i < charCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                var materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
                var vertexIndex = textInfo.characterInfo[i].vertexIndex;

                Vector3[] vertices = meshInfo[materialIndex].vertices;
                Vector3 offset = new Vector3(0, yOffsets[i], 0);

                // Apply offset to all 4 vertices of the character
                vertices[vertexIndex + 0] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }

            // Update the mesh with modified vertices
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        });

        seq.SetLink(gameObject); // Ensure tween is killed if object is destroyed
        seq.SetLink(gameObject); // Ensure tween is killed if object is destroyed
        seq.Play();
        await UniTask.WaitUntil(() => !seq.IsActive(), cancellationToken: token);
    }

    public void Hide()
    {
        HideAsync().Forget();
    }

    public async UniTask HideAsync()
    {
        DOTween.To(() => text.transform.localPosition, x => text.transform.localPosition = x, hiddenPosition, duration).SetEase(Ease.OutQuad);
        await UniTask.Delay((int)(duration * 1000));
        _cts?.Cancel();

    }
}
