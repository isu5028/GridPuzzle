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
    private bool breaksCrate;
    private bool resolveInstantly;

    public AttackCommand(Unit unit, Vector2Int[] target, GridManager grid, bool breaksCrate = false, bool resolveInstantly = false)
    {
        this.attacker = unit;
        this.targetPos = target;
        this.gridManager = grid;
        this.breaksCrate = breaksCrate;
        this.resolveInstantly = resolveInstantly;
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
                    // ReactiveProperty<int> から値を取り出すには .Value を使う
                    previousHP = targetUnit.HP.Value,
                    wasDead = targetUnit.IsDead
                });
            }
        }

        if (resolveInstantly)
        {
            for (int i = 0; i < damageInfos.Count; i++)
            {
                Unit targetUnit = damageInfos[i].target;
                if (targetUnit == null) continue;

                if (targetUnit is Crate crate)
                {
                    if (breaksCrate) crate.BreakCrate(attacker.CurrentPos);
                }
                else
                {
                    targetUnit.TakeDamage(1, attacker.CurrentPos);
                }
            }
        }
        else
        {
            attacker.Attack(targetPos, 0, breaksCrate);
        }
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