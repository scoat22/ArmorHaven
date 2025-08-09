using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Armor))]
public class DamagedMesh : MonoBehaviour
{
    Armor _Armor;
    public GameObject[] DamageLevels;

    // Start is called before the first frame update
    void Start()
    {
        _Armor = GetComponent<Armor>();
    }

    /*private void FixedUpdate()
    {
        var Health = _Armor.Health;

        for (int i = 0; i < DamageLevels.Length; i++)
            DamageLevels[i].SetActive(false);

        if (Health > 0 && DamageLevels.Length > 0)
        {
            int Level = (int)Mathf.Clamp(Health * DamageLevels.Length, 0, DamageLevels.Length - 1);

            DamageLevels[Level].SetActive(true);
        }

        if (TryGetComponent(out MeshCollider collider))
        {
            if (Health <= 0)
            {
                collider.enabled = false;
            }
            else collider.enabled = true;
        }
    }*/
}
