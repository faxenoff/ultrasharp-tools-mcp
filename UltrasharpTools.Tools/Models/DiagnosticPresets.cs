namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Предустановленные наборы правил для анализа кода
/// </summary>
public static class DiagnosticPresets
{
    /// <summary>
    /// Наборы правил по категориям
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// Проблемы производительности
        /// </summary>
        public static readonly string[] Performance =
        [
            "CA1860", // Prefer Count > 0 over Any()
            "CA1861", // Prefer static readonly fields
            "CA1850", // Prefer static HashData method
            "CA1845", // Use span-based string.Concat
            "CA1847", // Use char overload instead of string
            "CA1854", // Prefer TryGetValue over ContainsKey
            "CA1851", // Multiple enumeration
            "CA1853", // Concat and ToArray
            "CA1855", // Use Span.Clear instead of Span.Fill
            "CA1858", // StartsWith(char) instead of StartsWith(string)
            "CA1859", // Use concrete types for performance
        ];

        /// <summary>
        /// Проблемы поддержки кода
        /// </summary>
        public static readonly string[] Maintainability =
        [
            "CA1822", // Member can be marked as static
            "CA1506", // Avoid excessive class coupling
            "CA1505", // Avoid unmaintainable code
            "CA1502", // Avoid excessive complexity
            "CA1501", // Avoid excessive inheritance
        ];

        /// <summary>
        /// Проблемы безопасности
        /// </summary>
        public static readonly string[] Security =
        [
            "CA2100", // Review SQL queries for security
            "CA2109", // Review visible event handlers
            "CA2119", // Seal methods that satisfy private interfaces
            "CA2153", // Do not catch CorruptedStateExceptions
            "CA2300", // Do not use insecure deserializer BinaryFormatter
            "CA2301", // Do not call BinaryFormatter.Deserialize
            "CA2302", // Ensure BinaryFormatter.Binder is set
            "CA2305", // Do not use insecure deserializer LosFormatter
            "CA2310", // Do not use insecure deserializer NetDataContractSerializer
            "CA2315", // Do not use insecure deserializer ObjectStateFormatter
            "CA2321", // Do not deserialize with JavaScriptSerializer
            "CA2322", // Ensure JavaScriptSerializer is not initialized with SimpleTypeResolver
            "CA2326", // Do not use TypeNameHandling values other than None
            "CA2327", // Do not use insecure JsonSerializerSettings
            "CA2328", // Ensure JsonSerializerSettings are secure
            "CA2329", // Do not deserialize with JsonSerializer using an insecure configuration
            "CA2330", // Ensure that JsonSerializer has a secure configuration
            "CA3001", // Review code for SQL injection vulnerabilities
            "CA3002", // Review code for XSS vulnerabilities
            "CA3003", // Review code for file path injection vulnerabilities
            "CA3004", // Review code for information disclosure vulnerabilities
            "CA3005", // Review code for LDAP injection vulnerabilities
            "CA3006", // Review code for process command injection vulnerabilities
            "CA3007", // Review code for open redirect vulnerabilities
            "CA3008", // Review code for XPath injection vulnerabilities
            "CA3009", // Review code for XML injection vulnerabilities
            "CA3010", // Review code for XAML injection vulnerabilities
            "CA3011", // Review code for DLL injection vulnerabilities
            "CA3012", // Review code for regex injection vulnerabilities
            "CA5350", // Do not use weak cryptographic algorithms
            "CA5351", // Do not use broken cryptographic algorithms
            "CA5359", // Do not disable certificate validation
            "CA5360", // Do not call dangerous methods in deserialization
            "CA5361", // Do not disable SChannel use of strong crypto
            "CA5362", // Potential reference cycle in deserialized object graph
            "CA5363", // Do not disable request validation
            "CA5364", // Do not use deprecated security protocols
            "CA5365", // Do not disable HTTP Header Checking
            "CA5366", // Use XmlReader for DataSet read XML
            "CA5367", // Do not serialize types with pointer fields
            "CA5368", // Set ViewStateUserKey for classes derived from Page
            "CA5369", // Use XmlReader for deserialize
            "CA5370", // Use XmlReader for validating reader
            "CA5371", // Use XmlReader for schema read
            "CA5372", // Use XmlReader for XPathDocument
            "CA5373", // Do not use obsolete key derivation function
            "CA5374", // Do not use XslTransform
            "CA5375", // Do not use account shared access signature
            "CA5376", // Use SharedAccessProtocol HttpsOnly
            "CA5377", // Use container level access policy
            "CA5378", // Do not disable ServicePointManagerSecurityProtocols
            "CA5379", // Ensure key derivation function algorithm is sufficiently strong
            "CA5380", // Do not add certificates to root store
            "CA5381", // Ensure certificates are not added to root store
            "CA5382", // Use secure cookies in ASP.NET Core
            "CA5383", // Ensure use secure cookies in ASP.NET Core
            "CA5384", // Do not use digital signature algorithm (DSA)
            "CA5385", // Use Rivest-Shamir-Adleman (RSA) algorithm with sufficient key size
            "CA5386", // Avoid hardcoding SecurityProtocolType value
            "CA5387", // Do not use weak key derivation function with insufficient iteration count
            "CA5388", // Ensure sufficient iterations when using weak key derivation function
            "CA5389", // Do not add archive item's path to target file system path
            "CA5390", // Do not hard-code encryption key
            "CA5391", // Use antiforgery tokens in ASP.NET Core MVC controllers
            "CA5392", // Use DefaultDllImportSearchPaths attribute for P/Invokes
            "CA5393", // Do not use unsafe DllImportSearchPath value
            "CA5394", // Do not use insecure randomness
            "CA5395", // Miss HttpVerb attribute for action methods
            "CA5396", // Set HttpOnly to true for HttpCookie
            "CA5397", // Do not use deprecated SslProtocols values
            "CA5398", // Avoid hardcoded SslProtocols values
            "CA5399", // Definitely disable HttpClient certificate revocation list check
            "CA5400", // Ensure HttpClient certificate revocation list check is not disabled
            "CA5401", // Do not use CreateEncryptor with non-default IV
            "CA5402", // Use CreateEncryptor with the default IV
            "CA5403", // Do not hard-code certificate
            "CA5404", // Do not disable token validation checks
            "CA5405", // Do not always skip token validation in delegates
        ];

        /// <summary>
        /// Проблемы надежности
        /// </summary>
        public static readonly string[] Reliability =
        [
            "CA2000", // Dispose objects before losing scope
            "CA2002", // Do not lock on objects with weak identity
            "CA2007", // Do not directly await a Task
            "CA2008", // Do not create tasks without passing a TaskScheduler
            "CA2009", // Do not call ToImmutableCollection on ImmutableCollection
            "CA2011", // Do not assign property within its setter
            "CA2012", // Use ValueTasks correctly
            "CA2013", // Do not use ReferenceEquals with value types
            "CA2014", // Do not use stackalloc in loops
            "CA2015", // Do not define finalizers for types derived from MemoryManager
            "CA2016", // Forward CancellationToken parameter
            "CA2017", // Parameter count mismatch
            "CA2018", // Buffer size incorrectly used
            "CA2019", // ThreadStatic fields should not use inline initialization
            "CA2020", // Prevent behavioral change
        ];

        /// <summary>
        /// Проблемы использования API
        /// </summary>
        public static readonly string[] Usage =
        [
            "CA1031", // Do not catch general exception types
            "CA1303", // Do not pass literals as localized parameters
            "CA2201", // Do not raise reserved exception types
            "CA2207", // Initialize value type static fields inline
            "CA2208", // Instantiate argument exceptions correctly
            "CA2211", // Non-constant fields should not be visible
            "CA2213", // Disposable fields should be disposed
            "CA2214", // Do not call overridable methods in constructors
            "CA2215", // Dispose methods should call base class dispose
            "CA2216", // Disposable types should declare finalizer
            "CA2217", // Do not mark enums with FlagsAttribute
            "CA2218", // Override GetHashCode on overriding Equals
            "CA2219", // Do not raise exceptions in exception clauses
            "CA2224", // Override Equals on overloading operator equals
            "CA2225", // Operator overloads have named alternates
            "CA2226", // Operators should have symmetrical overloads
            "CA2227", // Collection properties should be read only
            "CA2229", // Implement serialization constructors
            "CA2231", // Overload operator equals on overriding ValueType.Equals
            "CA2234", // Pass System.Uri objects instead of strings
            "CA2235", // Mark all non-serializable fields
            "CA2237", // Mark ISerializable types with SerializableAttribute
            "CA2241", // Provide correct arguments to formatting methods
            "CA2242", // Test for NaN correctly
            "CA2243", // Attribute string literals should parse correctly
            "CA2244", // Do not duplicate indexed element initializations
            "CA2245", // Do not assign a property to itself
            "CA2246", // Do not assign a symbol and its member in the same statement
            "CA2247", // Argument to TaskCompletionSource constructor should be TaskCreationOptions
            "CA2248", // Provide correct enum argument to Enum.HasFlag
            "CA2249", // Use string.Contains instead of string.IndexOf
            "CA2250", // Use ThrowIfCancellationRequested
            "CA2251", // Use String.Equals over String.Compare
            "CA2252", // Opt in to preview features
            "CA2253", // Named placeholders should not be numeric values
            "CA2254", // Template should be a static expression
            "CA2255", // The ModuleInitializer attribute should not be used in libraries
            "CA2256", // All members declared in parent interfaces must have implementation
            "CA2257", // Members defined on an interface with the DynamicInterfaceCastableImplementation
            "CA2258", // Providing a DynamicInterfaceCastableImplementation interface in Visual Basic
            "CA2259", // ThreadStatic only affects static fields
            "CA2260", // Implement generic math interfaces correctly
        ];

        /// <summary>
        /// Проблемы дизайна
        /// </summary>
        public static readonly string[] Design =
        [
            "CA1000", // Do not declare static members on generic types
            "CA1001", // Types that own disposable fields should be disposable
            "CA1002", // Do not expose generic lists
            "CA1003", // Use generic event handler instances
            "CA1005", // Avoid excessive parameters on generic types
            "CA1008", // Enums should have zero value
            "CA1010", // Collections should implement generic interface
            "CA1012", // Abstract types should not have constructors
            "CA1014", // Mark assemblies with CLSCompliantAttribute
            "CA1016", // Mark assemblies with AssemblyVersionAttribute
            "CA1017", // Mark assemblies with ComVisibleAttribute
            "CA1018", // Mark attributes with AttributeUsageAttribute
            "CA1019", // Define accessors for attribute arguments
            "CA1021", // Avoid out parameters
            "CA1024", // Use properties where appropriate
            "CA1027", // Mark enums with FlagsAttribute
            "CA1028", // Enum storage should be Int32
            "CA1030", // Use events where appropriate
            "CA1031", // Do not catch general exception types
            "CA1032", // Implement standard exception constructors
            "CA1033", // Interface methods should be callable by child types
            "CA1034", // Nested types should not be visible
            "CA1036", // Override methods on comparable types
            "CA1040", // Avoid empty interfaces
            "CA1041", // Provide ObsoleteAttribute message
            "CA1043", // Use integral or string argument for indexers
            "CA1044", // Properties should not be write only
            "CA1045", // Do not pass types by reference
            "CA1046", // Do not overload operator equals on reference types
            "CA1047", // Do not declare protected members in sealed types
            "CA1050", // Declare types in namespaces
            "CA1051", // Do not declare visible instance fields
            "CA1052", // Static holder types should be sealed
            "CA1053", // Static holder types should not have constructors
            "CA1054", // URI parameters should not be strings
            "CA1055", // URI return values should not be strings
            "CA1056", // URI properties should not be strings
            "CA1058", // Types should not extend certain base types
            "CA1060", // Move P/Invokes to NativeMethods class
            "CA1061", // Do not hide base class methods
            "CA1062", // Validate arguments of public methods
            "CA1063", // Implement IDisposable correctly
            "CA1064", // Exceptions should be public
            "CA1065", // Do not raise exceptions in unexpected locations
            "CA1066", // Implement IEquatable when overriding Equals
            "CA1067", // Override Equals when implementing IEquatable
            "CA1068", // CancellationToken parameters must come last
            "CA1069", // Enums should not have duplicate values
            "CA1070", // Do not declare event fields as virtual
        ];

        /// <summary>
        /// Проблемы глобализации
        /// </summary>
        public static readonly string[] Globalization =
        [
            "CA1303", // Do not pass literals as localized parameters
            "CA1304", // Specify CultureInfo
            "CA1305", // Specify IFormatProvider
            "CA1307", // Specify StringComparison for clarity
            "CA1308", // Normalize strings to uppercase
            "CA1309", // Use ordinal StringComparison
            "CA1310", // Specify StringComparison for correctness
            "CA2101", // Specify marshaling for P/Invoke string arguments
        ];

        /// <summary>
        /// Проблемы именования
        /// </summary>
        public static readonly string[] Naming =
        [
            "CA1700", // Do not name enum values Reserved
            "CA1707", // Identifiers should not contain underscores
            "CA1708", // Identifiers should differ by more than case
            "CA1710", // Identifiers should have correct suffix
            "CA1711", // Identifiers should not have incorrect suffix
            "CA1712", // Do not prefix enum values with type name
            "CA1713", // Events should not have before or after prefix
            "CA1714", // Flags enums should have plural names
            "CA1715", // Identifiers should have correct prefix
            "CA1716", // Identifiers should not match keywords
            "CA1717", // Only FlagsAttribute enums should have plural names
            "CA1720", // Identifiers should not contain type names
            "CA1721", // Property names should not match get methods
            "CA1722", // Identifiers should not have incorrect prefix
            "CA1724", // Type names should not match namespaces
            "CA1725", // Parameter names should match base declaration
            "CA1726", // Use preferred terms
        ];

        /// <summary>
        /// Проблемы документации
        /// </summary>
        public static readonly string[] Documentation =
        [
            "CA1200", // Avoid using cref tags with a prefix
            "CS1591", // Missing XML comment for publicly visible type or member
            "CS1573", // Parameter has no matching param tag in XML comment
            "CS1572", // XML comment has a param tag for which there is no parameter
            "CS1574", // XML comment has cref attribute that could not be resolved
        ];

        /// <summary>
        /// Проблемы логирования
        /// </summary>
        public static readonly string[] Logging =
        [
            "CA1848", // Use LoggerMessage delegates
            "CA1849", // Call async methods when in an async method
            "CA1850", // Prefer static HashData method over ComputeHash
            "CA1852", // Seal internal types
            "CA1853", // Unnecessary call to Dictionary.ContainsKey
            "CA1854", // Prefer Dictionary.TryGetValue
            "CA1855", // Use Span<T>.Clear()
            "CA1856", // Incorrect usage of ConstantExpected attribute
            "CA1857", // The parameter expects a constant for optimal performance
            "CA1858", // Use StartsWith instead of IndexOf
            "CA1859", // Use concrete types when possible for improved performance
            "CA1860", // Avoid using Enumerable.Any() extension method
            "CA1861", // Avoid constant arrays as arguments
            "CA1862", // Prefer using StringComparison
            "CA1863", // Use char literal instead of string literal
            "CA1864", // Prefer the IDictionary.TryAdd(TKey, TValue) method
            "CA1865", // Use char overload
            "CA1866", // Use char overload
            "CA1867", // Use char overload
            "CA1868", // Unnecessary call to Contains for sets
            "CA1869", // Cache and reuse JsonSerializerOptions instances
            "CA1870", // Use cached SearchValues instance
            "CA1871", // Do not pass a cached SearchValues instance to multiple simultaneous operations
            "CA1872", // Prefer Convert.ToHexString
            "CA1873", // Avoid conditional access expressions in parameters
        ];
    }

    /// <summary>
    /// Наборы правил по приоритету
    /// </summary>
    public static class Priority
    {
        /// <summary>
        /// Критические проблемы (ошибки + безопасность)
        /// </summary>
        public static readonly string[] Critical = Categories.Security;

        /// <summary>
        /// Высокий приоритет (надежность + некоторые performance)
        /// </summary>
        public static readonly string[] High =
        [
            ..Categories.Reliability,
            "CA1860", // Performance: Avoid Any()
            "CA1854", // Performance: TryGetValue
            "CA1869", // Performance: Cache JsonSerializerOptions
        ];

        /// <summary>
        /// Средний приоритет (производительность + поддержка)
        /// </summary>
        public static readonly string[] Medium =
        [
            ..Categories.Performance,
            ..Categories.Maintainability,
        ];

        /// <summary>
        /// Низкий приоритет (стиль кода, именование)
        /// </summary>
        public static readonly string[] Low =
        [
            ..Categories.Naming,
            ..Categories.Documentation,
            "CA1822", // Member can be static
        ];
    }

    /// <summary>
    /// Получить коды правил по имени preset
    /// </summary>
    public static string[]? GetPreset(string presetName)
    {
        return presetName?.ToLowerInvariant() switch
        {
            // Categories
            "performance" => Categories.Performance,
            "maintainability" => Categories.Maintainability,
            "security" => Categories.Security,
            "reliability" => Categories.Reliability,
            "usage" => Categories.Usage,
            "design" => Categories.Design,
            "globalization" => Categories.Globalization,
            "naming" => Categories.Naming,
            "documentation" => Categories.Documentation,
            "logging" => Categories.Logging,

            // Priority
            "critical" => Priority.Critical,
            "high" => Priority.High,
            "medium" => Priority.Medium,
            "low" => Priority.Low,

            _ => null,
        };
    }

    /// <summary>
    /// Получить все доступные preset имена
    /// </summary>
    public static string[] GetAvailablePresets()
    {
        return
        [
            // Categories
            "performance",
            "maintainability",
            "security",
            "reliability",
            "usage",
            "design",
            "globalization",
            "naming",
            "documentation",
            "logging",

            // Priority
            "critical",
            "high",
            "medium",
            "low",
        ];
    }
}
