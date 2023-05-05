using Cassia;
using System.Diagnostics;
using System.Net.NetworkInformation;
using RestSharp;
using Newtonsoft.Json;
using System.Net;
using System.ServiceProcess;
using System.Configuration;

namespace SNMP_TEST
{
    internal class citrix_server
    {
        //attributes
        const string Epic_Citrix_HyperSpace_Server = "Epic Citrix Hyperspace Server";
        const string Epic_Citrix_WebEAD_Server = "Epic Citrix WebEAD Server";
        const string desktopServiceName = "Citrix Desktop Service";
        const string EventLogSource = "Application";
        const string EventLogName = "Application";

        public string citrix_server_name { get; set; }
        public static string Citrix_Cloud_API_Id_Value;
        public static string Citrix_Cloud_API_Secret_Value;
        public static string Citrix_Cloud_Customer_Id_Value;
        public static string Citrix_Cloud_Site_ID_Value;
        public static string Citrix_Cloud_Bearer_Token;

        protected static void SetCloudAPIIDValue(string value) => Citrix_Cloud_API_Id_Value = value;

        protected static void SetCloudAPISecretValue(string value) => Citrix_Cloud_API_Secret_Value = value;

        protected static void SetCloudCustomerIdValue(string value) => Citrix_Cloud_Customer_Id_Value = value;

        protected static void SetCloudSiteIdValue(string value) => Citrix_Cloud_Site_ID_Value = value;

        protected static void SetCloudBearerToken(string value) => Citrix_Cloud_Bearer_Token = value;

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        public class TokenRoot
        {
            public string token_type { get; set; }
            public string access_token { get; set; }
            public string expires_in { get; set; }
        }

        public citrix_server()
        {
            CloudAPIData cad = new CloudAPIData();
        }

        public class CloudAPIData
        {
            public string Citrix_Cloud_API_Id_Value { get; set; }
            public string Citrix_Cloud_API_Secret_Value { get; set; }
            public string Citrix_Cloud_Customer_Id_Value { get; set; }
            public string Citrix_Cloud_Site_ID_Value { get; set; }
            public string Citrix_Cloud_Bearer_Token { get; set; }

        }



        public bool PingHost ()
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(citrix_server_name);
                pingable = reply.Status == IPStatus.Success;
            } catch (PingException)
            {
                // 
                WriteToEventLog(EventLogSource, string.Format("Unable to Ping make sure the server is online.", citrix_server_name),"Ping Failed");

            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }

            return pingable;
        }

        public void RestartDesktopService()
        {
            if (PingHost())
            {
                StopService(desktopServiceName);
                // Wait for Service to stop

                do
                {
                    // Just sit here and wait till the service is stopped.
                } while (serviceStatus(desktopServiceName, 0));

                StartService(desktopServiceName);
                LogWriter log = new LogWriter();
                log.WriteLog(citrix_server_name, "Restarted Desktop Service", Epic_Citrix_HyperSpace_Server);
            }
            else
            {

                EventLog eventLog = new EventLog(EventLogName);
                eventLog.Source = EventLogSource;
                eventLog.WriteEntry(string.Format("{0} is not respinding to ping.", citrix_server_name));

                return;
            }
        }

        public bool serviceStatus (string ServiceName, int requestedStatus)
        {

            try
            {

                ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
                if (requestedStatus == 1)
                {
                    sc.WaitForStatus(ServiceControllerStatus.Running);
                }
                else
                {
                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                }
            }
            catch (Exception e)
            {
                EventLog eventLog = new EventLog(EventLogName);
                eventLog.Source = EventLogSource;
                eventLog.WriteEntry(string.Format("Error getting the status of the {0}\n Error message {1}", ServiceName, e.Message));
            }
            return false;
        }

        protected void StopService (string ServiceName)
        {
            try
            {

                ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    try
                    {
                        sc.Stop();
                    }
                    catch (Exception e)
                    {
                        EventLog eventLog = new EventLog(EventLogName);
                        eventLog.Source = EventLogSource;
                        eventLog.WriteEntry(string.Format("Error stopping service {0} on server {1}.\n {2}", ServiceName, citrix_server_name, e.Message));
                    }


                }
            }
            catch (Exception e)
            {
                WriteToEventLog(EventLogSource, "Error connecting to machine.", e.Message);               
            }
        }
        
        protected void StartService (string ServiceName)
        {
            try
            {
                ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    try
                    {
                        sc.Start();
                    }
                    catch (Exception e)
                    {
                        EventLog eventLog = new EventLog("Application");
                        eventLog.Source = EventLogSource;
                        eventLog.WriteEntry(string.Format("Error starting service {0} on server {1}.\n {2}", ServiceName, citrix_server_name, e.Message));
                    }
                }
            }
            catch (Exception e)
            {
                WriteToEventLog(EventLogSource, "Error connecting to machine.", e.Message);
            }
        }

        // This seemed to either fail or take forever to get a response.  
        // Not even shure this is necessary.
        public int GetDisconnectedSesssion()
        {
            int disconnectedCount = 0;
            ITerminalServicesManager manager = new TerminalServicesManager();
            using (ITerminalServer server = manager.GetRemoteServer(citrix_server_name))
            {
                server.Open();
                foreach(ITerminalServicesSession session in server.GetSessions())
                {
                    if(session.ConnectionState == ConnectionState.Disconnected)
                    {
                        disconnectedCount ++;
                    }
                }
            }
            return disconnectedCount;
        }

        public async void shutdownCitrixServer()
        {

            if(citrix_server.Citrix_Cloud_Customer_Id_Value == null || citrix_server.Citrix_Cloud_Site_ID_Value == null)
            {
                WriteToEventLog(EventLogSource, string.Format("Did not have needed Citrix Cloud Info.\nCustomer ID {0}\nCloud Site ID {1}",
                    citrix_server.Citrix_Cloud_Customer_Id_Value,
                    citrix_server.Citrix_Cloud_Site_ID_Value),
                    "Going to get the needed info.");
                GetAzureSecrets();
            }

            if (!TestBearerToken())
            {
                WriteToEventLog(EventLogSource, "New to get a new Bearer Token from Citrix.", string.Format("The old token was {0}",citrix_server.Citrix_Cloud_Bearer_Token));
                GetDaaSBearerToken();
            }

            string URL = String.Format("https://api-us.cloud.com/cvad/manage/Machines/{0}.slhn.org/{1}", citrix_server_name, "$shutdown");

            try
            {
                var client = new RestClient();
                var request = new RestRequest(URL, Method.Post);
                request.AddHeader("Citrix-CustomerId", Citrix_Cloud_Customer_Id_Value);
                request.AddHeader("Citrix-InstanceId", Citrix_Cloud_Site_ID_Value);
                request.AddHeader("Authorization", string.Format("CwsAuth Bearer={0}", Citrix_Cloud_Bearer_Token));
                RestResponse response = await client.ExecuteAsync(request);

                LogWriter logWriter = new LogWriter();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    logWriter.WriteLog(citrix_server_name, "Shutdown", Epic_Citrix_HyperSpace_Server);
                } else
                {
                    logWriter.WriteLog(citrix_server_name, "Unable to Shutdown Citrix Server. Status Code " + response.StatusCode.ToString(), Epic_Citrix_HyperSpace_Server);
                }
            }
            catch (Exception ex)
            {
                WriteToEventLog(EventLogSource, "Unable to Shutdown citrix server via Cloud API", ex.Message);
                
            }
        }

        private static void GetDaaSBearerToken()
        {

            var client = new RestClient();
            var request = new RestRequest("https://api-us.cloud.com/cctrustoauth2/root/tokens/clients", Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", Citrix_Cloud_API_Id_Value);
            request.AddParameter("client_secret", Citrix_Cloud_API_Secret_Value);
            RestResponse response = client.Execute(request);
            TokenRoot bearerToken = null;
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    bearerToken = JsonConvert.DeserializeObject<TokenRoot>(response.Content);
                }
                catch (Exception ex)
                {
                    citrix_server writeevent = new citrix_server();
                    writeevent.WriteToEventLog(EventLogSource, string.Format("Unable to convert the response to Json object\n{0}", response.Content), ex.Message);

                }

                if (bearerToken != null && response.StatusCode == HttpStatusCode.OK)
                {
                    SetCloudBearerToken(bearerToken.access_token);
                }
                else
                {
                    EventLog eventLog = new EventLog("Application");
                    eventLog.Source = EventLogSource;
                    eventLog.WriteEntry(string.Format("Error getting the Bearer Token with {0}\n{1}\n{2}",
                        Citrix_Cloud_API_Id_Value,
                        Citrix_Cloud_API_Secret_Value));
                    return;
                } 
            } else
            {
                EventLog eventLog = new EventLog(EventLogName);
                eventLog.Source = EventLogSource;
                eventLog.WriteEntry(string.Format("Unable to get Bearer Token with Status code: {0}", response.StatusCode.ToString()));
                return;
            }
            //Console.WriteLine(response.Content);
        }

        private bool TestBearerToken()
        {
            string URL = "https://api-us.cloud.com/cvad/manage/About";
            var client = new RestClient();
            var request = new RestRequest(URL, Method.Get);
            request.AddHeader("Citrix-CustomerId", Citrix_Cloud_Customer_Id_Value);
            request.AddHeader("Citrix-InstanceId", Citrix_Cloud_Site_ID_Value);
            request.AddHeader("Authorization", string.Format("CwsAuth Bearer={0}", Citrix_Cloud_Bearer_Token));
            RestResponse response = client.Execute(request);
            if (response != null)
            {
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                } else { 
                    return true;
                }
            } else
            {
                return false;
            }
        }

        public static void GetAzureSecrets()
        {
            const string secretName_Citrix_Cloud_API_ID = "Citrix-Cloud-API-ID";
            const string secretName_Citrix_Cloud_API_Secret = "Citrix-Cloud-API-Secret";
            const string secretName_Citrix_Cloud_Customer_Id = "Citrix-Cloud-Customer-Id";
            const string secretName_Citrix_Cloud_Site_ID = "Citrix-Cloud-Site-ID";

            SetCloudAPIIDValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_API_ID]);
            SetCloudAPISecretValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_API_Secret]);
            SetCloudCustomerIdValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_Customer_Id]);
            SetCloudSiteIdValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_Site_ID]);

            // This is not working at the moment, leaving this for now until i can figure out how to get it working properly.  
            //const string keyVaultName = "KV-SLUHNPROD-Automation";
            //string kvUri = "https://" + keyVaultName + ".vault.azure.net";

            // Get the secret info to receive the bearer token
            // Generate the connection to the secret vault
            //var creds = new ManagedIdentityCredential("51f45e34-c525-4099-b325-49f711f6c5e9");
            //var creds = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = "51f45e34-c525-4099-b325-49f711f6c5e9" });
            //var token = creds.GetToken(
            //    new Azure.Core.TokenRequestContext(
            //        new[] { "https://vault.azure.net/.default" }));
            //try
            //{

            //    //var client = new SecretClient(new Uri(kvUri), creds);


            //    var secretCloudAPIID = client.GetSecret(secretName_Citrix_Cloud_API_ID);
            //    var secretCloudAPISecret = client.GetSecret(secretName_Citrix_Cloud_API_Secret);
            //    var secretCloudCustomerID = client.GetSecret(secretName_Citrix_Cloud_Customer_Id);
            //    var secretCloudSiteID = client.GetSecret(secretName_Citrix_Cloud_Site_ID);
            //    if (secretCloudAPIID != null)
            //    {
            //        SetCloudAPIIDValue(secretCloudAPIID.Value.Value.ToString());
            //    }
            //    if (secretCloudAPISecret != null)
            //    {
            //        SetCloudAPISecretValue(secretCloudAPISecret.Value.Value.ToString());
            //        //Citrix_Cloud_API_Secret_Value = secretCloudAPISecret.Value.Value.ToString(); 
            //    }
            //    if (secretCloudCustomerID != null)
            //    {
            //        SetCloudCustomerIdValue(secretCloudCustomerID.Value.Value.ToString());
            //        //Citrix_Cloud_Customer_Id_Value = secretCloudCustomerID.Value.Value.ToString(); 
            //    }
            //    if (secretCloudSiteID != null)
            //    {
            //        SetCloudSiteIdValue(secretCloudSiteID.Value.Value.ToString());
            //        //Citrix_Cloud_Site_ID_Value = secretCloudSiteID.Value.Value.ToString(); 
            //    }

            //    //GetDaaSBearerToken();
            //}
            //catch (Exception e)
            //{
            //    EventLog eventLog = new EventLog("Application");
            //    eventLog.Source = "SLUHNTrapperKeeper";
            //    eventLog.WriteEntry(string.Format("Error getting Secrets from the KeyVault with message {0}", e.Message, creds.ToString()));
            //}


        }

        private void WriteToEventLog(string Source, string Message, string ExceptionMessage)
        {
            EventLog eventLog = new EventLog(EventLogName);
            eventLog.Source = Source;
            eventLog.WriteEntry(string.Format("{0} with exception {1}", Message, ExceptionMessage));

        }
    }
}
