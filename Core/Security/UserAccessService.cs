using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SEE_INSADE.Core.Security
{
    public sealed class UserAccessService
    {
        private readonly string _userPath = Path.Combine(Environment.CurrentDirectory, "Security", "users.json");

        public static UserAccessService Instance { get; } = new();

        public ObservableCollection<UserAccount> Users { get; } = new();
        public UserAccount? CurrentUser { get; private set; }

        public void Load()
        {
            Users.Clear();

            try
            {
                if (File.Exists(_userPath))
                {
                    string json = File.ReadAllText(_userPath);
                    var users = JsonSerializer.Deserialize<List<UserAccount>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (users != null)
                    {
                        foreach (UserAccount user in users)
                            NormalizeAndAdd(user);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"User load failed: {ex.Message}");
            }

            if (Users.Count == 0)
                SeedDefaults();

            EnsureAdminDefaultPassword();

            CurrentUser = Users.FirstOrDefault(user => user.Role == UserRole.Administrator && user.IsActive)
                ?? Users.FirstOrDefault(user => user.IsActive);

            Save();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_userPath, JsonSerializer.Serialize(Users.ToList(), options));
        }

        public void AddUser(UserAccount user)
        {
            NormalizeAndAdd(user);
            Save();
        }

        public void RemoveUser(UserAccount user)
        {
            Users.Remove(user);

            if (CurrentUser?.Id == user.Id)
                CurrentUser = Users.FirstOrDefault(item => item.IsActive);

            Save();
        }

        public void SetCurrentUser(UserAccount user)
        {
            if (!user.IsActive)
                return;

            user.LastLoginAt = DateTime.Now;
            CurrentUser = user;
            Save();
        }

        public bool HasPermission(AccessPermission permission)
        {
            return CurrentUser?.IsActive == true && CurrentUser.Permissions.Contains(permission);
        }

        public bool Authenticate(string userName, string password, out UserAccount? user)
        {
            string normalizedName = userName.Trim();
            string hash = HashPassword(password);
            user = Users.FirstOrDefault(item =>
                item.IsActive &&
                item.UserName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) &&
                item.PasswordHash == hash);

            if (user == null)
                return false;

            SetCurrentUser(user);
            return true;
        }

        public static string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        public static int GetDefaultAccessLevel(UserRole role)
        {
            return role switch
            {
                UserRole.Observer => 10,
                UserRole.Operator => 30,
                UserRole.Supervisor => 70,
                UserRole.Administrator => 100,
                _ => 10
            };
        }

        public static List<AccessPermission> GetDefaultPermissions(UserRole role)
        {
            return role switch
            {
                UserRole.Observer => new List<AccessPermission>
                {
                    AccessPermission.ViewScan
                },
                UserRole.Operator => new List<AccessPermission>
                {
                    AccessPermission.ViewScan,
                    AccessPermission.ControlScan,
                    AccessPermission.UseFilters,
                    AccessPermission.ExportData
                },
                UserRole.Supervisor => new List<AccessPermission>
                {
                    AccessPermission.ViewScan,
                    AccessPermission.ControlScan,
                    AccessPermission.UseFilters,
                    AccessPermission.RunDiagnostics,
                    AccessPermission.CalibrateDetectors,
                    AccessPermission.ExportData
                },
                UserRole.Administrator => Enum.GetValues<AccessPermission>().ToList(),
                _ => new List<AccessPermission>()
            };
        }

        public void ApplyRoleDefaults(UserAccount user)
        {
            user.AccessLevel = GetDefaultAccessLevel(user.Role);
            user.Permissions = GetDefaultPermissions(user.Role);
        }

        private void NormalizeAndAdd(UserAccount user)
        {
            if (user.Id == Guid.Empty)
                user.Id = Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(user.UserName))
                user.UserName = user.Role.ToString().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(user.DisplayName))
                user.DisplayName = user.UserName;

            if (user.Permissions.Count == 0)
                ApplyRoleDefaults(user);

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                user.PasswordHash = HashPassword(GetDefaultPassword(user.UserName, user.Role));

            if (!Users.Any(existing => existing.UserName.Equals(user.UserName, StringComparison.OrdinalIgnoreCase)))
                Users.Add(user);
        }

        private void SeedDefaults()
        {
            AddSeedUser("observer", "Observer", UserRole.Observer);
            AddSeedUser("operator", "Operator", UserRole.Operator);
            AddSeedUser("supervisor", "Supervisor", UserRole.Supervisor);
            AddSeedUser("admin", "Administrator", UserRole.Administrator);
        }

        private void AddSeedUser(string userName, string displayName, UserRole role)
        {
            var user = new UserAccount
            {
                UserName = userName,
                DisplayName = displayName,
                Role = role,
                IsActive = true,
                PasswordHash = HashPassword(GetDefaultPassword(userName, role))
            };
            ApplyRoleDefaults(user);
            Users.Add(user);
        }

        private void EnsureAdminDefaultPassword()
        {
            string legacyAdminHash = HashPassword("admin");
            string defaultAdminHash = HashPassword("123456");

            foreach (UserAccount admin in Users.Where(user =>
                user.Role == UserRole.Administrator &&
                user.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(admin.PasswordHash) || admin.PasswordHash == legacyAdminHash)
                    admin.PasswordHash = defaultAdminHash;
            }
        }

        private static string GetDefaultPassword(string userName, UserRole role)
        {
            return role == UserRole.Administrator && userName.Equals("admin", StringComparison.OrdinalIgnoreCase)
                ? "123456"
                : userName;
        }
    }
}
