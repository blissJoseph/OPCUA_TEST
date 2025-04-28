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
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/Own",
                            SubjectName = "CN=SimpleOpcUaClient"
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/TrustedPeer"
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/TrustedIssuer"
                        },
                        RejectedCertificateStore = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/Rejected"
                        },
                        AutoAcceptUntrustedCertificates = false
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000
                    }
                };

                await config.Validate(ApplicationType.Client);

                config.CertificateValidator.CertificateValidation += (s, e2) =>
                {
                    if (e2.Error.StatusCode == StatusCodes.BadCertificateUntrusted ||
                        e2.Error.StatusCode == StatusCodes.BadCertificateTimeInvalid)
                    {
                        e2.Accept = true;
                    }
                };

                //  Server address
                string serverUrl = ServerUrlBox.Text.Trim();
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(serverUrl, useSecurity: false);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config));

                // User ID and PW
                string username = UsernameBox.Text.Trim();
                string password = PasswordBox.Password.Trim();

                // Create UserIdentity 
                var userIdentity = new UserIdentity(username, password);

                // Conntect session
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
