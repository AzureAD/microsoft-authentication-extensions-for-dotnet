using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    internal class FileAccessor : ICacheAccessor
    {
        private readonly string _cacheFilePath;
        private readonly TraceSourceLogger _logger;

        public FileAccessor(string cacheFilePath, TraceSourceLogger logger)
        {
            _cacheFilePath = cacheFilePath;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Clear()
        {
            _logger.LogInformation("Deleting cache file");
            FileIOWithRetries.DeleteCacheFile(_cacheFilePath, _logger);
        }

        public ICacheAccessor CreateForPersistenceValidation()
        {
            return new FileAccessor(_cacheFilePath + ".test", _logger);
        }

        public byte[] Read()
        {
            _logger.LogInformation("Reading from file");

            byte[] fileData = null;
            bool cacheFileExists = File.Exists(_cacheFilePath);
            _logger.LogInformation($"Cache file exists? '{cacheFileExists}'");

            if (cacheFileExists)
            {
                FileIOWithRetries.TryProcessFile(() =>
                {
                    fileData = File.ReadAllBytes(_cacheFilePath);
                    _logger.LogInformation($"Read '{fileData.Length}' bytes from the file");
                }, _logger);
            }

            return fileData;
        }

        public void Write(byte[] data)
        {
            FileIOWithRetries.WriteDataToFile(_cacheFilePath, data, _logger);
        }
    }
}
