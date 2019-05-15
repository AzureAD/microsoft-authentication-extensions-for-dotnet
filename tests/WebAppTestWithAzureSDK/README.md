# Web App Example
This example shows how easy it could be using the Azure SDK and authenticating with the
TokenProviderChain. The example adds a dependency injection friendly layer above the
Azure SDK ResourceManagementClient and the ServiceClientCredentials classes, registers these
shims with the ASPNETCORE dependency injection container and uses them in the values controller.

The [DefaultChainServiceCredentials](./Credentials/DefaultChainServiceCredentials.cs) implements
`ServiceClientCredentials` and wraps the [DefaultTokenProviderChain](../../src/Microsoft.Identity.Client.Extensions.Msal/Providers/DefaultTokenProviderChain.cs),
which provides a simple abstraction over Service Principal, Managed Identity and the local shared
developer token cache. The DefaultTokenProviderChain uses the `IConfiguration` interface to find
an available token provider given the configuration options.
- For example, the [ServicePrincipalTokenProvider](../../src/Microsoft.Identity.Client.Extensions.Msal/Providers/ServicePrincipalProvider.cs)
looks for `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` and other environment variables
to build a client to request an access token from Azure Active Directory.
- In another example, the
[ManagedIdentityTokenProvider](../../src/Microsoft.Identity.Client.Extensions.Msal/Providers/ManagedIdentityTokenProvider.cs)
check to see if AppService environment variables are set or if your VM is running with a managed
identity, and if so, it will use that managed identity to procure a token from Azure Active Directory.
- In a final example, the [SharedTokenCacheProvider](../../src/Microsoft.Identity.Client.Extensions.Msal/Providers/SharedTokenCacheProvider.cs)
will look to see if the current user has authenticated with other Microsoft developer tools such as Azure CLI,
Visual Studio, Azure PowerShell, etc, if so, the provider will use the refresh token for that account and procure
an access token from Azure Active Directory on the user's behalf.

## Running the App
- `dotnet run`
- `curl http://localhost:5000/api/values -v`

## Scenarios
- Running locally
  - Current user should be able to login into the Azure CLI and run the app (`SharedTokenCacheProvider`)
  - User should also be able to set environment vars for Service Principal (`ServicePrincipalTokenProvider`)
- Running in AppService w/ Managed Identity
  - Should be git push app and then curl the endpoint
- Running in Virtual Machine w/ Managed Identity
  - Should be able to run app from either Linux or Windows using Managed Identity on each box

## Deploying the Infrastructure
The script below will log into Azure CLI, execute [Terraform](https://www.terraform.io/downloads.html) and create a .env
file with all required information for accessing the Azure Resources.
```bash
az login
terraform apply -auto-approve
terraform output > .env
```

### Resources Created
- Windows VM w/ Managed Identity
  - Public IP w/ FQDN
- Linux VM w/ Managed Identity
  - Public IP w/ FQDN
- App Service w/ Managed Identity
  - .NET v4.0+

### Tear Down the Resources
```bash
terraform destroy
```
