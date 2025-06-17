using UnityEngine;

public class Shop : Interactable
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Tests
    }


    protected override void Interact()
    {
        base.Interact();
        Debug.Log("Interact with shop" + gameObject.name);
    }
}
