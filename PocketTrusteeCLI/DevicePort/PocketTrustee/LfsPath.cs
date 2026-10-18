namespace PocketTrusteeCLI.DevicePort.PocketTrustee;

internal static class LfsPath {
    public static string Resolve(string currentPath, string path) {
        var combinedPath = path.StartsWith('/')
            ? path
            : $"{currentPath.TrimEnd('/')}/{path}";
        return Normalize(combinedPath);
    }

    public static string Normalize(string path) {
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            switch (segment) {
                case ".":
                    break;
                case "..":
                    if (segments.Count > 0) {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        return segments.Count == 0 ? "/" : $"/{string.Join('/', segments)}";
    }

    public static (string Parent, string Name) GetParentAndName(string path) {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash <= 0
            ? ("/", path[(lastSlash + 1)..])
            : (path[..lastSlash], path[(lastSlash + 1)..]);
    }

    public static bool IsSelfOrDescendant(string path, string ancestor) {
        if (string.Equals(path, ancestor, StringComparison.Ordinal)) {
            return true;
        }

        if (ancestor == "/") {
            return path.StartsWith('/');
        }

        return path.StartsWith($"{ancestor}/", StringComparison.Ordinal);
    }

    public static string Rebase(string path, string source, string destination) {
        if (string.Equals(path, source, StringComparison.Ordinal)) {
            return destination;
        }

        var suffix = path[source.Length..].TrimStart('/');
        return Normalize($"{destination}/{suffix}");
    }
}
