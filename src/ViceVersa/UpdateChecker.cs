using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace ViceVersa;

/// <summary>
/// Asks the GitHub releases API whether a newer version exists. Nothing is
/// downloaded and nothing is installed. The check reports what it found and the
/// user decides what to do about it.
/// </summary>
public static class UpdateChecker
{
    /// <summary>Where the user is sent when they want the new build.</summary>
    public const string ReleasesPageUrl = "https://github.com/matanlevanon/Vice-Versa/releases/latest";

    private const string LatestReleaseApi =
        "https://api.github.com/repos/matanlevanon/Vice-Versa/releases/latest";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public enum Outcome
    {
        UpToDate,
        UpdateAvailable,
        Failed
    }

    public sealed class Result
    {
        public Outcome Outcome { get; init; }

        /// <summary>Version found on GitHub, empty when the check failed.</summary>
        public string LatestVersion { get; init; } = string.Empty;

        /// <summary>Why the check failed, empty otherwise.</summary>
        public string Message { get; init; } = string.Empty;
    }

    public static Version CurrentVersion =>
        Normalise(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    /// <summary>
    /// Drops the revision field. A tag reads "1.0.0" and the assembly reports
    /// "1.0.0.0". Version treats those as different, because an absent revision
    /// sorts below a zero one, so without this every check claims an update.
    /// </summary>
    public static Version Normalise(Version version) =>
        new(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);

    /// <summary>Turns a release tag such as "v1.2.0" into a comparable version.</summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string trimmed = tag.Trim();

        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        // Drop a pre-release suffix, for example the "-beta.1" in "1.2.0-beta.1".
        int dash = trimmed.IndexOf('-');

        if (dash >= 0)
        {
            trimmed = trimmed[..dash];
        }

        if (!Version.TryParse(trimmed, out Version? parsed) || parsed is null)
        {
            return false;
        }

        version = Normalise(parsed);
        return true;
    }

    /// <summary>True when the tag names a version above the one supplied.</summary>
    public static bool IsNewer(string? tag, Version current) =>
        TryParseTag(tag, out Version candidate) && candidate > Normalise(current);

    public static async Task<Result> CheckAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = Timeout };

            // GitHub rejects requests without a User-Agent.
            client.DefaultRequestHeaders.Add("User-Agent", "ViceVersa-update-check");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            using HttpResponseMessage response = await client.GetAsync(LatestReleaseApi).ConfigureAwait(true);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Failure(
                    "No published release was found. A private repository answers this way too, " +
                    "so update checks start working once it is public.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return Failure("GitHub refused the request. Its rate limit is the usual reason. Try again later.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failure($"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            using JsonDocument document = JsonDocument.Parse(json);

            string? tag = document.RootElement.TryGetProperty("tag_name", out JsonElement element)
                ? element.GetString()
                : null;

            if (!TryParseTag(tag, out Version latest))
            {
                return Failure("The latest release carries no version tag Vice Versa understands.");
            }

            return new Result
            {
                Outcome = latest > CurrentVersion ? Outcome.UpdateAvailable : Outcome.UpToDate,
                LatestVersion = latest.ToString(3)
            };
        }
        catch (TaskCanceledException)
        {
            return Failure("The check timed out.");
        }
        catch (HttpRequestException exception)
        {
            return Failure("Could not reach GitHub. " + exception.Message);
        }
        catch (JsonException)
        {
            return Failure("GitHub returned something Vice Versa could not read.");
        }
        catch (Exception exception)
        {
            return Failure(exception.Message);
        }
    }

    private static Result Failure(string message) => new()
    {
        Outcome = Outcome.Failed,
        Message = message
    };
}
