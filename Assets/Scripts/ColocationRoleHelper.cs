public static class ColocationRoleHelper
{
    public static bool IsLocalPatient()
    {
        if (SimpleRelayManager.Instance != null)
            return SimpleRelayManager.Instance.LocalRole == SimpleRelayManager.UserRole.Patient;

        if (SessionRoleManager.Instance != null)
            return SessionRoleManager.Instance.LocalRole == SessionRoleManager.UserRole.Patient;

        return true;
    }

    public static bool IsLocalTherapist()
    {
        if (SimpleRelayManager.Instance != null)
            return SimpleRelayManager.Instance.LocalRole == SimpleRelayManager.UserRole.Therapist;

        if (SessionRoleManager.Instance != null)
            return SessionRoleManager.Instance.LocalRole == SessionRoleManager.UserRole.Therapist;

        return false;
    }
}
