using System;
using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [SerializeField] private int maxMana = 100;
    [SerializeField] private int startingMana = 100;
    [SerializeField] private float regenPerSecond;

    public event Action<int, int> ManaChanged;

    private float mana;

    public int MaxMana => maxMana;
    public int CurrentMana => Mathf.FloorToInt(mana + 0.001f);

    private void Awake()
    {
        mana = Mathf.Clamp(startingMana, 0, maxMana);
    }

    private void Start()
    {
        NotifyManaChanged();
    }

    private void Update()
    {
        if (regenPerSecond <= 0f || mana >= maxMana)
        {
            return;
        }

        mana = Mathf.Min(maxMana, mana + regenPerSecond * Time.deltaTime);
        NotifyManaChanged();
    }

    public bool HasMana(int amount)
    {
        return amount <= 0 || mana >= amount - 0.001f;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (mana < amount)
        {
            return false;
        }

        mana -= amount;
        NotifyManaChanged();
        return true;
    }

    public void Restore(int amount)
    {
        if (amount <= 0 || mana >= maxMana)
        {
            return;
        }

        mana = Mathf.Min(maxMana, mana + amount);
        NotifyManaChanged();
    }

    private void NotifyManaChanged()
    {
        ManaChanged?.Invoke(CurrentMana, maxMana);
    }
}
