using UnityEngine;

public class Death_disappear : MonoBehaviour
{

    private Health health;
   
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        health = GetComponentInParent<Health>();
    }

    // Update is called once per frame
    void Update()
    {
         health = GetComponentInParent<Health>();
         if (health.IsDead)
         {
            Destroy(gameObject);
         }
    }
}
