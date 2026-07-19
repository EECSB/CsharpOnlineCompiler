using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.JSInterop;

namespace CsharpOnlineCompiler
{
    ///<summary>
    ///A drop-in <see cref="ILocalStorageService"/> backed by IndexedDB (via the <c>appIdb</c> interop)
    ///instead of localStorage, so the saved app state can grow far past localStorage's ~5 MB per-origin
    ///cap. Two extras on top of a plain key-value store:
    ///<list type="bullet">
    ///<item>Large values are transparently <b>compressed</b> (deflate + base64), so the state blob takes a
    ///fraction of the room, with zero data loss.</item>
    ///<item><b>Lazy migration</b>: the first read of a key that isn't in IndexedDB falls back to the old
    ///localStorage value and copies it over — so existing users keep their saved state, and only this app's
    ///own keys move (localStorage is shared across the Decent apps on this origin). localStorage is left
    ///intact as a backup.</item>
    ///</list>
    ///Writes are best-effort: a failed write (full quota / storage disabled) is swallowed rather than thrown.
    ///</summary>
    public class IndexedDbLocalStorage : ILocalStorageService
    {
        private readonly IJSRuntime _js;

        //Values longer than this are stored compressed; shorter ones aren't worth the deflate/base64 overhead.
        private const int CompressThreshold = 256;
        private const char RawMarker = 'r';
        private const char CompressedMarker = 'z';

        public IndexedDbLocalStorage(IJSRuntime js)
        {
            _js = js;
        }

        //These apps don't subscribe to storage-change events; satisfy the interface without a backing field.
        public event System.EventHandler<ChangingEventArgs> Changing { add { } remove { } }
        public event System.EventHandler<ChangedEventArgs> Changed { add { } remove { } }

        public async ValueTask<string> GetItemAsStringAsync(string key, CancellationToken cancellationToken = default)
        {
            var stored = await IdbGetAsync(key);

            if (stored != null)
                return Decode(stored);

            //Not in IndexedDB yet — one-time fall back to the old localStorage value and carry it over.
            var legacy = await LocalStorageGetAsync(key);

            if (legacy != null)
                await IdbSetAsync(key, Encode(legacy));

            return legacy;
        }

        public async ValueTask SetItemAsStringAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            await IdbSetAsync(key, Encode(data ?? string.Empty));
        }

        public async ValueTask<T> GetItemAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var json = await GetItemAsStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }

        public async ValueTask SetItemAsync<T>(string key, T data, CancellationToken cancellationToken = default)
        {
            await SetItemAsStringAsync(key, JsonSerializer.Serialize(data), cancellationToken);
        }

        public async ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _js.InvokeVoidAsync("appIdb.remove", key);
            }
            catch { }
        }

        public async ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            foreach (var key in keys)
                await RemoveItemAsync(key, cancellationToken);
        }

        public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _js.InvokeVoidAsync("appIdb.clear");
            }
            catch { }
        }

        public async ValueTask<int> LengthAsync(CancellationToken cancellationToken = default)
        {
            var keys = await KeysListAsync();

            return keys.Count;
        }

        public async ValueTask<string> KeyAsync(int index, CancellationToken cancellationToken = default)
        {
            var keys = await KeysListAsync();

            if (index < 0 || index >= keys.Count)
                return null;

            return keys[index];
        }

        public async ValueTask<IEnumerable<string>> KeysAsync(CancellationToken cancellationToken = default)
        {
            return await KeysListAsync();
        }

        public async ValueTask<bool> ContainKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            var value = await GetItemAsStringAsync(key, cancellationToken);

            return value != null;
        }

        #region Interop helpers

        private async Task<string> IdbGetAsync(string key)
        {
            try
            {
                return await _js.InvokeAsync<string>("appIdb.get", key);
            }
            catch
            {
                return null;
            }
        }

        private async Task IdbSetAsync(string key, string value)
        {
            try
            {
                await _js.InvokeAsync<bool>("appIdb.set", key, value);
            }
            catch { }
        }

        private async Task<string> LocalStorageGetAsync(string key)
        {
            try
            {
                return await _js.InvokeAsync<string>("localStorage.getItem", key);
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<string>> KeysListAsync()
        {
            try
            {
                var keys = await _js.InvokeAsync<List<string>>("appIdb.keys");

                return keys ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        #endregion

        #region Compression

        //Encodes a value for storage: a one-char marker plus the payload — the raw string for short values,
        //deflate + base64 for longer ones. Round-trips with Decode.
        private static string Encode(string value)
        {
            if (value == null)
                return null;

            if (value.Length < CompressThreshold)
                return RawMarker + value;

            var bytes = Encoding.UTF8.GetBytes(value);

            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(bytes, 0, bytes.Length);

            return CompressedMarker + System.Convert.ToBase64String(output.ToArray());
        }

        //Inverse of Encode. An unmarked string is returned as-is (defensive).
        private static string Decode(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return stored;

            var marker = stored[0];
            var payload = stored.Substring(1);

            if (marker == RawMarker)
                return payload;

            if (marker == CompressedMarker)
            {
                var data = System.Convert.FromBase64String(payload);

                using var input = new MemoryStream(data);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);

                return Encoding.UTF8.GetString(output.ToArray());
            }

            return stored;
        }

        #endregion
    }
}
