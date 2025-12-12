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

        // Apply instant damage
        unit.TakeDamage(damage, deathPos);
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
