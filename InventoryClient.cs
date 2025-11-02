using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StyleInfoWin
{
    public class InventoryRow
    {
        public string StyleName { get; set; } = "";
        public string Warehouse { get; set; } = "";
        public string Color     { get; set; } = "";
        public string Size      { get; set; } = "";
        public int    QtyIn     { get; set; }
        public int    QtyOut    { get; set; }
    }

    public sealed class InventoryClient
    {
        private readonly AppConfig _cfg;
        private static readonly HttpClient _http = new HttpClient();

        public InventoryClient(AppConfig cfg) => _cfg = cfg;

        public async Task<List<InventoryRow>> FetchAsync(string styleName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_cfg.inventory_api_url))
                return new List<InventoryRow>();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _cfg.inventory_timeout_seconds)));

            var url = $"{_cfg.inventory_api_url}?style_name={Uri.EscapeDataString(styleName)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            return ParseRows(doc, styleName);
        }

        private static List<InventoryRow> ParseRows(JsonDocument doc, string style)
        {
            var keyWarehouse = "warehouse";
            var keyColor     = "color";
            var keySize      = "size";
            var keyIn        = "in";
            var keyOut       = "out";

            var result = new List<InventoryRow>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string wh  = el.TryGetProperty(keyWarehouse, out var wv) ? wv.GetString() ?? "" : "";
                    string col = el.TryGetProperty(keyColor,     out var cv) ? cv.GetString() ?? "" : "";
                    string sz  = el.TryGetProperty(keySize,      out var sv) ? sv.GetString() ?? "" : "";
                    int qin    = el.TryGetProperty(keyIn,        out var iv) && iv.TryGetInt32(out var i) ? i : 0;
                    int qout   = el.TryGetProperty(keyOut,       out var ov) && ov.TryGetInt32(out var o) ? o : 0;

                    result.Add(new InventoryRow {
                        StyleName = style,
                        Warehouse = wh,
                        Color     = col,
                        Size      = sz,
                        QtyIn     = qin,
                        QtyOut    = qout
                    });
                }
            }
            return result;
        }
    }
}
