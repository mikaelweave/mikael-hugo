---
author: mikael
# categories: [azure-functions]
date: 2020-10-18 15:00:00
noindex: false
featured_image: go-on-azure-functions-header.png
tag: [azure functions, golang, go, custom handler]
title: Go on Azure Functions with Custom Handlers
meta_description: Investigation of Golang Azure Functions using Custom Handlers
type: post
table_of_contents: true
url: /go-azure-functions
download-files:
  getting-started/host.json: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/getting-started/host.json"
  getting-started/GoCustomHandlers.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/getting-started/GoCustomHandlers.go"
  getting-started/HttpTriggerWithOutputs/function.json: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/getting-started/HttpTriggerWithOutputs/function.json"
  project-restructure/internal/config/config.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/internal/config/config.go"
  project-restructure/internal/config/env.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/internal/config/env.go"
  project-restructure/pkg/api/request.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/pkg/api/request.go"
  project-restructure/pkg/api/response.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/pkg/api/response.go"
  project-restructure/pkg/errors/error.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/pkg/errors/error.go"
  project-restructure/pkg/playground/test.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/pkg/playground/test.go"
  project-restructure/main.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/main.go"
  project-restructure/go.mod: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/go.mod"
  project-restructure/scripts/build.sh: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/scripts/build.sh"
  project-restructure/scripts/run-function.sh: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/scripts/run-function.sh"
  project-restructure/Makefile: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/project-restructure/Makefile"
  basic-playground/.env.sample: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/.env.sample"
  basic-playground/internal/config/config.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/internal/config/config.go"
  basic-playground/internal/config/env.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/internal/config/env.go"
  basic-playground/pkg/playground/model.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/playground/model.go"
  basic-playground/pkg/playground/common.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/playground/common.go"
  basic-playground/pkg/playground/list.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/playground/list.go"
  basic-playground/pkg/playground/get.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/playground/get.go"
  basic-playground/pkg/playground/create.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/playground/create.go"
  basic-playground/pkg/playground/delete.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/playground/delete.go"
  basic-playground/functions/playground-list/function.json: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/functions/playground-list/function.json"
  basic-playground/functions/playground-get/function.json: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/functions/playground-get/function.json"
  basic-playground/functions/playground-create/function.json: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/functions/playground-create/function.json"
  basic-playground/functions/playground-delete/function.json: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/functions/playground-delete/function.json"
  basic-playground/pkg/errors/error.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/errors/error.go"
  basic-playground/main.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/main.go"
  basic-playground/pkg/api/request.go: "https://raw.githubusercontent.com/mikaelweave/go-azfunc-sample/basic-playground/pkg/api/request.go"
---
## Overview

I recently worked on a project where we wanted to port an existing API to Azure. Problem was all the code was written in Go and Azure is not known for its Go support. Enter [Azure Functions custom handlers](https://docs.microsoft.com/en-us/azure/azure-functions/functions-custom-handlers) which allows you to bring your own HTTP server to receive and respond to Azure Functions requests. I was impressed by this preview feature and how easily it enabled us to bring our existing code to Azure. In this post, I'm going to walk through creating some simple Go based Azure Functions to demonstrate how it works.
&nbsp;

![Azure Functions custom handlers overview](azure-functions-custom-handlers-overview.png "Azure Functions custom handlers overview - from Microsoft documentation")
&nbsp;

At a high level, custom handlers enables the Azure Function host to proxy requests to a HTTP server written in any language. The server application must run on the Azure Function host (meaning you may need to cross-compile the application to work on the Function Host's operating system). The only requirements are the server must be able to receive and return HTTP requests in a specific, Azure Function centric format and the server needs to startup and respond in 60 seconds or less.
&nbsp;
&nbsp;
## What We're Building

The problem we're going to solve is the automated creation of developer environments on Azure, called "playgrounds". The goal is to add some automation around the creation of resource groups our developers need to create and test their applications. The requirements our functions needs to meet are:

&nbsp;
- Creation of "Playgrounds" (a.k.a. resource groups) by an administrator and assignment of access to a developer
- Getting list and details of existing "Playgrounds" to help with management
- Deletion of "Playgrounds" once the developer's work has completed

&nbsp;
&nbsp;
![Sample API Azure Function Flow](function-flow.png)

&nbsp;
We also have some requirements for developer experience:
- Short inner loop for local development ([more info](https://mitchdenny.com/the-inner-loop/))
- Automated deployment of infrastructure and code checked into the master branch
- Logging and monitoring for the functions

&nbsp;
&nbsp;
## Testing Go Custom Handlers

&nbsp;
*NOTE: Full code for this section is [here](https://github.com/mikaelweave/go-azfunc-sample/tree/getting-started).*
&nbsp;

The Azure Functions team has provided a [set of basic samples on Github](https://github.com/Azure-Samples/functions-custom-handlers/tree/master/go) that walk through some basic input and output binding scenarios. The [official documentation](https://docs.microsoft.com/en-us/azure/azure-functions/functions-custom-handlers) is also quite good in describing the request and response payload required for writing a custom handler. Let's start with a basic, modified version of the samples repository to see if we can get a simple HTTP function working.

&nbsp;
Let's start with an empty directory and copy over the following files from the samples repository linked above:
- [*host.json*](https://github.com/Azure-Samples/functions-custom-handlers/blob/35cdfc0acb35dfcb02dc855b48388bd9a13b8652/go/host.json)
- [*GoCustomHandlers.go*](https://github.com/Azure-Samples/functions-custom-handlers/blob/35cdfc0acb35dfcb02dc855b48388bd9a13b8652/go/GoCustomHandlers.go)
- [*HttpTriggerWithOutputs/function.json*](https://github.com/Azure-Samples/functions-custom-handlers/blob/35cdfc0acb35dfcb02dc855b48388bd9a13b8652/go/HttpTriggerWithOutputs/function.json)

&nbsp;
We'll need to make some changes in these files to get our basic sample working. Below is the new **host.json** code. The executable name needs to change for the Go HTTP server we'll compile soon. Also, set `enableForwardingHttpRequest` to `false` so we're able to handle other output bindings in the future (like blobs and queues).

&nbsp;
**host.json**
{{< highlightFile "blog/go-azure-functions/getting-started/host.json" json "linenos=table,hl_lines=9 11" >}}
&nbsp;

We'll need to pair down and modify our Go code quite a bit to isolate our sample and properly instruct our output HTTP binding to return the correct headers, status code, and body.
&nbsp;

**GoCustomHandlers.go**
{{< highlightFile "blog/go-azure-functions/getting-started/GoCustomHandlers.go" "go" "linenos=table,hl_lines=18-19 21-24 26-28" >}}
&nbsp;

And finally, we'll need to setup our function bindings. Note the modification of `$return` to a http output - this is required to properly set the `res` output (but may be a bug that will be fixed).
&nbsp;

**HttpTriggerWithOutputs/function.json**
{{< highlightFile "blog/go-azure-functions/getting-started/HttpTriggerWithOutputs/function.json" "json" "linenos=table,hl_lines=17-21" >}}
&nbsp;

Now we just need to compile our Go HTTP server and run our function.

```bash
# Compile the server
go build -o azure-playground-generator
chmod +x azure-playground-generator

# Start Azure Function locally
func host start
```
&nbsp;

This will expose `http://localhost:7071/api/HttpTriggerWithOutputs` which we can test to ensure we are receiving the body, content type, and response code we specified.

![Screenshot showing successful return of getting started Azure Function](getting-started-return-screenshot.png)

&nbsp;
&nbsp;

## Structuring The Project

&nbsp;
*NOTE: Full code for this section is [here](https://github.com/mikaelweave/go-azfunc-sample/tree/project-restructure).*
&nbsp;

Before adding any more functionality to our code, let's spend some time maturing the project structure. I'll be leaning heavily on the project layout pattern as [defined in this repository](https://github.com/golang-standards/project-layout), especially since I'm a Golang newcomer. If you are creating your own Go on Azure project, I recommend reviewing this repository for pointers. Now, we're going to start with the below project structure. The goal is to split out separate responsibilities into separate packages of our project (config, API helpers, standard errors, and a shell for the playground functionality) and isolate the Azure Function specific configuration. We're also adding some automation around building and running the code to reduce our [inner loop](https://mitchdenny.com/the-inner-loop/). 
&nbsp;

```text
.
├── functions
│   ├── HttpTriggerWithOutputs
│   │   └── function.json
│   └── host.json
├── internal
│   └── config
│       ├── config.go
│       └── env.go
├── pkg
│   ├── api
│   │   ├── request.go
│   │   └── response.go
│   ├── errors
│   │   └── error.go
│   └── playground
│       └── test.go
├── scripts
│   ├── build.sh
│   └── run-function.sh
├── Makefile
├── go.mod
└── main.go
```
&nbsp;

First, create a `functions` folder at the root of the repository and copy the `HttpTriggerWithOutputs` folder and the `host.json` file into this folder. While this doesn't follow the guide mentioned above, these files are special enough to warrant their own directory. No changes are required for these files.
&nbsp;

Next, create the `config.go` and `env.go` files inside of `internal/config`. We only have one value for configuration at the moment (the Go server port), but this isolation is useful when adding more configuration. This setup will allow setting our configuration by passing environment variables.
&nbsp;

**internal/config/config.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/internal/config/config.go" "go" "linenos=table" >}}
&nbsp;

**internal/config/env.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/internal/config/config.go" "go" "linenos=table" >}}
&nbsp;

Next, let's isolate API handling specific code into `pkg/api`. Create two files here: `request.go` for helping handle requests, and `response.go` for help with building response objects. As we add more Azure Functions, isolating this code will be useful to standardize how we handle requests and responses.
&nbsp;

**pkg/api/request.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/pkg/api/request.go" "go" "linenos=table" >}}
&nbsp;

**pkg/api/response.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/pkg/api/response.go" "go" "linenos=table" >}}
&nbsp;

Now, lets add a couple standard errors to make creating errors in our application simpler. Create `pkg/errors/error.go` and add the below code.
&nbsp;

**pkg/errors/error.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/pkg/errors/error.go" "go" "linenos=table" >}}
&nbsp;

For the final package in our project, let's stub out the `playground` package and add a simple test method which will return some test data. We're finally making use of the defined `POST` method of our function - here we will simply return the data sent in the request body to verify we are correctly parsing the object from Azure Functions.
&nbsp;

**pkg/playground/test.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/pkg/playground/test.go" "go" "linenos=table" >}}
&nbsp;

To wire all of this together, we need to create two files at the root of the project: `main.go` (which replaces `GoCustomHandlers.go`) and `go.mod` which will allow our project to reference its internal packages.
&nbsp;

**main.go**
{{< highlightFile "blog/go-azure-functions/project-restructure/main.go" "go" "linenos=table" >}}
&nbsp;

**go.mod**
{{< highlightFile "blog/go-azure-functions/project-restructure/go.mod" "go" "linenos=table" >}}
&nbsp;

Let's also improve our development inner loop (the time it takes to build and run our application) to make it easier to test our changes. Create `build.sh` and `run-function.sh` under the `scripts` directory. Once you have done that, create a file called `Makefile` in the root of the repository that we'll use for simple build and run commands. 
&nbsp;

**scripts/build.sh**
{{< highlightFile "blog/go-azure-functions/project-restructure/scripts/build.sh" "sh" "linenos=table" >}}
&nbsp;

**scripts/run-function.sh**
{{< highlightFile "blog/go-azure-functions/project-restructure/scripts/run-function.sh" "sh" "linenos=table" >}}
&nbsp;

**Makefile**
{{< highlightFile "blog/go-azure-functions/project-restructure/Makefile" "make" "linenos=table" >}}
&nbsp;

Now let's test! Fire up the function host with the `make run` command from the root of the repository. This will use the Makefile we just created to build our Go HTTP server and then launch the Azure Functions host for local execution. The function now will handle both GET requests and POST requests with a payload.
&nbsp;

![](restructure-get-screenshot.png)
&nbsp;

![](restructure-post-screenshot.png)
&nbsp;

## Adding "Playground" Functionality

&nbsp;
*NOTE: Full code for this section is [here](https://github.com/mikaelweave/go-azfunc-sample/tree/add-playground-code).*
&nbsp;

The goal of this sample is some light automation around the management of Azure resource groups. In this section, we'll add code to interact with the Azure Management APIs to list, create, get, and delete resource groups, which maps to Playgrounds in our application. We'll be adding actual functionality to our API to accomplish our basic use case to create "Playgrounds" or resource groups for developers to test out their applications. 
&nbsp;

### Authenticating to Azure
&nbsp;
Since the code will be accessing Azure and creating resource groups on the user's behalf, we need to create an Azure service principal that can create resource groups on our behalf. Follow the instructions [here](https://docs.microsoft.com/en-us/cli/azure/create-an-azure-service-principal-azure-cli) to create a service principal with the Azure CLI and then make sure to give it "Contributor" rights to it can create the resource groups and "User Access Manager" rights to it can assign users to resource groups. Create a file named ".env" at the root of the repository in the following format which our Go application will load.
&nbsp;

**.env**
{{< highlightFile "blog/go-azure-functions/basic-playground/.env.sample" "sh" "linenos=table" >}}
&nbsp;

Now let's change our configuration loaded to pull these values from our application. I'll be borrowing heavily from the Azure SDK for Go ([link here](https://github.com/Azure-Samples/azure-sdk-for-go-samples/blob/4e95ecd68b1c0969dc8e7b6bf3a3b2f7e5ecdc76/internal/config/env.go) and [here](https://github.com/Azure-Samples/azure-sdk-for-go-samples/blob/3d51ac8a1a5097b8881a8cf29888d4a44f7205f5/internal/config/config.go)). We need to add the values required by the SDK to access Azure to `config.go` and the appropriate loader to `env.go`.
&nbsp;

**internal/config/config.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/internal/config/config.go" "go" "linenos=table,hl_lines=8-9 16-28" >}}
&nbsp;

**internal/config/env.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/internal/config/env.go" "go" "linenos=table,hl_lines=30-34" "18-36" >}}
&nbsp;

### Accessing Azure with the Go SDK
&nbsp;
Now that we can authenticate to Azure, let's write the code to actually access Azure and translate from the Azure concept of resource groups to our application's concept of Playgrounds. We're going to focus on adding the list, get, create, and delete operations for Playgrounds in this example. But first, let's add some common code to get started.
&nbsp;

First, let's add a model for our "Playground" object. This is almost a direct map from the Azure resource group object, except we have an owner field.
&nbsp;

**pkg/playground/model.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/playground/model.go" "go" "linenos=table" >}}
&nbsp;

The `common.go` file is meant for code that may be used across the package. Here we have the code for the necessary Azure Go SDK clients.
&nbsp;

**pkg/playground/common.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/playground/common.go" "go" "linenos=table" >}}

&nbsp;
Before we forget, we will be needing two more custom errors in our `error.go` file. Add them now so we have something to reference!
&nbsp;

**pkg/errors/error.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/errors/error.go" "go" "linenos=table,hl_lines=49-66" "46-66" >}}

&nbsp;
Now let's start with our CRUD like operations. This code isn't complete - there are edge cases that were not considered in the spirit of this sample.  First we'll retrieve a list of Playgrounds, converted from Azure resource groups. We can identify which resource groups mag to Playgrounds via the `System` tag.
&nbsp;

**pkg/playground/list.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/playground/list.go" "go" "linenos=table" >}}

&nbsp;
Next, to get a specific Playground, we look for a resource group with the same name. We also will throw one of our custom errors if the resource group is not found.
&nbsp;

**pkg/playground/get.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/playground/get.go" "go" "linenos=table" >}}

&nbsp;
Creating a Playground is our most complex function. Here we need to create a resource group and assign the correct permissions to the owner provided. We also check and see if the Playground already exists and return a custom error if so.
&nbsp;

**pkg/playground/create.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/playground/create.go" "go" "linenos=table" >}}

&nbsp;
Here we enable deleting of a Playground given it's name. If the Playground is not found, then we return a custom error again.
&nbsp;

**pkg/playground/delete.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/playground/delete.go" "go" "linenos=table" >}}
&nbsp;

### Wiring up the HTTP server

&nbsp;
We are making progress! Now we need to enable our Playground package to be called from a HTTP request. I've decided to do all of this in the `request.go` file. As you 
&nbsp;

**pkg/api/request.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/pkg/api/request.go" "go" "linenos=table,hl_lines=53-126 135-143 149" "50-150" >}}
&nbsp;

**main.go**
{{< highlightFile "blog/go-azure-functions/basic-playground/main.go" "go" "linenos=table,hl_lines=21-24" "19-29" >}}
&nbsp;

### Adding Function Configuration

We need to add four new function configurations for the four operations we just added so we can expose them to the Azure Function host. Add these 
&nbsp;

**functions/playground-list/function.json**
{{< highlightFile "blog/go-azure-functions/basic-playground/functions/playground-list/function.json" "json" "linenos=table" >}}
&nbsp;

**functions/playground-get/function.json**
{{< highlightFile "blog/go-azure-functions/basic-playground/functions/playground-get/function.json" "json" "linenos=table" >}}
&nbsp;

**functions/playground-create/function.json**
{{< highlightFile "blog/go-azure-functions/basic-playground/functions/playground-create/function.json" "json" "linenos=table" >}}
&nbsp;

**functions/playground-delete/function.json**
{{< highlightFile "blog/go-azure-functions/basic-playground/functions/playground-delete/function.json" "json" "linenos=table" >}}
&nbsp;
&nbsp;

## Automated Deployment
&nbsp;
*Coming Soon!*
&nbsp;
&nbsp;

## Logging and Monitoring
&nbsp;
*Coming Soon!*
&nbsp;
&nbsp;

## More Resources
For more information on Azure Function custom handlers, check out the following resources:
- [Official Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/functions-custom-handlers)
- [Official Samples](https://github.com/Azure-Samples/functions-custom-handlers)
- [Blog post - Write Azure Functions in *any* language with the HTTP Worker (Custom Handler)](https://itnext.io/write-azure-functions-in-any-language-with-the-http-worker-34d01f522bfd)
- [Blog Post - Detailed view on Azure Function Custom Handlers](https://www.serverless360.com/blog/azure-function-custom-handlers)

&nbsp;
&nbsp;

*Disclaimer - While I work at Microsoft, I do not work on Azure Functions or with the Azure Functions team. This post is a analysis of my work with the technology as an end-user. Microsoft and Azure are registered trademarks ot Microsoft Corporation*
