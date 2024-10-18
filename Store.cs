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

    internal static readonly AppInstallManager AppInstallManager = new();

    static readonly PackageManager packageManager = new();

    internal static IEnumerable<AppInstallItem> Get(params string[] @productIds)
    {
        foreach (var productId in productIds)
        {
            using var stream = client.OpenRead(string.Format(address, productId));
            using var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max);
            var element = XElement.Load(reader);

            var appInstallItem = (packageManager.FindPackagesForUser(string.Empty, element.Descendants("PackageFamilyName").First().Value).Any()
            ? AppInstallManager.SearchForUpdatesAsync(productId, element.Descendants("PreferredSkuId").First().Value)
            : AppInstallManager.StartAppInstallAsync(productId, element.Descendants("PreferredSkuId").First().Value, false, false))?.AsTask().Result;

            if (appInstallItem is not null)
            {
                AppInstallManager.MoveToFrontOfDownloadQueue(productId, string.Empty);
                yield return appInstallItem;
            }
        }
    }
}