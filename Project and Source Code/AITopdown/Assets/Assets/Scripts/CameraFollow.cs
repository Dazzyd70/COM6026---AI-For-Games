using UnityEngine;
using System.Collections;

// ----------- CAMERA FOLLOW -----------

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 200f;
    public Vector3 offset = new Vector3(0, 10, 0);
    public float maxLeadDistance = 7f;
    public PlayerController player;
    private bool isZooming = false;
    public Vector3 desiredPosition;
    private Camera cam;
    public float zoomDuration = 5f;
    public float zoomInOrthoSize = 0.1f;
    public float normalOrthoSize = 1f;

    private void Start()
    {
        if (player == null && target != null)
            player = target.GetComponent<PlayerController>();

        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            Debug.LogError("No Camera component found on CameraFollow and no Camera.main in scene!");

        cam.orthographicSize = normalOrthoSize;
    }

    void LateUpdate()
    {
        if (!target) return;
        if (player != null && player.isDead)
        {
            if (!isZooming)
            {
                StartCoroutine(ZoomOnDeath());
                isZooming = true;
            }
            desiredPosition = target.position + offset;
            transform.position = new Vector3(desiredPosition.x, transform.position.y, desiredPosition.z);
            return;
        }

        // Mouse lead
        Vector3 mouseViewport = cam != null ? cam.ScreenToViewportPoint(Input.mousePosition) : Vector3.zero;
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);
        Vector2 mouseFromCenter = new Vector2(mouseViewport.x, mouseViewport.y) - screenCenter;
        Vector3 lead = new Vector3(mouseFromCenter.x, 0, mouseFromCenter.y) * maxLeadDistance;

        // Desired camera position
        desiredPosition = target.position + offset + lead;

        // Smooth move (XZ only, fixed Y)
        Vector3 current = transform.position;
        Vector3 smoothPos = Vector3.Lerp(current, new Vector3(desiredPosition.x, offset.y, desiredPosition.z), Time.deltaTime * smoothSpeed);
        transform.position = smoothPos;
    }

    IEnumerator ZoomOnDeath()
    {
        float elapsed = 0f;
        float startOrtho = cam.orthographicSize;
        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(target.position.x, startPos.y, target.position.z);

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / zoomDuration);
            cam.orthographicSize = Mathf.Lerp(startOrtho, zoomInOrthoSize, t);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        cam.orthographicSize = zoomInOrthoSize;
        transform.position = endPos;
    }
}
