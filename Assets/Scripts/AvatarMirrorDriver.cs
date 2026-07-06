using UnityEngine;

// Drives a humanoid/stick avatar so it reproduces the user's head + hand motion.
//
// Two modes:
//   Overlay  -> the avatar sits exactly on the user (root at the headset).
//   Anchored -> the avatar stands at a fixed anchor (the placed cylinder) and
//               reproduces the same relative motion there.
//
// Runs in LateUpdate with a high execution order so it wins over Movement SDK retargeting.
[DefaultExecutionOrder(10000)]
public class AvatarMirrorDriver : MonoBehaviour
{
    public enum MirrorMode { OverlayOnUser, AnchoredAtTransform }

    [Header("Mode")]
    public MirrorMode mode = MirrorMode.OverlayOnUser;

    [Header("User Tracking Sources (real hardware)")]
    public Transform userHead;
    public Transform userLeftHand;
    public Transform userRightHand;

    [Header("Anchor (only used in AnchoredAtTransform mode)")]
    [Tooltip("Where the twin stands - e.g. the placed cylinder gizmo transform.")]
    public Transform anchor;

    [Header("Avatar Bones To Drive")]
    public Transform avatarRoot;
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    [Header("Behaviour")]
    public bool mirroringEnabled = true;
    [Tooltip("If true the avatar root yaw faces the same way the anchor faces; otherwise it copies the user's yaw.")]
    public bool useAnchorYaw = true;

    void LateUpdate()
    {
        if (!mirroringEnabled || userHead == null) return;

        Vector3 srcPos = userHead.position;
        Quaternion srcYaw = YawOnly(userHead.rotation);

        Vector3 dstPos;
        Quaternion dstYaw;

        if (mode == MirrorMode.AnchoredAtTransform && anchor != null)
        {
            dstPos = anchor.position;
            dstYaw = useAnchorYaw ? YawOnly(anchor.rotation) : srcYaw;
        }
        else
        {
            dstPos = srcPos;
            dstYaw = srcYaw;
        }

        Quaternion srcYawInv = Quaternion.Inverse(srcYaw);

        if (avatarRoot != null)
            avatarRoot.SetPositionAndRotation(dstPos, dstYaw);

        ApplyBone(avatarHead, userHead, srcPos, srcYawInv, dstPos, dstYaw);
        ApplyBone(avatarLeftHand, userLeftHand, srcPos, srcYawInv, dstPos, dstYaw);
        ApplyBone(avatarRightHand, userRightHand, srcPos, srcYawInv, dstPos, dstYaw);
    }

    static void ApplyBone(Transform avatarBone, Transform userBone, Vector3 srcPos, Quaternion srcYawInv, Vector3 dstPos, Quaternion dstYaw)
    {
        if (avatarBone == null || userBone == null) return;

        Vector3 localPos = srcYawInv * (userBone.position - srcPos);
        Quaternion localRot = srcYawInv * userBone.rotation;
        avatarBone.SetPositionAndRotation(dstPos + dstYaw * localPos, dstYaw * localRot);
    }

    static Quaternion YawOnly(Quaternion q)
    {
        Vector3 fwd = q * Vector3.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
    }

    public void SetEnabled(bool enabled) => mirroringEnabled = enabled;
}
