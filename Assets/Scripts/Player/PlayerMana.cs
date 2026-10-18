using System;
using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [SerializeField] private int maxMana = 100;
    [SerializeField] private int startingMana = 100;

    [Header("Out of Combat Regeneration")]
    [SerializeField] private float outOfCombatDelay = 4f;
    [SerializeField] private float outOfCombatRegenPerSecond = 8f;

    public event Action<int, int> ManaChanged;

    private static PlayerMana activeInstance;

    private float mana;
    private float timeSinceCombat;

    public int MaxMana => maxMana;
    public int CurrentMana => Mathf.FloorToInt(mana + 0.001f);
    public bool IsInCombat => timeSinceCombat < outOfCombatDelay;
    public float OutOfCombatTimeRemaining => Mathf.Max(0f, outOfCombatDelay - timeSinceCombat);
    public float OutOfCombatRegenPerSecond => outOfCombatRegenPerSecond;

    private void Awake()
    {
        activeInstance = this;
        mana = Mathf.Clamp(startingMana, 0, maxMana);
        timeSinceCombat = outOfCombatDelay;
    }

    private void Start()
    {
        NotifyManaChanged();
    }

    private void Update()
    {
        timeSinceCombat = Mathf.Min(outOfCombatDelay, timeSinceCombat + Time.deltaTime);

        if (IsInCombat || outOfCombatRegenPerSecond <= 0f || mana >= maxMana)
        {
            return;
        }

        mana = Mathf.Min(maxMana, mana + outOfCombatRegenPerSecond * Time.deltaTime);
        NotifyManaChanged();
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    public void NotifyCombatActivity()
    {
        timeSinceCombat = 0f;
    }

    public static void ReportCombatActivity()
    {
        if (activeInstance != null)
        {
            activeInstance.NotifyCombatActivity();
        }
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
