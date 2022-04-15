using System;

namespace CacheInstrumentation
{
    internal class Debug
    {
        [System.Diagnostics.Conditional("DBG")]
        internal static void Assert(bool assertion, string message)
        {
#if DBG
            EnsureInit();
            if (assertion == false) {
                if (DoAssert(message)) {
                    Break();
                }
            }
#endif
        }


        [System.Diagnostics.Conditional("DBG")]
        internal static void Assert(bool assertion)
        {
#if DBG
            EnsureInit();
            if (assertion == false) {
                if (DoAssert(null)) {
                    Break();
                }
            }
#endif
        }


        [System.Diagnostics.Conditional("DBG")]
        internal static void Trace(string tagName, string message)
        {
#if DBG
            if (TraceBreak(tagName, message, null, true)) {
                Break();
            }
#endif
        }
    }
}
