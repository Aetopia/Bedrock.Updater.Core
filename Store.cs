using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using Windows.ApplicationModel.Store.Preview.InstallControl;
using Windows.Globalization;
using Windows.Management.Deployment;

static class Store
{
    static readonly string address = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{{0}}?languages=iv&market={new GeographicRegion().CodeTwoLetter}";

    static readonly WebClient client = new();

    static readonly AppInstallManager appInstallManager = new();

    static readonly PackageManager packageManager = new();

    static readonly AppUpdateOptions updateOptions = new() { AutomaticallyDownloadAndInstallUpdateIfFound = true };

    // Attempt to request installation of the specified product IDs.
    internal static IEnumerable<AppInstallItem> Get(params string[] @productIds) => productIds.Select(productId =>
    {
        // Try to get an existing app from the queue.
        var appInstallItem = appInstallManager.AppInstallItems.FirstOrDefault(_ => _.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));

        // If we couldn't get any existing app from the queue then resolve & add it.
        if (appInstallItem is null)
        {
            var (packageFamilyName, skuId) = string.Format(address, productId).Get();
            appInstallItem = (packageManager.FindPackagesForUser(string.Empty, packageFamilyName).Any()
            ? appInstallManager.SearchForUpdatesAsync(productId, skuId, string.Empty, string.Empty, updateOptions)
            : appInstallManager.StartAppInstallAsync(productId, skuId, false, false))?.AsTask().Result;
        }

        // Prioritize our app's installation by moving to the front of the queue.  
        if (appInstallItem is not null) appInstallManager.MoveToFrontOfDownloadQueue(productId, string.Empty);

        return appInstallItem;
    });

    // Resolve the package family name & SKU ID.
    static (string, string) Get(this string address)
    {
        using var stream = client.OpenRead(address);
        using var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max);
        var element = XElement.Load(reader);

        return (element.Descendants("PackageFamilyName").First().Value, element.Descendants("PreferredSkuId").First().Value);
    }
}