using UnityEngine;
using UnityEngine.InputSystem;


/// Follows a target transform and constrains orthographic zoom via the mouse scroll wheel.
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour {
    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float minZoom = 10f;
    [SerializeField, Min(0.01f)] private float maxZoom = 30f;
    [SerializeField, Min(0.01f)] private float zoomStep = 50f;

    private Camera _camera;

    private void Awake() {
        _camera = GetComponent<Camera>();

        if (!_camera.orthographic) {
            Debug.LogWarning($"{nameof(CameraZoom)} expects an orthographic camera for zoom control.");
        }

        minZoom = Mathf.Min(minZoom, maxZoom);
    }

    private void LateUpdate() {
        HandleZoom();
    }

    private void HandleZoom() {
        if (!_camera.orthographic)
            return;

        float scrollDelta = 0f;

        if (Mouse.current != null) {
            // Scroll value is reported in "lines". Normalize to a ±1 range by dividing by the common 120 factor.
            scrollDelta = Mouse.current.scroll.ReadValue().y / 120f;
        }

        if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
            return;

        float targetSize = Mathf.Clamp(_camera.orthographicSize - scrollDelta * zoomStep, minZoom, maxZoom);
        _camera.orthographicSize = targetSize;
    }
}
