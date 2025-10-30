using UnityEngine;
using Unity.Cinemachine;

public class TransitionCameraBehavior : CinemachineExtension
{
    [SerializeField] Transform Player;
    [SerializeField] float Offset = 10f;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        // Only modify the final Body stage (after camera position is calculated)
        if (stage == CinemachineCore.Stage.Body)
        {
            var pos = state.RawPosition;
            state.RawPosition = new Vector3(pos.x, pos.y, Player.position.z - Offset);
        }
    }
}
