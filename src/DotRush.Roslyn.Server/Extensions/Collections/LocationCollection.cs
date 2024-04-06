using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Extensions;

public class LocationCollection {
    private readonly HashSet<Location> locations;

    public bool IsEmpty => !this.locations.Any();

    public LocationCollection() {
        this.locations = new HashSet<Location>();
    }

    public LocationCollection Add(Location location) {
        this.locations.Add(location);
        return this;
    }
    public LocationCollection AddRange(IEnumerable<Location?> locations) {
        foreach (var location in locations)
            if (location != null)
                Add(location);

        return this;
    }

    public LocationContainer ToLocationContainer() {
        return new LocationContainer(this.locations);
    }
    public LocationOrLocationLinks ToLocationOrLocationLinks() {
        return new LocationOrLocationLinks(this.locations.Select(loc => new LocationOrLocationLink(loc)));
    }
}