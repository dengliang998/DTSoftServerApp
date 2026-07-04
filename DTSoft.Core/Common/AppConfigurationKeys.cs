namespace DTSoft.Core.Common;

public static class AppConfigurationKeys
{
    public static class Application
    {
        public const string InitializeOnStartup = "Application:Initialization:RunOnStartup";
        public const string LegacyInitializeOnStartup = "InitializeOnStartup";
    }

    public static class ApiDocumentation
    {
        public const string Enabled = "ApiDocumentation:Enabled";
        public const string LegacyEnabled = "ScalarEnabled";
    }

    public static class Authentication
    {
        public static class Jwt
        {
            public const string SigningKey = "Authentication:Jwt:SigningKey";
            public const string Issuer = "Authentication:Jwt:Issuer";
            public const string Audience = "Authentication:Jwt:Audience";

            public const string LegacySigningKey = "Jwt:Key";
            public const string LegacyIssuer = "Jwt:Issuer";
            public const string LegacyAudience = "Jwt:Audience";
        }
    }

    public static class Security
    {
        public static class PasswordHashing
        {
            public const string Iterations = "Security:PasswordHashing:Iterations";
            public const string LegacyIterations = "PasswordHashing:Iterations";
        }
    }

    public static class Storage
    {
        public const string RootPath = "Storage:RootPath";
        public const string LegacyRootPath = "RootPath";

        public static class Attachments
        {
            public const string Directory = "Storage:Attachments:Directory";
            public const string LegacyDirectory = "AttachmentPath";
        }

        public static class Users
        {
            public const string Directory = "Storage:Users:Directory";
            public const string AvatarDirectory = "Storage:Users:AvatarDirectory";
            public const string LegacyDirectory = "UserData";
        }
    }

    public static class Database
    {
        public const string Provider = "Database:Provider";
        public const string LegacyProvider = "DBType";
        public const string ConnectionName = "Default";
        public const string LegacyConnectionName = "DBConnection";
    }

    public static class Cache
    {
        public const string Provider = "Cache:Provider";
        public const string LegacyProvider = "CacheModel";

        public static class Redis
        {
            public const string Host = "Cache:Redis:Host";
            public const string Port = "Cache:Redis:Port";
            public const string Password = "Cache:Redis:Password";

            public const string LegacyHost = "RedisHost";
            public const string LegacyPort = "RedisPort";
            public const string LegacyPassword = "RedisPassword";
        }
    }
}
