using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WheelSpinner : MonoBehaviour
{
    float radius;
    Vector3 lastPosition;

    void Awake()
    {
        radius = GetComponent<SpriteRenderer>().bounds.extents.y;
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        float distance = transform.position.x - lastPosition.x;
        lastPosition = transform.position;
        if (radius <= 0f) return;

        // Rolling without slipping: angle = distance / radius. Moving right turns clockwise (negative Z);
        // a mirrored vehicle (scale X = -1) also mirrors the rotation, so the scale sign compensates.
        float degrees = -distance / radius * Mathf.Rad2Deg * Mathf.Sign(transform.lossyScale.x);
        transform.Rotate(0f, 0f, degrees);
    }
}
