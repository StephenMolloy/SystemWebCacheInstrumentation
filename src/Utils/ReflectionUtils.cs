using System;
using System.Reflection;
using System.Web;
using System.Web.Caching;
using System.Web.Configuration;
using System.Web.Hosting;

namespace CacheInstrumentation
{
    internal class ReflectionUtils
    {
        static readonly MethodInfo RC_GetAppConfig;
        static readonly MethodInfo RC_GetAppLKGConfig;
        static readonly PropertyInfo RC_Cache;
        static readonly Type AppImpersonationContextType;
        static readonly MethodInfo HAF_RaiseError;
        static readonly MethodInfo WE_RaiseRuntimeError;
        static readonly PropertyInfo HE_ShutdownInitiated;
        static readonly MethodInfo HE_TrimCache;
        static readonly PropertyInfo MM_ConfiguredProcessMemoryLimit;
        static readonly PropertyInfo MM_ProcessPrivateBytesLimit;
        static readonly MethodInfo UT_ReportUnhandledException;
        static readonly MethodInfo AM_ShutdownInProgress;
        static readonly MethodInfo AM_GetLockableAppDomainContext;

        static ReflectionUtils()
        {
            var he = typeof(HostingEnvironment);
            var sysweb = he.Assembly;

            var runtimeConfig = sysweb.GetType("System.Web.Configuration.RuntimeConfig");
            RC_GetAppConfig = runtimeConfig.GetMethod("GetAppConfig", BindingFlags.NonPublic | BindingFlags.Static);
            RC_GetAppLKGConfig = runtimeConfig.GetMethod("GetAppLKGConfig", BindingFlags.NonPublic | BindingFlags.Static);
            RC_Cache = runtimeConfig.GetProperty("Cache", BindingFlags.NonPublic | BindingFlags.Instance);

            AppImpersonationContextType = sysweb.GetType("System.Web.ApplicationImpersonationContext");

            var httpAppFactory = sysweb.GetType("System.Web.HttpApplicationFactory");
            HAF_RaiseError = httpAppFactory.GetMethod("RaiseError", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Exception) }, null);

            var webEventBase = sysweb.GetType("System.Web.Management.WebBaseEvent");
            WE_RaiseRuntimeError = webEventBase.GetMethod("RaiseRuntimeError", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Exception), typeof(object) }, null);

            HE_ShutdownInitiated = he.GetProperty("ShutdownInitiated", BindingFlags.NonPublic | BindingFlags.Static);
            HE_TrimCache = he.GetMethod("TrimCache", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(int) }, null);

            var memMonitor = sysweb.GetType("System.Web.Hosting.AspNetMemoryMonitor");
            MM_ConfiguredProcessMemoryLimit = memMonitor.GetProperty("ConfiguredProcessMemoryLimit", BindingFlags.NonPublic | BindingFlags.Static);
            MM_ProcessPrivateBytesLimit = memMonitor.GetProperty("ProcessPrivateBytesLimit", BindingFlags.NonPublic | BindingFlags.Static);

            var misc = sysweb.GetType("System.Web.Util.Misc");
            UT_ReportUnhandledException = misc.GetMethod("ReportUnhandledException", BindingFlags.NonPublic | BindingFlags.Static, null,
                new Type[] { typeof(Exception), typeof(String[]) }, null);

            var am = sysweb.GetType("System.Web.Hosting.ApplicationManager");
            AM_ShutdownInProgress = am.GetMethod("ShutdownInProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            AM_GetLockableAppDomainContext = am.GetMethod("GetLockableAppDomainContext", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
        }

        public static object GetAppConfig()
        {
            return RC_GetAppConfig.Invoke(null, null);
        }

        public static object GetAppLKGConfig()
        {
            return RC_GetAppLKGConfig.Invoke(null, null);
        }

        public static CacheSection GetCacheSection(object runtimeConfig = null)
        {
            object config = runtimeConfig ?? GetAppConfig();
            return RC_Cache.GetValue(config, null) as CacheSection;
        }

        public static IDisposable GetAppImpersonationContext()
        {
            return Activator.CreateInstance(AppImpersonationContextType) as IDisposable;
        }

        public static void HttpAppFactory_RaiseError(Exception e)
        {
            HAF_RaiseError.Invoke(null, new object[] { e });
        }

        public static void WebEvent_RaiseRuntimeError(Exception e, object source)
        {
            WE_RaiseRuntimeError.Invoke(null, new object[] { e, source });
        }

        public static bool HostEnv_ShutdownInitated()
        {
            return (bool)HE_ShutdownInitiated.GetValue(null, null);
        }

        public static long HostEnv_TrimCache(int percent)
        {
            return (long)HE_TrimCache.Invoke(null, new object[] { percent });
        }

        public static long GetConfiguredProcessMemoryLimit()
        {
            return (long)MM_ConfiguredProcessMemoryLimit.GetValue(null, null);
        }

        public static long GetProcessPrivateBytesLimit()
        {
            return (long)MM_ProcessPrivateBytesLimit.GetValue(null, null);
        }

        public static void ReportUnhandledException(Exception e, String[] strings)
        {
            UT_ReportUnhandledException.Invoke(null, new object[] { e, strings });
        }

        public static bool ShutdownInProgress(ApplicationManager am)
        {
            return (bool)AM_ShutdownInProgress.Invoke(am, null);
        }

        public static object GetLockableAppDomainContext(ApplicationManager am, string appId)
        {
            return AM_GetLockableAppDomainContext.Invoke(am, new object[] { appId });
        }

        public static long HttpRuntimeCacheCount()
        {
            CacheStoreProvider internalCache = (CacheStoreProvider)HttpRuntime.Cache.GetType().GetProperty("InternalCache", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(HttpRuntime.Cache, null);
            return internalCache.ItemCount + HttpRuntime.Cache.Count; // HttpRuntime.Cache.Count only gives us ObjectCache. Does not include internal cache.
        }

        public static object GetOtherCache(bool isPublic)
        {
            string otherCacheFieldName = (isPublic ? "_internalCache" : "_objectCache");
            return HttpRuntime.Cache.GetType().GetField(otherCacheFieldName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }
    }
}
