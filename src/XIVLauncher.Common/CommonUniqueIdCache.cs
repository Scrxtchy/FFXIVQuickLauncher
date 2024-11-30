using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.PlatformAbstractions
{
    public class CommonUniqueIdCache : IUniqueIdCache
    {
        private const int DAYS_TO_TIMEOUT = 1;

        private List<UniqueIdCacheEntry> _cache;
        private string CacheURL => Environment.GetEnvironmentVariable("XL_CACHE_VERIFIER");
        private (string username, bool verified) isCacheVerified;

        public CommonUniqueIdCache(FileInfo? saveFile)
        {
            this.configFile = saveFile;

            Load();
        }

        #region SaveLoad

        private readonly FileInfo? configFile;

        public void Save()
        {
            if (configFile is null)
                return;

            File.WriteAllText(configFile.FullName, JsonConvert.SerializeObject(_cache, Formatting.Indented));
        }

        public void Load()
        {
            if (configFile is null)
                return;

            if (!File.Exists(configFile.FullName))
            {
                _cache = new List<UniqueIdCacheEntry>();
                return;
            }

            _cache = JsonConvert.DeserializeObject<List<UniqueIdCacheEntry>>(File.ReadAllText(configFile.FullName)) ?? new List<UniqueIdCacheEntry>();
        }

        public void Reset()
        {
            _cache.Clear();
            Save();
        }

        #endregion

        private void DeleteOldCaches()
        {
            _cache.RemoveAll(entry => (DateTime.Now - entry.CreationDate).TotalDays > DAYS_TO_TIMEOUT);
        }

        public bool HasValidCache(string userName)
        {
            return _cache.Any(entry => IsValidCache(entry, userName));
        }

        public void Add(string userName, string uid, int region, int expansionLevel)
        {
            _cache.Add(new UniqueIdCacheEntry
            {
                CreationDate = DateTime.Now,
                UserName = userName,
                UniqueId = uid,
                Region = region,
                ExpansionLevel = expansionLevel
            });

            if (CacheURL?.StartsWith("https://") ?? false)
            {
                Task.Run(async () =>
                {
                    using var client = new HttpClient();
                    await client.PostAsync(CacheURL, new FormUrlEncodedContent(
                        [new KeyValuePair<string, string>("action", "save"),
                    new KeyValuePair<string, string>("username", userName),
                    new KeyValuePair<string, string>("host", System.Net.Dns.GetHostName())]
                        ));
                });
            }
            Save();
        }

        public bool TryGet(string userName, out IUniqueIdCache.CachedUid cached)
        {
            DeleteOldCaches();

            var cache = _cache.FirstOrDefault(entry => IsValidCache(entry, userName));

            if (cache == null)
            {
                cached = default;
                return false;
            }

            cached = new IUniqueIdCache.CachedUid
            {
                UniqueId = cache.UniqueId,
                Region = cache.Region,
                MaxExpansion = cache.ExpansionLevel,
            };
            return true;
        }

        private bool IsValidCache(UniqueIdCacheEntry entry, string name)
        {
            bool localValid = entry.UserName == name && (DateTime.Now - entry.CreationDate).TotalDays <= DAYS_TO_TIMEOUT;

            if (localValid && (CacheURL?.StartsWith("https://") ?? false)) // 
            {

                if (this.isCacheVerified.verified && this.isCacheVerified.username == name) return true;
                var onlineVerified = Task.Run(() =>
                {
                    var client = new HttpClient();
                    using (var res = client.PostAsync(CacheURL,
                        new FormUrlEncodedContent(
                            [new KeyValuePair<string, string>("action", "load"),
                            new KeyValuePair<string, string>("username", name),
                            new KeyValuePair<string, string>("host", System.Net.Dns.GetHostName())])
                            ))
                    {
                        Log.Information($"Recieved ${(int)res.Result.StatusCode} from verifier");
                        return (int)res.Result.StatusCode == 200;
                    }

                }).Result;
                if (onlineVerified) this.isCacheVerified = (name, onlineVerified);
                return onlineVerified;
            }

            return localValid;
        }

        public class UniqueIdCacheEntry
        {
            public string UserName { get; set; }
            public string UniqueId { get; set; }
            public int Region { get; set; }
            public int ExpansionLevel { get; set; }

            public DateTime CreationDate { get; set; }
        }
    }
}
