using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spinning : MonoBehaviour
{
    public float speed;
    private bool left;
    private float speedUp;
    private const float accelerate = 0.05f;

    private void FixedUpdate()
    {
        if (speedUp < speed)
        {
            speedUp += accelerate;
        }
        else
        {
            speedUp = speed;
        }
        Quaternion rotateTo = transform.rotation * Quaternion.AngleAxis((left ? -1 : 1) * 90, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, rotateTo, Time.deltaTime * speedUp);
    }
    public void ChangeDirection()
    {
        speedUp = 0f;
        left = !left;
    }
}