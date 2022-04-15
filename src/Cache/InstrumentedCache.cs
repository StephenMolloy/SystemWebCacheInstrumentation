//------------------------------------------------------------------------------
// InstrumentedCache.cs - based on AspNetCache from .Net Framework
//
// <copyright file="AspNetCache.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;
using System.Web;
using System.Web.Caching;
using System.Web.Configuration;

using CacheInstrumentation;

namespace CacheInstrumentation.CacheProvider
{
    internal sealed class InstrumentedCache : CacheStoreProvider {
        static readonly CacheInsertOptions DefaultInsertOptions = new CacheInsertOptions();

        internal CacheInternal _cacheInternal;
        bool _isPublic = true;
        bool _isDisposed = false;

        public InstrumentedCache() {
            _cacheInternal = CacheInternal.Create();
            Interlocked.Exchange(ref _cacheInternal._refCount, 1);
        }

        internal InstrumentedCache(bool isPublic)
        {
            // Check to see if we've already created another instance of this class to handle the other side
            // of the public/internal coin. Ordinarily, it shouldn't hurt to just use two caches for this
            // separation of concerns... but again, we're trying to be faithful to the in-box implementation,
            // which means we should have both public and internal backed by the same CacheInternal
            InstrumentedCache otherCache = ReflectionUtils.GetOtherCache(isPublic) as InstrumentedCache;

            _isPublic = isPublic;
            _cacheInternal = otherCache?._cacheInternal ?? CacheInternal.Create();
            Interlocked.Increment(ref _cacheInternal._refCount);
        }

        internal InstrumentedCache(InstrumentedCache cache, bool isPublic) {
            _isPublic = isPublic;
            _cacheInternal = cache._cacheInternal;
            Interlocked.Increment(ref _cacheInternal._refCount);
        }

        
        public override long ItemCount {
            get {
                if (_isPublic) {
                    return _cacheInternal.PublicCount;
                }
                return _cacheInternal.TotalCount - _cacheInternal.PublicCount;
            }
        }
        public override long SizeInBytes {
            get {
                return _cacheInternal.ApproximateSize;
            }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (Boolean.TryParse(config["isPublic"], out bool isPublic)) {
                _isPublic = isPublic;
            }

            //CacheSection cacheSection = RuntimeConfig.GetAppConfig().Cache;
            CacheSection cacheSection = ReflectionUtils.GetCacheSection();
            _cacheInternal.ReadCacheInternalConfig(cacheSection);
        }

        public override object Add(string key, object item, CacheInsertOptions options) {
            CacheInsertOptions opts = options ?? DefaultInsertOptions;
            return _cacheInternal.DoInsert(_isPublic, key, item, opts.Dependencies, opts.AbsoluteExpiration,
                opts.SlidingExpiration, opts.Priority, opts.OnRemovedCallback, false);
        }

        public override object Get(string key) { return _cacheInternal.DoGet(_isPublic, key, CacheGetOptions.None); }

        public override void Insert(string key, object item, CacheInsertOptions options) {
            CacheInsertOptions opts = options ?? DefaultInsertOptions;
            _cacheInternal.DoInsert(_isPublic, key, item, opts.Dependencies, opts.AbsoluteExpiration, opts.SlidingExpiration,
                opts.Priority, opts.OnRemovedCallback, true);
        }

        public override object Remove(string key) { return Remove(key, CacheItemRemovedReason.Removed); }
        public override object Remove(string key, CacheItemRemovedReason reason) {
            CacheKey cacheKey = new CacheKey(key, _isPublic);
            return _cacheInternal.Remove(cacheKey, reason);
        }

        public override long Trim(int percent) { return _cacheInternal.TrimCache(percent); }

        public override bool AddDependent(string key, CacheDependency dependency, out DateTime utcLastUpdated) {
            CacheEntry entry = (CacheEntry)_cacheInternal.DoGet(_isPublic, key, CacheGetOptions.ReturnCacheEntry);
            if (entry != null) {
               utcLastUpdated = entry.UtcCreated;
               entry.AddDependent(dependency);  // This seems better in the next if... but here is more faithful to original code

               if (entry.State == CacheEntry.EntryState.AddedToCache) {
                   return true;
               }
            }

            utcLastUpdated = DateTime.MinValue;
            return false;
        }

        public override void RemoveDependent(string key, CacheDependency dependency) {
            CacheEntry entry = (CacheEntry)_cacheInternal.DoGet(_isPublic, key, CacheGetOptions.ReturnCacheEntry);
            if (entry != null) {
                entry.RemoveDependent(dependency);
            }
        }

        public override IDictionaryEnumerator GetEnumerator() { return _cacheInternal.CreateEnumerator(!_isPublic); }

        public override bool Equals(object obj)
        {
            if (obj is InstrumentedCache other)
                return (_cacheInternal == other._cacheInternal);

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override void Dispose() {
            if (!_isDisposed) {
                lock (this) {
                    if (!_isDisposed) {
                        _isDisposed = true;
                        Interlocked.Decrement(ref _cacheInternal._refCount);
                        _cacheInternal.Dispose();
                    }
                }
            }
        }
    }
}

