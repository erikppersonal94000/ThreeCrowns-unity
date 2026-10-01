using System.Collections.Generic;
using UnityEngine;

// Which campaign locations have been won. Saved between sessions.
public static class CampaignProgress
{
    const string Key = "tc-campaign-cleared";
    static HashSet<string> cleared;

    static void Load()
    {
        if (cleared != null) return;
        cleared = new HashSet<string>();
        foreach (var id in PlayerPrefs.GetString(Key, "").Split(','))
            if (id.Length > 0) cleared.Add(id);
    }

    public static bool IsCleared(string id)
    {
        Load();
        return !string.IsNullOrEmpty(id) && cleared.Contains(id);
    }

    public static void MarkCleared(string id)
    {
        Load();
        if (string.IsNullOrEmpty(id) || !cleared.Add(id)) return;
        PlayerPrefs.SetString(Key, string.Join(",", cleared));
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        cleared = new HashSet<string>();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}