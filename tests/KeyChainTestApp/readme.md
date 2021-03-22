This simple app tries to read - write - delete from various locations of the `login` keychain. Its purpose is to troubleshoot keychain sharing issues.

1. Clone https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet
2. Make sure you have .NET Core SDK installed, at least version 3.1 - https://dotnet.microsoft.com/download. Check by running `dotnet --list-sdks`
3. Navigate to _repo_\tests\KeyChainTestApp
4. Build using `dotnet build`
5. Run using `dotnet run`

6. In the app, try option 1 and then option 2. Errors will be printed in red in the console.

Note: please hit "always allow" or "allow" if prompted 

7. Try to delete KeyChain entry Microsoft.Developer.IdentityService and try step 6 again
8. Try to use the app that fails (PowerShell etc.) again 