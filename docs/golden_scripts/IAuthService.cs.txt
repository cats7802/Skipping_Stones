using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SkippingStones.Auth
{
    public enum AuthProviderType
    {
        Guest,
        Kakao,
        Steam
    }

    [System.Serializable]
    public class AuthUserData
    {
        public string userId;
        public string nickname;
        public string profileImageUrl;
        public AuthProviderType providerType;
        public bool isAuthenticated;
    }

    public interface IAuthService
    {
        AuthProviderType ProviderType { get; }
        Task<AuthUserData> LoginAsync();
        Task<bool> LogoutAsync();
        Task<bool> LinkAccountAsync(string targetUserId);
    }

    public class GuestAuthService : IAuthService
    {
        public AuthProviderType ProviderType => AuthProviderType.Guest;
        private const string GUEST_ID_KEY = "GUEST_UUID_KEY";
        private const string GUEST_NICK_KEY = "GUEST_NICK_KEY";

        public Task<AuthUserData> LoginAsync()
        {
            string guestId = PlayerPrefs.GetString(GUEST_ID_KEY, string.Empty);
            if (string.IsNullOrEmpty(guestId))
            {
                guestId = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString(GUEST_ID_KEY, guestId);
            }

            string nick = PlayerPrefs.GetString(GUEST_NICK_KEY, "조약돌 마스터");

            var user = new AuthUserData
            {
                userId = guestId,
                nickname = nick,
                profileImageUrl = string.Empty,
                providerType = AuthProviderType.Guest,
                isAuthenticated = true
            };

            return Task.FromResult(user);
        }

        public Task<bool> LogoutAsync()
        {
            return Task.FromResult(true);
        }

        public Task<bool> LinkAccountAsync(string targetUserId)
        {
            return Task.FromResult(true);
        }
    }

    public class KakaoAuthService : IAuthService
    {
        public AuthProviderType ProviderType => AuthProviderType.Kakao;

        public async Task<AuthUserData> LoginAsync()
        {
            await Task.Yield();
            string kakaoId = "kakao_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return new AuthUserData
            {
                userId = kakaoId,
                nickname = "카카오 물수제비 달인",
                profileImageUrl = "https://k.kakaocdn.net/profile_sample.png",
                providerType = AuthProviderType.Kakao,
                isAuthenticated = true
            };
        }

        public Task<bool> LogoutAsync()
        {
            return Task.FromResult(true);
        }

        public Task<bool> LinkAccountAsync(string targetUserId)
        {
            return Task.FromResult(true);
        }
    }

    public class SteamAuthService : IAuthService
    {
        public AuthProviderType ProviderType => AuthProviderType.Steam;

        public async Task<AuthUserData> LoginAsync()
        {
            await Task.Yield();
            string steamId = "steam_76561198000000000";
            return new AuthUserData
            {
                userId = steamId,
                nickname = "Steam Skipper",
                profileImageUrl = string.Empty,
                providerType = AuthProviderType.Steam,
                isAuthenticated = true
            };
        }

        public Task<bool> LogoutAsync()
        {
            return Task.FromResult(true);
        }

        public Task<bool> LinkAccountAsync(string targetUserId)
        {
            return Task.FromResult(true);
        }
    }
}
