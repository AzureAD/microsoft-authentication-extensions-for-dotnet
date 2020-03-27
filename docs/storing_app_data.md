# Proposal for storing custom secrets

**Context:** Consumers of MSAL / MSAL extension need to securily store additional data for their apps, which also benefit from cross-process syncronization

**Proposal:** MSAL extentsion already handles secure storage and cross process locking, so an API can be exposed

```csharp
// Reuse the existing CacheHelper object, created based on storage config
byte[] data = "..."; 

// Read / Writes use file locks!
cacheHelper.WriteCustomData("custom_data_1", data);  
var readData = cacheHelper.ReadCustomData("custom_data_1");
cacheHelper.Clear("custom_data_1");
```

The behavior of this depends on the operating system: 

- Windows: use  the same location as the cache, but file name will be `custom_data_1`
- Mac: use the keychain location with service = `custom_data_1` and same account
- Linux: write in the keyring with additional attribute `custom_data_1 = 1`
- Linux fallback: write in plaintext in the same location as the cache, but filename will `custom_data_1`

All read and write operations use the same locking mechanism as the token cache.