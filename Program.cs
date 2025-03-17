using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Org.BouncyCastle.Tls;

class Program
{
    private static Session _session;
    private static string _serverUrl = "opc.tcp://localhost:62541/Quickstarts/ReferenceServer"; // Server-Adresse anpassen
    private static ApplicationConfiguration _config;

    static void Main()
    {
        _config = CreateApplicationConfiguration();
        ConnectToServer();

        // Halte das Programm am Laufen
        Console.ReadLine();
    }

    static ApplicationConfiguration CreateApplicationConfiguration()
    {
        return new ApplicationConfiguration
        {
            ApplicationName = "OPCClient",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier(),
                AutoAcceptUntrustedCertificates = true
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 5000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };
    }

    static void ConnectToServer()
    {
        #region list endpoints
        System.Uri uri = new(_serverUrl);
        using (var client = DiscoveryClient.Create(uri))
        {
            var endpoints = client.GetEndpoints(null);
            foreach (var ep in endpoints)
            {
                Console.WriteLine($"Endpoint: {ep.EndpointUrl}, SecurityMode: {ep.SecurityMode}, Policy: {ep.SecurityPolicyUri}, Auth: {string.Join(", ", ep.UserIdentityTokens.Select(t => t.TokenType))}");
            }
        }
        #endregion
        try
        {
            string certPath = "certificate.pfx";
            string certPass = "";
            var certificate = new X509Certificate2(certPath, certPass);
            UserIdentity userIdentity = new UserIdentity(certificate);
            var endpointConfiguration = EndpointConfiguration.Create(_config);
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(_serverUrl, useSecurity: true);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            _session = Session.Create(
                _config,
                endpoint,
                false,
                false,
                "OPCClient",
                60000,
                userIdentity,
                null
            ).Result;

            _session.KeepAlive += OnKeepAlive;
            Console.WriteLine("Verbindung zum OPC UA Server hergestellt.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Verbinden: {ex.Message}");
            RetryConnection();
        }
    }

    static void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (!ServiceResult.IsGood(e.Status))
        {
            Console.WriteLine("Verbindung verloren. Versuche erneut zu verbinden...");
            RetryConnection();
        }
    }

    static void RetryConnection()
    {
        _session?.Dispose();
        _session = null;

        Thread.Sleep(5000); // Wartezeit vor dem erneuten Verbindungsversuch
        ConnectToServer();
    }
}
