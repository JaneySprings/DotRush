using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Containers;

public class LocationsContainer {
    private readonly HashSet<Location> locations = new HashSet<Location>();
    public bool IsEmpty => locations.Count == 0;

    public LocationsContainer Add(Location location) {
        locations.Add(location);
        return this;
    }
    public LocationsContainer AddRange(IEnumerable<Location?> locations) {
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