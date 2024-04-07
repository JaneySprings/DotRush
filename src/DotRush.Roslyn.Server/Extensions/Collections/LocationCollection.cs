using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Extensions;

public class LocationCollection {
    private readonly HashSet<Location> locations;

    public bool IsEmpty => locations.Count == 0;

    public LocationCollection() {
        locations = new HashSet<Location>();
    }

    public LocationCollection Add(Location location) {
        locations.Add(location);
        return this;
    }
    public LocationCollection AddRange(IEnumerable<Location?> locations) {
        foreach (var location in locations)
            if (location != null)
                Add(location);

        return this;
    }

    public LocationContainer ToLocationContainer() {
        return new LocationContainer(locations);
    }
    public LocationOrLocationLinks ToLocationOrLocationLinks() {
        return new LocationOrLocationLinks(locations.Select(loc => new LocationOrLocationLink(loc)));
    }
}