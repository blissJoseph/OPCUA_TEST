using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;



namespace OPCUA_TEST
{

    public partial class MainWindow : Window
    {
        private Session _session;

        public MainWindow()
        {
            InitializeComponent();
            EnsureCertificateFolders();
        }


        // Create a certificate storage directory when the program starts.
        private void EnsureCertificateFolders()
        {
            Directory.CreateDirectory("Certificates/Own");
            Directory.CreateDirectory("Certificates/TrustedPeer");
            Directory.CreateDirectory("Certificates/TrustedIssuer");
            Directory.CreateDirectory("Certificates/Rejected");
        }

        // Connection button to server
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new ApplicationConfiguration()
                {
                    ApplicationName = "SimpleOpcUaClient",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        // Where the client stores the certificate for their own use
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/Own",
                            SubjectName = "CN=SimpleOpcUaClient"
                        },

                        // Where to store trusted server certificates
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/TrustedPeer"
                        },

                        // Where to store certificate issuers
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/TrustedIssuer"
                        },

                        // Where to store rejected certificates
                        RejectedCertificateStore = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/Rejected"
                        },

                        // Reject untrusted certificates by default (false)
                        AutoAcceptUntrustedCertificates = false
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000
                    }
                };

                // Code to verify that the config setting is correct
                await config.Validate(ApplicationType.Client);

                // Makes it possible to ignore server certificate errors and still connect
                config.CertificateValidator.CertificateValidation += (s, e2) =>
                {
                    if (e2.Error.StatusCode == StatusCodes.BadCertificateUntrusted ||
                        e2.Error.StatusCode == StatusCodes.BadCertificateTimeInvalid)
                    {
                        e2.Accept = true;
                    }
                };

                string serverUrl = ServerUrlBox.Text.Trim();

                // Select the communication path (Endpoint) to use to connect to the server.
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(serverUrl, useSecurity: false);

                // Create a ConfiguredEndpoint object to actually communicate with the server
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config));


                var userIdentity = new UserIdentity("TEST", "testtest");


                // Create an actual connection (Session) with the server.
                _session = await Session.Create(config, endpoint, false, "SimpleOpcUaClient", 60000, userIdentity, null);

                ConnectionStatus.Content = "Connected!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection Failed: {ex.Message}");
            }
        }


        // Read button from server data
        private async void ReadNodesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || !_session.Connected)
            {
                MessageBox.Show("Not connected to server.");
                return;
            }

            try
            {
                string[] nodeIdStrings = NodeIdsBox.Text
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (nodeIdStrings.Length == 0)
                {
                    MessageBox.Show("Please enter at least one NodeId.");
                    return;
                }

                ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
                foreach (var nodeIdStr in nodeIdStrings)
                {
                    nodesToRead.Add(new ReadValueId
                    {
                        NodeId = new NodeId(nodeIdStr.Trim()),
                        AttributeId = Attributes.Value
                    });
                }

                // Add CancellationToken 
                var response = await _session.ReadAsync(
                    null,
                    0,
                    TimestampsToReturn.Neither,
                    nodesToRead,
                    CancellationToken.None
                );

                NodeValuesText.Text = "";
                for (int i = 0; i < nodesToRead.Count; i++)
                {
                    var result = response.Results[i];
                    if (StatusCode.IsGood(result.StatusCode))
                    {
                        NodeValuesText.Text += $"{nodesToRead[i].NodeId}: {result.Value}\n";
                    }
                    else
                    {
                        NodeValuesText.Text += $"{nodesToRead[i].NodeId}: Error ({result.StatusCode})\n";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Read Failed: {ex.Message}");
            }
        }
    }
}
