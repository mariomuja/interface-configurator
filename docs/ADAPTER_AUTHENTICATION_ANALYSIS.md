# Adapter Authentifizierungs- und Security-Analyse

## Zusammenfassung

Diese Analyse identifiziert universelle Authentifizierungs- und Security-Methoden für Adapter, um sie universell verwendbar zu machen, Code-Duplikation zu vermeiden und die Benutzerfreundlichkeit zu verbessern.

## 1. Aktuelle Authentifizierungsmethoden pro Adapter

### SQL Server Adapter
- **Windows Authentication (Integrated Security)**: Verwendet Windows-Credentials
- **SQL Authentication**: Username/Password
- **Azure SQL Managed Identity**: Über Resource Group (teilweise implementiert)

### SFTP Adapter
- **Password Authentication**: Username/Password
- **SSH Key Authentication**: Private Key (PEM-Format)

### Dynamics 365 Adapter
- **OAuth 2.0 Client Credentials Flow**: TenantId, ClientId, ClientSecret
- **Token Caching**: Bereits implementiert in HttpClientAdapterBase

### SAP Adapter
- **RFC Authentication**: Username/Password
- **OData/REST Authentication**: OAuth 2.0 (für S/4HANA)
- **Certificate Authentication**: (Noch nicht implementiert)

### CRM Adapter
- **Basic Authentication**: Username/Password (Legacy)
- **OAuth 2.0**: (Noch nicht vollständig implementiert)

## 2. Empfohlene universelle Authentifizierungsmethoden

### 2.1 Authentifizierungstypen (Authentication Types)

#### A. Credential-basierte Authentifizierung
1. **Username/Password**
   - Standard für SQL, SFTP, SAP RFC, CRM Basic Auth
   - Sollte verschlüsselt gespeichert werden (Azure Key Vault)

2. **API Key**
   - Für REST APIs ohne OAuth
   - Header-basiert (z.B. `X-API-Key`)

3. **Connection String**
   - Für SQL Server, Service Bus, etc.
   - Kann verschiedene Auth-Methoden enthalten

#### B. Token-basierte Authentifizierung
1. **OAuth 2.0 Client Credentials Flow**
   - Für Dynamics 365, SAP S/4HANA, moderne REST APIs
   - Automatisches Token-Refresh
   - Token-Caching

2. **OAuth 2.0 Authorization Code Flow**
   - Für Benutzer-interaktive Szenarien
   - Refresh Token Support

3. **Bearer Token**
   - Statisches Token (z.B. für APIs)
   - Mit optionalem Expiry

#### C. Certificate-basierte Authentifizierung
1. **Client Certificate (X.509)**
   - Für mTLS (mutual TLS)
   - SAP, einige REST APIs

2. **SSH Key**
   - Für SFTP, SSH-basierte Verbindungen
   - RSA, ECDSA, Ed25519 Support

#### D. Azure-spezifische Authentifizierung
1. **Managed Identity**
   - Für Azure SQL, Azure Storage, Azure Service Bus
   - Keine Credentials nötig

2. **Service Principal**
   - ClientId/ClientSecret für Azure Services
   - Ähnlich OAuth 2.0 Client Credentials

#### E. Windows-spezifische Authentifizierung
1. **Integrated Security**
   - Windows Authentication für SQL Server
   - Verwendet aktuellen Windows-User

2. **Kerberos**
   - Für Enterprise-Umgebungen
   - Single Sign-On

## 3. Was sollte in Basisklassen ausgelagert werden?

### 3.1 `AdapterBase` - Erweiterungen

#### A. Credential Management
```csharp
// Gemeinsame Credential-Struktur
public abstract class AdapterBase
{
    // Credential Properties (optional, je nach Auth-Type)
    protected readonly IAuthenticationProvider? _authProvider;
    
    // Credential Management Methods
    protected virtual Task<AuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken = default);
    protected virtual Task<bool> ValidateCredentialsAsync(CancellationToken cancellationToken = default);
    protected virtual Task RefreshCredentialsAsync(CancellationToken cancellationToken = default);
}
```

#### B. Secure Storage Integration
```csharp
// Integration mit Azure Key Vault oder ähnlichem
protected virtual Task<string> GetSecureCredentialAsync(string credentialName, CancellationToken cancellationToken = default);
protected virtual Task StoreSecureCredentialAsync(string credentialName, string value, CancellationToken cancellationToken = default);
```

### 3.2 Neue Basisklasse: `HttpClientAdapterBase` (bereits vorhanden, erweitern)

#### A. Token Management (bereits teilweise vorhanden)
```csharp
public abstract class HttpClientAdapterBase : AdapterBase
{
    // Token Caching (bereits vorhanden)
    private string? _cachedToken;
    private DateTime? _tokenExpiry;
    
    // Erweitern um:
    protected virtual Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    protected abstract Task<string> GetAccessTokenInternalAsync(CancellationToken cancellationToken = default);
    
    // Token Refresh Logic
    protected virtual Task RefreshTokenIfNeededAsync(CancellationToken cancellationToken = default);
    
    // Verschiedene OAuth Flows
    protected virtual Task<string> GetOAuth2ClientCredentialsTokenAsync(
        string tokenUrl, 
        string clientId, 
        string clientSecret, 
        string scope, 
        CancellationToken cancellationToken = default);
    
    protected virtual Task<string> GetOAuth2AuthorizationCodeTokenAsync(
        string tokenUrl,
        string authorizationCode,
        string redirectUri,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default);
}
```

#### B. HTTP Client Configuration
```csharp
// Certificate Handling
protected virtual void ConfigureHttpClientWithCertificate(X509Certificate2 certificate);
protected virtual void ConfigureHttpClientWithClientCertificate(string certificatePath, string certificatePassword);

// Proxy Support
protected virtual void ConfigureHttpClientProxy(string proxyUrl, string? username = null, string? password = null);

// Timeout Configuration
protected virtual void ConfigureHttpClientTimeouts(TimeSpan? connectTimeout, TimeSpan? requestTimeout);
```

### 3.3 Neue Basisklasse: `DatabaseAdapterBase` (für SQL-basierte Adapter)

```csharp
public abstract class DatabaseAdapterBase : AdapterBase
{
    // Connection String Management
    protected virtual string BuildConnectionString(DatabaseConnectionConfig config);
    protected virtual Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
    
    // Authentication Methods
    protected virtual string BuildSqlAuthConnectionString(string server, string database, string username, string password);
    protected virtual string BuildIntegratedSecurityConnectionString(string server, string database);
    protected virtual string BuildManagedIdentityConnectionString(string server, string database);
    
    // Connection Pooling
    protected virtual void ConfigureConnectionPooling(int minPoolSize, int maxPoolSize);
}
```

### 3.4 Neue Basisklasse: `SftpAdapterBase` (für SFTP-basierte Adapter)

```csharp
public abstract class SftpAdapterBase : AdapterBase
{
    // SSH Key Management
    protected virtual PrivateKeyFile LoadPrivateKeyFromString(string privateKeyContent, string? passphrase = null);
    protected virtual PrivateKeyFile LoadPrivateKeyFromFile(string keyFilePath, string? passphrase = null);
    
    // Connection Management
    protected virtual Task<SftpClient> CreateSftpClientAsync(CancellationToken cancellationToken = default);
    protected virtual Task<bool> TestSftpConnectionAsync(CancellationToken cancellationToken = default);
    
    // Key Algorithms Support
    protected virtual bool SupportsKeyAlgorithm(string algorithm); // RSA, ECDSA, Ed25519
}
```

## 4. Empfohlene Authentifizierungs-Konfiguration

### 4.1 Einheitliche Konfigurationsstruktur

```csharp
public class AdapterAuthenticationConfig
{
    public AuthenticationType AuthType { get; set; }
    
    // Username/Password
    public string? Username { get; set; }
    public string? Password { get; set; } // Encrypted
    
    // OAuth 2.0
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; } // Encrypted
    public string? Scope { get; set; }
    public string? TokenUrl { get; set; }
    
    // Certificate
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; } // Encrypted
    public string? CertificateThumbprint { get; set; } // For Azure Key Vault
    
    // SSH Key
    public string? SshKeyPath { get; set; }
    public string? SshKeyContent { get; set; } // Encrypted
    public string? SshKeyPassphrase { get; set; } // Encrypted
    
    // API Key
    public string? ApiKey { get; set; } // Encrypted
    public string? ApiKeyHeaderName { get; set; } // Default: "X-API-Key"
    
    // Bearer Token
    public string? BearerToken { get; set; } // Encrypted
    public DateTime? TokenExpiry { get; set; }
    
    // Connection String
    public string? ConnectionString { get; set; } // Encrypted
    
    // Azure Managed Identity
    public bool UseManagedIdentity { get; set; }
    public string? ManagedIdentityClientId { get; set; } // Optional, for User-Assigned Identity
    
    // Windows Integrated Security
    public bool UseIntegratedSecurity { get; set; }
    
    // Secure Storage
    public string? KeyVaultName { get; set; }
    public Dictionary<string, string> KeyVaultSecrets { get; set; } // Secret names mapped to property names
}

public enum AuthenticationType
{
    None,
    UsernamePassword,
    OAuth2ClientCredentials,
    OAuth2AuthorizationCode,
    ClientCertificate,
    SshKey,
    ApiKey,
    BearerToken,
    ConnectionString,
    ManagedIdentity,
    IntegratedSecurity,
    Kerberos
}
```

## 5. UI/UX-Empfehlungen für Settings-Dialoge

### 5.1 Einheitliche Authentifizierungs-Sektion

#### A. Authentication Type Selector (Dropdown/Radio Buttons)
```
┌─────────────────────────────────────────┐
│ Authentication Method:                 │
│ ○ None (No Authentication)            │
│ ○ Username / Password                  │
│ ○ OAuth 2.0 (Client Credentials)      │
│ ○ OAuth 2.0 (Authorization Code)      │
│ ○ Client Certificate                  │
│ ○ SSH Key                             │
│ ○ API Key                             │
│ ○ Bearer Token                        │
│ ○ Connection String                   │
│ ○ Azure Managed Identity              │
│ ○ Windows Integrated Security         │
└─────────────────────────────────────────┘
```

#### B. Dynamische Felder basierend auf Auswahl

**Username/Password:**
```
┌─────────────────────────────────────────┐
│ Username: [________________]           │
│ Password: [________________] [👁️]      │
│ ☑ Store password securely              │
└─────────────────────────────────────────┘
```

**OAuth 2.0 Client Credentials:**
```
┌─────────────────────────────────────────┐
│ Tenant ID: [________________]           │
│ Client ID: [________________]           │
│ Client Secret: [________________] [👁️] │
│ Scope: [________________]               │
│ Token URL: [________________]           │
│ ☑ Auto-refresh token                   │
│ ☑ Store credentials securely           │
└─────────────────────────────────────────┘
```

**Client Certificate:**
```
┌─────────────────────────────────────────┐
│ Certificate Source:                     │
│ ○ Upload File                           │
│ ○ Azure Key Vault                       │
│ ○ File Path                            │
│                                         │
│ [Choose File] certificate.pfx         │
│ Certificate Password: [________] [👁️] │
│ ☑ Store certificate securely           │
└─────────────────────────────────────────┘
```

**SSH Key:**
```
┌─────────────────────────────────────────┐
│ Key Source:                             │
│ ○ Upload File                           │
│ ○ Paste Key Content                     │
│ ○ File Path                            │
│                                         │
│ [Choose File] id_rsa                    │
│ Passphrase: [________] [👁️]            │
│ Key Type: [RSA ▼]                      │
│ ☑ Store key securely                   │
└─────────────────────────────────────────┘
```

**Azure Managed Identity:**
```
┌─────────────────────────────────────────┐
│ ☑ Use System-Assigned Managed Identity │
│                                         │
│ ○ Use User-Assigned Managed Identity    │
│   Client ID: [________________]        │
└─────────────────────────────────────────┘
```

### 5.2 Gemeinsame Komponenten

#### A. Secure Storage Indicator
```
┌─────────────────────────────────────────┐
│ 🔒 Credentials stored securely          │
│    Using: Azure Key Vault               │
│    [View Details] [Rotate Credentials]   │
└─────────────────────────────────────────┘
```

#### B. Test Connection Button
```
┌─────────────────────────────────────────┐
│ [🔍 Test Connection]                    │
│                                         │
│ Status: ✅ Connected                    │
│ Last tested: 2 minutes ago              │
└─────────────────────────────────────────┘
```

#### C. Credential Validation
```
┌─────────────────────────────────────────┐
│ ⚠️ Missing required fields:             │
│    • Client Secret                      │
│    • Scope                              │
└─────────────────────────────────────────┘
```

### 5.3 Adapter-spezifische Erweiterungen

#### SQL Server Adapter
```
┌─────────────────────────────────────────┐
│ Authentication Method:                  │
│ ○ Windows Integrated Security          │
│ ○ SQL Authentication                   │
│ ○ Azure Managed Identity               │
│ ○ Connection String                    │
│                                         │
│ [Dynamische Felder je nach Auswahl]    │
└─────────────────────────────────────────┘
```

#### SFTP Adapter
```
┌─────────────────────────────────────────┐
│ Authentication Method:                  │
│ ○ Password                              │
│ ○ SSH Key                               │
│                                         │
│ [Dynamische Felder je nach Auswahl]    │
└─────────────────────────────────────────┘
```

#### Dynamics 365 / CRM Adapter
```
┌─────────────────────────────────────────┐
│ Authentication Method:                  │
│ ○ OAuth 2.0 (Client Credentials)       │
│ ○ OAuth 2.0 (Authorization Code)       │
│                                         │
│ [Dynamische Felder je nach Auswahl]    │
│                                         │
│ ☑ Enable token caching                 │
│ ☑ Auto-refresh expired tokens          │
└─────────────────────────────────────────┘
```

## 6. Implementierungsreihenfolge

### Phase 1: Basis-Infrastruktur
1. `AdapterAuthenticationConfig` Klasse erstellen
2. `AuthenticationType` Enum erstellen
3. Secure Storage Service (Azure Key Vault Integration)
4. Credential Encryption/Decryption Utilities

### Phase 2: Basisklassen erweitern
1. `AdapterBase` um Credential Management erweitern
2. `HttpClientAdapterBase` Token Management verbessern
3. `DatabaseAdapterBase` erstellen (für SQL Server)
4. `SftpAdapterBase` erstellen (für SFTP)

### Phase 3: UI-Komponenten
1. Gemeinsame Authentication-Selector Komponente
2. Dynamische Credential-Felder Komponente
3. Secure Storage Indicator Komponente
4. Test Connection Komponente

### Phase 4: Adapter-Migration
1. SQL Server Adapter migrieren
2. SFTP Adapter migrieren
3. Dynamics 365 Adapter migrieren
4. SAP Adapter migrieren
5. CRM Adapter migrieren

## 7. Security Best Practices

### 7.1 Credential Storage
- **Nie im Klartext speichern**: Alle Passwords, Secrets, Keys verschlüsselt
- **Azure Key Vault**: Für Production-Umgebungen verwenden
- **Environment Variables**: Für Development (nicht für Production)
- **Rotation Support**: Mechanismen für Credential-Rotation

### 7.2 Token Management
- **Token Caching**: Mit Expiry-Tracking
- **Automatic Refresh**: Vor Ablauf erneuern
- **Secure Storage**: Tokens verschlüsselt speichern

### 7.3 Certificate Management
- **Secure Upload**: Certificates verschlüsselt übertragen
- **Key Vault Integration**: Für Production
- **Password Protection**: Certificate-Passwords verschlüsselt

### 7.4 Connection Security
- **TLS/SSL**: Immer für externe Verbindungen
- **Certificate Validation**: Server-Zertifikate validieren
- **Encrypted Connections**: Für alle Netzwerk-Verbindungen

## 8. Code-Duplikation vermeiden

### 8.1 Gemeinsame Utilities
```csharp
// Credential Encryption
public static class CredentialEncryption
{
    public static string Encrypt(string plainText, string key);
    public static string Decrypt(string encryptedText, string key);
}

// Token Management
public static class TokenManager
{
    public static Task<string> GetCachedTokenAsync(string key, Func<Task<string>> refreshFunc);
    public static Task RefreshTokenAsync(string key);
}

// Connection Testing
public static class ConnectionTester
{
    public static Task<bool> TestDatabaseConnectionAsync(string connectionString);
    public static Task<bool> TestHttpConnectionAsync(string url, AuthenticationConfig auth);
    public static Task<bool> TestSftpConnectionAsync(string host, int port, AuthenticationConfig auth);
}
```

### 8.2 Factory Pattern für Authentication
```csharp
public interface IAuthenticationProvider
{
    Task<AuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(CancellationToken cancellationToken = default);
}

public class AuthenticationProviderFactory
{
    public static IAuthenticationProvider Create(AuthenticationType type, AdapterAuthenticationConfig config)
    {
        return type switch
        {
            AuthenticationType.UsernamePassword => new UsernamePasswordAuthProvider(config),
            AuthenticationType.OAuth2ClientCredentials => new OAuth2ClientCredentialsProvider(config),
            AuthenticationType.ClientCertificate => new ClientCertificateAuthProvider(config),
            // ...
        };
    }
}
```

## 9. Zusammenfassung der Empfehlungen

### Für universelle Verwendbarkeit:
1. ✅ Einheitliche `AdapterAuthenticationConfig` Struktur
2. ✅ Unterstützung aller gängigen Auth-Methoden
3. ✅ Secure Storage Integration (Azure Key Vault)
4. ✅ Token Management mit Auto-Refresh
5. ✅ Certificate Support (Client Certificates, SSH Keys)

### Für Code-Duplikation vermeiden:
1. ✅ `AdapterBase` um Credential Management erweitern
2. ✅ `HttpClientAdapterBase` Token Management verbessern
3. ✅ Neue Basisklassen: `DatabaseAdapterBase`, `SftpAdapterBase`
4. ✅ Gemeinsame Utilities: Encryption, Token Management, Connection Testing
5. ✅ Factory Pattern für Authentication Providers

### Für UI/UX:
1. ✅ Einheitlicher Authentication Type Selector
2. ✅ Dynamische Felder basierend auf Auswahl
3. ✅ Secure Storage Indicator
4. ✅ Test Connection Funktionalität
5. ✅ Validierung und Fehleranzeige
6. ✅ Adapter-spezifische Erweiterungen wo nötig

## 10. Nächste Schritte

1. **Design Review**: Diese Analyse mit dem Team besprechen
2. **Prototyp**: Gemeinsame Authentication-Komponente erstellen
3. **Migration Plan**: Schrittweise Migration der bestehenden Adapter
4. **Testing**: Umfassende Tests für alle Auth-Methoden
5. **Documentation**: Benutzer-Dokumentation für jede Auth-Methode

