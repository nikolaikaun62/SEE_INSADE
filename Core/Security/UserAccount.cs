using System;
using System.Collections.Generic;

namespace SEE_INSADE.Core.Security
{
    public sealed class UserAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public UserRole Role { get; set; } = UserRole.Observer;
        public int AccessLevel { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public List<AccessPermission> Permissions { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }

        public override string ToString()
        {
            return $"{DisplayName} ({Role}, L{AccessLevel})";
        }
    }
}
