using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace CacheInstrumentation
{
    internal class Utils
    {
        static readonly DateTime MinValuePlusOneDay = DateTime.MinValue.AddDays(1);
        static readonly DateTime MaxValueMinusOneDay = DateTime.MaxValue.AddDays(-1);
        
        static internal DateTime ConvertToLocalTime(DateTime utcTime)
        {
            if (utcTime < MinValuePlusOneDay)
            {
                return DateTime.MinValue;
            }

            if (utcTime > MaxValueMinusOneDay)
            {
                return DateTime.MaxValue;
            }

            return utcTime.ToLocalTime();
        }
    }

    // This wrapper around a managed object is opaque to SizedReference GC handle
    // and therefore helps with calculating size of only relevant graph of objects
    internal class DisposableGCHandleRef<T> : IDisposable
    where T : class, IDisposable
    {
        GCHandle _handle;
        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public DisposableGCHandleRef(T t)
        {
            Debug.Assert(t != null);
            _handle = GCHandle.Alloc(t);
        }

        public T Target
        {
            [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
            get
            {
                Debug.Assert(_handle.IsAllocated);
                return (T)_handle.Target;
            }
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public void Dispose()
        {
            Target.Dispose();
            Debug.Assert(_handle.IsAllocated);
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
