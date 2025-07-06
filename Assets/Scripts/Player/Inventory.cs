using System.Collections.Generic;
using UnityEngine;
using System;

public class Inventory : MonoBehaviour
{   

    //Daily cmmitcommt
    public int capacity = 20;
    [SerializeField]
    public event Action OnInventoryChanged;
    private Dictionary<string, int> items = new Dictionary<string, int>();

    public int cash = 0;
    [SerializeField]

    public bool Add(string item, int amount = 1)
    {
        int currentCount = GetTotalCount();
        Debug.Log($"[Inventory] Trying to add {amount} {item}. Current: {currentCount}, Capacity: {capacity}");
        if (currentCount + amount > capacity)
        {
            Debug.Log($"[Inventory] Cannot add {item}: Not enough space.");
            return false;
        }
        if (items.ContainsKey(item))
        {
            items[item] += amount;
            Debug.Log($"[Inventory] Increased {item} to {items[item]}");
        }
        else
        {
            items[item] = amount;
            Debug.Log($"[Inventory] Added new item {item} (x{amount})");
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool Remove(string item, int amount = 1)
    {
        if (!items.ContainsKey(item) || items[item] < amount)
            return false;
        items[item] -= amount;
        if (items[item] <= 0)
            items.Remove(item);

        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetCount(string item)
    {
        return items.ContainsKey(item) ? items[item] : 0;
    }

    public int GetTotalCount()
    {
        int total = 0;
        foreach (var kv in items)
            total += kv.Value;
        return total;
    }

    public Dictionary<string, int> GetAllItems() => new Dictionary<string, int>(items);
}