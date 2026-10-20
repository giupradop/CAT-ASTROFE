using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;
    [SerializeField] Vector2 offset = Vector2.zero;

    [Header("Bounds")]
    [SerializeField] bool useBounds = false;
    [SerializeField] Vector2 minBounds;
    [SerializeField] Vector2 maxBounds;

    Camera cam;
    Vector3 velocity;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);

        Vector3 smoothed = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

        if (useBounds)
            smoothed = ClampToBounds(smoothed);

        transform.position = smoothed;
    }

    Vector3 ClampToBounds(Vector3 position)
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        position.x = ClampAxis(position.x, minBounds.x, maxBounds.x, halfWidth);
        position.y = ClampAxis(position.y, minBounds.y, maxBounds.y, halfHeight);
        return position;
    }

    float ClampAxis(float value, float min, float max, float halfExtent)
    {
        float low = min + halfExtent;
        float high = max - halfExtent;
        if (low > high)
            return (min + max) * 0.5f;
        return Mathf.Clamp(value, low, high);
    }

    void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.magenta;
        Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
        Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
