using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Caching;
using System.Web.Caching;

namespace CacheInstrumentation.MemoryCacheProvider
{
    public class MemoryCacheProvider : CacheStoreProvider
    {
        private static readonly DateTime DATETIME_MINVALUE_UTC = new DateTime(0, DateTimeKind.Utc);

        internal MemoryCache _mc;
        Dictionary<CacheDependency, CacheEntryChangeMonitor> _dependencies;

        private string _name;
        private NameValueCollection _config;

        public override long ItemCount {
            get { return _mc.GetCount(); }
        }

        public override long SizeInBytes {
            get { return _mc.GetLastSize(); }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            _name = name;
            _config = config;

            // Don't use magical behind the scenes wiring up to ASP.Net, since we will wire up directly via the new provider extension point.
            _config.Add("useMemoryCacheManager", "false");

            _mc = new MemoryCache(_name, _config, true);
            _dependencies = new Dictionary<CacheDependency, CacheEntryChangeMonitor>();

            // If you wish to behave differently between the public and internal instances, this flag is passed by ASP.Net
            // to help you determine which cache this instance will be used for.
            if (config["isPublic"] == "true")
            {
                // Do things here
            }
        }

        public override object Add(string key, object item, CacheInsertOptions options)
        {

            CacheItemPolicy policy = new CacheItemPolicy() {
                AbsoluteExpiration = ToDateTimeOffset(options.AbsoluteExpiration),
                SlidingExpiration = options.SlidingExpiration,
                Priority = TranslatePriority(((int)options.Priority != 0) ? options.Priority : System.Web.Caching.CacheItemPriority.Default),
                RemovedCallback = WrapItemRemovedCallback(options.OnRemovedCallback)
            };
            object retrieved = _mc.AddOrGetExisting(key, item, policy, null); // returns existing item if it exists... null otherwise. Just like Cache.Add should do.
            MonitorDependencyChanges(key, options.Dependencies);
            return retrieved;
        }

        public override object Get(string key) {
            return _mc.Get(key, null);
        }

        public override void Insert(string key, object item, CacheInsertOptions options)
        {

            CacheItemPolicy policy = new CacheItemPolicy() {
                AbsoluteExpiration = ToDateTimeOffset(options.AbsoluteExpiration),
                SlidingExpiration = options.SlidingExpiration,
                Priority = TranslatePriority(((int)options.Priority != 0) ? options.Priority : System.Web.Caching.CacheItemPriority.Default),
                RemovedCallback = WrapItemRemovedCallback(options.OnRemovedCallback)
            };
            _mc.Set(key, item, policy, null);
            MonitorDependencyChanges(key, options.Dependencies);
        }

        public override object Remove(string key) { return Remove(key, CacheItemRemovedReason.Removed); }
        public override object Remove(string key, CacheItemRemovedReason reason = CacheItemRemovedReason.Removed)
        {
            return _mc.Remove(key, TranslateRemovedReason(reason));
        }

        public override long Trim(int percent)
        {
            return _mc.Trim(percent);
        }

        public override IDictionaryEnumerator GetEnumerator()
        {
            return (IDictionaryEnumerator)((IEnumerable)_mc).GetEnumerator();
        }

        public override void Dispose()
        {
            _mc.Dispose();
        }

        public override bool AddDependent(string key, CacheDependency dependency, out DateTime utcLastUpdated)
        {

            utcLastUpdated = DateTime.MinValue;

            if (dependency != null && !String.IsNullOrWhiteSpace(key)) {
                CacheEntryChangeMonitor mon = _mc.CreateCacheEntryChangeMonitor(new string[] { key });

                // We should clean up and return false if we could not attach to an actual entry.
                // CreateCacheEntryChangeMonitor will always return a monitor, even if the item does not exist in the cache.
                // So let's tease out some information about the situation, based on what we know the fields of the
                // monitor will look like.
                if (mon.HasChanged && mon.LastModified == DATETIME_MINVALUE_UTC) {
                    mon.Dispose();
                    return false;
                }

                // Otherwise, the entry has been found. We are good to go.
                _dependencies.Add(dependency, mon);
                mon.NotifyOnChanged(WrapDependencyChangedCallback(dependency));
                utcLastUpdated = mon.LastModified.UtcDateTime;
                return true;
            }
            return false;
        }

        public override void RemoveDependent(string key, CacheDependency dependency)
        {
            CacheEntryChangeMonitor mon = _dependencies[dependency];
            if (mon != null)
                mon.Dispose();
        }

        private void MonitorDependencyChanges(string key, CacheDependency dependency)
        {
            if (dependency != null) {
                if (!dependency.TakeOwnership()) {
                    throw new InvalidOperationException("Can't use a cache dependency more than once.");
                }

                if (!String.IsNullOrEmpty(key)) {
                    dependency.SetCacheDependencyChanged((Object sender, EventArgs args) => {
                        _mc.Remove(key, CacheEntryRemovedReason.ChangeMonitorChanged);
                    });
                }
            }
        }

        private static System.Runtime.Caching.CacheItemPriority TranslatePriority(System.Web.Caching.CacheItemPriority pri)
        {
            if (pri == System.Web.Caching.CacheItemPriority.NotRemovable)
                return System.Runtime.Caching.CacheItemPriority.NotRemovable;
            return System.Runtime.Caching.CacheItemPriority.Default;
        }

        private static CacheItemRemovedReason TranslateRemovedReason(CacheEntryRemovedReason reason)
        {
            if (reason == CacheEntryRemovedReason.CacheSpecificEviction)
                return CacheItemRemovedReason.Removed;
            return (CacheItemRemovedReason)(reason + 1);
        }

        private static CacheEntryRemovedReason TranslateRemovedReason(CacheItemRemovedReason reason)
        {
            return (CacheEntryRemovedReason)(reason - 1);
        }

        private static CacheEntryRemovedCallback WrapItemRemovedCallback(CacheItemRemovedCallback onRemovedCallback)
        {
            if (onRemovedCallback == null)
                return null;
            return new CacheEntryRemovedCallback(args => {
                if (args != null && args.CacheItem != null)
                    onRemovedCallback(args.CacheItem.Key, args.CacheItem.Value, TranslateRemovedReason(args.RemovedReason));
            });
        }

        private static OnChangedCallback WrapDependencyChangedCallback(CacheDependency dependency)
        {
            return new OnChangedCallback((state) => { dependency.ItemRemoved(); });
        }

        static internal DateTimeOffset ToDateTimeOffset(DateTime dateTime)
        {
            DateTime utcTime = dateTime.ToUniversalTime();
            return (utcTime <= DateTimeOffset.MinValue.UtcDateTime ? DateTimeOffset.MinValue
                        : (utcTime >= DateTimeOffset.MaxValue.UtcDateTime ? DateTimeOffset.MaxValue : new DateTimeOffset(dateTime)));
        }
    }
}
