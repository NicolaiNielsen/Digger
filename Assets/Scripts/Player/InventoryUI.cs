using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public Inventory playerInventory;
    public TMP_Text inventoryText;
    public GameObject panelToToggle;

    void OnEnable()
    {
        playerInventory.OnInventoryChanged += Refresh;
    }
    void OnDisable()
    {
        playerInventory.OnInventoryChanged -= Refresh;
    }

    public void Refresh()
    {
        var items = playerInventory.GetAllItems();
        string display = "";
        foreach (var kv in items)
            display += $"{kv.Key}: {kv.Value}\n";
        inventoryText.text = display;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("ARE We being claled?");
            bool showing = !panelToToggle.activeSelf;
            panelToToggle.SetActive(showing);
            if (showing)
                Refresh(); // Ensure it's up to date the first time opened
        }
    }

    void Start()
    {
        panelToToggle.SetActive(false);
    }
}
