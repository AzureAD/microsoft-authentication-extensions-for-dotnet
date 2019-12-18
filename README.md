# Contents

This repository contains the source for 2 extension libraries that are built against Identity SDKs [ADAL](https://github.com/AzureAD/azure-activedirectory-library-for-dotnet) and [MSAL](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet): 

### MSAL extension for public client applications

![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Microsoft.Identity.Client.Extensions.Msal?logo=nuget&logoColor=blue)

This is the main extension containing: 

- A cross platform token cache serialization mechanism - [see details on the Wiki](https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet/wiki/Cross-platform-Token-Cache)


### ADAL extensions for public client applications

![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Microsoft.Identity.Client.Extensions.Adal?logo=nuget&logoColor=blue)

This extension was built with the purpose of supporting dual stack implementations (ADAL + MSAL). This is relevant if you want to support older versions of ADFS. It contains the same token cache serialization strategy as the MSAL extensions.

No new functionality will be added to this extension since ADAL library is deprecated.

# Release schedule

We releae the extensions when: 

- important changes are done to MSAL
- changes are done to the extension library

Please log an issue if you require an out of band release.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


