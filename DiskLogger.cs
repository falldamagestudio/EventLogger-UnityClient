using System;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

namespace EventLogger
{
    public class DiskLogger
    {
        /// <summary>
        /// What to do if the the log file already exists when DiskLogger starts
        /// </summary>
        public enum ExistingFileMode
        {
            /// <summary>
            /// Replace the existing log file with the new log file
            /// </summary>
            Overwrite,
            /// <summary>
            /// Add a suffix to the log file name, which makes the new file name unique
            /// </summary>
            DoNotOverwrite
        };

        [Serializable]
        public class Configuration
        {
            public string FileLogName = "Events.log.txt";
            public ExistingFileMode ExistingFileHandling = ExistingFileMode.DoNotOverwrite;
        }

        private readonly Configuration config;
        private readonly StreamWriter LogFileWriter;

        public DiskLogger(Configuration config)
        {
            this.config = config;
            Assert.IsNotNull(config);

            string path = Path.Combine(Application.persistentDataPath, config.FileLogName);

            string fileLogPathWithCount = path;
            if (config.ExistingFileHandling != ExistingFileMode.Overwrite)
            {
                // Look for the first nonexistent suitable file name 
                // Note, this poses a race condition if starting two clients on the same machine
                int suffixCounter = 0;
                while (System.IO.File.Exists(fileLogPathWithCount))
                {
                    suffixCounter++;
                    fileLogPathWithCount = path + "." + suffixCounter;
                }
            }

            Debug.LogFormat("DiskLogger initialized. Events will be logged to {0}", fileLogPathWithCount);
            LogFileWriter = new StreamWriter(fileLogPathWithCount, false);
            LogFileWriter.AutoFlush = true;
        }

        public void Log(string jsonEvent)
        {
            LogFileWriter.WriteLine(jsonEvent);
        }
    }
}
