using System;

namespace ClearBank.DeveloperTest.Types
{
    // [Flags] makes the bit-field intent explicit — the values were already powers of two,
    // the attribute was just missing. Without it, ToString() prints a raw number
    // instead of readable names like "Bacs | FasterPayments".
    [Flags]
    public enum AllowedPaymentSchemes
    {
        FasterPayments = 1 << 0,
        Bacs = 1 << 1,
        Chaps = 1 << 2
    }
}
