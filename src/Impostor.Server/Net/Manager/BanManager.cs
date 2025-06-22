using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Impostor.Api.Net;

namespace Impostor.Server.Net.Manager;

public static class BanManager
{
    private static readonly string BanFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "banlist.json");

    public class BanEntry
    {
        public string Name { get; set; }

        public string IP { get; set; }

        public string HWID { get; set; }

        public string Reason { get; set; }
    }

    static BanManager()
    {
        if (!File.Exists(BanFilePath))
        {
            var emptyList = new List<BanEntry>();
            var json = JsonSerializer.Serialize(emptyList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BanFilePath, json);
        }
    }


    private static List<BanEntry> LoadBanList()
    {
        if (!File.Exists(BanFilePath))
        {
            return [];
        }

        var json = File.ReadAllText(BanFilePath);
        return JsonSerializer.Deserialize<List<BanEntry>>(json) ?? [];
    }

    private static void SaveBanList(List<BanEntry> banList)
    {
        var json = JsonSerializer.Serialize(banList, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(BanFilePath, json);
    }

    public static void Ban(IClient client, string reason = "")
    {
        var name = client.Name;
        var ip = client.Connection?.EndPoint?.Address?.ToString();
        var hwid = client.DeviceId;

        if (string.IsNullOrEmpty(ip) && string.IsNullOrEmpty(hwid))
        {
            return;
        }

        var banList = LoadBanList();

        // Check if already banned
        if (banList.Any(b => b.IP == ip || b.HWID == hwid))
        {
            return;
        }

        banList.Add(new BanEntry { Name = name, IP = ip, HWID = hwid, Reason = reason });
        SaveBanList(banList);
    }

    public static void Ban(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return;
        }

        var banList = LoadBanList();
        var isIp = IPAddress.TryParse(identifier, out _);

        var alreadyBanned = isIp
            ? banList.Any(b => b.IP == identifier)
            : banList.Any(b => b.HWID == identifier);

        if (alreadyBanned)
        {
            return;
        }

        var newEntry = isIp
            ? new BanEntry { IP = identifier, HWID = null }
            : new BanEntry { IP = null, HWID = identifier };

        banList.Add(newEntry);
        SaveBanList(banList);
    }

    public static bool IsBanned(IClient client)
    {
        var ip = client.Connection?.EndPoint?.Address?.ToString();
        var hwid = client.DeviceId;

        if (string.IsNullOrEmpty(ip) && string.IsNullOrEmpty(hwid))
        {
            return false;
        }

        var banList = LoadBanList();
        return banList.Any(b => b.IP == ip || b.HWID == hwid);
    }

    public static BanEntry? GetBanEntry(IClient client)
    {
        var ip = client.Connection?.EndPoint?.Address?.ToString();
        var hwid = client.DeviceId;

        if (string.IsNullOrEmpty(ip) && string.IsNullOrEmpty(hwid))
        {
            return null;
        }

        var banList = LoadBanList();
        return banList.FirstOrDefault(b => b.IP == ip || b.HWID == hwid);
    }

    public static BanEntry? GetBanEntry(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        var banList = LoadBanList();
        var isIp = IPAddress.TryParse(identifier, out _);

        return isIp
            ? banList.FirstOrDefault(b => b.IP == identifier)
            : banList.FirstOrDefault(b => b.HWID == identifier);
    }

    public static bool IsBanned(string hwid, string ip)
    {
        if (string.IsNullOrEmpty(ip) && string.IsNullOrEmpty(hwid))
        {
            return false;
        }

        var banList = LoadBanList();
        return banList.Any(b => b.IP == ip || b.HWID == hwid);
    }

    public static bool Unban(string identifier)
    {
        var banList = LoadBanList();

        var isIp = IPAddress.TryParse(identifier, out _);

        var removed = isIp
            ? banList.RemoveAll(b => b.IP == identifier)
            : banList.RemoveAll(b => b.HWID == identifier);

        if (removed > 0)
        {
            SaveBanList(banList);
            return true;
        }

        return false;
    }
}
