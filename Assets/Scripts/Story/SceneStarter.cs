using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
public class SceneStarter : MonoBehaviour
{
    [Header("Sound")]
    public AudioSource startSound;

    [Header("Entrance cutscene")]
    [Tooltip("Tick it on the levels where Bigode walks in from the previous room.")]
    [SerializeField] bool playEntrance = false;
    [Tooltip("Bigode. Empty = the first PlayerController2D found in the scene.")]
    [SerializeField] PlayerController2D player;

    [Header("Where he arrives (X, Y)")]
    [Tooltip("On: the spot where Bigode is placed in the scene is where the level starts. Off: use 'Target Position' below.")]
    [SerializeField] bool useCurrentPlayerPositionAsTarget = true;
    [SerializeField] Vector2 targetPosition;

    [Header("Where he comes from")]
    [Tooltip("Where he starts, relative to the target. Negative X = comes in from the left, positive = from the right. It must be bigger than half of the visible width (about 14 units at camera size 8) so he starts off-screen. Y is the height he starts at; he falls to the floor from there.")]
    [SerializeField] Vector2 startOffset = new Vector2(-18f, 0f);

    [Header("Timing")]
    [Tooltip("Seconds to wait before he starts to walk.")]
    [SerializeField, Min(0f)] float startDelay = 0.3f;
    [Tooltip("He stops walking when he is this close to the target X. He slides a little after stopping, so a bigger value stops him earlier.")]
    [SerializeField, Min(0.01f)] float arriveTolerance = 0.3f;
    [Tooltip("Safety: the controls are given back after this many seconds even if he did not arrive (stuck on something).")]
    [SerializeField, Min(1f)] float maxDuration = 15f;

    [Header("Scene help")]
    [Tooltip("Objects turned off during the entrance and back on at the end. Use it for an invisible wall at the edge of the level that would block him on the way in.")]
    [SerializeField] GameObject[] disableDuringEntrance;
    [Tooltip("The Cinemachine camera. Empty = the first one in the scene.")]
    [SerializeField] CinemachineCamera virtualCamera;
    [Tooltip("The camera stays on the target spot while he walks in, instead of following him from off-screen, and goes back to following him at the end.")]
    [SerializeField] bool keepCameraOnTarget = true;

    IEnumerator Start()
    {
        if (startSound != null)
            startSound.Play();

        if (playEntrance)
            yield return Entrance();
    }

    IEnumerator Entrance()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController2D>();
        if (player == null)
        {
            Debug.LogWarning("SceneStarter: no PlayerController2D found for the entrance.", this);
            yield break;
        }

        Vector3 playerPosition = player.transform.position;
        Vector2 target = useCurrentPlayerPositionAsTarget ? (Vector2)playerPosition : targetPosition;
        Vector2 start = target + startOffset;

        player.SetControlEnabled(false);
        player.SetForcedMove(0f);

        var body = player.GetComponent<Rigidbody2D>();
        player.transform.position = new Vector3(start.x, start.y, playerPosition.z);
        if (body != null)
        {
            body.position = start;
            body.linearVelocity = Vector2.zero;
        }

        SetActive(disableDuringEntrance, false);

        Transform cameraSpot = null;
        Transform originalTarget = null;
        if (keepCameraOnTarget)
        {
            if (virtualCamera == null)
                virtualCamera = FindFirstObjectByType<CinemachineCamera>();

            if (virtualCamera != null)
            {
                cameraSpot = new GameObject("EntranceCameraTarget").transform;
                cameraSpot.position = new Vector3(target.x, target.y, playerPosition.z);

                CameraTarget cameraTarget = virtualCamera.Target;
                originalTarget = cameraTarget.TrackingTarget;
                cameraTarget.TrackingTarget = cameraSpot;
                virtualCamera.Target = cameraTarget;
                virtualCamera.PreviousStateIsValid = false;
            }
        }

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        while (elapsed < maxDuration)
        {
            float dx = target.x - player.transform.position.x;
            if (Mathf.Abs(dx) <= arriveTolerance)
                break;

            player.SetForcedMove(Mathf.Sign(dx));
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        player.SetForcedMove(0f);
        float slide = 0f;
        while (body != null && Mathf.Abs(body.linearVelocity.x) > 0.05f && slide < 1f)
        {
            slide += Time.deltaTime;
            yield return null;
        }

        SetActive(disableDuringEntrance, true);

        if (virtualCamera != null && cameraSpot != null)
        {
            CameraTarget cameraTarget = virtualCamera.Target;
            cameraTarget.TrackingTarget = originalTarget != null ? originalTarget : player.transform;
            virtualCamera.Target = cameraTarget;
            Destroy(cameraSpot.gameObject);
        }

        player.SetControlEnabled(true);
    }

    static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            if (obj != null)
                obj.SetActive(active);
    }

    void OnDrawGizmosSelected()
    {
        if (!playEntrance) return;

        var shown = player != null ? player : FindFirstObjectByType<PlayerController2D>();
        Vector2 target = useCurrentPlayerPositionAsTarget && shown != null
            ? (Vector2)shown.transform.position
            : targetPosition;
        Vector2 start = target + startOffset;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(start, 0.5f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target, 0.5f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(start, target);
    }
}