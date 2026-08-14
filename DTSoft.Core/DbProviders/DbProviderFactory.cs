using System.Reflection;
using DTSoft.Core.Localization;

namespace DTSoft.Core.DbProviders
{
    public static class DbProviderFactory
    {
        private static readonly Dictionary<string, Type> _providerTypes = new(StringComparer.OrdinalIgnoreCase);

        static DbProviderFactory()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var providerTypes = assembly.GetTypes()
                .Where(t => typeof(IDbProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var type in providerTypes)
            {
                var providerName = type.Name.Replace("Provider", "").ToLower();
                _providerTypes[providerName] = type;
            }
        }

        public static IDbProvider Create(string? providerName, string? databaseName = null, ITextLocalizer? localizer = null)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException(DbProviderMessages.Text(localizer, "db.providerRequired"));
            }
        
            var provider = providerName.ToLower();
        
            if (_providerTypes.TryGetValue(provider, out var exactType))
            {
                return (IDbProvider)Activator.CreateInstance(exactType, databaseName, localizer)!;
            }
        
            foreach (var kvp in _providerTypes)
            {
                if (provider.Contains(kvp.Key))
                {
                    return (IDbProvider)Activator.CreateInstance(kvp.Value, databaseName, localizer)!;
                }
            }
        
            throw new NotSupportedException(DbProviderMessages.Text(localizer, "db.providerUnsupported", providerName, string.Join(", ", _providerTypes.Keys)));
        }

        public static IEnumerable<string> GetSupportedProviders() => _providerTypes.Keys.OrderBy(k => k);
    }
}
