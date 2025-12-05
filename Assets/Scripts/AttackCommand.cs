using System.Collections.Generic;
using UnityEngine;

public class AttackCommand : ICommand
{
    private Unit attacker;
    private Vector2Int[] targetPos;
    private GridManager gridManager;
    private class DamageInfo
    {
        public Unit target = null!;
        public int previousHP;
        public bool wasDead;
    }
    private List<DamageInfo> damageInfos = new List<DamageInfo>();

    public AttackCommand(Unit unit, Vector2Int[] target, GridManager grid)
    {
        this.attacker = unit;
        this.targetPos = target;
        this.gridManager = grid;
    }

    public void Execute()
    {
        damageInfos.Clear();
        foreach (var pos in targetPos)
        {
            Unit targetUnit = gridManager.GetUnitAtPosition(pos);
            if (targetUnit != null)
            {
                damageInfos.Add(new DamageInfo
                {
                    target = targetUnit,
                    previousHP = targetUnit.HP,
                    wasDead = targetUnit.IsDead
                });
            }
        }

        attacker.Attack(targetPos);
    }

    public void Undo()
    {
        for (int i = damageInfos.Count - 1; i >= 0; i--)
        {
            var info = damageInfos[i];

            // HP復元
            info.target.SetHP(info.previousHP);

            // 死亡していた場合は復活
            if (!info.wasDead && info.target.IsDead)
            {
                info.target.Revive();
            }
        }
    }
}