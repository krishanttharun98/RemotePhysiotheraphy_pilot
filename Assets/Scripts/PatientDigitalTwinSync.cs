using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Keeps the patient's movin avatar pose replicated to remote peers (therapist digital twin view).
/// Pose is authored on the host/patient and applied on every client from NetworkVariables.
/// </summary>
public class PatientDigitalTwinSync : NetworkBehaviour
{
    [Header("Avatar Targets")]
    public Transform avatarRoot;
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    private readonly NetworkVariable<bool> _avatarVisibleNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> _avatarPositionNet = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Quaternion> _avatarRotationNet = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> _leftHandPositionNet = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Quaternion> _leftHandRotationNet = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> _rightHandPositionNet = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Quaternion> _rightHandRotationNet = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsAvatarVisible => _avatarVisibleNet.Value;

    public void BindAvatar(GameObject avatarObject, Transform head, Transform leftHand, Transform rightHand)
    {
        if (avatarObject != null)
        {
            avatarRoot = avatarObject.transform;
        }

        if (head != null) avatarHead = head;
        if (leftHand != null) avatarLeftHand = leftHand;
        if (rightHand != null) avatarRightHand = rightHand;
    }

    public void PublishPatientPose(bool visible, Vector3 rootPosition, Quaternion rootRotation, Transform leftHand, Transform rightHand)
    {
        if (!IsServer) return;

        _avatarVisibleNet.Value = visible;
        if (!visible) return;

        _avatarPositionNet.Value = rootPosition;
        _avatarRotationNet.Value = rootRotation;

        if (leftHand != null)
        {
            _leftHandPositionNet.Value = leftHand.position;
            _leftHandRotationNet.Value = leftHand.rotation;
        }

        if (rightHand != null)
        {
            _rightHandPositionNet.Value = rightHand.position;
            _rightHandRotationNet.Value = rightHand.rotation;
        }
    }

    void LateUpdate()
    {
        ApplyNetworkedPose();
    }

    void ApplyNetworkedPose()
    {
        if (ColocationRoleHelper.IsLocalPatient()) return;
        if (avatarRoot == null) return;

        avatarRoot.gameObject.SetActive(_avatarVisibleNet.Value);
        if (!_avatarVisibleNet.Value) return;

        avatarRoot.SetPositionAndRotation(_avatarPositionNet.Value, _avatarRotationNet.Value);

        if (avatarLeftHand != null)
        {
            avatarLeftHand.SetPositionAndRotation(_leftHandPositionNet.Value, _leftHandRotationNet.Value);
        }

        if (avatarRightHand != null)
        {
            avatarRightHand.SetPositionAndRotation(_rightHandPositionNet.Value, _rightHandRotationNet.Value);
        }
    }
}
