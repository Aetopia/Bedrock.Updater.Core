# Bedrock Updater Core

Bedrock Updater Core is an experimental spinoff of [Bedrock Updater](https://github.com/Aetopia/BedrockUpdater).

## Why?

Bedrock Updater directly interacts with Microsoft Store endpoints to resolve & update app packages related to Minecraft: Bedrock Edition. 
Since these endpoints aren't suppose to public, a lot of code needs to be handwritten to interact with endpoints. 

When packages need to be resolved, the endpoints require a list of non-leaf update IDs. 
These are used to determine what package versions to return. 
The main concern is that there is no mechanism to resolve these non-leaf Update IDs **publicly**.

Bedrock Updater ships with a minimum list of non-leaf update IDs. Enough that the endpoints can return a result.
Currently that suffices the functionality of the project with 0 issues.

For the purpose of futureproofing Bedrock Updater, Bedrock Updater Core was created.

## How?

Bedrock Updater Core leverages the Windows Runtime to provide the ability to install & update Minecraft: Bedrock Edition.
The project uses [`Windows.ApplicationModel.Store.Preview.InstallControl.AppInstallManager`](https://learn.microsoft.com/uwp/api/windows.applicationmodel.store.preview.installcontrol.appinstallmanager) to install & update Minecraft: Bedrock Edition.

It does much of the heavy lifting of interacting with Microsoft Store endpoints, it provides more of a pre-made abstraction but an official one.

## Building
1. Download the following:
    - [.NET SDK](https://dotnet.microsoft.com/en-us/download)
    - [.NET Framework 4.8.1 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net481-developer-pack-offline-installer)

2. Run the following command to compile:

    ```cmd
    dotnet publish "src\Bedrock.Updater.Core.csproj"
    ```
