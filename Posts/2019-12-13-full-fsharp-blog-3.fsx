(*** hide ***)

#r "nuget: Microsoft.NET.Sdk.Functions"

(**
I promised a third part in the this FSharp.Blog series, and take the occasion
of this new edition of the [F# Advent Calendar](https://sergeytihon.com/2019/11/05/f-advent-calendar-in-english-2019/) to write it !

This time we'll see how to install autorenewable letsencrypt certificates
on azure functions.  

You probably already know [letsencrypt](https://letsencrypt.org/), they provide free SSL certificates for everybody. 
Having a public website today without HTTPS is totally discouraged for security and privacy reasons, and most major
browser issue warnings even if no form is posted. Using HTTPS on a static readonly web site is highly advise to
prevent Man in the middle HTTP or script injection while on untrusted wifi.

The only requirement for letscencrypt is to prove that you own the website for which you request a
certificate. For this, letsencrypt uses a challenge, a single use file that you have to serve from
your web site and that will assert you have administrative rights on it.

## Overview

For this, we will use the [Let's encrypt extension](https://github.com/sjkp/letsencrypt-siteextension) 
and automate the certificate renewal with a timer trigger. On one side we'll have 
an azure function app with the extension and the trigger, and on the other side, your blog where you
want to install the certificate. 

Let's first create a function app. For this we need to install the templates first:

    [lang=bash]
    dotnet new -i Microsoft.Azure.WebJobs.ProjectTemplates::3.0.10379

The create a directory, and create the app inside

    [lang=bash]
    mkdir letsencryptadvent
    cd letsencryptadvent
    dotnet new func -lang F#

The only other dependency we need is the [TaskBuild.fs](https://www.nuget.org/packages/TaskBuilder.fs) nuget
to interop with task more easily:

    [lang=bash]
    dotnet add package TaskBuilder.fs 
    dotnet restore

Create a timer trigger function with the following command:

    [lang=bash]
    dotnet new TimerTrigger -lang F# -n Renew

Now open the project in your favorite editor. Set the host.json file as "Copy if newer" using the IDE,
or add the following lines to the fsproj:

    [lang=xml]
    <ItemGroup>
      <None Update="host.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      </None>
      <None Update="local.settings.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <CopyToPublishDirectory>Never</CopyToPublishDirectory>
      </None>
    </ItemGroup>
        
Create a LetsEncrypt.fs file and add it as well as the Renew.fs to the project:

    [lang=xml]
    <ItemGroup>
      <Compile Include="LetsEncrypt.fs" />
      <Compile Include="Renew.fs" />
    </ItemGroup>

Start the file with:
    
    [lang=fs]
    module LetsEncrypt

and type inside:
*)
module LetsEncrypt =
    open System
    open System.Text
    open System.Net.Http
    open Newtonsoft.Json
    open Microsoft.Azure.WebJobs.Host
    open Microsoft.Extensions.Logging


    type Request = {
        AzureEnvironment: AzureEnvironment
        AcmeConfig: AcmeConfig
        CertificateSettings: CertificateSettings
        AuthorizationChallengeProviderConfig: AuthorizationChallengeProviderConfig
    }
    and AzureEnvironment = {
        WebAppName: string
        ClientId: string
        ClientSecret: string
        ResourceGroupName: string
        SubscriptionId: string
        Tenant: string
    }
    and AcmeConfig = {
        RegistrationEmail: string
        Host: string
        AlternateNames: string[]
        RSAKeyLength: int
        PFXPassword: string
        UseProduction: bool
    }
    and CertificateSettings = {
        UseIPBasedSSL: bool 
    }
    and AuthorizationChallengeProviderConfig = {
        DisableWebConfigUpdate: bool
    }

    type ServiceResponse = {
        CertificateInfo: CertificateInfo
    }
    and CertificateInfo = {
        PfxCertificate: string
        Password: string
    }

    type [<Struct>] WebApp = WebApp of string
    type [<Struct>] ResourceGroup = ResourceGroup of string
    type [<Struct>] Host = Host of string

    let env name = Environment.GetEnvironmentVariable(name)
    let basic username password = 
        sprintf "%s:%s" username password
        |> Encoding.UTF8.GetBytes
        |> Convert.ToBase64String

    let RenewCert (log: ILogger) (ResourceGroup resourceGroup) (WebApp webApp) (Host host) =
        task {
            try
                let tenant = env "Tenant"
                let subscriptionId = env "Subscription.Id"

                let publishUrl = env "Publish.Url"
                let username = env "Publish.UserName"
                let password = env "Publish.Password"

                let clientId = env "Client.Id"
                let clientSecret = env "Client.Secret"

                let registrationEmail = env "Registration.Email"
                let certificatePassword = env "Cert.Password"
                use client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Authorization", 
                        "Basic " + basic username password )
                |> ignore
            
                
                let body = {
                    AzureEnvironment = 
                        {   //AzureWebSitesDefaultDomainName = "string", 
                            //    Defaults to azurewebsites.net
                            //ServicePlanResourceGroupName = "string",
                            //    Defaults to ResourceGroupName
                            //SiteSlotName = "string",
                            //    Not required if site slots isn't used
                            WebAppName = webApp
                            //AuthenticationEndpoint = "string",
                            //    Defaults to https://login.windows.net/
                            ClientId = clientId
                            ClientSecret = clientSecret
                            //ManagementEndpoint = "string", 
                            //    Defaults to https://management.azure.com
                            //Resource group of the web app 
                            ResourceGroupName = resourceGroup 
                            SubscriptionId = subscriptionId
                            Tenant = tenant //Azure AD tenant ID 
                            //TokenAudience = "string"
                            //    Defaults to https://management.core.windows.net/
                        }
                    AcmeConfig = 
                        {   RegistrationEmail = registrationEmail
                            Host = host
                            AlternateNames =  [||]
                            RSAKeyLength = 2048
                            //Replace with your own
                            PFXPassword = certificatePassword  
                            UseProduction = true 
                            // Replace with true if you want
                            // production certificate from Lets Encrypt 
                        }
                    CertificateSettings = 
                        {   UseIPBasedSSL = false }
                    AuthorizationChallengeProviderConfig = 
                        {   DisableWebConfigUpdate = false }
                }

                log.LogInformation("Post request")
                let uri = sprintf "https://%s/letsencrypt/api/certificates/challengeprovider/http/kudu/certificateinstall/azurewebapp?api-version=2017-09-01" publishUrl
                let! res = client.PostAsync(
                                uri, 
                                new StringContent(JsonConvert.SerializeObject(body), 
                                                  Encoding.UTF8, "application/json")) 

                let! value = res.Content.ReadAsStringAsync()
                let response  = value
                return Ok response

            with
            | ex ->
                log.LogError(sprintf "Error while renewing cert: %O" ex, ex)
                return Error ex
        }

(**
It's a bit long but actually really simple. It crafts a json http request on the extension
and get the result.

The three parameters resourceGroup, webApp and host are the one of your blog azure function app.
The host is the host name you need a certificate for. In the case of this blog, this is simply **thinkbeforecoding.com**.

The function also use a lot of environment variables.

**Tenant** and **Subscription.Id** are your azure account information. You can find
them when you switch account/subscription. The Tenant ends with *.onmicrosoft.com* and the 
subscription id is a guid.

The **Publish.Url**, **Publish.UserName** and **Publish.Password** are Kudu publish profile
credentials of your timer trigger functions used to call the extension.

The **Client.Id** and **Client.Secret** are the appId and secret of an Active Directory
account that has Contributor access to your blog functions resource group. We'll see
how to configure this later.

The **Registration.Email** is the email sent to Let's Encrypt. They use it only
to notify you when the certificate is about to expire.

Finally **Cert.Password** is a password used to protect generated pfx. You may have
to use it if you wan't to reuse an existing certificate.

When called with the Publish info, the extension checks that it can access your blog function app using
provided Client account. Then it calls Let's encrypt to request a certificate. Let's encrypt
responds with a challenge. The extension then takes the challenge and creates a file in a .well-known directory
in your blog function. Let's encrypt calls your blog to check the presence of the challenge, and in case of success,
creates the certificate. The extension finally installs this certificate in your blog SSL configuration.

Let's call this RenewCert function from the trigger. In the Renew.fs file, type:
*)

open System
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Logging
open System.Threading.Tasks

open LetsEncrypt

module Renew =
    [<FunctionName("Renew")>]
    let Run([<TimerTrigger("0 3 2 24 * *") >] myTimer: TimerInfo,  log : ILogger) =
        task {
            sprintf "Timer function to renew myblog.com executed at: %O" DateTime.Now
            |> log.LogInformation

            let! result = 
                LetsEncrypt.RenewCert 
                            log 
                            (ResourceGroup "blog-resourcegroup") 
                            (WebApp "blog-func") 
                            (Host "myblog.com")

            log.LogInformation "Cert for myblog.com renewed"

            match result with
            | Ok success -> log.LogInformation(string success)
            | Error err -> log.LogError(string err)
        } :> Task

(**
The cron expresion is meant to run on each 24th of the month a 02:03. It's
a good idea to not run the trigger at midnight to avoid renewing certificates at 
the same time as everybody else...

## Deployment

Our Renew function is ready, use the az cli to deploy it.

First login and select the subscription

    [lang=bash]
    az login
    az account  set --subscription yoursubscription

Create a resource group and a storage account. You can of course use a different region

    [lang=bash]
    az group create --name letsencryptadvent --location NorthEurope
    az storage account create --name letsencryptadvent -g letsencryptadvent  --sku Standard_LRS

We can now proceed to the functions creation:

    [lang=bash]
    az functionapp create --name letsencryptadvent -g letsencryptadvent --storage-account letsencryptadvent --consumption-plan-location NorthEurope --os-type Windows --runtime dotnet

Next step is to deploy our code:

    [lang=bash]
    dotnet publish -c release -o deploy
    ls ./deploy/* | Compress-Archive  -DestinationPath deploy.zip
    az functionapp deployment source config-zip --src deploy.zip --name letsencryptadvent -g letsencryptadvent

## Extension installation

On the portal, in the letsencryptadvent functions, go to the extensions tab,
and click Add. Find and select the "Let's Encrypt (No web Jobs)" extensions. Accept
the legal terms and OK. 

## Access Rights

We need to create a client user and give it appropriate access rights on the blog: 

    [lang=bash]
    az ad app create --display-name letsencryptadvent --password '$ecureP@ssw0rd'

I've not found yet how to fully configure this account using the cli... So login to [azure portal](https://portal.azure.com/) 
and go to the Active Directory settings. Click on the App registrations tab and select the letsencryptadvent application.

In the overview tab, the Managed application in local directory is not set, just click on the link. It will create the user.
Don't forget to note the account App Id for later use.

Now go to your blog resource group, and open the Access Control (IAM) tab. Once there, add 
a role assignment. Select the Contributor role, and select the letencryptadvent account, and save.
Alternatively, you can use the following command:

    [lang=bash]
    az role assignment create --role Contributor --assignee 'the account appid' -g yourblog-resourcegroup

The account we created now has access to your blog to upload the challenge and install the certificate.

We can set the environment variables for the client:

    [lang=bash]
    az functionapp config appsettings set --settings 'Client.Id=theappid' \
        --name letsencryptadvent -g letsencryptadvent
    az functionapp config appsettings set --settings 'Client.Secret=$ecureP@ssw0rd' \
        --name letsencryptadvent -g letsencryptadvent


## Publish information

To get the publish profile information, use the azure cli: 
    
    [lang=bash]
    az webapp deployment list-publishing-profiles -n letsencryptadvent -g letsencryptadvent

get the publish url, the username and password, and set the environment variables:

    [lang=bash]
    az functionapp config appsettings set --settings 'Publish.Url=letsencryptadvent.scm.azurewebsites.net' \
        --name letsencryptadvent -g letsencryptadvent
    az functionapp config appsettings set --settings 'Publish.UserName=$letsencryptadvent' \
        --name letsencryptadvent -g letsencryptadvent
    az functionapp config appsettings set --settings 'Publish.Password=...' \
        --name letsencryptadvent -g letsencryptadvent


Don't forget to set the Subscription.Id and Tenant, as well as the Registration.Email and Cert.Password with your own values.

## Responding to the challenge

Let's encrypt will call your blog app to check the challenge.
The extension puts the challenge in a file on the local disk, and we have
to serve it.

For this we'll create a function !

In your blog functions code, add a 'challenge' http trigger:
*)
open System
open System.IO
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Newtonsoft.Json
open Microsoft.Extensions.Logging
open System.Net.Http
open System.Net

module challenge =
    [<FunctionName("challenge")>]
    let Run([<HttpTrigger(AuthorizationLevel.Anonymous, [| "get" |],Route="acme-challenge/{code}")>]req: HttpRequestMessage, code: string, log: ILogger) =
        task {
            try
                log.LogInformation (sprintf "chalenge for: %s" code)
                let content = File.ReadAllText(@"D:\home\site\wwwroot\.well-known\acme-challenge\"+code);
                log.LogInformation("Challenge found")
                return new HttpResponseMessage(
                        Content = new StringContent(content, Text.Encoding.UTF8, "text/plain") )
            with
            | ex ->
                log.LogError(ex.Message)
                return raise (AggregateException ex)
        } 

(**

It just loads the challenge file from local disk and returns its content.

We also have to add a proxy rule to serve it on the expected path. For this
create a proxies.json file or edit existing one:

    [lang=json]
    {
      "$schema": "http://json.schemastore.org/proxies",
      "proxies": {
        "acmechallenge": {
          "matchCondition": {
            "route": "/.well-known/acme-challenge/{*code}"
          },
          "backendUri": "https://localhost/api/acme-challenge/{code}"
        }
      }
    }

Place it as the first rule to be sure the path is not overloaded by a
less specific one.

You can now republish you blog with this function.

## Test

To generate your first certificate, go to the letsencryptadvent function, and trigger
the timer manually.

You'll see the output in the log console. You can also follow the activity with
the following command:

    [lang=bash]
    func azure functionapp logstream letsencryptadvent


Once exectued, you should be able to go to your blog with HTTPS. You can 
check that the certificate is correctly installed in the SSL tab.

Errors are logged, so you should be able to troubleshoot problems as they occure.

## Improvements

Storing the passwords as plain text in the configuration is highly discouraged.

Hopefully, you can store them in a keyvault and use a key vault reference as a parameter value:

    [lang=none]
    @Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/secret/guid)

Don't forget to give read access to your function app to the key vault.

When the function runtime finds parameters like this, it loads them from keyvault, decrypts them
and pass them as environment variable to the function.


Now, enjoy your auto renewed certificates every month !!

Happy xmas

*)





