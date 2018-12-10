*Previously in thinkbeforecoding...*

We've seen in [previous post](/post/2018/12/07/full-fsharp-blog) how to process 
markdown and F# script to generate static HTML content.

We'll now see how to host it on Azure Functions.

## The plan

Azure functions is a FaaS platform, but we'll write no code for it. Using consumption plan,
we'll pay only for used resource:

* blob storage to store posts and functions definitions: currently €0.0166/GB/month in West Europe region
* Read operations: €0.0037/10.000reads/month
* Write (when uploading and some logs): €0.0456/10000writes/month

in my case, I get 0.01€ for reads and 0.01€ for writes... you mileage may vary depending
on the trafic on your site.

Functions provide custom domain support for free, and you can install your own SSL certificate.

## Creating functions

First install azure [command line](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest).

and login using the az CLI tool:

    az login

you'll be prompted for your azure credentials, then select your subscription:

    az account set --subscription <mysubscription>

To create functions, we firt need a resource group. This is a unit for resource
definition and grouping in azure. You can skip this step if you plan to put your functions
in an existing group:

    az group create --name staticapp --location westeurope

You can of course change the location if you prefere to host it closer from your audience.

Then we have to create a storage that will contain the function app storage:

    az storage account create --name staticapp --resource-group staticapp --sku STANDARD_LRS

And we can now create the function app:

    az functionapp create --name staticblog --resource-group staticapp --storage staticapp --consumption-plan-location westeurope --os-type windows --runtime dotnet

And activate HTTP2, better especially for browsing from mobile phone :

    az functionapp config set --http20-enabled --name staticblog --resource-group staticapp

## Custom host name

If you already own a domain name you can use it to point to your azure functions.

in you DNS settings, add a CNAME record that point to the function app:

    www 1800 IN CNAME staticblog.azurewebsites.net

then add it to your azure functions:

    az functionapp config hostname add --hostname www.yourdomain.com --name staticblog -g staticapp

note that azure performs a domain ownership test, so this command will succeed only once 
DNS name has propagated.

## SSL

If you already own a SSL certificate, you can install it.

Next part of this series will cover how to use let's encrypt to automatically generate and
install free SSL certificates.

## Uploading content in blobs

First we can create a container for our blog posts:

    az storage container create --name blog --public-access blob --account-name staticapp

We use a public read access, since the content will be on the internet anyway.

Let's create a sample page:

    <!DOCTYPE html>
    <html>
        <head>My static blog</head>
        <body>
            <h1>My static blog in azure functions !</h1>
        </body>
    </html>

you can do something more fancy using what we've seen in previous post.

Now we upload it to the blob storage:

    az storage blob upload --container blog --file .\index.html --name index.html --content-type text/html --account-name staticapp

## Setting the proxy

Now we can define the proxy inside the functionapp. For this, create a `proxies.json` file
with the following content:

    {
        "$schema": "http://json.schemastore.org/proxies",
        "proxies": {
            "Posts": {
                "matchCondition": {
                    "route": "post/{*path}",
                    "methods": [
                        "GET",
                        "HEAD"
                    ]
                },
                "backendUri": "https://%Blog.Storage%.blob.core.windows.net/%Blog.Container%/posts/{path}"
            },
            "public": {
                "matchCondition": {
                    "route": "public/{*path}",
                    "methods": [
                        "GET",
                        "HEAD"
                    ]
                },
                "backendUri": "https://%Blog.Storage%.blob.core.windows.net/%Blog.Container%/media/{path}"
            },
            "content": {
                "matchCondition": {
                    "route": "content/{*path}"
                },
                "backendUri": "https://%Blog.Storage%.blob.core.windows.net/%Blog.Container%/content/{path}"
            },
            "Index": {
                "matchCondition": {
                    "route": "/"
                },
                "backendUri": "https://%Blog.Storage%.blob.core.windows.net/%Blog.Container%/index.html"
            },
            "feed": {
                "matchCondition": {
                    "route": "/feed/atom",
                    "methods": [
                        "GET",
                        "HEAD",
                        "OPTIONS"
                    ]
                },
                "backendUri": "https://%Blog.Storage%.blob.core.windows.net/%Blog.Container%/feed/atom"
            },
            "redirect": {
                "matchCondition": {
                    "route": "post/oldurl"
                },
                "responseOverrides": {
                    "response.statusCode": "301",
                    "response.statusReason": "Redirect",
                    "response.headers.Location": "post/newurl"
                }
            }
        }
    }

This proxy redirect each call to post/ path to corresponding blob.
The Index rule is used to server the index.html page on /.

You can also see how to set a redirect in case you need it.

To run, the function also need a `host.json` file:

    {
        "version": "2.0",
        "logger": {
          "categoryFilter": {
            "defaultLevel": "Error",
            "categoryLevels": {
              "Host": "Information",
              "Function": "Error",
              "Host.Aggregator": "Error"
            }
          }
        },
        "applicationInsights": {
          "sampling": {
            "isEnabled": true,
            "maxTelemetryItemsPerSecond": 1
          }
        },
        "tracing": {
          "consoleLevel": "error",
          "fileLoggingMode": "debugOnly"
        },
        "aggregator": {
          "batchSize": 1000,
          "flushTimeout": "00:01:00"
        }
      }

The only mandatory parameter is version, the rest is used to reduce the amount of tracing.
Adjust it to your needs but lots of appInsight metrics can increase the overall price of hosting.

We can upload it to the application using the zip upload:

    rm blog.zip
    dir *.json | Compress-Archive -DestinationPath blog.zip -Update
    az functionapp deployment source config-zip --src .\blog.zip -n staticblog -g staticapp --verbose

I use here powershell but you can easily use zip to do the same on linux or mac.
The zip only contains the `host.json` and `proxies.json`. All that is actually needed
to run the function app.

The last step is to set the value of the Blog.Storage and Blog.Container app settings that
are used in the proxies paths:

    az functionapp config appsettings set --settings Blog.Storage=staticapp --name staticblog --resource-group staticapp
    az functionapp config appsettings set --settings Blog.Container=blog --name staticblog --resource-group staticapp

you can now go to https://staticblog.azurewebsites.net/ and enjoy your just deployed static website. You can
also test it with your own domain name if you set it up.

### Staging

You can use slots in azure functions for staging. Creating slots doesn't seem available on
the command line so you'll have to use the azure portal for this.

The interesting thing with staging is that you can then upload a new version of your proxy
in staging while previous version is still running in production. You can also push
your content to a new container and set a different version of the Blog.Container app settings for staging only.

Once tested, you can then swap staging and production to release the new version.

### Content Type

It is advised to set the content type correctly on your blobs so that browser display
them appropriatly. You can use the `--content-type` parameter in the `az strage blob upload`
command.


### Gzip

It's recommended to gzip static content to reduce storage and bandwith.

You can gzip your static files locally and add the `--content-encoding gzip` parameter
while uploading blobs.

### Cache policy

You can use the `--content-cache-control` parameter while uploading blobs to set the
cache control header. This way you can reduce even more the bandwith used by your application.

## Conclusion

Using all this, you should be able to publish you static web site quickly to azure and 
start to see trafic flow !

See you next time for some letsencrypt fun !