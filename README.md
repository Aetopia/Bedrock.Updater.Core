> [!IMPORTANT]
> Bedrock Updater [`v3.0.0`](https://github.com/Aetopia/BedrockUpdater/releases/tag/v3.0.0) now uses  [`Windows.ApplicationModel.Store.Preview.InstallControl.AppInstallManager`](https://learn.microsoft.com/uwp/api/windows.applicationmodel.store.preview.installcontrol.appinstallmanager).

# Bedrock Updater Core

Bedrock Updater Core is an experimental spinoff of [Bedrock Updater](https://github.com/Aetopia/BedrockUpdater).

## Why?

Bedrock Updater directly interacts with Microsoft Store endpoints to resolve & update app packages related to Minecraft: Bedrock Edition. 
Since these endpoints aren't suppose to public, a lot of code needs to be handwritten to interact with these endpoints. 

When packages need to be resolved, the endpoints require a list of non-leaf update IDs. 
These are used to determine what package versions to return. 
The main concern is that there is no mechanism to resolve these non-leaf Update IDs **publicly**.

Bedrock Updater ships with a minimum list of non-leaf update IDs. Enough that the endpoints can return a result.
Currently that suffices the functionality of the project with 0 issues.

For the purpose of futureproofing Bedrock Updater, Bedrock Updater Core was created.

The Windows Runtime provides a proper API called [`Windows.ApplicationModel.Store.Preview.InstallControl.AppInstallManager`](https://learn.microsoft.com/uwp/api/windows.applicationmodel.store.preview.installcontrol.appinstallmanager) to install & update apps from the Microsoft Store.

Bedrock Updater Core leverages this API to streamline & simplify operations to update & install Minecraft: Bedrock Edition. 

## Building
1. Download the following:
    - [.NET SDK](https://dotnet.microsoft.com/en-us/download)
    - [.NET Framework 4.8.1 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net481-developer-pack-offline-installer)

2. Run the following command to compile:

    ```cmd
    dotnet publish "src\Bedrock.Updater.Core.csproj"
    ```
