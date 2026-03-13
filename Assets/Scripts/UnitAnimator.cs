using UnityEngine;
using System;

[Serializable]
public struct DirectionAnimations
{
    public AnimationClip right;
    public AnimationClip down;
    public AnimationClip left;
    public AnimationClip up;

    public AnimationClip GetClip(Direction direction)
    {
        return direction switch
        {
            Direction.Right => right,
            Direction.Down => down,
            Direction.Left => left,
            Direction.Up => up,
            _ => null
        };
    }

}

public class UnitAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private DirectionAnimations _directionAnimation;
    [SerializeField] private AnimationClip _deathAnimation;
    [SerializeField] private AnimationClip _ignitionAnimation;

    public AnimationClip IgnitionAnimation => _ignitionAnimation;
    public bool IsAnimationLocked { get; set; } = false;

    public void SetDirection(Direction direction)
    {
        if (IsAnimationLocked) return;

        AnimationClip clip = _directionAnimation.GetClip(direction);
        if (clip == null)
        {
            Debug.LogWarning($"[UnitAnimator] Missing animation clip for direction: {direction} on {gameObject.name}");
            return;
        }

        // 現在再生中のアニメーション情報を取得
        AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = currentState.normalizedTime % 1f; // ループ内の正規化時間(0~1)

        // 新しいアニメーションを再生
        _animator.Play(clip.name, 0, normalizedTime);
    }

    public void PlayAnimation(AnimationClip clip)
    {
        if (clip == null || _animator == null) return;
        _animator.Play(clip.name);
    }

    public void SetDeath()
    {
        _animator.Play(_deathAnimation.name);
    }
}