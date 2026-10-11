using UnityEngine;

public class CrossStageAttackCommand : ICommand
{
    private Unit attacker;
    private Unit target;
    private int damage;
    private int recoilDamage = 1;

    // Target State
    private int targetPreviousHP;
    private bool targetWasDead;

    // Attacker State
    private int attackerPreviousHP;
    private bool attackerWasDead;

    public CrossStageAttackCommand(Unit attacker, Unit target, int damage)
    {
        this.attacker = attacker;
        this.target = target;
        this.damage = damage;
    }

    public void Execute()
    {
        // 1. Record State
        targetPreviousHP = target.HP.Value;
        targetWasDead = target.IsDead;

        attackerPreviousHP = attacker.HP.Value;
        attackerWasDead = attacker.IsDead;

        // 2. Apply Damage to Target
        Debug.Log($"[CrossStageAttack] Attacking {target.name} (Active:{target.gameObject.activeInHierarchy}) with damage {damage}");
        if (target is Crate c)
        {
            c.BreakCrate(attacker.CurrentPos);
        }
        else
        {
            target.TakeDamage(damage, attacker.CurrentPos);
        }

        // 3. Apply Recoil to Attacker
        Debug.Log($"[CrossStageAttack] Recoil Damage to {attacker.name}");
        attacker.TakeDamage(recoilDamage, attacker.CurrentPos);
    }

    public void Undo()
    {
        // Restore Target
        target.SetHP(targetPreviousHP);
        if (!targetWasDead && target.IsDead)
        {
            target.Revive();
        }

        // Restore Attacker
        attacker.SetHP(attackerPreviousHP);
        if (!attackerWasDead && attacker.IsDead)
        {
            attacker.Revive();
        }
    }
}
