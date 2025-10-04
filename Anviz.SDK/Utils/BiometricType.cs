namespace Anviz.SDK.Utils
{
    public enum BiometricType
    {
        Unknown = 0,
        Finger = 1,
        Face = 2,
        Iris = 3
    }

    public static class BiometricTypes
    {
        public static BiometricType DecodeBiometricType(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BiometricType.Unknown;
            }

            var normalized = code.Trim().ToUpperInvariant();

            switch (normalized)
            {
                case "FACE7": //FACEPASS7
                case "FACEPASS7":
                case "FACEDEEP5":
                    return BiometricType.Face;
                case "VF30PRO": //VF30 (PRO)
                case "W1": //W1 (PRO)
                case "TC-B-N": //TC550
                case "M7A+-N": //M7
                case "C2":
                case "C2-PRO":
                case "C2KA":
                    return BiometricType.Finger;
            }

            if (normalized.Contains("FACE"))
            {
                return BiometricType.Face;
            }

            if (normalized.StartsWith("C2") || normalized.Contains("KA"))
            {
                return BiometricType.Finger;
            }

            return BiometricType.Unknown;
        }
    }
}
