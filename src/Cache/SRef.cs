//------------------------------------------------------------------------------
// SRef.cs - based on SRef from .Net Framework
//
// <copyright file="SRef.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;

using CacheInstrumentation;

namespace CacheInstrumentation.CacheProvider
{
    internal class SRef {
        private static Type s_type = Type.GetType("System.SizedReference", true, false);
        private Object _sizedRef;
        private long _lastReportedSize; // This helps tremendously when looking at large dumps
        
        internal SRef(Object target) {
            //_sizedRef = HttpRuntime.CreateNonPublicInstance(s_type, new object[] {target});
            _sizedRef = Activator.CreateInstance(s_type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
                null, new object[] { target }, null);
        }
        
        internal long ApproximateSize {
            [PermissionSet(SecurityAction.Assert, Unrestricted=true)]
            get {
                object o = s_type.InvokeMember("ApproximateSize",
                                               BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, 
                                               null, // binder
                                               _sizedRef, // target
                                               null, // args
                                               CultureInfo.InvariantCulture);
                return _lastReportedSize = (long) o;
            }
        }
        
        [PermissionSet(SecurityAction.Assert, Unrestricted=true)]
        internal void Dispose() {
            s_type.InvokeMember("Dispose",
                                BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, 
                                null, // binder
                                _sizedRef, // target
                                null, // args
                                CultureInfo.InvariantCulture);
        }
    }

    internal class SRefMultiple {
        private List<SRef> _srefs = new List<SRef>();

        internal void AddSRefTarget(Object o) {
            _srefs.Add(new SRef(o));
        }

        internal long ApproximateSize {
            [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
            get {
                return _srefs.Sum(s => s.ApproximateSize);
            }
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        internal void Dispose() {
            foreach (SRef s in _srefs) {
                s.Dispose();
            }
        }
    }
}
