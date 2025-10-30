using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways]
public class PerspectiveCameras : MonoBehaviour
{
    private Camera orthoCam;
    [SerializeField] private Camera background_perspCam;
    [SerializeField] private Camera foreground_perspCam;
    private float lastAspect;
    private const float aspectThreshold = 0.001f;

    private void Awake()
    {
        // Cache references once
        orthoCam = GetComponent<Camera>();
    }

    private void Start()
    {
        // Initialize aspect and set view
        lastAspect = background_perspCam.aspect;
        SetPerspectiveCamera();
    }

    private void LateUpdate()
    {
        float currentAspect = background_perspCam.aspect;
        if (Mathf.Abs(currentAspect - lastAspect) > aspectThreshold)
        {
            lastAspect = currentAspect;
            SetPerspectiveCamera();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SetPerspectiveCamera()
    {
        float orthHeight = orthoCam.orthographicSize * 2f;
        float orthWidth = orthHeight * orthoCam.aspect;

        // Calculate Z offset once
        float zDistance_b = orthoCam.farClipPlane - (Mathf.Sqrt(3) / 2f * orthHeight);
        float zDistance_f = zDistance_b - (orthoCam.farClipPlane - orthoCam.nearClipPlane);

        // Update rect transform position directly (no struct copy)
        Vector3 pos = background_perspCam.transform.localPosition;
        pos.z = zDistance_b;
        background_perspCam.transform.localPosition = pos;

        pos.z = zDistance_f;
        foreground_perspCam.transform.localPosition = pos;

        // Update perspective camera settings
        background_perspCam.nearClipPlane = orthoCam.farClipPlane - zDistance_b;
        foreground_perspCam.farClipPlane = orthoCam.nearClipPlane - zDistance_f;

        background_perspCam.rect = orthoCam.rect;
        foreground_perspCam.rect = orthoCam.rect;
    }
}
