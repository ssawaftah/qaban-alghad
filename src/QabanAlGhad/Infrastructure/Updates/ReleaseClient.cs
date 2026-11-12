using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WeighbridgeBridge
{
    public sealed class AvailableUpdate
    {
        public string Version, Url, Sha256;
        public long Size;
    }

    // GitHub is the publisher trust root. No PAT, cookie or API credential is used.
    // The digest comes from the pinned GitHub API, never from the downloaded ZIP.
    public static class ReleaseClient
    {
        public const string Repository = "ssawaftah/qaban-alghad";
        public const long MaximumPackageBytes = 32 * 1024 * 1024;
        const string Api = "https://api.github.com/repos/" + Repository + "/releases/latest";

        public static AvailableUpdate Parse(string json, Version current)
        {
            var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("Invalid release response.");
            if (Convert.ToBoolean(root["draft"]) || Convert.ToBoolean(root["prerelease"])) return null;
            string tag = Convert.ToString(root["tag_name"]);
            if (!Regex.IsMatch(tag ?? "", @"^v\d+\.\d+\.\d+$")) throw new InvalidDataException("Invalid version.");
            Version version;
            if (!Version.TryParse(tag.Substring(1), out version)) throw new InvalidDataException("Invalid version.");
            if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;
            var assets = root["assets"] as object[];
            if (assets == null) throw new InvalidDataException("Missing release assets.");
            AvailableUpdate result = null;
            foreach (var value in assets)
            {
                var asset = value as Dictionary<string, object>;
                if (asset == null || Convert.ToString(asset["name"]) != "QabanAlGhad-" + version + ".zip") continue;
                if (result != null) throw new InvalidDataException("Duplicate release asset.");
                object digest;
                string hash = asset.TryGetValue("digest", out digest) ? Convert.ToString(digest) : "";
                string url = Convert.ToString(asset["browser_download_url"]);
                long size = Convert.ToInt64(asset["size"], CultureInfo.InvariantCulture);
                string expected = "https://github.com/" + Repository + "/releases/download/" + tag + "/QabanAlGhad-" + version + ".zip";
                if (url != expected || !Regex.IsMatch(hash ?? "", @"^sha256:[a-fA-F0-9]{64}$") || size <= 0 || size > MaximumPackageBytes)
                    throw new InvalidDataException("Untrusted or incomplete release asset.");
                result = new AvailableUpdate { Version = version.ToString(), Url = url, Sha256 = hash.Substring(7).ToLowerInvariant(), Size = size };
            }
            if (result == null) throw new InvalidDataException("Release package not ready.");
            return result;
        }

        static bool Allowed(Uri uri)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo != "" || uri.Fragment != "") return false;
            return (uri.Host == "api.github.com" && uri.AbsolutePath == "/repos/" + Repository + "/releases/latest") ||
                   (uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal)) ||
                   uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com";
        }

        static async Task Download(string address, string path, long limit, int timeoutMs, CancellationToken cancellation)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(timeoutMs);
                Uri uri = new Uri(address);
                for (int hop = 0; hop < 5; hop++)
                {
                    if (!Allowed(uri)) throw new InvalidDataException("Untrusted update destination.");
                    var request = (HttpWebRequest)WebRequest.Create(uri);
                    request.AllowAutoRedirect = false;
                    request.UserAgent = "QabanAlGhad-Updates/1.0";
                    request.Accept = uri.Host == "api.github.com" ? "application/vnd.github+json" : "application/octet-stream";
                    using (timeout.Token.Register(() => request.Abort()))
                    using (var response = (HttpWebResponse)await request.GetResponseAsync())
                    {
                        int code = (int)response.StatusCode;
                        if (code >= 300 && code < 400)
                        {
                            if (address == Api) throw new InvalidDataException("API redirect rejected.");
                            uri = new Uri(uri, response.Headers["Location"]);
                            continue;
                        }
                        if (code != 200 || response.ContentLength > limit) throw new InvalidDataException("Update response rejected.");
                        using (var input = response.GetResponseStream())
                        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[16384]; int count; long total = 0;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0)
                            {
                                total += count;
                                if (total > limit) throw new InvalidDataException("Update exceeds size limit.");
                                await output.WriteAsync(buffer, 0, count, timeout.Token);
                            }
                        }
                        return;
                    }
                }
                throw new InvalidDataException("Too many update redirects.");
            }
        }

        public static async Task<AvailableUpdate> Check(Version current, CancellationToken cancellation)
        {
            string file = Path.Combine(Path.GetTempPath(), "qaban-release-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                try { await Download(Api, file, 512 * 1024, 12000, cancellation); }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null) response.Dispose();
                    throw;
                }
                return Parse(File.ReadAllText(file), current);
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        public static string Hash(string file)
        {
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        public static async Task Fetch(AvailableUpdate update, string file, CancellationToken cancellation)
        {
            try
            {
                await Download(update.Url, file, update.Size, 120000, cancellation);
                if (new FileInfo(file).Length != update.Size || Hash(file) != update.Sha256)
                    throw new InvalidDataException("Update integrity check failed.");
            }
            catch { if (File.Exists(file)) File.Delete(file); throw; }
        }
    }
}
