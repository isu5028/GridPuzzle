using UnityEngine;
using Cysharp.Threading.Tasks;

public class InstantDeathCommand : ICommand
{
    private Unit unit;
    private int damage;
    private Vector2Int deathPos;
    private int hpBefore;
    private bool wasDeadBefore;
    private GameObject effectPrefab;

    public InstantDeathCommand(Unit unit, int damage, GameObject effectPrefab = null)
    {
        this.unit = unit;
        this.damage = damage;
        this.deathPos = unit.CurrentPos;
        this.effectPrefab = effectPrefab;
    }

    public void Execute()
    {
        hpBefore = unit.HP.Value;
        wasDeadBefore = unit.IsDead;

        if (effectPrefab != null)
        {
            GameObject.Instantiate(effectPrefab, unit.transform.position, Quaternion.identity);
        }

        // Screen Effects for Collision Death
        CameraManager.Instance.ShakeCamera(0.5f, 8.0f, 40f, Vector2.up);
        CameraManager.Instance.PlayGlitch(0.5f, 1.0f);
        AudioManager.Instance.PlaySE(AudioManager.SEType.superposition);
        AudioManager.Instance.TriggerSuperpositionEffect();

        // Apply instant damage
        unit.TakeDamage(damage, deathPos, false);
    }

    public void Undo()
    {
        // Restore State
        if (!wasDeadBefore && unit.IsDead)
        {
            unit.Revive();
        }

        unit.SetHP(hpBefore);
    }
}
