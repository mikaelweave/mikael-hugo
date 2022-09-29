---
author: mikael
# categories: [azure-functions]
date: 2022-09-28 15:00:00
noindex: false
featured_image: easy-postman-and-azure-fhir-configuration-header.png
tag: [azure fhir, postman, oauth]
title: Easy Postman and Azure FHIR Setup
meta_description: Connect Postman to FHIR Service on Azure without any Azure Active Directory Setup
type: post
table_of_contents: true
url: /easy-postman-and-azure-fhir-configuration
download-files:
  InteractiveBrowserCredential.cs: "https://raw.githubusercontent.com/Azure/azure-sdk-for-net/4bd80d7095648246a31d68c27e2e8921f5ef50df/sdk/identity/Azure.Identity/src/Credentials/InteractiveBrowserCredential.cs"
  Constants.cs: "https://raw.githubusercontent.com/Azure/azure-sdk-for-net/4b2579556b7271587d2fb122163e23090a043597/sdk/identity/Azure.Identity/src/Constants.cs"
---
## Overview

Postman is super useful for testing API calls to the FHIR Service in Azure Health Data Services or the Azure API for FHIR. One common annoyance is I've always had to create an Application Registration in Azure Active Directory to access my FHIR Service through either the `client credentials` flow or the `authorization code` flow. Well, you don't need **any** configuration changes in AAD for simple test queries.

&nbsp;
## Postman Setup
&nbsp;

In Postman, go to the "Authorization" tab for your request or folder. Configure a new token and add the below configuration values.

- Callback URL: `http://localhost`
- Authorize using browser: Unchecked
- Auth URL: `https://login.microsoftonline.com/{{tenantId}}/oauth2/v2.0/authorize`
- Access Token URL: `https://login.microsoftonline.com/{{tenantId}}/oauth2/v2.0/token`
- Client ID: `04b07795-8ddb-461a-bbee-02f9e1bf7b46`
- Scope: `{{fhirUrl}/.default`

&nbsp;
The above is assuming you have the following variables set:

- `tenantId`: Your Azure Active Directory Tenant ID (like )
- `fhirUrl`: The base URL for your FHIR Service (like `https://workspace-service.fhir.azurehealthcareapis.com/`)

&nbsp;
![Screen capture showing above configuration in Postman](https://mikaeldevcdn.blob.core.windows.net/blog/easy-postman-and-azure-fhir-configuration/postman-setup-recording.gif)
&nbsp;

Now click the "Get New Access Token" button. This will initiate the login process in a popup window. Once you are logged in, the token will be displayed to you and you can use this token!

&nbsp;
![Screen capture showing getting of token](https://mikaeldevcdn.blob.core.windows.net/blog/easy-postman-and-azure-fhir-configuration/postman-get-token-recording.gif)
&nbsp;

## How Does It Work?

This works seamlessly by using the existing application id from the Azure CLI. Instead of registering a new application, we can piggyback off of Azure CLI, which already exists in every Azure tenant. This is not something I would have thought of, but I found the Azure SDK for .NET is doing this to support the [InteractiveBrowserCredential Class in Azure.Identity](https://learn.microsoft.com/dotnet/api/azure.identity.interactivebrowsercredential).
&nbsp;

**azure-sdk-for-net/sdk/identity/Azure.Identity/src/Credentials/InteractiveBrowserCredential.cs**
{{< highlightFile "blog/easy-postman-and-azure-fhir-configuration/InteractiveBrowserCredential.cs" "c#" "linenos=table,hl_lines=36-36" "32-37" >}}
&nbsp;

**azure-sdk-for-net/sdk/identity/Azure.Identity/src/Constants.cs**
{{< highlightFile "blog/easy-postman-and-azure-fhir-configuration/Constants.cs" "c#" "linenos=table,hl_lines=16-17" "15-18" >}}
&nbsp;

*Disclaimer - While I work at Microsoft on the FHIR Team, this isn't a Microsoft sanctioned approach.*
