using UnityEngine;
using System.Collections.Generic;

public class RushAttackCommand : ICommand
{
    private Unit attacker;
    private GridManager gridManager;
    private int recoilDamage = 1;

    // Attacker State for Undo
    private int attackerPrevHP;
    private bool attackerWasDead;

    // Target State for Undo (Standard AttackCommand logic duplicated or simplified)
    // Actually, Unit.Attack() handles target damage logic, but it's hard to Undo cleanly without tracking targets.
    // However, Unit.Attack() calls unit.AttackAsync() which is async/visual.
    // The standard `AttackCommand` (which we viewed earlier) manually tracks target HP for undo.
    // We should replicate that pattern.

    private Vector2Int[] targetPositions;
    private class DamageInfo
    {
        public Unit target = null!;
        public int previousHP;
        public bool wasDead;
    }
    private List<DamageInfo> targetDamageInfos = new List<DamageInfo>();



    // Overload to supply specific position
    public RushAttackCommand(Unit attacker, Vector2Int targetPos, GridManager grid)
    {
        this.attacker = attacker;
        this.gridManager = grid;
        this.targetPositions = new Vector2Int[] { targetPos };
    }

    // Overload for No Target (Recoil Only)
    public RushAttackCommand(Unit attacker, GridManager grid)
    {
        this.attacker = attacker;
        this.gridManager = grid;
        this.targetPositions = new Vector2Int[0];
    }

    public void Execute()
    {
        // 1. Record Target State
        targetDamageInfos.Clear();
        foreach (var pos in targetPositions)
        {
            Unit targetUnit = gridManager.GetUnitAtPosition(pos);
            if (targetUnit != null)
            {
                targetDamageInfos.Add(new DamageInfo
                {
                    target = targetUnit,
                    previousHP = targetUnit.HP.Value,
                    wasDead = targetUnit.IsDead
                });
            }
        }

        // 2. Record Attacker State
        attackerPrevHP = attacker.HP.Value;
        attackerWasDead = attacker.IsDead;

        // 3. Execute Attack
        // Use attacker.Attack() for visuals?
        // EnemyAIRush used `enemy.TryAttack()` which uses `attackTargetPos`.
        // But in Rush loop, we know exactly where we hit.
        // `Unit.Attack(targetPos)` exists.
        // Target that is a Crate should be broken
        attacker.Attack(targetPositions, recoilDamage, true);

        // 4. Apply Recoil Damage to Attacker
        Debug.Log($"[RushAttack] Recoil Damage to {attacker.name} handled by Attack");
        // attacker.TakeDamage(recoilDamage, attacker.CurrentPos); // Moved to Unit.Attack logic
    }

    public void Undo()
    {
        // 1. Restore Target State
        foreach (var info in targetDamageInfos)
        {
            info.target.SetHP(info.previousHP);
            if (!info.wasDead && info.target.IsDead)
            {
                info.target.Revive();
            }
        }

        // 2. Restore Attacker State
        attacker.SetHP(attackerPrevHP);
        if (!attackerWasDead && attacker.IsDead)
        {
            attacker.Revive();
        }
    }
}
