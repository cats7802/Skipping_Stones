using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SkippingStones.Auth
{
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        public AuthUserData CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser != null && CurrentUser.isAuthenticated;

        public event Action<AuthUserData> OnLoginSuccess;
        public event Action OnLogout;

        private readonly Dictionary<AuthProviderType, IAuthService> _services = new Dictionary<AuthProviderType, IAuthService>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeServices();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeServices()
        {
            _services[AuthProviderType.Guest] = new GuestAuthService();
            _services[AuthProviderType.Kakao] = new KakaoAuthService();
            _services[AuthProviderType.Steam] = new SteamAuthService();
        }

        public async Task<AuthUserData> LoginAsync(AuthProviderType providerType)
        {
            if (!_services.TryGetValue(providerType, out var service))
            {
                Debug.LogError($"[AuthManager] 미지원 인증 제공자: {providerType}");
                return null;
            }

            try
            {
                CurrentUser = await service.LoginAsync();
                OnLoginSuccess?.Invoke(CurrentUser);
                return CurrentUser;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] 로그인 실패 ({providerType}): {ex.Message}");
                return null;
            }
        }

        public async Task<bool> LogoutAsync()
        {
            if (CurrentUser != null && _services.TryGetValue(CurrentUser.providerType, out var service))
            {
                await service.LogoutAsync();
            }

            CurrentUser = null;
            OnLogout?.Invoke();
            return true;
        }
    }
}
