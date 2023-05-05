using System.Configuration;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SNMP_TEST
{
    internal class LogWriter
    {
        private static string log_file_location;

        private string logFilePath;
        private static List<data> CurrentJsonFile;

        protected class data
        {
            public string ServerName { get; set; }
            public string DateTime { get; set; }
            public string Action { get; set; }
            public string ServerType { get; set; }
        }


        public LogWriter() {
            log_file_location = ConfigurationManager.Appsettings["log_file_location"];
            SetLogFilePath();
        }

        // Not sure if I need to have a seperate constructor.
        //public LogWriter(string name) { }

        private void SetLogFilePath()
        {
            logFilePath = Path.Combine(log_file_location, DateTime.Now.ToString("yyyyMMdd") + ".json");
            //var commonPath = GetFolderPath(SpecialFolder.CommonApplicationData);
            if (!Directory.Exists(log_file_location))
            {
                Directory.CreateDirectory(log_file_location);
            }

            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Close();
                CurrentJsonFile = null;
                CurrentJsonFile = new List<data>();
            }
        }

        private bool CheckDate()
        {
            string Date = DateTime.Now.ToString("yyyyMMdd");
            return Path.GetFileNameWithoutExtension(logFilePath).EndsWith(Date) ;
        }

        public void ReadJsonLog()
        {
            try
            {
                string json = File.ReadAllText(logFilePath);
                if (json != null || json != "")
                {
                    CurrentJsonFile = JsonSerializer.Deserialize<List<data>>(json);
                }
            } catch (Exception ex)
            {
                //CurrentJsonFile = null;
                EventLog eventLog = new EventLog("Application");
                eventLog.Source = "Application";
                eventLog.WriteEntry(string.Format("Error deserializing the log file with error {0}.", ex.Message));
            }
        }


        public async void WriteLog(string serverName, string action, string serverType)
        {


            //List<data> _data = new List<data>();
            //_data.Add(new data()
            //{
            //    ServerName = serverName,
            //    DateTime = DateTime.Now.ToString(), 
            //    Action = action,
            //    ServerType = serverType
            //});

            if (!CheckDate()) { SetLogFilePath(); }

            try
            {
                if (CurrentJsonFile == null)
                {
                    CurrentJsonFile = new List<data> { };
                }
                CurrentJsonFile.Add(new data()
                {
                    ServerName = serverName,
                    DateTime = DateTime.Now.ToString(),
                    Action = action,
                    ServerType = serverType
                });
            }
            catch (Exception ex)
            {
                EventLog eventLog = new EventLog("Application");
                eventLog.Source = "Application";
                eventLog.WriteEntry("Unable to add the data to the json file: " + ex.Message);
            }

            string json = JsonSerializer.Serialize(CurrentJsonFile, 
                new JsonSerializerOptions{ 
                    WriteIndented = true
                });
            
            try
            {
                if (CurrentJsonFile != null)
                {
                    await File.WriteAllTextAsync(logFilePath, json);
                }
            }catch (Exception ex)
            {
                EventLog eventLog = new EventLog("Application");
                eventLog.Source = "SLUHNTrapperKeeper";
                eventLog.WriteEntry(string.Format("Unable to write to the current log file {0}, with error message {1}", logFilePath, ex.Message));

            }

        }
    }
}
