//------------------------------------------------------------------------------
// MemoryMonitor.cs - based on MemoryMonitor from .Net Framework
//
// <copyright file="MemoryMonitor.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Web.Hosting;

using CacheInstrumentation;

namespace CacheInstrumentation.Monitors
{
    public sealed class InstrumentedMemoryMonitor : IApplicationMonitor, IObservable<RecycleLimitInfo>, IObservable<LowPhysicalMemoryInfo> {

        internal const long TERABYTE = 1L << 40;
        internal const long GIGABYTE = 1L << 30;
        internal const long MEGABYTE = 1L << 20;
        internal const long KILOBYTE = 1L << 10;

        internal const long PRIVATE_BYTES_LIMIT_2GB = 800 * MEGABYTE;
        internal const long PRIVATE_BYTES_LIMIT_3GB = 1800 * MEGABYTE;
        internal const long PRIVATE_BYTES_LIMIT_64BIT = 1L * TERABYTE;

        internal static long s_totalPhysical;
        internal static long s_totalVirtual;
        internal static long s_processPrivateBytesLimit = -1;
        internal static long s_configuredProcessMemoryLimit = 0;

        private static InstrumentedMemoryMonitor _firstMemoryMonitor = null;

        private RecycleLimitMonitor _recycleMonitor = null;
        private IObserver<RecycleLimitInfo> _defaultRecycleObserver = null;
        private IDisposable _defaultRecycleSubscription = null;

        private LowPhysicalMemoryMonitor _lowMemoryMonitor = null;
        private IObserver<LowPhysicalMemoryInfo> _defaultLowMemObserver = null;
        private IDisposable _defaultLowMemSubscription = null;

        internal static long ConfiguredProcessMemoryLimit {
            get {
                long memoryLimit = s_configuredProcessMemoryLimit;

                if (memoryLimit == 0) {
                    memoryLimit = ReflectionUtils.GetConfiguredProcessMemoryLimit();
                    Interlocked.Exchange(ref s_configuredProcessMemoryLimit, memoryLimit);
                }

                return memoryLimit;
            }
        }

        internal static long ProcessPrivateBytesLimit {
            get {
                long memoryLimit = s_processPrivateBytesLimit;
                if (memoryLimit == -1) {
                    memoryLimit = ConfiguredProcessMemoryLimit;

                    // AutoPrivateBytesLimit
                    if (memoryLimit == 0) {
                        bool is64bit = (IntPtr.Size == 8);
                        if (s_totalPhysical != 0) {
                            long recommendedPrivateByteLimit;
                            if (is64bit) {
                                recommendedPrivateByteLimit = PRIVATE_BYTES_LIMIT_64BIT;
                            }
                            else {
                                // Figure out if it's 2GB or 3GB
                                if (s_totalVirtual > 2 * GIGABYTE) {
                                    recommendedPrivateByteLimit = PRIVATE_BYTES_LIMIT_3GB;
                                }
                                else {
                                    recommendedPrivateByteLimit = PRIVATE_BYTES_LIMIT_2GB;
                                }
                            }

                            // if we're hosted, use 60% of physical RAM; otherwise 100%
                            long usableMemory = HostingEnvironment.IsHosted ? s_totalPhysical * 3 / 5 : s_totalPhysical;
                            memoryLimit = Math.Min(usableMemory, recommendedPrivateByteLimit);
                        }
                        else {
                            // If GlobalMemoryStatusEx fails, we'll use these as our auto-gen private bytes limit
                            memoryLimit = is64bit ? PRIVATE_BYTES_LIMIT_64BIT : PRIVATE_BYTES_LIMIT_2GB;
                        }
                    }
                    Interlocked.Exchange(ref s_processPrivateBytesLimit, memoryLimit);
                }
                return memoryLimit;
            }
        }

        internal static long PhysicalMemoryPercentageLimit {
            get {
                    if (_firstMemoryMonitor != null && _firstMemoryMonitor._lowMemoryMonitor != null) {
                        return _firstMemoryMonitor._lowMemoryMonitor.PressureHigh;
                    }
                return 0;
            }
        }

        public IObserver<LowPhysicalMemoryInfo> DefaultLowPhysicalMemoryObserver {
            get {
                return _defaultLowMemObserver;
            }
            set {
                if (_defaultLowMemSubscription != null) {
                    _defaultLowMemSubscription.Dispose();
                    _defaultLowMemSubscription = null;
                }
                _defaultLowMemObserver = null;

                if (value != null) {
                    _defaultLowMemObserver = value;
                    _defaultLowMemSubscription = Subscribe(value);
                }
            }
        }

        public IObserver<RecycleLimitInfo> DefaultRecycleLimitObserver {
            get {
                return _defaultRecycleObserver;
            }
            set {
                if (_defaultRecycleSubscription != null) {
                    _defaultRecycleSubscription.Dispose();
                    _defaultRecycleSubscription = null;
                }
                _defaultRecycleObserver = null;

                if (value != null) {
                    _defaultRecycleObserver = value;
                    _defaultRecycleSubscription = Subscribe(value);
                }
            }
        }

        static InstrumentedMemoryMonitor() {
            UnsafeNativeMethods.MEMORYSTATUSEX memoryStatusEx = new UnsafeNativeMethods.MEMORYSTATUSEX();
            memoryStatusEx.Init();
            if (UnsafeNativeMethods.GlobalMemoryStatusEx(ref memoryStatusEx) != 0) {
                s_totalPhysical = memoryStatusEx.ullTotalPhys;
                s_totalVirtual = memoryStatusEx.ullTotalVirtual;
            }
        }

        internal InstrumentedMemoryMonitor() {
            _recycleMonitor = new RecycleLimitMonitor();
            DefaultRecycleLimitObserver = new RecycleLimitObserver();

            _lowMemoryMonitor = new LowPhysicalMemoryMonitor();
            DefaultLowPhysicalMemoryObserver = new LowPhysicalMemoryObserver();

            if (_firstMemoryMonitor == null) {
                _firstMemoryMonitor = this;
            }
        }

        public IDisposable Subscribe(IObserver<LowPhysicalMemoryInfo> observer) {
            if (_lowMemoryMonitor != null) {
                _lowMemoryMonitor.Subscribe(observer);
            }

            return new Unsubscriber(() => { _lowMemoryMonitor.Unsubscribe(observer); });
        }

        public IDisposable Subscribe(IObserver<RecycleLimitInfo> observer) {
            if (_recycleMonitor != null) {
                _recycleMonitor.Subscribe(observer);
            }

            return new Unsubscriber(() => { _recycleMonitor.Unsubscribe(observer); });
        }

        public void Start() {
            _recycleMonitor.Start();
            _lowMemoryMonitor.Start();
        }

        public void Stop() {
            _recycleMonitor.Stop();
            _lowMemoryMonitor.Stop();
        }

        public void Dispose() {
            DefaultLowPhysicalMemoryObserver = null;
            DefaultRecycleLimitObserver = null;
            _recycleMonitor.Dispose();
        }

        class Unsubscriber : IDisposable {
            Action _unsub;

            public Unsubscriber(Action unsubscribeAction) {
                _unsub = unsubscribeAction;
            }

            public void Dispose() {
                if (_unsub != null) {
                    _unsub.Invoke();
                }
            }
        }
    }
}

