using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBar : MonoBehaviour
{
    public RectTransform Fill;

    public void SetAmount(float Amount) => SetFill(Amount);
    public void SetFill(float Amount)
    {
        Amount = Mathf.Clamp01(Amount);

        // Todo: if amount is below 10%, make it flash red.
        Fill.localScale = new Vector3(Amount, 1.0f, 1.0f);
    }
}
