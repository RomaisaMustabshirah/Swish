using System.Collections.Generic;
using UnityEngine;

public class Skin
{
    public string id, name, baseHex, accentHex;
    public int price, pattern;   // 0 plain, 1 stripes, 2 dots, 3 checker, 4 rings, 5 wedges

    public Skin(string id, string name, int price, int pattern, string baseHex, string accentHex)
    {
        this.id = id; this.name = name; this.price = price;
        this.pattern = pattern; this.baseHex = baseHex; this.accentHex = accentHex;
    }
}

public static class Store
{
    public static readonly Skin[] All =
    {
        new Skin("classic", "Classic",      0,   0, "#f26a1b", "#f26a1b"),
        new Skin("cream",   "Cream",        40,  0, "#f3ede2", "#f3ede2"),
        new Skin("sky",     "Sky Stripes",  60,  1, "#38a3ff", "#ffffff"),
        new Skin("pink",    "Pink Dots",    90,  2, "#ff5c9e", "#ffffff"),
        new Skin("lime",    "Lime Check",   120, 3, "#b4e22b", "#5f7d10"),
        new Skin("violet",  "Violet Rings", 160, 4, "#8b66ff", "#c9b8ff"),
        new Skin("beach",   "Beach",        200, 5, "#ffc21a", "#e63b2e"),
        new Skin("night",   "Midnight",     260, 1, "#1b1f33", "#f26a1b"),
        new Skin("mint",    "Mint Dots",    320, 2, "#19c3b1", "#0e5d54"),
        new Skin("fire",    "Fire",         400, 4, "#e63b2e", "#ffc21a"),
        new Skin("ice",     "Ice Check",    500, 3, "#bfe9ff", "#ffffff"),
        new Skin("gold",    "Gold Rings",   800, 4, "#ffc21a", "#fff1a8"),
    };

    public static int coins;
    public static int selected;
    static HashSet<string> owned = new HashSet<string>();

    public static Skin Current { get { return All[selected]; } }

    public static void Load()
    {
        coins = PlayerPrefs.GetInt("swish-coins", 0);
        owned.Clear();
        owned.Add(All[0].id);
        foreach (var id in PlayerPrefs.GetString("swish-owned", "").Split(','))
            if (id.Length > 0) owned.Add(id);
        string sel = PlayerPrefs.GetString("swish-ball", All[0].id);
        selected = 0;
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == sel && owned.Contains(sel)) selected = i;
    }

    static void Save()
    {
        PlayerPrefs.SetInt("swish-coins", coins);
        PlayerPrefs.SetString("swish-owned", string.Join(",", owned));
        PlayerPrefs.SetString("swish-ball", All[selected].id);
        PlayerPrefs.Save();
    }

    public static bool Owned(int i) { return owned.Contains(All[i].id); }
    public static void Add(int n) { coins += n; Save(); }

    public static bool Buy(int i)
    {
        if (Owned(i) || coins < All[i].price) return false;
        coins -= All[i].price;
        owned.Add(All[i].id);
        selected = i;
        Save();
        return true;
    }

    public static void Select(int i)
    {
        if (Owned(i)) { selected = i; Save(); }
    }
}