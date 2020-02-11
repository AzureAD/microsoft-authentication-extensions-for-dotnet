# Proposal for Linux plain-text file fallback

**Context:** Today, the MSAL extension libraries (.net, java, python, javascript) store secrets in KeyRings via LibSecret. These components not available on all Linux distros and they cannot be started when connected via SSH.

**Proposal:** Extensions are to provide a mechanism for products to detect if this secret storage is usable. If it is not, extensions are to write the token cache to an unecrypted file. It then becomes the reponsability of the Linux users to protect their files, using, for example, encrypted disks.

#### Current API

```csharp
 var storagePropertiesBuilder =
    // CacheFileName is used for storgae only on Win. On Mac and Linux, it's used 
    // to produce an event, but contents are empty. Actual secrets are stored in KeyRing 
    // KeyChain.
    new StorageCreationPropertiesBuilder(Config.CacheFileName, Config.CacheDir, Config.ClientId)
        .WithLinuxKeyring(
            Config.LinuxKeyRingSchema,
            Config.LinuxKeyRingCollection,
            Config.LinuxKeyRingLabel,
            Config.LinuxKeyRingAttr1,
            Config.LinuxKeyRingAttr2)
        .WithMacKeyChain(
            Config.KeyChainServiceName,
            Config.KeyChainAccountName);

MsalCacheHelper cacheHelper = MsalCacheHelper.Create(storageCreationProperties);
cacheHelper.RegisterCache(app.UserTokenCache);

```
## Goals

1. API for fallback to file for Linux. (P0)

2. Developers must opt-in to fallback, it should not be a default, since fallback is insecure. (P0)

3. API for detecting if persistence is not working. This will allow products to show users a warning message about the fallback. (P1)

4. If a user connects both via SSH and via UI, her SSH token cache (i.e. the plaintext file) should not be deleted. (P2)

### Non-goals
1. We do not plan to support multiple token cache sources. Token cache is read either from file or from KeyRing. No merging mechanisms exist.
2. Mechanism is not supposed to work on Windows and Mac. Encryption on Windows and Mac via current mechanisms (DPAPI / KeyChain) is guaranteed by the OS.

## Proposal

#### Add a method to check persistence on Linux

```csharp
void cacheHelper.VerifyLinuxPersistence();
```

This method MUST not affect the token cache. It will attempt to write and read a dummy secret. Different KeyRing attributes will be used so as to not interfere with the real token cache. 

If this method fails it throws an exception with more details. Typically the failure points are:

- LibSecret is not installed
- Incorrect version of LibSecret is installed
- D-BUS is not running (typical in SSH scenario)
- No wallet is listening on the other end

TODO: understand perf impact of this call. If expensive, need to inform consumers to not call this too often.

#### Add a method to persist data in a plaintext file


```csharp
 new StorageCreationPropertiesBuier(Config.CacheFileName, Config.CacheDir, Config.ClientId) 
    .WithLinuxPlaintextFile(ConPlaintextFilePath) //new method                     
    .WithMacKeyChain(...); // no change
                     
```                     

As an alternative, the `.WithLinuxPlaintextFile()` can take 0 arguments, and simply use `Config.CacheFileName`, however this would invalidate Goal 4.

#### Suggested pattern for extension consumers

Libraries consuming the extension will: 

1. create a cache helper with a the normal `KeyRing` setup
2. call `cacheHelper.VerifyLinuxPersistence()`
2.1. If this throws an exception, show the user a meaningful message / URL to help page to inform them to secure their secrets storage
2.2. Create a cache helper using `.WithLinuxPlaintextFile` using a file path that comes from either: 
a. a well known ENV variable, e.g. DEV_TOOLS_TOKEN_CACHE
b. if DEV_TOOLS_TOKEN_CACHE is not set, default to a well known location 


