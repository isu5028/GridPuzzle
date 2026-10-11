#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public static class SteamAchievementHelper
{
    private const string Coin50Achievement = "COIN_50";
    private const string Coin100Achievement = "COIN_100";
    private const string CoinAllAchievement = "COIN_ALL";
    private const string ChangeColorAchievement = "CHANGE_COLOR";
    private const string ClearAchievement = "CLEAR";
    private const string ClearAllAchievement = "CLEAR_ALL";

    public static void TryUnlockCoinAchievements(int totalCoins)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;

        bool hasUpdated = false;

        if (totalCoins >= 50)
        {
            hasUpdated |= TryUnlockAchievement(Coin50Achievement);
        }

        if (totalCoins >= 100)
        {
            hasUpdated |= TryUnlockAchievement(Coin100Achievement);
        }

        if (totalCoins >= 125)
        {
            hasUpdated |= TryUnlockAchievement(CoinAllAchievement);
        }

        if (hasUpdated)
        {
            SteamUserStats.StoreStats();
        }
#endif
    }

    public static void TryUnlockChangeColorAchievement()
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;

        if (TryUnlockAchievement(ChangeColorAchievement))
        {
            SteamUserStats.StoreStats();
        }
#endif
    }

    public static void TryUnlockClearAchievements(bool shouldUnlockClearAll)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;

        bool hasUpdated = false;
        hasUpdated |= TryUnlockAchievement(ClearAchievement);

        if (shouldUnlockClearAll)
        {
            hasUpdated |= TryUnlockAchievement(ClearAllAchievement);
        }

        if (hasUpdated)
        {
            SteamUserStats.StoreStats();
        }
#endif
    }

#if !DISABLESTEAMWORKS
    private static bool TryUnlockAchievement(string apiName)
    {
        if (SteamUserStats.GetAchievement(apiName, out bool isUnlocked) && isUnlocked)
        {
            return false;
        }

        return SteamUserStats.SetAchievement(apiName);
    }
#endif
}
